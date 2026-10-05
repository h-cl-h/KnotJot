// EN: Run approved regressions in an isolated hidden renderer and retain machine-readable evidence.
// ZH: 在隔离隐藏渲染器执行已批准的回归，并保留机器可读证据。
const {app,BrowserWindow}=require('electron'),fs=require('fs'),path=require('path');
const source=path.resolve(process.env.KNOTJOT_TEST_SOURCE||path.join(__dirname,'..'));
const out=path.resolve(__dirname,'../../preview/testing/approved-followup');fs.mkdirSync(out,{recursive:true});
app.setPath('userData',path.join(out,'profiles','renderer-'+Date.now()));
process.env.KNOTJOT_TEXT_STYLES_DIR=path.join(out,'libraries','renderer-'+Date.now());process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');require(path.join(source,'main.js'));
app.whenReady().then(async()=>{
  // EN: Load the actual preload and renderer; watchdog catches activation hangs.
  // ZH: 加载真实预加载和渲染器；看门狗检测激活卡死。
  const timer=setTimeout(()=>app.exit(2),60000);
  try{const win=new BrowserWindow({show:false,width:1280,height:900,webPreferences:{offscreen:true,backgroundThrottling:false,preload:path.join(source,'preload.js')}});
    await win.loadFile(path.join(source,'index.html'));const result=await win.webContents.executeJavaScript(`(${cases.toString()})()`);
    fs.writeFileSync(path.join(out,'renderer-'+(process.env.KNOTJOT_TEST_LABEL||'final')+'.json'),JSON.stringify(result,null,2));fs.writeFileSync(path.join(out,'main-final.png'),(await win.webContents.capturePage()).toPNG());await win.webContents.executeJavaScript('openSettings()');await new Promise(r=>setTimeout(r,250));fs.writeFileSync(path.join(out,'settings-final.png'),(await win.webContents.capturePage()).toPNG());console.log(JSON.stringify(result,null,2));clearTimeout(timer);app.exit(result.some(x=>!x.pass)?1:0);
  }catch(e){console.error(e);clearTimeout(timer);app.exit(2);}
});
async function cases(){
  // EN: Assert visible results and persisted metadata through production commands.
  // ZH: 通过生产命令断言可见结果与持久化元数据。
  const results=[],check=(name,pass,details)=>results.push({name,pass:!!pass,details});window.alert=()=>{};
  await new Promise(r=>setTimeout(r,250));hideStart();dirty=false;await doNew('brace','brace');
  const id=roots[0];TB[id].textStyleId='ocean';TB[id].textRules={maxLength:12};TB[id]._textRulesOverride=true;TB[id].textFormat={bold:true};update();
  const base=cleanSheetObject(activeSheetObject(),false);sheets=Array.from({length:99},()=>clonePlain(base));curSheet=0;initHistory(true);
  drillDownToNewSheet(id);check('99 to 100 sheets succeeds',sheets.length===100);
  const copied=TB[roots[0]];check('branch copy retains style and per-node rules',copied.id!==id&&copied.textStyleId==='ocean'&&copied.textRules?.maxLength===12&&copied._textRulesOverride&&copied.textFormat?.bold,copied);
  const saved=serialize();check('branch copy fresh validation/reopen',await loadDataAsync(saved,'copy'));check('style and rules survive reopen',TB[roots[0]].textStyleId==='ocean'&&TB[roots[0]].textRules?.maxLength===12);
  dirty=false;const before=serialize(),rev=modelRevision;drillDownToNewSheet(roots[0]);check('100 sheets refusal is mutation free',sheets.length===100&&serialize()===before&&modelRevision===rev&&!dirty);
  addSheet('brace','brace');check('normal add-sheet also refuses 100 without mutation',sheets.length===100&&serialize()===before&&modelRevision===rev&&!dirty);
  dirty=false;await doNew('brace','brace');const first=roots[0],second=newTextbox('Distant free root');roots.push(second);TB[first].x=100;TB[first].y=100;TB[second].x=15100;TB[second].y=300;update();
  check('Fit All command exists',typeof fitAllContent==='function');if(typeof fitAllContent==='function'){fitAllContent();const viewport=canvas.getBoundingClientRect();const rects=[els[first],els[second]].map(el=>el.getBoundingClientRect());check('two roots 15000 apart both visible',scale>0&&rects.every(r=>r.left>=viewport.left&&r.right<=viewport.right&&r.top>=viewport.top&&r.bottom<=viewport.bottom),{scale,rects:rects.map(r=>({left:r.left,right:r.right}))});check('Fit All View menu entry',menuItems('view').some(x=>x.label==='适配全部内容'));}
  const normal=await rasterize('<svg xmlns="http://www.w3.org/2000/svg" width="100" height="50"><rect width="100" height="50" fill="red"/></svg>',100,50,KnotJotExportScene.safeSingleRatio(100,50,2),'image/jpeg',.95);const image=new Image();image.src=normal;await image.decode();check('normal JPG output dimensions',image.naturalWidth===200&&image.naturalHeight===100);
  if(typeof fitAllContent==='function'){
    CO.coFit={id:'coFit',tb:second,text:'Far annotation',offsetX:3000,offsetY:1500};BD.bdFit={id:'bdFit',members:[first,second],label:'All roots'};const cv=document.createElement('canvas');cv.width=32;cv.height=16;TB[second].img={src:cv.toDataURL('image/png'),w:400,h:200};update();fitAllContent();const vp=canvas.getBoundingClientRect();const rects=[...overlayLayer.children,els[second]].map(el=>el.getBoundingClientRect()).filter(r=>r.width&&r.height);check('Fit All includes image annotation and boundary',rects.every(r=>r.left>=vp.left-1&&r.right<=vp.right+1&&r.top>=vp.top-1&&r.bottom<=vp.bottom+1));
    AppearanceEditor.loadDocumentAppearance({background:{enabled:true,mode:'limited',canvasWidth:2200,canvasHeight:1200,color:'#ffffff'}});fitAllContent();const finite=document.getElementById('tcCanvasLimited').getBoundingClientRect();check('Fit All includes finite canvas',finite.width>0&&finite.left>=vp.left-1&&finite.right<=vp.right+1&&finite.top>=vp.top-1&&finite.bottom<=vp.bottom+1);
    dirty=false;await doNew('brace');fitAllContent();check('Fit All handles empty canvas',Number.isFinite(scale)&&scale>0);
  }
  dirty=false;await doNew('brace','brace');for(let i=0;i<120;i++){TB[roots[0]].text='Revision '+i;markDirty();}const bounded=captureRecovery();check('persisted native history bounded to 100 operations',bounded.history.sheets.reduce((n,s)=>n+s.history.length,0)+bounded.history.fileHistory.length===100);check('bounded native history validates',!!KnotJotRecoveryCodec.validateEnvelope(bounded));
  for(let i=0;i<120;i++)historyBySheet.set('retired-'+i,{history:[],hidx:0,shadow:sheetData(true)});const pruned=captureRecovery();check('empty retired sheet histories do not exceed envelope bound',pruned.history.sheets.length===1&&!!KnotJotRecoveryCodec.validateEnvelope(pruned));
  dirty=false;await doNew('brace','brace');TB[roots[0]].text='First sheet';update();initHistory(true);addSheet('brace','brace');TB[roots[0]].text='Second sheet edited';update();markDirty();const multi=KnotJotRecoveryCodec.validateEnvelope(captureRecovery());applyLoadedDocument(multi.document,'multi');restoreRecoveryHistory(multi.history);undo();check('restored sheet undo precedes file undo',TB[roots[0]].text==='中心主题'&&sheets.length===2);undo();check('restored file undo removes added sheet',sheets.length===1&&TB[roots[0]].text==='First sheet');redo();check('restored file redo recreates second sheet',sheets.length===2);
  for(const cursor of [0,60,120]){
    // EN: Retained operations must remain contiguous around the saved cursor, including all-redo and mixed histories.
    // ZH: 保留操作必须围绕保存游标连续，涵盖全部待重做及混合历史。
    dirty=false;await doNew('brace','brace');const a=roots[0],b=newTextbox('B initial');roots.push(b);update();initHistory(true);
    for(let i=1;i<=20;i++){TB[a].text='A '+i;markDirty();}for(let i=1;i<=100;i++){TB[b].text='B '+i;markDirty();}for(let i=cursor;i<120;i++)undo();
    const envelope=KnotJotRecoveryCodec.validateEnvelope(captureRecovery());applyLoadedDocument(envelope.document,'cursor');restoreRecoveryHistory(envelope.history);
    if(cursor<120){redo();check('first restored redo follows cursor '+cursor,cursor===0?TB[a].text==='A 1'&&TB[b].text==='B initial':TB[b].text==='B 41');undo();}
    let count=0;while(canRedo()&&count++<160)redo();check('bounded redo keeps prerequisites at cursor '+cursor,TB[a].text==='A 20'&&TB[b].text===(cursor===0?'B 80':cursor===60?'B 90':'B 100'));
    count=0;while(canUndo()&&count++<160)undo();check('bounded undo reaches contiguous left edge '+cursor,TB[a].text===(cursor===0?'中心主题':cursor===60?'A 10':'A 20')&&TB[b].text==='B initial');
  }
  for(const cursor of [0,51,102]){
    // EN: File snapshots and node-creation patches share one global frontier, so trimming cannot remove the prerequisite beneath retained edits.
    // ZH: 文件快照与节点创建补丁共享全局边界，截断不能移除所保留编辑的创建前提。
    dirty=false;await doNew('brace','brace');renameSheet(0,'Renamed');const b=newTextbox('B initial');roots.push(b);update();markDirty();for(let i=1;i<=100;i++){TB[b].text='B '+i;markDirty();}for(let i=cursor;i<102;i++)undo();
    const envelope=KnotJotRecoveryCodec.validateEnvelope(captureRecovery());applyLoadedDocument(envelope.document,'mixed');restoreRecoveryHistory(envelope.history);
    let count=0;while(canUndo()&&count++<120)undo();check('mixed global undo reaches coherent edge '+cursor,cursor===102?!!TB[b]&&roots.includes(b)&&TB[b].text==='B initial':!TB[b]&&!roots.includes(b));
    count=0;while(canRedo()&&count++<120)redo();check('mixed global redo preserves root membership '+cursor,!!TB[b]&&roots.includes(b)&&TB[b].text===(cursor===0?'B 98':cursor===51?'B 99':'B 100'));
  }
  return results;
}

