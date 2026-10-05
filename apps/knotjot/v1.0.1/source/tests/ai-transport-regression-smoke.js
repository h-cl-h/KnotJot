const { app, BrowserWindow } = require('electron');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const out = path.resolve(__dirname, '../../preview/testing/ai');
fs.mkdirSync(out, { recursive: true });
app.setPath('userData', path.join(out, 'profile'));
process.env.KNOTJOT_INTEGRATION_TEST = '1';
require('../main');

// EN: Serve an isolated HTTP proxy to prove that desktop AI uses the Electron session network settings.
// ZH: 提供隔离的 HTTP 代理，验证桌面 AI 使用 Electron 会话网络设置。
const requests = [];
const closedStreams = [];
const proxy = http.createServer((req, res) => {
  let body = '';
  req.on('data', chunk => { body += chunk; });
  req.on('end', () => {
    const payload = body ? JSON.parse(body) : null;
    requests.push({ url: req.url, method: req.method, body: payload });
    if (req.url.endsWith('/models')) { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ data: [{ id: 'local-test' }] })); }
    else if (req.url.includes('/html/')) { res.setHeader('Content-Type', 'text/html'); res.end('<html>Sign in</html>'); }
    else if (payload && payload.probeCase) {
      // EN: Leave selected synthetic SSE streams open so prompt client cancellation is observable, and delay errors/output past metadata events.
      // ZH: 保持指定合成 SSE 流打开以观察客户端及时取消，并在元数据事件之后延迟发送错误或内容。
      const kind = payload.probeCase;
      const send = value => res.write('data: ' + (typeof value === 'string' ? value : JSON.stringify(value)) + '\n\n');
      res.setHeader('Content-Type', 'text/event-stream');
      res.on('close', () => closedStreams.push(kind));
      if (kind === 'done-open') send('[DONE]');
      else if (kind === 'content-open') send({ choices: [{ delta: { content: 'pong' } }] });
      else if (kind === 'completed-open') send({ choices: [{ delta: {}, finish_reason: 'stop' }] });
      else if (kind === 'empty') { send({ choices: [{ delta: {} }] }); send('[DONE]'); res.end(); }
      else {
        send({ choices: [{ delta: { role: 'assistant' } }] });
        const delayed = setTimeout(() => {
          if (kind === 'role-error') send({ error: { message: 'synthetic model failed to load' } });
          else if (kind === 'reasoning-open') send({ choices: [{ delta: { reasoning_content: 'thinking' } }] });
          else { send('[DONE]'); res.end(); }
        }, 30);
        res.on('close', () => clearTimeout(delayed));
      }
    }
    else if (body && JSON.parse(body).stream) { res.setHeader('Content-Type', 'text/event-stream'); res.end('data: {"choices":[{"delta":{"content":"本地代理测试成功"}}]}\n\ndata: [DONE]\n\n'); }
    else { res.setHeader('Content-Type', 'application/json'); res.end('{"choices":[{"message":{"content":"pong"}}]}'); }
  });
});

// EN: Exercise real preload IPC against an isolated proxy, retaining only synthetic request data and results.
// ZH: 通过隔离代理验证真实预加载 IPC，仅保留合成请求与结果。
app.whenReady().then(async () => {
  const timer = setTimeout(() => { console.error('AI smoke timed out'); app.exit(1); }, 45000);
  try {
    await new Promise(resolve => proxy.listen(0, '127.0.0.1', resolve));
    const win = new BrowserWindow({ show: false, webPreferences: { contextIsolation: true, nodeIntegration: false, preload: path.join(__dirname, '../preload.js') } });
    await win.loadFile(path.join(__dirname, '../index.html'));
    await win.webContents.session.setProxy({ proxyRules: `http=127.0.0.1:${proxy.address().port}`, proxyBypassRules: '<-loopback>' });
    const result = await win.webContents.executeJavaScript(`(async () => {
      const base='http://knotjot-test.invalid/v1', headers={'Content-Type':'application/json'};
      const body=JSON.stringify({model:'local-test',messages:[{role:'user',content:'ping'}],stream:false});
      const probe=await api.aiProbe({url:base+'/chat/completions',headers,body});
      const streamProbe=await api.aiProbe({url:base+'/chat/completions',headers,body:JSON.stringify({model:'local-test',messages:[{role:'user',content:'ping'}],stream:true})});
      const models=await api.aiModels({url:base+'/models',headers:{}});
      const html=await api.aiProbe({url:'http://knotjot-test.invalid/html/chat/completions',headers,body});
      // EN: Drive delayed metadata and intentionally open responses through the real preload bridge, measuring termination latency.
      // ZH: 通过真实预加载桥接处理延迟元数据及故意保持打开的响应，并测量终止延迟。
      const streamCases={};
      for(const probeCase of ['role-only','empty','role-error','content-open','reasoning-open','completed-open','done-open']){
        const started=Date.now();
        const response=await api.aiProbe({url:base+'/chat/completions',headers,body:JSON.stringify({model:'local-test',stream:true,probeCase}),requestId:'transport-'+probeCase});
        streamCases[probeCase]={response,elapsedMs:Date.now()-started};
      }
      let text='';
      if(probe.ok) await aiChatStreamIPC({url:base+'/chat/completions',headers,body:JSON.stringify({model:'local-test',messages:[{role:'user',content:'ping'}],stream:true})}, d=>text+=d);
      return {probe,streamProbe,models,html,streamCases,text};
    })()`);
    await new Promise(resolve => setTimeout(resolve, 60));
    fs.writeFileSync(path.join(out, 'transport-result.json'), JSON.stringify({ result, requests, closedStreams }, null, 2));
    assert.equal(result.probe.ok, true, 'AI must use the configured Electron proxy');
    assert.equal(result.models.data.data[0].id, 'local-test');
    assert.equal(result.streamProbe.ok, true, 'streaming model probes must finish at the first valid event');
    assert.equal(result.html.ok, false, 'HTML login pages must not report successful model connection');
    // EN: Require evidence of inference rather than a role header, and ensure DONE/error/first output close the remaining response promptly.
    // ZH: 成功必须有推理证据而非角色头；DONE、错误或首个输出均应及时关闭剩余响应。
    for (const [kind, entry] of Object.entries(result.streamCases)) {
      assert.equal(entry.response.ok, ['content-open', 'reasoning-open', 'completed-open'].includes(kind), kind);
      assert.ok(entry.elapsedMs < 2500, kind + ' must terminate without waiting for stream EOF or timeout');
      assert.ok(closedStreams.includes(kind), kind + ' response body must be closed');
    }
    assert.equal(result.streamCases['role-error'].response.error, 'synthetic model failed to load');
    assert.equal(result.streamCases['done-open'].response.error, '接口没有返回有效的聊天事件');
    assert.equal(result.text, '本地代理测试成功');
    console.log('AI_TRANSPORT_PASS', JSON.stringify(result));
    clearTimeout(timer); proxy.close(); app.exit(0);
  } catch (err) { console.error(err.stack); clearTimeout(timer); proxy.close(); app.exit(1); }
});
