// EN: Characterize the shipped sanitizer boundary and real image/PDF exports in a hidden, isolated renderer; retain artifacts without opening native save dialogs.
// ZH: 在隐藏隔离渲染器中验证随包净化库边界和真实图片/PDF 导出，保存样本而不打开原生保存对话框。
const { app, BrowserWindow } = require('electron');
const fs = require('node:fs'), path = require('node:path');
const source = path.resolve(process.env.KNOTJOT_TEST_SOURCE || path.join(__dirname, '..'));
const output = path.resolve(process.env.KNOTJOT_TEST_OUTPUT || path.join(__dirname, '../../preview/testing/release-runtime'));
const dependencyRoot = path.resolve(process.env.KNOTJOT_TEST_DEPENDENCIES || path.join(source, 'node_modules'));
fs.mkdirSync(output, { recursive: true });
app.setPath('userData', path.join(output, 'profile'));
app.setPath('sessionData', path.join(output, 'session'));
app.commandLine.appendSwitch('disable-gpu');
process.env.KNOTJOT_INTEGRATION_TEST = '1';
process.env.KNOTJOT_TEXT_STYLES_DIR = path.join(output, 'text-styles');
process.env.KNOTJOT_UI_SKINS_FILE = path.join(output, 'ui-skins.json');
require(path.join(source, 'main.js'));

// EN: Run the actual application renderer and dependency bytes, save exported files, and exit nonzero if any behavior fails or the bounded timeout expires.
// ZH: 运行真实应用渲染器和依赖字节，保存导出文件；任一行为失败或超过限定时间即以非零码退出。
app.whenReady().then(async () => {
    const timer = setTimeout(() => { console.error('RELEASE_RUNTIME_TIMEOUT'); app.exit(2); }, 60000);
    const window = new BrowserWindow({ show: false, width: 1280, height: 900, webPreferences: { offscreen: true, contextIsolation: true, nodeIntegration: false, backgroundThrottling: false, preload: path.join(source, 'preload.js') } });
    // EN: Retain renderer exception details because executeJavaScript otherwise reports only a generic IPC failure.
    // ZH: 保留渲染器异常详情，避免 executeJavaScript 仅返回笼统的 IPC 失败信息。
    window.webContents.on('console-message', event => { if (event.level === 'error') console.error(event.message); });
    try {
        // EN: Observe startup before application scripts run; optionally seed English to cover both initial language paths.
        // ZH: 在应用脚本运行前观察启动，可预置英文以覆盖两种初始语言路径。
        const startupErrors = [];
        await window.loadURL('about:blank');
        window.webContents.debugger.attach('1.3');
        window.webContents.debugger.on('message', (_event, method, params) => { if (method === 'Runtime.exceptionThrown') startupErrors.push(params.exceptionDetails.exception?.description || params.exceptionDetails.text); });
        await window.webContents.debugger.sendCommand('Runtime.enable');
        await window.webContents.debugger.sendCommand('Page.enable');
        const language = process.env.KNOTJOT_TEST_LANGUAGE === 'en' ? 'en' : 'zh';
        await window.webContents.debugger.sendCommand('Page.addScriptToEvaluateOnNewDocument', { source: `localStorage.setItem('bmap.lang',${JSON.stringify(language)});` });
        await window.loadFile(path.join(source, 'index.html'));
        const sanitizer = fs.readFileSync(path.join(dependencyRoot, 'dompurify/dist/purify.min.js'), 'utf8');
        await window.webContents.executeJavaScript(sanitizer);
        const result = await window.webContents.executeJavaScript(`(${rendererChecks.toString()})(${JSON.stringify(language)})`);
        await checkNativeDrop(window, result);
        result.checks.push({ name: 'startup and runtime finish without uncaught renderer exceptions', pass: startupErrors.length === 0, detail: startupErrors });
        result.runtime = { electron: process.versions.electron, chromium: process.versions.chrome, node: process.versions.node };
        for (const [extension, base64] of Object.entries(result.exports)) fs.writeFileSync(path.join(output, `runtime-export.${extension}`), Buffer.from(base64, 'base64'));
        fs.writeFileSync(path.join(output, 'runtime-fixture.knotjot'), result.documentText);
        fs.writeFileSync(path.join(output, 'runtime-image.png'), Buffer.from(result.image.split(',')[1], 'base64'));
        delete result.exports; delete result.documentText; delete result.image;
        fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
        fs.writeFileSync(path.join(output, 'renderer.png'), (await window.webContents.capturePage()).toPNG());
        console.log(JSON.stringify(result));
        clearTimeout(timer); app.exit(result.checks.every(check => check.pass) ? 0 : 1);
    } catch (error) {
        clearTimeout(timer); fs.writeFileSync(path.join(output, 'error.txt'), error.stack); console.error(error.stack); app.exit(2);
    }
});

// EN: Feed a real disk-backed File through the application's drop handler and save IPC; synthetic Files must not gain a filesystem path.
// ZH: 让真实磁盘 File 经过应用拖入处理和保存 IPC；合成 File 不应获得文件系统路径。
async function checkNativeDrop(window, result) {
    const fixture = path.join(output, 'native-drop.knotjot');
    fs.writeFileSync(fixture, result.documentText);
    await window.webContents.executeJavaScript(`dirty=false; window.__dropLoaded=false; const oldLoad=loadDataAsync; loadDataAsync=async(...args)=>{const loaded=await oldLoad(...args);setTimeout(()=>window.__dropLoaded=true,0);return loaded;}; document.body.insertAdjacentHTML('beforeend','<input id="runtime-file" type="file" style="display:none">');`);
    const { root } = await window.webContents.debugger.sendCommand('DOM.getDocument');
    const { nodeId } = await window.webContents.debugger.sendCommand('DOM.querySelector', { nodeId: root.nodeId, selector: '#runtime-file' });
    await window.webContents.debugger.sendCommand('DOM.setFileInputFiles', { nodeId, files: [fixture] });
    await window.webContents.executeJavaScript(`const transfer=new DataTransfer();transfer.items.add(document.getElementById('runtime-file').files[0]);window.dispatchEvent(new DragEvent('drop',{dataTransfer:transfer}));`);
    for (let attempt = 0; attempt < 100; attempt++) {
        if (await window.webContents.executeJavaScript('window.__dropLoaded')) break;
        await new Promise(resolve => setTimeout(resolve, 50));
    }
    const destination = await window.webContents.executeJavaScript('curPath');
    result.checks.push({ name: 'native document drop retains its original save path', pass: destination === fixture, detail: { destination, expected: fixture } });
    if (destination === fixture) {
        const saved = await window.webContents.executeJavaScript(`(async()=>{TB[roots[0]].text='Native drop saved successfully';dirty=true;return await doSave();})()`);
        result.checks.push({ name: 'dropped document saves back through real file IPC', pass: !!saved?.ok && fs.readFileSync(fixture, 'utf8').includes('Native drop saved successfully') });
    }
    const synthetic = await window.webContents.executeJavaScript(`window.api.getPathForFile ? window.api.getPathForFile(new File(['{}'],'synthetic.knotjot')) : ''`);
    result.checks.push({ name: 'synthetic File has no native save destination', pass: synthetic === '' });
    window.webContents.debugger.detach();
}

async function rendererChecks(expectedLanguage) {
    // EN: Detect persistent sanitizer allowlist leakage with a harmless title attribute; then exercise actual image import, save/reopen and JPEG/PDF rendering.
    // ZH: 用无害 title 属性检测净化器白名单永久泄漏，再执行真实图片导入、保存重开及 JPEG/PDF 渲染。
    const checks = [], exports = {}, alerts = [];
    const check = (name, pass, detail) => checks.push({ name, pass: !!pass, detail });
    window.alert = message => alerts.push(String(message));
    window.confirm = () => true;
    const purifier = DOMPurify(window);
    let trusted = true;
    purifier.setConfig({ ALLOWED_TAGS: ['span'], ALLOWED_ATTR: [] });
    purifier.addHook('uponSanitizeAttribute', (_, data) => { if (trusted) data.allowedAttributes.title = true; });
    purifier.sanitize('<span title="trusted">one</span>');
    trusted = false;
    const sanitized = purifier.sanitize('<span title="must-not-leak">two</span>');
    check('sanitizer hook cannot persist an attribute allowance into the next call', sanitized === '<span>two</span>', { version: purifier.version, sanitized });

    await new Promise(resolve => setTimeout(resolve, 200));
    check('initial skin shortcuts are rendered in the selected language', LANG === expectedLanguage && document.querySelectorAll('#uiQuick .uichip').length > 0 && (LANG !== 'en' || !/[\u3400-\u9fff]/.test(document.getElementById('uiQuick').textContent)), { language: LANG, expectedLanguage, text: document.getElementById('uiQuick').textContent });
    dirty = false; await doNew('brace'); hideStart();
    const id = newTextbox('运行内核回归 Runtime export'); roots.push(id); TB[id].x = 160; TB[id].y = 180; update();
    const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 240;
    const context = canvas.getContext('2d'); context.fillStyle = '#1b57c8'; context.fillRect(0, 0, 320, 240); context.fillStyle = '#ffd54a'; context.fillRect(320, 0, 320, 240);
    const image = canvas.toDataURL('image/png');
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    await applyImageFile(id, new File([blob], 'runtime-image.png', { type: 'image/png' }));
    update();
    const documentText = serialize();
    check('original PNG bytes survive document serialization', documentText.includes(image), { bytes: image.length });
    const reopened = await loadDataAsync(documentText, 'runtime-fixture.knotjot');
    check('document reopens with its original image bytes', reopened && serialize().includes(image));
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    // EN: Replace only the native destination selection boundary; keep export scene, rasterization and PDF encoding real.
    // ZH: 仅替代原生保存目标选择边界，导出场景、栅格化和 PDF 编码均运行真实实现。
    saveExport = async (base64, extension) => { exports[extension] = base64; };
    await exportAs('jpg', true);
    await exportAs('pdf', true);
    check('real JPEG export contains encoded image bytes', !!exports.jpg && atob(exports.jpg).startsWith('\xff\xd8\xff') && exports.jpg.length > 1000);
    check('real PDF export contains an image and page', !!exports.pdf && atob(exports.pdf).startsWith('%PDF-') && /\/Subtype\s*\/Image/.test(atob(exports.pdf)) && /\/Type\s*\/Page\b/.test(atob(exports.pdf)));
    // EN: Keep the large scene short vertically to exercise real PDF tiling without exceeding the raster memory budget.
    // ZH: 大场景保持较小高度，在不超出栅格内存预算的前提下验证真实 PDF 分片。
    exports['single.pdf'] = exports.pdf;
    const far = newTextbox('Tiled PDF edge'); roots.push(far); TB[far].x = 9000; TB[far].y = 180; update();
    await exportAs('pdf', true);
    check('real wide PDF export produces multiple image pages', !!exports.pdf && (atob(exports.pdf).match(/\/Type\s*\/Page\b/g) || []).length > 1);
    check('exports complete without application alerts', alerts.length === 0, alerts);
    return { checks, exports, documentText, image };
}
