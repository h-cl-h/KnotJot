const assert = require('node:assert/strict');
const { test } = require('node:test');
const AI = require('../src/ai-client');

// EN: Accept common provider addresses without duplicating endpoint suffixes; keep custom API paths.
// ZH: 接受常见服务地址且不重复拼接端点后缀，保留自定义接口路径。
test('provider URLs accept host, version root, and pasted completion endpoint', () => {
  assert.equal(AI.endpointUrl('https://example.test', 'chat/completions'), 'https://example.test/v1/chat/completions');
  assert.equal(AI.endpointUrl('https://example.test/v1/', 'models'), 'https://example.test/v1/models');
  assert.equal(AI.endpointUrl('https://example.test/v1/chat/completions', 'chat/completions'), 'https://example.test/v1/chat/completions');
  assert.equal(AI.endpointUrl('https://example.test/v1/chat/completions', 'models'), 'https://example.test/v1/models');
  assert.equal(AI.endpointUrl('http://localhost:11434/v1', 'models'), 'http://localhost:11434/v1/models');
  assert.equal(AI.endpointUrl('https://example.test/custom/api/v4', 'chat/completions'), 'https://example.test/custom/api/v4/chat/completions');
  assert.throws(() => AI.endpointUrl('file:///secret', 'models'), /HTTP/);
});

// EN: Local inference receives a loading allowance while remote requests retain bounded deadlines.
// ZH: 为本地推理提供加载等待时间，同时保留远程请求的有限超时。
test('local model deadlines allow loading and probe streaming', () => {
  assert.equal(AI.requestTimeouts('http://localhost:11434/v1', true).firstByte, 120000);
  assert.equal(AI.requestTimeouts('http://127.0.0.1:1234/v1').idle, 120000);
  assert.ok(AI.requestTimeouts('https://example.test', true).total <= 90000);
});
