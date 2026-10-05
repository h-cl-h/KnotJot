const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-region-roundtrip-${process.pid}`));
require('../main');

const styleFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
function finish(win,code,message,details){
/* EN: finish in module: call console.error, JSON.stringify, console.log.
   ZH: module 中的 finish：调用 console.error、JSON.stringify、console.log。 */

  if(message)console.error(message,JSON.stringify(details||''));else console.log('TEXT_REGION_ROUNDTRIP_OK',JSON.stringify(details));
  try{win.destroy();}catch(_){ }
  app.exit(code);
}

app.whenReady().then(async()=>{
/* EN: Continue module at app.whenReady().then: call path.join, win.webContents.setBackgroundThrottling, setTimeout.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 path.join、win.webContents.setBackgroundThrottling、setTimeout。 */

  const root=path.join(__dirname,'..');
  const packagedRoot=process.env.KNOTJOT_PACKAGED_ROOT||path.join(__dirname,'..','dist','win-unpacked','resources','app.asar');
  const appRoot=process.env.TEST_PACKAGED==='1'?packagedRoot:root;
  const win=new BrowserWindow({show:false,width:1280,height:820,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(appRoot,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  const timer=setTimeout(()=>/* EN: Callback for setTimeout: call finish; return finish(win,1,'TEXT_REGION_ROUNDTRIP_FAILED timeout'). ZH: setTimeout 的回调：调用 finish；返回 finish(win,1,'TEXT_REGION_ROUNDTRIP_FAILED timeout')。 */ finish(win,1,'TEXT_REGION_ROUNDTRIP_FAILED timeout'),90000);
  try{
    await win.loadFile(path.join(appRoot,'index.html'));
    const allStyles=JSON.parse(fs.readFileSync(styleFile,'utf8')).styles.filter(s=>/* EN: Test an item for JSON.parse(fs.readFileSync(styleFile,'utf8')).styles.filter: return s&&s.replaceFrame&&s.textRegion&&s.layers&&s.layers.length. ZH: 判断 JSON.parse(fs.readFileSync(styleFile,'utf8')).styles.filter 的元素条件：返回 s&&s.replaceFrame&&s.textRegion&&s.layers&&s.layers.length。 */ s&&s.replaceFrame&&s.textRegion&&s.layers&&s.layers.length);
    const styles=[allStyles.find(s=>/* EN: Test an item for allStyles.find: return s.id==='custom-style'. ZH: 判断 allStyles.find 的元素条件：返回 s.id==='custom-style'。 */ s.id==='custom-style')||allStyles.find(s=>/* EN: Test an item for allStyles.find: call s.layers.some. ZH: 判断 allStyles.find 的元素条件：调用 s.layers.some。 */ s.layers.some(l=>/* EN: Test an item for s.layers.some: return l.clipMode==='subtract'. ZH: 判断 s.layers.some 的元素条件：返回 l.clipMode==='subtract'。 */ l.clipMode==='subtract'))].filter(Boolean);
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const styles=${JSON.stringify(styles)};
      const frames=(count=2)=>new Promise(resolve=>{const next=()=>count--?requestAnimationFrame(next):resolve();next();});
      const replace=(inner,text,inputType)=>{inner.textContent=text;inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType,data:null}));};
      const close=(a,b,tol=1)=>Math.abs(a-b)<=tol;
      const results=[];
      for(const style of styles){for(const zoom of [.49,1,1.6]){scale=zoom;applyTransform();
        const id=newTextbox('');TB[id].x=2400;TB[id].y=1500;TB[id].textStyle=JSON.parse(JSON.stringify(style));roots.push(id);update();startEdit(id);const e=els[id],inner=e._inner,card=e._card,tr=style.textRegion,samples=[];
        const take=label=>{const c=card.getBoundingClientRect(),r=inner.getBoundingClientRect(),svgEl=card.querySelector('.ts-decoration'),svg=svgEl.getBoundingClientRect(),fo=inner.parentElement,viewBox=svgEl.getAttribute('viewBox').split(/\s+/).map(Number),range=document.createRange();range.selectNodeContents(inner);const g=range.getBoundingClientRect(),expected={x:c.x+c.width*tr.x/100,y:c.y+c.height*tr.y/100,w:c.width*tr.w/100,h:c.height*tr.h/100},anchor={x:c.x+c.width*(tr.x+tr.w/2)/100,y:c.y+c.height*(tr.y+tr.h/2)/100};const cs=getComputedStyle(inner),ns=getComputedStyle(e);samples.push({label,text:inner.textContent,c:{x:c.x,y:c.y,w:c.width,h:c.height},r:{x:r.x,y:r.y,w:r.width,h:r.height},g:{x:g.x,y:g.y,w:g.width,h:g.height,cx:g.x+g.width/2,cy:g.y+g.height/2},svg:{x:svg.x,y:svg.y,w:svg.width,h:svg.height},expected,anchor,unified:fo&&fo.tagName.toLowerCase()==='foreignobject'&&fo.parentElement===svgEl&&svgEl.classList.contains('ts-composite'),pixelPlane:viewBox.length===4&&close(viewBox[2],card.offsetWidth,1)&&close(viewBox[3],card.offsetHeight,1),err:{x:r.x-expected.x,y:r.y-expected.y,w:r.width-expected.w,h:r.height-expected.h},glyphErr:{cx:g.x+g.width/2-(r.x+r.width/2),cy:g.y+g.height/2-(r.y+r.height/2)},css:{left:cs.left,top:cs.top,width:cs.width,height:cs.height,position:cs.position,transform:cs.transform,nodeTransition:ns.transitionDuration}});};
        for(let n=1;n<=9;n++){replace(inner,'123456789'.slice(0,n),'insertText');await frames();if(n===1||n===3||n===6||n===9)take('up-'+n);}
        replace(inner,'1','deleteContentBackward');await frames();take('down-1');
        replace(inner,'123456789','insertText');await frames();take('up-again-9');
        const geometry=samples.every(x=>close(x.r.x,x.expected.x)&&close(x.r.y,x.expected.y)&&close(x.r.w,x.expected.w)&&close(x.r.h,x.expected.h)&&close(x.svg.x,x.c.x)&&close(x.svg.y,x.c.y)&&close(x.svg.w,x.c.w)&&close(x.svg.h,x.c.h));
        const glyphCentered=samples.every(x=>Math.abs(x.glyphErr.cx)<=1&&Math.abs(x.glyphErr.cy)<=1&&x.g.x>=x.r.x-1&&x.g.y>=x.r.y-1&&x.g.x+x.g.w<=x.r.x+x.r.w+1&&x.g.y+x.g.h<=x.r.y+x.r.h+1);
        const first=samples.find(x=>x.label==='up-1'),down=samples.find(x=>x.label==='down-1'),nine=samples.find(x=>x.label==='up-9'),again=samples.find(x=>x.label==='up-again-9');
        const reversible=['x','y','w','h'].every(k=>close(first.c[k],down.c[k],1.5)&&close(nine.c[k],again.c[k],1.5));
        const anchorTolerance=.5+.2*zoom;
        const anchorStable=samples.every(x=>close(x.anchor.x,first.anchor.x,anchorTolerance)&&close(x.anchor.y,first.anchor.y,anchorTolerance));
        const unifiedLayer=samples.every(x=>x.unified&&x.css.position==='static');
        const noEditingTransition=samples.every(x=>x.css.nodeTransition==='0s');
        results.push({id:style.id,zoom,geometry,glyphCentered,reversible,anchorStable,unifiedLayer,noEditingTransition,samples});commitEdit(id);doDelete(id);
      }}
      return results;
    })()`);
    const ok=result.length>0&&result.every(x=>/* EN: Test an item for result.every: return x.geometry&&x.glyphCentered&&x.reversible&&x.anchorStable&&x.unifiedLayer&&x.noEditingTransition. ZH: 判断 result.every 的元素条件：返回 x.geometry&&x.glyphCentered&&x.reversible&&x.anchorStable&&x.unifiedLayer&&x.noEditingTransition。 */ x.geometry&&x.glyphCentered&&x.reversible&&x.anchorStable&&x.unifiedLayer&&x.noEditingTransition);
    clearTimeout(timer);finish(win,ok?0:1,ok?'':'TEXT_REGION_ROUNDTRIP_FAILED',result);
  }catch(e){clearTimeout(timer);finish(win,1,'TEXT_REGION_ROUNDTRIP_FAILED',e.stack||String(e));}
});
