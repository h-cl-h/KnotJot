'use strict';
// EN: Store recovery only beneath Electron userData; caller paths are metadata, never write targets.
// ZH: 恢复数据仅存于 Electron userData；调用方路径仅是元数据，绝不作为写入目标。
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {writeAtomicText}=require('./atomic-store'),{validateEnvelope}=require('./recovery-codec');
const MAX_ENTRY=128*1024*1024,MAX_TOTAL=256*1024*1024;
function digest(value){
  // EN: Derive safe filenames and exact saved-document identity from canonical normalized data.
  // ZH: 从规范数据生成安全文件名和精确保存文档标识。
  return crypto.createHash('sha256').update(typeof value==='string'?value:JSON.stringify(value)).digest('hex');
}
function createRecoveryStore(directory){
  // EN: Serialize reads, writes and pruning so old asynchronous writes cannot overtake newer captures.
  // ZH: 串行读取、写入及清理，避免旧异步写入超过新捕获。
  let queue=Promise.resolve(),preferences={autosave:true,undo:true};
  function ordered(action){
    // EN: Recover the queue after individual storage failures. ZH: 单个存储失败后恢复队列。
    const job=queue.then(action);queue=job.catch(()=>{});return job;
  }
  async function inventory(){
    // EN: Preserve unreadable files in place and return errors instead of overwriting or activating them.
    // ZH: 原位保留无法读取的文件，返回错误而非覆盖或激活。
    await fs.promises.mkdir(directory(),{recursive:true});const entries=[],errors=[];
    for(const name of await fs.promises.readdir(directory())){if(!name.endsWith('.json'))continue;const file=path.join(directory(),name);
      try{const st=await fs.promises.stat(file);if(st.size>MAX_ENTRY)throw new Error('副本超过 128 MiB');const entry=validateEnvelope(JSON.parse(await fs.promises.readFile(file,'utf8')));entries.push({entry,file,size:st.size});}
      catch(e){errors.push({file,error:e.message});}
    }return {entries,errors};
  }
  async function put(raw){
    // EN: Validate and atomically persist bounded data; disabling features removes their payloads before writing.
    // ZH: 校验并原子持久化有界数据；功能关闭时在写入前移除对应载荷。
    if(Buffer.byteLength(JSON.stringify(raw))>MAX_ENTRY)throw new Error('恢复副本超过 128 MiB');
    const entry=validateEnvelope(raw);return ordered(async()=>{
      if(entry.dirty&&!preferences.autosave)return {ok:true,disabled:true};if(!preferences.undo)entry.history=null;
      if(!entry.dirty&&!entry.history){const all=await inventory();for(const row of all.entries)if(row.entry.id===entry.id)await fs.promises.unlink(row.file);return {ok:true,disabled:true};}entry.updatedAt=Date.now();
      const text=JSON.stringify(entry);if(Buffer.byteLength(text)>MAX_ENTRY)throw new Error('恢复副本超过 128 MiB，请减少历史或图片');
      const file=path.join(directory(),digest(entry.id)+'.json');
      // EN: A corrupt existing target is never silently replaced. ZH: 不静默替换已损坏的现有目标。
      try{validateEnvelope(JSON.parse(await fs.promises.readFile(file,'utf8')));}catch(e){if(e.code!=='ENOENT')throw new Error('恢复目标损坏，已原位保留：'+file);}
      await writeAtomicText(file,text);const all=await inventory();const sorted=all.entries.sort((a,b)=>b.entry.updatedAt-a.entry.updatedAt);let bytes=0;
      for(let i=0;i<sorted.length;i++){bytes+=sorted[i].size;if(i>=10||bytes>MAX_TOTAL)await fs.promises.unlink(sorted[i].file);}
      return {ok:true};
    });
  }
  function list(){
    // EN: Expose only dirty launch candidates while retaining clean histories for exact reopen matches.
    // ZH: 启动仅显示脏候选，干净历史保留用于精确重开匹配。
    return ordered(async()=>{const all=await inventory();return {entries:all.entries.filter(x=>x.entry.dirty).map(x=>x.entry),errors:all.errors};});
  }
  function remove(id){
    // EN: Remove only a known app-owned identifier; no arbitrary path reaches the filesystem.
    // ZH: 仅删除已知应用标识对应文件，不允许任意路径进入文件系统。
    return ordered(async()=>{const all=await inventory();for(const row of all.entries)if(row.entry.id===id)await fs.promises.unlink(row.file);return {ok:true};});
  }
  function findSaved(document){
    // EN: Restore history only if the saved file content exactly matches a clean validated envelope.
    // ZH: 仅当保存文件内容与干净且已校验封装完全相同才恢复历史。
    const normalized=require('./document-model').validateAndNormalizeDocument(document),hash=digest(normalized);
    return ordered(async()=>{if(!preferences.undo)return null;const all=await inventory();return all.entries.filter(x=>!x.entry.dirty&&x.entry.history&&digest(x.entry.document)===hash).sort((a,b)=>b.entry.updatedAt-a.entry.updatedAt)[0]?.entry||null;});
  }
  function configure(next){
    // EN: Disabling autosave stops future writes without discarding deferred unsaved copies; disabling undo removes only history payloads.
    // ZH: 关闭自动恢复仅停止未来写入，不丢弃延后的未保存副本；关闭撤销仅移除历史载荷。
    return ordered(async()=>{preferences={autosave:next.autosave!==false,undo:next.undo!==false};const all=await inventory();for(const row of all.entries){if(!row.entry.dirty&&!preferences.undo)await fs.promises.unlink(row.file);else if(!preferences.undo&&row.entry.history){row.entry.history=null;await writeAtomicText(row.file,JSON.stringify(row.entry));}}return {ok:true};});
  }
  return {put,list,remove,findSaved,configure};
}
module.exports={createRecoveryStore};
