const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');
const os = require('os');

app.commandLine.appendSwitch('disable-gpu');
process.env.KNOTJOT_INTEGRATION_TEST = '1';
app.setPath('userData', path.join(os.tmpdir(), `knotjot-performance-profile-${process.pid}`));
require('../main');
const resultPath = path.join(os.tmpdir(), 'knotjot-performance-result.json');
try { fs.unlinkSync(resultPath); } catch (_) {}

function fail(message, details) {
  fs.writeFileSync(resultPath, JSON.stringify({ ok: false, message, details }, null, 2));
  console.error('PERF_SMOKE_FAILED', message, details || '');
  process.exitCode = 1;
  app.exit(1);
}

app.whenReady().then(async () => {
  const win = new BrowserWindow({
    show: false,
    width: 1280,
    height: 820,
    webPreferences: { contextIsolation: true, nodeIntegration: false, preload: path.join(__dirname, '..', 'preload.js') }
  });
  try {
    await win.loadFile(path.join(__dirname, '..', 'index.html'));
    const savePath = path.join(os.tmpdir(), `knotjot-perf-${process.pid}.knotjot`);
    const raceSavePath = path.join(os.tmpdir(), `knotjot-perf-race-${process.pid}.bmap`);
    const failureTargetDir = path.join(os.tmpdir(), `knotjot-save-failure-${process.pid}`);
    const abandonedTarget = path.join(os.tmpdir(), `knotjot-save-abandoned-${process.pid}.bmap`);
    await fs.promises.mkdir(failureTargetDir, { recursive: true });
    const result = await win.webContents.executeJavaScript(`(async()=>{
      const makeSheet=(si,count)=>{
        const textboxes={},children=[];
        textboxes.t1={id:'t1',text:'画布 '+si,locked:false,parentBrace:null,brace:'b1',x:3000,y:1800};
        for(let i=2;i<=count;i++){const id='t'+i;children.push(id);textboxes[id]={id,text:'节点 '+si+'-'+i,locked:false,parentBrace:'b1',brace:null,x:0,y:0};}
        return {name:'画布 '+si,docType:'brace',idc:count,focusId:null,roots:['t1'],textboxes,
          braces:{b1:{id:'b1',locked:false,parentTb:'t1',children}},rbraces:{},fgroups:{},links:{},boundaries:{},summaries:{},callouts:{},relations:{},
          customMarkers:[],legend:{on:false,x:null,y:null,texts:{}},view:{numbering:'',compactMode:si===1?2:0,scale:1,panX:-2200,panY:-1300}};
      };
      const source={app:'brace-mindmap',version:2,name:'压力测试',curSheet:0,sheets:Array.from({length:24},(_,i)=>makeSheet(i+1,120)),
        aiChat:[{role:'user',content:'生成压力测试导图'},{role:'assistant',content:'已生成'}]};
      const tLoad=performance.now(); if(!await loadDataAsync(JSON.stringify(source),'压力测试'))throw new Error('loadData failed'); const loadMs=performance.now()-tLoad;
      const aiLoaded=aiHistory.length===2&&aiHistory[0].content==='生成压力测试导图';
      const configWrites=[];for(let i=0;i<8;i++)configWrites.push(window.api.saveSkins(JSON.stringify({format:'knotjot-ui-skins',version:1,revision:i,writerId:'perf-smoke',updatedUtc:new Date().toISOString(),current:'default',skins:{},pad:i===0?'x'.repeat(500000):''})));
      await Promise.all(configWrites);const orderedConfigWrites=JSON.parse(await window.api.loadSkins()).revision===7;
      const failedBegin=await window.api.saveBegin(${JSON.stringify(failureTargetDir)},'失败清理测试');await window.api.saveChunk(failedBegin.id,'test');
      let failedSaveResult=null;try{failedSaveResult=await window.api.saveEnd(failedBegin.id,false);}catch(e){failedSaveResult={ok:false,error:String(e)};}
      const firstEl=els.t1,key0=ensureSheetKey(sheets[0]);
      const kids=BR.b1.children,compactNoOverlap=kids.every((id,i)=>i===0||TB[id]._cy-TB[id]._h/2>=TB[kids[i-1]]._cy+TB[kids[i-1]]._h/2);
      const originalLayout=layout;let dragLayouts=0;layout=function(){dragLayouts++;return originalLayout();};
      const card=els.t1._card,rect=card.getBoundingClientRect();
      card.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,button:0,pointerId:77,clientX:rect.left+10,clientY:rect.top+10}));
      for(let i=0;i<100;i++)card.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,button:0,pointerId:77,clientX:rect.left+20+i,clientY:rect.top+20+i}));
      card.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,button:0,pointerId:77,clientX:rect.left+120,clientY:rect.top+120}));
      await new Promise(requestAnimationFrame);layout=originalLayout;const dragFrameCoalesced=dragLayouts<=4;initHistory();
      const tSwitch=performance.now(); switchSheet(1);const cached=sheetRuntimeCache.has(key0);switchSheet(0);const switchMs=performance.now()-tSwitch;
      const domReused=els.t1===firstEl;
      for(let i=0;i<60;i++){TB.t1.text='历史 '+i;markDirty();}
      const incrementalHistory=history.length===60&&history.every(x=>x&&Array.isArray(x.ops)&&x.ops.length<=3);
      undo();const undoOk=TB.t1.text==='历史 58';redo();const redoOk=TB.t1.text==='历史 59';
      const originalRender=render;let fullRenders=0;render=function(ids){if(ids==null)fullRenders++;return originalRender(ids);};
      const tSelect=performance.now();for(let i=2;i<102;i++)selectTb('t'+i);const selectMs=performance.now()-tSelect;render=originalRender;const selectionWasLocal=fullRenders===0;
      const sameNeed=(a,b)=>a===b||(Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<1e-9);
      const naiveCollisionNeed=(placed,boxes,dx,gap,epsilon=0)=>{let d=-Infinity;for(const rb of placed)for(const cb of boxes){const x1=cb.x1+dx,x2=cb.x2+dx;
        if(x2>rb.x1+epsilon&&x1<rb.x2-epsilon)d=Math.max(d,rb.bottom+gap-cb.top);}return d;};
      const collisionIndex=new Map();indexPlacedBoxes(collisionIndex,[{x1:0,x2:10,top:0,bottom:10}],0,0);
      const boundaryIndex=new Map();indexPlacedBoxes(boundaryIndex,[{x1:0,x2:24.005,top:0,bottom:10}],0,0);
      const touchIndex=new Map();indexPlacedBoxes(touchIndex,[{x1:-24,x2:0,top:0,bottom:10}],0,0);
      const collisionIndexExact=collisionNeed(collisionIndex,[{x1:11,x2:20,top:0,bottom:10}],0,26)===-Infinity
        && collisionNeed(collisionIndex,[{x1:5,x2:15,top:0,bottom:10}],0,26)===36
        && collisionNeed(boundaryIndex,[{x1:24.002,x2:30,top:0,bottom:10}],0,26)===36
        && collisionNeed(touchIndex,[{x1:0,x2:12,top:0,bottom:10}],0,26)===-Infinity
        && collisionNeed(touchIndex,[{x1:-0.3,x2:12,top:0,bottom:10}],0,26,0.5)===-Infinity
        && collisionNeed(touchIndex,[{x1:-0.6,x2:12,top:0,bottom:10}],0,26,0.5)===36;
      let seed=0x5eed1234,collisionDifferentialExact=true;const rnd=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/4294967296;};
      for(let trial=0;trial<240&&collisionDifferentialExact;trial++){
        const idx=new Map(),placed=[];
        for(let p=0;p<28;p++){const boxes=[];for(let j=0,n=1+(rnd()*3|0);j<n;j++){const x1=(rnd()-.5)*500,w=(p%13===0?180:4)+rnd()*(p%13===0?360:90),top=(rnd()-.5)*120,h=4+rnd()*80;boxes.push({x1,x2:x1+w,top,bottom:top+h});}
          const dx=(rnd()-.5)*80,dy=(rnd()-.5)*160;indexPlacedBoxes(idx,boxes,dx,dy);boxes.forEach(cb=>placed.push({x1:cb.x1+dx,x2:cb.x2+dx,bottom:cb.bottom+dy}));}
        const query=[];for(let j=0,n=1+(rnd()*4|0);j<n;j++){let x1=(rnd()-.5)*550;if(trial%17===0&&j===0)x1=24.002;const w=(trial%19===0?220:3)+rnd()*100,top=(rnd()-.5)*100;query.push({x1,x2:x1+w,top,bottom:top+10+rnd()*60});}
        const dx=(rnd()-.5)*70,gap=rnd()*45;
        for(const epsilon of [0,0.5])if(!sameNeed(collisionNeed(idx,query,dx,gap,epsilon),naiveCollisionNeed(placed,query,dx,gap,epsilon))){collisionDifferentialExact=false;break;}
      }
      const savedStruct=TB.t1.struct;
      deselect();TB.t1.struct='timeline';update();selectTb('t1');
      const timelineSelection=svg.querySelectorAll('[data-brace-visual="b1"].sel').length===2&&!svg.querySelector('.struct-dot.sel');
      deselect();TB.t1.struct='matrix';update();selectTb('t1');
      const matrixSelection=!!svg.querySelector('.struct-grid[data-brace-visual="b1"].sel')&&!svg.querySelector('.elbow-path.sel');
      deselect();RB.rbtest={id:'rbtest',sources:['t3','t4'],target:'t2'};TB.t1.struct=savedStruct;update();selectTb('t2');
      const reverseSelection=!!svg.querySelector('[data-rev-visual="rbtest"].sel');
      deselect();delete RB.rbtest;update();const connectorSelectionExact=timelineSelection&&matrixSelection&&reverseSelection;
      TB.t1.tags=['A','B'];toggleOutline(true);renderOutline();let meta=outlineBody.querySelector('.ol-row[data-id="t1"] .ol-meta'),ms=getComputedStyle(meta);
      const outlineMetaSpaced=ms.display==='flex'&&parseFloat(ms.gap)===7;delete TB.t1.tags;renderOutline();meta=outlineBody.querySelector('.ol-row[data-id="t1"] .ol-meta');
      const outlineEmptyHidden=getComputedStyle(meta).display==='none';toggleOutline(false);const outlineMetaLayout=outlineMetaSpaced&&outlineEmptyHidden;
      const waitForCacheLimit=async()=>{const end=performance.now()+4000;while(sheets.filter(s=>!isLazySheet(s)).length>4&&performance.now()<end)await new Promise(r=>setTimeout(r,100));};
      await waitForCacheLimit();
      const materialized=sheets.filter(s=>!isLazySheet(s)).length;
      const tabs=sheetTabEls.length,nodes=Object.keys(TB).length;
      curPath=${JSON.stringify(raceSavePath)};const raceSaving=doSave();
      TB.t1.text='保存期间修改';markDirty();await raceSaving;
      const saveEditPreserved=dirty&&TB.t1.text==='保存期间修改';
      curPath=${JSON.stringify(savePath)};const saved=await doSave();const stableSaveCleared=!dirty;
      const sheetsBeforeAI=sheets.length;
      aiGenerateSheets(Array.from({length:8},(_,i)=>({name:'AI 压测 '+(i+1),struct:'brace',tree:[{text:'AI '+(i+1),children:[]}]})));
      await waitForCacheLimit();
      const aiGeneratedSheets=sheets.length-sheetsBeforeAI,aiMaterialized=sheets.filter(s=>!isLazySheet(s)).length;
      return {loadMs,switchMs,selectMs,cached,domReused,compactNoOverlap,collisionIndexExact,collisionDifferentialExact,connectorSelectionExact,outlineMetaLayout,dragFrameCoalesced,selectionWasLocal,incrementalHistory,undoOk,redoOk,aiLoaded,orderedConfigWrites,failedSaveResult,failedSaveSessionId:failedBegin.id,saveEditPreserved,stableSaveCleared,materialized,aiGeneratedSheets,aiMaterialized,tabs,nodes,saved:!!(saved&&saved.ok)};
    })()`);
    const abandonedWin = new BrowserWindow({show:false,webPreferences:{contextIsolation:true,nodeIntegration:false,preload:path.join(__dirname,'..','preload.js')}});
    await abandonedWin.loadFile(path.join(__dirname,'..','index.html'));
    const abandonedId=await abandonedWin.webContents.executeJavaScript(`(async()=>{const b=await window.api.saveBegin(${JSON.stringify(abandonedTarget)},'退出清理测试');await window.api.saveChunk(b.id,'test');return b.id;})()`);
    abandonedWin.destroy();await new Promise(r=>setTimeout(r,250));
    const abandonedTemp=`${abandonedTarget}.tmp-${abandonedId}`,abandonedTempCleaned=!fs.existsSync(abandonedTemp);
    await fs.promises.unlink(abandonedTemp).catch(()=>{});
    const savedDocument=JSON.parse(await fs.promises.readFile(savePath,'utf8'));
    const reopened=await win.webContents.executeJavaScript(`(async()=>{const ok=await loadDataAsync(${JSON.stringify(JSON.stringify(savedDocument))},'knotjot-perf.knotjot');return ok&&sheets.length===24&&Object.keys(sheets[0].textboxes).length===120;})()`);
    const raceSavedDocument=JSON.parse(await fs.promises.readFile(raceSavePath,'utf8'));
    const legacyReopened=await win.webContents.executeJavaScript(`(async()=>{const ok=await loadDataAsync(${JSON.stringify(JSON.stringify(raceSavedDocument))},'knotjot-perf-legacy.bmap');return ok&&sheets.length===24&&Object.keys(sheets[0].textboxes).length===120;})()`);
    await fs.promises.unlink(savePath).catch(()=>{});
    await fs.promises.unlink(raceSavePath).catch(()=>{});
    const failedTempPath=`${failureTargetDir}.tmp-${result.failedSaveSessionId}`;
    const failedTempRetained=fs.existsSync(failedTempPath);
    await fs.promises.rmdir(failureTargetDir).catch(()=>{});
    console.log('PERF_RESULT', JSON.stringify(result));
    if (!result.cached || !result.domReused) return fail('DOM cache was not reused', result);
    if (!result.compactNoOverlap || !result.collisionIndexExact || !result.collisionDifferentialExact) return fail('compact layout collision index changed exact semantics', result);
    if (!result.dragFrameCoalesced || !result.selectionWasLocal || !result.connectorSelectionExact) return fail('frame throttling or local selection rendering failed', result);
    if (!result.outlineMetaLayout) return fail('outline metadata layout changed', result);
    if (!result.incrementalHistory || !result.undoOk || !result.redoOk) return fail('incremental history invariant failed', result);
    if (!result.orderedConfigWrites) return fail('ordered config persistence failed', result);
    if (!result.failedSaveResult || result.failedSaveResult.ok !== false || !result.failedSaveResult.originalPreserved || !failedTempRetained) return fail('failed save recovery contract failed', result);
    if (!abandonedTempCleaned) return fail('destroyed renderer save cleanup failed', result);
    if (!result.saveEditPreserved || !result.stableSaveCleared || raceSavedDocument.sheets[0].textboxes.t1.text !== '历史 59' || savedDocument.sheets[0].textboxes.t1.text !== '保存期间修改') return fail('streaming save snapshot or revision guard failed', result);
    if (!result.aiLoaded || !Array.isArray(savedDocument.aiChat) || savedDocument.aiChat.length !== 2) return fail('AI chat save/load integrity failed', result);
    if (result.materialized > 4) return fail('lazy sheet cache limit exceeded', result);
    if (result.aiGeneratedSheets !== 8 || result.aiMaterialized > 4) return fail('AI-generated sheets bypassed lazy eviction', result);
    if (result.tabs !== 24 || result.nodes !== 120) return fail('stress document integrity failed', result);
    if (!result.saved || !reopened || !legacyReopened || savedDocument.sheets.length !== 24 || Object.keys(savedDocument.sheets[0].textboxes).length !== 120) return fail('new-format and legacy save/reopen integrity failed', result);
    await fs.promises.unlink(failedTempPath).catch(()=>{});
    await fs.promises.writeFile(resultPath, JSON.stringify({ ok: true, result }, null, 2));
    app.exit(0);
  } catch (error) {
    fail(error && error.stack ? error.stack : String(error));
  }
});
