(function(root,factory){
  // EN: Share recovery validation between Node storage and the isolated renderer.
  // ZH: 在 Node 存储与隔离渲染器之间共享恢复校验。
  const api=factory(typeof module==='object'&&module.exports?require('./document-model'):root.KnotJotDocumentModel);
  if(typeof module==='object'&&module.exports)module.exports=api;else root.KnotJotRecoveryCodec=api;
})(typeof globalThis!=='undefined'?globalThis:this,function(Model){
  // EN: Keep persisted histories bounded and validate every state reachable by undo or redo before activation.
  // ZH: 限制持久化历史，并在激活前校验每个可由撤销或重做到达的状态。
  const dicts=['textboxes','braces','rbraces','fgroups','links','boundaries','summaries','callouts','relations'];
  const meta=['name','docType','idc','focusId','roots','customMarkers','legend','view','appearance'];
  const clone=v=>JSON.parse(JSON.stringify(v));
  function assert(ok,message){
    // EN: Reject malformed recovery data with a user-readable reason. ZH: 用可读原因拒绝损坏的恢复数据。
    if(!ok)throw new Error('恢复副本无效：'+message);
  }
  function key(value){
    // EN: Disallow prototype keys and require bounded opaque identifiers. ZH: 禁止原型键并要求有界不透明标识。
    return typeof value==='string'&&value.length>0&&value.length<=128&&!['__proto__','constructor','prototype'].includes(value);
  }
  function equivalent(a,b){
    // EN: Compare JSON states by structure, ignoring harmless object-key ordering while preserving arrays and absent values.
    // ZH: 按结构比较 JSON 状态，忽略无害对象键顺序，同时保留数组顺序及缺失值差异。
    if(a===b)return true;if(a==null||b==null||typeof a!=='object'||typeof b!=='object'||Array.isArray(a)!==Array.isArray(b))return false;
    const keys=Object.keys(a);return keys.length===Object.keys(b).length&&keys.every(k=>Object.prototype.hasOwnProperty.call(b,k)&&equivalent(a[k],b[k]));
  }
  function snapshot(s){
    // EN: Validate file-history snapshots without interpreting opaque conversation payloads.
    // ZH: 校验文件历史快照，不解释不透明对话载荷。
    assert(s&&Array.isArray(s.sheets),'文件历史');const keys=new Set();
    for(const row of s.sheets){assert(row&&key(row.key)&&!keys.has(row.key),'画布历史标识');keys.add(row.key);}
    Model.validateAndNormalizeDocument({app:'brace-mindmap',version:2,name:s.name,curSheet:s.curSheet,sheets:s.sheets.map(x=>x.data),appearance:s.appearance,aiChat:s.aiChat});
    if(s.textStyles!=null)assert(typeof s.textStyles==='object'&&!Array.isArray(s.textStyles),'样式历史');
  }
  function patch(d,entry,side){
    // EN: Require opposite-side preconditions for unique whitelisted fields, apply the detached patch and validate the resulting topology.
    // ZH: 对唯一白名单字段要求另一侧前置状态一致，应用独立补丁后校验结果拓扑。
    assert(entry&&Number.isSafeInteger(entry.seq)&&entry.seq>=0&&Array.isArray(entry.ops)&&entry.ops.length<=500000,'画布补丁');
    const opposite=side==='before'?'after':'before',seen=new Set();
    for(const op of entry.ops){assert(op&&['meta','entity'].includes(op.kind),'补丁类型');
      const token=op.kind==='meta'?'meta:'+op.key:op.section+':'+op.id;assert(!seen.has(token),'重复补丁字段');seen.add(token);
      if(op.kind==='meta'){assert(meta.includes(op.key),'补丁字段');assert(equivalent(d[op.key],op[opposite]),'历史前置状态不一致');if(op[side]===undefined)delete d[op.key];else d[op.key]=clone(op[side]);}
      else{assert(dicts.includes(op.section)&&key(op.id),'补丁记录');const target=d[op.section]||(d[op.section]={});assert(equivalent(target[op.id],op[opposite]),'历史前置记录不一致');if(op[side]===undefined)delete target[op.id];else target[op.id]=clone(op[side]);}
    }
    Model.validateSheet(d,'history');return d;
  }
  function validateHistory(value,sheetCount){
    // EN: Traverse each complete timeline in both directions and verify cursor round trips, including sides reached only after crossing the cursor.
    // ZH: 完整双向遍历各时间线并验证游标往返，覆盖只有跨过游标才会到达的操作侧。
    if(value==null)return null;const h=clone(value);
    assert(Array.isArray(h.sheetKeys)&&h.sheetKeys.length===sheetCount&&new Set(h.sheetKeys).size===sheetCount&&h.sheetKeys.every(key),'当前画布标识');
    assert(Array.isArray(h.sheets)&&h.sheets.length<=100&&Array.isArray(h.fileHistory)&&h.fileHistory.length<=30,'历史数量');
    assert(Number.isSafeInteger(h.fileHidx)&&h.fileHidx>=0&&h.fileHidx<=h.fileHistory.length&&Number.isSafeInteger(h.historySeq)&&h.historySeq>=0,'历史游标');
    let count=h.fileHistory.length;const seen=new Set();
    for(const state of h.sheets){assert(state&&key(state.key)&&!seen.has(state.key),'历史键');seen.add(state.key);assert(Array.isArray(state.history)&&Number.isInteger(state.hidx)&&state.hidx>=0&&state.hidx<=state.history.length,'画布游标');count+=state.history.length;
      assert(count<=100,'最多保留 100 次操作');const shadow=Model.validateSheet(state.shadow,'history.shadow');
      let d=clone(state.shadow);for(let i=state.hidx-1;i>=0;i--)patch(d,state.history[i],'before');
      const first=clone(d);
      for(let i=0;i<state.history.length;i++){patch(d,state.history[i],'after');if(i+1===state.hidx)assert(equivalent(Model.validateSheet(d,'history.cursor'),shadow),'历史游标往返不一致');}
      const last=clone(d);
      for(let i=state.history.length-1;i>=0;i--){patch(d,state.history[i],'before');if(i===state.hidx)assert(equivalent(Model.validateSheet(d,'history.cursor'),shadow),'历史游标往返不一致');}
      assert(equivalent(Model.validateSheet(d,'history.first'),Model.validateSheet(first,'history.first')),'历史起点往返不一致');
      d=clone(state.shadow);for(let i=state.hidx;i<state.history.length;i++)patch(d,state.history[i],'after');
      assert(equivalent(Model.validateSheet(d,'history.last'),Model.validateSheet(last,'history.last')),'历史终点往返不一致');
    }
    for(const entry of h.fileHistory){assert(entry&&Number.isSafeInteger(entry.seq),'文件序号');snapshot(entry.before);snapshot(entry.after);}
    return h;
  }
  function validateEnvelope(raw){
    // EN: Accept only versioned app-owned envelopes and detached validated document/history data.
    // ZH: 仅接受版本化应用自有封装及独立且已校验的文档和历史。
    assert(raw&&raw.version===1&&key(raw.id)&&typeof raw.dirty==='boolean','版本或标识');
    assert(raw.originalPath==null||(typeof raw.originalPath==='string'&&raw.originalPath.length<=32768),'原文件路径');
    assert(new TextEncoder().encode(JSON.stringify(raw.document)).length<=64*1024*1024,'文档超过 64 MiB');
    const document=Model.validateAndNormalizeDocument(raw.document),history=validateHistory(raw.history,document.sheets.length);
    if(history)for(let i=0;i<document.sheets.length;i++){const state=history.sheets.find(s=>s.key===history.sheetKeys[i]);if(!state)continue;const shadow=Model.validateSheet(state.shadow,'history.shadow'),sheet=document.sheets[i];for(const field of dicts)assert(JSON.stringify(shadow[field])===JSON.stringify(sheet[field]),'历史与当前画布不匹配');}
    return {version:1,id:raw.id,dirty:raw.dirty,originalPath:raw.originalPath||null,name:String(raw.name||document.name||'Untitled').slice(0,200),document,history,updatedAt:Number(raw.updatedAt)||Date.now()};
  }
  return {validateEnvelope,validateHistory};
});
