const { app, BrowserWindow, Menu, ipcMain, dialog, shell, safeStorage, net } = require('electron');
const path = require('path');
const fs = require('fs');
const { atomicReplacePrepared, writeAtomicText, orderedTextStore, errorDetails } = require('./src/atomic-store');
const Security = require('./src/security');
const AIClient = require('./src/ai-client');
const FileExtensions = require('./src/file-extensions');
// EN: Recovery is an app-owned userData store with named, validation-only IPC boundaries.
// ZH: 恢复使用应用自有 userData 存储，仅提供具名且经过校验的 IPC 边界。
const recoveryStore = require('./src/recovery-store').createRecoveryStore(() => path.join(app.getPath('userData'),'recovery-v1'));
ipcMain.handle('recovery-put',async(_event,entry)=>recoveryStore.put(entry));
ipcMain.handle('recovery-list',async()=>recoveryStore.list());
ipcMain.handle('recovery-remove',async(_event,id)=>recoveryStore.remove(id));
ipcMain.handle('recovery-saved',async(_event,document)=>recoveryStore.findSaved(document));
ipcMain.handle('recovery-configure',async(_event,preferences)=>recoveryStore.configure(preferences||{}));
ipcMain.handle('recovery-prompt',async(event,name)=>{
  // EN: Offer explicit recovery, discard and later choices; cancel never replaces the current document.
  // ZH: 提供明确的恢复、丢弃、稍后选择；取消绝不替换当前文档。
  const result=await dialog.showMessageBox(BrowserWindow.fromWebContents(event.sender),{type:'question',title:'KnotJot',message:'发现未保存的恢复副本 / Unsaved recovery copy',detail:String(name||'').slice(0,200),buttons:['恢复 / Recover','丢弃 / Discard','稍后 / Later'],defaultId:2,cancelId:2,noLink:true});return result.response;
});

app.setName('KnotJot');

// 开发版放源码目录；便携版放 EXE 同级；安装版放 Electron 的 userData，避免 Program Files 写权限问题。
const textStylesDir = () => /* EN: Resolve the writable text-style directory for development, portable and installed launches. ZH: 按开发、便携和安装运行方式确定可写的文本样式目录。 */ process.env.KNOTJOT_TEXT_STYLES_DIR || (
  !app.isPackaged
    ? path.join(__dirname, 'resources', 'text-styles')
    : path.join(process.env.PORTABLE_EXECUTABLE_DIR || path.join(app.getPath('appData'), 'KnotJot'), 'text-styles')
);
const textStylesFile = () => /* EN: Return the primary custom-text-styles.json path inside the selected text-style storage directory. ZH: 返回所选文本样式存储目录内的主库 custom-text-styles.json 路径。 */ path.join(textStylesDir(), 'custom-text-styles.json');
const textStylesStore = orderedTextStore(textStylesFile);
const textStyleCandidates = () => /* EN: List distinct library paths: the primary path plus the executable-adjacent compatibility path for packaged launches. ZH: 列出不重复的样式库路径：主库路径，以及打包运行时可执行程序旁的兼容路径。 */ [...new Set([textStylesFile(), ...(app.isPackaged ? [path.join(path.dirname(process.execPath),'text-styles','custom-text-styles.json')] : [])])];
let textStyleWatchStarted = false;
let textStylesRecoveryRequired = false;
let textStylesCorruptPath = '';
async function readTextStyles() {
/* EN: Read and validate the newest library candidate; on corruption, preserve a recovery path and immediately require confirmation before replacement.
   ZH: 读取并验证最新候选库；损坏时保留恢复路径，并立即要求确认后才允许覆盖。 */

  const found=[];for(const file of textStyleCandidates()){try{const st=await fs.promises.stat(file);found.push({file,mtime:st.mtimeMs});}catch(_){}}
  found.sort((a,b)=>/* EN: Compare items for found.sort: return b.mtime-a.mtime. ZH: 比较 found.sort 中的元素顺序：返回 b.mtime-a.mtime。 */ b.mtime-a.mtime);
  for(const item of found){try{const json=JSON.parse(await fs.promises.readFile(item.file,'utf8'));if(!json||!['knotjot-text-styles','thoughtcanvas-text-styles'].includes(json.format)||Number(json.version)!==1||!Array.isArray(json.styles))throw new Error('format、version 或 styles 无效');const ids=new Set();for(const style of json.styles){const id=String(style&&style.id||'').trim().toLowerCase();if(!id||ids.has(id))throw new Error(!id?'样式缺少 ID':'样式 ID 重复：'+id);ids.add(id);}textStylesRecoveryRequired=false;textStylesCorruptPath='';return {ok:true,path:item.file,format:'knotjot-text-styles',version:1,revision:Number(json.revision)||0,writerId:String(json.writerId||''),defaultStyleId:json.defaultStyleId||'classic',styleSettings:json.styleSettings||{},styles:json.styles};}catch(err){
    const stamp=new Date().toISOString().replace(/[:.]/g,'-');textStylesCorruptPath=`${item.file}.${stamp}.corrupt`;
    await fs.promises.copyFile(item.file,textStylesCorruptPath).catch(()=>{
/* EN: Continue readTextStyles at fs.promises.copyFile(item.file,textStylesCorruptPath).catch: update textStylesCorruptPath.
   ZH: 在 fs.promises.copyFile(item.file,textStylesCorruptPath).catch 阶段继续 readTextStyles 流程：更新 textStylesCorruptPath。 */
textStylesCorruptPath=item.file;});textStylesRecoveryRequired=true;
    return {ok:false,path:item.file,defaultStyleId:'classic',styleSettings:{},styles:[],error:'文本框样式库损坏，已保留副本：'+textStylesCorruptPath,corruptPath:textStylesCorruptPath,requiresConfirmation:true};
  }}
  return {ok:false,path:textStylesFile(),defaultStyleId:'classic',styleSettings:{},styles:[],error:''};
}
function startTextStyleWatch() {
/* EN: Register polling once for every candidate library, create the primary directory, and notify the live renderer when a watched file changes.
   ZH: 为每个候选库注册轮询且仅初始化一次，创建主库目录，并在监视文件变化时通知仍存活的渲染窗口。 */

  if (textStyleWatchStarted) return;
  textStyleWatchStarted = true;
  fs.mkdirSync(path.dirname(textStylesFile()), { recursive: true });
  textStyleCandidates().forEach(file=>/* EN: Process each item in textStyleCandidates().forEach: call fs.watchFile. ZH: 逐项处理 textStyleCandidates().forEach 中的元素：调用 fs.watchFile。 */ fs.watchFile(file,{interval:450},()=>{
/* EN: Callback for fs.watchFile: call mainWin.isDestroyed, mainWin.webContents.send.
   ZH: fs.watchFile 的回调：调用 mainWin.isDestroyed、mainWin.webContents.send。 */
if(mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('text-styles-changed');}));
}

const FILTERS = [
  { name: 'KnotJot 思维导图', extensions: FileExtensions.mapExtensions.map(extension => /* EN: Derive the next value for FileExtensions.mapExtensions.map: call extension.slice; return extension.slice(1). ZH: 计算 FileExtensions.mapExtensions.map 的下一项结果：调用 extension.slice；返回 extension.slice(1)。 */ extension.slice(1)) },
  { name: '所有文件', extensions: ['*'] }
];

let mainWin = null;
let pendingFile = null;   // 启动时通过双击文件传入、等待渲染层读取的路径
let allowClose = false;   // 是否已确认可以关闭

// 让安装版在 Windows 的任务栏、通知和文件关联中使用稳定的应用标识。
if (process.platform === 'win32') app.setAppUserModelId('io.github.h-cl-h.knotjot');

// 从命令行参数里找出 KnotJot 或旧版 BMAP 文件路径（双击文件时由系统传入）
function fileFromArgv(argv) {
/* EN: Find an existing new-format or legacy mind-map file in launch arguments.
   ZH: 从启动参数中寻找存在的新格式或旧格式导图文件。 */

  if (!argv) return null;
  const hit = argv.find(a => /* EN: Test an item for argv.find: call FileExtensions.isMapFile, fs.existsSync; return FileExtensions.isMapFile(a) && fs.existsSync(a). ZH: 判断 argv.find 的元素条件：调用 FileExtensions.isMapFile、fs.existsSync；返回 FileExtensions.isMapFile(a) && fs.existsSync(a)。 */ FileExtensions.isMapFile(a) && fs.existsSync(a));
  return hit || null;
}
async function readFileSafe(fp) {
/* EN: Read a regular UTF-8 document only after enforcing the 64 MB input limit.
   ZH: 确认目标为普通文件且不超过 64 MB 后读取 UTF-8 导图。 */

  try { const st=await fs.promises.stat(fp);if(!st.isFile())throw new Error('目标不是文件');if(st.size>Security.LIMITS.documentBytes)throw Object.assign(new Error('思维导图超过 64 MB 上限'),{code:'FILE_TOO_LARGE'});return { ok: true, path: fp, name: path.basename(fp), content: await fs.promises.readFile(fp, 'utf8') }; }
  catch (err) { return { ok: false, error: err.message }; }
}

let saveSessionId = 0;
const saveSessions = new Map();
const saveSenderCleanupBound = new Set();

async function cleanupSaveArtifacts(s, closeHandle = true) {
/* EN: Release temporary save files and handles when their renderer session ends or aborts.
   ZH: 渲染进程会话结束或中止时释放保存临时文件及句柄。 */

  if (!s) return;
  if (closeHandle) await s.handle.close().catch(() => {
/* EN: Continue cleanupSaveArtifacts at s.handle.close().catch: complete without changing state.
   ZH: 在 s.handle.close().catch 阶段继续 cleanupSaveArtifacts 流程：完成且不修改状态。 */
});
  await fs.promises.unlink(s.tempPath).catch(() => {
/* EN: Continue cleanupSaveArtifacts at fs.promises.unlink(s.tempPath).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(s.tempPath).catch 阶段继续 cleanupSaveArtifacts 流程：完成且不修改状态。 */
});
}
async function discardSaveSession(id) {
/* EN: Release temporary save files and handles when their renderer session ends or aborts.
   ZH: 渲染进程会话结束或中止时释放保存临时文件及句柄。 */

  const s = saveSessions.get(id);
  if (!s) return;
  saveSessions.delete(id);
  await cleanupSaveArtifacts(s);
}
async function discardSenderSaveSessions(senderId) {
/* EN: Release temporary save files and handles when their renderer session ends or aborts.
   ZH: 渲染进程会话结束或中止时释放保存临时文件及句柄。 */

  const ids = [...saveSessions].filter(([, s]) => /* EN: Test an item for [...saveSessions].filter: return s.senderId === senderId. ZH: 判断 [...saveSessions].filter 的元素条件：返回 s.senderId === senderId。 */ s.senderId === senderId).map(([id]) => /* EN: Derive the next value for [...saveSessions].filter(([, s]) => s.senderId === senderId).map: return id. ZH: 计算 [...saveSessions].filter(([, s]) => s.senderId === senderId).map 的下一项结果：返回 id。 */ id);
  await Promise.all(ids.map(discardSaveSession));
}
function bindSaveSenderCleanup(sender) {
/* EN: Release temporary save files and handles when their renderer session ends or aborts.
   ZH: 渲染进程会话结束或中止时释放保存临时文件及句柄。 */

  const senderId = sender.id;
  if (saveSenderCleanupBound.has(senderId)) return;
  saveSenderCleanupBound.add(senderId);
  const cleanup = () => {
/* EN: cleanup in bindSaveSenderCleanup: call saveSenderCleanupBound.delete, sender.removeListener, discardSenderSaveSessions.
   ZH: bindSaveSenderCleanup 中的 cleanup：调用 saveSenderCleanupBound.delete、sender.removeListener、discardSenderSaveSessions。 */

    saveSenderCleanupBound.delete(senderId);
    sender.removeListener('destroyed', cleanup);
    sender.removeListener('render-process-gone', cleanup);
    void discardSenderSaveSessions(senderId);
  };
  sender.once('destroyed', cleanup);
  sender.once('render-process-gone', cleanup);
}

function createWindow() {
/* EN: Create the Electron main window and route close requests through the renderer save decision.
   ZH: 创建 Electron 主窗口，并将关闭请求交给渲染层确认是否保存。 */

  const win = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 760,
    minHeight: 520,
    backgroundColor: '#f5f6f8',
    title: 'KnotJot',
    icon: path.join(__dirname, 'resources', 'icons', 'icon.ico'),
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  Menu.setApplicationMenu(null);          // 去掉浏览器式菜单栏
  win.loadFile(path.join(__dirname, 'index.html'));
  win.webContents.setWindowOpenHandler(() => (/* EN: Callback for win.webContents.setWindowOpenHandler: return { action: 'deny' }. ZH: win.webContents.setWindowOpenHandler 的回调：返回 { action: 'deny' }。 */ { action: 'deny' }));
  win.webContents.on('will-navigate', (event, url) => {
/* EN: Handle will-navigate on win.webContents.on: call win.webContents.getURL, event.preventDefault.
   ZH: 处理 win.webContents.on 的 will-navigate 事件或通道：调用 win.webContents.getURL、event.preventDefault。 */

    if (url !== win.webContents.getURL()) event.preventDefault();
  });
  mainWin = win;
  startTextStyleWatch();
  startSkinWatch();
  void publishSkinLocation();

  // 关闭前：交给渲染层判断是否有未保存更改
  win.on('close', (e) => {
/* EN: Handle close on win.on: call e.preventDefault, win.webContents.send.
   ZH: 处理 win.on 的 close 事件或通道：调用 e.preventDefault、win.webContents.send。 */

    if (allowClose) return;
    e.preventDefault();
    win.webContents.send('app-close-request');
  });
}

// 渲染层处理完“保存/不保存/取消”后回传结果
ipcMain.on('close-confirmed', (e, proceed) => {
/* EN: Handle close-confirmed on ipcMain.on: call mainWin.close; update allowClose.
   ZH: 处理 ipcMain.on 的 close-confirmed 事件或通道：调用 mainWin.close；更新 allowClose。 */

  if (proceed && mainWin) { allowClose = true; mainWin.close(); }
});

// 大文件按画布分片写入：渲染层不再构造并跨 IPC 复制一份完整 JSON。
ipcMain.handle('save-begin', async (e, { filePath, defaultName }) => {
/* EN: Handle save-begin on ipcMain.handle: call dialog.showSaveDialog, fs.promises.open, saveSessions.set; update target.
   ZH: 处理 ipcMain.handle 的 save-begin 事件或通道：调用 dialog.showSaveDialog、fs.promises.open、saveSessions.set；更新 target。 */

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
/* EN: Handle save-chunk on ipcMain.handle: call saveSessions.get, Buffer.from, s.handle.write; update s.position.
   ZH: 处理 ipcMain.handle 的 save-chunk 事件或通道：调用 saveSessions.get、Buffer.from、s.handle.write；更新 s.position。 */

  const s = saveSessions.get(id);
  if (!s || s.senderId !== e.sender.id) throw new Error('保存会话无效');
  const buf = Buffer.from(String(chunk || ''), 'utf8');
  await s.handle.write(buf, 0, buf.length, s.position);
  s.position += buf.length;
  return { ok: true };
});

ipcMain.handle('save-end', async (e, { id, abort }) => {
/* EN: Handle save-end on ipcMain.handle: call saveSessions.get, saveSessions.delete, s.handle.sync; update closed.
   ZH: 处理 ipcMain.handle 的 save-end 事件或通道：调用 saveSessions.get、saveSessions.delete、s.handle.sync；更新 closed。 */

  const s = saveSessions.get(id);
  if (!s || s.senderId !== e.sender.id) throw new Error('保存会话无效');
  saveSessions.delete(id);
  let closed = false;
  try {
    await s.handle.sync();
    await s.handle.close();
    closed = true;
    if (abort) { await fs.promises.unlink(s.tempPath).catch(()=>{
/* EN: Continue module at fs.promises.unlink(s.tempPath).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(s.tempPath).catch 阶段继续 module 流程：完成且不修改状态。 */
});return { ok: false, canceled: true }; }
    const result=await atomicReplacePrepared(s.tempPath,s.filePath);
    if(!result.ok)return Object.assign(result,{name:path.basename(s.filePath)});
    return { ok: true, path: s.filePath, name: path.basename(s.filePath) };
  } catch(err){
    return errorDetails(err,{path:s.filePath,tempPath:s.tempPath,originalPreserved:true});
  } finally {
    if(!closed)await s.handle.close().catch(()=>{
/* EN: Continue module at s.handle.close().catch: complete without changing state.
   ZH: 在 s.handle.close().catch 阶段继续 module 流程：完成且不修改状态。 */
});
    if(abort)await fs.promises.unlink(s.tempPath).catch(()=>{
/* EN: Continue module at fs.promises.unlink(s.tempPath).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(s.tempPath).catch 阶段继续 module 流程：完成且不修改状态。 */
});
  }
});

// 打开：选择文件并读取内容
ipcMain.handle('open', async () => {
/* EN: Handle open on ipcMain.handle: call dialog.showOpenDialog, readFileSafe.
   ZH: 处理 ipcMain.handle 的 open 事件或通道：调用 dialog.showOpenDialog、readFileSafe。 */

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
/* EN: Handle open-path on ipcMain.handle: call readFileSafe.
   ZH: 处理 ipcMain.handle 的 open-path 事件或通道：调用 readFileSafe。 */

  return readFileSafe(filePath);
});

// 导出图片 / PDF：选择保存位置并写入二进制
ipcMain.handle('export-save', async (e, { base64, defaultName, ext }) => {
/* EN: Handle export-save on ipcMain.handle: call dialog.showSaveDialog, fs.promises.writeFile, Buffer.from.
   ZH: 处理 ipcMain.handle 的 export-save 事件或通道：调用 dialog.showSaveDialog、fs.promises.writeFile、Buffer.from。 */

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
/* EN: loadSkinProtocol in module: call path.resolve, path.join, JSON.parse.
   ZH: module 中的 loadSkinProtocol：调用 path.resolve、path.join、JSON.parse。 */

  const candidates=[path.resolve(__dirname,'..','..','..','..','shared','ui-skin-protocol.json'),path.join(process.resourcesPath||'','protocols','ui-skin-protocol.json')];
  for(const file of candidates){try{return JSON.parse(fs.readFileSync(file,'utf8'));}catch(_){}}
  return {format:'knotjot-ui-skins',version:1,locationFormat:'knotjot-skins-location',idPattern:'^[A-Za-z0-9_-]{3,80}$',fileName:'ui-skins.json',applicationDataDirectory:'KnotJot',locationFileName:'skin-sync-location.json',legacyApplicationDataDirectories:['ThoughtCanvas'],limits:{libraryBytes:16*1024*1024,cssBytes:2*1024*1024,skinCount:500}};
}
const SKIN_PROTOCOL=loadSkinProtocol();
const skinsFile = () => /* EN: Resolve the dedicated user-data file for this settings or credential category. ZH: 为此设置或凭据类别定位独立的用户数据文件。 */ process.env.KNOTJOT_UI_SKINS_FILE || path.join(app.getPath('userData'), SKIN_PROTOCOL.fileName);
const skinLocationFile=()=>/* EN: Resolve the dedicated user-data file for this settings or credential category. ZH: 为此设置或凭据类别定位独立的用户数据文件。 */ path.join(path.dirname(skinsFile()),SKIN_PROTOCOL.locationFileName);
async function migrateLegacySkins(){
/* EN: Seed the current skin store from a valid legacy store only when the current file is absent.
   ZH: 仅在当前皮肤库不存在时，从有效旧皮肤库迁移初始数据。 */

  try{await fs.promises.access(skinsFile());return;}catch(_){}
  for(const dir of SKIN_PROTOCOL.legacyApplicationDataDirectories||[]){const legacy=path.join(app.getPath('appData'),dir,SKIN_PROTOCOL.fileName);try{const text=await fs.promises.readFile(legacy,'utf8');const parsed=JSON.parse(text);if(parsed&&parsed.skins&&typeof parsed.skins==='object'){parsed.format=SKIN_PROTOCOL.format;parsed.version=SKIN_PROTOCOL.version;parsed.writerId='knotjot-migration';parsed.updatedUtc=new Date().toISOString();await writeAtomicText(skinsFile(),JSON.stringify(parsed,null,2));return;}}catch(_){}}
}
async function publishSkinLocation(){
/* EN: Publish the runtime skin path and executable/source identity for companion-editor discovery.
   ZH: 发布运行时皮肤路径及程序、源码标识，供配套编辑器发现连接目标。 */

  const payload={format:SKIN_PROTOCOL.locationFormat,version:1,file:skinsFile(),directory:path.dirname(skinsFile()),executable:process.execPath,sourceMain:path.join(__dirname,'main.js'),updatedUtc:new Date().toISOString()};
  await writeAtomicText(skinLocationFile(),JSON.stringify(payload,null,2)).catch(()=>{
/* EN: Continue publishSkinLocation at writeAtomicText(skinLocationFile(),JSON.stringify(payload,null,2)).catch: complete without changing state.
   ZH: 在 writeAtomicText(skinLocationFile(),JSON.stringify(payload,null,2)).catch 阶段继续 publishSkinLocation 流程：完成且不修改状态。 */
});
}
let skinWatchStarted=false;
function startSkinWatch(){
/* EN: Start one file watcher and notify the renderer when an external editor replaces the library.
   ZH: 只启动一次文件监视，在外部编辑器替换样式库后通知渲染层。 */

  if(skinWatchStarted)return;skinWatchStarted=true;
  fs.mkdirSync(path.dirname(skinsFile()),{recursive:true});
  void migrateLegacySkins().then(publishSkinLocation);
  fs.watchFile(skinsFile(),{interval:450},()=>{
/* EN: Callback for fs.watchFile: call mainWin.isDestroyed, mainWin.webContents.send.
   ZH: fs.watchFile 的回调：调用 mainWin.isDestroyed、mainWin.webContents.send。 */

    if(mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('ui-skins-changed');
  });
}
const skinsStore=orderedTextStore(skinsFile);
ipcMain.handle('skins-load', async () => {
/* EN: Handle skins-load on ipcMain.handle: call skinsStore.read.
   ZH: 处理 ipcMain.handle 的 skins-load 事件或通道：调用 skinsStore.read。 */
 try { return await skinsStore.read(); } catch (err) { return null; } });
function validateSkinsJson(json){
/* EN: Validate skin IDs, library limits and user CSS before serializing the shared protocol.
   ZH: 序列化共享协议前验证皮肤 ID、库容量限制及用户 CSS。 */

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
ipcMain.handle('skins-save', async (e, json) => {
/* EN: Handle skins-save on ipcMain.handle: call skinsStore.write, validateSkinsJson, skinsFile.
   ZH: 处理 ipcMain.handle 的 skins-save 事件或通道：调用 skinsStore.write、validateSkinsJson、skinsFile。 */
 try { await skinsStore.write(validateSkinsJson(json)); return { ok: true,path:skinsFile() }; } catch (err) { return { ok: false, error: err.message,path:skinsFile() }; } });
ipcMain.handle('skins-location', async () => (/* EN: Handle skins-location on ipcMain.handle: call skinsFile, path.dirname, skinLocationFile. ZH: 处理 ipcMain.handle 的 skins-location 事件或通道：调用 skinsFile、path.dirname、skinLocationFile。 */ {format:SKIN_PROTOCOL.format,version:SKIN_PROTOCOL.version,file:skinsFile(),directory:path.dirname(skinsFile()),locationFile:skinLocationFile(),executable:process.execPath}));

// 原版 UI 配色与新建导图默认背景保存在 appearance.json；当前背景同时写进 .knotjot（旧 .bmap 仍兼容），且不污染 UI 皮肤库或文本框样式库。
const appearanceFile = () => /* EN: Resolve the dedicated user-data file for this settings or credential category. ZH: 为此设置或凭据类别定位独立的用户数据文件。 */ path.join(app.getPath('userData'), 'appearance.json');
const appearanceStore=orderedTextStore(appearanceFile);
function validateAppearanceJson(json){
/* EN: Reject incompatible appearance payloads and oversized embedded background images.
   ZH: 拒绝格式不兼容的外观数据和超限的内嵌背景图片。 */

  if(typeof json!=='string'||Buffer.byteLength(json,'utf8')>Security.LIMITS.dataUrlBytes)throw new Error('外观设置格式无效或超过 16 MB 上限');
  const o=JSON.parse(json);if(!o||o.app!=='knotjot-appearance'||o.version!==1)throw new Error('外观设置格式无效');
  const b=o.background||{},data=String(b.imageData||'');
  if(data&&!/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(data))throw new Error('背景图片数据无效');
  if(data&&Buffer.byteLength(data,'utf8')>Security.LIMITS.dataUrlBytes)throw new Error('背景图片超过 16 MB 上限');
  if((Number(b.imageWidth)||0)>Security.LIMITS.imageSide||(Number(b.imageHeight)||0)>Security.LIMITS.imageSide||(Number(b.imageWidth)||0)*(Number(b.imageHeight)||0)>Security.LIMITS.imagePixels)throw new Error('背景图片尺寸超过上限');
  return json;
}
ipcMain.handle('appearance-load', async () => {
/* EN: Handle appearance-load on ipcMain.handle: call appearanceStore.read.
   ZH: 处理 ipcMain.handle 的 appearance-load 事件或通道：调用 appearanceStore.read。 */
 try { return await appearanceStore.read(); } catch (_) { return null; } });
ipcMain.handle('appearance-save', async (e, json) => {
/* EN: Handle appearance-save on ipcMain.handle: call appearanceStore.write, validateAppearanceJson, appearanceFile.
   ZH: 处理 ipcMain.handle 的 appearance-save 事件或通道：调用 appearanceStore.write、validateAppearanceJson、appearanceFile。 */
 try { await appearanceStore.write(validateAppearanceJson(json)); return {ok:true,path:appearanceFile()}; } catch(err){ return {ok:false,error:err.message,path:appearanceFile()}; } });

ipcMain.handle('text-styles-load', async () => /* EN: Handle text-styles-load on ipcMain.handle: call readTextStyles; return readTextStyles(). ZH: 处理 ipcMain.handle 的 text-styles-load 事件或通道：调用 readTextStyles；返回 readTextStyles()。 */ readTextStyles());
ipcMain.handle('text-styles-location', async () => (/* EN: Handle text-styles-location on ipcMain.handle: call textStylesDir, textStylesFile; return { directory: textStylesDir(), file: textStylesFile(), executable: process.execPath }. ZH: 处理 ipcMain.handle 的 text-styles-location 事件或通道：调用 textStylesDir、textStylesFile；返回 { directory: textStylesDir(), file: textStylesFile(), executable: process.execPath }。 */ { directory: textStylesDir(), file: textStylesFile(), executable: process.execPath }));
ipcMain.handle('text-styles-save', async (e, library) => {
/* EN: Handle text-styles-save on ipcMain.handle: call textStylesFile, Array.isArray, String(style&&style.id||'').trim().toLowerCase; update style.textRules.maxLength, clean.revision, textStylesRecoveryRequired.
   ZH: 处理 ipcMain.handle 的 text-styles-save 事件或通道：调用 textStylesFile、Array.isArray、String(style&&style.id||'').trim().toLowerCase；更新 style.textRules.maxLength、clean.revision、textStylesRecoveryRequired。 */

  try {
    if(textStylesRecoveryRequired&&!(library&&library.confirmOverwriteCorrupt))return {ok:false,error:'样式库损坏，覆盖前需要确认',path:textStylesFile(),corruptPath:textStylesCorruptPath,requiresConfirmation:true};
    const clean={format:'knotjot-text-styles',version:1,revision:0,writerId:`knotjot-${process.pid}`,defaultStyleId:(library&&library.defaultStyleId)||'classic',styleSettings:(library&&library.styleSettings)||{},styles:Array.isArray(library&&library.styles)?library.styles:[]};const ids=new Set();for(const style of clean.styles){const id=String(style&&style.id||'').trim().toLowerCase();if(!id||ids.has(id))throw new Error(!id?'样式缺少 ID':'样式 ID 重复：'+id);ids.add(id);if(style.textRules){style.textRules.maxLength=Math.max(0,Math.min(10000,Number(style.textRules.maxLength)||0));if(style.textRules.type==='regex'&&style.textRules.pattern){try{new RegExp(`^(?:${style.textRules.pattern})$`,'u');}catch(err){throw new Error('样式 '+style.id+' 的正则表达式无效：'+err.message);}}}}
    try{const disk=JSON.parse(await textStylesStore.read());if(!disk||!Array.isArray(disk.styles))throw new Error('styles 必须是数组');clean.revision=Math.max(0,Number(disk.revision)||0)+1;}catch(err){if(err&&err.code!=='ENOENT'&&!library.confirmOverwriteCorrupt)throw new Error('现有样式库无效，已停止写入：'+err.message);clean.revision=1;}
    await textStylesStore.write(JSON.stringify(clean,null,2));textStylesRecoveryRequired=false;textStylesCorruptPath='';
    return {ok:true,path:textStylesFile(),revision:clean.revision,writerId:clean.writerId};
  } catch(err){ return {ok:false,error:err.message,path:textStylesFile()}; }
});

// AI 设置：普通配置写 JSON；API Key 只允许进入 Electron safeStorage（Windows 下为 DPAPI），不写 localStorage/明文文件。
const aiConfFile = () => /* EN: Resolve the dedicated user-data file for this settings or credential category. ZH: 为此设置或凭据类别定位独立的用户数据文件。 */ path.join(app.getPath('userData'), 'ai-config.json');
const aiSecretFile = () => /* EN: Resolve the dedicated user-data file for this settings or credential category. ZH: 为此设置或凭据类别定位独立的用户数据文件。 */ path.join(app.getPath('userData'), 'ai-secret.bin');
const aiConfStore=orderedTextStore(aiConfFile);
function cleanAIConfig(value){
/* EN: cleanAIConfig in module: call String(o.baseUrl||'').trim().slice, String(o.baseUrl||'').trim, String(o.model||'').trim().slice.
   ZH: module 中的 cleanAIConfig：调用 String(o.baseUrl||'').trim().slice、String(o.baseUrl||'').trim、String(o.model||'').trim().slice。 */
const o=value&&typeof value==='object'?value:{};return {backend:'openai',baseUrl:String(o.baseUrl||'').trim().slice(0,2048),model:String(o.model||'').trim().slice(0,500)};}
async function saveAISecret(key){
/* EN: Store the API credential using the main-process credential persistence path.
   ZH: 通过主进程凭据持久化路径保存 API 凭据。 */

  if(!key){await fs.promises.unlink(aiSecretFile()).catch(()=>{
/* EN: Continue saveAISecret at fs.promises.unlink(aiSecretFile()).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(aiSecretFile()).catch 阶段继续 saveAISecret 流程：完成且不修改状态。 */
});return {stored:false};}
  if(!safeStorage.isEncryptionAvailable())return {stored:false,warning:'系统凭据加密当前不可用；密钥只在本次运行中使用'};
  await fs.promises.mkdir(path.dirname(aiSecretFile()),{recursive:true});
  const encrypted=safeStorage.encryptString(String(key));const tmp=`${aiSecretFile()}.tmp-${process.pid}-${Date.now()}`;await fs.promises.writeFile(tmp,encrypted);const result=await atomicReplacePrepared(tmp,aiSecretFile());if(!result.ok)throw Object.assign(new Error(result.error),result);return {stored:true};
}
async function loadAISecret(){
/* EN: Read the saved API credential according to this version's storage implementation.
   ZH: 按本版本存储实现读取已保存的 API 凭据。 */
try{if(!safeStorage.isEncryptionAvailable())return '';return safeStorage.decryptString(await fs.promises.readFile(aiSecretFile()));}catch(_){return '';}}
ipcMain.handle('aiconf-load', async () => {
/* EN: Handle aiconf-load on ipcMain.handle: call JSON.parse, aiConfStore.read, cleanAIConfig; update apiKey.
   ZH: 处理 ipcMain.handle 的 aiconf-load 事件或通道：调用 JSON.parse、aiConfStore.read、cleanAIConfig；更新 apiKey。 */
 try { const raw=JSON.parse(await aiConfStore.read()),legacyKey=typeof raw.apiKey==='string'?raw.apiKey:'';const clean=cleanAIConfig(raw);let apiKey=await loadAISecret();if(!apiKey&&legacyKey&&safeStorage.isEncryptionAvailable()){await saveAISecret(legacyKey);apiKey=legacyKey;await aiConfStore.write(JSON.stringify(clean,null,2));}return JSON.stringify(Object.assign(clean,{apiKey,keyStored:!!apiKey})); } catch (err) { return null; } });
ipcMain.handle('aiconf-save', async (e, json) => {
/* EN: Handle aiconf-save on ipcMain.handle: call JSON.parse, cleanAIConfig, saveAISecret.
   ZH: 处理 ipcMain.handle 的 aiconf-save 事件或通道：调用 JSON.parse、cleanAIConfig、saveAISecret。 */
 try { const input=typeof json==='string'?JSON.parse(json):json||{},clean=cleanAIConfig(input);const secret=await saveAISecret(input.rememberKey===false?'':input.apiKey);await aiConfStore.write(JSON.stringify(clean,null,2));return { ok: true,keyStored:secret.stored,warning:secret.warning||'' }; } catch (err) { return { ok: false, error: err.message }; } });

// EN: Use Chromium's network stack so AI honors system/session proxy settings without renderer CORS restrictions.
// ZH: 使用 Chromium 网络栈，使 AI 遵循系统和会话代理设置，且不受渲染层跨域限制。
// EN: Forward credentials only in request headers; never log or persist request bodies or credentials here.
// ZH: 凭据仅通过请求头转发，此处不记录或持久化请求正文与凭据。
function aiFetch(url, options) {
  /* EN: Fetch through Electron networking and retain actionable native error details on connection failure.
     ZH: 使用 Electron 网络请求，并在连接失败时保留可定位问题的原生错误信息。 */
  return net.fetch(url, options);
}
const aiReqs = new Map();
ipcMain.on('ai-chat-start', async (e, { reqId, url, headers, body }) => {
/* EN: Handle ai-chat-start on ipcMain.on: call AIClient.createDeadlineController, aiReqs.set, fetch; update t, buffered, sse.
   ZH: 处理 ipcMain.on 的 ai-chat-start 事件或通道：调用 AIClient.createDeadlineController、aiReqs.set、fetch；更新 t、buffered、sse。 */

  const deadline=AIClient.createDeadlineController(null,AIClient.requestTimeouts(url));
  aiReqs.set(reqId, deadline);
  const send = (ch, data) => {
/* EN: send in module: call e.sender.isDestroyed, e.sender.send.
   ZH: module 中的 send：调用 e.sender.isDestroyed、e.sender.send。 */
 if (!e.sender.isDestroyed()) e.sender.send(ch, Object.assign({ reqId }, data)); };
  const sendJson=json=>{
/* EN: sendJson in module: call AIClient.extractJsonContent, send, JSON.stringify.
   ZH: module 中的 sendJson：调用 AIClient.extractJsonContent、send、JSON.stringify。 */
const content=AIClient.extractJsonContent(json);if(content)send('ai-chat-raw',{chunk:'data: '+JSON.stringify({choices:[{delta:{content}}]})+'\n'});if(json&&json.usage)send('ai-chat-raw',{chunk:'data: '+JSON.stringify({choices:[],usage:json.usage})+'\n'});};
  try {
    const res = await aiFetch(url, { method: 'POST', headers, body, signal: deadline.signal });deadline.touch();
    if (!res.ok) { let t = ''; try { t = await res.text(); } catch (_) {}
      send('ai-chat-error', { message: 'HTTP ' + res.status + (t ? (' · ' + t.slice(0, 300)) : '') }); aiReqs.delete(reqId); deadline.finish(); return; }
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
ipcMain.on('ai-chat-abort', (e, { reqId }) => {
/* EN: Handle ai-chat-abort on ipcMain.on: call aiReqs.get, c.controller.abort, c.finish.
   ZH: 处理 ipcMain.on 的 ai-chat-abort 事件或通道：调用 aiReqs.get、c.controller.abort、c.finish。 */
 const c = aiReqs.get(reqId); if (c) { try { c.controller.abort();c.finish(); } catch (_) {} aiReqs.delete(reqId); } });
// 测试连接（非流式，返回结果）
const aiUtilityReqs=new Map();
ipcMain.on('ai-utility-abort',(e,{requestId})=>{
/* EN: Handle ai-utility-abort on ipcMain.on: call aiUtilityReqs.get, d.controller.abort, d.finish.
   ZH: 处理 ipcMain.on 的 ai-utility-abort 事件或通道：调用 aiUtilityReqs.get、d.controller.abort、d.finish。 */
const d=aiUtilityReqs.get(requestId);if(d){d.controller.abort();d.finish();aiUtilityReqs.delete(requestId);}});
ipcMain.handle('ai-probe', async (e, { url, headers, body,requestId }) => {
/* EN: Handle ai-probe on ipcMain.handle: call AIClient.createDeadlineController, aiUtilityReqs.set, fetch; update t.
   ZH: 处理 ipcMain.handle 的 ai-probe 事件或通道：调用 AIClient.createDeadlineController、aiUtilityReqs.set、fetch；更新 t。 */

  const deadline=AIClient.createDeadlineController(null,AIClient.requestTimeouts(url,true));
  if(requestId)aiUtilityReqs.set(requestId,deadline);
  // EN: Read and validate the probe response before releasing its deadline; an HTML login page is not a model response.
  // ZH: 在释放超时保护前读取并验证探测响应，HTML 登录页不能视为模型连接成功。
  try { const res = await aiFetch(url, { method: 'POST', headers, body,signal:deadline.signal });deadline.touch();
    if (res.ok) {
      if (AIClient.contentType(res).includes('text/event-stream') && res.body) {
        // EN: Require actual text/thinking or a valid completed choice; preserve upstream errors and stop at DONE before cancelling the remaining generation.
        // ZH: 仅实际文本、思考内容或有效完成结果可判成功；保留上游错误，遇到 DONE 及时停止并取消剩余生成。
        const reader = res.body.getReader(), decoder = new TextDecoder(), state = { buf: '' };
        let received = false, streamError = '';
        try {
          while (!received && !streamError && !state.done) {
            const { value, done } = await reader.read(); deadline.touch();
            AIClient.sseLines(state, decoder.decode(value || new Uint8Array(), { stream: !done }), event => {
              // EN: Metadata-only deltas cannot prove inference works, and errors must remain outside the SSE parser's swallowed callback exceptions.
              // ZH: 仅有元数据的增量不能证明推理成功；错误需显式保存，避免被 SSE 解析器的回调异常捕获吞掉。
              if (event && event.error) {
                const error = event.error;
                streamError = String(typeof error === 'string' ? error : error.message || error.code || error.type || '模型返回流式错误').slice(0, 500);
                return;
              }
              const choice = event && event.choices && event.choices[0];
              if (!choice) return;
              const delta = choice.delta || {}, message = choice.message || {};
              const output = [choice.text, delta.content, delta.reasoning_content, delta.reasoning, message.content, message.reasoning_content, message.reasoning];
              if (output.some(value => typeof value === 'string' && value.trim()) || ['stop', 'length', 'tool_calls', 'function_call', 'content_filter'].includes(choice.finish_reason)) received = true;
            }, done);
            if (done || state.done) break;
          }
        } finally {
          // EN: Abort Electron's underlying request as well as its reader so a successful probe cannot leave model generation running.
          // ZH: 同时终止 Electron 底层请求和读取器，避免探测成功后模型仍持续生成。
          deadline.controller.abort();
          await reader.cancel().catch(() => {});
        }
        if (streamError) return { ok: false, error: streamError };
        return received ? { ok: true } : { ok: false, error: '接口没有返回有效的聊天事件' };
      }
      const raw = await res.text();
      let json;
      try { json = JSON.parse(raw); } catch (_) { return { ok: false, error: '接口未返回模型 JSON，请检查接口路径或代理登录状态' }; }
      if (!json || !Array.isArray(json.choices) || json.choices.length === 0) return { ok: false, error: '接口未返回聊天结果，请检查模型名和接口地址' };
      return { ok: true };
    }
    let t = ''; try { t = await res.text(); } catch (_) {}
    return { ok: false, status: res.status, text: t.slice(0, 200) };
  } catch (err) { const timeout=deadline.getError();deadline.finish();return { ok: false, error: (timeout&&timeout.message)||(err && err.message) || String(err),code:(timeout&&timeout.code)||'' }; }
  finally{deadline.finish();if(requestId)aiUtilityReqs.delete(requestId);}
});
// 列出端点可用模型（GET /models，OpenAI 兼容；Ollama/LM Studio/各家云都支持）
ipcMain.handle('ai-models', async (e, { url, headers,requestId }) => {
/* EN: Handle ai-models on ipcMain.handle: call AIClient.createDeadlineController, aiUtilityReqs.set, fetch; update t.
   ZH: 处理 ipcMain.handle 的 ai-models 事件或通道：调用 AIClient.createDeadlineController、aiUtilityReqs.set、fetch；更新 t。 */

  const deadline=AIClient.createDeadlineController(null,{firstByte:AIClient.DEFAULT_TIMEOUTS.models,total:AIClient.DEFAULT_TIMEOUTS.models,idle:AIClient.DEFAULT_TIMEOUTS.models});
  if(requestId)aiUtilityReqs.set(requestId,deadline);
  try { const res = await aiFetch(url, { method: 'GET', headers,signal:deadline.signal });deadline.touch();
    if (!res.ok) { let t = ''; try { t = await res.text(); } catch (_) {} return { ok: false, status: res.status, text: t.slice(0, 200) }; }
    const j = await res.json();deadline.finish();return { ok: true, data: j };
  } catch (err) { const timeout=deadline.getError();deadline.finish();return { ok: false, error: (timeout&&timeout.message)||(err && err.message) || String(err),code:(timeout&&timeout.code)||'' }; }
  finally{deadline.finish();if(requestId)aiUtilityReqs.delete(requestId);}
});

// 渲染层启动时索取“双击打开的初始文件”
ipcMain.handle('get-initial-file', async () => {
/* EN: Handle get-initial-file on ipcMain.handle: call readFileSafe; update pendingFile.
   ZH: 处理 ipcMain.handle 的 get-initial-file 事件或通道：调用 readFileSafe；更新 pendingFile。 */

  if (!pendingFile) return null;
  const r = await readFileSafe(pendingFile);
  pendingFile = null;
  return r.ok ? r : null;
});

ipcMain.handle('open-external', async (e, value) => {
/* EN: Handle open-external on ipcMain.handle: call Security.parseSafeExternalUrl, shell.openExternal.
   ZH: 处理 ipcMain.handle 的 open-external 事件或通道：调用 Security.parseSafeExternalUrl、shell.openExternal。 */

  try { const url=Security.parseSafeExternalUrl(value,false);await shell.openExternal(url);return {ok:true,url}; }
  catch(err){return {ok:false,error:err.message};}
});

// 未保存提示框
ipcMain.handle('confirm-discard', async (e, name) => {
/* EN: Handle confirm-discard on ipcMain.handle: call dialog.showMessageBox.
   ZH: 处理 ipcMain.handle 的 confirm-discard 事件或通道：调用 dialog.showMessageBox。 */

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
const skinLocationReplyArg=(process.argv||[]).find(x=>/* EN: Test an item for (process.argv||[]).find: call x.startsWith. ZH: 判断 (process.argv||[]).find 的元素条件：调用 x.startsWith。 */ typeof x==='string'&&(
  x.startsWith('--knotjot-ui-skins-location-file=') ||
  x.startsWith('--bmap-ui-skins-location-file=')
));
const skinLocationReply=skinLocationReplyArg?skinLocationReplyArg.slice(skinLocationReplyArg.indexOf('=')+1).replace(/^"|"$/g,''):'';
const textStylesLocationReplyArg=(process.argv||[]).find(x=>/* EN: Test an item for (process.argv||[]).find: call x.startsWith; return typeof x==='string'&&x.startsWith('--knotjot-text-styles-location-file='). ZH: 判断 (process.argv||[]).find 的元素条件：调用 x.startsWith；返回 typeof x==='string'&&x.startsWith('--knotjot-text-styles-location-file=')。 */ typeof x==='string'&&x.startsWith('--knotjot-text-styles-location-file='));
const textStylesLocationReply=textStylesLocationReplyArg?textStylesLocationReplyArg.slice(textStylesLocationReplyArg.indexOf('=')+1).replace(/^"|"$/g,''):'';
if(integrationTest)module.exports.__test={
  attachWindowForSkinWatch(win){
/* EN: attachWindowForSkinWatch in module: call startSkinWatch; update mainWin.
   ZH: module 中的 attachWindowForSkinWatch：调用 startSkinWatch；更新 mainWin。 */
mainWin=win;startSkinWatch();},
  skinsFile
};
let openFileQueue=Promise.resolve();
function enqueueOpenFile(fp){
/* EN: Serialize incoming open-file requests before forwarding document contents to the active window.
   ZH: 将外部打开请求串行化后，把文档内容转发给当前窗口。 */

  openFileQueue=openFileQueue.then(async()=>{
/* EN: Continue enqueueOpenFile at openFileQueue.then: call readFileSafe, mainWin.isDestroyed, mainWin.webContents.send.
   ZH: 在 openFileQueue.then 阶段继续 enqueueOpenFile 流程：调用 readFileSafe、mainWin.isDestroyed、mainWin.webContents.send。 */
const r=await readFileSafe(fp);
    if(r.ok&&mainWin&&!mainWin.isDestroyed())mainWin.webContents.send('open-file-data',r);}).catch(()=>{
/* EN: Callback for openFileQueue.then(async()=>{const r=await readFileSafe(fp); if(r.ok&&mainWin&&!mainWin.isDestroyed())mainWin.: complete without changing state.
   ZH: openFileQueue.then(async()=>{const r=await readFileSafe(fp); if(r.ok&&mainWin&&!mainWin.isDestroyed())mainWin. 的回调：完成且不修改状态。 */
});
  return openFileQueue;
}
const gotLock = integrationTest || !!skinLocationReply || !!textStylesLocationReply || app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', (event, argv) => {
/* EN: Handle second-instance on app.on: call fileFromArgv, mainWin.isMinimized, mainWin.restore.
   ZH: 处理 app.on 的 second-instance 事件或通道：调用 fileFromArgv、mainWin.isMinimized、mainWin.restore。 */

    const f = fileFromArgv(argv);
    if (mainWin) {
      if (mainWin.isMinimized()) mainWin.restore();
      mainWin.focus();
      if (f) enqueueOpenFile(f);
    }
  });

  // macOS：通过“打开方式”打开文件
  app.on('open-file', (event, fp) => {
/* EN: Handle open-file on app.on: call event.preventDefault, enqueueOpenFile; update pendingFile.
   ZH: 处理 app.on 的 open-file 事件或通道：调用 event.preventDefault、enqueueOpenFile；更新 pendingFile。 */

    event.preventDefault();
    if (mainWin) enqueueOpenFile(fp);
    else pendingFile = fp;
  });

  app.whenReady().then(() => {
/* EN: Continue module at app.whenReady().then: call skinsFile, path.dirname, writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally; update pendingFile.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 skinsFile、path.dirname、writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally；更新 pendingFile。 */

    if (integrationTest) return;
    if(skinLocationReply){const payload={format:SKIN_PROTOCOL.locationFormat,version:1,file:skinsFile(),directory:path.dirname(skinsFile()),executable:process.execPath};writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally(()=>/* EN: Continue module at writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally: call app.quit; return app.quit(). ZH: 在 writeAtomicText(skinLocationReply,JSON.stringify(payload,null,2)).finally 阶段继续 module 流程：调用 app.quit；返回 app.quit()。 */ app.quit());return;}
    if(textStylesLocationReply){const payload={format:'knotjot-text-styles-location',version:1,file:textStylesFile(),directory:textStylesDir(),executable:process.execPath};writeAtomicText(textStylesLocationReply,JSON.stringify(payload,null,2)).finally(()=>/* EN: Continue module at writeAtomicText(textStylesLocationReply,JSON.stringify(payload,null,2)).finally: call app.quit; return app.quit(). ZH: 在 writeAtomicText(textStylesLocationReply,JSON.stringify(payload,null,2)).finally 阶段继续 module 流程：调用 app.quit；返回 app.quit()。 */ app.quit());return;}
    pendingFile = fileFromArgv(process.argv);   // 首次启动时双击传入的文件
    createWindow();
    app.on('activate', () => {
/* EN: Handle activate on app.on: call BrowserWindow.getAllWindows, createWindow.
   ZH: 处理 app.on 的 activate 事件或通道：调用 BrowserWindow.getAllWindows、createWindow。 */

      if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
  });

  app.on('window-all-closed', () => {
/* EN: Handle window-all-closed on app.on: call app.quit.
   ZH: 处理 app.on 的 window-all-closed 事件或通道：调用 app.quit。 */

    if (process.platform !== 'darwin') app.quit();
  });
}
