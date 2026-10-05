const { app, BrowserWindow } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const out = path.resolve(__dirname, '../../preview/testing/ai');
fs.mkdirSync(out, { recursive: true });
app.setPath('userData', path.join(out, 'local-probe-profile'));
process.env.KNOTJOT_INTEGRATION_TEST = '1';
require('../main');

// EN: Verify the already configured local Ollama model with synthetic text, without reading or copying credentials.
// ZH: 使用合成文本验证已配置的本机 Ollama 模型，不读取或复制凭据。
app.whenReady().then(async () => {
  const timer = setTimeout(() => app.exit(1), 310000);
  try {
    const win = new BrowserWindow({ show: false, webPreferences: { preload: path.join(__dirname, '../preload.js'), contextIsolation: true, nodeIntegration: false } });
    await win.loadFile(path.join(__dirname, '../index.html'));
    const started = Date.now();
    const result = await win.webContents.executeJavaScript(`(async () => {
      const models=await api.aiModels({url:'http://localhost:11434/v1/models',headers:{}});
      const probe=await api.aiProbe({url:'http://localhost:11434/v1/chat/completions',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'deepseek-r1:32b',messages:[{role:'user',content:'Reply with OK.'}],stream:true})});
      return {models:models.ok,modelPresent:!!models.data?.data?.some(m=>m.id==='deepseek-r1:32b'),probe};
    })()`);
    result.durationMs = Date.now() - started;
    fs.writeFileSync(path.join(out, 'actual-local-model-result.json'), JSON.stringify(result, null, 2));
    console.log('LOCAL_MODEL_RESULT', JSON.stringify(result));
    clearTimeout(timer); app.exit(result.probe.ok ? 0 : 1);
  } catch (err) { console.error(err.message); clearTimeout(timer); app.exit(1); }
});
