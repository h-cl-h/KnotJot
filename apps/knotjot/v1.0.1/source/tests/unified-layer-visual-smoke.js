const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-unified-visual-${process.pid}`));
require('../main');

const styleFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
function finish(win,code,message,details){
/* EN: finish in module: call JSON.stringify, console.error, console.log.
   ZH: module 中的 finish：调用 JSON.stringify、console.error、console.log。 */

  const line=JSON.stringify(details||{});
  if(message)console.error('UNIFIED_LAYER_VISUAL_FAILED',message,line);else console.log('UNIFIED_LAYER_VISUAL_OK',line);
  try{win.destroy();}catch(_){ }
  app.exit(code);
}

app.whenReady().then(async()=>{
/* EN: Continue module at app.whenReady().then: call path.join, win.webContents.setBackgroundThrottling, setTimeout; update metrics.screenshot.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 path.join、win.webContents.setBackgroundThrottling、setTimeout；更新 metrics.screenshot。 */

  const sourceRoot=path.join(__dirname,'..');
  const packagedRoot=process.env.KNOTJOT_PACKAGED_ROOT||path.join(__dirname,'..','dist','win-unpacked','resources','app.asar');
  const appRoot=process.env.TEST_PACKAGED==='1'?packagedRoot:sourceRoot;
  const win=new BrowserWindow({show:false,width:900,height:650,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(appRoot,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  const timer=setTimeout(()=>/* EN: Callback for setTimeout: call finish; return finish(win,1,'timeout'). ZH: setTimeout 的回调：调用 finish；返回 finish(win,1,'timeout')。 */ finish(win,1,'timeout'),60000);
  try{
    await win.loadFile(path.join(appRoot,'index.html'));
    const library=JSON.parse(fs.readFileSync(styleFile,'utf8'));
    const style=library.styles.find(x=>/* EN: Test an item for library.styles.find: return x.id==='custom-style'. ZH: 判断 library.styles.find 的元素条件：返回 x.id==='custom-style'。 */ x.id==='custom-style');
    if(!style)throw new Error('custom-style not found');
    await win.webContents.executeJavaScript(`(()=>{hideStart();scale=1;panX=0;panY=0;applyTransform();const id=newTextbox('');TB[id].x=390;TB[id].y=330;TB[id].textStyle=${JSON.stringify(style)};roots.push(id);update();startEdit(id);window.__visualTestId=id;})()`);
    const sequence=[['one','1'],['three','123'],['six','123456'],['nine','123456789'],['shrink-one','1'],['grow-nine','123456789']];
    const results=[];
    for(const [label,text] of sequence){
      const metrics=await win.webContents.executeJavaScript(`(async()=>{const id=window.__visualTestId,e=els[id],inner=e._inner;inner.textContent=${JSON.stringify(text)};inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:null}));await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>requestAnimationFrame(r))));const svg=e._card.querySelector('.ts-decoration'),outer=svg.querySelector('.ts-artwork > g > rect'),or=outer.getBoundingClientRect(),ir=inner.getBoundingClientRect(),range=document.createRange();range.selectNodeContents(inner);const gr=range.getBoundingClientRect(),center=r=>({x:r.left+r.width/2,y:r.top+r.height/2}),oc=center(or),ic=center(ir),gc=center(gr);return{label:${JSON.stringify(label)},text:inner.textContent,unified:inner.closest('svg')===svg&&inner.parentElement.tagName.toLowerCase()==='foreignobject',outer:{x:or.x,y:or.y,w:or.width,h:or.height},region:{x:ir.x,y:ir.y,w:ir.width,h:ir.height},glyph:{x:gr.x,y:gr.y,w:gr.width,h:gr.height},centerError:{regionX:ic.x-oc.x,regionY:ic.y-oc.y,glyphX:gc.x-ic.x,glyphY:gc.y-ic.y}};})()`);
      const png=await win.webContents.capturePage();
      const screenshot=path.join(os.tmpdir(),`knotjot-unified-${process.pid}-${label}.png`);
      fs.writeFileSync(screenshot,png.toPNG());
      metrics.screenshot=screenshot;
      results.push(metrics);
    }
    const ok=results.every(x=>/* EN: Test an item for results.every: call Math.abs. ZH: 判断 results.every 的元素条件：调用 Math.abs。 */ x.unified&&Math.abs(x.centerError.regionX)<.75&&Math.abs(x.centerError.regionY)<.75&&Math.abs(x.centerError.glyphX)<.75&&Math.abs(x.centerError.glyphY)<.75);
    clearTimeout(timer);finish(win,ok?0:1,ok?'':'centre mismatch',results);
  }catch(e){clearTimeout(timer);finish(win,1,e.stack||String(e));}
});
