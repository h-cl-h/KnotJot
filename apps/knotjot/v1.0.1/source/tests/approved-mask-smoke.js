// EN: Compare actual renderer SVG mask pixels against sRGB alpha/luminance semantics.
// ZH: 比较真实渲染器 SVG 蒙版像素与 sRGB 透明度及亮度语义。
const {app,BrowserWindow}=require('electron'),fs=require('fs'),path=require('path');
const source=process.env.KNOTJOT_TEST_SOURCE||path.join(__dirname,'..'),out=path.resolve(__dirname,'../../preview/testing/approved-followup/masks-'+(process.env.KNOTJOT_TEST_LABEL||'final'));fs.mkdirSync(out,{recursive:true});app.setPath('userData',path.join(out,'profile-'+Date.now()));app.commandLine.appendSwitch('disable-gpu');
app.whenReady().then(async()=>{
  // EN: Rasterize authored geometry and transparent image cutters in an isolated browser process.
  // ZH: 在隔离浏览器进程中栅格化设计几何及透明图片蒙版。
  const timer=setTimeout(()=>app.exit(2),45000);
  try{const win=new BrowserWindow({show:false,width:900,height:650,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false}});await win.loadFile(path.join(source,'index.html'));
    const cases=[['alpha-half','alpha','#ffffff',.5,128],['inverse-alpha-half','inverseAlpha','#000000',.5,128],['inverse-black','inverseLuminance','#000000',1,255],['inverse-white','inverseLuminance','#ffffff',1,0],['black','luminance','#000000',1,0],['gray','luminance','#808080',1,128],['red-half','luminance','#ff0000',.5,27],['inverse-red-half','inverseLuminance','#ff0000',.5,228],['image-alpha','alpha','#ffffff',.5,64],['image-inverse','inverseAlpha','#ffffff',.5,191],['image-lum','luminance','#ffffff',.5,64],['rotated','alpha','#ffffff',.5,128]];
    const results=[];for(const sample of cases){const result=await win.webContents.executeJavaScript(`(${render.toString()})(${JSON.stringify(sample)})`);fs.writeFileSync(path.join(out,sample[0]+'.svg'),result.svg);fs.writeFileSync(path.join(out,sample[0]+'.png'),Buffer.from(result.png.split(',')[1],'base64'));delete result.svg;delete result.png;result.pass=Math.abs(result.pixel[3]-sample[4])<=2;results.push(result);}
    fs.writeFileSync(path.join(out,'result.json'),JSON.stringify(results,null,2));console.log(JSON.stringify(results,null,2));clearTimeout(timer);app.exit(results.every(r=>r.pass)?0:1);
  }catch(e){console.error(e);clearTimeout(timer);app.exit(2);}
});
async function render(sample){
  // EN: Sample the center of the actual generated SVG after its images decode.
  // ZH: 图片解码后采样实际生成 SVG 的中心。
  hideStart();const id=newTextbox('');TB[id].x=350;TB[id].y=300;const cutter={id:'mask',type:'rect',x:0,y:0,w:100,h:100,fill:sample[2],strokeWidth:0,opacity:sample[3],maskMode:sample[1]};
  if(sample[0].startsWith('image')){const cv=document.createElement('canvas');cv.width=10;cv.height=10;const ctx=cv.getContext('2d');ctx.fillStyle='rgba(255,255,255,0.5)';ctx.fillRect(0,0,10,10);cutter.type='image';cutter.imageData=cv.toDataURL('image/png');}
  if(sample[0]==='rotated'){cutter.rotation=25;cutter.x=10;cutter.y=10;cutter.w=80;cutter.h=80;}
  TB[id].textStyle={id:'mask-audit',replaceFrame:false,layers:[{id:'target',type:'rect',x:0,y:0,w:100,h:100,fill:'#ff0000',strokeWidth:0,opacity:1},cutter]};roots.push(id);update();window.applyTextStyleToNode(id);await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
  const svg=els[id]._card.querySelector('.ts-decoration').cloneNode(true);svg.setAttribute('xmlns','http://www.w3.org/2000/svg');svg.setAttribute('width','200');svg.setAttribute('height','120');const markup=new XMLSerializer().serializeToString(svg),image=new Image(),url=URL.createObjectURL(new Blob([markup],{type:'image/svg+xml'}));
  try{image.src=url;await image.decode();const cv=document.createElement('canvas');cv.width=200;cv.height=120;const ctx=cv.getContext('2d');ctx.drawImage(image,0,0,200,120);return {name:sample[0],expected:sample[4],pixel:Array.from(ctx.getImageData(100,60,1,1).data),svg:markup,png:cv.toDataURL('image/png')};}finally{URL.revokeObjectURL(url);}
}
