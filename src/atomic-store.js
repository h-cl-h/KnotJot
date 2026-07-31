'use strict';

const fs = require('fs');
const path = require('path');

let writeId = 0;

function errorDetails(err, extras = {}) {
  return Object.assign({
    ok: false,
    errorCode: err && err.code ? String(err.code) : 'WRITE_FAILED',
    error: err && err.message ? err.message : String(err || '写入失败')
  }, extras);
}

async function syncFile(file) {
  const handle = await fs.promises.open(file, 'r+');
  try { await handle.sync(); } finally { await handle.close(); }
}

async function atomicReplacePrepared(tempPath, targetPath, options = {}) {
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
    if (hadOriginal && !options.keepBackup) await fs.promises.unlink(backupPath).catch(() => {});
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
  const target = path.resolve(file);
  await fs.promises.mkdir(path.dirname(target), { recursive: true });
  const tempPath = `${target}.tmp-${process.pid}-${Date.now()}-${++writeId}`;
  const handle = await fs.promises.open(tempPath, 'wx');
  try {
    await handle.writeFile(String(text == null ? '' : text), options.encoding || 'utf8');
    await handle.sync();
  } catch (err) {
    await handle.close().catch(() => {});
    await fs.promises.unlink(tempPath).catch(() => {});
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
  let queue = Promise.resolve();
  return {
    read: async () => { await queue; return fs.promises.readFile(fileFor(), 'utf8'); },
    write: text => {
      const task = queue.then(() => writeAtomicText(fileFor(), text));
      queue = task.catch(() => {});
      return task;
    }
  };
}

module.exports = { atomicReplacePrepared, writeAtomicText, orderedTextStore, errorDetails, syncFile };
