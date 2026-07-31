const {spawn}=require('child_process');
const fs=require('fs'),path=require('path');

const exe=process.env.TC_PACKAGED_EXE;
const profile=process.env.TC_PACKAGED_PROFILE;
const port=Number(process.env.TC_DEBUG_PORT||19339);
if(!exe||!fs.existsSync(exe)||!profile)throw new Error('TC_PACKAGED_EXE/TC_PACKAGED_PROFILE 无效');

const child=spawn(exe,[`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`],{stdio:'ignore',windowsHide:true});
const wait=ms=>new Promise(r=>setTimeout(r,ms));
async function target(){for(let i=0;i<120;i++){try{const list=await (await fetch(`http://127.0.0.1:${port}/json`)).json();const page=list.find(x=>x.type==='page'&&x.webSocketDebuggerUrl&&/index\.html/i.test(x.url||''));if(page){await wait(500);return page;}}catch(_){}await wait(125);}throw new Error('未找到已加载的打包程序页面');}
async function evaluate(wsUrl,expression){
  const ws=new WebSocket(wsUrl),pending=new Map();let next=0;
  await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
  ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id&&pending.has(m.id)){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(new Error(m.error.message)):p.resolve(m.result);}};
  const send=(method,params)=>new Promise((resolve,reject)=>{const id=++next;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  try{const r=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true,userGesture:true});if(r.exceptionDetails)throw new Error(r.exceptionDetails.text);return r.result.value;}
  finally{ws.close();}
}
function stop(){try{child.kill();}catch(_){} }
(async()=>{try{
  const page=await target();
  const result=await evaluate(page.webSocketDebuggerUrl,`(async()=>{
    const wait=(ms=80)=>new Promise(r=>setTimeout(r,ms)),fire=(el,type='input')=>el.dispatchEvent(new Event(type,{bubbles:true}));
    for(let i=0;i<100&&(!window.AppearanceEditor||!document.getElementById('tcAppearancePanel'));i++)await wait(50);
    openSettings();document.querySelector('[data-pane="paneAppearance"]').click();await wait();
    const c=document.createElement('canvas');c.width=32;c.height=16;const x=c.getContext('2d');x.fillStyle='#2468c9';x.fillRect(0,0,32,16);
    const blob=await new Promise((resolve,reject)=>c.toBlob(v=>v?resolve(v):reject(new Error('PNG 生成失败')),'image/png'));
    const file=new File([blob],'packaged-background.png',{type:'image/png'}),input=document.getElementById('tcBgFile');Object.defineProperty(input,'files',{value:[file],configurable:true});fire(input,'change');
    for(let i=0;i<80&&!document.getElementById('tcBgName').textContent.includes('packaged-background');i++)await wait(50);
    const panel=document.getElementById('tcAppearancePanel'),limited=panel.querySelector('input[name="tcBgMode"][value="limited"]');limited.checked=true;fire(limited,'change');
    const preview=document.getElementById('tcBgPreview'),previewImage=document.getElementById('tcBgPreviewImage'),reference=preview.querySelector('.tc-bg-reference'),previewGrid=preview.querySelector('.tc-bg-preview-grid'),zoom=document.getElementById('tcPreviewZoom');
    zoom.value=25;fire(zoom);await wait();const low={image:previewImage.getBoundingClientRect(),ref:reference.getBoundingClientRect()};
    zoom.value=800;fire(zoom);await wait();const high={image:previewImage.getBoundingClientRect(),ref:reference.getBoundingClientRect()},bounds=preview.getBoundingClientRect();
    const previewNoGrid=getComputedStyle(previewGrid).display==='none';
    const referenceFixed=Math.abs(low.ref.width-high.ref.width)<.5&&Math.abs(low.ref.height-high.ref.height)<.5&&high.ref.left>=bounds.left&&high.ref.right<=bounds.right&&high.ref.top>=bounds.top&&high.ref.bottom<=bounds.bottom;
    const zoomVisible=high.image.width>low.image.width*20&&high.image.height>low.image.height*20;
    document.getElementById('tcAppearanceApply').click();for(let i=0;i<80&&!document.getElementById('tcAppearanceStatus').textContent.includes('写入当前导图');i++)await wait(50);
    const applied=AppearanceEditor.getState().background,layer=document.getElementById('tcCanvasLimited'),actualGrid=document.getElementById('tcCanvasLimitedGrid');
    const actualNoGrid=layer.classList.contains('has-image')&&getComputedStyle(actualGrid).display==='none';
    const immediate=applied.enabled&&applied.mode==='limited'&&applied.imageData.startsWith('data:image/png;base64,')&&getComputedStyle(layer).display==='block';
    closeSettings();await wait();openSettings();document.querySelector('[data-pane="paneAppearance"]').click();await wait();
    const reopened=AppearanceEditor.getDraft().background.imageData===applied.imageData&&document.getElementById('tcBgName').textContent.includes('packaged-background');
    closeSettings();dirty=false;await doNew('brace');await wait();
    const inherited=AppearanceEditor.getState().background.imageData===applied.imageData&&getComputedStyle(layer).display==='block';
    const disk=JSON.parse(await api.loadAppearance()),diskSaved=disk.background.imageData===applied.imageData;
    return {immediate,previewNoGrid,actualNoGrid,referenceFixed,zoomVisible,reopened,inherited,diskSaved,exe:await api.skinsLocation().then(x=>x.executable)};
  })()`);
  console.log('PACKAGED_APPEARANCE_PERSISTENCE_RESULT',JSON.stringify(result));
  if(!result||!result.immediate||!result.previewNoGrid||!result.actualNoGrid||!result.referenceFixed||!result.zoomVisible||!result.reopened||!result.inherited||!result.diskSaved||path.resolve(result.exe)!==path.resolve(exe))process.exitCode=1;
}catch(e){console.error('PACKAGED_APPEARANCE_PERSISTENCE_FAILED',e.stack||String(e));process.exitCode=1;}
finally{stop();setTimeout(()=>process.exit(process.exitCode||0),800);}})();
