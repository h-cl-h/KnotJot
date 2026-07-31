const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-wpf-parity-${process.pid}`));

// Use a deterministic export fixture so source tests never depend on a user's
// private style library or on a deleted real-time-preview build.
const fixtureFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
if(!fs.existsSync(fixtureFile)){
  console.error('WPF_REAL_LIBRARY_PARITY_FAILED missing fixture',fixtureFile);
  process.exit(1);
}
const library=JSON.parse(fs.readFileSync(fixtureFile,'utf8'));
const target=library.styles.find(x=>x.id==='custom-style')||library.styles.find(x=>x.replaceFrame&&x.textRegion&&(x.layers||[]).some(l=>l.type==='rect'&&Number(l.radius)>0));
if(!target){
  console.error('WPF_REAL_LIBRARY_PARITY_FAILED no suitable real style');
  process.exit(1);
}
const fixtureStylesDir=path.join(os.tmpdir(),`knotjot-style-fixture-${process.pid}`);
fs.mkdirSync(fixtureStylesDir,{recursive:true});
fs.writeFileSync(path.join(fixtureStylesDir,'custom-text-styles.json'),JSON.stringify(library),'utf8');
process.env.KNOTJOT_TEXT_STYLES_DIR=fixtureStylesDir;
require('../main');

const near=(a,b,t=1.5)=>Math.abs(a-b)<=t;
function finish(win,code,message,details){
  if(message)console.error('WPF_REAL_LIBRARY_PARITY_FAILED',message,JSON.stringify(details||{}));
  else console.log('WPF_REAL_LIBRARY_PARITY_OK',JSON.stringify(details));
  try{win.destroy();}catch(_){ }
  app.exit(code);
}

app.whenReady().then(async()=>{
  const root=path.join(__dirname,'..');
  const win=new BrowserWindow({show:false,width:1400,height:1000,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(root,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  const timer=setTimeout(()=>finish(win,1,'timeout'),35000);
  try{
    await win.loadFile(path.join(root,'index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const expected=${JSON.stringify(target)};
      const frame=()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
      await TextStyleFeature.loadExternal();hideStart();await frame();
      scale=1;panX=0;panY=0;applyTransform();

      // Create an ordinary textbox and apply the disk-loaded style only by ID.
      // This exercises the same path used after editor synchronization.
      const id=newTextbox('');TB[id].x=100;TB[id].y=390;TB[id].textStyleId=expected.id;delete TB[id].textStyle;roots.push(id);update();await frame();
      const text='可是来得及发过来看数据的方法论开工建设就离开家离开家窗开记录就立即离开家离开家离开家就立刻交流空间离开家离开家了就立即开始工作 ABCD 1234';
      TB[id].text=text;render([id]);layout();await frame();
      const node=els[id],card=node._card,inner=node._inner,svg=card.querySelector('.ts-decoration'),layer=expected.layers.find(x=>x.isVisible!==false&&x.type==='rect'),visibleRect=svg.querySelector('g > rect');
      const cr=card.getBoundingClientRect(),sr=svg.getBoundingClientRect(),ir=inner.getBoundingClientRect(),lr=visibleRect.getBoundingClientRect(),tr=expected.textRegion;
      const range=document.createRange();range.selectNodeContents(inner);const rr=range.getBoundingClientRect();
      const viewBox=svg.getAttribute('viewBox').trim().split(/\\s+/).map(Number);
      const rx=Number(visibleRect.getAttribute('rx')||0),ry=Number(visibleRect.getAttribute('ry')||0);
      const ordinary={
        usesIdOnly:TB[id].textStyleId===expected.id&&!TB[id].textStyle,
        card:{x:cr.x,y:cr.y,w:cr.width,h:cr.height,ratio:cr.width/cr.height},
        svg:{x:sr.x,y:sr.y,w:sr.width,h:sr.height,viewBox},
        inner:{x:ir.x,y:ir.y,w:ir.width,h:ir.height,contentDx:(rr.left+rr.width/2)-(ir.left+ir.width/2),contentDy:(rr.top+rr.height/2)-(ir.top+ir.height/2),overflowX:Math.max(0,inner.scrollWidth-inner.clientWidth),overflowY:Math.max(0,inner.scrollHeight-inner.clientHeight)},
        unified:inner.closest('svg')===svg&&inner.parentElement&&inner.parentElement.tagName.toLowerCase()==='foreignobject',
        layer:{x:lr.x,y:lr.y,w:lr.width,h:lr.height,ratio:lr.width/lr.height,radiusPxX:rx*sr.width/viewBox[2],radiusPxY:ry*sr.height/viewBox[3],strokeWidth:Number(visibleRect.getAttribute('stroke-width')||0),vectorEffect:visibleRect.getAttribute('vector-effect')},
        oracle:{innerX:cr.x+Number(tr.x)*cr.width/100,innerY:cr.y+Number(tr.y)*cr.height/100,innerW:Number(tr.w)*cr.width/100,innerH:Number(tr.h)*cr.height/100,layerX:cr.x+Number(layer.x)*cr.width/100,layerY:cr.y+Number(layer.y)*cr.height/100,layerW:Number(layer.w)*cr.width/100,layerH:Number(layer.h)*cr.height/100,radius:Number(layer.radius)||0}
      };

      // A structural-node textbox must place the user-drawn text-region centre
      // on the structural node, not the centre of the full 0..100 design plane.
      const parent=newTextbox('parent'),child=newTextbox('child');TB[parent].x=850;TB[parent].y=360;TB[parent].struct='timeline';roots.push(parent);const bid=ensureBrace(parent);TB[child].parentBrace=bid;BR[bid].children.push(child);update();await frame();
      const ref={braceId:bid,childId:child},anchorBefore=structuralNodePoint(ref),attachedId=createStructuralNodeTextbox(ref,expected.id,false);render([attachedId]);layout();await frame();
      const anchor=structuralNodePoint(ref),attachedInner=els[attachedId]._inner.getBoundingClientRect(),canvasRect=canvas.getBoundingClientRect(),clientCenter={x:attachedInner.left+attachedInner.width/2,y:attachedInner.top+attachedInner.height/2},innerWorld={x:(clientCenter.x-canvasRect.left-panX)/scale,y:(clientCenter.y-canvasRect.top-panY)/scale};
      const attached={anchor,clientCenter,innerWorld,dx:innerWorld.x-anchor.x,dy:innerWorld.y-anchor.y,anchorStable:Math.hypot(anchor.x-anchorBefore.x,anchor.y-anchorBefore.y)<.01};
      return{styleId:expected.id,textLength:Array.from(text).length,ordinary,attached,capture:{x:Math.max(0,Math.floor(cr.x-8)),y:Math.max(0,Math.floor(cr.y-8)),width:Math.ceil(cr.width+16),height:Math.ceil(cr.height+16)}};
    })()`);

    const o=result.ordinary,a=o.oracle;
    const fullPlane=o.svg.viewBox.length===4&&near(o.svg.viewBox[0],0,.001)&&near(o.svg.viewBox[1],0,.001)&&near(o.svg.viewBox[2],o.card.w,1)&&near(o.svg.viewBox[3],o.card.h,1);
    const svgFillsCard=near(o.svg.x,o.card.x,.75)&&near(o.svg.y,o.card.y,.75)&&near(o.svg.w,o.card.w,.75)&&near(o.svg.h,o.card.h,.75);
    const directRegion=near(o.inner.x,a.innerX,1.5)&&near(o.inner.y,a.innerY,1.5)&&near(o.inner.w,a.innerW,1.5)&&near(o.inner.h,a.innerH,1.5);
    const directLayer=near(o.layer.x,a.layerX,1.5)&&near(o.layer.y,a.layerY,1.5)&&near(o.layer.w,a.layerW,1.5)&&near(o.layer.h,a.layerH,1.5);
    const pixelRadius=near(o.layer.radiusPxX,a.radius,.8)&&near(o.layer.radiusPxY,a.radius,.8);
    const pixelStroke=near(o.layer.strokeWidth,Number(target.layers.find(x=>x.isVisible!==false&&x.type==='rect').strokeWidth)||0,.01)&&o.layer.vectorEffect==='non-scaling-stroke';
    const contentCentered=Math.abs(o.inner.contentDx)<3&&Math.abs(o.inner.contentDy)<3&&o.inner.overflowX<=2&&o.inner.overflowY<=2;
    const fixedAspect=near(o.card.ratio,Number(target.textSizing&&target.textSizing.aspect)||1.8,.03);
    const structuralAnchor=Math.abs(result.attached.dx)<=1.5&&Math.abs(result.attached.dy)<=1.5&&result.attached.anchorStable;
    const ok=o.usesIdOnly&&o.unified&&fullPlane&&svgFillsCard&&directRegion&&directLayer&&pixelRadius&&pixelStroke&&contentCentered&&fixedAspect&&structuralAnchor;
    if(ok){
      const png=await win.webContents.capturePage();
      const screenshot=path.join(os.tmpdir(),'knotjot-wpf-real-library-parity.png');
      fs.writeFileSync(screenshot,png.toPNG());result.screenshot=screenshot;
    }
    clearTimeout(timer);finish(win,ok?0:1,ok?'':'WPF coordinate/runtime mismatch',result);
  }catch(e){clearTimeout(timer);finish(win,1,e.stack||String(e));}
});
