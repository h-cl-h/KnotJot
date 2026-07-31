const {app,BrowserWindow}=require('electron');
const fs=require('fs'),path=require('path'),os=require('os');

process.env.KNOTJOT_INTEGRATION_TEST='1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData',path.join(os.tmpdir(),`knotjot-live-input-${process.pid}`));
require('../main');

const packagedRoot=process.env.KNOTJOT_PACKAGED_ROOT||path.join(__dirname,'..','dist','win-unpacked','resources','app.asar');
const appRoot=process.env.TEST_PACKAGED==='1'?packagedRoot:path.join(__dirname,'..');
const fixtureFile=path.join(__dirname,'fixtures','text-style-regression-library.json');
const userStyles=JSON.parse(fs.readFileSync(fixtureFile,'utf8')).styles||[];

function fail(message,details){console.error('LIVE_INPUT_RULES_FAILED',message,details||'');app.exit(1);}

app.whenReady().then(async()=>{
  const timer=setTimeout(()=>fail('测试超时'),45000);
  const win=new BrowserWindow({show:false,width:1280,height:820,webPreferences:{contextIsolation:true,nodeIntegration:false,backgroundThrottling:false,preload:path.join(appRoot,'preload.js')}});
  win.webContents.setBackgroundThrottling(false);
  try{
    await win.loadFile(path.join(appRoot,'index.html'));
    const result=await win.webContents.executeJavaScript(`(async()=>{
      const external=${JSON.stringify(userStyles)};
      const base=external.find(x=>x&&x.replaceFrame&&x.textRegion&&x.textRegion.w>0&&x.textRegion.h>0)||{
        id:'live-sizing',name:'Live sizing',replaceFrame:true,fontSize:18,
        layers:[{id:'bg',type:'rect',x:5,y:5,w:90,h:90,fill:'#DDE9FF',stroke:'#5B8DEF',strokeWidth:1.5,isVisible:true}],
        textRegion:{x:22,y:22,w:56,h:56},textSizing:{mode:'uniform',width:180,height:100,aspect:1.8},textRules:{type:'any',maxLength:0,required:false}
      };
      const clone=x=>JSON.parse(JSON.stringify(x));
      const flush=x=>{window.applyTextStyleToNode(x.id);layout();void x.card.offsetWidth;};
      const make=(rules,mode='uniform')=>{
        const style=clone(base);style.id='test-'+Math.random();style.textRules=Object.assign({type:'any',maxLength:0,pattern:'',required:false},rules||{});style.textSizing=Object.assign({width:180,height:100,maxWidth:360,aspect:1.8},style.textSizing||{},{mode});
        const id=newTextbox('');TB[id].x=2600;TB[id].y=1600;TB[id].textStyle=style;delete TB[id].textRules;delete TB[id].textSizing;roots.push(id);update();startEdit(id);return{id,style,inner:els[id]._inner,card:els[id]._card};
      };
      const caretEnd=inner=>{const r=document.createRange();r.selectNodeContents(inner);r.collapse(false);const s=getSelection();s.removeAllRanges();s.addRange(r);};
      const insert=(inner,text,inputType='insertText')=>{caretEnd(inner);const before=new InputEvent('beforeinput',{bubbles:true,cancelable:true,inputType,data:text});const accepted=inner.dispatchEvent(before);if(accepted){inner.append(document.createTextNode(text));caretEnd(inner);inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType,data:text}));}return accepted;};
      const paste=(inner,text)=>{caretEnd(inner);const ev=new Event('paste',{bubbles:true,cancelable:true});Object.defineProperty(ev,'clipboardData',{value:{getData:type=>type==='text/plain'?text:''}});return inner.dispatchEvent(ev);};
      const cleanup=x=>{if(editing===x.id)commitEdit(x.id);doDelete(x.id);};

      const live=[];
      for(const mode of ['uniform','stretch']){
        const x=make({type:'any'},mode),samples=[];flush(x);
        const measure=length=>{const c=x.card.getBoundingClientRect(),i=x.inner,r=i.getBoundingClientRect();return{length,w:c.width,h:c.height,regionW:r.width,regionH:r.height,overflowX:Math.max(0,i.scrollWidth-i.clientWidth),overflowY:Math.max(0,i.scrollHeight-i.clientHeight)};};
        samples.push(measure(0));
        const checkpoints=new Set([1,2,8,20,40,70,110,150,180]);
        for(let i=1;i<=180;i++){
          if(!insert(x.inner,i%11===0?'中':'a'))throw new Error('任意文字被错误拦截');
          flush(x);
          if(checkpoints.has(i))samples.push(measure(i));
        }
        samples.push(measure(180));
        x.inner.textContent='';x.inner.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'deleteContentBackward',data:null}));flush(x);const emptyAgain=measure(0);
        const rounded=samples.map(v=>Math.round(v.w*10)+'x'+Math.round(v.h*10));
        const growthSteps=new Set(rounded).size;
        const monotonic=samples.every((v,i)=>!i||(v.w+0.6>=samples[i-1].w&&v.h+0.6>=samples[i-1].h));
        const contained=samples.every(v=>v.overflowX<=2&&v.overflowY<=2);
        const grew=samples.at(-1).w>samples[0].w+2||samples.at(-1).h>samples[0].h+2;
        const firstGrowthAt=(samples.find(v=>v.w>samples[0].w+2||v.h>samples[0].h+2)||{length:Infinity}).length;
        const reset=Math.abs(emptyAgain.w-samples[0].w)<1.5&&Math.abs(emptyAgain.h-samples[0].h)<1.5;
        const oneCharacterMinimum=Math.abs(samples[0].w-samples[1].w)<1.5&&Math.abs(samples[0].h-samples[1].h)<1.5;
        const authoredAspect=Number(x.style.textSizing&&x.style.textSizing.aspect)||1.8,baselineRatioOk=Math.abs(samples[0].w/samples[0].h-authoredAspect)<.03;
        const shortTextStable=samples.slice(0,3).every(v=>Math.abs(v.w-samples[0].w)<1.5&&Math.abs(v.h-samples[0].h)<1.5);
        const firstGrown=samples.find(v=>v.w>samples[0].w+2||v.h>samples[0].h+2);
        const growthAxesOk=!!firstGrown&&(mode==='uniform'?(firstGrown.w>samples[0].w+2&&firstGrown.h>samples[0].h+2):(firstGrown.w>samples[0].w+2&&Math.abs(firstGrown.h-samples[0].h)<1.5));
        live.push({mode,samples,growthSteps,monotonic,contained,grew,firstGrowthAt,reset,oneCharacterMinimum,baselineRatioOk,shortTextStable,growthAxesOk});cleanup(x);
      }

      const ruleCases=[];
      const check=(rules,valid,invalid)=>{const x=make(rules,'uniform'),accepted=[];for(const value of valid)accepted.push(insert(x.inner,value));const before=x.inner.textContent;const rejected=invalid.map(value=>!insert(x.inner,value));const unchanged=x.inner.textContent===before;const pasteRejected=!paste(x.inner,invalid[0]||'!');const nodeHasNoInlineRules=!TB[x.id].textRules;ruleCases.push({rules,accepted:accepted.every(Boolean),rejected:rejected.every(Boolean),unchanged,pasteRejected,nodeHasNoInlineRules,text:x.inner.textContent});cleanup(x);};
      check({type:'number'},['0','9'],['.','-','A','中']);
      check({type:'letter'},['A','z'],['1','中',' ']);
      check({type:'chinese'},['中','文'],['A','1',' ']);
      check({type:'alnum'},['A','9'],['中','-',' ']);
      check({type:'regex',pattern:'^[A-Z]{0,3}$'},['A','B'],['a','1','中']);
      check({type:'any',maxLength:2},['甲','乙'],['丙']);

      const stale=make({type:'number'},'uniform');TB[stale.id].textRules={type:'any',maxLength:0};delete TB[stale.id]._textRulesOverride;const staleCopyIgnored=!insert(stale.inner,'A')&&insert(stale.inner,'7');cleanup(stale);
      const override=make({type:'number'},'uniform');TB[override.id].textRules={type:'letter',maxLength:0};TB[override.id]._textRulesOverride=true;const explicitOverrideWorks=insert(override.inner,'A')&&!insert(override.inner,'7');cleanup(override);

      const required=make({type:'any',required:true},'uniform');const original='原文';TB[required.id].text=original;required.inner.textContent='';commitEdit(required.id);const requiredKept=TB[required.id].text===original;doDelete(required.id);
      return{live,ruleCases,requiredKept,staleCopyIgnored,explicitOverrideWorks};
    })()`);
    console.log('LIVE_INPUT_RULES_RESULT',JSON.stringify(result));
    const sizingOk=result.live.length===2&&result.live.every(x=>x.grew&&x.growthSteps>=3&&x.monotonic&&x.contained&&x.reset&&x.oneCharacterMinimum&&x.firstGrowthAt>1&&x.baselineRatioOk&&x.shortTextStable&&x.growthAxesOk);
    const rulesOk=result.ruleCases.length===6&&result.ruleCases.every(x=>x.accepted&&x.rejected&&x.unchanged&&x.pasteRejected&&x.nodeHasNoInlineRules);
    if(!sizingOk||!rulesOk||!result.requiredKept||!result.staleCopyIgnored||!result.explicitOverrideWorks)return fail('逐字扩展或输入限制检查失败',result);
    clearTimeout(timer);app.exit(0);
  }catch(e){fail(e.stack||String(e));}
});
