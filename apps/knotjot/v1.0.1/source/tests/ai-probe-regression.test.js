const assert = require('node:assert/strict');
const { test } = require('node:test');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const AIClient = require('../src/ai-client');
const main = fs.readFileSync(path.join(__dirname, '../main.js'), 'utf8');
const start = main.indexOf("ipcMain.handle('ai-probe',");
const source = main.slice(start, main.indexOf('// 列出端点可用模型', start));

// EN: Exercise the actual IPC probe handler with deterministic stream reads and real bounded deadlines, without network traffic.
// ZH: 使用确定的流读取及真实有限超时验证实际 IPC 探测处理器，不产生网络请求。
async function probe(frames, holdOpen = false) {
  let handler, signal, reads = 0, cancelled = 0, finished = 0;
  const pending = new Map();
  const context = {
    ipcMain: { handle: (_, callback) => { handler = callback; } },
    TextDecoder, Uint8Array, aiUtilityReqs: pending,
    AIClient: { ...AIClient, requestTimeouts: () => ({ firstByte: 100, idle: 100, total: 300 }),
      createDeadlineController: (...args) => {
        const deadline = AIClient.createDeadlineController(...args), finish = deadline.finish;
        deadline.finish = () => { finished++; finish(); };
        return deadline;
      } },
    aiFetch: async (_, options) => {
      signal = options.signal;
      return { ok: true, headers: { get: () => 'text/event-stream' }, body: { getReader: () => ({
        read: async () => {
          const frame = frames[reads++];
          if (frame !== undefined) return { value: new TextEncoder().encode(frame), done: false };
          if (!holdOpen) return { done: true };
          return new Promise((_, reject) => signal.addEventListener('abort', () => reject(signal.reason), { once: true }));
        },
        cancel: async () => { cancelled++; }
      }) } };
    }
  };
  vm.runInNewContext(source, context);
  const result = JSON.parse(JSON.stringify(await handler({}, { url: 'https://example.test/v1/chat/completions', headers: {}, body: '{}', requestId: 'test-probe' })));
  assert.equal(pending.size, 0, 'probe registration must be removed');
  assert.ok(finished > 0, 'all deadline timers must be released');
  assert.equal(cancelled, 1, 'probe must cancel its remaining response body');
  return { result, reads };
}

// EN: Encode synthetic provider events while preserving a literal completion marker for the stream termination cases.
// ZH: 编码合成服务事件，为流终止用例保留原样完成标记。
const event = value => 'data: ' + (typeof value === 'string' ? value : JSON.stringify(value)) + '\n\n';
const choice = value => event({ choices: [value] });

// EN: Cover protocol metadata, real model output, upstream failures and termination independently of the desktop transport.
// ZH: 分别覆盖协议元数据、真实模型输出、上游故障与终止，不依赖桌面传输环境。
const cases = [
  { name: 'role-only completion is not successful', frames: [choice({ delta: { role: 'assistant' } }), event('[DONE]')], ok: false, reads: 2 },
  { name: 'empty delta is not successful', frames: [choice({ delta: {} }), event('[DONE]')], ok: false, reads: 2 },
  { name: 'whitespace output is not successful', frames: [choice({ delta: { content: '   ' } }), event('[DONE]')], ok: false, reads: 2 },
  { name: 'role event waits for actual content', frames: [choice({ delta: { role: 'assistant' } }), choice({ delta: { content: 'pong' } })], ok: true, reads: 2 },
  { name: 'reasoning content proves successful generation', frames: [choice({ delta: { reasoning_content: 'thinking' } })], ok: true, reads: 1 },
  { name: 'reasoning field proves successful generation', frames: [choice({ delta: { reasoning: 'thinking' } })], ok: true, reads: 1 },
  { name: 'completed message proves successful generation', frames: [choice({ message: { content: 'pong' } })], ok: true, reads: 1 },
  { name: 'legacy completion text proves successful generation', frames: [choice({ text: 'pong' })], ok: true, reads: 1 },
  { name: 'valid empty completion result is successful', frames: [choice({ delta: {}, finish_reason: 'stop' })], ok: true, reads: 1 },
  { name: 'unrecognized finish reason is not successful', frames: [choice({ delta: {}, finish_reason: 'broken' }), event('[DONE]')], ok: false, reads: 2 },
  { name: 'role then model error preserves the upstream message', frames: [choice({ delta: { role: 'assistant' } }), event({ error: { message: 'model failed to load' } })], ok: false, error: 'model failed to load', reads: 2 },
  { name: 'string stream errors preserve the upstream message', frames: [event({ error: 'model unavailable' })], ok: false, error: 'model unavailable', reads: 1 },
  { name: 'error dominates output in the same packet', frames: [choice({ delta: { content: 'pong' } }) + event({ error: { message: 'generation failed' } })], ok: false, error: 'generation failed', reads: 1 },
  { name: 'DONE exits immediately even if server keeps connection open', frames: [event('[DONE]')], holdOpen: true, ok: false, error: '接口没有返回有效的聊天事件', reads: 1 },
  { name: 'split SSE frames are buffered until a complete event', frames: ['data: {"choices":[{"delta":', '{"content":"pong"}}]}\n\n'], ok: true, reads: 2 }
];
for (const item of cases) {
  test(item.name, async () => {
    const { result, reads } = await probe(item.frames, item.holdOpen);
    assert.equal(result.ok, item.ok);
    if (item.error) assert.equal(result.error, item.error);
    assert.equal(reads, item.reads);
  });
}
