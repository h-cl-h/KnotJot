(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotAIClient = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const DEFAULT_TIMEOUTS = Object.freeze({ connect: 20000, firstByte: 30000, idle: 45000, total: 300000, probe: 15000, models: 15000 });
  function timeoutError(kind) { const e = new Error(kind === 'firstByte' ? '等待首个响应超时' : kind === 'idle' ? '响应流长时间无数据' : kind === 'total' ? '请求超过总时长限制' : '连接超时'); e.code = `AI_${kind.toUpperCase()}_TIMEOUT`; return e; }
  function createDeadlineController(parentSignal, limits = {}) {
    const cfg = Object.assign({}, DEFAULT_TIMEOUTS, limits), controller = new AbortController();
    let reason = null, first = false, firstTimer = 0, idleTimer = 0, totalTimer = 0;
    const abort = kind => { if (controller.signal.aborted) return; reason = timeoutError(kind); controller.abort(reason); };
    if (parentSignal) { if (parentSignal.aborted) controller.abort(parentSignal.reason); else parentSignal.addEventListener('abort', () => controller.abort(parentSignal.reason), { once: true }); }
    firstTimer = setTimeout(() => abort('firstByte'), cfg.firstByte || cfg.connect);
    totalTimer = setTimeout(() => abort('total'), cfg.total);
    const touch = () => { if (!first) { first = true; clearTimeout(firstTimer); } clearTimeout(idleTimer); idleTimer = setTimeout(() => abort('idle'), cfg.idle); };
    const finish = () => { clearTimeout(firstTimer); clearTimeout(idleTimer); clearTimeout(totalTimer); };
    return { controller, signal: controller.signal, touch, finish, getError: () => reason };
  }
  function contentType(response) { return String(response && response.headers && response.headers.get && response.headers.get('content-type') || '').toLowerCase(); }
  function extractJsonContent(json) { return String(((((json || {}).choices || [])[0] || {}).message || {}).content || ((((json || {}).choices || [])[0] || {}).text || '')); }
  function sseLines(state, chunk, onEvent, flush) {
    state.buf = (state.buf || '') + String(chunk || '');
    const lines = state.buf.split(/\r?\n/); state.buf = flush ? '' : lines.pop();
    for (let line of lines) { line = line.trim(); if (!line || line[0] === ':' || !line.startsWith('data:')) continue; const data = line.slice(5).trim(); if (data === '[DONE]') { state.done = true; continue; } try { onEvent(JSON.parse(data)); } catch (_) {} }
  }
  return { DEFAULT_TIMEOUTS, createDeadlineController, contentType, extractJsonContent, sseLines, timeoutError };
});
