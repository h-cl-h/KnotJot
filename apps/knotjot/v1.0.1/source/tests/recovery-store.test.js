// EN: Verify app-owned recovery persistence, validation and corruption preservation on disk.
// ZH: 验证应用自有恢复持久化、校验和磁盘损坏保留。
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const modulePath=path.join(__dirname,'../src/recovery-store');
test('recovery service is available',()=>assert.ok(fs.existsSync(modulePath+'.js')));
test('ordered durable recovery, clean-history lookup, disable and corrupt preservation',async()=>{
  // EN: Instantiate fresh services against an isolated directory to emulate fresh processes.
  // ZH: 对隔离目录创建新服务来模拟新进程。
  const {createRecoveryStore}=require(modulePath),dir=fs.mkdtempSync(path.join(os.tmpdir(),'knotjot-recovery-'));
  const doc={app:'brace-mindmap',version:2,curSheet:0,sheets:[{roots:['t'],textboxes:{t:{id:'t',text:'one'}}}]};
  const entry={version:1,id:'session-one',dirty:true,originalPath:'C:/isolated/original.knotjot',name:'Test',document:doc,history:null};
  const store=createRecoveryStore(()=>dir);await store.put(entry);let fresh=createRecoveryStore(()=>dir);assert.equal((await fresh.list()).entries[0].document.sheets[0].textboxes.t.text,'one');
  await Promise.all([store.put({...entry,name:'older'}),store.put({...entry,name:'newer'})]);assert.equal((await fresh.list()).entries[0].name,'newer');
  await store.put({...entry,dirty:false,history:{sheetKeys:['s1'],sheets:[],fileHistory:[],fileHidx:0,historySeq:0}});assert.equal((await fresh.list()).entries.length,0);assert.ok(await fresh.findSaved(doc));
  await store.configure({autosave:false,undo:false});assert.equal(await fresh.findSaved(doc),null);assert.equal((await fresh.list()).entries.length,0);
  await store.configure({autosave:true,undo:false});await store.put(entry);await store.configure({autosave:false,undo:false});await store.put({...entry,name:'Must not replace deferred copy'});assert.equal((await fresh.list()).entries[0].name,'Test');await store.remove(entry.id);assert.equal((await fresh.list()).entries.length,0);
  const bad=path.join(dir,'bad.json');fs.writeFileSync(bad,'{broken');const result=await fresh.list();assert.equal(result.errors.length,1);assert.equal(fs.readFileSync(bad,'utf8'),'{broken');
  await assert.rejects(store.put({...entry,document:{app:'bad'}}));assert.equal(fs.readFileSync(bad,'utf8'),'{broken');
});
test('history rejects unsafe fields, invalid reachable states and mismatched shadows',()=>{
  // EN: A valid current document cannot smuggle invalid tree state through persisted undo.
  // ZH: 当前合法文档不能通过持久撤销夹带无效树状态。
  const {validateEnvelope}=require('../src/recovery-codec');
  const sheet={roots:['t'],textboxes:{t:{id:'t',text:'after'}}};
  const state={key:'s',hidx:1,shadow:sheet,history:[{seq:1,ops:[{kind:'entity',section:'textboxes',id:'t',before:{id:'t',text:'before'},after:{id:'t',text:'after'}}]}]};
  const e={version:1,id:'test',dirty:true,document:{app:'brace-mindmap',sheets:[sheet]},history:{sheetKeys:['s'],sheets:[state],fileHistory:[],fileHidx:0,historySeq:1}};
  assert.ok(validateEnvelope(e));const bad=JSON.parse(JSON.stringify(e));bad.history.sheets[0].history[0].ops[0].before.parentBrace='missing';assert.throws(()=>validateEnvelope(bad));
  bad.history.sheets[0].history[0].ops[0].section='__proto__';assert.throws(()=>validateEnvelope(bad));
  const mismatched=JSON.parse(JSON.stringify(e));mismatched.history.sheets[0].shadow.textboxes.t.text='different';assert.throws(()=>validateEnvelope(mismatched));
});
test('applied after and unapplied before sides are validated after crossing the cursor',()=>{
  // EN: Reproduce the independent review's hidden cyclic side in both cursor directions.
  // ZH: 在两个游标方向复现独立审查中的隐藏循环状态。
  const {validateEnvelope}=require('../src/recovery-codec');
  for(const cursor of [0,1]){
    const shadow={roots:['t'],textboxes:{t:{id:'t',text:'Current'}}};
    const entry={seq:1,ops:[{kind:'entity',section:'textboxes',id:'t',before:{id:'t',text:'Before'},after:{id:'t',text:'Cycle',parentBrace:'b',brace:'b'}},{kind:'entity',section:'braces',id:'b',after:{parentTb:'t',children:['t']}},{kind:'meta',key:'roots',before:['t'],after:[]}]};
    if(cursor===0)for(const op of entry.ops){const prior=op.before;op.before=op.after;op.after=prior;}
    const raw={version:1,id:'review',dirty:true,document:{app:'brace-mindmap',sheets:[shadow]},history:{sheetKeys:['s'],sheets:[{key:'s',shadow,history:[entry],hidx:cursor}],fileHistory:[],fileHidx:0,historySeq:1}};
    assert.throws(()=>validateEnvelope(raw),'cyclic side must never be attachable');
  }
});
test('valid but inconsistent operation sides cannot change the saved cursor state',()=>{
  // EN: Require patch preconditions and round-trip identity, not merely valid node shapes.
  // ZH: 要求补丁前置状态及往返一致性，不只检查节点形状是否合法。
  const {validateEnvelope}=require('../src/recovery-codec');const shadow={roots:['t'],textboxes:{t:{id:'t',text:'Current'}}};
  const entry={seq:1,ops:[{kind:'entity',section:'textboxes',id:'t',before:{id:'t',text:'Before'},after:{id:'t',text:'Different'}}]};
  assert.throws(()=>validateEnvelope({version:1,id:'review',dirty:true,document:{app:'brace-mindmap',sheets:[shadow]},history:{sheetKeys:['s'],sheets:[{key:'s',shadow,history:[entry],hidx:1}],fileHistory:[],fileHidx:0,historySeq:1}}));
});
