/* EN: Exercise real renderer background controls, sheet history and persisted documents in a hidden Electron window.
   ZH: 在隐藏 Electron 窗口中验证真实背景控件、画布历史及持久化文档。 */
'use strict';
const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path');
const output=path.resolve(__dirname,'../../preview/testing/background');
fs.mkdirSync(output,{recursive:true});
process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(output,'profile-'+process.pid));
require('../main');
app.whenReady().then(async()=>{
  /* EN: Retain the regression result, save fixture and screenshot; exit unsuccessfully if any behavior assertion fails.
     ZH: 保留回归结果、保存样本与截图；任一行为断言失败即以失败码退出。 */
  const timer=setTimeout(()=>{console.error('BACKGROUND_SHEET_TIMEOUT');app.exit(1);},60000);
  const win=new BrowserWindow({show:false,width:1360,height:960,webPreferences:{offscreen:true,contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(__dirname,'../preload.js')}});
  try{
    await win.loadFile(path.join(__dirname,'../index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      // EN: Drive existing controls with DOM events so the test follows the same draft and apply paths as a user.
      // ZH: 通过 DOM 事件操作现有控件，使测试走与用户相同的草稿及应用流程。
      const wait=(ms=80)=>new Promise(r=>setTimeout(r,ms));
      const fire=(el,type='input')=>el.dispatchEvent(new Event(type,{bubbles:true}));
      const input=(id,value,type='input')=>{const el=document.getElementById(id);el.value=value;fire(el,type);};
      const open=()=>{openSettings();document.querySelector('[data-pane="paneAppearance"]').click();};
      const apply=async()=>{await document.getElementById('tcAppearanceApply').onclick();await wait();};
      await wait(250);dirty=false;await doNew('brace');hideStart();
      const check={};
      const entry=document.getElementById('sheetBackground');check.editingEntry=!!entry;
      if(entry){entry.click();check.entryOpensPanel=document.getElementById('settingsMask').classList.contains('show')||getComputedStyle(document.getElementById('settingsMask')).display!=='none';}else{check.entryOpensPanel=false;open();}
      await wait();
      const limited=document.querySelector('[name="tcBgMode"][value="limited"]');limited.checked=true;fire(limited,'change');
      input('tcCanvasWidth',1200);input('tcCanvasHeight',800);
      // EN: Use a large decoded image and compare exact bytes after import, save and reopen.
      // ZH: 使用较大解码图片，并在导入、保存和重开后比较完整字节。
      const source=document.createElement('canvas');source.width=2400;source.height=1200;
      const ctx=source.getContext('2d');ctx.fillStyle='#223344';ctx.fillRect(0,0,2400,1200);
      const sourceData=source.toDataURL('image/png'),blob=await new Promise(resolve=>source.toBlob(resolve,'image/png'));
      const file=new File([blob],'full-resolution-background.png',{type:'image/png'}),fileInput=document.getElementById('tcBgFile');
      Object.defineProperty(fileInput,'files',{value:[file],configurable:true});fire(fileInput,'change');
      for(let i=0;i<50&&!AppearanceEditor.getDraft().background.imageData;i++)await wait(20);
      const bg=AppearanceEditor.getDraft().background;
      check.importFitsFinite=bg.imageScale===50&&bg.positionX===0&&bg.positionY===0;
      check.originalResolution=bg.imageData===sourceData&&bg.imageWidth===2400&&bg.imageHeight===1200;
      const ref=document.querySelector('.tc-bg-reference'),pic=document.getElementById('tcBgPreviewImage');
      input('tcPreviewZoom',100);await wait();const low={ref:ref.getBoundingClientRect(),pic:pic.getBoundingClientRect()};
      input('tcPreviewZoom',200);await wait();const high={ref:ref.getBoundingClientRect(),pic:pic.getBoundingClientRect()};
      check.previewScalesTogether=Math.abs(high.ref.width/low.ref.width-2)<.02&&Math.abs(high.ref.height/low.ref.height-2)<.02&&Math.abs(high.pic.width/low.pic.width-2)<.02;
      const id=newTextbox('');roots.push(id);TB[id].x=3000;TB[id].y=1800;update();
      const real=els[id]._card,reference=ref.querySelector('.card');
      const realSize={width:real.offsetWidth,height:real.offsetHeight};
      check.defaultNodeProportions=Math.abs(real.offsetWidth-reference.offsetWidth)<1&&Math.abs(real.offsetHeight-reference.offsetHeight)<1;
      initHistory(true);await apply();closeSettings();
      const first=JSON.parse(serialize());check.sheetSerialized=!!first.sheets[0].appearance?.background?.imageData;
      addSheet('brace');open();input('tcCanvasWidth',1600);await apply();closeSettings();
      switchSheet(0);check.sheetIsolation=AppearanceEditor.getState().background.canvasWidth===1200;
      switchSheet(1);check.secondSheet=AppearanceEditor.getState().background.canvasWidth===1600;
      open();input('tcCanvasWidth',1800);await apply();closeSettings();undo();
      check.sheetUndo=curSheet===1&&AppearanceEditor.getState().background.canvasWidth===1600;
      redo();check.sheetRedo=curSheet===1&&AppearanceEditor.getState().background.canvasWidth===1800;
      switchSheet(0);check.otherSheetHistoryIsolation=AppearanceEditor.getState().background.canvasWidth===1200;
      open();const color=document.getElementById('tcBackgroundColor');
      if(color){color.value='#d2e3f4';fire(color);}else{const palette=document.querySelector('[data-palette="bg"]');palette.value='#d2e3f4';fire(palette);}
      await apply();closeSettings();
      check.backgroundColorApplied=getComputedStyle(document.getElementById('tcCanvasLimited')).backgroundColor==='rgb(210, 227, 244)';
      applyUISkin('skeleton');await wait();check.backgroundColorUnderSkin=getComputedStyle(document.getElementById('tcCanvasLimited')).backgroundColor==='rgb(210, 227, 244)';applyUISkin('default');
      switchSheet(1);check.colorIsolated=AppearanceEditor.getState().background.color!=='#d2e3f4';switchSheet(0);
      const saved=serialize();applyLoadedDocument(KnotJotDocumentModel.validateAndNormalizeDocument(JSON.parse(saved)),'roundtrip');
      check.reopenFirst=AppearanceEditor.getState().background.canvasWidth===1200&&AppearanceEditor.getState().background.imageData===sourceData;
      switchSheet(1);check.reopenSecond=AppearanceEditor.getState().background.canvasWidth===1800;
      const legacy=JSON.parse(saved);for(const sheet of legacy.sheets)delete sheet.appearance;legacy.appearance={background:{...JSON.parse(saved).appearance.background,canvasWidth:2200}};
      applyLoadedDocument(KnotJotDocumentModel.validateAndNormalizeDocument(legacy),'legacy');switchSheet(1);
      check.legacyInheritance=AppearanceEditor.getState().background.canvasWidth===2200;
      // EN: Resolve defaults for every legacy sheet at load time, before one sheet can update application defaults.
      // ZH: 加载时为所有旧画布确定默认值，避免其中一张修改应用默认值后影响尚未访问的画布。
      const noAppearance=JSON.parse(saved);delete noAppearance.appearance;for(const sheet of noAppearance.sheets)delete sheet.appearance;
      applyLoadedDocument(KnotJotDocumentModel.validateAndNormalizeDocument(noAppearance),'legacy-default');
      const inheritedWidth=AppearanceEditor.getState().background.canvasWidth;open();input('tcCanvasWidth',inheritedWidth+200);await apply();closeSettings();switchSheet(1);
      check.legacyUnvisitedSheetIsolation=AppearanceEditor.getState().background.canvasWidth===inheritedWidth;
      const malformed=JSON.parse(saved);malformed.sheets[0].appearance={background:{color:'url(invalid)',imageData:'not-an-image'}};
      try{KnotJotDocumentModel.validateAndNormalizeDocument(malformed);check.sheetValidation=false;}catch(_){check.sheetValidation=true;}
      applyLoadedDocument(KnotJotDocumentModel.validateAndNormalizeDocument(JSON.parse(saved)),'screenshot');document.getElementById('sheetBackground').click();await wait(250);
      return {check,saved,metrics:{low:{width:low.ref.width,height:low.ref.height},high:{width:high.ref.width,height:high.ref.height},reference:{width:reference.offsetWidth,height:reference.offsetHeight},real:realSize}};
    })()`);
    fs.writeFileSync(path.join(output,'two-sheet-background.knotjot'),result.saved);
    // EN: Reopen the saved bytes in a fresh renderer to prove the persisted sheet backgrounds survive process-local state loss.
    // ZH: 在新的渲染器中重开已保存字节，验证丢失进程内状态后仍能恢复各画布背景。
    const reopened=new BrowserWindow({show:false,width:1360,height:960,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(__dirname,'../preload.js')}});
    await reopened.loadFile(path.join(__dirname,'../index.html'));
    const reopenedCheck=await reopened.webContents.executeJavaScript(`(async()=>{await new Promise(r=>setTimeout(r,250));const d=KnotJotDocumentModel.validateAndNormalizeDocument(JSON.parse(${JSON.stringify(fs.readFileSync(path.join(output,'two-sheet-background.knotjot'),'utf8'))}));applyLoadedDocument(d,'disk-reopen');const first=AppearanceEditor.getState().background;switchSheet(1);const second=AppearanceEditor.getState().background;return first.canvasWidth===1200&&first.color==='#d2e3f4'&&first.imageWidth===2400&&second.canvasWidth===1800&&second.color!=='#d2e3f4';})()`);
    result.check.diskReopenFreshRenderer=reopenedCheck;reopened.destroy();
    delete result.saved;
    fs.writeFileSync(path.join(output,(process.env.BASELINE?'baseline':'fixed')+'-result.json'),JSON.stringify(result,null,2));
    fs.writeFileSync(path.join(output,(process.env.BASELINE?'baseline':'fixed')+'-preview.png'),(await win.webContents.capturePage()).toPNG());
    console.log('BACKGROUND_SHEET_RESULT',JSON.stringify(result));clearTimeout(timer);
    app.exit(Object.values(result.check).every(v=>v===true)?0:1);
  }catch(error){clearTimeout(timer);console.error(error.stack||String(error));app.exit(1);}
});
