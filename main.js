const { app, BrowserWindow, Menu, ipcMain, dialog, shell, safeStorage } = require('electron');
const path = require('path');
const fs = require('fs');
const { atomicReplacePrepared, writeAtomicText, orderedTextStore, errorDetails } = require('./src/atomic-store');
const Security = require('./src/security');
const AIClient = require('./src/ai-client');
const FileExtensions = require('./src/file-extensions');

app.setName('KnotJot');

// 开发版放源码目录；便携版放 EXE 同级；安装版放 Electron 的 userData，避免 Program Files 写权限问题。
const textStylesDir = () => process.env.KNOTJOT_TEXT_STYLES_DIR || (
  !app.isPackaged
    ? path.join(__dirname, 'resources', 'text-styles')
    : path.join(process.env.PORTABLE_EXECUTABLE_DIR || path.join(app.getPath('appData'), 'KnotJot'), 'text-styles')
);
const textStylesFile = () => path.join(textStylesDir(), 'custom-text-styles.json');
const textStylesStore = orderedTextStore(textStylesFile);
const textStyleCandidates = () => [...new Set([textStylesFile(), ...(app.isPackaged ? [path.join(path.dirname(process.execPath),'text-styles','custom-text-styles.json')] : [])])];
let textStyleWatchStarted = false;
let textStylesRecoveryRequired = false;
let textStylesCorruptPath = '';
async function readTextStyles() {
  const found=[];for(const file of textStyleCandidates()){try{const st=await fs.promises.stat(file);found.push({file,mtime:st.mtimeMs});}catch(_){}}
  found.sort((a,b)=>b.mtime-a.mtime);
  for(const item of found){try{const json=JSON.parse(await fs.promises.readFile(item.file,'utf8'));if(!json||!['knotjot-text-styles','thoughtcanvas-text-styles'].includes(json.format)||Number(json.version)!==1||!Array.isArray(json.styles))throw new Error('format、version 或 styles 无效');const ids=new Set();for(const style of json.styles){const id=String(style&&style.id||'').trim().toLowerCase();if(!id||ids.has(id))throw new Error(!id?'样式缺少 ID':'样式 ID 重复：'+id);ids.add(id);}textStylesRecoveryRequired=false;textStylesCorruptPath='';return {ok:true,path:item.file,format:'knotjot-text-styles',version:1,revision:Number(json.revision)||0,writerId:String(json.writerId||''),defaultStyleId:json.defaultStyleId||'classic',styleSettings:json.styleSettings||{},styles:json.styles};}catch(err){
    const stamp=new Date().toISOString().replace(/[:.]/g,'-');textStylesCorruptPath=`${item.file}.${stamp}.corrupt`;
    await fs.promises.copyFile(item.file,textStylesCorruptPath).catch(()=>{textStylesCorruptPath=item.file;});textStylesRecoveryRequired=true;
    return {ok:false,path:item.file,defaultStyleId:'classic',styleSettings:{},styles:[],error:'文本框样式库损坏，已保留副本：'+textStylesCorruptPath,corruptPath:textStylesCorruptPath,requiresConfirmation:true};
  }}
  return {ok:false,path:textStylesFile(),defaultStyleId:'classic',styleSettings:{},styles:[],error:''};
}
function startTextStyleWatch() {
  if (textStyleWatchStarted) return;
  textStyleWatchStarted = true;
  fs.mkdirSync(path.dirname(textStylesFile()), { recursive: true });
  textStyleCandidates().forEach(file=>fs.watchFile(file,{interval:450},()=>{if(mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('text-styles-changed');}));
}

const FILTERS = [
  { name: 'KnotJot 思维导图', extensions: FileExtensions.mapExtensions.map(extension => extension.slice(1)) },
  { name: '所有文件', extensions: ['*'] }
];

let mainWin = null;
let pendingFile = null;   // 启动时通过双击文件传入、等待渲染层读取的路径
let allowClose = false;   // 是否已确认可以关闭

// 让安装版在 Windows 的任务栏、通知和文件关联中使用稳定的应用标识。
if (process.platform === 'win32') app.setAppUserModelId('io.github.h-cl-h.knotjot');

// 从命令行参数里找出 KnotJot 或旧版 BMAP 文件路径（双击文件时由系统传入）
function fileFromArgv(argv) {
  if (!argv) return null;
  const hit = argv.find(a => FileExtensions.isMapFile(a) && fs.existsSync(a));
  return hit || null;
}
async function readFileSafe(fp) {
  try { const st=await fs.promises.stat(fp);if(!st.isFile())throw new Error('目标不是文件');if(st.size>Security.LIMITS.documentBytes)throw Object.assign(new Error('思维导图超过 64 MB 上限'),{code:'FILE_TOO_LARGE'});return { ok: true, path: fp, name: path.basename(fp), content: await fs.promises.readFile(fp, 'utf8') }; }
  catch (err) { return { ok: false, error: err.message }; }
}

let saveSessionId = 0;
const saveSessions = new Map();
const saveSenderCleanupBound = new Set();

async function cleanupSaveArtifacts(s, closeHandle = true) {
  if (!s) return;
  if (closeHandle) await s.handle.close().catch(() => {});
  await fs.promises.unlink(s.tempPath).catch(() => {});
}
async function discardSaveSession(id) {
  const s = saveSessions.get(id);
  if (!s) return;
  saveSessions.delete(id);
  await cleanupSaveArtifacts(s);
}
async function discardSenderSaveSessions(senderId) {
  const ids = [...saveSessions].filter(([, s]) => s.senderId === senderId).map(([id]) => id);
  await Promise.all(ids.map(discardSaveSession));
}
function bindSaveSenderCleanup(sender) {
  const senderId = sender.id;
  if (saveSenderCleanupBound.has(senderId)) return;
  saveSenderCleanupBound.add(senderId);
  const cleanup = () => {
    saveSenderCleanupBound.delete(senderId);
    sender.removeListener('destroyed', cleanup);
    sender.removeListener('render-process-gone', cleanup);
    void discardSenderSaveSessions(senderId);
  };
  sender.once('destroyed', cleanup);
  sender.once('render-process-gone', cleanup);
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 760,
    minHeight: 520,
    backgroundColor: '#f5f6f8',
    title: 'KnotJot',
    icon: path.join(__dirname, 'build', 'icon.ico'),
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  Menu.setApplicationMenu(null);          // 去掉浏览器式菜单栏
  win.loadFile(path.join(__dirname, 'index.html'));
  win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  win.webContents.on('will-navigate', (event, url) => {
    if (url !== win.webContents.getURL()) event.preventDefault();
  });
  mainWin = win;
  startTextStyleWatch();
  startSkinWatch();
  void publishSkinLocation();

  // 关闭前：交给渲染层判断是否有未保存更改
  win.on('close', (e) => {
    if (allowClose) return;
    e.preventDefault();
    win.webContents.send('app-close-request');
  });
}

// 渲染层处理完“保存/不保存/取消”后回传结果
ipcMain.on('close-confirmed', (e, proceed) => {
  if (proceed && mainWin) { allowClose = true; mainWin.close(); }
});

// 大文件按画布分片写入：渲染层不再构造并跨 IPC 复制一份完整 JSON。
ipcMain.handle('save-begin', async (e, { filePath, defaultName }) => {
  let target = filePath;
  if (!target) {
    const r = await dialog.showSaveDialog(mainWin, {
      title: '另存为',
      defaultPath: (defaultName || '未命名思维导图') + FileExtensions.mapDefaultExtension,
      filters: FILTERS
    });
    if (r.canceled || !r.filePath) return { canceled: true };
    target = r.filePath;
  }
  const id = `${process.pid}-${++saveSessionId}`;
  const tempPath = `${target}.tmp-${id}`;
  const handle = await fs.promises.open(tempPath, 'w');
  saveSessions.set(id, { handle, tempPath, filePath: target, senderId: e.sender.id, position: 0 });
  bindSaveSenderCleanup(e.sender);
  return { canceled: false, id, path: target, name: path.basename(target) };
});

ipcMain.handle('save-chunk', async (e, { id, chunk }) => {
  const s = saveSessions.get(id);
  if (!s || s.senderId !== e.sender.id) throw new Error('保存会话无效');
  const buf = Buffer.from(String(chunk || ''), 'utf8');
  await s.handle.write(buf, 0, buf.length, s.position);
  s.position += buf.length;
  return { ok: true };
});

ipcMain.handle('save-end', async (e, { id, abort }) => {
  const s = saveSessions.get(id);
  if (!s || s.senderId !== e.sender.id) throw new Error('保存会话无效');
  saveSessions.delete(id);
  let closed = false;
  try {
    await s.handle.sync();
    await s.handle.close();
    closed = true;
    if (abort) { await fs.promises.unlink(s.tempPath).catch(()=>{});return { ok: false, canceled: true }; }
    const result=await atomicReplacePrepared(s.tempPath,s.filePath);
    if(!result.ok)return Object.assign(result,{name:path.basename(s.filePath)});
    return { ok: true, path: s.filePath, name: path.basename(s.filePath) };
  } catch(err){
    return errorDetails(err,{path:s.filePath,tempPath:s.tempPath,originalPreserved:true});
  } finally {
    if(!closed)await s.handle.close().catch(()=>{});
    if(abort)await fs.promises.unlink(s.tempPath).catch(()=>{});
  }
});

// 打开：选择文件并读取内容
ipcMain.handle('open', async () => {
  const { canceled, filePaths } = await dialog.showOpenDialog(mainWin, {
    title: '打开思维导图',
    properties: ['openFile'],
    filters: FILTERS
  });
  if (canceled || !filePaths.length) return { canceled: true };
  const filePath = filePaths[0];
  const result=await readFileSafe(filePath);return Object.assign({canceled:false},result);
});

// 按路径直接打开（用于“最近使用”）
ipcMain.handle('open-path', async (e, filePath) => {
  return readFileSafe(filePath);
});

// 导出图片 / PDF：选择保存位置并写入二进制
ipcMain.handle('export-save', async (e, { base64, defaultName, ext }) => {
  const names = { pdf: 'PDF 文档', jpg: 'JPEG 图片', png: 'PNG 图片' };
  const { canceled, filePath } = await dialog.showSaveDialog(mainWin, {
    title: '导出',
    defaultPath: (defaultName || '思维导图') + '.' + ext,
    filters: [{ name: names[ext] || ext, extensions: [ext] }]
  });
  if (canceled || !filePath) return { canceled: true };
  await fs.promises.writeFile(filePath, Buffer.from(base64, 'base64'));
  return { canceled: false, path: filePath };
});

// UI 皮肤持久化到 userData 下的文件（localStorage 万一被清也能恢复，导入后无需重复导入）
function loadSkinProtocol(){
  const candidates=[path.resolve(__dirname,'..','..','..','shared','ui-skin-protocol.json'),path.join(process.resourcesPath||'','protocols','ui-skin-protocol.json')];
  for(const file of candidates){try{return JSON.parse(fs.readFileSync(file,'utf8'));}catch(_){}}
  return {format:'knotjot-ui-skins',version:1,locationFormat:'knotjot-skins-location',idPattern:'^[A-Za-z0-9_-]{3,80}$',fileName:'ui-skins.json',applicationDataDirectory:'KnotJot',locationFileName:'skin-sync-location.json',legacyApplicationDataDirectories:['ThoughtCanvas'],limits:{libraryBytes:16*1024*1024,cssBytes:2*1024*1024,skinCount:500}};
}
const SKIN_PROTOCOL=loadSkinProtocol();
const skinsFile = () => process.env.KNOTJOT_UI_SKINS_FILE || path.join(app.getPath('userData'), SKIN_PROTOCOL.fileName);
const skinLocationFile=()=>path.join(path.dirname(skinsFile()),SKIN_PROTOCOL.locationFileName);
async function migrateLegacySkins(){
  try{await fs.promises.access(skinsFile());return;}catch(_){}
  for(const dir of SKIN_PROTOCOL.legacyApplicationDataDirectories||[]){const legacy=path.join(app.getPath('appData'),dir,SKIN_PROTOCOL.fileName);try{const text=await fs.promises.readFile(legacy,'utf8');const parsed=JSON.parse(text);if(parsed&&parsed.skins&&typeof parsed.skins==='object'){parsed.format=SKIN_PROTOCOL.format;parsed.version=SKIN_PROTOCOL.version;parsed.writerId='knotjot-migration';parsed.updatedUtc=new Date().toISOString();await writeAtomicText(skinsFile(),JSON.stringify(parsed,null,2));return;}}catch(_){}}
}
async function publishSkinLocation(){
  const payload={format:SKIN_PROTOCOL.locationFormat,version:1,file:skinsFile(),directory:path.dirname(skinsFile()),executable:process.execPath,sourceMain:path.join(__dirname,'main.js'),updatedUtc:new Date().toISOString()};
  await writeAtomicText(skinLocationFile(),JSON.stringify(payload,null,2)).catch(()=>{});
}
let skinWatchStarted=false;
function startSkinWatch(){
  if(skinWatchStarted)return;skinWatchStarted=true;
  fs.mkdirSync(path.dirname(skinsFile()),{recursive:true});
  void migrateLegacySkins().then(publishSkinLocation);
  fs.watchFile(skinsFile(),{interval:450},()=>{
    if(mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('ui-skins-changed');
  });
}
const skinsStore=orderedTextStore(skinsFile);
ipcMain.handle('skins-load', async () => { try { return await skinsStore.read(); } catch (err) { return null; } });
function validateSkinsJson(json){
  if(typeof json!=='string'||Buffer.byteLength(json,'utf8')>SKIN_PROTOCOL.limits.libraryBytes)throw new Error('UI 皮肤库超过 16 MB 上限');
  const o=JSON.parse(json);if(!o||typeof o!=='object'||!o.skins||typeof o.skins!=='object'||Array.isArray(o.skins))throw new Error('UI 皮肤库格式无效');
  if(o.format&&o.format!==SKIN_PROTOCOL.format)throw new Error('UI 皮肤库标识无效');o.format=SKIN_PROTOCOL.format;o.version=SKIN_PROTOCOL.version;
  const ids=Object.keys(o.skins);if(ids.length>SKIN_PROTOCOL.limits.skinCount)throw new Error('UI 皮肤数量超过 500 个上限');
  for(const id of ids){
    if(!(new RegExp(SKIN_PROTOCOL.idPattern)).test(id))throw new Error('UI 皮肤 ID 无效：'+id);
    const s=o.skins[id];if(!s||typeof s!=='object')throw new Error('UI 皮肤记录无效：'+id);
    if(s.kind==='css'){try{s.css=Security.validateUserCss(s.css);}catch(err){throw new Error(`UI 皮肤 ${id}：${err.message}`);}}
  }
  return JSON.stringify(o,null,2);
}
ipcMain.handle('skins-save', async (e, json) => { try { await skinsStore.write(validateSkinsJson(json)); return { ok: true,path:skinsFile() }; } catch (err) { return { ok: false, error: err.message,path:skinsFile() }; } });
ipcMain.handle('skins-location', async () => ({format:SKIN_PROTOCOL.format,version:SKIN_PROTOCOL.version,file:skinsFile(),directory:path.dirname(skinsFile()),locationFile:skinLocationFile(),executable:process.execPath}));

// 原版 UI 配色与新建导图默认背景保存在 appearance.json；当前背景同时写进 .knotjot（旧 .bmap 仍兼容），且不污染 UI 皮肤库或文本框样式库。
const appearanceFile = () => path.join(app.getPath('userData'), 'appearance.json');
const appearanceStore=orderedTextStore(appearanceFile);
function validateAppearanceJson(json){
  if(typeof json!=='string'||Buffer.byteLength(json,'utf8')>Security.LIMITS.dataUrlBytes)throw new Error('外观设置格式无效或超过 16 MB 上限');
  const o=JSON.parse(json);if(!o||o.app!=='knotjot-appearance'||o.version!==1)throw new Error('外观设置格式无效');
  const b=o.background||{},data=String(b.imageData||'');
  if(data&&!/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(data))throw new Error('背景图片数据无效');
  if(data&&Buffer.byteLength(data,'utf8')>Security.LIMITS.dataUrlBytes)throw new Error('背景图片超过 16 MB 上限');
  if((Number(b.imageWidth)||0)>Security.LIMITS.imageSide||(Number(b.imageHeight)||0)>Security.LIMITS.imageSide||(Number(b.imageWidth)||0)*(Number(b.imageHeight)||0)>Security.LIMITS.imagePixels)throw new Error('背景图片尺寸超过上限');
  return json;
}
ipcMain.handle('appearance-load', async () => { try { return await appearanceStore.read(); } catch (_) { return null; } });
ipcMain.handle('appearance-save', async (e, json) => { try { await appearanceStore.write(validateAppearanceJson(json)); return {ok:true,path:appearanceFile()}; } catch(err){ return {ok:false,error:err.message,path:appearanceFile()}; } });

ipcMain.handle('text-styles-load', async () => readTextStyles());
ipcMain.handle('text-styles-location', async () => ({ directory: textStylesDir(), file: textStylesFile(), executable: process.execPath }));
ipcMain.handle('text-styles-save', async (e, library) => {
  try {
    if(textStylesRecoveryRequired&&!(library&&library.confirmOverwriteCorrupt))return {ok:false,error:'样式库损坏，覆盖前需要确认',path:textStylesFile(),corruptPath:textStylesCorruptPath,requiresConfirmation:true};
    const clean={format:'knotjot-text-styles',version:1,revision:0,writerId:`knotjot-${process.pid}`,defaultStyleId:(library&&library.defaultStyleId)||'classic',styleSettings:(library&&library.styleSettings)||{},styles:Array.isArray(library&&library.styles)?library.styles:[]};const ids=new Set();for(const style of clean.styles){const id=String(style&&style.id||'').trim().toLowerCase();if(!id||ids.has(id))throw new Error(!id?'样式缺少 ID':'样式 ID 重复：'+id);ids.add(id);if(style.textRules){style.textRules.maxLength=Math.max(0,Math.min(10000,Number(style.textRules.maxLength)||0));if(style.textRules.type==='regex'&&style.textRules.pattern){try{new RegExp(`^(?:${style.textRules.pattern})$`,'u');}catch(err){throw new Error('样式 '+style.id+' 的正则表达式无效：'+err.message);}}}}
    try{const disk=JSON.parse(await textStylesStore.read());if(!disk||!Array.isArray(disk.styles))throw new Error('styles 必须是数组');clean.revision=Math.max(0,Number(disk.revision)||0)+1;}catch(err){if(err&&err.code!=='ENOENT'&&!library.confirmOverwriteCorrupt)throw new Error('现有样式库无效，已停止写入：'+err.message);clean.revision=1;}
    await textStylesStore.write(JSON.stringify(clean,null,2));textStylesRecoveryRequired=false;textStylesCorruptPath='';
    return {ok:true,path:textStylesFile(),revision:clean.revision,writerId:clean.writerId};
  } catch(err){ return {ok:false,error:err.message,path:textStylesFile()}; }
});

// AI 设置：普通配置写 JSON；API Key 只允许进入 Electron safeStorage（Windows 下为 DPAPI），不写 localStorage/明文文件。
const aiConfFile = () => path.join(app.getPath('userData'), 'ai-config.json');
const aiSecretFile = () => path.join(app.getPath('userData'), 'ai-secret.bin');
const aiConfStore=orderedTextStore(aiConfFile);
function cleanAIConfig(value){const o=value&&typeof value==='object'?value:{};return {backend:'openai',baseUrl:String(o.baseUrl||'').trim().slice(0,2048),model:String(o.model||'').trim().slice(0,500)};}
async function saveAISecret(key){
  if(!key){await fs.promises.unlink(aiSecretFile()).catch(()=>{});return {stored:false};}
  if(!safeStorage.isEncryptionAvailable())return {stored:false,warning:'系统凭据加密当前不可用；密钥只在本次运行中使用'};
  await fs.promises.mkdir(path.dirname(aiSecretFile()),{recursive:true});
  const encrypted=safeStorage.encryptString(String(key));const tmp=`${aiSecretFile()}.tmp-${process.pid}-${Date.now()}`;await fs.promises.writeFile(tmp,encrypted);const result=await atomicReplacePrepared(tmp,aiSecretFile());if(!result.ok)throw Object.assign(new Error(result.error),result);return {stored:true};
}
async function loadAISecret(){try{if(!safeStorage.isEncryptionAvailable())return '';return safeStorage.decryptString(await fs.promises.readFile(aiSecretFile()));}catch(_){return '';}}
ipcMain.handle('aiconf-load', async () => { try { const raw=JSON.parse(await aiConfStore.read()),legacyKey=typeof raw.apiKey==='string'?raw.apiKey:'';const clean=cleanAIConfig(raw);let apiKey=await loadAISecret();if(!apiKey&&legacyKey&&safeStorage.isEncryptionAvailable()){await saveAISecret(legacyKey);apiKey=legacyKey;await aiConfStore.write(JSON.stringify(clean,null,2));}return JSON.stringify(Object.assign(clean,{apiKey,keyStored:!!apiKey})); } catch (err) { return null; } });
ipcMain.handle('aiconf-save', async (e, json) => { try { const input=typeof json==='string'?JSON.parse(json):json||{},clean=cleanAIConfig(input);const secret=await saveAISecret(input.rememberKey===false?'':input.apiKey);await aiConfStore.write(JSON.stringify(clean,null,2));return { ok: true,keyStored:secret.stored,warning:secret.warning||'' }; } catch (err) { return { ok: false, error: err.message }; } });

// AI 请求走主进程（Node，无浏览器 CORS 限制，可直连本地 Ollama/LM Studio 与各家云）。
// 密钥/地址由渲染层传来，主进程只负责转发，不落任何日志、不改内容。
const aiReqs = new Map();
ipcMain.on('ai-chat-start', async (e, { reqId, url, headers, body }) => {
  const deadline=AIClient.createDeadlineController(null);
  aiReqs.set(reqId, deadline);
  const send = (ch, data) => { if (!e.sender.isDestroyed()) e.sender.send(ch, Object.assign({ reqId }, data)); };
  const sendJson=json=>{const content=AIClient.extractJsonContent(json);if(content)send('ai-chat-raw',{chunk:'data: '+JSON.stringify({choices:[{delta:{content}}]})+'\n'});if(json&&json.usage)send('ai-chat-raw',{chunk:'data: '+JSON.stringify({choices:[],usage:json.usage})+'\n'});};
  try {
    const res = await fetch(url, { method: 'POST', headers, body, signal: deadline.signal });deadline.touch();
    if (!res.ok) { let t = ''; try { t = await res.text(); } catch (_) {}
      send('ai-chat-error', { message: 'HTTP ' + res.status + (t ? (' · ' + t.slice(0, 300)) : '') }); aiReqs.delete(reqId); return; }
    const type=AIClient.contentType(res);
    if (type.includes('application/json') || !res.body) { const j = await res.json();sendJson(j);send('ai-chat-end', {});aiReqs.delete(reqId);deadline.finish();return; }
    const decoder=new TextDecoder('utf-8'),unknown=!type.includes('text/event-stream');let buffered='',sse=!unknown;
    for await (const chunk of res.body) { deadline.touch();const text=decoder.decode(chunk,{stream:true});if(unknown&&!sse){buffered+=text;if(buffered.trimStart().startsWith('data:')){sse=true;send('ai-chat-raw',{chunk:buffered});buffered='';}}else send('ai-chat-raw', { chunk:text }); }
    const tail=decoder.decode();if(sse){if(tail)send('ai-chat-raw',{chunk:tail});}else{buffered+=tail;try{sendJson(JSON.parse(buffered));}catch(_){if(buffered.trim())throw new Error('AI 服务返回了无法识别的非 SSE 内容');}}
    send('ai-chat-end', {});
  } catch (err) { const timeout=deadline.getError();send('ai-chat-error', { message: (timeout&&timeout.message)||(err && err.message) || String(err),code:(timeout&&timeout.code)||(err&&err.code)||'' }); }
  deadline.finish();
  aiReqs.delete(reqId);
});
ipcMain.on('ai-chat-abort', (e, { reqId }) => { const c = aiReqs.get(reqId); if (c) { try { c.controller.abort();c.finish(); } catch (_) {} aiReqs.delete(reqId); } });
// 测试连接（非流式，返回结果）
const aiUtilityReqs=new Map();
ipcMain.on('ai-utility-abort',(e,{requestId})=>{const d=aiUtilityReqs.get(requestId);if(d){d.controller.abort();d.finish();aiUtilityReqs.delete(requestId);}});
ipcMain.handle('ai-probe', async (e, { url, headers, body,requestId }) => {
  const deadline=AIClient.createDeadlineController(null,{firstByte:AIClient.DEFAULT_TIMEOUTS.probe,total:AIClient.DEFAULT_TIMEOUTS.probe,idle:AIClient.DEFAULT_TIMEOUTS.probe});
  if(requestId)aiUtilityReqs.set(requestId,deadline);
  try { const res = await fetch(url, { method: 'POST', headers, body,signal:deadline.signal });deadline.touch();deadline.finish();
    if (res.ok) return { ok: true };
    let t = ''; try { t = await res.text(); } catch (_) {}
    return { ok: false, status: res.status, text: t.slice(0, 200) };
  } catch (err) { const timeout=deadline.getError();deadline.finish();return { ok: false, error: (timeout&&timeout.message)||(err && err.message) || String(err),code:(timeout&&timeout.code)||'' }; }
  finally{if(requestId)aiUtilityReqs.delete(requestId);}
});
// 列出端点可用模型（GET /models，OpenAI 兼容；Ollama/LM Studio/各家云都支持）
ipcMain.handle('ai-models', async (e, { url, headers,requestId }) => {
  const deadline=AIClient.createDeadlineController(null,{firstByte:AIClient.DEFAULT_TIMEOUTS.models,total:AIClient.DEFAULT_TIMEOUTS.models,idle:AIClient.DEFAULT_TIMEOUTS.models});
  if(requestId)aiUtilityReqs.set(requestId,deadline);
  try { const res = await fetch(url, { method: 'GET', headers,signal:deadline.signal });deadline.touch();
    if (!res.ok) { let t = ''; try { t = await res.text(); } catch (_) {} return { ok: false, status: res.status, text: t.slice(0, 200) }; }
    const j = await res.json();deadline.finish();return { ok: true, data: j };
  } catch (err) { const timeout=deadline.getError();deadline.finish();return { ok: false, error: (timeout&&timeout.message)||(err && err.message) || String(err),code:(timeout&&timeout.code)||'' }; }
  finally{if(requestId)aiUtilityReqs.delete(requestId);}
});

// 渲染层启动时索取“双击打开的初始文件”
ipcMain.handle('get-initial-file', async () => {
  if (!pendingFile) return null;
  const r = await readFileSafe(pendingFile);
  pendingFile = null;
  return r.ok ? r : null;
});

ipcMain.handle('open-external', async (e, value) => {
  try { const url=Security.parseSafeExternalUrl(value,false);await shell.openExternal(url);return {ok:true,url}; }
  catch(err){return {ok:false,error:err.message};}
});

// 未保存提示框
ipcMain.handle('confirm-discard', async (e, name) => {
  const { response } = await dialog.showMessageBox(mainWin, {
    type: 'warning',
    buttons: ['保存', '不保存', '取消'],
    defaultId: 0,
    cancelId: 2,
    message: `“${name || '未命名思维导图'}” 有未保存的更改`,
    detail: '是否保存当前思维导图？'
  });
  return response; // 0=保存 1=不保存 2=取消
});

// 单实例：已开着软件时再双击文件，转发给现有窗口，而不是另开一个
const integrationTest = process.env.KNOTJOT_INTEGRATION_TEST === '1';
const skinLocationReplyArg=(process.argv||[]).find(x=>typeof x==='string'&&(
  x.startsWith('--knotjot-ui-skins-location-file=') ||
  x.startsWith('--bmap-ui-skins-location-file=')
));
const skinLocationReply=skinLocationReplyArg?skinLocationReplyArg.slice(skinLocationReplyArg.indexOf('=')+1).replace(/^"|"$/g,''):'';
const textStylesLocationReplyArg=(process.argv||[]).find(x=>typeof x==='string'&&x.startsWith('--knotjot-text-styles-location-file='));
const textStylesLocationReply=textStylesLocationReplyArg?textStylesLocationReplyArg.slice(textStylesLocationReplyArg.indexOf('=')+1).replace(/^"|"$/g,''):'';
if(integrationTest)module.exports.__test={
  attachWindowForSkinWatch(win){mainWin=win;startSkinWatch();},
  skinsFile
};
let openFileQueue=Promise.resolve();
function enqueueOpenFile(fp){
  openFileQueue=openFileQueue.then(async()=>{const r=await readFileSafe(fp);
    if(r.ok&&mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('open-file-data',r);}).catch(()=>{});
  return openFileQueue;
}
const gotLock = integrationTest || !!skinLocationReply || !!textStylesLocationReply || app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', (event, argv) => {
    const f = fileFromArgv(argv);
    if (mainWin) {
      if (mainWin.isMinimized()) mainWin.restore();
      mainWin.focus();
      if (f) enqueueOpenFile(f);
    }
  });

  // macOS：通过“打开方式”打开文件
  app.on('open-file', (event, fp) => {
    event.preventDefault();
    if (mainWin) enqueueOpenFile(fp);
    else pendingFile = fp;
  });

  app.whenReady().then(() => {
    if (integrationTest) return;
    if(skinLocationReply){const payload={format:SKIN_PROTOCOL.locationFormat,version:1,file:skinsFile(),directory:path.dirname(skinsFile()),executable:process.execPath};writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally(()=>app.quit());return;}
    if(textStylesLocationReply){const payload={format:'knotjot-text-styles-location',version:1,file:textStylesFile(),directory:textStylesDir(),executable:process.execPath};writeAtomicText(textStylesLocationReply,JSON.stringify(payload,null,2)).finally(()=>app.quit());return;}
    pendingFile = fileFromArgv(process.argv);   // 首次启动时双击传入的文件
    createWindow();
    app.on('activate', () => {
      if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
  });

  app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') app.quit();
  });
}
