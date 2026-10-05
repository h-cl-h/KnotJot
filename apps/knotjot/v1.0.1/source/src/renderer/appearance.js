(function(){
/* EN: Callback for function(){ 'use strict'; const FORMAT='knotjot-appearance'; const STORAGE_KEY='bmap.appearance.v1'; const CAN: call JSON.parse, JSON.stringify, defaultState; update committed, draft, initialized.
   ZH: function(){ 'use strict'; const FORMAT='knotjot-appearance'; const STORAGE_KEY='bmap.appearance.v1'; const CAN 的回调：调用 JSON.parse、JSON.stringify、defaultState；更新 committed、draft、initialized。 */

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
// EN: Keep an optional sheet color separate from application palette defaults and retain original image bytes.
// ZH: 将可选画布底色与应用配色默认值分离，并保留原始图片字节。
const DEFAULT_BG={enabled:false,mode:'unlimited',color:'',imageData:'',imageName:'',imageWidth:0,imageHeight:0,
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

const clone=o=>/* EN: clone in module: call JSON.parse, JSON.stringify; return JSON.parse(JSON.stringify(o)). ZH: module 中的 clone：调用 JSON.parse、JSON.stringify；返回 JSON.parse(JSON.stringify(o))。 */ JSON.parse(JSON.stringify(o));
const clamp=(n,a,b)=>/* EN: Restrict a numeric value to the supplied lower and upper limits. ZH: 将数值限制在给定的下界与上界之间。 */ Math.min(b,Math.max(a,Number(n)||0));
const nonNegativeInt=v=>{
/* EN: nonNegativeInt in module: call Number.isFinite, Math.round.
   ZH: module 中的 nonNegativeInt：调用 Number.isFinite、Math.round。 */
const n=Number(v);return Number.isFinite(n)&&n>0?Math.round(n):0;};
const validHex=v=>/* EN: Check whether the supplied string has the accepted hexadecimal color shape. ZH: 检查给定字符串是否符合所接受的十六进制颜色格式。 */ /^#[0-9a-f]{6}$/i.test(String(v||''));
function rgbToHex(v){
/* EN: Convert numeric color components into a hexadecimal CSS color.
   ZH: 将数值颜色分量转换为 CSS 十六进制颜色。 */

  if(validHex(v)) return String(v).toLowerCase();
  const m=String(v||'').match(/rgba?\(\s*(\d+)[, ]+\s*(\d+)[, ]+\s*(\d+)/i);
  if(!m) return '';
  return '#'+[m[1],m[2],m[3]].map(x=>/* EN: Derive the next value for [m[1],m[2],m[3]].map: call Math.min(255,+x).toString(16).padStart, Math.min(255,+x).toString; return Math.min(255,+x).toString(16).padStart(2,'0'). ZH: 计算 [m[1],m[2],m[3]].map 的下一项结果：调用 Math.min(255,+x).toString(16).padStart、Math.min(255,+x).toString；返回 Math.min(255,+x).toString(16).padStart(2,'0')。 */ Math.min(255,+x).toString(16).padStart(2,'0')).join('');
}
function gradientParts(v,a,b){
/* EN: gradientParts in module: call String(v||'').match, validHex, hits[0].toLowerCase.
   ZH: module 中的 gradientParts：调用 String(v||'').match、validHex、hits[0].toLowerCase。 */

  const hits=String(v||'').match(/#[0-9a-f]{6}/ig)||[];
  return [validHex(hits[0])?hits[0].toLowerCase():a,validHex(hits[1])?hits[1].toLowerCase():b];
}
function seedPalette(){
/* EN: Read, apply or clear the default UI palette through CSS custom properties.
   ZH: 通过 CSS 自定义属性读取、应用或清除默认界面配色。 */

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
function defaultState(){
/* EN: defaultState in module: call seedPalette, clone.
   ZH: module 中的 defaultState：调用 seedPalette、clone。 */
 return {app:FORMAT,version:1,palette:seedPalette(),background:clone(DEFAULT_BG)}; }
function normalize(input){
/* EN: Normalize optional sheet color and fractional image scale without cropping or re-encoding imported images.
   ZH: 规范可选画布底色及小数图片比例，不裁剪或重新编码导入图片。 */
/* EN: normalize in module: call defaultState, validHex, String(src[key]).toLowerCase; update base.palette[key], base.background.enabled, base.background.mode.
   ZH: module 中的 normalize：调用 defaultState、validHex、String(src[key]).toLowerCase；更新 base.palette[key]、base.background.enabled、base.background.mode。 */

  const base=defaultState(), o=input&&typeof input==='object'?input:{};
  const src=o.palette&&typeof o.palette==='object'?o.palette:{};
  for(const [key] of PALETTE_FIELDS) base.palette[key]=validHex(src[key])?String(src[key]).toLowerCase():base.palette[key];
  const b=o.background&&typeof o.background==='object'?o.background:{};
  base.background.enabled=!!b.enabled;
  base.background.mode=b.mode==='limited'?'limited':'unlimited';
  base.background.color=validHex(b.color)?String(b.color).toLowerCase():'';
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
  base.background.imageScale=clamp(b.imageScale||100,.01,2400000);
  base.background.positionX=Math.round(clamp(b.positionX,-100,100));
  base.background.positionY=Math.round(clamp(b.positionY,-100,100));
  return base;
}
function activeState(){
/* EN: activeState in module: return editingSession&&draft?draft:committed.
   ZH: module 中的 activeState：返回 editingSession&&draft?draft:committed。 */
 return editingSession&&draft?draft:committed; }
function documentBackground(){
/* EN: documentBackground in module: call normalize, clone.
   ZH: module 中的 documentBackground：调用 normalize、clone。 */

  try{return documentAppearance&&documentAppearance.background&&typeof documentAppearance.background==='object'
    ?normalize({background:documentAppearance.background}).background:clone(appDefaultBackground);}catch(_){return clone(appDefaultBackground);}
}
function isDefaultUI(){
/* EN: isDefaultUI in module: call document.documentElement.getAttribute.
   ZH: module 中的 isDefaultUI：调用 document.documentElement.getAttribute。 */
 return document.documentElement.getAttribute('data-uiskin')==='default'; }
function canvasBounds(state=committed){
/* EN: Calculate finite-canvas bounds and keep layout/view operations within the configured canvas.
   ZH: 计算有限画布边界，使布局和视图操作符合设定画布范围。 */

  const b=(state&&state.background)||DEFAULT_BG;
  return {left:CANVAS_CENTER.x-b.canvasWidth/2,top:CANVAS_CENTER.y-b.canvasHeight/2,width:b.canvasWidth,height:b.canvasHeight,
    right:CANVAS_CENTER.x+b.canvasWidth/2,bottom:CANVAS_CENTER.y+b.canvasHeight/2};
}
function setRootVar(name,value){
/* EN: setRootVar in module: call document.documentElement.style.setProperty.
   ZH: module 中的 setRootVar：调用 document.documentElement.style.setProperty。 */
 document.documentElement.style.setProperty(name,value); }
function applyPalette(p){
/* EN: Read, apply or clear the default UI palette through CSS custom properties.
   ZH: 通过 CSS 自定义属性读取、应用或清除默认界面配色。 */

  for(const key in SIMPLE_TOKENS) setRootVar(SIMPLE_TOKENS[key],p[key]);
  setRootVar('--side-grad',`linear-gradient(160deg,${p.startSideA},${p.startSideB})`);
  setRootVar('--thumb-blank',`linear-gradient(135deg,${p.thumbBlankA},${p.thumbBlankB})`);
  setRootVar('--thumb-sample',`linear-gradient(135deg,${p.thumbSampleA},${p.thumbSampleB})`);
  setRootVar('--thumb-open',`linear-gradient(135deg,${p.thumbOpenA},${p.thumbOpenB})`);
}
function clearPalette(){
/* EN: Read, apply or clear the default UI palette through CSS custom properties.
   ZH: 通过 CSS 自定义属性读取、应用或清除默认界面配色。 */

  Object.values(SIMPLE_TOKENS).concat(['--side-grad','--thumb-blank','--thumb-sample','--thumb-open']).forEach(x=>/* EN: Process each item in Object.values(SIMPLE_TOKENS).concat(['--side-grad','--thumb-blank','--thumb-sample','--thumb-open']).forEach: call document.documentElement.style.removeProperty; return document.documentElement.style.removeProperty(x). ZH: 逐项处理 Object.values(SIMPLE_TOKENS).concat(['--side-grad','--thumb-blank','--thumb-sample','--thumb-open']).forEach 中的元素：调用 document.documentElement.style.removeProperty；返回 document.documentElement.style.removeProperty(x)。 */ document.documentElement.style.removeProperty(x));
}
function ensureLayers(){
/* EN: ensureLayers in module: call document.getElementById, document.createElement, canvas.insertBefore; update unlimited, unlimited.id, limited.
   ZH: module 中的 ensureLayers：调用 document.getElementById、document.createElement、canvas.insertBefore；更新 unlimited、unlimited.id、limited。 */

  let unlimited=document.getElementById('tcCanvasUnlimited');
  if(!unlimited){ unlimited=document.createElement('div');unlimited.id='tcCanvasUnlimited';canvas.insertBefore(unlimited,world); }
  let limited=document.getElementById('tcCanvasLimited');
  if(!limited){ limited=document.createElement('div');limited.id='tcCanvasLimited';limited.innerHTML='<div id="tcCanvasLimitedGrid"></div>';world.insertBefore(limited,world.firstChild); }
  return {unlimited,limited};
}
function updateCanvasTransform(){
/* EN: Paint the active sheet color directly on its canvas layers, including under non-default skins.
   ZH: 将活动画布底色直接应用于画布图层，使其在非默认皮肤下也生效。 */
/* EN: updateCanvasTransform in module: call activeState, ensureLayers, canvas.classList.toggle; update canvas.style.backgroundImage, canvas.style.backgroundSize, canvas.style.backgroundPosition.
   ZH: module 中的 updateCanvasTransform：调用 activeState、ensureLayers、canvas.classList.toggle；更新 canvas.style.backgroundImage、canvas.style.backgroundSize、canvas.style.backgroundPosition。 */

  if(!committed) return;
  const s=activeState(), b=s.background, layers=ensureLayers();
  const color=b.color||s.palette.bg;
  canvas.style.backgroundColor=color;layers.limited.style.backgroundColor=color;
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
    const iw=b.imageWidth*b.imageScale/100*scale, ih=b.imageHeight*b.imageScale/100*scale;
    layers.unlimited.style.display='block';layers.unlimited.style.backgroundColor=color;layers.unlimited.style.backgroundImage=`url("${b.imageData}")`;
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
/* EN: applyCurrent in module: call isDefaultUI, applyPalette, activeState.
   ZH: module 中的 applyCurrent：调用 isDefaultUI、applyPalette、activeState。 */

  if(!committed) return;
  if(isDefaultUI()) applyPalette(activeState().palette); else clearPalette();
  updateCanvasTransform();
  renderLockState();renderPreview();
}
function setStatus(text,kind=''){
/* EN: setStatus in module: call document.getElementById, window.t, clearTimeout; update text, el.textContent, el.className.
   ZH: module 中的 setStatus：调用 document.getElementById、window.t、clearTimeout；更新 text、el.textContent、el.className。 */

  const el=document.getElementById('tcAppearanceStatus');if(!el)return;
  if(typeof window.t==='function')text=window.t(text);
  el.textContent=text;el.className='tc-app-status '+kind;
  clearTimeout(statusTimer);if(text&&kind==='ok')statusTimer=setTimeout(()=>{
/* EN: Callback for setTimeout: update el.textContent.
   ZH: setTimeout 的回调：更新 el.textContent。 */
if(el.textContent===text)el.textContent='';},5000);
}
function safeLocalSave(state){
/* EN: safeLocalSave in module: call localStorage.setItem, JSON.stringify, clone; update small.background.imageData, small.background.imageName, small.background.imageWidth.
   ZH: module 中的 safeLocalSave：调用 localStorage.setItem、JSON.stringify、clone；更新 small.background.imageData、small.background.imageName、small.background.imageWidth。 */

  try{localStorage.setItem(STORAGE_KEY,JSON.stringify(state));return true;}catch(_){
    try{const small=clone(state);small.background.imageData='';small.background.imageName='';small.background.imageWidth=0;small.background.imageHeight=0;localStorage.setItem(STORAGE_KEY,JSON.stringify(small));}catch(__){}
    return false;
  }
}
async function persist(state=committed){
/* EN: persist in module: call clone, JSON.stringify, window.api.saveAppearance; update localOk.
   ZH: module 中的 persist：调用 clone、JSON.stringify、window.api.saveAppearance；更新 localOk。 */

  const globalState={app:FORMAT,version:1,palette:clone(state.palette),background:clone(state.background)};
  const json=JSON.stringify(globalState);let localOk=true;
  if(window.api&&window.api.saveAppearance){
    const r=await window.api.saveAppearance(json);if(!r||!r.ok)throw new Error((r&&r.error)||'外观设置保存失败');
    localOk=safeLocalSave(globalState);
  }else{localOk=safeLocalSave(globalState);if(!localOk&&state.background.imageData)throw new Error('浏览器存储空间不足，背景图片未能持久化');}
  return {ok:true,localOk};
}
function parseStored(text){
/* EN: parseStored in module: call JSON.parse, normalize.
   ZH: module 中的 parseStored：调用 JSON.parse、normalize。 */
 try{const o=JSON.parse(text);return o&&o.app===FORMAT?normalize(o):null;}catch(_){return null;} }
async function restore(){
/* EN: restore in module: call parseStored, localStorage.getItem, clone; update committed.palette, appDefaultBackground, committed.background.
   ZH: module 中的 restore：调用 parseStored、localStorage.getItem、clone；更新 committed.palette、appDefaultBackground、committed.background。 */

  let found=parseStored(localStorage.getItem(STORAGE_KEY)||'');if(found){committed.palette=found.palette;appDefaultBackground=clone(found.background);}
  committed.background=documentBackground();documentAppearance={background:clone(committed.background)};
  applyCurrent();
  try{
    if(window.api&&window.api.loadAppearance){const txt=await window.api.loadAppearance();const fileState=parseStored(txt||'');if(fileState){committed.palette=fileState.palette;appDefaultBackground=clone(fileState.background);
      if(!documentBackgroundExplicit){committed.background=clone(appDefaultBackground);documentAppearance={background:clone(committed.background)};}
      safeLocalSave({app:FORMAT,version:1,palette:clone(committed.palette),background:clone(appDefaultBackground)});applyCurrent();syncControls(false);}}
  }catch(_){ }
}
function paletteMarkup(){
/* EN: paletteMarkup in module: call PALETTE_FIELDS.map(([key,label])=>{const x=typeof window.t==='function'?window.t(label):label;return `<label c, PALETTE_FIELDS.map.
   ZH: module 中的 paletteMarkup：调用 PALETTE_FIELDS.map(([key,label])=>{const x=typeof window.t==='function'?window.t(label):label;return `<label c、PALETTE_FIELDS.map。 */
return PALETTE_FIELDS.map(([key,label])=>{
/* EN: Derive the next value for PALETTE_FIELDS.map: call window.t.
   ZH: 计算 PALETTE_FIELDS.map 的下一项结果：调用 window.t。 */
const x=typeof window.t==='function'?window.t(label):label;return `<label class="tc-app-color"><input type="color" data-palette="${key}" value="${FALLBACK_PALETTE[key]}"><span title="${x}">${x}</span></label>`;}).join('');}
function buildPanel(){
/* EN: Expose sheet-specific background color and scale controls; keep palette controls application-wide.
   ZH: 提供画布专属背景底色和缩放控件；配色控件仍属于整个应用。 */
/* EN: buildPanel in module: call document.getElementById, document.createElement, paletteMarkup; update panel.id, panel.innerHTML.
   ZH: module 中的 buildPanel：调用 document.getElementById、document.createElement、paletteMarkup；更新 panel.id、panel.innerHTML。 */

  const pane=document.getElementById('paneAppearance');if(!pane||document.getElementById('tcAppearancePanel'))return;
  const panel=document.createElement('div');panel.id='tcAppearancePanel';panel.innerHTML=`
    <div class="tc-app-lock">当前不是原版 UI，原版配色暂不生效；导图背景与有限画布仍可独立编辑。<br><button class="tc-app-mini" id="tcSwitchDefault" type="button">切回原版 UI 编辑配色</button></div>
    <div class="tc-app-editable">
      <div class="tc-app-head"><span>原版 UI 配色</span><span class="spacer"></span><button class="tc-app-mini" id="tcPaletteExport" type="button">导出 .knotjot-theme</button><button class="tc-app-mini" id="tcPaletteReset" type="button">恢复原版颜色</button></div>
      <div class="tc-app-palette">${paletteMarkup()}</div>
      <div class="tc-app-head" id="tcBackgroundHeading"><span>思维导图背景</span><span class="spacer"></span><label class="tc-bg-enable"><input id="tcBgEnabled" type="checkbox">启用</label></div>
      <div class="tc-bg-toolbar"><button class="btn" id="tcBgImport" type="button">🖼 导入图片并预览</button><button class="tc-app-mini" id="tcBgClear" type="button">清除图片</button><span class="tc-bg-name" id="tcBgName">未导入图片</span></div>
      <input id="tcBgFile" type="file" accept="image/png,image/jpeg,image/webp,.png,.jpg,.jpeg,.webp" hidden>
      <div class="tc-bg-modes">
        <label class="tc-bg-mode"><input type="radio" name="tcBgMode" value="unlimited"><span><b>不限制画布大小</b>图片横向、纵向连续复制，画布仍可无限平移。</span></label>
        <label class="tc-bg-mode"><input type="radio" name="tcBgMode" value="limited"><span><b>限制画布大小</b>建立有限边界，默认 6000 × 3600，图片在边界内缩放和定位。</span></label>
      </div>
      <div class="tc-bg-preview-shell">
        <div class="tc-bg-preview-head"><span id="tcPreviewSize">预览</span><span class="spacer"></span><span>预览缩放</span><input id="tcPreviewZoom" type="range" min="25" max="800" step="25" value="100"><output id="tcPreviewZoomOut">100%</output></div>
        <div class="tc-bg-preview" id="tcBgPreview"><div class="tc-bg-preview-image" id="tcBgPreviewImage"><div class="tc-bg-preview-grid"></div></div><div class="tc-bg-reference" title="空白的原版默认文本框（实际比例）"><div class="card"><div class="card-inner empty"></div></div></div></div>
      </div>
      <div class="tc-bg-controls">
        <label class="tc-bg-control"><span>当前画布底色</span><input id="tcBackgroundColor" type="color"><output></output></label>
        <label class="tc-bg-control tc-bg-limited-only"><span>画布宽度</span><input id="tcCanvasWidth" type="number" min="640" max="24000" step="10"><output>px</output></label>
        <label class="tc-bg-control tc-bg-limited-only"><span>画布高度</span><input id="tcCanvasHeight" type="number" min="480" max="16000" step="10"><output>px</output></label>
        <label class="tc-bg-control"><span>图片缩放</span><input id="tcImageScale" type="range" min="0.01" max="400" step="0.01"><output id="tcImageScaleOut"></output></label>
        <label class="tc-bg-control"><span>水平位置</span><input id="tcImageX" type="range" min="-100" max="100" step="1"><output id="tcImageXOut"></output></label>
        <label class="tc-bg-control"><span>垂直位置</span><input id="tcImageY" type="range" min="-100" max="100" step="1"><output id="tcImageYOut"></output></label>
      </div>
      <div class="tc-app-actions"><button class="tc-app-mini" id="tcAppearanceCancel" type="button">撤销未应用修改</button><span class="spacer"></span><button class="btn primary" id="tcAppearanceApply" type="button">应用并保存</button></div>
      <div class="tc-app-status" id="tcAppearanceStatus"></div>
    </div>`;
  pane.appendChild(panel);bindPanel();if(typeof window.translateNode==='function')window.translateNode(panel);
}
function beginEdit(){
/* EN: Manage a reversible appearance-editing session using committed and draft state.
   ZH: 使用已提交状态和草稿管理可取消的外观编辑会话。 */

  if(!committed)return;draft=clone(committed);editingSession=true;sessionDirty=false;previewZoom=100;syncControls();applyCurrent();
}
function cancelEdit(silent=false){
/* EN: Manage a reversible appearance-editing session using committed and draft state.
   ZH: 使用已提交状态和草稿管理可取消的外观编辑会话。 */

  if(!editingSession)return;draft=clone(committed);sessionDirty=false;editingSession=false;applyCurrent();if(!silent)setStatus('已撤销未应用的外观修改','ok');
}
function edit(mutator){
/* EN: edit in module: call beginEdit, mutator, normalize; update draft, sessionDirty.
   ZH: module 中的 edit：调用 beginEdit、mutator、normalize；更新 draft、sessionDirty。 */
if(!editingSession)beginEdit();mutator(draft);draft=normalize(draft);sessionDirty=true;syncControls(false);applyCurrent();}
function syncControls(all=true){
/* EN: Reflect active sheet color and fitted fractional scale without clamping large upscales in the range control.
   ZH: 显示活动画布底色和适配后的小数缩放，避免滑块截断较大的放大比例。 */
/* EN: syncControls in module: call document.getElementById, panel.querySelectorAll('[data-palette]').forEach, panel.querySelectorAll; update document.getElementById('tcBgEnabled').checked, document.getElementById('tcBgName').textContent, document.getElementById('tcCanvasWidth').value.
   ZH: module 中的 syncControls：调用 document.getElementById、panel.querySelectorAll('[data-palette]').forEach、panel.querySelectorAll；更新 document.getElementById('tcBgEnabled').checked、document.getElementById('tcBgName').textContent、document.getElementById('tcCanvasWidth').value。 */

  if(!draft)return;const p=draft.palette,b=draft.background,panel=document.getElementById('tcAppearancePanel');if(!panel)return;
  if(all) panel.querySelectorAll('[data-palette]').forEach(x=>/* EN: Process each item in panel.querySelectorAll('[data-palette]').forEach: update x.value; return x.value=p[x.dataset.palette]. ZH: 逐项处理 panel.querySelectorAll('[data-palette]').forEach 中的元素：更新 x.value；返回 x.value=p[x.dataset.palette]。 */ x.value=p[x.dataset.palette]);
  document.getElementById('tcBgEnabled').checked=b.enabled;
  document.getElementById('tcBackgroundColor').value=b.color||p.bg;
  panel.querySelectorAll('input[name="tcBgMode"]').forEach(x=>/* EN: Process each item in panel.querySelectorAll('input[name="tcBgMode"]').forEach: update x.checked; return x.checked=x.value===b.mode. ZH: 逐项处理 panel.querySelectorAll('input[name="tcBgMode"]').forEach 中的元素：更新 x.checked；返回 x.checked=x.value===b.mode。 */ x.checked=x.value===b.mode);
  panel.classList.toggle('mode-limited',b.mode==='limited');
  document.getElementById('tcBgName').textContent=b.imageName?`${b.imageName} · ${b.imageWidth}×${b.imageHeight}`:(typeof window.t==='function'?window.t('未导入图片'):'未导入图片');
  document.getElementById('tcCanvasWidth').value=b.canvasWidth;document.getElementById('tcCanvasHeight').value=b.canvasHeight;
  document.getElementById('tcImageScale').max=Math.max(400,b.imageScale);document.getElementById('tcImageScale').value=b.imageScale;document.getElementById('tcImageScaleOut').value=Number(b.imageScale.toFixed(2))+'%';
  document.getElementById('tcImageX').value=b.positionX;document.getElementById('tcImageXOut').value=(b.positionX>0?'+':'')+b.positionX+'%';
  document.getElementById('tcImageY').value=b.positionY;document.getElementById('tcImageYOut').value=(b.positionY>0?'+':'')+b.positionY+'%';
  document.getElementById('tcPreviewZoom').value=previewZoom;document.getElementById('tcPreviewZoomOut').value=previewZoom+'%';
  renderPreview();renderLockState();
}
function renderLockState(){
/* EN: renderLockState in module: call document.getElementById, panel.classList.toggle, isDefaultUI.
   ZH: module 中的 renderLockState：调用 document.getElementById、panel.classList.toggle、isDefaultUI。 */

  const panel=document.getElementById('tcAppearancePanel');if(panel)panel.classList.toggle('palette-locked',!isDefaultUI());
}
function renderPreview(){
/* EN: Use the same world-to-preview factor for image, grid and default node so relative proportions match the canvas.
   ZH: 图片、网格与默认节点使用同一世界坐标到预览的缩放因子，使相对比例符合真实画布。 */
/* EN: renderPreview in module: call document.getElementById, box.querySelector, box.classList.toggle; update img.style.backgroundImage, img.style.backgroundColor, img.style.left.
   ZH: module 中的 renderPreview：调用 document.getElementById、box.querySelector、box.classList.toggle；更新 img.style.backgroundImage、img.style.backgroundColor、img.style.left。 */

  const box=document.getElementById('tcBgPreview'),img=document.getElementById('tcBgPreviewImage');if(!box||!draft)return;
  const b=draft.background, ref=box.querySelector('.tc-bg-reference'), W=box.clientWidth||520,H=box.clientHeight||210;
  box.classList.toggle('unlimited',b.mode==='unlimited');box.classList.toggle('has-image',!!b.imageData);img.style.backgroundImage=b.imageData?`url("${b.imageData}")`:'none';
  img.style.backgroundColor=b.color||draft.palette.bg;
  const factor=b.mode==='limited'?Math.min((W-20)/b.canvasWidth,(H-20)/b.canvasHeight)*previewZoom/100:.2*previewZoom/100;
  ref.style.transform=`translate(-50%,-50%) scale(${factor})`;
  img.querySelector('.tc-bg-preview-grid').style.backgroundSize=(26*factor)+'px '+(26*factor)+'px';
  if(b.mode==='limited'){
    const fit=Math.min((W-20)/b.canvasWidth,(H-20)/b.canvasHeight)*previewZoom/100;
    const rw=b.canvasWidth*fit,rh=b.canvasHeight*fit,iw=b.imageWidth*b.imageScale/100*fit,ih=b.imageHeight*b.imageScale/100*fit;
    img.style.left=(W-rw)/2+'px';img.style.top=(H-rh)/2+'px';img.style.width=rw+'px';img.style.height=rh+'px';
    img.style.backgroundRepeat='no-repeat';img.style.backgroundSize=iw+'px '+ih+'px';
    img.style.backgroundPosition=`calc(50% + ${b.positionX/100*rw/2}px) calc(50% + ${b.positionY/100*rh/2}px)`;
    document.getElementById('tcPreviewSize').textContent=`${b.canvasWidth} × ${b.canvasHeight} · ${typeof window.t==='function'?window.t('预览'):'预览'} ${Math.round(fit*100)}%`;
  }else{
    const unit=factor,iw=b.imageWidth*b.imageScale/100*unit,ih=b.imageHeight*b.imageScale/100*unit;
    img.style.left='0';img.style.top='0';img.style.width='100%';img.style.height='100%';img.style.backgroundRepeat='repeat';
    img.style.backgroundSize=iw+'px '+ih+'px';img.style.backgroundPosition=(b.positionX/100*iw)+'px '+(b.positionY/100*ih)+'px';
    document.getElementById('tcPreviewSize').textContent=typeof window.t==='function'?window.t('无限平铺预览'):'无限平铺预览';
  }
}
function decodeImage(file){
/* EN: Validate and load an image for the document background, preserving dimensions and data URL.
   ZH: 验证并加载文档背景图片，保存尺寸与数据 URL。 */
return new Promise((resolve,reject)=>{
/* EN: Callback for decodeImage: call URL.createObjectURL; update im.onload, im.onerror, im.src.
   ZH: decodeImage 的回调：调用 URL.createObjectURL；更新 im.onload、im.onerror、im.src。 */
const url=URL.createObjectURL(file),im=new Image();im.onload=()=>{
/* EN: im.onload in module: call URL.revokeObjectURL, resolve.
   ZH: module 中的 im.onload：调用 URL.revokeObjectURL、resolve。 */
const r={width:im.naturalWidth,height:im.naturalHeight};URL.revokeObjectURL(url);resolve(r);};im.onerror=()=>{
/* EN: im.onerror in module: call URL.revokeObjectURL, reject.
   ZH: module 中的 im.onerror：调用 URL.revokeObjectURL、reject。 */
URL.revokeObjectURL(url);reject(new Error('图片无法解码'));};im.src=url;});}
function readDataURL(file){
/* EN: Validate and load an image for the document background, preserving dimensions and data URL.
   ZH: 验证并加载文档背景图片，保存尺寸与数据 URL。 */
return new Promise((resolve,reject)=>{
/* EN: Callback for readDataURL: call rd.readAsDataURL; update rd.onload, rd.onerror.
   ZH: readDataURL 的回调：调用 rd.readAsDataURL；更新 rd.onload、rd.onerror。 */
const rd=new FileReader();rd.onload=()=>/* EN: rd.onload in module: call resolve; return resolve(String(rd.result||'')). ZH: module 中的 rd.onload：调用 resolve；返回 resolve(String(rd.result||''))。 */ resolve(String(rd.result||''));rd.onerror=()=>/* EN: rd.onerror in module: call reject; return reject(new Error('图片读取失败')). ZH: module 中的 rd.onerror：调用 reject；返回 reject(new Error('图片读取失败'))。 */ reject(new Error('图片读取失败'));rd.readAsDataURL(file);});}
async function importBackground(file){
/* EN: Fit each newly imported image inside finite canvas bounds, center it, and preserve its original dimensions/data.
   ZH: 每次导入图片时按有限画布边界等比适配并居中，同时保留原始尺寸与数据。 */
/* EN: Validate and load an image for the document background, preserving dimensions and data URL.
   ZH: 验证并加载文档背景图片，保存尺寸与数据 URL。 */

  if(!file)return;const owner=sheets[curSheet],okType=/\.(png|jpe?g|webp)$/i.test(file.name)||['image/png','image/jpeg','image/webp'].includes(file.type);
  if(!okType){setStatus('仅支持 PNG、JPG/JPEG、WebP','err');return;}
  try{
    if(file.size>KnotJotSecurity.LIMITS.imageBytes)throw new Error('背景图片文件超过 12 MB 上限');
    const meta=KnotJotSecurity.validateImageDimensions(KnotJotSecurity.dimensionsFromBytes(await file.slice(0,512*1024).arrayBuffer()));
    const dim=await decodeImage(file);
    if(dim.width!==meta.width||dim.height!==meta.height)throw new Error('图片元数据与解码尺寸不一致');
    const data=await readDataURL(file);
    if(sheets[curSheet]!==owner)return;
    edit(s=>{
/* EN: Callback for edit: complete without changing state.
   ZH: edit 的回调：完成且不修改状态。 */
/* EN: Reset offsets and derive a contain scale only for finite mode; repeated backgrounds start at native scale.
   ZH: 重置偏移，仅在有限模式计算完整包含比例；平铺背景从原始比例开始。 */
Object.assign(s.background,{enabled:true,imageData:data,imageName:file.name,imageWidth:dim.width,imageHeight:dim.height,
  imageScale:s.background.mode==='limited'?Math.min(s.background.canvasWidth/dim.width,s.background.canvasHeight/dim.height)*100:100,positionX:0,positionY:0});});
    setStatus('图片已载入预览；确认效果后点击“应用并保存”','ok');
  }catch(e){setStatus(e.message||'图片导入失败','err');}
}
function exportTheme(){
/* EN: exportTheme in module: call document.createElement, URL.createObjectURL, JSON.stringify; update a.href, a.download.
   ZH: module 中的 exportTheme：调用 document.createElement、URL.createObjectURL、JSON.stringify；更新 a.href、a.download。 */

  const p=(draft||committed).palette,o={app:'brace-mindmap-theme',version:2,name:'原版自定义配色',accent:p.accent,accentSoft:p.accentSoft,
    sideGrad:`160deg,${p.startSideA},${p.startSideB}`,thumbBlank:`135deg,${p.thumbBlankA},${p.thumbBlankB}`,
    thumbSample:`135deg,${p.thumbSampleA},${p.thumbSampleB}`,thumbOpen:`135deg,${p.thumbOpenA},${p.thumbOpenB}`,
    tokens:{bg:p.bg,grid:p.grid,card:p.card,cardBorder:p.cardBorder,ink:p.ink,muted:p.muted,toolbarBg:p.toolbarBg,menuBg:p.menuBg,menuHover:p.menuHover,brace:p.brace,danger:p.danger}};
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(o,null,2)],{type:'application/json'}));a.download='KnotJot-Original-Theme.knotjot-theme';a.click();setTimeout(()=>/* EN: Callback for setTimeout: call URL.revokeObjectURL; return URL.revokeObjectURL(a.href). ZH: setTimeout 的回调：调用 URL.revokeObjectURL；返回 URL.revokeObjectURL(a.href)。 */ URL.revokeObjectURL(a.href),1000);
}
async function applySchemeToState(s){
/* EN: applySchemeToState in module: call isDefaultUI, clone, gradientParts; update p.accent, p.accentSoft, p.startSideA.
   ZH: module 中的 applySchemeToState：调用 isDefaultUI、clone、gradientParts；更新 p.accent、p.accentSoft、p.startSideA。 */

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
/* EN: Commit background changes through active sheet history; keep asynchronous preference saving from applying to a different sheet.
   ZH: 通过活动画布历史提交背景更改；避免异步偏好保存把更改应用到另一张画布。 */
/* EN: bindPanel in module: call document.getElementById, panel.querySelectorAll('[data-palette]').forEach, panel.querySelectorAll; update document.getElementById('tcSwitchDefault').onclick, document.getElementById('tcPaletteReset').onclick, document.getElementById('tcPaletteExport').onclick.
   ZH: module 中的 bindPanel：调用 document.getElementById、panel.querySelectorAll('[data-palette]').forEach、panel.querySelectorAll；更新 document.getElementById('tcSwitchDefault').onclick、document.getElementById('tcPaletteReset').onclick、document.getElementById('tcPaletteExport').onclick。 */

  const panel=document.getElementById('tcAppearancePanel');
  document.getElementById('tcBackgroundColor').oninput=e=>{
    /* EN: Preview the current sheet color independently of palette locking and background-image enablement.
       ZH: 独立预览当前画布底色，不受配色锁定和背景图片启用状态影响。 */
    edit(s=>{s.background.color=e.target.value;});
  };
  panel.querySelectorAll('[data-palette]').forEach(x=>/* EN: Process each item in panel.querySelectorAll('[data-palette]').forEach: call x.addEventListener. ZH: 逐项处理 panel.querySelectorAll('[data-palette]').forEach 中的元素：调用 x.addEventListener。 */ x.addEventListener('input',()=>/* EN: Handle input on x.addEventListener: call edit. ZH: 处理 x.addEventListener 的 input 事件或通道：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.palette[x.dataset.palette]; return s.palette[x.dataset.palette]=x.value. ZH: edit 的回调：更新 s.palette[x.dataset.palette]；返回 s.palette[x.dataset.palette]=x.value。 */ s.palette[x.dataset.palette]=x.value)));
  document.getElementById('tcSwitchDefault').onclick=()=>{
/* EN: document.getElementById('tcSwitchDefault').onclick in bindPanel: call applyUISkin.
   ZH: bindPanel 中的 document.getElementById('tcSwitchDefault').onclick：调用 applyUISkin。 */
if(typeof applyUISkin==='function')applyUISkin('default');};
  document.getElementById('tcPaletteReset').onclick=()=>/* EN: document.getElementById('tcPaletteReset').onclick in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcPaletteReset').onclick：调用 edit。 */ edit(s=>/* EN: Callback for edit: call clone; update s.palette; return s.palette=clone(FALLBACK_PALETTE). ZH: edit 的回调：调用 clone；更新 s.palette；返回 s.palette=clone(FALLBACK_PALETTE)。 */ s.palette=clone(FALLBACK_PALETTE));
  document.getElementById('tcPaletteExport').onclick=exportTheme;
  document.getElementById('tcBgEnabled').onchange=e=>/* EN: document.getElementById('tcBgEnabled').onchange in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcBgEnabled').onchange：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.enabled; return s.background.enabled=e.target.checked. ZH: edit 的回调：更新 s.background.enabled；返回 s.background.enabled=e.target.checked。 */ s.background.enabled=e.target.checked);
  panel.querySelectorAll('input[name="tcBgMode"]').forEach(x=>/* EN: Process each item in panel.querySelectorAll('input[name="tcBgMode"]').forEach: update x.onchange. ZH: 逐项处理 panel.querySelectorAll('input[name="tcBgMode"]').forEach 中的元素：更新 x.onchange。 */ x.onchange=()=>{
/* EN: x.onchange in module: call edit.
   ZH: module 中的 x.onchange：调用 edit。 */
if(x.checked)edit(s=>/* EN: Callback for edit: update s.background.mode; return s.background.mode=x.value. ZH: edit 的回调：更新 s.background.mode；返回 s.background.mode=x.value。 */ s.background.mode=x.value);});
  document.getElementById('tcBgImport').onclick=()=>/* EN: document.getElementById('tcBgImport').onclick in bindPanel: call document.getElementById('tcBgFile').click, document.getElementById; return document.getElementById('tcBgFile').click(). ZH: bindPanel 中的 document.getElementById('tcBgImport').onclick：调用 document.getElementById('tcBgFile').click、document.getElementById；返回 document.getElementById('tcBgFile').click()。 */ document.getElementById('tcBgFile').click();
  document.getElementById('tcBgFile').onchange=e=>{
/* EN: document.getElementById('tcBgFile').onchange in bindPanel: call importBackground; update e.target.value.
   ZH: bindPanel 中的 document.getElementById('tcBgFile').onchange：调用 importBackground；更新 e.target.value。 */
importBackground(e.target.files&&e.target.files[0]);e.target.value='';};
  document.getElementById('tcBgClear').onclick=()=>/* EN: document.getElementById('tcBgClear').onclick in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcBgClear').onclick：调用 edit。 */ edit(s=>/* EN: Callback for edit: return Object.assign(s.background,{imageData:'',imageName:'',imageWidth:0,imageHeight:0}). ZH: edit 的回调：返回 Object.assign(s.background,{imageData:'',imageName:'',imageWidth:0,imageHeight:0})。 */ Object.assign(s.background,{imageData:'',imageName:'',imageWidth:0,imageHeight:0}));
  document.getElementById('tcCanvasWidth').oninput=e=>/* EN: document.getElementById('tcCanvasWidth').oninput in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcCanvasWidth').oninput：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.canvasWidth; return s.background.canvasWidth=e.target.value. ZH: edit 的回调：更新 s.background.canvasWidth；返回 s.background.canvasWidth=e.target.value。 */ s.background.canvasWidth=e.target.value);
  document.getElementById('tcCanvasHeight').oninput=e=>/* EN: document.getElementById('tcCanvasHeight').oninput in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcCanvasHeight').oninput：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.canvasHeight; return s.background.canvasHeight=e.target.value. ZH: edit 的回调：更新 s.background.canvasHeight；返回 s.background.canvasHeight=e.target.value。 */ s.background.canvasHeight=e.target.value);
  document.getElementById('tcImageScale').oninput=e=>/* EN: document.getElementById('tcImageScale').oninput in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcImageScale').oninput：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.imageScale; return s.background.imageScale=e.target.value. ZH: edit 的回调：更新 s.background.imageScale；返回 s.background.imageScale=e.target.value。 */ s.background.imageScale=e.target.value);
  document.getElementById('tcImageX').oninput=e=>/* EN: document.getElementById('tcImageX').oninput in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcImageX').oninput：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.positionX; return s.background.positionX=e.target.value. ZH: edit 的回调：更新 s.background.positionX；返回 s.background.positionX=e.target.value。 */ s.background.positionX=e.target.value);
  document.getElementById('tcImageY').oninput=e=>/* EN: document.getElementById('tcImageY').oninput in bindPanel: call edit. ZH: bindPanel 中的 document.getElementById('tcImageY').oninput：调用 edit。 */ edit(s=>/* EN: Callback for edit: update s.background.positionY; return s.background.positionY=e.target.value. ZH: edit 的回调：更新 s.background.positionY；返回 s.background.positionY=e.target.value。 */ s.background.positionY=e.target.value);
  document.getElementById('tcPreviewZoom').oninput=e=>{
/* EN: document.getElementById('tcPreviewZoom').oninput in bindPanel: call document.getElementById, renderPreview; update previewZoom, document.getElementById('tcPreviewZoomOut').value.
   ZH: bindPanel 中的 document.getElementById('tcPreviewZoom').oninput：调用 document.getElementById、renderPreview；更新 previewZoom、document.getElementById('tcPreviewZoomOut').value。 */
previewZoom=+e.target.value;document.getElementById('tcPreviewZoomOut').value=previewZoom+'%';renderPreview();};
  document.getElementById('tcAppearanceCancel').onclick=()=>{
/* EN: document.getElementById('tcAppearanceCancel').onclick in bindPanel: call clone, syncControls, applyCurrent; update draft, sessionDirty.
   ZH: bindPanel 中的 document.getElementById('tcAppearanceCancel').onclick：调用 clone、syncControls、applyCurrent；更新 draft、sessionDirty。 */
draft=clone(committed);sessionDirty=false;syncControls();applyCurrent();setStatus('已撤销未应用的外观修改','ok');};
  document.getElementById('tcAppearanceApply').onclick=async()=>{
/* EN: document.getElementById('tcAppearanceApply').onclick in bindPanel: call clone, normalize, JSON.stringify; update committed, draft, sessionDirty.
   ZH: bindPanel 中的 document.getElementById('tcAppearanceApply').onclick：调用 clone、normalize、JSON.stringify；更新 committed、draft、sessionDirty。 */
try{
    /* EN: Snapshot the owning sheet before persistence and record its background in the normal sheet undo timeline.
       ZH: 持久化前记录所属画布，并将背景写入普通画布撤销时间线。 */
    const owner=sheets[curSheet],previous=clone(committed),next=normalize(draft),backgroundChanged=JSON.stringify(previous.background)!==JSON.stringify(next.background);
    await persist(next);if(sheets[curSheet]!==owner)return;
    committed=next;draft=clone(next);sessionDirty=false;documentBackgroundExplicit=true;appDefaultBackground=clone(next.background);documentAppearance={background:clone(next.background)};
    applyCurrent();if(typeof layout==='function')layout();if(backgroundChanged&&typeof markDirty==='function')markDirty();
    setStatus('原版配色已保存；背景已写入当前导图','ok');
  }catch(e){draft=clone(committed);sessionDirty=false;applyCurrent();syncControls();setStatus(e.message||'保存失败，已回滚预览','err');}};
  window.addEventListener('resize',renderPreview);
}
function onSettingsOpened(){
/* EN: Manage a reversible appearance-editing session using committed and draft state.
   ZH: 使用已提交状态和草稿管理可取消的外观编辑会话。 */
beginEdit();}
function onSettingsClosed(){
/* EN: Manage a reversible appearance-editing session using committed and draft state.
   ZH: 使用已提交状态和草稿管理可取消的外观编辑会话。 */
if(editingSession){if(sessionDirty){draft=clone(committed);applyCurrent();}editingSession=false;sessionDirty=false;}}
function clampWorldPoint(x,y){
/* EN: Calculate finite-canvas bounds and keep layout/view operations within the configured canvas.
   ZH: 计算有限画布边界，使布局和视图操作符合设定画布范围。 */

  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited')return{x,y};
  const q=canvasBounds(committed),m=24;return{x:clamp(x,q.left+m,q.right-m),y:clamp(y,q.top+m,q.bottom-m)};
}
function constrainLayout(){
/* EN: Calculate finite-canvas bounds and keep layout/view operations within the configured canvas.
   ZH: 计算有限画布边界，使布局和视图操作符合设定画布范围。 */

  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited'||!roots||!roots.length)return false;
  if(constrainGuard){constrainGuard=false;return false;}
  let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
  for(const id in TB){const t=TB[id];if(t._ghost||!Number.isFinite(t._x)||!Number.isFinite(t._cy))continue;minX=Math.min(minX,t._x);minY=Math.min(minY,t._cy-t._h/2);maxX=Math.max(maxX,t._x+t._w);maxY=Math.max(maxY,t._cy+t._h/2);}
  if(!Number.isFinite(minX))return false;const q=canvasBounds(committed),m=24,L=q.left+m,R=q.right-m,T=q.top+m,B=q.bottom-m;
  let dx=0,dy=0;if(maxX-minX<=R-L){if(minX<L)dx=L-minX;else if(maxX>R)dx=R-maxX;}else dx=L-minX;
  if(maxY-minY<=B-T){if(minY<T)dy=T-minY;else if(maxY>B)dy=B-maxY;}else dy=T-minY;
  if(Math.abs(dx)<.01&&Math.abs(dy)<.01)return false;
  roots.forEach(id=>{
/* EN: Process each item in roots.forEach: update t.x, t.y.
   ZH: 逐项处理 roots.forEach 中的元素：更新 t.x、t.y。 */
const t=TB[id];if(t){t.x=(Number(t.x)||0)+dx;t.y=(Number(t.y)||0)+dy;}});constrainGuard=true;return true;
}
function fitLimitedCanvas(){
/* EN: Calculate finite-canvas bounds and keep layout/view operations within the configured canvas.
   ZH: 计算有限画布边界，使布局和视图操作符合设定画布范围。 */

  const b=committed&&committed.background;if(!b||!b.enabled||b.mode!=='limited')return false;
  const q=canvasBounds(committed),pad=48;scale=Math.max(.3,Math.min(2.5,(canvas.clientWidth-pad*2)/q.width,(canvas.clientHeight-pad*2)/q.height));
  panX=canvas.clientWidth/2-CANVAS_CENTER.x*scale;panY=canvas.clientHeight/2-CANVAS_CENTER.y*scale;applyTransform();return true;
}

function loadDocumentAppearance(value){
/* EN: Bind a sheet's committed background and discard drafts when switching, reloading or undoing that sheet.
   ZH: 绑定画布已提交背景，并在切换、重载或撤销该画布时丢弃草稿。 */
/* EN: Restore or serialize the document-specific background separately from application defaults.
   ZH: 将文档专属背景与应用默认值分开恢复或序列化。 */

  const hasExplicit=!!(value&&value.background&&typeof value.background==='object');
  const next=hasExplicit?normalize({background:value.background}).background:clone(appDefaultBackground);
  documentBackgroundExplicit=hasExplicit;
  documentAppearance={background:clone(next)};committed.background=clone(next);
  draft=clone(committed);sessionDirty=false;applyCurrent();syncControls();
}
function getDocumentAppearance(){
/* EN: Restore or serialize the document-specific background separately from application defaults.
   ZH: 将文档专属背景与应用默认值分开恢复或序列化。 */
return {background:clone(committed.background)};}

committed=defaultState();draft=clone(committed);buildPanel();ensureLayers();initialized=true;restore();
document.addEventListener('knotjot:settings-opened',onSettingsOpened);
document.addEventListener('knotjot:settings-closed',onSettingsClosed);
document.addEventListener('knotjot:ui-skin-applied',()=>{
/* EN: Handle knotjot:ui-skin-applied on document.addEventListener: call applyCurrent, syncControls.
   ZH: 处理 document.addEventListener 的 knotjot:ui-skin-applied 事件或通道：调用 applyCurrent、syncControls。 */
applyCurrent();syncControls(false);});
document.addEventListener('knotjot:scheme-applied',e=>/* EN: Handle knotjot:scheme-applied on document.addEventListener: call applySchemeToState; return applySchemeToState(e.detail&&e.detail.scheme). ZH: 处理 document.addEventListener 的 knotjot:scheme-applied 事件或通道：调用 applySchemeToState；返回 applySchemeToState(e.detail&&e.detail.scheme)。 */ applySchemeToState(e.detail&&e.detail.scheme));
window.AppearanceEditor={applyCurrent,updateCanvasTransform,cancelEdit,clampWorldPoint,constrainLayout,fitLimitedCanvas,loadDocumentAppearance,getDocumentAppearance,
  getDefaultBackground:()=>/* EN: getDefaultBackground in module: call clone; return clone(appDefaultBackground). ZH: module 中的 getDefaultBackground：调用 clone；返回 clone(appDefaultBackground)。 */ clone(appDefaultBackground),getState:()=>/* EN: getState in module: call clone; return clone(committed). ZH: module 中的 getState：调用 clone；返回 clone(committed)。 */ clone(committed),getDraft:()=>/* EN: getDraft in module: call clone, activeState; return clone(activeState()). ZH: module 中的 getDraft：调用 clone、activeState；返回 clone(activeState())。 */ clone(activeState()),normalizeForTest:normalize,canvasBounds:()=>/* EN: Calculate finite-canvas bounds and keep layout/view operations within the configured canvas. ZH: 计算有限画布边界，使布局和视图操作符合设定画布范围。 */ canvasBounds(committed)};
})();
