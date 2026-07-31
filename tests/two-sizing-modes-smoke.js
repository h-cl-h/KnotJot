const {app,BrowserWindow}=require('electron');
const path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-two-sizing-${process.pid}`));
require('../main');

function finish(win,code,message,details){
  if(message)console.error(message,details||'');
  else console.log('TWO_SIZING_MODES_OK',JSON.stringify(details));
  try{win.destroy();}catch(_){ }
  app.exit(code);
}

app.whenReady().then(async()=>{
  const root=path.join(__dirname,'..');
  const win=new BrowserWindow({show:false,width:1280,height:820,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(root,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  const timer=setTimeout(()=>finish(win,1,'TWO_SIZING_MODES_FAILED timeout'),30000);
  try{
    await win.loadFile(path.join(root,'index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const frame=()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
      const style=(mode,aspect=1.6)=>({id:'two-mode-'+mode+'-'+Math.random(),name:'two mode',replaceFrame:true,fontSize:18,
        layers:[{id:'bg',type:'rect',x:0,y:0,w:100,h:100,fill:'#dce8ff',stroke:'#5b8def',strokeWidth:1.5,isVisible:true}],
        textRegion:{x:18,y:16,w:64,h:68},textSizing:{mode,width:200,height:100,aspect},textRules:{type:'any',maxLength:0,required:false}});
      const make=(mode,text='',aspect=1.6)=>{const id=newTextbox(text);TB[id].x=2400;TB[id].y=1500;TB[id].textStyle=style(mode,aspect);roots.push(id);update();return{id,node:els[id],card:els[id]._card,inner:els[id]._inner};};
      const remove=x=>{if(editing===x.id)commitEdit(x.id);doDelete(x.id);};
      const migrations={};
      for(const mode of ['auto','uniform','fixed','stretch']){const x=make(mode);migrations[mode]={uniform:x.node.classList.contains('ts-size-uniform'),stretch:x.node.classList.contains('ts-size-stretch')};remove(x);}

      const uiNode=make('auto');selectTb(uiNode.id);TextStyleFeature.open('rules');await frame();
      const select=document.querySelector('[data-page="rules"] [data-z="mode"]'),options=[...select.options].map(x=>({value:x.value,label:x.textContent}));
      const maxWidthPresent=!!document.querySelector('[data-page="rules"] [data-z="maxWidth"]'),targetSizePresent=!!document.querySelector('[data-page="rules"] [data-z="width"], [data-page="rules"] [data-z="height"]');
      const aspectRow=document.querySelector('[data-page="rules"] [data-aspect-row]'),aspectVisibleBefore=!aspectRow.hidden;
      select.value='stretch';select.dispatchEvent(new Event('change',{bubbles:true}));const aspectHiddenForStretch=aspectRow.hidden;
      remove(uiNode);TextStyleFeature.close();

      const fixed=make('uniform','',1.6);await frame();
      const measure=x=>{const c=x.card.getBoundingClientRect(),i=x.inner,r=document.createRange();r.selectNodeContents(i);const content=r.getBoundingClientRect();return{w:c.width,h:c.height,ratio:c.width/c.height,overflowX:Math.max(0,i.scrollWidth-i.clientWidth),overflowY:Math.max(0,i.scrollHeight-i.clientHeight),contentDy:content.height?Math.abs((content.top+content.height/2)-(i.getBoundingClientRect().top+i.getBoundingClientRect().height/2)):0};};
      const fixedEmpty=measure(fixed);TB[fixed.id].text='字';render([fixed.id]);layout();await frame();const fixedOne=measure(fixed);TB[fixed.id].text='字字';render([fixed.id]);layout();await frame();const fixedTwo=measure(fixed);TB[fixed.id].text=('固定比例下的多行文字内容 '.repeat(35)).trim();render([fixed.id]);layout();await frame();const fixedLong=measure(fixed);TB[fixed.id].text='短文字';render([fixed.id]);layout();await frame();const fixedShort=measure(fixed);TB[fixed.id].text='';render([fixed.id]);layout();await frame();const fixedReset=measure(fixed);remove(fixed);

      const stretch=make('stretch','');await frame();const stretchEmpty=measure(stretch);TB[stretch.id].text='字';render([stretch.id]);layout();await frame();const stretchOne=measure(stretch);TB[stretch.id].text='字字';render([stretch.id]);layout();await frame();const stretchTwo=measure(stretch);TB[stretch.id].text=('free stretch 自由拉伸文字 '.repeat(42)).trim();render([stretch.id]);layout();await frame();const stretchLong=measure(stretch);TB[stretch.id].text='缩回';render([stretch.id]);layout();await frame();const stretchShort=measure(stretch);TB[stretch.id].text='第一行\\n第二行\\n第三行';render([stretch.id]);layout();await frame();const stretchMultiline=measure(stretch);remove(stretch);
      const imageNode=make('stretch','图片上的文字');TB[imageNode.id].textStyle=style('stretch');TB[imageNode.id].textStyle.layers=[{id:'photo',type:'image',x:5,y:8,w:90,h:84,imageData:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',isVisible:true}];render([imageNode.id]);layout();await frame();const imageSvg=imageNode.card.querySelector('.ts-decoration'),svgImage=imageSvg.querySelector('image'),imageRender={exists:!!svgImage,x:svgImage&&svgImage.getAttribute('x'),xPercent:svgImage&&(+svgImage.getAttribute('x'))/imageSvg.viewBox.baseVal.width*100,href:svgImage&&svgImage.getAttribute('href')};remove(imageNode);
      return{migrations,options,maxWidthPresent,targetSizePresent,aspectVisibleBefore,aspectHiddenForStretch,fixedEmpty,fixedOne,fixedTwo,fixedLong,fixedShort,fixedReset,stretchEmpty,stretchOne,stretchTwo,stretchLong,stretchShort,stretchMultiline,imageRender};
    })()`);
    const optionsOk=result.options.length===2&&result.options[0].value==='uniform'&&result.options[1].value==='stretch'&&result.options[0].label==='固定长宽比'&&result.options[1].label==='自由拉伸';
    const migrationOk=['auto','uniform','fixed'].every(x=>result.migrations[x].uniform&&!result.migrations[x].stretch)&&result.migrations.stretch.stretch&&!result.migrations.stretch.uniform;
    const fixedRatioOk=[result.fixedEmpty,result.fixedLong,result.fixedShort,result.fixedReset].every(x=>Math.abs(x.ratio-1.6)<.025);
    const contained=[result.fixedLong,result.fixedShort,result.stretchLong,result.stretchShort,result.stretchMultiline].every(x=>x.overflowX<=2&&x.overflowY<=2);
    const resetOk=Math.abs(result.fixedReset.w-result.fixedEmpty.w)<1.5&&Math.abs(result.fixedReset.h-result.fixedEmpty.h)<1.5;
    const shrinkOk=result.fixedShort.w<result.fixedLong.w-2&&result.stretchShort.w<result.stretchLong.w-2&&result.stretchShort.h<result.stretchLong.h-2;
    const stretchGrew=result.stretchLong.w>result.stretchEmpty.w+2&&result.stretchLong.h>result.stretchEmpty.h+2;
    const verticalCenterOk=result.fixedLong.contentDy<4&&result.stretchMultiline.contentDy<4;
    const baselineOk=result.fixedEmpty.w>20&&result.fixedEmpty.w<200&&result.fixedEmpty.h>20&&result.fixedEmpty.h<200&&result.stretchEmpty.w>20&&result.stretchEmpty.w<200&&result.stretchEmpty.h>20&&result.stretchEmpty.h<200;
    const oneCharacterMinimum=Math.abs(result.fixedEmpty.w-result.fixedOne.w)<1.5&&Math.abs(result.fixedEmpty.h-result.fixedOne.h)<1.5&&Math.abs(result.stretchEmpty.w-result.stretchOne.w)<1.5&&Math.abs(result.stretchEmpty.h-result.stretchOne.h)<1.5;
    const waitsForOverflow=Math.abs(result.fixedTwo.w-result.fixedOne.w)<1.5&&Math.abs(result.fixedTwo.h-result.fixedOne.h)<1.5&&Math.abs(result.stretchTwo.w-result.stretchOne.w)<1.5&&Math.abs(result.stretchTwo.h-result.stretchOne.h)<1.5;
    const imageOk=result.imageRender.exists&&Math.abs(result.imageRender.xPercent-5)<.01&&String(result.imageRender.href||'').startsWith('data:image/png;base64,');
    const ok=optionsOk&&!result.maxWidthPresent&&!result.targetSizePresent&&result.aspectVisibleBefore&&result.aspectHiddenForStretch&&migrationOk&&fixedRatioOk&&baselineOk&&oneCharacterMinimum&&waitsForOverflow&&contained&&resetOk&&shrinkOk&&stretchGrew&&verticalCenterOk&&imageOk;
    clearTimeout(timer);
    finish(win,ok?0:1,ok?'':'TWO_SIZING_MODES_FAILED',result);
  }catch(e){clearTimeout(timer);finish(win,1,'TWO_SIZING_MODES_FAILED',e.stack||String(e));}
});
