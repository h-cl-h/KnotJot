// EN: Exercise real renderer handlers in a hidden Electron window; keep baseline/final evidence outside source.
// ZH: 在隐藏的 Electron 窗口中执行真实渲染事件；将修复前后证据保存在源码目录外。
const {app, BrowserWindow} = require('electron');
const fs = require('fs');
const path = require('path');
const source = path.resolve(process.env.KNOTJOT_TEST_SOURCE || path.join(__dirname, '..'));
const out = path.resolve(__dirname, '../../preview/testing/renderer');
fs.mkdirSync(out, {recursive:true});
app.setPath('userData', path.join(out, 'profiles', String(Date.now())));
app.commandLine.appendSwitch('disable-gpu');
process.env.KNOTJOT_INTEGRATION_TEST='1';
require(path.join(source, 'main.js'));
app.whenReady().then(async()=>{
  // EN: Run independent cases and retain every failing assertion, screenshot and authored fixture.
  // ZH: 分别执行测试并保存全部失败断言、截图和编写的测试样本。
  const win=new BrowserWindow({show:false,width:1440,height:1000,webPreferences:{offscreen:true,backgroundThrottling:false,contextIsolation:true,nodeIntegration:false,preload:path.join(source,'preload.js')}});
  const timer=setTimeout(()=>app.exit(2),45000);
  try{
    await win.loadFile(path.join(source,'index.html'));
    const result=await win.webContents.executeJavaScript(`(${rendererCases.toString()})().catch(e=>({cases:[{name:'harness',pass:false,details:e.stack}]}))`);
    fs.writeFileSync(path.join(out,(process.env.KNOTJOT_TEST_LABEL||'final')+'.json'),JSON.stringify(result,null,2));
    const fixtureDirectory=path.join(out,(process.env.KNOTJOT_TEST_LABEL||'final')+'-fixtures');fs.mkdirSync(fixtureDirectory,{recursive:true});
    fs.writeFileSync(path.join(fixtureDirectory,'fixture.knotjot'),JSON.stringify(result.fixture||{},null,2));
    for(const fixture of result.fixtures||[])fs.writeFileSync(path.join(fixtureDirectory,fixture.name.replace(/[^a-z0-9]+/g,'-')+'.knotjot'),JSON.stringify(fixture.document,null,2));
    await new Promise(resolve=>setTimeout(resolve,250));
    fs.writeFileSync(path.join(out,(process.env.KNOTJOT_TEST_LABEL||'final')+'.png'),(await win.webContents.capturePage()).toPNG());
    console.log(JSON.stringify(result,null,2));
    clearTimeout(timer);app.exit(result.cases.some(x=>!x.pass)?1:0);
  }catch(error){console.error(error.stack);clearTimeout(timer);app.exit(2);}
});
async function rendererCases(){
  // EN: Use model fixtures only for setup; assert event outcomes and rendered geometry at non-unit zoom.
  // ZH: 模型样本仅用于准备；断言事件结果及非 100% 缩放下的实际几何。
  const cases=[],fixtures=[];const alerts=[];window.alert=message=>alerts.push(message);window.addEventListener('error',e=>alerts.push(e.message));const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  const check=(name,pass,details)=>cases.push({name,pass:!!pass,details});
  const documentFixture=()=>({app:'brace-mindmap',version:2,curSheet:0,sheets:[cleanSheetObject(activeSheetObject(),false)]});
  const run=async(name,fn)=>{try{await fn();}catch(e){check(name,false,e.message);}fixtures.push({name,document:documentFixture()});};
  const event=(el,type,x,y,extra={})=>el.dispatchEvent(new PointerEvent(type,{bubbles:true,cancelable:true,button:0,pointerId:73,clientX:x,clientY:y,...extra}));
  const reset=()=>{if(editing&&els[editing])els[editing]._inner.blur();if(ovEditing&&document.activeElement)document.activeElement.blur();hideStart();closeSettings();closeMenu();editing=null;ovEditing=null;history=[];hidx=0;historyShadow=null;fileHistory=[];fileHidx=0;historyBySheet.clear();TB={};BR={};RB={};FG={};LK={};roots=[];idc=0;loadExtras({});focusId=null;selOverlay=null;selSet=new Set();docType='brace';compactMode=0;readonly=false;curSheet=0;sheets=[activeSheetObject()];document.body.classList.remove('spider');scale=.75;panX=67;panY=43;update();applyTransform();};
  const node=(label,x,y,parent)=>{const id=newTextbox(label);TB[id].x=x;TB[id].y=y;if(parent){const bid=ensureBrace(parent);BR[bid].children.push(id);TB[id].parentBrace=bid;}else roots.push(id);return id;};
  await pause(200);reset();
  await run('selection drag',async()=>{const a=node('First',180,170),b=node('Second',650,310);update();selSet=new Set([a,b]);render();const c=els[a]._card,r=c.getBoundingClientRect();event(c,'pointerdown',r.x+30,r.y+25);event(c,'pointermove',r.x+90,r.y+55);event(c,'pointerup',r.x+90,r.y+55);check('selected roots drag together',Math.abs(TB[a].x-260)<1&&Math.abs(TB[b].x-730)<1&&selSet.size===2,{a:TB[a].x,b:TB[b].x,count:selSet.size});});
  reset();
  await run('todo cascade',async()=>{const a=node('Parent',140,130),b=node('Child',0,0,a),c=node('Grandchild',0,0,b);setTodo([a,b,c],true);toggleTodoDone(a);check('todo descendants complete by default',TB[b].done&&TB[c].done,{b:TB[b].done,c:TB[c].done});const toggle=document.getElementById('optTodoCascade');check('General cascade preference exists',!!toggle);if(toggle){toggle.checked=false;toggle.dispatchEvent(new Event('change'));TB[a].done=false;TB[b].done=false;TB[c].done=false;toggleTodoDone(a);check('cascade can be disabled',!TB[b].done&&!TB[c].done);toggle.checked=true;toggle.dispatchEvent(new Event('change'));}});
  reset();
  await run('lock appearance',async()=>{const a=node('Locked appearance',160,160);update();await pause(400);const before=getComputedStyle(els[a]._card);const prev={bg:before.backgroundColor,border:before.borderColor};toggleLockTb(a);await pause(250);const next=getComputedStyle(els[a]._card);check('lock preserves card appearance',prev.bg===next.backgroundColor&&prev.border===next.borderColor&&getComputedStyle(els[a].querySelector('.lock-badge')).display==='none',{before:prev,after:{bg:next.backgroundColor,border:next.borderColor}});check('no perpetual floating animation',getComputedStyle(els[a]._card).animationIterationCount!=='infinite');});
  reset();
  await run('annotation editing',async()=>{const a=node('Annotated node',180,240);update();addBoundary([a]);let bd=Object.keys(BD)[0],lab=overlayLayer.querySelector('.bd-label');check('new boundary has editable label',!!lab);if(lab){event(lab,'pointerdown',250,240);check('boundary selection preserves DOM for double click',lab.isConnected);lab.dispatchEvent(new MouseEvent('dblclick',{bubbles:true}));check('boundary label enters editing',lab.isConnected&&lab.isContentEditable);lab.textContent='';lab.blur();lab.dispatchEvent(new FocusEvent('blur'));check('empty label removes only label',!!BD[bd]&&!overlayLayer.querySelector('.bd-label'),{label:BD[bd]&&BD[bd].label,editing:ovEditing,active:document.activeElement.outerHTML.slice(0,400)});}addCallout(a);{const text=overlayLayer.querySelector('.co-text')||overlayLayer.querySelector('.co-bubble');if(text){text.textContent='Callout text';text.dispatchEvent(new FocusEvent('blur'));}}let co=Object.keys(CO)[0],bubble=overlayLayer.querySelector('.co-bubble');event(bubble,'pointerdown',400,260);check('callout selection preserves DOM for double click',bubble.isConnected);bubble.dispatchEvent(new MouseEvent('dblclick',{bubbles:true}));check('callout can enter editing',bubble.isConnected&&document.activeElement.isContentEditable);{const text=bubble.querySelector('.co-text')||bubble;text.textContent='Edited callout';text.dispatchEvent(new FocusEvent('blur'));}check('callout edit saved',CO[co].text==='Edited callout');bubble=overlayLayer.querySelector('.co-bubble');const r=bubble.getBoundingClientRect();event(bubble,'pointerdown',r.x+10,r.y+10);event(window,'pointermove',r.x+85,r.y+40);event(window,'pointerup',r.x+85,r.y+40);check('callout drag updates model',Math.abs(CO[co].offsetX-100)<1&&Math.abs(CO[co].offsetY-40)<1,CO[co]);const del=overlayLayer.querySelector('.co-del');check('callout delete control exists',!!del);if(del){del.click();check('callout deletion works',!CO[co]);}selectOverlay('bd',bd);const bdDel=overlayLayer.querySelector('.bd-del');check('empty boundary remains deletable',!!bdDel);if(bdDel){bdDel.click();check('boundary deletion works',!BD[bd]);}});
  reset();
  await run('image fidelity and paste',async()=>{const a=node('Image node',170,260);update();selectTb(a);const cv=document.createElement('canvas');cv.width=1600;cv.height=900;cv.getContext('2d').fillRect(0,0,1600,900);const src=cv.toDataURL('image/png');const blob=await new Promise(resolve=>cv.toBlob(resolve,'image/png'));const file=new File([blob],'full-resolution.png',{type:'image/png'});const data=new DataTransfer();data.items.add(file);window.dispatchEvent(new ClipboardEvent('paste',{bubbles:true,cancelable:true,clipboardData:data}));for(let wait=0;wait<50&&!TB[a].img;wait++)await pause(100);check('pasted image reaches selected node',!!TB[a].img,{active:document.activeElement.outerHTML.slice(0,150),editing,locked:TB[a].locked,alerts});if(TB[a].img){const image=new Image();image.src=TB[a].img.src;await image.decode();check('import preserves full resolution and bytes',image.naturalWidth===1600&&TB[a].img.src===src,{width:image.naturalWidth});const handle=els[a].querySelector('.img-resize');check('image has resize control',!!handle);if(handle){const r=handle.getBoundingClientRect(),w=TB[a].img.w;event(handle,'pointerdown',r.x,r.y);event(window,'pointermove',r.x+90,r.y+50);event(window,'pointerup',r.x+90,r.y+50);check('image resize grows without resampling',TB[a].img.w>w&&TB[a].img.src===src&&els[a]._imgWrap.firstElementChild.width>220,TB[a].img.w);}}});
  reset();
  await run('coordinates',async()=>{canvas.scrollLeft=37;canvas.scrollTop=29;const x=550,y=370;const count=roots.length;canvas.dispatchEvent(new MouseEvent('dblclick',{bubbles:true,clientX:x,clientY:y}));if(editing)els[editing]._inner.blur();await pause(50);const a=roots[count],r=els[a]._card.getBoundingClientRect();check('double click maps to pointer under scroll zoom and pan',Math.abs(r.left+r.width/2-x)<1&&Math.abs(r.top+r.height/2-y)<1,{pointer:{x,y},node:{left:r.left,center:r.top+r.height/2},scroll:{x:canvas.scrollLeft,y:canvas.scrollTop}});event(canvas,'pointerdown',800,550);event(canvas,'pointermove',900,650);const mq=document.getElementById('marquee').getBoundingClientRect();check('marquee begins at pointer',Math.abs(mq.left-800)<1&&Math.abs(mq.top-550)<1,{left:mq.left,top:mq.top});event(canvas,'pointerup',900,650);canvas.scrollLeft=0;canvas.scrollTop=0;});
  reset();
  await run('menus and metadata',async()=>{const a=node('Metadata label',200,160),b=node('Neighbour',205,115);TB[a].markers=['p1'];TB[a].note='Note';update();openMenu(a,350,220);const labels=ctx.textContent;check('node menu has Copy and Cut',labels.includes('复制')&&labels.includes('剪切'),labels);closeMenu();selSet=new Set([a,b]);openMenu(a,350,220);check('multi menu removes Add summary',!ctx.textContent.includes('概要'));check('insert menu removes summary',!menuItems('insert').some(x=>(x.label||'').includes('概要')));selectTb(a);const actions=els[a].querySelector('.actions'),r=actions.getBoundingClientRect();const top=document.elementFromPoint(r.left+10,r.top+10);check('node actions stack over neighbour',top&&actions.contains(top),top&&top.className);const mk=els[a]._mkRow.getBoundingClientRect(),txt=els[a]._inner.getBoundingClientRect();check('metadata icons precede text without overlap',mk.right<=txt.left+.5&&Math.abs((mk.top+mk.height/2)-(txt.top+txt.height/2))<25,{mk:{x:mk.x,y:mk.y,right:mk.right},text:{x:txt.x,y:txt.y}});closeMenu();});
  reset();
  await run('crossing handles',async()=>{
    const a=node('A',240,200),b=node('B',0,0,a);TB[a].struct='logicR';update();const h=svg.querySelector('.seg-handle'),hx=+h.getAttribute('cx'),hy=+h.getAttribute('cy');
    const c=node('Other',hx-170,hy),d=node('Other child',0,0,c);TB[c].struct='logicR';TB[d]._offX=130;update();
    const handle=svg.querySelector('[data-seg="'+b+'"]'),rect=handle.getBoundingClientRect(),top=document.elementFromPoint(rect.x+rect.width/2,rect.y+rect.height/2);
    check('handle wins hit testing over crossing branch',top===handle,top&&top.outerHTML);
  });
  reset();
  await run('clipboard round trip',async()=>{
    const a=node('Parent copy',200,200),b=node('Child copy',0,0,a);TB[a].note='Preserved note';TB[b].todo=true;TB[b].done=true;update();BD.bdCopy={id:'bdCopy',members:[a,b],label:'Group',color:''};CO.coCopy={id:'coCopy',tb:b,text:'Moved note',offsetX:70,offsetY:25};
    const serialized=typeof makeNodeClipboard==='function'?makeNodeClipboard([a]):'';check('node clipboard snapshot is available',!!serialized);
    if(serialized){check('paste accepts valid node snapshot',pasteNodes(serialized));const copy=selPrimary(),child=copy&&BR[TB[copy].brace].children[0];check('paste remaps hierarchy and metadata',copy!==a&&TB[copy].note==='Preserved note'&&child!==b&&TB[child].done);check('callout offsets survive copy',Object.values(CO).some(co=>co.tb===child&&co.offsetX===70&&co.offsetY===25));check('pasted state validates',!!KnotJotDocumentModel.validateSheet(cleanSheetObject(activeSheetObject(),false),'fixture'));}
  });
  reset();
  await run('converted spider lifecycle',async()=>{
    const a=node('Parent',200,200),b=node('Child',0,0,a),c=node('Grandchild',0,0,b);update();setStruct(a,'spider');
    selectTb(b);const x=TB[b]._x,y=TB[b]._cy,card=els[b]._card,r=card.getBoundingClientRect();event(card,'pointerdown',r.x+20,r.y+20);event(card,'pointermove',r.x+80,r.y+50);event(card,'pointerup',r.x+80,r.y+50);check('converted spider descendant drags as graph node',Math.abs(TB[b]._x-x-80)<1&&Math.abs(TB[b]._cy-y-40)<1,{before:x,after:TB[b]._x});
    if(typeof makeNodeClipboard==='function'){const data=JSON.parse(makeNodeClipboard([a]).split('\n').slice(1).join('\n'));check('spider clipboard contains only selected graph nodes',Object.keys(data.textboxes).length===1,Object.keys(data.textboxes));}
    doDelete(a);check('converted spider delete preserves unselected nodes',!!TB[b]&&!!TB[c]&&!TB[a],Object.keys(TB));if(TB[b]&&TB[c])check('surviving spider hierarchy validates',!!KnotJotDocumentModel.validateSheet(cleanSheetObject(activeSheetObject(),false),'fixture'));
  });
  reset();
  await run('copy and cut commands',async()=>{
    // EN: Replace only the OS write boundary so this test does not alter the user's clipboard; inspect real node mutations.
    // ZH: 仅替换系统写入边界以免改动用户剪贴板；检查真实节点状态变化。
    const a=node('Cut parent',200,200),b=node('Cut child',0,0,a);update();let copied='';const write=navigator.clipboard.writeText;
    try{navigator.clipboard.writeText=async text=>{copied=text;};check('copy command preserves source nodes',await copyNodes(false,[a])&&!!TB[a]&&!!TB[b]);check('cut command removes selected subtree',await copyNodes(true,[a])&&!TB[a]&&!TB[b]);check('cut payload pastes with hierarchy',pasteNodes(copied)&&Object.values(TB).some(node=>node.text==='Cut child'));}finally{navigator.clipboard.writeText=write;}
  });
  reset();
  await run('selected ancestor drag',async()=>{
    const a=node('Selected ancestor',180,200),b=node('Selected descendant',0,0,a),c=node('Peer',600,350);update();const bx=TB[b]._x,by=TB[b]._cy;selSet=new Set([a,b,c]);render();const el=els[a]._card,r=el.getBoundingClientRect();event(el,'pointerdown',r.x+20,r.y+20);event(window,'pointermove',r.x+80,r.y+50);event(window,'pointerup',r.x+80,r.y+50);check('selected descendants move once with ancestor',Math.abs(TB[b]._x-bx-80)<1&&Math.abs(TB[b]._cy-by-40)<1&&TB[c].x===680,{b:TB[b]._x,before:bx,c:TB[c].x});
  });
  reset();
  await run('annotation history and file round trip',async()=>{
    const a=node('Round trip',200,240);update();CO.coHistory={id:'coHistory',tb:a,text:'Remember position',color:'',offsetX:0,offsetY:0};BD.bdEmpty={id:'bdEmpty',members:[a],label:'',color:''};SM.smLegacy={id:'smLegacy',members:[a],text:'Legacy summary retained'};update();initHistory();const bubble=overlayLayer.querySelector('.co-bubble'),r=bubble.getBoundingClientRect();event(bubble,'pointerdown',r.x+8,r.y+8);event(window,'pointermove',r.x+68,r.y+38);event(window,'pointerup',r.x+68,r.y+38);check('annotation drag is undoable',CO.coHistory.offsetX===80);undo();check('undo restores callout location',CO.coHistory.offsetX===0);redo();check('redo restores moved callout location',CO.coHistory.offsetX===80);const text=JSON.stringify(documentFixture());check('saved document reload succeeds',await loadDataAsync(text,'Renderer round trip'));check('annotations survive save and reopen',CO.coHistory.offsetX===80&&BD.bdEmpty.label===''&&SM.smLegacy.text==='Legacy summary retained');const bounds=overlayLayer.querySelector('.co-bubble').getBoundingClientRect();check('callout text has readable width',bounds.width>70&&bounds.height<60,{width:bounds.width,height:bounds.height});
  });
  check('no unexpected renderer errors',alerts.length===0,alerts);
  return {cases,fixtures,fixture:documentFixture(),userAgent:navigator.userAgent};
}
