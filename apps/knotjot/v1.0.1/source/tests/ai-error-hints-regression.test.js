const assert = require('node:assert/strict');
const { test } = require('node:test');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// EN: Execute the actual renderer hint function without browser dependencies; only translation is an identity adapter.
// ZH: 不依赖浏览器执行实际渲染层提示函数，仅用原文适配替代翻译。
const html = fs.readFileSync(path.join(__dirname, '../index.html'), 'utf8');
const start = html.indexOf('function aiErrHint(msg){');
const end = html.indexOf('// EN: Display token throughput', start);
const context = { t: value => value };
vm.runInNewContext(html.slice(start, end), context);

// EN: Catch missing HTTP classes and distinguish actionable timeout, authentication, quota, model and parameter advice.
// ZH: 捕捉遗漏的 HTTP 类别，并区分超时、认证、额度、模型和参数的可操作建议。
for (const [raw, hint] of [
  ['HTTP 400 unsupported parameter stream_options', /参数/],
  ['HTTP 400 model does not exist', /模型名/],
  ['HTTP 401 Your API key has expired', /Key/],
  ['HTTP 403 forbidden', /权限/],
  ['HTTP 404 model not found', /Base URL.*模型名/],
  ['HTTP 408 Request Timeout', /超时.*稍后/],
  ['HTTP 429 insufficient_quota', /额度.*余额/],
  ['HTTP 429 rate limit', /频繁.*稍后/],
  ['等待首个响应超时', /超时.*网络.*模型/],
  ['响应流长时间无数据', /超时.*网络.*模型/],
  ['请求超过总时长限制', /超时.*网络.*模型/],
  ['AI_IDLE_TIMEOUT', /超时.*网络.*模型/]
]) {
  test(`AI advice explains ${raw}`, () => assert.match(context.aiErrHint(raw), hint));
}
test('every HTTP 5xx status receives server failure advice', () => {
  for (let status = 500; status <= 599; status++) assert.match(context.aiErrHint(`HTTP ${status} synthetic failure`), /服务端.*稍后/, String(status));
});
test('unrelated errors remain available without a misleading HTTP diagnosis', () => {
  assert.equal(context.aiErrHint('socket disconnected'), '');
});
