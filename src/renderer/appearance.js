(function(){
'use strict';

const FORMAT='knotjot-appearance';
const STORAGE_KEY='bmap.appearance.v1';
const CANVAS_CENTER={x:3000,y:1800};
const SIMPLE_TOKENS={
  accent:'--accent',accentSoft:'--accent-soft',bg:'--bg',grid:'--grid',card:'--card',cardBorder:'--card-border',
  ink:'--ink',muted:'--muted',toolbarBg:'--toolbar-bg',menuBg:'--menu-bg',menuHover:'--menu-hover',brace:'--brace',danger:'--danger'
};
const PALETTE_FIELDS=[
  ['accent','主色'],['accentSoft','浅主色'],['bg','画布底色'],['grid','网格'],['card','卡片'],['cardBorder','卡片边框'],
  ['ink','主要文字'],['muted','次要文字'],['toolbarBg','工具栏'],['menuBg','菜单'],['menuHover','菜单悬停'],['brace','连线/大括号'],
  ['danger','危险色'],['startSideA','开始页侧栏 1'],['startSideB','开始页侧栏 2'],['thumbBlankA','新建卡片 1'],
  ['thumbBlankB','新建卡片 2'],['thumbSampleA','示例卡片 1'],['thumbSampleB','示例卡片 2'],['thumbOpenA','打开卡片 1'],['thumbOpenB','打开卡片 2']
];
const FALLBACK_PALETTE={
  accent:'#5b8def',accentSoft:'#e8f0ff',bg:'#f5f6f8',grid:'#e7e9ee',card:'#ffffff',cardBorder:'#e2e5ec',
  ink:'#2c3140',muted:'#8a90a0',toolbarBg:'#ffffff',menuBg:'#ffffff',menuHover:'#e8f0ff',brace:'#b6bdcc',danger:'#e3604f',
  startSideA:'#5b8def',startSideB:'#7b6cf0',thumbBlankA:'#5b8def',thumbBlankB:'#6f9bf2',
  thumbSampleA:'#7b6cf0',thumbSampleB:'#9a7af2',thumbOpenA:'#39b59a',thumbOpenB:'#46c4a8'
};
const DEFAULT_BG={enabled:false,mode:'unlimited',imageData:'',imageName:'',imageWidth:0,imageHeight:0,
  canvasWidth:6000,canvasHeight:3600,imageScale:100,positionX:0,positionY:0};

let committed=null;
let draft=null;
let appDefaultBackground=JSON.parse(JSON.stringify(DEFAULT_BG));
let documentBackgroundExplicit=false;
let editingSession=false;
let sessionDirty=false;
let previewZoom=100;
let initialized=false;
let constrainGuard=false;
let statusTimer=0;

const clone=o=>JSON.parse(JSON.stringify(o));
const clamp=(n,a,b)=>Math.min(b,Math.max(a,Number(n)||0));
const nonNegativeInt=v=>{const n=Number(v);return Number.isFinite(n)&&n>0?Math.round(n):0;};
const validHex=v=>/^#[0-9a-f]{6}$/i.test(String(v||''));
function rgbToHex(v){
  if(validHex(v)) return String(v).toLowerCase();
  const m=String(v||'').match(/rgba?\(\s*(\d+)[, ]+\s*(\d+)[, ]+\s*(\d+)/i);
  if(!m) return '';
  return '#'+[m[1],m[2],m[3]].map(x=>Math.min(255,+x).toString(16).padStart(2,'0')).join('');
}
function gradientParts(v,a,b){
  const hits=String(v||'').match(/#[0-9a-f]{6}/ig)||[];
  return [validHex(hits[0])?hits[0].toLowerCase():a,validHex(hits[1])?hits[1].toLowerCase():b];
}
function seedPalette(){
  const cs=getComputedStyle(document.documentElement), p=clone(FALLBACK_PALETTE);
  if(document.documentElement.getAttribute('data-uiskin')==='default'){
    for(const key in SIMPLE_TOKENS){ const h=rgbToHex(cs.getPropertyValue(SIMPLE_TOKENS[key]).trim()); if(h)p[key]=h; }
  }
  try{
    const s=typeof allSchemes==='function'?(allSchemes()[curScheme]||null):null;
    if(s){
      let q=gradientParts(s.sideGrad,p.startSideA,p.startSideB);p.startSideA=q[0];p.startSideB=q[1];
      q=gradientParts(s.thumbBlank,p.thumbBlankA,p.thumbBlankB);p.thumbBlankA=q[0];p.thumbBlankB=q[1];
      q=gradientParts(s.thumbSample,p.thumbSampleA,p.thumbSampleB);p.thumbSampleA=q[0];p.thumbSampleB=q[1];
      q=gradientParts(s.thumbOpen,p.thumbOpenA,p.thumbOpenB);p.thumbOpenA=q[0];p.thumbOpenB=q[1];
    }
  }catch(_){ }
  return p;
}
function defaultState(){ return {app:FORMAT,version:1,palette:seedPalette(),background:clone(DEFAULT_BG)}; }
function normalize(input){
  const base=defaultState(), o=input&&typeof input==='object'?input:{};
  const src=o.palette&&typeof o.palette==='object'?o.palette:{};
  for(const [key] of PALETTE_FIELDS) base.palette[key]=validHex(src[key])?String(src[key]).toLowerCase():base.palette[key];
  const b=o.background&&typeof o.background==='object'?o.background:{};
  base.background.enabled=!!b.enabled;
  base.background.mode=b.mode==='limited'?'limited':'unlimited';
  base.background.imageName=String(b.imageName||'').replace(/[\r\n]/g,' ').slice(0,120);
  base.background.imageWidth=nonNegativeInt(b.imageWidth);
  base.background.imageHeight=nonNegativeInt(b.imageHeight);
  const data=String(b.imageData||'');
  base.background.imageData=/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(data)?data:'';
  if(!base.background.imageData){base.background.imageName='';base.background.imageWidth=0;base.background.imageHeight=0;}
  base.background.canvasWidth=Math.round(clamp(b.canvasWidth||6000,640,24000));
  base.background.canvasHeight=Math.round(clamp(b.canvasHeight||3600,480,16000));
  if(!base.background.enabled&&!base.background.imageData&&base.background.canvasWidth===1920&&base.background.canvasHeight===1080){
    base.background.canvasWidth=6000;base.background.canvasHeight=3600;
  }
  base.background.imageScale=Math.round(clamp(b.imageScale||100,10,400));
  base.background.positionX=Math.round(clamp(b.positionX,-100,100));
  base.background.positionY=Math.round(clamp(b.positionY,-100,100));
  return base;
}
function activeState(){ return editingSession&&draft?draft:committed; }
function documentBackground(){
  try{return documentAppearance&&documentAppearance.background&&typeof documentAppearance.background==='object'
    ?normalize({background:documentAppearance.background}).background:clone(appDefaultBackground);}catch(_){return clone(appDefaultBackground);}
}
function isDefaultUI(){ return document.documentElement.getAttribute('data-uiskin')==='default'; }
function canvasBounds(state=committed){
  const b=(state&&state.background)||DEFAULT_BG;
  return {left:CANVAS_CENTER.x-b.canvasWidth/2,top:CANVAS_CENTER.y-b.canvasHeight/2,width:b.canvasWidth,height:b.canvasHeight,
    right:CANVAS_CENTER.x+b.canvasWidth/2,bottom:CANVAS_CENTER.y+b.canvasHeight/2};
}
function setRootVar(name,value){ document.documentElement.style.setProperty(name,value); }
function applyPalette(p){
  for(const key in SIMPLE_TOKENS) setRootVar(SIMPLE_TOKENS[key],p[key]);
  setRootVar('--side-grad',`linear-gradient(160deg,${p.startSideA},${p.startSideB})`);
  setRootVar('--thumb-blank',`linear-gradient(135deg,${p.thumbBlankA},${p.thumbBlankB})`);
  setRootVar('--thumb-sample',`linear-gradient(135deg,${p.thumbSampleA},${p.thumbSampleB})`);
  setRootVar('--thumb-open',`linear-gradient(135deg,${p.thumbOpenA},${p.thumbOpenB})`);
}
function clearPalette(){
  Object.values(SIMPLE_TOKENS).concat(['--side-grad','--thumb-blank','--thumb-sample','--thumb-open']).forEach(x=>document.documentElement.style.removeProperty(x));
}
function ensureLayers(){
  let unlimited=document.getElementById('tcCanvasUnlimited');
  if(!unlimited){ unlimited=document.createElement('div');unlimited.id='tcCanvasUnlimited';canvas.insertBefore(unlimited,world); }
  let limited=document.getElementById('tcCanvasLimited');
  if(!limited){ limited=document.createElement('div');limited.id='tcCanvasLimited';limited.innerHTML='<div id="tcCanvasLimitedGrid"></div>';world.insertBefore(limited,world.firstChild); }
  return {unlimited,limited};
}
function updateCanvasTransform(){
  if(!committed) return;
  const s=activeState(), b=s.background, layers=ensureLayers();
  const gs=26*scale;
  canvas.classList.toggle('tc-limited',b.enabled&&b.mode==='limited');
  if(!(b.enabled&&b.mode==='limited')){
    canvas.style.backgroundImage='linear-gradient(var(--grid) 1px,transparent 1px),linear-gradient(90deg,var(--grid) 1px,transparent 1px)';
    canvas.style.backgroundSize=gs+'px '+gs+'px';canvas.style.backgroundPosition=panX+'px '+panY+'px';
  }else{ canvas.style.backgroundImage='none'; }
  layers.unlimited.style.display='none';layers.limited.style.display='none';
  layers.unlimited.classList.toggle('has-image',!!b.imageData);
  layers.limited.classList.toggle('has-image',!!b.imageData);
  if(!b.enabled) return;
  if(b.mode==='unlimited'){
    if(!b.imageData) return;
    const iw=Math.max(8,b.imageWidth*b.imageScale/100*scale), ih=Math.max(8,b.imageHeight*b.imageScale/100*scale);
    layers.unlimited.style.display='block';layers.unlimited.style.backgroundColor=b.imageData?'var(--bg)':'transparent';layers.unlimited.style.backgroundImage=`url("${b.imageData}")`;
    layers.unlimited.style.backgroundSize=iw+'px '+ih+'px';
    layers.unlimited.style.backgroundPosition=(panX+b.positionX/100*iw)+'px '+(panY+b.positionY/100*ih)+'px';
  }else{
    const q=canvasBounds(s), iw=b.imageWidth*b.imageScale/100, ih=b.imageHeight*b.imageScale/100;
    layers.limited.style.display='block';layers.limited.style.left=q.left+'px';layers.limited.style.top=q.top+'px';
    layers.limited.style.width=q.width+'px';layers.limited.style.height=q.height+'px';
    layers.limited.style.backgroundImage=b.imageData?`url("${b.imageData}")`:'none';
    layers.limited.style.backgroundRepeat='no-repeat';
    layers.limited.style.backgroundSize=(iw||0)+'px '+(ih||0)+'px';
    layers.limited.style.backgroundPosition=`calc(50% + ${b.positionX/100*q.width/2}px) calc(50% + ${b.positionY/100*q.height/2}px)`;
  }
}
function applyCurrent(){
  if(!committed) return;
  if(isDefaultUI()) applyPalette(activeState().palette); else clearPalette();
  updateCanvasTransform();
  renderLockState();renderPreview();
}
function setStatus(text,kind=''){
  const el=document.getElementById('tcAppearanceStatus');if(!el)return;
  if(typeof window.t==='function')text=window.t(text);
  el.textContent=text;el.className='tc-app-status '+kind;
  clearTimeout(statusTimer);if(text&&kind==='ok')statusTimer=setTimeout(()=>{if(el.textContent===text)el.textContent='';},5000);
}
function safeLocalSave(state){
  try{localStorage.setItem(STORAGE_KEY,JSON.stringify(state));return true;}catch(_){
    try{const small=clone(state);small.background.imageData='';small.background.imageName='';small.background.imageWidth=0;small.background.imageHeight=0;localStorage.setItem(STORAGE_KEY,JSON.stringify(small));}catch(__){}
    return false;
  }
}
async function persist(state=committed){
  const globalState={app:FORMAT,version:1,palette:clone(state.palette),background:clone(state.background)};
  const json=JSON.stringify(globalState);let localOk=true;
  if(window.api&&window.api.saveAppearance){
    const r=await window.api.saveAppearance(json);if(!r||!r.ok)throw new Error((r&&r.error)||'外观设置保存失败');
    localOk=safeLocalSave(globalState);
  }else{localOk=safeLocalSave(globalState);if(!localOk&&state.background.imageData)throw new Error('浏览器存储空间不足，背景图片未能持久化');}
  return {ok:true,localOk};
}
function parseStored(text){ try{const o=JSON.parse(text);return o&&o.app===FORMAT?normalize(o):null;}catch(_){return null;} }
async function restore(){
  let found=parseStored(localStorage.getItem(STORAGE_KEY)||'');if(found){committed.palette=found.palette;appDefaultBackground=clone(found.background);}
  committed.background=documentBackground();documentAppearance={background:clone(committed.background)};
  applyCurrent();
  try{
    if(window.api&&window.api.loadAppearance){const txt=await window.api.loadAppearance();const fileState=parseStored(txt||'');if(fileState){committed.palette=fileState.palette;appDefaultBackground=clone(fileState.background);
      if(!documentBackgroundExplicit){committed.background=clone(appDefaultBackground);documentAppearance={background:clone(committed.background)};}
      safeLocalSave({app:FORMAT,version:1,palette:clone(committed.palette),background:clone(appDefaultBackground)});applyCurrent();syncControls(false);}}
  }catch(_){ }
}
function paletteMarkup(){return PALETTE_FIELDS.map(([key,label])=>{const x=typeof window.t==='function'?window.t(label):label;return `<label class="tc-app-color"><input type="color" data-palette="${key}" value="${FALLBACK_PALETTE[key]}"><span title="${x}">${x}</span></label>`;}).join('');}
function buildPanel(){
  const pane=document.getElementById('paneAppearance');if(!pane||document.getElementById('tcAppearancePanel'))return;
  const panel=document.createElement('div');panel.id='tcAppearancePanel';panel.innerHTML=`
    <div class="tc-app-lock">当前不是原版 UI，原版配色暂不生效；导图背景与有限画布仍可独立编辑。<br><button class="tc-app-mini" id="tcSwitchDefault" type="button">切回原版 UI 编辑配色</button></div>
    <div class="tc-app-editable">
      <div class="tc-app-head"><span>原版 UI 配色</span><span class="spacer"></span><button class="tc-app-mini" id="tcPaletteExport" type="button">导出 .knotjot-theme</button><button class="tc-app-mini" id="tcPaletteReset" type="button">恢复原版颜色</button></div>
      <div class="tc-app-palette">${paletteMarkup()}</div>
      <div class="tc-app-head"><span>思维导图背景</span><span class="spacer"></span><label class="tc-bg-enable"><input id="tcBgEnabled" type="checkbox">启用</label></div>
      <div class="tc-bg-toolbar"><button class="btn" id="tcBgImport" type="button">🖼 导入图片并预览</button><button class="tc-app-mini" id="tcBgClear" type="button">清除图片</button><span class="tc-bg-name" id="tcBgName">未导入图片</span></div>
      <input id="tcBgFile" type="file" accept="image/png,image/jpeg,image/webp,.png,.jpg,.jpeg,.webp" hidden>
      <div class="tc-bg-modes">
        <label class="tc-bg-mode"><input type="radio" name="tcBgMode" value="unlimited"><span><b>不限制画布大小</b>图片横向、纵向连续复制，画布仍可无限平移。</span></label>
        <label class="tc-bg-mode"><input type="radio" name="tcBgMode" value="limited"><span><b>限制画布大小</b>建立有限边界，默认 6000 × 3600，图片在边界内缩放和定位。</span></label>
      </div>
      <div class="tc-bg-preview-shell">
        <div class="tc-bg-preview-head"><span id="tcPreviewSize">预览</span><span class="spacer"></span><span>预览缩放</span><input id="tcPreviewZoom" type="range" min="25" max="800" step="25" value="100"><output id="tcPreviewZoomOut">100%</output></div>
        <div class="tc-bg-preview" id="tcBgPreview"><div class="tc-bg-preview-image" id="tcBgPreviewImage"><div class="tc-bg-preview-grid"></div></div><div class="tc-bg-reference" title="空白的原版默认文本框（实际比例）"><div class="card"><div class="card-inner"></div></div></div></div>
      </div>
      <div class="tc-bg-controls">
        <label class="tc-bg-control tc-bg-limited-only"><span>画布宽度</span><input id="tcCanvasWidth" type="number" min="640" max="24000" step="10"><output>px</output></label>
        <label class="tc-bg-control tc-bg-limited-only"><span>画布高度</span><input id="tcCanvasHeight" type="number" min="480" max="16000" step="10"><output>px</output></label>
        <label class="tc-bg-control"><span>图片缩放</span><input id="tcImageScale" type="range" min="10" max="400" step="1"><output id="tcImageScaleOut"></output></label>
        <label class="tc-bg-control"><span>水平位置</span><input id="tcImageX" type="range" min="-100" max="100" step="1"><output id="tcImageXOut"></output></label>
        <label class="tc-bg-control"><span>垂直位置</span><input id="tcImageY" type="range" min="-100" max="100" step="1"><output id="tcImageYOut"></output></label>
      </div>
      <div class="tc-app-actions"><button class="tc-app-mini" id="tcAppearanceCancel" type="button">撤销未应用修改</button><span class="spacer"></span><button class="btn primary" id="tcAppearanceApply" type="button">应用并保存</button></div>
      <div class="tc-app-status" id="tcAppearanceStatus"></div>
    </div>`;
  pane.appendChild(panel);bindPanel();if(typeof window.translateNode==='function')window.translateNode(panel);
}
function beginEdit(){
  if(!committed)return;draft=clone(committed);editingSession=true;sessionDirty=false;previewZoom=100;syncControls();applyCurrent();
}
function cancelEdit(silent=false){
  if(!editingSession)return;draft=clone(committed);sessionDirty=false;editingSession=false;applyCurrent();if(!silent)setStatus('已撤销未应用的外观修改','ok');
}
function edit(mutator){if(!editingSession)beginEdit();mutator(draft);draft=normalize(draft);sessionDirty=true;syncControls(false);applyCurrent();}
function syncControls(all=true){
  if(!draft)return;const p=draft.palette,b=draft.background,panel=document.getElementById('tcAppearancePanel');if(!panel)return;
  if(all) panel.querySelectorAll('[data-palette]').forEach(x=>x.value=p[x.dataset.palette]);
  document.getElementById('tcBgEnabled').checked=b.enabled;
  panel.querySelectorAll('input[name="tcBgMode"]').forEach(x=>x.checked=x.value===b.mode);
  panel.classList.toggle('mode-limited',b.mode==='limited');
  document.getElementById('tcBgName').textContent=b.imageName?`${b.imageName} · ${b.imageWidth}×${b.imageHeight}`:(typeof window.t==='function'?window.t('未导入图片'):'未导入图片');
  document.getElementById('tcCanvasWidth').value=b.canvasWidth;document.getElementById('tcCanvasHeight').value=b.canvasHeight;
  document.getElementById('tcImageScale').value=b.imageScale;document.getElementById('tcImageScaleOut').value=b.imageScale+'%';
  document.getElementById('tcImageX').value=b.positionX;document.getElementById('tcImageXOut').value=(b.positionX>0?'+':'')+b.positionX+'%';
  document.getElementById('tcImageY').value=b.positionY;document.getElementById('tcImageYOut').value=(b.positionY>0?'+':'')+b.positionY+'%';
  document.getElementById('tcPreviewZoom').value=previewZoom;document.getElementById('tcPreviewZoomOut').value=previewZoom+'%';
  renderPreview();renderLockState();
}
function renderLockState(){
  const panel=document.getElementById('tcAppearancePanel');if(panel)panel.classList.toggle('palette-locked',!isDefaultUI());
}
function renderPreview(){
  const box=document.getElementById('tcBgPreview'),img=document.getElementById('tcBgPreviewImage');if(!box||!draft)return;
  const b=draft.background, ref=box.querySelector('.tc-bg-reference'), W=box.clientWidth||520,H=box.clientHeight||210;
  box.classList.toggle('unlimited',b.mode==='unlimited');box.classList.toggle('has-image',!!b.imageData);img.style.backgroundImage=b.imageData?`url("${b.imageData}")`:'none';
  img.style.backgroundColor=draft.palette.bg;
  if(b.mode==='limited'){
    const fit=Math.min((W-20)/b.canvasWidth,(H-20)/b.canvasHeight)*previewZoom/100;
    const rw=b.canvasWidth*fit,rh=b.canvasHeight*fit,iw=b.imageWidth*b.imageScale/100*fit,ih=b.imageHeight*b.imageScale/100*fit;
    img.style.left=(W-rw)/2+'px';img.style.top=(H-rh)/2+'px';img.style.width=rw+'px';img.style.height=rh+'px';
    img.style.backgroundRepeat='no-repeat';img.style.backgroundSize=iw+'px '+ih+'px';
    img.style.backgroundPosition=`calc(50% + ${b.positionX/100*rw/2}px) calc(50% + ${b.positionY/100*rh/2}px)`;
    ref.style.transform='translate(-50%,-50%)';
    document.getElementById('tcPreviewSize').textContent=`${b.canvasWidth} × ${b.canvasHeight} · ${typeof window.t==='function'?window.t('预览'):'预览'} ${Math.round(fit*100)}%`;
  }else{
    const unit=.2*previewZoom/100,iw=Math.max(12,b.imageWidth*b.imageScale/100*unit),ih=Math.max(12,b.imageHeight*b.imageScale/100*unit);
    img.style.left='0';img.style.top='0';img.style.width='100%';img.style.height='100%';img.style.backgroundRepeat='repeat';
    img.style.backgroundSize=iw+'px '+ih+'px';img.style.backgroundPosition=(b.positionX/100*iw)+'px '+(b.positionY/100*ih)+'px';
    ref.style.transform='translate(-50%,-50%)';document.getElementById('tcPreviewSize').textContent=typeof window.t==='function'?window.t('无限平铺预览'):'无限平铺预览';
  }
}
function decodeImage(file){return new Promise((resolve,reject)=>{const url=URL.createObjectURL(file),im=new Image();im.onload=()=>{const r={width:im.naturalWidth,height:im.naturalHeight};URL.revokeObjectURL(url);resolve(r);};im.onerror=()=>{URL.revokeObjectURL(url);reject(new Error('图片无法解码'));};im.src=url;});}
function readDataURL(file){return new Promise((resolve,reject)=>{const rd=new FileReader();rd.onload=()=>resolve(String(rd.result||''));rd.onerror=()=>reject(new Error('图片读取失败'));rd.readAsDataURL(file);});}
async function importBackground(file){
  if(!file)return;const okType=/\.(png|jpe?g|webp)$/i.test(file.name)||['image/png','image/jpeg','image/webp'].includes(file.type);
  if(!okType){setStatus('仅支持 PNG、JPG/JPEG、WebP','err');return;}
  try{
    if(file.size>KnotJotSecurity.LIMITS.imageBytes)throw new Error('背景图片文件超过 12 MB 上限');
    const meta=KnotJotSecurity.validateImageDimensions(KnotJotSecurity.dimensionsFromBytes(await file.slice(0,512*1024).arrayBuffer()));
    const dim=await decodeImage(file);
    if(dim.width!==meta.width||dim.height!==meta.height)throw new Error('图片元数据与解码尺寸不一致');
    const data=await readDataURL(file);
    edit(s=>{Object.assign(s.background,{enabled:true,imageData:data,imageName:file.name,imageWidth:dim.width,imageHeight:dim.height});});
    setStatus('图片已载入预览；确认效果后点击“应用并保存”','ok');
  }catch(e){setStatus(e.message||'图片导入失败','err');}
}
function exportTheme(){
  const p=(draft||committed).palette,o={app:'brace-mindmap-theme',version:2,name:'原版自定义配色',accent:p.accent,accentSoft:p.accentSoft,
    sideGrad:`160deg,${p.startSideA},${p.startSideB}`,thumbBlank:`135deg,${p.thumbBlankA},${p.thumbBlankB}`,
    thumbSample:`135deg,${p.thumbSampleA},${p.thumbSampleB}`,thumbOpen:`135deg,${p.thumbOpenA},${p.thumbOpenB}`,
    tokens:{bg:p.bg,grid:p.grid,card:p.card,cardBorder:p.cardBorder,ink:p.ink,muted:p.muted,toolbarBg:p.toolbarBg,menuBg:p.menuBg,menuHover:p.menuHover,brace:p.brace,danger:p.danger}};
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(o,null,2)],{type:'application/json'}));a.download='KnotJot-Original-Theme.knotjot-theme';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000);
}
async function applySchemeToState(s){
  if(!initialized||!s||!isDefaultUI())return;const next=clone(committed),p=next.palette;p.accent=s.accent;p.accentSoft=s.accentSoft;
  let q=gradientParts(s.sideGrad,p.startSideA,p.startSideB);p.startSideA=q[0];p.startSideB=q[1];
  q=gradientParts(s.thumbBlank,p.thumbBlankA,p.thumbBlankB);p.thumbBlankA=q[0];p.thumbBlankB=q[1];
  q=gradientParts(s.thumbSample,p.thumbSampleA,p.thumbSampleB);p.thumbSampleA=q[0];p.thumbSampleB=q[1];
  q=gradientParts(s.thumbOpen,p.thumbOpenA,p.thumbOpenB);p.thumbOpenA=q[0];p.thumbOpenB=q[1];
  if(s.tokens&&typeof s.tokens==='object') for(const key of ['bg','grid','card','cardBorder','ink','muted','toolbarBg','menuBg','menuHover','brace','danger']) if(validHex(s.tokens[key]))p[key]=String(s.tokens[key]).toLowerCase();
  try{await persist(next);committed.palette=clone(p);if(editingSession)draft.palette=clone(p);applyCurrent();syncControls();setStatus('原版配色已保存','ok');}
  catch(e){draft=clone(committed);applyCurrent();syncControls();setStatus(e.message||'配色保存失败','err');}
}
function bindPanel(){
  const panel=document.getElementById('tcAppearancePanel');
  panel.querySelectorAll('[data-palette]').forEach(x=>x.addEventListener('input',()=>edit(s=>s.palette[x.dataset.palette]=x.value)));
  document.getElementById('tcSwitchDefault').onclick=()=>{if(typeof applyUISkin==='function')applyUISkin('default');};
  document.getElementById('tcPaletteReset').onclick=()=>edit(s=>s.palette=clone(FALLBACK_PALETTE));
  document.getElementById('tcPaletteExport').onclick=exportTheme;
  document.getElementById('tcBgEnabled').onchange=e=>edit(s=>s.background.enabled=e.target.checked);
  panel.querySelectorAll('input[name="tcBgMode"]').forEach(x=>x.onchange=()=>{if(x.checked)edit(s=>s.background.mode=x.value);});
  document.getElementById('tcBgImport').onclick=()=>document.getElementById('tcBgFile').click();
  document.getElementById('tcBgFile').onchange=e=>{importBackground(e.target.files&&e.target.files[0]);e.target.value='';};
  document.getElementById('tcBgClear').onclick=()=>edit(s=>Object.assign(s.background,{imageData:'',imageName:'',imageWidth:0,imageHeight:0}));
  document.getElementById('tcCanvasWidth').oninput=e=>edit(s=>s.background.canvasWidth=e.target.value);
  document.getElementById('tcCanvasHeight').oninput=e=>edit(s=>s.background.canvasHeight=e.target.value);
  document.getElementById('tcImageScale').oninput=e=>edit(s=>s.background.imageScale=e.target.value);
  document.getElementById('tcImageX').oninput=e=>edit(s=>s.background.positionX=e.target.value);
  document.getElementById('tcImageY').oninput=e=>edit(s=>s.background.positionY=e.target.value);
  document.getElementById('tcPreviewZoom').oninput=e=>{previewZoom=+e.target.value;document.getElementById('tcPreviewZoomOut').value=previewZoom+'%';renderPreview();};
  document.getElementById('tcAppearanceCancel').onclick=()=>{draft=clone(committed);sessionDirty=false;syncControls();applyCurrent();setStatus('已撤销未应用的外观修改','ok');};
  document.getElementById('tcAppearanceApply').onclick=async()=>{try{
    const previous=clone(committed),next=normalize(draft),before=JSON.stringify(previous.background),backgroundChanged=before!==JSON.stringify(next.background),historyBefore=backgroundChanged&&window.FileHistory?window.FileHistory.capture():null;
    await persist(next);committed=next;draft=clone(next);sessionDirty=false;documentBackgroundExplicit=true;appDefaultBackground=clone(next.background);documentAppearance={background:clone(next.background)};
    applyCurrent();if(typeof layout==='function')layout();if(backgroundChanged&&typeof markDirty==='function'){markDirty(false);if(historyBefore&&window.FileHistory)window.FileHistory.commit(historyBefore);}
    setStatus('原版配色已保存；背景已写入当前导图','ok');
  }catch(e){draft=clone(committed);sessionDirty=false;applyCurrent();syncControls();setStatus(e.message||'保存失败，已回滚预览','err');}};
  window.addEventListener('resize',renderPreview);
}
function onSettingsOpened(){beginEdit();}
function onSettingsClosed(){if(editingSession){if(sessionDirty){draft=clone(committed);applyCurrent();}editingSession=false;sessionDirty=false;}}
function clampWorldPoint(x,y){
  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited')return{x,y};
  const q=canvasBounds(committed),m=24;return{x:clamp(x,q.left+m,q.right-m),y:clamp(y,q.top+m,q.bottom-m)};
}
function constrainLayout(){
  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited'||!roots||!roots.length)return false;
  if(constrainGuard){constrainGuard=false;return false;}
  let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
  for(const id in TB){const t=TB[id];if(t._ghost||!Number.isFinite(t._x)||!Number.isFinite(t._cy))continue;minX=Math.min(minX,t._x);minY=Math.min(minY,t._cy-t._h/2);maxX=Math.max(maxX,t._x+t._w);maxY=Math.max(maxY,t._cy+t._h/2);}
  if(!Number.isFinite(minX))return false;const q=canvasBounds(committed),m=24,L=q.left+m,R=q.right-m,T=q.top+m,B=q.bottom-m;
  let dx=0,dy=0;if(maxX-minX<=R-L){if(minX<L)dx=L-minX;else if(maxX>R)dx=R-maxX;}else dx=L-minX;
  if(maxY-minY<=B-T){if(minY<T)dy=T-minY;else if(maxY>B)dy=B-maxY;}else dy=T-minY;
  if(Math.abs(dx)<.01&&Math.abs(dy)<.01)return false;
  roots.forEach(id=>{const t=TB[id];if(t){t.x=(Number(t.x)||0)+dx;t.y=(Number(t.y)||0)+dy;}});constrainGuard=true;return true;
}
function fitLimitedCanvas(){
  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited')return false;
  const q=canvasBounds(committed),pad=48;scale=Math.max(.3,Math.min(2.5,(canvas.clientWidth-pad*2)/q.width,(canvas.clientHeight-pad*2)/q.height));
  panX=canvas.clientWidth/2-CANVAS_CENTER.x*scale;panY=canvas.clientHeight/2-CANVAS_CENTER.y*scale;applyTransform();return true;
}

function loadDocumentAppearance(value){
  const hasExplicit=!!(value&&value.background&&typeof value.background==='object');
  const next=hasExplicit?normalize({background:value.background}).background:clone(appDefaultBackground);
  documentBackgroundExplicit=hasExplicit;
  documentAppearance={background:clone(next)};committed.background=clone(next);
  if(editingSession)draft=clone(committed);applyCurrent();syncControls();
}
function getDocumentAppearance(){return {background:clone(committed.background)};}

committed=defaultState();draft=clone(committed);buildPanel();ensureLayers();initialized=true;restore();
document.addEventListener('knotjot:settings-opened',onSettingsOpened);
document.addEventListener('knotjot:settings-closed',onSettingsClosed);
document.addEventListener('knotjot:ui-skin-applied',()=>{applyCurrent();syncControls(false);});
document.addEventListener('knotjot:scheme-applied',e=>applySchemeToState(e.detail&&e.detail.scheme));
window.AppearanceEditor={applyCurrent,updateCanvasTransform,cancelEdit,clampWorldPoint,constrainLayout,fitLimitedCanvas,loadDocumentAppearance,getDocumentAppearance,
  getDefaultBackground:()=>clone(appDefaultBackground),getState:()=>clone(committed),getDraft:()=>clone(activeState()),normalizeForTest:normalize,canvasBounds:()=>canvasBounds(committed)};
})();
