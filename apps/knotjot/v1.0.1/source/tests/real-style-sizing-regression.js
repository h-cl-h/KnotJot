const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-real-sizing-${process.pid}`));
require('../main');

const root=path.join(__dirname,'..');
const packagedRoot=process.env.KNOTJOT_PACKAGED_ROOT||path.join(root,'dist','win-unpacked','resources','app.asar');
const appRoot=process.env.TEST_PACKAGED==='1'?packagedRoot:root;
const styleFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
const outputDir=path.join(os.tmpdir(),`knotjot-real-sizing-artifacts-${process.pid}`);

function finish(win,code,details){
/* EN: finish in module: call code?console.error:console.log, JSON.stringify, win.destroy.
   ZH: module 中的 finish：调用 code?console.error:console.log、JSON.stringify、win.destroy。 */

  const label=code?'REAL_STYLE_SIZING_FAILED':'REAL_STYLE_SIZING_OK';
  (code?console.error:console.log)(label,JSON.stringify(details));
  try{win.destroy();}catch(_){}
  app.exit(code);
}

app.whenReady().then(async()=>{
/* EN: Continue module at app.whenReady().then: call path.join, win.webContents.setBackgroundThrottling, setTimeout; update result.screenshot, result.checks.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 path.join、win.webContents.setBackgroundThrottling、setTimeout；更新 result.screenshot、result.checks。 */

  const win=new BrowserWindow({show:false,width:1100,height:760,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(appRoot,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  const timer=setTimeout(()=>/* EN: Callback for setTimeout: call finish; return finish(win,1,{error:'timeout'}). ZH: setTimeout 的回调：调用 finish；返回 finish(win,1,{error:'timeout'})。 */ finish(win,1,{error:'timeout'}),60000);
  try{
    await win.loadFile(path.join(appRoot,'index.html'));
    const library=JSON.parse(fs.readFileSync(styleFile,'utf8'));
    const mainStyle=library.styles.find(x=>/* EN: Test an item for library.styles.find: return x.id==='custom-style'. ZH: 判断 library.styles.find 的元素条件：返回 x.id==='custom-style'。 */ x.id==='custom-style')||library.styles[0];
    if(!mainStyle)throw new Error('No real custom style');
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const styles=${JSON.stringify(library.styles)};
      const mainStyle=${JSON.stringify(mainStyle)};
      const frame=()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>requestAnimationFrame(r))));
      hideStart();scale=1;panX=0;panY=0;applyTransform();
      const create=(style,mode)=>{const id=newTextbox('');const t=TB[id];t.x=430;t.y=330;t.textStyle=JSON.parse(JSON.stringify(style));t.textSizing={mode,aspect:Number(style.textSizing&&style.textSizing.aspect)||1.8};t._textSizingOverride=true;roots.push(id);update();startEdit(id);return id;};
      const metric=id=>{const e=els[id],card=e._card,inner=e._inner,svg=card.querySelector('.ts-decoration'),cr=card.getBoundingClientRect(),ir=inner.getBoundingClientRect(),range=document.createRange();range.selectNodeContents(inner);const gr=range.getBoundingClientRect(),outer=svg&&svg.querySelector('.ts-artwork > g > :is(rect,ellipse,line,image)'),or=outer&&outer.getBoundingClientRect(),inside=gr.width===0||(gr.left>=ir.left-1&&gr.right<=ir.right+1&&gr.top>=ir.top-1&&gr.bottom<=ir.bottom+1),centre={x:(gr.left+gr.right-ir.left-ir.right)/2,y:(gr.top+gr.bottom-ir.top-ir.bottom)/2};return{w:cr.width,h:cr.height,ratio:cr.width/cr.height,region:{x:ir.x,y:ir.y,w:ir.width,h:ir.height},glyph:{x:gr.x,y:gr.y,w:gr.width,h:gr.height},inside,centre,unified:!!svg&&inner.closest('svg')===svg,outerRatio:or&&or.width/or.height};};
      const run=async mode=>{const id=create(mainStyle,mode),rows=[];for(const text of ['1','123','123456','123456789','1']){const inner=els[id]._inner;inner.textContent=text;inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:null}));await frame();rows.push({text,...metric(id)});}commitEdit(id);doDelete(id);return rows;};
      const uniform=await run('uniform'),stretch=await run('stretch'),baselines=[];
      for(const style of styles){const id=create(style,'stretch');const inner=els[id]._inner;inner.textContent='1';inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:'1'}));await frame();baselines.push({id:style.id,...metric(id)});commitEdit(id);doDelete(id);}
      const previewStyle=styles.find(x=>x.id==='my-text-style-4')||mainStyle,previewId=create(previewStyle,'stretch'),previewInner=els[previewId]._inner;previewInner.textContent='123456789';previewInner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:null}));await frame();
      return{uniform,stretch,baselines};
    })()`);
    fs.mkdirSync(outputDir,{recursive:true});
    const png=await win.webContents.capturePage();
    const screenshot=path.join(outputDir,'final.png');fs.writeFileSync(screenshot,png.toPNG());
    result.screenshot=screenshot;
    const near=(a,b,t=.03)=>/* EN: near in module: call Math.abs; return Math.abs(a-b)<=t. ZH: module 中的 near：调用 Math.abs；返回 Math.abs(a-b)<=t。 */ Math.abs(a-b)<=t;
    const unifiedAndContained=[...result.uniform,...result.stretch,...result.baselines].every(x=>/* EN: Test an item for [...result.uniform,...result.stretch,...result.baselines].every: call Math.abs; return x.unified&&x.inside&&Math.abs(x.centre.x)<1&&Math.abs(x.centre.y)<1. ZH: 判断 [...result.uniform,...result.stretch,...result.baselines].every 的元素条件：调用 Math.abs；返回 x.unified&&x.inside&&Math.abs(x.centre.x)<1&&Math.abs(x.centre.y)<1。 */ x.unified&&x.inside&&Math.abs(x.centre.x)<1&&Math.abs(x.centre.y)<1);
    const uniformRatio=result.uniform.every(x=>/* EN: Test an item for result.uniform.every: call near; return near(x.ratio,1.8,.025). ZH: 判断 result.uniform.every 的元素条件：调用 near；返回 near(x.ratio,1.8,.025)。 */ near(x.ratio,1.8,.025));
    const uniformWaitsForOverflow=near(result.uniform[0].w,result.uniform[1].w,1.5)&&near(result.uniform[0].h,result.uniform[1].h,1.5);
    const uniformGrowsAfterOverflow=result.uniform[2].w>result.uniform[1].w+2&&result.uniform[2].h>result.uniform[1].h+2&&result.uniform[3].w>=result.uniform[2].w-1&&result.uniform[3].h>=result.uniform[2].h-1;
    const uniformShrinks=near(result.uniform[0].w,result.uniform[4].w,1.5)&&near(result.uniform[0].h,result.uniform[4].h,1.5);
    const stretchStartsAuthored=result.baselines.every(x=>/* EN: Test an item for result.baselines.every: call near; return near(x.ratio,1.8,.025). ZH: 判断 result.baselines.every 的元素条件：调用 near；返回 near(x.ratio,1.8,.025)。 */ near(x.ratio,1.8,.025));
    const stretchWaitsForOverflow=near(result.stretch[0].w,result.stretch[1].w,1.5)&&near(result.stretch[0].h,result.stretch[1].h,1.5);
    const stretchIndependent=result.stretch[2].w>result.stretch[1].w+2&&result.stretch[3].w>result.stretch[2].w+2&&result.stretch.slice(0,4).every(x=>/* EN: Test an item for result.stretch.slice(0,4).every: call near; return near(x.h,result.stretch[0].h,1.5). ZH: 判断 result.stretch.slice(0,4).every 的元素条件：调用 near；返回 near(x.h,result.stretch[0].h,1.5)。 */ near(x.h,result.stretch[0].h,1.5));
    const stretchShrinks=near(result.stretch[0].w,result.stretch[4].w,1.5)&&near(result.stretch[0].h,result.stretch[4].h,1.5);
    result.checks={unifiedAndContained,uniformRatio,uniformWaitsForOverflow,uniformGrowsAfterOverflow,uniformShrinks,stretchStartsAuthored,stretchWaitsForOverflow,stretchIndependent,stretchShrinks};
    const ok=Object.values(result.checks).every(Boolean);
    clearTimeout(timer);finish(win,ok?0:1,result);
  }catch(e){clearTimeout(timer);finish(win,1,{error:e.stack||String(e),styleFile});}
});
