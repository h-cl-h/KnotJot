const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const out = path.resolve(process.env.KNOTJOT_AI_ACCEPTANCE_DIR || path.join(__dirname, '../../../../../reports/native-acceptance-2026-10-05/ai-controlled'));
const syntheticKey = 'synthetic-knotjot-acceptance-credential';
fs.mkdirSync(out, { recursive: true });

// EN: Launch three separate hidden Electron lifetimes against one fresh isolated profile to verify real restart persistence.
// ZH: 使用同一全新隔离配置启动三个独立隐藏 Electron 生命周期，验证真实进程重启后的持久化。
if (!process.versions.electron) {
  const { spawnSync } = require('node:child_process');
  const profile = fs.mkdtempSync(path.join(out, 'profile-'));
  const childEnv = { ...process.env, KNOTJOT_AI_ACCEPTANCE_DIR: out, KNOTJOT_AI_ACCEPTANCE_PROFILE: profile };
  delete childEnv.ELECTRON_RUN_AS_NODE;
  const stages = [];
  for (const stage of ['network', 'remembered-restart', 'session-restart']) {
    const child = spawnSync(require('electron'), [__filename, stage], {
      cwd: path.join(__dirname, '..'), windowsHide: true, encoding: 'utf8', timeout: 100000,
      env: childEnv
    });
    fs.writeFileSync(path.join(out, stage + '.log'), (child.stdout || '') + (child.stderr || ''));
    const reportFile = path.join(out, stage + '.json');
    const evidence = fs.existsSync(reportFile) ? JSON.parse(fs.readFileSync(reportFile, 'utf8')) : null;
    const passed = child.status === 0 && evidence && evidence.runId === path.basename(profile) && evidence.checks.length > 0 && evidence.checks.every(item => item.passed);
    stages.push({ stage, exitCode: child.status, passed: !!passed, error: child.error && child.error.message });
    console.log(stage, passed ? 'PASS' : 'FAIL');
  }
  const reports = stages.map(item => {
    const file = path.join(out, item.stage + '.json');
    return { ...item, evidence: fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null };
  });
  fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ scope: 'Synthetic local service; no external provider success claimed', stages: reports }, null, 2));
  process.exitCode = stages.every(item => item.passed) ? 0 : 1;
} else {
  const { app, BrowserWindow, safeStorage } = require('electron');
  const http = require('node:http');
  const stage = process.argv[2];
  const profile = path.resolve(process.env.KNOTJOT_AI_ACCEPTANCE_PROFILE);
  assert.ok(profile.startsWith(out + path.sep), 'profile must stay inside controlled evidence directory');
  app.setPath('userData', profile);
  process.env.KNOTJOT_INTEGRATION_TEST = '1';
  require('../main');
  const checks = [], requests = [], observed = [], blocked = [];
  let server, win;

  // EN: Retain each acceptance outcome without exposing credential values or stopping later independent checks.
  // ZH: 保存每项验收结果，不暴露凭据值，也不阻断后续独立检查。
  function check(name, passed, detail) {
    checks.push({ name, passed: !!passed, ...(detail === undefined ? {} : { detail }) });
  }
  // EN: Invoke production renderer functions inside the isolated real page with JSON-safe synthetic arguments.
  // ZH: 通过 JSON 安全的合成参数，在真实隔离页面中调用生产渲染函数。
  function render(fn, ...args) {
    return win.webContents.executeJavaScript(`(${fn.toString()})(...${JSON.stringify(args)})`);
  }
  // EN: Retry transient compositor capture failures without abandoning the independent transport and storage acceptance cases.
  // ZH: 重试临时合成器截图失败，不放弃独立传输与存储验收用例。
  async function screenshot(name) {
    for (let attempt = 0; attempt < 3; attempt++) {
      try { fs.writeFileSync(path.join(out, name), (await win.webContents.capturePage()).toPNG()); return; }
      catch (error) { if (attempt === 2) { check('capture ' + name, false, error.message); return; } }
      await new Promise(resolve => setTimeout(resolve, 200));
    }
  }
  // EN: Scan persisted files only within the newly created fixture profile for accidental plaintext synthetic credentials.
  // ZH: 仅在新建样本配置目录中扫描持久化文件，检查是否意外明文保存合成凭据。
  function plaintextFiles(dir) {
    const found = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const file = path.join(dir, entry.name);
      if (entry.isDirectory()) found.push(...plaintextFiles(file));
      else if (entry.isFile()) {
        try { if (fs.readFileSync(file).includes(Buffer.from(syntheticKey))) found.push(path.relative(profile, file)); } catch (_) {}
      }
    }
    return found;
  }

  // EN: Run actual preload/main transport, settings controls and Windows credential storage without opening a visible window.
  // ZH: 不打开可见窗口，验证真实预加载及主进程传输、设置控件和 Windows 凭据存储。
  app.whenReady().then(async () => {
    const watchdog = setTimeout(() => { console.error('controlled AI stage exceeded deadline'); app.exit(2); }, 90000);
    try {
      win = new BrowserWindow({ show: false, width: 1280, height: 960, webPreferences: { backgroundThrottling: false, contextIsolation: true, nodeIntegration: false, preload: path.join(__dirname, '../preload.js') } });
      // EN: Allow only the local fault fixture and its synthetic timeout host; record and reject any other network destination.
      // ZH: 仅允许本地故障样本及其合成超时域名，记录并拒绝任何其他网络目的地址。
      win.webContents.session.webRequest.onBeforeRequest({ urls: ['http://*/*', 'https://*/*'] }, (details, callback) => {
        const url = new URL(details.url), allowed = ['127.0.0.1', 'knotjot-timeout.invalid'].includes(url.hostname);
        (allowed ? observed : blocked).push({ url: details.url, method: details.method });
        callback({ cancel: !allowed });
      });
      await win.webContents.session.setProxy({ mode: 'direct' });
      await win.loadFile(path.join(__dirname, '../index.html'));
      // EN: Wait for the asynchronous production configuration restore before reading session state after restart.
      // ZH: 重启后读取会话状态前，等待生产配置异步恢复完成。
      await render(async restart => {
        if (restart) {
          const started = Date.now();
          while (!aiCfgGet().model && Date.now() - started < 5000) await new Promise(resolve => setTimeout(resolve, 20));
        }
        LANG = 'zh';
        openAIChat('new'); aiOpenCfg();
      }, stage !== 'network');

      if (stage === 'network') {
        // EN: Serve literal successful SSE/model fixtures, HTTP failures, rejected optional parameters and a deliberately stalled response.
        // ZH: 提供固定成功 SSE 和模型样本、HTTP 故障、可选参数拒绝以及故意停滞的响应。
        server = http.createServer((req, res) => {
          let body = '';
          req.on('data', chunk => { body += chunk; });
          req.on('end', () => {
            const url = new URL(req.url, 'http://' + req.headers.host), payload = body ? JSON.parse(body) : {};
            requests.push({ url: url.href, method: req.method, model: payload.model, stream: payload.stream, optionalParameters: Object.keys(payload).filter(key => !['model', 'messages', 'stream'].includes(key)), authorizationPresent: !!req.headers.authorization, syntheticAuthorizationMatches: req.headers.authorization === 'Bearer ' + syntheticKey });
            if (url.hostname === 'knotjot-timeout.invalid') return;
            const status = /\/status-(\d+)/.exec(url.pathname);
            if (status || url.pathname.includes('/retry-always/') || (url.pathname.includes('/retry-once/') && payload.stream_options)) {
              const code = status ? +status[1] : 400;
              res.writeHead(code, { 'Content-Type': 'application/json' });
              const message = status ? (code === 401 ? 'Your API key has expired' : code === 429 ? 'rate limit' : 'synthetic service failure') : 'unsupported parameter stream_options';
              res.end(JSON.stringify({ error: { message } }));
            } else if (url.pathname.endsWith('/models')) {
              res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ data: [{ id: 'controlled-text-model' }, { id: 'controlled-embedding' }] }));
            } else {
              res.writeHead(200, { 'Content-Type': 'text/event-stream' });
              res.end('data: {"choices":[{"delta":{"content":"controlled fixture response"}}]}\n\ndata: [DONE]\n\n');
            }
          });
        });
        await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
        const origin = `http://127.0.0.1:${server.address().port}`;
        const presets = await render(() => {
          const results = [];
          document.getElementById('aiBaseUrl').value = 'https://previous.invalid/v1'; aiUpdateKeyDestination();
          for (const button of document.querySelectorAll('#aiPreset button')) {
            button.click();
            results.push({ id: button.dataset.preset, base: document.getElementById('aiBaseUrl').value, model: document.getElementById('aiModel').value, hint: document.getElementById('aiCfgStatus').textContent, destination: document.getElementById('aiKeyDestination').textContent });
          }
          return results;
        });
        const expected = {
          ollama: ['http://localhost:11434/v1', 'qwen2.5:7b', false], lmstudio: ['http://localhost:1234/v1', '', false],
          deepseek: ['https://api.deepseek.com/v1', 'deepseek-chat', true], openai: ['https://api.openai.com/v1', 'gpt-4o-mini', true],
          siliconflow: ['https://api.siliconflow.cn/v1', 'Qwen/Qwen2.5-7B-Instruct', true]
        };
        for (const preset of presets) {
          const want = expected[preset.id];
          check('preset ' + preset.id + ' fields and key guidance', preset.base === want[0] && preset.model === want[1] && (want[2] ? /需要 API Key/.test(preset.hint) : /无需 Key/.test(preset.hint)), preset);
          check('preset ' + preset.id + ' refreshes credential destination', preset.destination.includes(new URL(want[0]).host), preset.destination);
        }
        check('preset changes do not send requests before an explicit action', requests.length === 0 && observed.length === 0);
        await screenshot('preset-destination.png');

        // EN: Test configuration save, probe, model selection and chat against literal expected normalized local destinations.
        // ZH: 对照固定预期的规范本地地址，验证配置保存、探测、模型选择和聊天。
        for (const [suffix, root] of [['', '/v1'], ['/v1/', '/v1'], ['/v1/chat/completions', '/v1'], ['/custom/api/v4/', '/custom/api/v4'], ['/v1/models', '/v1']]) {
          const start = requests.length;
          const result = await render(async base => {
            aiOpenCfg();
            document.getElementById('aiBaseUrl').value = base; document.getElementById('aiBaseUrl').dispatchEvent(new Event('input', { bubbles: true })); document.getElementById('aiModel').value = 'controlled-text-model';
            document.getElementById('aiKey').value = ''; document.getElementById('aiRememberKey').checked = false;
            await aiSaveCfg(); aiOpenCfg(); await aiTestConn();
            const probe = document.getElementById('aiCfgStatus').textContent;
            await aiListModelsFn();
            document.querySelector('#aiModelChips .mchip').click();
            const selected = document.getElementById('aiModel').value;
            const chat = await aiChatStream([{ role: 'user', content: 'synthetic acceptance request' }]);
            return { probe, selected, chat, savedBase: aiCfgGet().baseUrl };
          }, origin + suffix);
          const actual = requests.slice(start);
          check('normalized endpoint ' + (suffix || '<host>'), result.savedBase === origin + suffix && /成功/.test(result.probe) && result.selected === 'controlled-text-model' && result.chat === 'controlled fixture response' && actual.length === 3 && actual[0].url === origin + root + '/chat/completions' && actual[1].url === origin + root + '/models' && actual[2].url === origin + root + '/chat/completions', { result, requests: actual });
        }
        for (const kind of ['retry-once', 'retry-always']) {
          const start = requests.length;
          const result = await render(async base => {
            await aiCfgSet({ baseUrl: base, model: 'controlled-text-model', apiKey: '', rememberKey: false });
            try { return { text: await aiChatStream([{ role: 'user', content: 'synthetic retry request' }]) }; }
            catch (error) { return { error: error.message }; }
          }, origin + '/' + kind);
          const actual = requests.slice(start);
          check(kind + ' retries exactly once with optional parameters removed', actual.length === 2 && actual[0].optionalParameters.includes('stream_options') && actual[1].optionalParameters.length === 0 && (kind === 'retry-once' ? result.text === 'controlled fixture response' : /400/.test(result.error)), { result, requests: actual });
        }
        const noRetryStart = requests.length;
        const noRetry = await render(async base => {
          await aiCfgSet({ baseUrl: base, model: 'controlled-text-model', apiKey: '', rememberKey: false });
          try { await aiChatStream([{ role: 'user', content: 'synthetic unauthorized request' }]); return ''; }
          catch (error) { return error.message; }
        }, origin + '/status-401');
        check('authentication failure does not trigger a parameter retry', /HTTP 401/.test(noRetry) && requests.length === noRetryStart + 1);

        // EN: Require actionable advice in the real displayed connection status in both supported interface languages.
        // ZH: 要求两种界面语言的真实连接状态均显示可操作建议。
        for (const language of ['zh', 'en']) {
          for (const status of [400, 401, 403, 404, 408, 429, 500, 501, 502, 503, 504, 599]) {
            const result = await render(async (base, lang) => {
              LANG = lang; aiOpenCfg(); document.getElementById('aiBaseUrl').value = base; document.getElementById('aiBaseUrl').dispatchEvent(new Event('input', { bubbles: true })); document.getElementById('aiModel').value = 'controlled-text-model';
              await aiTestConn(); return { text: document.getElementById('aiCfgStatus').textContent, className: document.getElementById('aiCfgStatus').className };
            }, origin + '/status-' + status, language);
            const tail = result.text.split(' — ')[1] || '';
            check(language + ' HTTP ' + status + ' displays targeted advice', result.className.includes('err') && result.text.includes('HTTP ' + status) && tail.length > 0 && (language === 'zh' || !/[\u3400-\u9fff]/.test(tail)), result);
          }
        }
        // EN: Exercise the unmodified 30-second remote first-response deadline through a local proxy that intentionally sends no headers.
        // ZH: 通过故意不返回响应头的本地代理，验证未修改的远端 30 秒首响应超时。
        await win.webContents.session.setProxy({ proxyRules: `http=127.0.0.1:${server.address().port}`, proxyBypassRules: '<-loopback>' });
        const timeout = await render(async () => {
          LANG = 'zh'; aiOpenCfg(); document.getElementById('aiBaseUrl').value = 'http://knotjot-timeout.invalid/v1';
          document.getElementById('aiBaseUrl').dispatchEvent(new Event('input', { bubbles: true }));
          const started = Date.now(); await aiTestConn();
          return { elapsedMs: Date.now() - started, text: document.getElementById('aiCfgStatus').textContent };
        });
        check('real first-response timeout displays advice and releases request', timeout.elapsedMs >= 25000 && timeout.elapsedMs < 45000 && /超时.*网络.*模型/.test(timeout.text), timeout);
        await screenshot('timeout-advice.png');
        await win.webContents.session.setProxy({ mode: 'direct' });

        // EN: Save only a synthetic remembered key through the same controls a user operates, then prove actual header delivery locally.
        // ZH: 通过用户使用的同一控件只保存合成记住密钥，再在本地验证实际请求头传递。
        const remember = await render(async (base, key) => {
          await aiCfgSet({ baseUrl: base, model: 'controlled-text-model', apiKey: '', rememberKey: false }); aiOpenCfg();
          document.getElementById('aiKey').value = key; document.getElementById('aiRememberKey').checked = true;
          await aiSaveCfg(); const result = { keyStored: aiKeyStored, sessionMatches: aiSessionKey === key, publicConfigHasKey: Object.hasOwn(JSON.parse(localStorage.getItem('bmap.ai')), 'apiKey') };
          aiOpenCfg(); await aiTestConn(); return result;
        }, origin + '/remembered', syntheticKey);
        check('synthetic credential is encrypted and active in the same session', safeStorage.isEncryptionAvailable() && remember.keyStored && remember.sessionMatches && !remember.publicConfigHasKey && fs.existsSync(path.join(profile, 'ai-secret.bin')) && requests.at(-1).syntheticAuthorizationMatches, remember);
        const publicConfig = JSON.parse(fs.readFileSync(path.join(profile, 'ai-config.json'), 'utf8'));
        check('public disk settings contain no API key field', !Object.hasOwn(publicConfig, 'apiKey'));
        // EN: Chromium may replay an HTTP 408 internally, so compare destinations rather than assuming one wire request per observer event.
        // ZH: Chromium 可能内部重发 HTTP 408，因此比较目的地址，不假定一次观察事件只对应一个线上请求。
        check('all network traffic targets explicitly configured controlled endpoints', blocked.length === 0 && observed.length > 0 && requests.every(request => observed.some(item => item.url === request.url && item.method === request.method)), { observedCount: observed.length, requestCount: requests.length, blocked });
      } else if (stage === 'remembered-restart') {
        const restored = await render(key => ({ sessionMatches: aiSessionKey === key, keyStored: aiKeyStored, rememberedControl: document.getElementById('aiRememberKey').checked, publicConfigHasKey: Object.hasOwn(JSON.parse(localStorage.getItem('bmap.ai')), 'apiKey') }), syntheticKey);
        check('remembered synthetic key survives full process restart', restored.sessionMatches && restored.keyStored && restored.rememberedControl && !restored.publicConfigHasKey, restored);
        const clear = await render(async key => {
          document.getElementById('aiRememberKey').checked = false; await aiSaveCfg();
          const disk = JSON.parse(await api.loadAIConf());
          return { sessionMatches: aiSessionKey === key, keyStored: aiKeyStored, diskKeyAbsent: !disk.apiKey };
        }, syntheticKey);
        check('session-only save keeps memory key and removes encrypted saved key', clear.sessionMatches && !clear.keyStored && clear.diskKeyAbsent && !fs.existsSync(path.join(profile, 'ai-secret.bin')), clear);
      } else {
        const restored = await render(() => ({ sessionEmpty: aiSessionKey === '', keyStored: aiKeyStored, model: aiCfgGet().model, keyInputEmpty: document.getElementById('aiKey').value === '' }));
        check('session-only key does not return after full process restart', restored.sessionEmpty && !restored.keyStored && restored.keyInputEmpty && restored.model === 'controlled-text-model', restored);
      }
      await win.webContents.session.flushStorageData();
      const leaks = plaintextFiles(profile);
      check('fixture profile has no plaintext synthetic credential', leaks.length === 0, leaks);
    } catch (error) { check('unexpected stage error', false, error.stack); }
    finally {
      clearTimeout(watchdog);
      fs.writeFileSync(path.join(out, stage + '.json'), JSON.stringify({ stage, runId: path.basename(profile), electron: process.versions.electron, checks, requests, observed, blocked }, null, 2));
      const failed = checks.filter(item => !item.passed);
      console.log(JSON.stringify({ stage, passed: checks.length - failed.length, failed }));
      if (server) { server.closeAllConnections(); server.close(); }
      if (win && !win.isDestroyed()) win.destroy();
      // EN: Clean application shutdown persists Chromium's encryption state for the next real process lifetime.
      // ZH: 正常退出应用以持久化 Chromium 加密状态，供下一个真实进程生命周期使用。
      process.exitCode = failed.length ? 1 : 0;
      app.quit();
    }
  });
}
