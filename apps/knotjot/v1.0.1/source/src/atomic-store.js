'use strict';

const fs = require('fs');
const path = require('path');

let writeId = 0;

function errorDetails(err, extras = {}) {
/* EN: Return structured storage failure details together with recovery paths supplied by the caller.
   ZH: 返回结构化存储错误及调用方提供的恢复路径。 */

  return Object.assign({
    ok: false,
    errorCode: err && err.code ? String(err.code) : 'WRITE_FAILED',
    error: err && err.message ? err.message : String(err || '写入失败')
  }, extras);
}

async function syncFile(file) {
/* EN: Flush pending file data and always close the file handle.
   ZH: 将待写文件数据刷盘并始终关闭文件句柄。 */

  const handle = await fs.promises.open(file, 'r+');
  try { await handle.sync(); } finally { await handle.close(); }
}

async function atomicReplacePrepared(tempPath, targetPath, options = {}) {
/* EN: Flush a same-directory temporary file, preserve the original backup, then atomically replace the target.
   ZH: 刷盘同目录临时文件、保留原文件备份，再原子替换目标文件。 */

  const target = path.resolve(targetPath);
  const temp = path.resolve(tempPath);
  if (path.dirname(target) !== path.dirname(temp)) {
    throw Object.assign(new Error('临时文件必须与目标文件位于同一目录'), { code: 'CROSS_DEVICE_TEMP' });
  }
  const backupPath = options.backupPath || `${target}.bak-${process.pid}-${Date.now()}-${++writeId}`;
  let hadOriginal = false;
  try {
    await syncFile(temp);
    try {
      await fs.promises.copyFile(target, backupPath, fs.constants.COPYFILE_EXCL);
      hadOriginal = true;
      await syncFile(backupPath);
    } catch (err) {
      if (!err || err.code !== 'ENOENT') throw err;
    }
    await fs.promises.rename(temp, target);
    await syncFile(target);
    if (hadOriginal && !options.keepBackup) await fs.promises.unlink(backupPath).catch(() => {
/* EN: Continue atomicReplacePrepared at fs.promises.unlink(backupPath).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(backupPath).catch 阶段继续 atomicReplacePrepared 流程：完成且不修改状态。 */
});
    return { ok: true, path: target, backupPath: options.keepBackup && hadOriginal ? backupPath : null };
  } catch (err) {
    let originalPreserved = false;
    try {
      await fs.promises.access(target, fs.constants.R_OK);
      originalPreserved = true;
    } catch (_) {
      if (hadOriginal) {
        try {
          await fs.promises.rename(backupPath, target);
          originalPreserved = true;
        } catch (_) {}
      }
    }
    return errorDetails(err, { path: target, tempPath: temp, backupPath: hadOriginal ? backupPath : null, originalPreserved });
  }
}

async function writeAtomicText(file, text, options = {}) {
/* EN: Write UTF-8 text to an exclusive temporary file and commit it through the atomic replacement routine.
   ZH: 将 UTF-8 文本写入独占临时文件，并通过原子替换流程提交。 */

  const target = path.resolve(file);
  await fs.promises.mkdir(path.dirname(target), { recursive: true });
  const tempPath = `${target}.tmp-${process.pid}-${Date.now()}-${++writeId}`;
  const handle = await fs.promises.open(tempPath, 'wx');
  try {
    await handle.writeFile(String(text == null ? '' : text), options.encoding || 'utf8');
    await handle.sync();
  } catch (err) {
    await handle.close().catch(() => {
/* EN: Continue writeAtomicText at handle.close().catch: complete without changing state.
   ZH: 在 handle.close().catch 阶段继续 writeAtomicText 流程：完成且不修改状态。 */
});
    await fs.promises.unlink(tempPath).catch(() => {
/* EN: Continue writeAtomicText at fs.promises.unlink(tempPath).catch: complete without changing state.
   ZH: 在 fs.promises.unlink(tempPath).catch 阶段继续 writeAtomicText 流程：完成且不修改状态。 */
});
    throw err;
  }
  await handle.close();
  const result = await atomicReplacePrepared(tempPath, target, options);
  if (!result.ok) {
    const err = Object.assign(new Error(result.error), result);
    throw err;
  }
  return result;
}

function orderedTextStore(fileFor) {
/* EN: Serialize writes and make reads await the current queue without poisoning later operations after a failure.
   ZH: 串行执行写入并让读取等待当前队列，单次失败不阻断后续操作。 */

  let queue = Promise.resolve();
  return {
    read: async () => {
/* EN: read in orderedTextStore: call fs.promises.readFile, fileFor.
   ZH: orderedTextStore 中的 read：调用 fs.promises.readFile、fileFor。 */
 await queue; return fs.promises.readFile(fileFor(), 'utf8'); },
    write: text => {
/* EN: write in orderedTextStore: call queue.then, task.catch; update queue.
   ZH: orderedTextStore 中的 write：调用 queue.then、task.catch；更新 queue。 */

      const task = queue.then(() => /* EN: Continue module at queue.then: call writeAtomicText, fileFor; return writeAtomicText(fileFor(), text). ZH: 在 queue.then 阶段继续 module 流程：调用 writeAtomicText、fileFor；返回 writeAtomicText(fileFor(), text)。 */ writeAtomicText(fileFor(), text));
      queue = task.catch(() => {
/* EN: Continue module at task.catch: complete without changing state.
   ZH: 在 task.catch 阶段继续 module 流程：完成且不修改状态。 */
});
      return task;
    }
  };
}

module.exports = { atomicReplacePrepared, writeAtomicText, orderedTextStore, errorDetails, syncFile };
