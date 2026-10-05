// EN: Exercise crash/relaunch and save/reopen undo through actual main/preload/renderer boundaries.
// ZH: 通过真实主进程、预加载、渲染器边界测试崩溃重启和保存重开撤销。
const {app,BrowserWindow,dialog}=require('electron'),fs=require('fs'),path=require('path');
const out=path.resolve(process.env.KNOTJOT_RECOVERY_TEST_DIR||path.join(__dirname,'../../preview/testing/approved-followup/recovery-process'));
const phase=process.env.KNOTJOT_RECOVERY_PHASE||'write';fs.mkdirSync(out,{recursive:true});
app.setPath('userData',path.join(out,'profile'));process.env.KNOTJOT_TEXT_STYLES_DIR=path.join(out,'text-styles');process.env.KNOTJOT_INTEGRATION_TEST='1';app.commandLine.appendSwitch('disable-gpu');
let prompts=0;dialog.showMessageBox=async(_win,options)=>{
  // EN: Script only native choices in this isolated test; all document operations remain real.
  // ZH: 本隔离测试仅脚本化原生选择，全部文档操作保持真实。
  if(options.message.includes('Unsaved recovery')){prompts++;return {response:phase==='recover'?0:2};}return {response:1};
};
require('../main');
app.whenReady().then(async()=>{
  // EN: Preserve the profile across independent Electron processes and capture each phase's assertions.
  // ZH: 在独立 Electron 进程间保留配置目录，并记录每阶段断言。
  const timer=setTimeout(()=>app.exit(2),60000);
  try{const win=new BrowserWindow({show:false,width:1280,height:900,webPreferences:{offscreen:true,backgroundThrottling:false,preload:path.join(__dirname,'../preload.js')}});await win.loadFile(path.join(__dirname,'../index.html'));
    await new Promise(r=>setTimeout(r,700));const result=await win.webContents.executeJavaScript(`(${run.toString()})(${JSON.stringify(phase)},${JSON.stringify(path.join(out,'saved.knotjot'))})`);
    result.push({name:'launch prompt count',pass:prompts===(['later','recover'].includes(phase)?1:0),details:prompts});fs.writeFileSync(path.join(out,phase+'.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));clearTimeout(timer);app.exit(result.some(x=>!x.pass)?1:0);
  }catch(e){console.error(e.stack||e);clearTimeout(timer);app.exit(2);}
});
async function run(phase,target){
  // EN: Assert disk recovery, native undo, discard/later semantics, and disabled feature behavior.
  // ZH: 断言磁盘恢复、原生撤销、丢弃与稍后语义及功能关闭行为。
  const out=[],check=(name,pass,details)=>out.push({name,pass:!!pass,details});window.alert=message=>out.push({name:'unexpected alert',pass:false,details:message});
  if(phase==='write'){
    hideStart();dirty=false;await doNew('brace','brace');const id=roots[0];TB[id].text='Before';update();initHistory(true);TB[id].text='Saved after';update();markDirty();curPath=target;
    check('explicit save succeeds',(await doSave())?.ok&&!dirty);check('clean save has no recovery prompt candidates',(await window.api.recoveryList()).entries.length===0);
    TB[id].text='Crash after';update();markDirty();await flushRecovery();const entries=(await window.api.recoveryList()).entries;check('dirty copy persisted before abrupt exit',entries.length===1&&entries[0].document.sheets[0].textboxes[id].text==='Crash after',document.getElementById('recoveryStatus').textContent);
  }else if(phase==='later'){
    check('Later preserves candidate and current start page',(await window.api.recoveryList()).entries.length===1&&!startEl.classList.contains('hidden')&&!dirty);
  }else if(phase==='recover'){
    check('fresh process restores unsaved content',dirty&&TB[roots[0]]?.text==='Crash after'&&curPath===target);
    undo();check('recovered undo reaches saved edit',TB[roots[0]].text==='Saved after');undo();check('recovered undo reaches original',TB[roots[0]].text==='Before');redo();check('recovered redo works',TB[roots[0]].text==='Saved after');
    const saved=await window.api.openPath(target);dirty=false;await loadDataAsync(saved.content,'saved');curPath=target;dirty=false;check('fresh disk reopen keeps native undo',canUndo());undo();check('reopened saved file undo restores original',TB[roots[0]].text==='Before');
    const prior=(await window.api.recoveryList()).entries;optRecoveryAutosave=false;optPersistUndo=false;localStorage.setItem('bmap.optRecoveryAutosave','0');localStorage.setItem('bmap.optPersistUndo','0');await configureRecovery();TB[roots[0]].text='Disabled';markDirty();await flushRecovery();const kept=(await window.api.recoveryList()).entries;check('off switches preserve deferred copy but prevent new writes',kept.length===prior.length&&kept.every(x=>x.document.sheets[0].textboxes[Object.keys(x.document.sheets[0].textboxes)[0]].text!=='Disabled'));check('off switch prevents saved undo lookup',await window.api.recoverySaved(JSON.parse(saved.content))===null);for(const entry of kept)await window.api.recoveryRemove(entry.id);check('explicit discard removes deferred copies',(await window.api.recoveryList()).entries.length===0);
  }else if(phase==='corrupt'){
    const data=await window.api.recoveryList();check('corrupt recovery rejected without activation',data.entries.length===0&&data.errors.length===1&&!dirty);check('corrupt error displayed',document.getElementById('recoveryStatus').textContent.includes('corrupt.json'));
  }else if(phase==='schedule'){
    dirty=false;await doNew('brace','brace');curPath=target;TB[roots[0]].text='Scheduled old owner';markDirty();const oldOwner=recoveryId;
    await new Promise(r=>setTimeout(r,31500));check('30-second debounce writes recovery automatically',(await window.api.recoveryList()).entries.some(e=>e.id===oldOwner));
    const next={app:'brace-mindmap',sheets:[{roots:['n'],textboxes:{n:{id:'n',text:'New owner'}}}]};await loadDataAsync(JSON.stringify(next),'new');curPath=target+'.other';dirty=false;TB.n.text='New changed';markDirty();await flushRecovery();const entries=(await window.api.recoveryList()).entries;
    check('document switches preserve snapshot ownership',entries.some(e=>e.id===oldOwner&&e.originalPath===target)&&entries.some(e=>e.id===recoveryId&&e.originalPath===target+'.other'));
    check('explicit discard resolves active owner',await guardUnsaved());check('discard preserves other document recovery',(await window.api.recoveryList()).entries.length===1);
  }else if(phase==='cursor-write'){
    // EN: Persist an all-redo document through the actual explicit-save path before terminating this process.
    // ZH: 终止进程前通过真实显式保存流程持久化全部待重做的文档。
    dirty=false;await doNew('brace','brace');const a=roots[0],b=newTextbox('B initial');roots.push(b);update();initHistory(true);
    for(let i=1;i<=20;i++){TB[a].text='A '+i;markDirty();}for(let i=1;i<=100;i++){TB[b].text='B '+i;markDirty();}for(let i=0;i<120;i++)undo();curPath=target;
    check('all-redo explicit save succeeds',(await doSave())?.ok&&!dirty);check('all-redo saved history does not create launch prompt',(await window.api.recoveryList()).entries.length===0);
  }else if(phase==='cursor-reopen'){
    // EN: A fresh process must replay the nearest redo prefix, not a suffix missing the first node's edits.
    // ZH: 新进程必须重放最近的重做前缀，不可跳过第一个节点的修改。
    const saved=await window.api.openPath(target);check('fresh cursor document loads',await loadDataAsync(saved.content,'cursor'));curPath=target;dirty=false;const [a,b]=roots;
    redo();check('fresh-process first redo retains prerequisite',TB[a].text==='A 1'&&TB[b].text==='B initial');let count=1;while(canRedo()&&count<150){redo();count++;}check('fresh-process bounded redo reaches coherent endpoint',count===100&&TB[a].text==='A 20'&&TB[b].text==='B 80');
    count=0;while(canUndo()&&count<150){undo();count++;}check('fresh-process bounded undo returns to saved state',count===100&&TB[a].text==='中心主题'&&TB[b].text==='B initial');
  }
  return out;
}
