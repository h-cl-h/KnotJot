const { app, BrowserWindow } = require('electron');
const fs = require('fs');
const os = require('os');
const path = require('path');

app.commandLine.appendSwitch('disable-gpu');
process.env.KNOTJOT_INTEGRATION_TEST = '1';
const profile = path.join(os.tmpdir(), `knotjot-hardening-profile-${process.pid}`);
app.setPath('userData', profile);
require('../main');

function fail(message, details) {
/* EN: fail in module: call console.error, app.exit.
   ZH: module 中的 fail：调用 console.error、app.exit。 */

  console.error('HARDENING_SMOKE_FAILED', message, details || '');
  app.exit(1);
}

async function rendererSmoke() {
/* EN: rendererSmoke in module: call document.querySelector, csp.includes, unsafe.every; update window.alert, window.confirm, broken.sheets[0].roots.
   ZH: module 中的 rendererSmoke：调用 document.querySelector、csp.includes、unsafe.every；更新 window.alert、window.confirm、broken.sheets[0].roots。 */

  const sleep = ms => /* EN: Resolve a promise after the requested delay. ZH: 经过指定延迟后完成 Promise。 */ new Promise(resolve => /* EN: Callback for module: call setTimeout; return setTimeout(resolve, ms). ZH: module 的回调：调用 setTimeout；返回 setTimeout(resolve, ms)。 */ setTimeout(resolve, ms));
  const oldAlert = window.alert, oldConfirm = window.confirm;
  window.alert = () => {
/* EN: window.alert in rendererSmoke: complete without changing state.
   ZH: rendererSmoke 中的 window.alert：完成且不修改状态。 */
};
  window.confirm = () => /* EN: window.confirm in rendererSmoke: return true. ZH: rendererSmoke 中的 window.confirm：返回 true。 */ true;
  try {
    const csp = document.querySelector('meta[http-equiv="Content-Security-Policy"]')?.content || '';
    const cspOk = csp.includes("object-src 'none'") && csp.includes("base-uri 'none'") && csp.includes("form-action 'none'");

    const unsafe = ['javascript:alert(1)', 'file:///C:/Windows/win.ini', 'data:text/html,x', 'custom:payload'];
    const rendererLinksBlocked = unsafe.every(value => {
/* EN: Test an item for unsafe.every: call KnotJotSecurity.parseSafeExternalUrl.
   ZH: 判断 unsafe.every 的元素条件：调用 KnotJotSecurity.parseSafeExternalUrl。 */
 try { KnotJotSecurity.parseSafeExternalUrl(value); return false; } catch (_) { return true; } });
    const normalizedHttps = KnotJotSecurity.parseSafeExternalUrl('example.com/path').startsWith('https://example.com/path');
    const mainLinkResult = await window.api.openExternal('file:///C:/Windows/win.ini');
    const mainLinkBlocked = !!mainLinkResult && mainLinkResult.ok === false;

    const gradientSafe = KnotJotSecurity.normalizeGradient('135deg,#112233 0%,#aabbcc 100%') === '135deg,#112233 0%,#aabbcc 100%';
    const gradientBlocked = ['135deg,#fff,url(https://bad.test/x)', '1deg,#112233;display:none,#445566', '999deg,#112233,#445566'].every(value => /* EN: Test an item for ['135deg,#fff,url(https://bad.test/x)', '1deg,#112233;display:none,#445566', '999deg,#112233,#445566'].every: call KnotJotSecurity.normalizeGradient; return KnotJotSecurity.normalizeGradient(value) === null. ZH: 判断 ['135deg,#fff,url(https://bad.test/x)', '1deg,#112233;display:none,#445566', '999deg,#112233,#445566'].every 的元素条件：调用 KnotJotSecurity.normalizeGradient；返回 KnotJotSecurity.normalizeGradient(value) === null。 */ KnotJotSecurity.normalizeGradient(value) === null);
    const cssBlocked = ['@import url(https://bad.test/x);', '.x{background:url(https://bad.test/x)}', '#settingsMask{display:none}', '.x{behavior:url(x)}'].every(value => {
/* EN: Callback for ['@import url(https://bad.test/x);', '.x{background:url(https://bad.test/x)}', '#settingsMask{display:none}', : call KnotJotSecurity.validateUserCss.
   ZH: ['@import url(https://bad.test/x);', '.x{background:url(https://bad.test/x)}', '#settingsMask{display:none}',  的回调：调用 KnotJotSecurity.validateUserCss。 */
 try { KnotJotSecurity.validateUserCss(value); return false; } catch (_) { return true; } });
    const cssSafe = KnotJotSecurity.validateUserCss(':root{--accent:#123456}.btn{border-radius:8px}') !== '';

    const beforeText = serialize();
    const beforeState = { name: curName, sheet: curSheet, dirty, revision: modelRevision, history: history.length, hidx };
    const broken = JSON.parse(beforeText);
    broken.sheets[0].roots = ['missing-root'];
    const invalidAccepted = await loadDataAsync(JSON.stringify(broken), 'broken');
    const afterState = { name: curName, sheet: curSheet, dirty, revision: modelRevision, history: history.length, hidx };
    const invalidLoadAtomic = invalidAccepted === false && serialize() === beforeText && JSON.stringify(afterState) === JSON.stringify(beforeState);

    if (!roots.length) { const id = newTextbox('sheet-a-original'); TB[id].x = 3000; TB[id].y = 1800; roots.push(id); update(); markDirty(); }
    addSheet('brace');
    const sheetB = curSheet;
    if (!roots.length) { const id = newTextbox('sheet-b-original'); TB[id].x = 3000; TB[id].y = 1800; roots.push(id); update(); markDirty(); }
    switchSheet(0);
    const aId = roots[0], aOriginal = TB[aId].text;
    TB[aId].text = 'sheet-a-change'; markDirty();
    switchSheet(sheetB);
    const bId = roots[0], bOriginal = TB[bId].text;
    TB[bId].text = 'sheet-b-change'; markDirty();
    switchSheet(0);
    const aHistoryPreserved = history.length > 0 && canUndo();
    undo(); const aUndo = TB[aId].text === aOriginal;
    redo(); const aRedo = TB[aId].text === 'sheet-a-change';
    switchSheet(sheetB);
    undo(); const bUndo = TB[bId].text === bOriginal;
    redo(); const bRedo = TB[bId].text === 'sheet-b-change';

    switchSheet(0);
    const beforeAdd = sheets.length;
    addSheet('brace');
    const addedIndex = curSheet, addApplied = sheets.length === beforeAdd + 1;
    undo(); const addUndo = sheets.length === beforeAdd;
    redo(); const addRedo = sheets.length === beforeAdd + 1;
    const oldName = sheets[curSheet].name;
    renameSheet(curSheet, 'renamed-history-sheet');
    undo(); const renameUndo = sheets[curSheet].name === oldName;
    redo(); const renameRedo = sheets[curSheet].name === 'renamed-history-sheet';
    const beforeDelete = sheets.length;
    deleteSheet(curSheet);
    const deleteApplied = sheets.length === beforeDelete - 1;
    undo(); const deleteUndo = sheets.length === beforeDelete && sheets.some(s => /* EN: Test an item for sheets.some: return s.name === 'renamed-history-sheet'. ZH: 判断 sheets.some 的元素条件：返回 s.name === 'renamed-history-sheet'。 */ s.name === 'renamed-history-sheet');
    redo(); const deleteRedo = sheets.length === beforeDelete - 1;

    const beforeAI = sheets.length;
    aiGenerateSheets([
      { name: 'AI history one', struct: 'brace', tree: [{ text: 'AI root one', children: [] }] },
      { name: 'AI history two', struct: 'brace', tree: [{ text: 'AI root two', children: [] }] }
    ]);
    const aiApplied = sheets.length === beforeAI + 2;
    undo(); const aiUndo = sheets.length === beforeAI;
    redo(); const aiRedo = sheets.length === beforeAI + 2;

    const backgroundBefore = FileHistory.capture();
    const backgroundBeforeJson = JSON.stringify(backgroundBefore.appearance);
    const backgroundNext = { background: { enabled: true, mode: 'limited', imageData: '', imageName: '', imageWidth: 0, imageHeight: 0, imageScale: 100, positionX: 0, positionY: 0, canvasWidth: 1800, canvasHeight: 1200 } };
    documentAppearance = JSON.parse(JSON.stringify(backgroundNext));
    AppearanceEditor.loadDocumentAppearance(documentAppearance);
    markDirty(false); FileHistory.commit(backgroundBefore);
    const backgroundApplied = JSON.stringify(FileHistory.capture().appearance) !== backgroundBeforeJson;
    undo(); const backgroundUndo = JSON.stringify(FileHistory.capture().appearance) === backgroundBeforeJson;
    redo(); const backgroundRedo = JSON.stringify(FileHistory.capture().appearance) !== backgroundBeforeJson;

    const exportId = roots[0];
    const pixel = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';
    TB[exportId].img = { src: pixel, w: 1, h: 1 };
    TB[exportId].tags = ['export-tag'];
    TB[exportId].note = 'export-note';
    TB[exportId].textStyle = { replaceFrame: true, layers: [{ type: 'rect', x: 0, y: 0, w: 100, h: 100, fill: '#dde9ff', stroke: '#5b8def', strokeWidth: 1 }], textRegion: { x: 10, y: 10, w: 80, h: 80 } };
    const boundaryId = 'bd-hardening';
    BD[boundaryId] = { id: boundaryId, members: [exportId], label: 'export-boundary', color: '#336699' };
    update(); layout();
    const skinStyle = document.createElement('style');
    skinStyle.setAttribute('data-uiskin', '1');
    skinStyle.textContent = '.node{outline:7px solid rgb(1,2,3)}';
    document.head.appendChild(skinStyle);
    const skinCssIncluded = KnotJotExportScene.collectCssText(document, true).includes('rgb(1, 2, 3)');
    const skinCssExcluded = !KnotJotExportScene.collectCssText(document, false).includes('rgb(1, 2, 3)');
    const scene = KnotJotExportScene.buildScene({ document, canvas, world, scale, panX, panY, appearance: AppearanceEditor.getState(), pageBackground: '#f5f6f8', rootVariables: '--bg:#f5f6f8;--ink:#2c3140', includeSkin: true });
    const exportLayers = scene.markup.includes('ts-decoration') && scene.markup.includes('data:image/png') && scene.markup.includes('export-tag') && scene.markup.includes('bd-rect') && scene.markup.includes('export-boundary');
    const tiled = KnotJotExportScene.tilePlan(12000, 9000, 2).length > 4;
    const safeRatio = KnotJotExportScene.safeSingleRatio(20000, 10000, 2) < 1;
    const exportSvg = KnotJotExportScene.svgFor(scene).includes('<foreignObject');
    skinStyle.remove();

    localStorage.setItem('bmap.ai', JSON.stringify({ backend: 'openai', baseUrl: 'https://legacy.test/v1', model: 'legacy', apiKey: 'legacy-local-secret' }));
    const migrated = aiCfgGet();
    const legacyRemoved = migrated.apiKey === 'legacy-local-secret' && !localStorage.getItem('bmap.ai').includes('legacy-local-secret');
    const aiSave = await aiCfgSet({ backend: 'openai', baseUrl: 'https://api.example.test/v1', model: 'smoke-model', apiKey: 'hardening-secret-value', rememberKey: true });
    const localKeyAbsent = !localStorage.getItem('bmap.ai').includes('hardening-secret-value');

    LANG = 'en'; applyLang();
    openSettings();
    TextStyleFeature.open('rules');
    renderGallery();
    openAIChat('new'); aiOpenCfg();
    await sleep(80);
    const hasHan = value => /* EN: hasHan in rendererSmoke: call /[\u3400-\u9fff]/.test; return /[\u3400-\u9fff]/.test(value || ''). ZH: rendererSmoke 中的 hasHan：调用 /[\u3400-\u9fff]/.test；返回 /[\u3400-\u9fff]/.test(value || '')。 */ /[\u3400-\u9fff]/.test(value || '');
    const languagePanels = {
      textStyles: !hasHan(document.querySelector('#textStylePanel')?.textContent),
      appearance: !hasHan(document.querySelector('#tcAppearancePanel')?.textContent),
      gallery: !hasHan(document.querySelector('#uiGalleryGrid')?.textContent),
      ai: !hasHan(document.querySelector('#aiCfg')?.textContent)
    };
    LANG = 'zh'; applyLang(); TextStyleFeature.refresh();
    const languageRoundTrip = document.querySelector('#textStylePanel strong')?.textContent === '文本框样式与字体';

    return {
      cspOk, rendererLinksBlocked, normalizedHttps, mainLinkBlocked, gradientSafe, gradientBlocked, cssBlocked, cssSafe,
      invalidLoadAtomic,
      history: { aHistoryPreserved, aUndo, aRedo, bUndo, bRedo, addApplied, addUndo, addRedo, renameUndo, renameRedo, deleteApplied, deleteUndo, deleteRedo, aiApplied, aiUndo, aiRedo, backgroundApplied, backgroundUndo, backgroundRedo },
      export: { exportLayers, tiled, safeRatio, exportSvg, skinCssIncluded, skinCssExcluded, width: scene.W, height: scene.H },
      aiSecret: { legacyRemoved, localKeyAbsent, saveOk: !!aiSave?.ok, keyStored: !!aiSave?.keyStored },
      languagePanels, languageRoundTrip, addedIndex
    };
  } finally {
    window.alert = oldAlert;
    window.confirm = oldConfirm;
  }
}

app.whenReady().then(async () => {
/* EN: Continue module at app.whenReady().then: call path.join, win.webContents.on, win.loadFile; update result.aiSecret.publicFileNoPlaintext, result.aiSecret.secretFileNoPlaintext, result.cspConsoleClean.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 path.join、win.webContents.on、win.loadFile；更新 result.aiSecret.publicFileNoPlaintext、result.aiSecret.secretFileNoPlaintext、result.cspConsoleClean。 */

  const consoleMessages = [];
  const win = new BrowserWindow({
    show: false,
    width: 1280,
    height: 820,
    webPreferences: { contextIsolation: true, nodeIntegration: false, preload: path.join(__dirname, '..', 'preload.js') }
  });
  win.webContents.on('console-message', (_event, _level, message) => /* EN: Handle console-message on win.webContents.on: call consoleMessages.push; return consoleMessages.push(message). ZH: 处理 win.webContents.on 的 console-message 事件或通道：调用 consoleMessages.push；返回 consoleMessages.push(message)。 */ consoleMessages.push(message));
  try {
    await win.loadFile(path.join(__dirname, '..', 'index.html'));
    const result = await win.webContents.executeJavaScript(`(${rendererSmoke.toString()})()`);
    const publicPath = path.join(profile, 'ai-config.json');
    const secretPath = path.join(profile, 'ai-secret.bin');
    const publicText = await fs.promises.readFile(publicPath, 'utf8').catch(() => /* EN: Continue module at fs.promises.readFile(publicPath, 'utf8').catch: return ''. ZH: 在 fs.promises.readFile(publicPath, 'utf8').catch 阶段继续 module 流程：返回 ''。 */ '');
    const secretBytes = await fs.promises.readFile(secretPath).catch(() => /* EN: Continue module at fs.promises.readFile(secretPath).catch: call Buffer.alloc; return Buffer.alloc(0). ZH: 在 fs.promises.readFile(secretPath).catch 阶段继续 module 流程：调用 Buffer.alloc；返回 Buffer.alloc(0)。 */ Buffer.alloc(0));
    result.aiSecret.publicFileNoPlaintext = !publicText.includes('hardening-secret-value') && !publicText.includes('legacy-local-secret');
    result.aiSecret.secretFileNoPlaintext = !secretBytes.toString('utf8').includes('hardening-secret-value');
    result.cspConsoleClean = !consoleMessages.some(x => /* EN: Test an item for consoleMessages.some: call /Insecure Content-Security-Policy|Electron Security Warning/i.test; return /Insecure Content-Security-Policy|Electron Security Warning/i.test(x). ZH: 判断 consoleMessages.some 的元素条件：调用 /Insecure Content-Security-Policy|Electron Security Warning/i.test；返回 /Insecure Content-Security-Policy|Electron Security Warning/i.test(x)。 */ /Insecure Content-Security-Policy|Electron Security Warning/i.test(x));
    await win.webContents.executeJavaScript(`aiCfgSet({backend:'openai',baseUrl:'',model:'',apiKey:'',rememberKey:false})`);

    const historyOk = Object.values(result.history).every(Boolean);
    const exportOk = Object.entries(result.export).filter(([key]) => /* EN: Test an item for Object.entries(result.export).filter: call ['width','height'].includes; return !['width','height'].includes(key). ZH: 判断 Object.entries(result.export).filter 的元素条件：调用 ['width','height'].includes；返回 !['width','height'].includes(key)。 */ !['width','height'].includes(key)).every(([, value]) => /* EN: Test an item for Object.entries(result.export).filter(([key]) => !['width','height'].includes(key)).every: return value === true. ZH: 判断 Object.entries(result.export).filter(([key]) => !['width','height'].includes(key)).every 的元素条件：返回 value === true。 */ value === true);
    const aiSecretOk = result.aiSecret.legacyRemoved && result.aiSecret.localKeyAbsent && result.aiSecret.saveOk && result.aiSecret.publicFileNoPlaintext && result.aiSecret.secretFileNoPlaintext;
    const languageOk = Object.values(result.languagePanels).every(Boolean) && result.languageRoundTrip;
    const baseOk = result.cspOk && result.cspConsoleClean && result.rendererLinksBlocked && result.normalizedHttps && result.mainLinkBlocked && result.gradientSafe && result.gradientBlocked && result.cssBlocked && result.cssSafe && result.invalidLoadAtomic;
    console.log('HARDENING_RESULT', JSON.stringify(result));
    if (!baseOk) return fail('security or atomic-load checks failed', result);
    if (!historyOk) return fail('sheet/file history checks failed', result.history);
    if (!exportOk) return fail('unified/tiled export checks failed', result.export);
    if (!aiSecretOk) return fail('AI secret storage checks failed', result.aiSecret);
    if (!languageOk) return fail('dynamic language checks failed', result.languagePanels);
    app.exit(0);
  } catch (error) {
    fail(error && error.stack ? error.stack : String(error));
  }
});
