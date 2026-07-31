const {app,BrowserWindow}=require('electron');
const path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-appearance-${process.pid}`));
require('../main');

function fail(message,details){console.error('APPEARANCE_BACKGROUND_FAILED',message,details||'');app.exit(1);}
app.whenReady().then(async()=>{
  const timer=setTimeout(()=>fail('测试超时'),90000);
  const win=new BrowserWindow({show:false,width:1360,height:900,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(__dirname,'..','preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  try{
    await win.loadFile(path.join(__dirname,'..','index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const wait=(ms=80)=>new Promise(r=>setTimeout(r,ms));
      const fire=(el,type='input')=>el.dispatchEvent(new Event(type,{bubbles:true}));
      openSettings();document.querySelector('[data-pane="paneAppearance"]').click();await wait();
      const panel=document.getElementById('tcAppearancePanel'),initialUnlocked=!panel.classList.contains('is-locked');
      const cardBefore=(()=>{const id=newTextbox('背景参考');TB[id].x=2920;TB[id].y=1800;roots.push(id);update();return{id,bg:getComputedStyle(els[id]._card).backgroundColor,border:getComputedStyle(els[id]._card).borderColor};})();
      const defaultBg=AppearanceEditor.getDraft().background,defaultCanvasUseful=defaultBg.canvasWidth===6000&&defaultBg.canvasHeight===3600;
      const refCard=panel.querySelector('.tc-bg-reference .card'),refInner=panel.querySelector('.tc-bg-reference .card-inner'),realCard=els[cardBefore.id]._card;
      const referenceIsRealDefault=!!refCard&&!!refInner&&refInner.textContent===''&&getComputedStyle(refCard).backgroundColor===getComputedStyle(realCard).backgroundColor&&getComputedStyle(refCard).borderRadius===getComputedStyle(realCard).borderRadius&&getComputedStyle(refCard).borderTopWidth===getComputedStyle(realCard).borderTopWidth;

      const sourceCanvas=document.createElement('canvas');sourceCanvas.width=8200;sourceCanvas.height=2;
      const sourceContext=sourceCanvas.getContext('2d');sourceContext.fillStyle='#2468c9';sourceContext.fillRect(0,0,8200,2);
      const pngBlob=await new Promise((resolve,reject)=>sourceCanvas.toBlob(v=>v?resolve(v):reject(new Error('测试 PNG 生成失败')),'image/png'));
      const file=new File([pngBlob,new Uint8Array(9*1024*1024)],'background-smoke-unlimited.png',{type:'image/png'}),input=document.getElementById('tcBgFile');
      Object.defineProperty(input,'files',{value:[file],configurable:true});fire(input,'change');
      for(let i=0;i<40&&!document.getElementById('tcBgName').textContent.includes('background-smoke');i++)await wait(50);
      const imported=document.getElementById('tcBgName').textContent.includes('8200×2')&&document.getElementById('tcAppearanceStatus').textContent.includes('已载入预览');
      const importedState=AppearanceEditor.getDraft().background;
      const unlimitedImport=file.size>8*1024*1024&&importedState.imageData.length>12*1024*1024&&importedState.imageWidth===8200&&importedState.imageHeight===2;
      const limited=panel.querySelector('input[name="tcBgMode"][value="limited"]');limited.checked=true;fire(limited,'change');
      const width=document.getElementById('tcCanvasWidth'),height=document.getElementById('tcCanvasHeight'),scaleInput=document.getElementById('tcImageScale');
      width.value=1200;fire(width);height.value=800;fire(height);scaleInput.value=75;fire(scaleInput);
      const previewBox=document.getElementById('tcBgPreview'),previewImage=document.getElementById('tcBgPreviewImage'),previewRef=previewBox.querySelector('.tc-bg-reference'),previewGrid=previewBox.querySelector('.tc-bg-preview-grid'),previewZoomInput=document.getElementById('tcPreviewZoom');
      previewZoomInput.value=25;fire(previewZoomInput);await wait();const zoomLow={image:previewImage.getBoundingClientRect(),ref:previewRef.getBoundingClientRect()};
      previewZoomInput.value=800;fire(previewZoomInput);await wait();const zoomHigh={image:previewImage.getBoundingClientRect(),ref:previewRef.getBoundingClientRect()},boxRect=previewBox.getBoundingClientRect();
      const referenceStaysFixed=Math.abs(zoomLow.ref.width-zoomHigh.ref.width)<.5&&Math.abs(zoomLow.ref.height-zoomHigh.ref.height)<.5&&zoomHigh.ref.left>=boxRect.left&&zoomHigh.ref.right<=boxRect.right&&zoomHigh.ref.top>=boxRect.top&&zoomHigh.ref.bottom<=boxRect.bottom;
      const previewZoomVisible=zoomHigh.image.width>zoomLow.image.width*20&&zoomHigh.image.height>zoomLow.image.height*20;
      const previewImageNoGrid=getComputedStyle(previewGrid).display==='none';
      document.getElementById('tcAppearanceApply').click();for(let i=0;i<40&&!document.getElementById('tcAppearanceStatus').textContent.includes('写入当前导图');i++)await wait(50);
      const saved=JSON.parse(serialize()),bg=saved.appearance&&saved.appearance.background,layer=document.getElementById('tcCanvasLimited');
      const actualGrid=document.getElementById('tcCanvasLimitedGrid'),limitedApplied=bg&&bg.enabled&&bg.mode==='limited'&&bg.canvasWidth===1200&&bg.canvasHeight===800&&bg.imageScale===75&&bg.imageData.startsWith('data:image/png;base64,')&&getComputedStyle(layer).display==='block'&&canvas.classList.contains('tc-limited');
      const actualImageNoGrid=layer.classList.contains('has-image')&&getComputedStyle(actualGrid).display==='none';
      const appearanceFile=JSON.parse(await api.loadAppearance()),globalPersisted=appearanceFile.app==='knotjot-appearance'&&appearanceFile.background.imageData===bg.imageData&&appearanceFile.background.canvasWidth===1200;
      const cardAfter={bg:getComputedStyle(els[cardBefore.id]._card).backgroundColor,border:getComputedStyle(els[cardBefore.id]._card).borderColor};
      const textboxUntouched=cardBefore.bg===cardAfter.bg&&cardBefore.border===cardAfter.border&&!els[cardBefore.id].classList.contains('ts-custom');
      const q=AppearanceEditor.canvasBounds(),newId=addRootAt(q.left-500,q.top-500);commitEdit(newId);layout();const clamped=TB[newId]._x>=q.left-25&&TB[newId]._cy-TB[newId]._h/2>=q.top-25;

      closeSettings();await wait();openSettings();document.querySelector('[data-pane="paneAppearance"]').click();await wait();
      const reopened=AppearanceEditor.getDraft().background,reopenKept=reopened.imageData===bg.imageData&&reopened.canvasWidth===1200&&document.getElementById('tcBgName').textContent.includes('background-smoke-unlimited');
      const reopenedReference=panel.querySelector('.tc-bg-reference .card-inner').textContent===''&&document.getElementById('tcPreviewZoom').max==='800';
      width.value=1600;fire(width);closeSettings();await wait();const cancelKept=JSON.parse(serialize()).appearance.background.canvasWidth===1200;
      openSettings();document.querySelector('[data-pane="paneAppearance"]').click();await wait();
      const accent=document.querySelector('[data-palette="accent"]');accent.value='#2468c9';fire(accent);document.getElementById('tcAppearanceApply').click();for(let i=0;i<40&&AppearanceEditor.getState().palette.accent!=='#2468c9';i++)await wait(50);
      const paletteApplied=getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()==='#2468c9';
      applyUISkin('skeleton');await wait();const skinDecoupled=panel.classList.contains('palette-locked')&&getComputedStyle(layer).display==='block';
      applyUISkin('default');await wait();const restored=!panel.classList.contains('palette-locked')&&getComputedStyle(layer).display==='block'&&getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()==='#2468c9';
      const unlimitedRadio=panel.querySelector('input[name="tcBgMode"][value="unlimited"]');unlimitedRadio.checked=true;fire(unlimitedRadio,'change');document.getElementById('tcAppearanceApply').click();
      for(let i=0;i<40&&AppearanceEditor.getState().background.mode!=='unlimited';i++)await wait(50);
      const unlimitedLayer=document.getElementById('tcCanvasUnlimited'),unlimitedApplied=JSON.parse(serialize()).appearance.background.mode==='unlimited'&&getComputedStyle(unlimitedLayer).display==='block'&&!canvas.classList.contains('tc-limited')&&getComputedStyle(unlimitedLayer).backgroundRepeat==='repeat';
      closeSettings();
      const savedFile=JSON.parse(serialize());AppearanceEditor.loadDocumentAppearance({background:{enabled:false}});applyLoadedDocument(savedFile,'saved-reload');await wait();
      const savedFileReloaded=AppearanceEditor.getState().background.imageData===savedFile.appearance.background.imageData&&getComputedStyle(unlimitedLayer).display==='block';
      dirty=false;await doNew('brace');await wait();const newMapBg=AppearanceEditor.getState().background;
      const newMapInherited=newMapBg.enabled&&newMapBg.imageData===savedFile.appearance.background.imageData&&JSON.parse(serialize()).appearance.background.imageData===newMapBg.imageData;
      const legacy=JSON.parse(serialize());delete legacy.appearance;applyLoadedDocument(legacy,'legacy');await wait();const legacyUsesDefault=AppearanceEditor.getState().background.imageData===newMapBg.imageData;
      return {initialUnlocked,defaultCanvasUseful,referenceIsRealDefault,imported,unlimitedImport,previewImageNoGrid,actualImageNoGrid,referenceStaysFixed,previewZoomVisible,limitedApplied,globalPersisted,textboxUntouched,clamped,reopenKept,reopenedReference,cancelKept,paletteApplied,skinDecoupled,restored,unlimitedApplied,savedFileReloaded,newMapInherited,legacyUsesDefault,background:{mode:bg&&bg.mode,width:bg&&bg.canvasWidth,height:bg&&bg.canvasHeight,imageScale:bg&&bg.imageScale},layerDisplay:getComputedStyle(layer).display};
    })()`);
    const loaded=new Promise(resolve=>win.webContents.once('did-finish-load',resolve));win.reload();await loaded;
    const restartRestored=await win.webContents.executeJavaScript(`(async()=>{for(let i=0;i<100&&(!window.AppearanceEditor||!AppearanceEditor.getState().background.imageData);i++)await new Promise(r=>setTimeout(r,50));const b=AppearanceEditor.getState().background;return b.enabled&&b.imageData.length>12*1024*1024&&b.imageWidth===8200&&b.imageHeight===2;})()`);
    result.restartRestored=restartRestored;
    console.log('APPEARANCE_BACKGROUND_RESULT',JSON.stringify(result));
    if(!Object.entries(result).filter(([k])=>!['background','layerDisplay'].includes(k)).every(([,v])=>v===true))return fail('外观/背景检查失败',result);
    clearTimeout(timer);app.exit(0);
  }catch(err){fail(err.stack||String(err));}
});
