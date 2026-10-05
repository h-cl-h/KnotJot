// EN: Exercise actual textbox CSS and document round trips; export the same bounded renderer checks for packaged acceptance.
// ZH: 验证真实文本框CSS及文档往返，并导出相同的有界渲染检查供打包成品验收使用。
async function renderCases(){
  hideStart();await doNew('brace','brace');hideStart();
  const cases=[{id:'zero',width:0,expected:'0px'},{id:'omitted',expected:'1.5px'},{id:'null',width:null,expected:'1.5px'},{id:'false-legacy',width:false,expected:'1.5px'},{id:'empty-legacy',width:'',expected:'1.5px'},{id:'fractional',width:.75,expected:'0.75px'},{id:'positive',width:3,expected:'3px'},{id:'string-zero',width:'0',expected:'0px'},{id:'zero-inset',width:0,expected:'0px',shadow:'inset 4px 3px 2px 1px #f00'},{id:'replace-frame',width:3,expected:'0px',replace:true}];
  const results=[],zeroIds=[];
  for(const sample of cases){
    const id=newTextbox(sample.id),style={id:'audit-'+sample.id,bg:'#ffffff',border:'#ff0000',shadow:sample.shadow||'none'};
    if(Object.prototype.hasOwnProperty.call(sample,'width'))style.borderWidth=sample.width;
    if(sample.replace){style.replaceFrame=true;style.layers=[{id:'art',type:'rect',x:0,y:0,w:100,h:100,fill:'#ffffff',strokeWidth:0}];}
    TB[id].textStyle=style;TB[id].x=250;TB[id].y=200;roots.push(id);update();applyTextStyleToNode(id);
    await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
    const element=els[id],card=element._card,css=getComputedStyle(card),variable=element.style.getPropertyValue('--ts-border-width');
    results.push({id:sample.id,expected:sample.expected,variable,computedWidth:css.borderTopWidth,computedShadow:css.boxShadow,pass:variable===sample.expected&&(sample.expected==='0px'?parseFloat(css.borderTopWidth)===0:parseFloat(css.borderTopWidth)>0)&&(!sample.shadow||css.boxShadow.includes('inset'))});
    if(sample.width===0)zeroIds.push(id);
  }
  const documentText=serialize(),opened=await loadDataAsync(documentText,'zero-border-roundtrip');
  await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
  results.push({id:'save-reopen-zero',pass:!!opened&&zeroIds.every(id=>TB[id]?.textStyle?.borderWidth===0&&getComputedStyle(els[id]._card).borderTopWidth==='0px')});
  return {results,documentText,ok:results.every(r=>r.pass)};
}
module.exports={renderCases};
if(process.versions.electron){
  const {app,BrowserWindow}=require('electron'),fs=require('node:fs'),path=require('node:path');
  const source=process.env.KNOTJOT_TEST_SOURCE||path.join(__dirname,'..'),out=path.resolve(process.env.KNOTJOT_TEST_OUTPUT||path.join(__dirname,'../../preview/testing/completion-fixes/m06/final'));
  fs.mkdirSync(out,{recursive:true});app.setPath('userData',path.join(out,'profile'));app.commandLine.appendSwitch('disable-gpu');
  // EN: Load an isolated real renderer, retain the fixture/result/screenshot, and enforce a bounded test lifetime without AI or installed-profile access.
  // ZH: 加载隔离真实渲染器，保存样本、结果及截图，并限制测试生命周期；不访问AI或安装配置。
  app.whenReady().then(async()=>{const timer=setTimeout(()=>app.exit(2),45000);try{const win=new BrowserWindow({show:false,width:1100,height:800,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false}});await win.loadFile(path.join(source,'index.html'));const result=await win.webContents.executeJavaScript(`(${renderCases.toString()})()`);fs.writeFileSync(path.join(out,'zero-border-roundtrip.knotjot'),result.documentText);delete result.documentText;fs.writeFileSync(path.join(out,'result.json'),JSON.stringify(result,null,2));fs.writeFileSync(path.join(out,'renderer.png'),(await win.webContents.capturePage()).toPNG());console.log(JSON.stringify(result));clearTimeout(timer);app.exit(result.ok?0:1);}catch(error){fs.writeFileSync(path.join(out,'error.txt'),error.stack);console.error(error);clearTimeout(timer);app.exit(2);}});
}
