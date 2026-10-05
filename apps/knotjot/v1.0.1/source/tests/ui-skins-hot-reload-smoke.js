const {app,BrowserWindow}=require('electron');
const path=require('path'),os=require('os'),fs=require('fs');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
const profile=path.join(os.tmpdir(),`knotjot-ui-hot-sync-${process.pid}-${Date.now()}`);
app.setPath('userData',profile);
const main=require('../main');

function fail(message,details){
/* EN: fail in module: call console.error, app.exit.
   ZH: module 中的 fail：调用 console.error、app.exit。 */
console.error('UI_SKINS_HOT_RELOAD_FAILED',message,details||'');app.exit(1);}
function library(revision,color){
/* EN: library in module: call JSON.stringify, new Date().toISOString.
   ZH: module 中的 library：调用 JSON.stringify、new Date().toISOString。 */
return JSON.stringify({format:'knotjot-ui-skins',version:1,revision,writerId:'smoke-editor',updatedUtc:new Date().toISOString(),current:'ui_hot_smoke',skins:{ui_hot_smoke:{name:'热同步测试',kind:'css',css:`#toolbar{background:${color}!important}.card{outline:3px solid ${color}!important}`,schemaVersion:1,contentHash:'hash-'+revision,writerId:'smoke-editor'}}},null,2);}

app.whenReady().then(async()=>{
/* EN: Continue module at app.whenReady().then: call setTimeout, path.join, win.webContents.setBackgroundThrottling.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 setTimeout、path.join、win.webContents.setBackgroundThrottling。 */

  const watchdog=setTimeout(()=>/* EN: Callback for setTimeout: call fail; return fail('测试超时'). ZH: setTimeout 的回调：调用 fail；返回 fail('测试超时')。 */ fail('测试超时'),35000);
  const win=new BrowserWindow({show:false,width:1280,height:820,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(__dirname,'..','preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  main.__test.attachWindowForSkinWatch(win);
  try{
    await win.loadFile(path.join(__dirname,'..','index.html'));
    const state=await win.webContents.executeJavaScript(`(()=>{const id=newTextbox('热同步保留状态');TB[id].x=2888;TB[id].y=1777;roots.push(id);update();return{id,count:Object.keys(TB).length,x:TB[id].x,y:TB[id].y};})()`);
    const file=path.join(profile,'ui-skins.json');
    await fs.promises.mkdir(profile,{recursive:true});
    await fs.promises.writeFile(file+'.incoming',library(1,'rgb(18, 52, 86)'),'utf8');
    await fs.promises.rename(file+'.incoming',file);
    const first=await win.webContents.executeJavaScript(`(async()=>{for(let i=0;i<60&&(document.documentElement.getAttribute('data-uiskin')!=='ui_hot_smoke'||getComputedStyle(document.getElementById('toolbar')).backgroundColor!=='rgb(18, 52, 86)');i++)await new Promise(r=>setTimeout(r,100));return{skin:document.documentElement.getAttribute('data-uiskin'),color:getComputedStyle(document.getElementById('toolbar')).backgroundColor};})()`);
    await fs.promises.writeFile(file+'.incoming',library(2,'rgb(101, 67, 33)'),'utf8');
    await fs.promises.rename(file+'.incoming',file);
    const second=await win.webContents.executeJavaScript(`(async()=>{for(let i=0;i<60&&getComputedStyle(document.getElementById('toolbar')).backgroundColor!=='rgb(101, 67, 33)';i++)await new Promise(r=>setTimeout(r,100));const t=TB[${JSON.stringify(state.id)}];return{skin:document.documentElement.getAttribute('data-uiskin'),color:getComputedStyle(document.getElementById('toolbar')).backgroundColor,stateKept:!!t&&t.text==='热同步保留状态'&&t.x===2888&&t.y===1777,count:Object.keys(TB).length};})()`);
    await new Promise(r=>/* EN: Callback for module: call setTimeout; return setTimeout(r,1300). ZH: module 的回调：调用 setTimeout；返回 setTimeout(r,1300)。 */ setTimeout(r,1300));
    const disk=JSON.parse(await fs.promises.readFile(file,'utf8'));
    const result={firstApplied:first.skin==='ui_hot_smoke'&&first.color==='rgb(18, 52, 86)',sameIdUpdated:second.color==='rgb(101, 67, 33)',stateKept:second.stateKept&&second.count===state.count,revisionNotRewritten:disk.revision===2&&disk.writerId==='smoke-editor',current:second.skin};
    const artifactDir=path.join(__dirname,'.artifacts');await fs.promises.mkdir(artifactDir,{recursive:true});await fs.promises.writeFile(path.join(artifactDir,'ui-hot-sync-result.json'),JSON.stringify({result,first,second,diskRevision:disk.revision},null,2),'utf8');
    console.log('UI_SKINS_HOT_RELOAD_RESULT',JSON.stringify(result));
    if(!result.firstApplied||!result.sameIdUpdated||!result.stateKept||!result.revisionNotRewritten)return fail('热同步检查失败',result);
    clearTimeout(watchdog);app.exit(0);
  }catch(err){fail(err.stack||String(err));}
});
