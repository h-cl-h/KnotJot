/* EN: Verify the real export command keeps the active sheet's unlimited background color without writing user exports.
   ZH: 验证真实导出命令保留当前无限画布背景色，不写入用户导出文件。 */
'use strict';
const {app,BrowserWindow}=require('electron'),fs=require('fs'),path=require('path');
const out=path.resolve(__dirname,'../../preview/testing/background/export');fs.mkdirSync(out,{recursive:true});
app.setPath('userData',path.join(out,'profile-'+process.pid));process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');require('../main');
app.whenReady().then(async()=>{
  // EN: Retain baseline/fixed observations and bound the hidden renderer lifetime.
  // ZH: 保留修复前后观察结果，并限制隐藏渲染器运行时长。
  const timer=setTimeout(()=>app.exit(1),45000),win=new BrowserWindow({show:false,webPreferences:{contextIsolation:true,nodeIntegration:false,preload:path.join(__dirname,'../preload.js')}});
  try{
    await win.loadFile(path.join(__dirname,'../index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      await new Promise(r=>setTimeout(r,250));dirty=false;await doNew('brace');hideStart();
      const id=newTextbox('Export background');roots.push(id);update();
      const check={},metrics={};
      // EN: Inspect the SVG passed by exportAs to its rasterizer; stub only output encoding and file persistence.
      // ZH: 检查 exportAs 传给栅格器的 SVG，仅替代输出编码和文件保存。
      let captured='';rasterize=async(svg)=>{captured=svg;return 'data:image/jpeg;base64,AA==';};saveExport=async()=>{};
      const bg={enabled:true,mode:'unlimited',color:'#d2e3f4'};
      AppearanceEditor.loadDocumentAppearance({background:bg});await exportAs('jpg',true);
      const color=svg=>new DOMParser().parseFromString(svg,'image/svg+xml').querySelector('foreignObject>div').style.backgroundColor;
      metrics.canvas=getComputedStyle(canvas).backgroundColor;metrics.current=color(captured);
      check.currentStyleColor=metrics.current==='rgb(210, 227, 244)';
      const png=document.createElement('canvas');png.width=2;png.height=2;
      AppearanceEditor.loadDocumentAppearance({background:{...bg,imageData:png.toDataURL(),imageWidth:2,imageHeight:2,imageScale:100}});await exportAs('jpg',true);
      check.imageAndColor=color(captured)==='rgb(210, 227, 244)'&&captured.includes('background-image:url(');
      AppearanceEditor.loadDocumentAppearance({background:bg});await exportAs('jpg',false);
      check.originalStyleFallback=color(captured)==='rgb(245, 246, 248)';
      AppearanceEditor.loadDocumentAppearance({background:{...bg,enabled:false}});await exportAs('jpg',true);
      check.disabledFallback=color(captured)===getComputedStyle(document.documentElement).getPropertyValue('--bg').trim()||color(captured)==='rgb(245, 246, 248)';
      return {check,metrics};
    })()`);
    fs.writeFileSync(path.join(out,(process.env.BASELINE?'baseline':'fixed')+'-result.json'),JSON.stringify(result,null,2));
    console.log('BACKGROUND_EXPORT_RESULT',JSON.stringify(result));clearTimeout(timer);app.exit(Object.values(result.check).every(Boolean)?0:1);
  }catch(e){clearTimeout(timer);console.error(e.stack||e);app.exit(1);}
});
