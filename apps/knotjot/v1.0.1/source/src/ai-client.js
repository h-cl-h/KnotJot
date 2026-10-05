(function (root, factory) {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: call factory; update module.exports, root.KnotJotAIClient.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：调用 factory；更新 module.exports、root.KnotJotAIClient。 */

  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotAIClient = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: call Object.freeze.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：调用 Object.freeze。 */

  'use strict';
  function endpointUrl(value, endpoint) {
    /* EN: Normalize an OpenAI-compatible root or pasted endpoint while preserving custom API paths.
       ZH: 规范 OpenAI 兼容接口根地址或粘贴的完整端点，并保留自定义接口路径。 */
    let url;
    try { url = new URL(String(value || '').trim()); } catch (_) { throw new Error('请填写完整的 HTTP 或 HTTPS 接口地址'); }
    if (!['http:', 'https:'].includes(url.protocol)) throw new Error('AI 接口只支持 HTTP 或 HTTPS');
    let base = url.pathname.replace(/\/+$/, '').replace(/\/(?:chat\/completions|models)$/, '');
    if (!base) base = '/v1';
    url.pathname = base + '/' + endpoint;
    url.hash = '';
    return url.href;
  }
  const DEFAULT_TIMEOUTS = Object.freeze({ connect: 20000, firstByte: 30000, idle: 45000, total: 300000, probe: 15000, models: 15000 });
  function requestTimeouts(url, probe = false) {
    /* EN: Allow loopback model loading without treating slow local inference as a broken connection.
       ZH: 给本机模型加载留出时间，避免把本地推理较慢误判为连接故障。 */
    let host = '';
    try { host = new URL(url).hostname.toLowerCase(); } catch (_) {}
    const local = ['localhost', '127.0.0.1', '[::1]'].includes(host);
    return local ? { firstByte: 120000, idle: 120000, total: 300000 }
      : probe ? { firstByte: 30000, idle: 45000, total: 90000 } : DEFAULT_TIMEOUTS;
  }
  function timeoutError(kind) {
/* EN: Build a timeout error with a stable code for the expired request phase.
   ZH: 为超时的请求阶段创建带稳定错误码的异常。 */
 const e = new Error(kind === 'firstByte' ? '等待首个响应超时' : kind === 'idle' ? '响应流长时间无数据' : kind === 'total' ? '请求超过总时长限制' : '连接超时'); e.code = `AI_${kind.toUpperCase()}_TIMEOUT`; return e; }
  function createDeadlineController(parentSignal, limits = {}) {
/* EN: Coordinate first-byte, idle and total request deadlines with cancellation and timer cleanup.
   ZH: 统一管理首响应、空闲和总请求超时，以及取消和计时器清理。 */

    const cfg = Object.assign({}, DEFAULT_TIMEOUTS, limits), controller = new AbortController();
    let reason = null, first = false, firstTimer = 0, idleTimer = 0, totalTimer = 0;
    const abort = kind => {
/* EN: abort in createDeadlineController: call timeoutError, controller.abort; update reason.
   ZH: createDeadlineController 中的 abort：调用 timeoutError、controller.abort；更新 reason。 */
 if (controller.signal.aborted) return; reason = timeoutError(kind); controller.abort(reason); };
    if (parentSignal) { if (parentSignal.aborted) controller.abort(parentSignal.reason); else parentSignal.addEventListener('abort', () => /* EN: Handle abort on parentSignal.addEventListener: call controller.abort; return controller.abort(parentSignal.reason). ZH: 处理 parentSignal.addEventListener 的 abort 事件或通道：调用 controller.abort；返回 controller.abort(parentSignal.reason)。 */ controller.abort(parentSignal.reason), { once: true }); }
    firstTimer = setTimeout(() => /* EN: Callback for setTimeout: call abort; return abort('firstByte'). ZH: setTimeout 的回调：调用 abort；返回 abort('firstByte')。 */ abort('firstByte'), cfg.firstByte || cfg.connect);
    totalTimer = setTimeout(() => /* EN: Callback for setTimeout: call abort; return abort('total'). ZH: setTimeout 的回调：调用 abort；返回 abort('total')。 */ abort('total'), cfg.total);
    const touch = () => {
/* EN: touch in createDeadlineController: call clearTimeout, setTimeout; update first, idleTimer.
   ZH: createDeadlineController 中的 touch：调用 clearTimeout、setTimeout；更新 first、idleTimer。 */
 if (!first) { first = true; clearTimeout(firstTimer); } clearTimeout(idleTimer); idleTimer = setTimeout(() => /* EN: Callback for setTimeout: call abort; return abort('idle'). ZH: setTimeout 的回调：调用 abort；返回 abort('idle')。 */ abort('idle'), cfg.idle); };
    const finish = () => {
/* EN: finish in createDeadlineController: call clearTimeout.
   ZH: createDeadlineController 中的 finish：调用 clearTimeout。 */
 clearTimeout(firstTimer); clearTimeout(idleTimer); clearTimeout(totalTimer); };
    return { controller, signal: controller.signal, touch, finish, getError: () => /* EN: getError in createDeadlineController: return reason. ZH: createDeadlineController 中的 getError：返回 reason。 */ reason };
  }
  function contentType(response) {
/* EN: Extract the compatible response metadata or completion text used by the AI transport.
   ZH: 提取 AI 传输层使用的兼容响应元数据或生成文本。 */
 return String(response && response.headers && response.headers.get && response.headers.get('content-type') || '').toLowerCase(); }
  function extractJsonContent(json) {
/* EN: Extract the compatible response metadata or completion text used by the AI transport.
   ZH: 提取 AI 传输层使用的兼容响应元数据或生成文本。 */
 return String(((((json || {}).choices || [])[0] || {}).message || {}).content || ((((json || {}).choices || [])[0] || {}).text || '')); }
  function sseLines(state, chunk, onEvent, flush) {
/* EN: Buffer incomplete SSE lines and dispatch parsed data events while recognizing the completion marker.
   ZH: 缓冲不完整的 SSE 行，解析并分发数据事件，同时识别完成标记。 */

    state.buf = (state.buf || '') + String(chunk || '');
    const lines = state.buf.split(/\r?\n/); state.buf = flush ? '' : lines.pop();
    for (let line of lines) { line = line.trim(); if (!line || line[0] === ':' || !line.startsWith('data:')) continue; const data = line.slice(5).trim(); if (data === '[DONE]') { state.done = true; continue; } try { onEvent(JSON.parse(data)); } catch (_) {} }
  }
  return { DEFAULT_TIMEOUTS, createDeadlineController, contentType, extractJsonContent, sseLines, timeoutError, endpointUrl, requestTimeouts };
});
