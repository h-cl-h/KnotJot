const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');
process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-centering-${process.pid}`));
require('../main');
const fixtureFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
const styles=(JSON.parse(fs.readFileSync(fixtureFile,'utf8')).styles||[]).map(style=>(/* EN: Derive the next value for (JSON.parse(fs.readFileSync(fixtureFile,'utf8')).styles||[]).map: return {file:'tests/fixtures/text-style-regression-library.json',style}. ZH: 计算 (JSON.parse(fs.readFileSync(fixtureFile,'utf8')).styles||[]).map 的下一项结果：返回 {file:'tests/fixtures/text-style-regression-library.json',style}。 */ {file:'tests/fixtures/text-style-regression-library.json',style}));
const packagedRoot=process.env.KNOTJOT_PACKAGED_ROOT||path.join(__dirname,'..','dist','win-unpacked','resources','app.asar');
const appRoot=process.env.TEST_PACKAGED==='1'?packagedRoot:path.join(__dirname,'..');
function fail(message,details){
/* EN: fail in module: call console.error, app.exit.
   ZH: module 中的 fail：调用 console.error、app.exit。 */
console.error('USER_STYLE_CENTERING_FAILED',message,details||'');app.exit(1);}
app.whenReady().then(async()=>{
/* EN: Continue module at app.whenReady().then: call setTimeout, path.join, win.loadFile.
   ZH: 在 app.whenReady().then 阶段继续 module 流程：调用 setTimeout、path.join、win.loadFile。 */

  const timer=setTimeout(()=>/* EN: Callback for setTimeout: call fail; return fail('测试超时'). ZH: setTimeout 的回调：调用 fail；返回 fail('测试超时')。 */ fail('测试超时'),25000);
  const win=new BrowserWindow({show:false,width:1280,height:820,webPreferences:{contextIsolation:true,nodeIntegration:false,preload:path.join(appRoot,'preload.js')}});
  try{
    await win.loadFile(path.join(appRoot,'index.html'));
    const result=await win.webContents.executeJavaScript(`(()=>{
      const inputs=${JSON.stringify(styles)},modes=['auto','uniform','stretch'],samples=['','字','ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789','中文 English 123 混合内容，需要自动换行并保持在用户画出的文字区域以内。',('极长文本English123').repeat(200)],out=[];
      for(const input of inputs)for(const mode of modes){
        const id=newTextbox(''),style=JSON.parse(JSON.stringify(input.style));style.textSizing=Object.assign({width:220,height:96,maxWidth:360,aspect:1.8},style.textSizing||{},{mode});TB[id].x=3000;TB[id].y=1800;TB[id].textStyle=style;roots.push(id);update();const measures=[];
        for(const text of samples){TB[id].text=text;render([id]);layout();const card=els[id]._card.getBoundingClientRect(),inner=els[id]._inner.getBoundingClientRect(),tr=style.textRegion,range=document.createRange();range.selectNodeContents(els[id]._inner);const content=range.getBoundingClientRect(),expected={x:card.left+(+tr.x||0)*card.width/100,y:card.top+(+tr.y||0)*card.height/100,w:(+tr.w||0)*card.width/100,h:(+tr.h||0)*card.height/100};measures.push({length:text.length,regionDx:inner.left-expected.x,regionDy:inner.top-expected.y,regionDw:inner.width-expected.w,regionDh:inner.height-expected.h,contentDx:text.length?(content.left+content.width/2)-(inner.left+inner.width/2):0,contentDy:text.length?(content.top+content.height/2)-(inner.top+inner.height/2):0,ratio:card.width/card.height,w:card.width,h:card.height,overflowX:Math.max(0,els[id]._inner.scrollWidth-els[id]._inner.clientWidth),overflowY:Math.max(0,els[id]._inner.scrollHeight-els[id]._inner.clientHeight)});}
        const nonEmpty=measures.slice(1);out.push({file:input.file,id:style.id,mode,measures,regionMatched:measures.every(x=>Math.abs(x.regionDx)<1.5&&Math.abs(x.regionDy)<1.5&&Math.abs(x.regionDw)<1.5&&Math.abs(x.regionDh)<1.5),contentCentered:nonEmpty.every(x=>Math.abs(x.contentDx)<3&&Math.abs(x.contentDy)<3),ratioStable:mode==='stretch'||measures.every(x=>Math.abs(x.ratio-measures[0].ratio)<.02),textContained:nonEmpty.every(x=>x.overflowX<=2&&x.overflowY<=2),emptyStable:measures[0].w>0&&measures[0].h>0,growsForHuge:measures.at(-1).w>measures[0].w||measures.at(-1).h>measures[0].h});doDelete(id);
      }return out;
    })()`);
    const failures=result.filter(x=>/* EN: Test an item for result.filter: return !x.regionMatched||!x.contentCentered||!x.ratioStable||!x.textContained||!x.emptyStable||!x.growsForHuge. ZH: 判断 result.filter 的元素条件：返回 !x.regionMatched||!x.contentCentered||!x.ratioStable||!x.textContained||!x.emptyStable||!x.growsForHuge。 */ !x.regionMatched||!x.contentCentered||!x.ratioStable||!x.textContained||!x.emptyStable||!x.growsForHuge).map(x=>(/* EN: Callback for result.filter(x=>!x.regionMatched||!x.contentCentered||!x.ratioStable||!x.textContained||!x.emptyStable||!x.gr: call x.measures.map. ZH: result.filter(x=>!x.regionMatched||!x.contentCentered||!x.ratioStable||!x.textContained||!x.emptyStable||!x.gr 的回调：调用 x.measures.map。 */ {file:x.file,id:x.id,mode:x.mode,checks:{regionMatched:x.regionMatched,contentCentered:x.contentCentered,ratioStable:x.ratioStable,textContained:x.textContained,emptyStable:x.emptyStable,growsForHuge:x.growsForHuge},maxOverflowX:Math.max(...x.measures.map(m=>/* EN: Derive the next value for x.measures.map: return m.overflowX. ZH: 计算 x.measures.map 的下一项结果：返回 m.overflowX。 */ m.overflowX)),maxOverflowY:Math.max(...x.measures.map(m=>/* EN: Derive the next value for x.measures.map: return m.overflowY. ZH: 计算 x.measures.map 的下一项结果：返回 m.overflowY。 */ m.overflowY)),measures:x.measures.map(m=>(/* EN: Derive the next value for x.measures.map: return {length:m.length,w:m.w,h:m.h,overflowX:m.overflowX,overflowY:m.overflowY}. ZH: 计算 x.measures.map 的下一项结果：返回 {length:m.length,w:m.w,h:m.h,overflowX:m.overflowX,overflowY:m.overflowY}。 */ {length:m.length,w:m.w,h:m.h,overflowX:m.overflowX,overflowY:m.overflowY}))}));
    console.log('USER_STYLE_CENTERING_RESULT',JSON.stringify({count:result.length,failures}));
    if(!result.length||failures.length)return fail('真实样式文字区域、居中或比例检查失败',failures);
    clearTimeout(timer);app.exit(0);
  }catch(e){fail(e.stack||String(e));}
});
