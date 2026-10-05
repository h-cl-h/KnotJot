(function(){
/* EN: Callback for function(){ 'use strict'; const MAX_TEXT_RULE_CHARACTERS=window.KnotJotTextInputRules.MAX_CHARACTERS,MAX_TEXT_: call BUILTINS.map, document.addEventListener, init; update window.applyTextStyleToNode, window.TextStyleFeature.
   ZH: function(){ 'use strict'; const MAX_TEXT_RULE_CHARACTERS=window.KnotJotTextInputRules.MAX_CHARACTERS,MAX_TEXT_ 的回调：调用 BUILTINS.map、document.addEventListener、init；更新 window.applyTextStyleToNode、window.TextStyleFeature。 */

'use strict';
const MAX_TEXT_RULE_CHARACTERS=window.KnotJotTextInputRules.MAX_CHARACTERS,MAX_TEXT_STYLE_DIMENSION=3000;
const BUILTINS=[
 {id:'classic',name:'经典白',bg:'#ffffff',border:'#dfe3eb',color:'#2c3140',radius:12,shadow:'0 2px 8px #17203314'},
 {id:'ocean',name:'海洋蓝',bg:'#eaf4ff',border:'#4d93e6',color:'#174f8f',radius:14,shadow:'0 5px 14px #2f77c52b'},
 {id:'mint',name:'薄荷绿',bg:'#eafff6',border:'#37b77b',color:'#176644',radius:18,shadow:'0 5px 14px #229a6622'},
 {id:'sunset',name:'日落橙',bg:'#fff3e9',border:'#ee8c45',color:'#8c431b',radius:10,shadow:'0 5px 14px #d26b2b25'},
 {id:'ink',name:'墨黑标题',bg:'#252a34',border:'#11151c',color:'#ffffff',radius:7,shadow:'0 7px 16px #1115',fontWeight:700},
 {id:'note',name:'便签纸',bg:'#fff9b8',border:'#e7d86a',color:'#5b5120',radius:2,shadow:'3px 5px 10px #6f63352c'},
 {id:'glass',name:'玻璃紫',bg:'#f1edffdd',border:'#9273e8',color:'#4a318c',radius:20,shadow:'0 8px 22px #6746b833'},
 {id:'outline',name:'极简线框',bg:'transparent',border:'#5c6475',color:'#303644',radius:0,shadow:'none',borderWidth:2},
 {id:'neon',name:'霓虹夜色',bg:'#17162b',border:'#52e4ff',color:'#f1fbff',radius:12,shadow:'0 0 13px #42dff077'}
];
let customStyles=[],styleSettings={},styles=BUILTINS.map(x=>/* EN: Derive the next value for BUILTINS.map: return Object.assign({_builtin:true,scope:'all'},x). ZH: 计算 BUILTINS.map 的下一项结果：返回 Object.assign({_builtin:true,scope:'all'},x)。 */ Object.assign({_builtin:true,scope:'all'},x)),defaultStyleId='classic',placementMode='normal',currentTab='styles',externalPath='',systemFontFamilies=['system-ui','sans-serif','serif','monospace'];
const q=s=>/* EN: q in module: call document.querySelector; return document.querySelector(s). ZH: module 中的 q：调用 document.querySelector；返回 document.querySelector(s)。 */ document.querySelector(s), ids=()=>/* EN: ids in module: call [...selSet].filter. ZH: module 中的 ids：调用 [...selSet].filter。 */ [...selSet].filter(id=>/* EN: Test an item for [...selSet].filter: return TB[id]&&!TB[id]._ghost. ZH: 判断 [...selSet].filter 的元素条件：返回 TB[id]&&!TB[id]._ghost。 */ TB[id]&&!TB[id]._ghost);
const tr=s=>/* EN: tr in module: call window.t. ZH: module 中的 tr：调用 window.t。 */ typeof window.t==='function'?window.t(s):s;
function esc(s){
/* EN: Escape text for safe insertion into generated HTML or XML markup.
   ZH: 转义文字，使其可以安全插入生成的 HTML 或 XML 标记。 */
return String(s==null?'':s).replace(/[&<>\"]/g,c=>(/* EN: Callback for String(s==null?'':s).replace: return {'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;'}[c]. ZH: String(s==null?'':s).replace 的回调：返回 {'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;'}[c]。 */ {'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;'}[c]));}
function safeImageData(value){
/* EN: safeImageData in module: call /^data:image\/(?:png|jpeg|gif|bmp);base64,/i.exec, /^[A-Za-z0-9+/=]+$/.test, s.slice.
   ZH: module 中的 safeImageData：调用 /^data:image\/(?:png|jpeg|gif|bmp);base64,/i.exec、/^[A-Za-z0-9+/=]+$/.test、s.slice。 */
const s=String(value||'');if(s.length>36000000)return'';const prefix=/^data:image\/(?:png|jpeg|gif|bmp);base64,/i.exec(s);return prefix&&/^[A-Za-z0-9+/=]+$/.test(s.slice(prefix[0].length))?s:'';}
function getStyle(id){
/* EN: getStyle in module: call styles.find.
   ZH: module 中的 getStyle：调用 styles.find。 */
return styles.find(x=>/* EN: Test an item for styles.find: return x.id===id. ZH: 判断 styles.find 的元素条件：返回 x.id===id。 */ x.id===id)||BUILTINS[0];}
function styleData(t){
/* EN: styleData in module: call getStyle.
   ZH: module 中的 styleData：调用 getStyle。 */
const base=getStyle(t.textStyleId||defaultStyleId||'classic');return Object.assign({},base,t.textStyle||{});}
 function effectiveRules(t){
/* EN: Resolve style defaults and explicit textbox overrides for input or size behavior.
   ZH: 解析样式默认值与文本框显式覆盖值，确定输入或尺寸行为。 */
const inherited=styleData(t).textRules||{},own=t.textRules||{},r=t._textRulesOverride===true||!Object.keys(inherited).length?Object.assign({},inherited,own):Object.assign({},inherited);r.maxLength=Math.max(0,Math.min(MAX_TEXT_RULE_CHARACTERS,Number(r.maxLength)||0));return r;}
function effectiveSizing(t){
/* EN: Resolve style defaults and explicit textbox overrides for input or size behavior.
   ZH: 解析样式默认值与文本框显式覆盖值，确定输入或尺寸行为。 */
const inherited=styleData(t).textSizing||{},own=t.textSizing||{};return t._textSizingOverride===true||!Object.keys(inherited).length?Object.assign({},inherited,own):Object.assign({},inherited);}
function sizingMode(mode){
/* EN: sizingMode in module: return mode==='stretch'?'stretch':'uniform'.
   ZH: module 中的 sizingMode：返回 mode==='stretch'?'stretch':'uniform'。 */
return mode==='stretch'?'stretch':'uniform';}
function oneCharacterRegion(s,f){
/* EN: Derive a one-character minimum frame using the authored aspect ratio and text-region fractions.
   ZH: 依据设计长宽比与文字区域比例推导单字符最小外框。 */
const fontSize=Math.max(1,Number(f&&f.fontSize||s&&s.fontSize)||14),lineHeight=Math.max(.8,Number(f&&f.lineHeight)||1.4);return{width:fontSize+12,height:fontSize*lineHeight+8};}
function textRegionBaseline(s,z,mode,aspect,glyph){
/* EN: Derive a one-character minimum frame using the authored aspect ratio and text-region fractions.
   ZH: 依据设计长宽比与文字区域比例推导单字符最小外框。 */
const tr=s&&s.textRegion||{},wf=Number(tr.w)>0?Math.min(1,Math.max(.001,Number(tr.w)/100)):1,hf=Number(tr.h)>0?Math.min(1,Math.max(.001,Number(tr.h)/100)):1,targetW=Math.max(1,Number(glyph&&glyph.width)||26),targetH=Math.max(1,Number(glyph&&glyph.height)||27.6);aspect=Math.max(.1,Number(aspect)||1.8);
 // Both policies start from the authored frame ratio.  Free stretch is allowed to
 // change the axes independently only after this one-character baseline; deriving
 // width and height separately from the region fractions turns wide artwork into a
 // vertical frame before the user has typed anything.
 let width=Math.max(targetW/wf,targetH*aspect/hf),height=width/aspect;
  const scale=Math.min(1,MAX_TEXT_STYLE_DIMENSION/Math.max(1,width),MAX_TEXT_STYLE_DIMENSION/Math.max(1,height));return{width:width*scale,height:height*scale,targetW,targetH,wf,hf};}
function setVar(el,k,v){
/* EN: setVar in module: call el.style.removeProperty, el.style.setProperty.
   ZH: module 中的 setVar：调用 el.style.removeProperty、el.style.setProperty。 */
if(v==null||v==='')el.style.removeProperty(k);else el.style.setProperty(k,String(v));}
async function loadSystemFonts(){
/* EN: loadSystemFonts in module: call window.queryLocalFonts, [...new Set(fonts.map(x=>x.family).filter(Boolean))].sort, fonts.map(x=>x.family).filter; update systemFontFamilies.
   ZH: module 中的 loadSystemFonts：调用 window.queryLocalFonts、[...new Set(fonts.map(x=>x.family).filter(Boolean))].sort、fonts.map(x=>x.family).filter；更新 systemFontFamilies。 */
if(typeof window.queryLocalFonts!=='function')return;try{const fonts=await window.queryLocalFonts(),names=[...new Set(fonts.map(x=>/* EN: Derive the next value for fonts.map: return x.family. ZH: 计算 fonts.map 的下一项结果：返回 x.family。 */ x.family).filter(Boolean))].sort((a,b)=>/* EN: Compare items for [...new Set(fonts.map(x=>x.family).filter(Boolean))].sort: call a.localeCompare; return a.localeCompare(b). ZH: 比较 [...new Set(fonts.map(x=>x.family).filter(Boolean))].sort 中的元素顺序：调用 a.localeCompare；返回 a.localeCompare(b)。 */ a.localeCompare(b));if(names.length){systemFontFamilies=names;if(q('#textStylePanel')&&currentTab==='font')refresh();}}catch(_){}}
function fontOptions(current){
/* EN: Build the font-selection choices used by text styling controls.
   ZH: 构建文本样式控件使用的字体选项。 */
const names=systemFontFamilies.includes(current)?systemFontFamilies:[current,...systemFontFamilies].filter(Boolean);return names.map(x=>/* EN: Derive the next value for names.map: call esc; return `<option value="${esc(x)}">${esc(x)}</option>`. ZH: 计算 names.map 的下一项结果：调用 esc；返回 `<option value="${esc(x)}">${esc(x)}</option>`。 */ `<option value="${esc(x)}">${esc(x)}</option>`).join('');}
function styleBounds(s){
/* EN: styleBounds in module: call (Array.isArray(s.layers)?s.layers:[]).forEach, Array.isArray, parts.push.
   ZH: module 中的 styleBounds：调用 (Array.isArray(s.layers)?s.layers:[]).forEach、Array.isArray、parts.push。 */
const parts=[];(Array.isArray(s.layers)?s.layers:[]).forEach(l=>{
/* EN: Process each item in (Array.isArray(s.layers)?s.layers:[]).forEach: call Math.abs, parts.push, [[x,y],[x+w,y],[x+w,y+h],[x,y+h]].map.
   ZH: 逐项处理 (Array.isArray(s.layers)?s.layers:[]).forEach 中的元素：调用 Math.abs、parts.push、[[x,y],[x+w,y],[x+w,y+h],[x,y+h]].map。 */
if(l.isVisible===false)return;const x=Number(l.x)||0,y=Number(l.y)||0,w=Math.max(0,Number(l.w)||0),h=Math.max(0,Number(l.h)||0),a=(Number(l.rotation)||0)*Math.PI/180,cx=x+w/2,cy=y+h/2;if(Math.abs(a)<.0001){parts.push({x,y,w,h});return;}const pts=[[x,y],[x+w,y],[x+w,y+h],[x,y+h]].map(p=>{
/* EN: Derive the next value for [[x,y],[x+w,y],[x+w,y+h],[x,y+h]].map: call Math.cos, Math.sin.
   ZH: 计算 [[x,y],[x+w,y],[x+w,y+h],[x,y+h]].map 的下一项结果：调用 Math.cos、Math.sin。 */
const dx=p[0]-cx,dy=p[1]-cy;return [cx+dx*Math.cos(a)-dy*Math.sin(a),cy+dx*Math.sin(a)+dy*Math.cos(a)];});const xs=pts.map(p=>/* EN: Derive the next value for pts.map: return p[0]. ZH: 计算 pts.map 的下一项结果：返回 p[0]。 */ p[0]),ys=pts.map(p=>/* EN: Derive the next value for pts.map: return p[1]. ZH: 计算 pts.map 的下一项结果：返回 p[1]。 */ p[1]);parts.push({x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs),h:Math.max(...ys)-Math.min(...ys)});});const tr=s.textRegion;if(tr&&Number(tr.w)>0&&Number(tr.h)>0)parts.push({x:Number(tr.x)||0,y:Number(tr.y)||0,w:Number(tr.w),h:Number(tr.h)});if(!parts.length)return{x:0,y:0,w:100,h:100};const x=Math.min(...parts.map(p=>/* EN: Derive the next value for parts.map: return p.x. ZH: 计算 parts.map 的下一项结果：返回 p.x。 */ p.x)),y=Math.min(...parts.map(p=>/* EN: Derive the next value for parts.map: return p.y. ZH: 计算 parts.map 的下一项结果：返回 p.y。 */ p.y)),r=Math.max(...parts.map(p=>/* EN: Derive the next value for parts.map: return p.x+p.w. ZH: 计算 parts.map 的下一项结果：返回 p.x+p.w。 */ p.x+p.w)),b=Math.max(...parts.map(p=>/* EN: Derive the next value for parts.map: return p.y+p.h. ZH: 计算 parts.map 的下一项结果：返回 p.y+p.h。 */ p.y+p.h));return{x,y,w:Math.max(.01,r-x),h:Math.max(.01,b-y)};}
// The editor stores every X/W value as a percentage of its 900px design width and
// every Y/H value as a percentage of its 520px design height.  Its preview always
// renders that complete 0..100 by 0..100 design plane.  Cropping the SVG to the
// visible layers changes both the artwork scale and the text-region position, so
// keep the same fixed coordinate plane here as well.
function renderBounds(){
/* EN: Keep the full 0..100 design plane so imported artwork and text-region positions remain aligned.
   ZH: 保持完整的 0..100 设计平面，使导入图形与文字区域位置一致。 */
return{x:0,y:0,w:100,h:100};}
function renderArt(card,s){
/* EN: Preserve painted mask alpha, image transparency and sRGB luminance; inverse masks complement source-over coverage while clipping retains geometric semantics.
   ZH: 保留绘制蒙版透明度、图片透明像素及 sRGB 亮度；反向蒙版取源覆盖量补值，剪切保持几何语义。 */
/* EN: Render authored layers, clipping/masks and the unique text region in one SVG coordinate system.
   ZH: 在统一 SVG 坐标系内绘制设计图层、剪切蒙版和唯一文字区域。 */

 const NS='http://www.w3.org/2000/svg',node=card.closest('.node'),inner=node&&node._inner;
 let svg=card.querySelector(':scope > .ts-decoration');
 if(svg&&svg.tagName.toLowerCase()!=='svg'){svg.remove();svg=null;}
 if(!svg){svg=document.createElementNS(NS,'svg');svg.classList.add('ts-decoration');svg.setAttribute('preserveAspectRatio','none');card.prepend(svg);}
 let defsEl=svg.querySelector(':scope > defs.ts-defs'),artEl=svg.querySelector(':scope > g.ts-artwork');
 if(!defsEl){defsEl=document.createElementNS(NS,'defs');defsEl.classList.add('ts-defs');svg.prepend(defsEl);}
 if(!artEl){artEl=document.createElementNS(NS,'g');artEl.classList.add('ts-artwork');defsEl.after(artEl);}
 const pixelW=Math.max(1,card.clientWidth||card.getBoundingClientRect().width||100),pixelH=Math.max(1,card.clientHeight||card.getBoundingClientRect().height||100),sx=pixelW/100,sy=pixelH/100;
 svg.setAttribute('viewBox',`0 0 ${pixelW} ${pixelH}`);
 const tr=s&&s.textRegion||{},useComposite=!!(node&&node.classList.contains('ts-replace-frame')&&node.classList.contains('ts-has-text-region')&&inner),rx=Number(tr.x)||0,ry=Number(tr.y)||0,rw=Math.max(0,Number(tr.w)||0),rh=Math.max(0,Number(tr.h)||0);
 let textObject=svg.querySelector(':scope > foreignObject.ts-text-object');
 if(useComposite){
  if(!textObject){textObject=document.createElementNS(NS,'foreignObject');textObject.classList.add('ts-text-object');svg.appendChild(textObject);}
  textObject.setAttribute('x',rx*sx);textObject.setAttribute('y',ry*sy);textObject.setAttribute('width',rw*sx);textObject.setAttribute('height',rh*sy);
  if(inner.parentNode!==textObject)textObject.appendChild(inner);
  svg.classList.add('ts-composite');
  setVar(node,'--ts-group-anchor-x',(rx+rw/2)+'%');setVar(node,'--ts-group-anchor-y',(ry+rh/2)+'%');
 }else{
  if(textObject&&inner&&inner.parentNode===textObject)card.insertBefore(inner,card.querySelector('.tag-row'));
  if(textObject)textObject.remove();
  svg.classList.remove('ts-composite');
 }
 const layers=Array.isArray(s.layers)?s.layers:[],uid='tsa-'+String(node&&node.dataset.id||Math.random()).replace(/[^a-z0-9_-]/gi,'-'),maskRect=`x="0" y="0" width="${pixelW}" height="${pixelH}"`;
 const geom=(l,paint=true,color='')=>{
/* EN: geom in renderArt: call ['rect','ellipse','line','image'].includes, Number.isFinite, Math.abs.
   ZH: renderArt 中的 geom：调用 ['rect','ellipse','line','image'].includes、Number.isFinite、Math.abs。 */

   const type=['rect','ellipse','line','image'].includes(l.type)?l.type:'rect',x=(Number(l.x)||0)*sx,y=(Number(l.y)||0)*sy,w=(Number(l.w)||0)*sx,h=(Number(l.h)||0)*sy,x1=Number.isFinite(Number(l.x1))?Number(l.x1)*sx:x,y1=Number.isFinite(Number(l.y1))?Number(l.y1)*sy:y,x2=Number.isFinite(Number(l.x2))?Number(l.x2)*sx:x+w,y2=Number.isFinite(Number(l.y2))?Number(l.y2)*sy:y+h,rot=Number(l.rotation)||0,cx=x+w/2,cy=y+h/2,transform=rot?` transform="rotate(${rot} ${cx} ${cy})"`:'',radius=Math.max(0,Number(l.radius)||0),round=radius?` rx="${Math.min(Math.abs(w)/2,radius)}" ry="${Math.min(Math.abs(h)/2,radius)}"`:'';
   if(!paint){if(type==='ellipse')return `<ellipse cx="${cx}" cy="${cy}" rx="${w/2}" ry="${h/2}"${transform} fill="${color||'#fff'}"/>`;if(type==='line')return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"${transform} stroke="${color||'#fff'}" stroke-width="${Math.max(1,Number(l.strokeWidth)||1)}"/>`;return `<rect x="${x}" y="${y}" width="${w}" height="${h}"${round}${transform} fill="${color||'#fff'}"/>`;}
   const opacity=l.opacity==null?1:Number(l.opacity),blend=['normal','multiply','screen','overlay'].includes(l.blendMode)?l.blendMode:'normal',blendStyle=blend==='normal'?'':` style="mix-blend-mode:${blend}"`;if(type==='image'){const href=safeImageData(l.imageData);return href?`<image x="${x}" y="${y}" width="${w}" height="${h}" href="${esc(href)}" preserveAspectRatio="none"${transform}${blendStyle} opacity="${opacity}"/>`:'';}const fill=type==='line'?'none':esc(l.fill||'#DDE9FF'),stroke=esc(l.stroke||'#5B8DEF'),sw=Math.max(0,Number(l.strokeWidth)||0),common=`${transform}${blendStyle} fill="${fill}" stroke="${stroke}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke" opacity="${opacity}"`;if(type==='ellipse')return `<ellipse cx="${cx}" cy="${cy}" rx="${w/2}" ry="${h/2}" ${common}/>`;if(type==='line')return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" ${common}/>`;return `<rect x="${x}" y="${y}" width="${w}" height="${h}"${round} ${common}/>`;
 };
 let defs='',body='';layers.forEach((l,i)=>{
/* EN: Process each item in layers.forEach: call geom; update defs, effect, body.
   ZH: 逐项处理 layers.forEach 中的元素：调用 geom；更新 defs、effect、body。 */
if(l.isVisible===false)return;const ownEffect=i>0&&((l.clipMode&&l.clipMode!=='none')||(l.maskMode&&l.maskMode!=='none'));if(ownEffect)return;const cutter=layers[i+1]&&layers[i+1].isVisible!==false?layers[i+1]:null;let effect='';
if(cutter&&cutter.maskMode&&cutter.maskMode!=='none'){
  // EN: Paint real transformed geometry onto an opaque black/white base; color conversion leaves its alpha untouched, giving A or 1-A and L*A or 1-L*A.
  // ZH: 将真实变换几何绘制到不透明黑白底上；颜色转换保留透明度，得到 A、1-A、L*A 或 1-L*A。
  const mid=`${uid}-usermask-${i}`,fid=`${mid}-coverage`,inverse=cutter.maskMode==='inverseAlpha'||cutter.maskMode==='inverseLuminance',lum=cutter.maskMode==='luminance'||cutter.maskMode==='inverseLuminance';
  const matrix=lum?(inverse?'-1 0 0 0 1 0 -1 0 0 1 0 0 -1 0 1 0 0 0 1 0':null):(inverse?'0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0':'0 0 0 0 1 0 0 0 0 1 0 0 0 0 1 0 0 0 1 0');
  if(matrix)defs+=`<filter id="${fid}" filterUnits="userSpaceOnUse" ${maskRect} color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="${matrix}"/></filter>`;
  const source=geom({...cutter,blendMode:'normal'});
  defs+=`<mask id="${mid}" maskUnits="userSpaceOnUse" mask-type="luminance" color-interpolation="sRGB" ${maskRect}><rect ${maskRect} fill="${inverse?'#fff':'#000'}"/><g${matrix?` filter="url(#${fid})"`:''}>${source}</g></mask>`;effect=` mask="url(#${mid})"`;
}else if(cutter&&cutter.clipMode==='intersect'){const cid=`${uid}-clip-${i}`;defs+=`<clipPath id="${cid}" clipPathUnits="userSpaceOnUse">${geom(cutter,false,'#fff')}</clipPath>`;effect=` clip-path="url(#${cid})"`;}else if(cutter&&cutter.clipMode==='subtract'){const mid=`${uid}-mask-${i}`;defs+=`<mask id="${mid}" maskUnits="userSpaceOnUse" mask-type="luminance" ${maskRect}><rect ${maskRect} fill="#fff"/>${geom(cutter,false,'#000')}</mask>`;effect=` mask="url(#${mid})"`;}body+=`<g${effect}>${geom(l)}</g>`;});
 defsEl.innerHTML=defs;artEl.innerHTML=body;
}
let textMeasureCanvas;
function textDemand(inner,text){
/* EN: Measure actual text demand and overflow to determine a fitting style-frame size.
   ZH: 测量实际文字需求及溢出情况，以确定合适的样式外框尺寸。 */
try{const cs=getComputedStyle(inner),canvas=textMeasureCanvas||(textMeasureCanvas=document.createElement('canvas')),ctx=canvas.getContext('2d');ctx.font=`${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;const fs=Math.max(1,parseFloat(cs.fontSize)||14),lh=Math.max(fs,parseFloat(cs.lineHeight)||fs*1.4),spacing=parseFloat(cs.letterSpacing)||0,lines=String(text).split('\n'),widths=lines.map(line=>{
/* EN: Derive the next value for lines.map: call Array.from, ctx.measureText.
   ZH: 计算 lines.map 的下一项结果：调用 Array.from、ctx.measureText。 */
const chars=Array.from(line),raw=ctx.measureText(line||' ').width+Math.max(0,chars.length-1)*spacing;return Math.max(fs*.45,raw);});return{fontSize:fs,lineHeight:lh,widths,longest:Math.max(fs*.45,...widths),explicitLines:Math.max(1,lines.length),characters:Math.max(1,Array.from(String(text).replace(/\n/g,'')).length)};}catch(_){const characters=Math.max(1,Array.from(String(text).replace(/\n/g,'')).length);return{fontSize:14,lineHeight:19.6,widths:[characters*14],longest:characters*14,explicitLines:1,characters};}}
function measuredTextBox(demand,regionWidth,wrap,safe=false){
/* EN: Measure actual text demand and overflow to determine a fitting style-frame size.
   ZH: 测量实际文字需求及溢出情况，以确定合适的样式外框尺寸。 */
const padX=12+(safe?6:0),contentWidth=Math.max(1,regionWidth-padX);let rows=0;for(const lineWidth of demand.widths)rows+=wrap?Math.max(1,Math.ceil((lineWidth-.01)/contentWidth)):1;return{width:(wrap?Math.min(demand.longest,contentWidth):demand.longest)+padX,height:rows*demand.lineHeight+8+(safe&&rows>1?demand.lineHeight:0)};}
function overflowRatios(inner){
/* EN: Measure actual text demand and overflow to determine a fitting style-frame size.
   ZH: 测量实际文字需求及溢出情况，以确定合适的样式外框尺寸。 */
inner.classList.add('ts-measuring');const cw=Math.max(1,inner.clientWidth),ch=Math.max(1,inner.clientHeight);let rw=1,rh=1;try{const range=document.createRange();range.selectNodeContents(inner);const r=range.getBoundingClientRect(),cs=getComputedStyle(inner),px=(parseFloat(cs.paddingLeft)||0)+(parseFloat(cs.paddingRight)||0),py=(parseFloat(cs.paddingTop)||0)+(parseFloat(cs.paddingBottom)||0);rw=Math.max(1,(r.width+px)/cw);rh=Math.max(1,(r.height+py)/ch);}catch(_){rw=Math.max(1,inner.scrollWidth)/cw;rh=Math.max(1,inner.scrollHeight)/ch;}inner.classList.remove('ts-measuring');return{cw,ch,rw,rh};}
function preserveTextRegionAnchor(t,previous,width,height){
/* EN: Resize the whole styled textbox under the uniform/stretch policy while preserving its text-region anchor.
   ZH: 按等比或拉伸策略调整整张样式文本框，同时保持文字区域锚点。 */
if(!previous||!previous.positioned||t.parentBrace||t._nodeAttach||!Array.isArray(roots)||!roots.includes(t.id))return;const ax=Number.isFinite(t._textRegionAnchorX)?t._textRegionAnchorX:.5,ay=Number.isFinite(t._textRegionAnchorY)?t._textRegionAnchorY:.5,anchorX=t._x+previous.w*ax,anchorY=t._cy+previous.h*(ay-.5);t.x=anchorX-width*ax;t.y=anchorY-height*(ay-.5);}
function fitTextRegion(e,t,s,mode,aspect,previous){
/* EN: Resize the whole styled textbox under the uniform/stretch policy while preserving its text-region anchor.
   ZH: 按等比或拉伸策略调整整张样式文本框，同时保持文字区域锚点。 */
const card=e._card,inner=e._inner,z=effectiveSizing(t),f=t.textFormat||{},editingNow=inner.getAttribute('contenteditable')==='true',text=editingNow?(inner.textContent||''):(t.text||'');mode=sizingMode(mode);aspect=Math.max(.1,Number(aspect)||1.8);const baseline=textRegionBaseline(s,z,mode,aspect,oneCharacterRegion(s,f));let width=baseline.width,height=baseline.height;
  const apply=()=>{
/* EN: apply in fitTextRegion: call setVar, renderArt; update width, height.
   ZH: fitTextRegion 中的 apply：调用 setVar、renderArt；更新 width、height。 */
width=Math.max(1,Math.min(MAX_TEXT_STYLE_DIMENSION,width));height=Math.max(1,Math.min(MAX_TEXT_STYLE_DIMENSION,height));if(mode==='uniform'){height=width/aspect;setVar(e,'--ts-uniform-size',width+'px');setVar(e,'--ts-uniform-height',height+'px');}else{setVar(e,'--ts-stretch-width',width+'px');setVar(e,'--ts-stretch-height',height+'px');}void card.offsetWidth;renderArt(card,s);};
  if(text.length){const demand=textDemand(inner,text),wrap=f.wrap!==false,baseRegionWidth=baseline.width*baseline.wf,baseRegionHeight=baseline.height*baseline.hf,fits=(s,safe=false)=>{
/* EN: fits in fitTextRegion: call measuredTextBox.
   ZH: fitTextRegion 中的 fits：调用 measuredTextBox。 */
const box=measuredTextBox(demand,baseRegionWidth*s,wrap,safe);return box.width<=baseRegionWidth*s+.5&&box.height<=baseRegionHeight*s+.5;};if(mode==='uniform'){if(!fits(1)){let low=1,high=1;while(high<MAX_TEXT_STYLE_DIMENSION/Math.max(baseline.width,baseline.height)&&!fits(high,true))high*=2;high=Math.min(high,MAX_TEXT_STYLE_DIMENSION/Math.max(baseline.width,baseline.height));for(let i=0;i<36;i++){const middle=(low+high)/2;if(fits(middle,true))high=middle;else low=middle;}width=baseline.width*high;height=width/aspect;}}else{const initial=measuredTextBox(demand,baseRegionWidth,wrap);if(initial.width>baseRegionWidth+.5||initial.height>baseRegionHeight+.5){const naturalRegionWidth=demand.longest+18,desiredRegionWidth=Math.max(baseRegionWidth,wrap?Math.min(420,naturalRegionWidth):naturalRegionWidth),box=measuredTextBox(demand,desiredRegionWidth,wrap,true);width=desiredRegionWidth/baseline.wf;height=Math.max(baseline.height,box.height/baseline.hf);}}}
 apply();
  if(text.length)for(let i=0;i<4;i++){const cw=Math.max(1,inner.clientWidth),ch=Math.max(1,inner.clientHeight),dx=Math.max(0,inner.scrollWidth-cw),dy=Math.max(0,inner.scrollHeight-ch);if(dx<=1&&dy<=1)break;if(mode==='uniform'){const correction=Math.max(1,(cw+dx+2)/cw,(ch+dy+2)/ch);width=Math.min(MAX_TEXT_STYLE_DIMENSION,width*correction);height=width/aspect;}else{width=Math.min(MAX_TEXT_STYLE_DIMENSION,width+(dx>1?(dx+2)/baseline.wf:0));height=Math.min(MAX_TEXT_STYLE_DIMENSION,height+(dy>1?(dy+2)/baseline.hf:0));}apply();}
 const fittedWidth=Math.max(1,card.offsetWidth||width),fittedHeight=Math.max(1,card.offsetHeight||height);preserveTextRegionAnchor(t,previous,fittedWidth,fittedHeight);if(!text.length)delete e._tsFit;else e._tsFit={length:Array.from(text).length,width:fittedWidth,height:fittedHeight,mode};}
window.applyTextStyleToNode=function(id){
/* EN: Apply resolved library and node overrides to the frame, text and artwork, updating sizing anchors; preserve an explicit zero border while retaining the existing fallback for other defaulted values.
   ZH: 将解析后的样式库及节点覆盖应用于外框、文字和图稿，并更新尺寸锚点；保留明确的零边框，同时保留其他默认值的原有回退。 */

  const t=TB[id],e=els[id];if(!t||!e)return;const s=styleData(t),f=t.textFormat||{},z=effectiveSizing(t),layers=Array.isArray(s.layers)?s.layers:[],replace=!!s.replaceFrame&&layers.length>0,tr=s.textRegion,hasRegion=!!(tr&&tr.isVisible!==false&&tr.w>0&&tr.h>0),bounds=renderBounds(s),rawMode=z.mode||s.textSizing&&s.textSizing.mode||'',effectiveMode=(rawMode||replace)?sizingMode(rawMode):'';e.classList.toggle('ts-custom',!!(t.textStyleId||t.textStyle));e.classList.toggle('ts-replace-frame',replace);e.classList.toggle('ts-has-text-region',replace&&hasRegion);e.classList.remove('ts-size-fixed','ts-size-uniform','ts-size-stretch');if(effectiveMode)e.classList.add('ts-size-'+effectiveMode);
 const card=e._card,previous={w:Math.max(1,Number(t._w)||card.offsetWidth||1),h:Math.max(1,Number(t._h)||card.offsetHeight||1),positioned:Number.isFinite(t._x)&&Number.isFinite(t._cy)};if(replace){card.style.setProperty('background','transparent','important');card.style.setProperty('background-color','transparent','important');card.style.setProperty('border-width','0px','important');card.style.setProperty('box-shadow','none','important');}else{card.style.removeProperty('background');card.style.removeProperty('background-color');card.style.removeProperty('border-width');card.style.removeProperty('box-shadow');}
 setVar(e,'--ts-bg',replace?'transparent':s.bg);setVar(e,'--ts-border',replace?'transparent':s.border);setVar(e,'--ts-color',f.color||s.color);setVar(e,'--ts-radius',replace?'0px':(s.radius==null?12:s.radius)+'px');setVar(e,'--ts-shadow',replace?'none':s.shadow||'none');setVar(e,'--ts-border-width',replace?'0px':(s.borderWidth===0?0:(s.borderWidth||1.5))+'px');setVar(e,'--ts-font-family',f.fontFamily||s.fontFamily||'inherit');setVar(e,'--ts-font-size',(f.fontSize||s.fontSize||14)+'px');setVar(e,'--ts-font-weight',f.bold?700:(s.fontWeight||400));setVar(e,'--ts-font-style',f.italic?'italic':'normal');setVar(e,'--ts-decoration',f.underline?'underline':'none');
 const textAlign=f.align||s.textAlign||(hasRegion?'center':'left');setVar(e,'--ts-align',textAlign);setVar(e,'--ts-letter-spacing',(f.letterSpacing||0)+'px');setVar(e,'--ts-line-height',f.lineHeight||1.4);setVar(e,'--ts-white-space',f.wrap===false?'nowrap':'pre-wrap');setVar(e,'--ts-max-width','none');setVar(e,'--ts-padding',s.padding||'11px 18px');
 const designAspect=9/5.2,storedAspect=Number(z.aspect||(s.textSizing&&s.textSizing.aspect)),aspect=rawMode==='auto'?designAspect:(storedAspect>0?storedAspect:designAspect),glyph=oneCharacterRegion(s,f),uniformBase=textRegionBaseline(s,z,'uniform',aspect,glyph),stretchBase=textRegionBaseline(s,z,'stretch',aspect,glyph);
 setVar(e,'--ts-fixed-width',stretchBase.width+'px');setVar(e,'--ts-fixed-height',stretchBase.height+'px');setVar(e,'--ts-aspect',aspect);setVar(e,'--ts-uniform-size',uniformBase.width+'px');setVar(e,'--ts-uniform-height',uniformBase.height+'px');setVar(e,'--ts-stretch-width',stretchBase.width+'px');setVar(e,'--ts-stretch-height',stretchBase.height+'px');setVar(e,'--ts-min-height',(z.minHeight||48)+'px');
 if(hasRegion){const rx=Number(tr.x)||0,ry=Number(tr.y)||0,rw=Math.max(0,Number(tr.w)||0),rh=Math.max(0,Number(tr.h)||0);setVar(e,'--ts-text-x',rx+'%');setVar(e,'--ts-text-y',ry+'%');setVar(e,'--ts-text-w',rw+'%');setVar(e,'--ts-text-h',rh+'%');setVar(e,'--ts-text-justify',textAlign==='left'?'flex-start':textAlign==='right'?'flex-end':'center');t._textRegionAnchorX=Math.min(1,Math.max(0,(rx+rw/2)/100));t._textRegionAnchorY=Math.min(1,Math.max(0,(ry+rh/2)/100));}else{delete t._textRegionAnchorX;delete t._textRegionAnchorY;}renderArt(card,s);if(replace&&hasRegion)fitTextRegion(e,t,s,effectiveMode,aspect,previous);
};
function applyAll(){
/* EN: applyAll in module: call Object.keys(TB).forEach, Object.keys, layout.
   ZH: module 中的 applyAll：调用 Object.keys(TB).forEach、Object.keys、layout。 */
Object.keys(TB).forEach(window.applyTextStyleToNode);layout();}
function shell(){
/* EN: shell in module: call q, document.body.insertAdjacentHTML, window.translateNode; update q('.ts-close').onclick, q('.ts-tabs').onclick.
   ZH: module 中的 shell：调用 q、document.body.insertAdjacentHTML、window.translateNode；更新 q('.ts-close').onclick、q('.ts-tabs').onclick。 */
if(q('#textStylePanel'))return;document.body.insertAdjacentHTML('beforeend',`<aside id="textStylePanel"><div class="ts-head"><strong>文本框样式与字体</strong><span class="sp"></span><button class="ts-close" title="关闭">×</button></div><div class="ts-tabs"><button class="ts-tab on" data-tab="styles">样式库</button><button class="ts-tab" data-tab="font">字体</button><button class="ts-tab" data-tab="rules">尺寸与输入</button></div><div class="ts-body"><section class="ts-page on" data-page="styles"><div class="ts-grid"></div></section><section class="ts-page" data-page="font"></section><section class="ts-page" data-page="rules"></section></div><div class="ts-status">请选择文本框</div></aside><div class="ts-card-menu" hidden></div>`);q('.ts-close').onclick=close;q('.ts-tabs').onclick=e=>{
/* EN: q('.ts-tabs').onclick in shell: call e.target.closest, refresh; update currentTab.
   ZH: shell 中的 q('.ts-tabs').onclick：调用 e.target.closest、refresh；更新 currentTab。 */
const b=e.target.closest('[data-tab]');if(!b)return;currentTab=b.dataset.tab;refresh();};if(typeof window.translateNode==='function')window.translateNode(q('#textStylePanel'));}
function open(tab,mode='normal'){
/* EN: open in module: call shell, refresh, document.body.classList.add; update placementMode, currentTab, q('#textStylePanel').style.display.
   ZH: module 中的 open：调用 shell、refresh、document.body.classList.add；更新 placementMode、currentTab、q('#textStylePanel').style.display。 */
shell();placementMode=mode;currentTab=tab||'styles';refresh();document.body.classList.add('ts-panel-open');q('#textStylePanel').style.display='flex';setTimeout(()=>{
/* EN: Callback for setTimeout: call layout.
   ZH: setTimeout 的回调：调用 layout。 */
if(typeof layout==='function')layout();},0);}
function close(){
/* EN: close in module: call document.body.classList.remove, q, closeStyleMenu; update q('#textStylePanel').style.display.
   ZH: module 中的 close：调用 document.body.classList.remove、q、closeStyleMenu；更新 q('#textStylePanel').style.display。 */
document.body.classList.remove('ts-panel-open');q('#textStylePanel').style.display='none';closeStyleMenu();setTimeout(()=>{
/* EN: Callback for setTimeout: call layout.
   ZH: setTimeout 的回调：调用 layout。 */
if(typeof layout==='function')layout();},0);}
function selected(){
/* EN: selected in module: call ids, [selPrimary()].filter, selPrimary.
   ZH: module 中的 selected：调用 ids、[selPrimary()].filter、selPrimary。 */
const a=ids();return a.length?a:[selPrimary()].filter(Boolean);}
function patch(part,key,value){
/* EN: patch in module: call selected, a.forEach, layout.
   ZH: module 中的 patch：调用 selected、a.forEach、layout。 */
const a=selected();if(!a.length)return;a.forEach(id=>{
/* EN: Process each item in a.forEach: call effectiveRules, effectiveSizing, sizingMode; update next.mode, TB[id][part], TB[id]._textRulesOverride.
   ZH: 逐项处理 a.forEach 中的元素：调用 effectiveRules、effectiveSizing、sizingMode；更新 next.mode、TB[id][part]、TB[id]._textRulesOverride。 */
const inherited=part==='textRules'?effectiveRules(TB[id]):part==='textSizing'?effectiveSizing(TB[id]):TB[id][part]||{},next=Object.assign({},inherited,{[key]:value});if(part==='textSizing')next.mode=sizingMode(next.mode);TB[id][part]=next;if(part==='textRules')TB[id]._textRulesOverride=true;if(part==='textSizing')TB[id]._textSizingOverride=true;render([id]);});layout();markDirty();}
async function persistStyles(){
/* EN: Maintain the editable style library and its serializable undo/persistence state.
   ZH: 维护可编辑样式库及其可序列化的撤销和持久化状态。 */
try{const lib={format:'knotjot-text-styles',version:1,defaultStyleId,styleSettings,styles:customStyles.map(x=>{
/* EN: Derive the next value for customStyles.map: call window.KnotJotTextInputRules.pattern; update y.textRules.maxLength.
   ZH: 计算 customStyles.map 的下一项结果：调用 window.KnotJotTextInputRules.pattern；更新 y.textRules.maxLength。 */
const y=Object.assign({},x);delete y._builtin;if(y.textRules){y.textRules.maxLength=Math.max(0,Math.min(MAX_TEXT_RULE_CHARACTERS,Number(y.textRules.maxLength)||0));if(y.textRules.type==='regex'&&y.textRules.pattern&&!window.KnotJotTextInputRules.pattern(y.textRules.pattern))throw new Error('样式 '+y.id+' 的正则表达式无效');}return y;})};let r;if(window.api&&api.saveTextStyles){r=await api.saveTextStyles(lib);if(r&&r.requiresConfirmation){if(!confirm((r.error||'样式库已损坏')+'\n损坏副本：'+(r.corruptPath||'')+'\n仍要用当前样式覆盖吗？'))throw new Error('已取消覆盖损坏的样式库');lib.confirmOverwriteCorrupt=true;r=await api.saveTextStyles(lib);}}else{localStorage.setItem('bmap.textStyles',JSON.stringify(lib));r={ok:true,path:'localStorage'};}if(!r||r.ok===false)throw new Error(r&&r.error||'保存失败');externalPath=r.path||externalPath;q('.ts-status').textContent='样式库已永久保存';}catch(e){q('.ts-status').textContent='样式库保存失败：'+e.message;} }
function closeStyleMenu(){
/* EN: closeStyleMenu in module: call q; update m.hidden.
   ZH: module 中的 closeStyleMenu：调用 q；更新 m.hidden。 */
const m=q('.ts-card-menu');if(m)m.hidden=true;}
function rebuildStyles(){
/* EN: Maintain the editable style library and its serializable undo/persistence state.
   ZH: 维护可编辑样式库及其可序列化的撤销和持久化状态。 */
styles=BUILTINS.map(x=>/* EN: Derive the next value for BUILTINS.map: return Object.assign({_builtin:true,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||'all'},x). ZH: 计算 BUILTINS.map 的下一项结果：返回 Object.assign({_builtin:true,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||'all'},x)。 */ Object.assign({_builtin:true,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||'all'},x)).concat(customStyles.map(x=>/* EN: Derive the next value for customStyles.map: return Object.assign({_builtin:false,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||x.scope||'all'},x). ZH: 计算 customStyles.map 的下一项结果：返回 Object.assign({_builtin:false,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||x.scope||'all'},x)。 */ Object.assign({_builtin:false,scope:(styleSettings[x.id]&&styleSettings[x.id].scope)||x.scope||'all'},x)));}
function setStyleScope(id,scope){
/* EN: setStyleScope in module: call customStyles.find, rebuildStyles, persistStyles; update styleSettings[id], s.scope.
   ZH: module 中的 setStyleScope：调用 customStyles.find、rebuildStyles、persistStyles；更新 styleSettings[id]、s.scope。 */
styleSettings[id]=Object.assign({},styleSettings[id]||{},{scope});const s=customStyles.find(x=>/* EN: Test an item for customStyles.find: return x.id===id. ZH: 判断 customStyles.find 的元素条件：返回 x.id===id。 */ x.id===id);if(s)s.scope=scope;rebuildStyles();persistStyles();refresh();}
function captureState(){
/* EN: Maintain the editable style library and its serializable undo/persistence state.
   ZH: 维护可编辑样式库及其可序列化的撤销和持久化状态。 */
return JSON.parse(JSON.stringify({defaultStyleId,styleSettings,customStyles}));}
function restoreState(state){
/* EN: Maintain the editable style library and its serializable undo/persistence state.
   ZH: 维护可编辑样式库及其可序列化的撤销和持久化状态。 */
if(!state||!Array.isArray(state.customStyles))return;const before=JSON.stringify(captureState()),next=JSON.parse(JSON.stringify(state));if(before===JSON.stringify(next))return;customStyles=next.customStyles;defaultStyleId=next.defaultStyleId||'classic';styleSettings=next.styleSettings||{};rebuildStyles();persistStyles();applyAll();if(q('#textStylePanel'))refresh();}
function countStyleReferences(id){
/* EN: Resolve cross-sheet style references before deleting a reusable text style.
   ZH: 删除可复用文本样式前处理跨画布的样式引用。 */
return sheets.reduce((n,s)=>{
/* EN: Derive the next value for sheets.reduce: call isLazySheet, JSON.parse, Object.values(d.textboxes||{}).filter.
   ZH: 计算 sheets.reduce 的下一项结果：调用 isLazySheet、JSON.parse、Object.values(d.textboxes||{}).filter。 */
const d=isLazySheet(s)?JSON.parse(s.__lazyJson):s;return n+Object.values(d.textboxes||{}).filter(t=>/* EN: Test an item for Object.values(d.textboxes||{}).filter: return t&&t.textStyleId===id. ZH: 判断 Object.values(d.textboxes||{}).filter 的元素条件：返回 t&&t.textStyleId===id。 */ t&&t.textStyleId===id).length;},0);}
function deleteStyle(id){
/* EN: Resolve cross-sheet style references before deleting a reusable text style.
   ZH: 删除可复用文本样式前处理跨画布的样式引用。 */

 const i=customStyles.findIndex(x=>/* EN: Test an item for customStyles.findIndex: return x.id===id. ZH: 判断 customStyles.findIndex 的元素条件：返回 x.id===id。 */ x.id===id);if(i<0)return;
 if(window.replaceTextStyleReferences){
  const count=countStyleReferences(id);if(count&&!confirm(`删除后，当前导图中 ${count} 个文本框会改用默认样式。继续吗？`))return;
  const before=window.FileHistory&&window.FileHistory.capture?window.FileHistory.capture():null;
  customStyles.splice(i,1);if(defaultStyleId===id)defaultStyleId='classic';window.replaceTextStyleReferences(id,defaultStyleId);
  rebuildStyles();persistStyles();applyAll();if(count){markDirty(false);if(before&&window.FileHistory)window.FileHistory.commit(before);}refresh();return;
 }
 const refs=Object.values(TB).filter(t=>/* EN: Test an item for Object.values(TB).filter: return t&&t.textStyleId===id. ZH: 判断 Object.values(TB).filter 的元素条件：返回 t&&t.textStyleId===id。 */ t&&t.textStyleId===id);if(refs.length&&!confirm(`删除后，当前导图中 ${refs.length} 个文本框会改用默认样式。继续吗？`))return;
 customStyles.splice(i,1);if(defaultStyleId===id)defaultStyleId='classic';refs.forEach(t=>/* EN: Process each item in refs.forEach: update t.textStyleId; return t.textStyleId=defaultStyleId. ZH: 逐项处理 refs.forEach 中的元素：更新 t.textStyleId；返回 t.textStyleId=defaultStyleId。 */ t.textStyleId=defaultStyleId);rebuildStyles();persistStyles();applyAll();if(refs.length)markDirty();refresh();
}
function setDefault(id){
/* EN: setDefault in module: call persistStyles, refresh; update defaultStyleId.
   ZH: module 中的 setDefault：调用 persistStyles、refresh；更新 defaultStyleId。 */
defaultStyleId=id;persistStyles();refresh();}
function applyStyleRecord(t,style,placement='normal'){
/* EN: Apply the selected style to target textboxes or structural nodes with inherited rule settings.
   ZH: 将所选样式及继承规则应用到目标文本框或结构节点。 */
delete t.textStyle;t.textStyleId=style.id;t.textStylePlacement=placement;delete t.textRules;delete t.textSizing;delete t._textRulesOverride;delete t._textSizingOverride;}
function applyStyleToAll(style){
/* EN: Apply the selected style to target textboxes or structural nodes with inherited rule settings.
   ZH: 将所选样式及继承规则应用到目标文本框或结构节点。 */
Object.values(TB).forEach(t=>{
/* EN: Process each item in Object.values(TB).forEach: call applyStyleRecord.
   ZH: 逐项处理 Object.values(TB).forEach 中的元素：调用 applyStyleRecord。 */
if(t&&!t._ghost)applyStyleRecord(t,style,t._nodeAttach?'node':'normal');});render();layout();markDirty();refresh();}
function placeStyleOnAllNodes(style){
/* EN: Apply the selected style to target textboxes or structural nodes with inherited rule settings.
   ZH: 将所选样式及继承规则应用到目标文本框或结构节点。 */
if(typeof window.placeTextStyleOnAllStructuralNodes!=='function')return;const count=window.placeTextStyleOnAllStructuralNodes(style.id);q('.ts-status').textContent=`已在 ${count} 个节点上放置此样式文本框`;refresh();}
function openStyleMenu(style,x,y){
/* EN: openStyleMenu in module: call q, window.translateNode; update m.innerHTML, m.hidden, m.style.left.
   ZH: module 中的 openStyleMenu：调用 q、window.translateNode；更新 m.innerHTML、m.hidden、m.style.left。 */
const m=q('.ts-card-menu');m.innerHTML=`<button data-a="default">设为默认文本框样式</button><button data-a="apply-all">应用于所有文本框</button><button data-a="place-nodes">在所有节点上放置此样式文本框</button><button data-a="delete" class="danger" ${style._builtin?'disabled':''}>删除自定义样式${style._builtin?'（内置不可删除）':''}</button>`;m.hidden=false;m.style.left=Math.min(x,innerWidth-260)+'px';m.style.top=Math.min(y,innerHeight-190)+'px';if(typeof window.translateNode==='function')window.translateNode(m);m.onclick=e=>{
/* EN: m.onclick in openStyleMenu: call e.target.closest, closeStyleMenu, setDefault.
   ZH: openStyleMenu 中的 m.onclick：调用 e.target.closest、closeStyleMenu、setDefault。 */
const a=e.target.closest('button:not(:disabled)')?.dataset.a;if(!a)return;closeStyleMenu();if(a==='default')setDefault(style.id);else if(a==='apply-all')applyStyleToAll(style);else if(a==='place-nodes')placeStyleOnAllNodes(style);else if(a==='delete')deleteStyle(style.id);};}
function refresh(){
/* EN: refresh in module: call shell, q('.ts-tabs').querySelectorAll('.ts-tab').forEach, q('.ts-tabs').querySelectorAll; update q('.ts-grid').innerHTML, q('.ts-grid').onclick, q('.ts-grid').oncontextmenu.
   ZH: module 中的 refresh：调用 shell、q('.ts-tabs').querySelectorAll('.ts-tab').forEach、q('.ts-tabs').querySelectorAll；更新 q('.ts-grid').innerHTML、q('.ts-grid').onclick、q('.ts-grid').oncontextmenu。 */
shell();q('.ts-tabs').querySelectorAll('.ts-tab').forEach(x=>/* EN: Process each item in q('.ts-tabs').querySelectorAll('.ts-tab').forEach: call x.classList.toggle; return x.classList.toggle('on',x.dataset.tab===currentTab). ZH: 逐项处理 q('.ts-tabs').querySelectorAll('.ts-tab').forEach 中的元素：调用 x.classList.toggle；返回 x.classList.toggle('on',x.dataset.tab===currentTab)。 */ x.classList.toggle('on',x.dataset.tab===currentTab));q('.ts-body').querySelectorAll('.ts-page').forEach(x=>/* EN: Process each item in q('.ts-body').querySelectorAll('.ts-page').forEach: call x.classList.toggle; return x.classList.toggle('on',x.dataset.page===currentTab). ZH: 逐项处理 q('.ts-body').querySelectorAll('.ts-page').forEach 中的元素：调用 x.classList.toggle；返回 x.classList.toggle('on',x.dataset.page===currentTab)。 */ x.classList.toggle('on',x.dataset.page===currentTab));const a=selected(),t=a.length?TB[a[0]]:{};
 if(currentTab==='styles'){
  q('.ts-grid').innerHTML=styles.map(s=>/* EN: Derive the next value for styles.map: call esc, tr. ZH: 计算 styles.map 的下一项结果：调用 esc、tr。 */ `<button class="ts-card ${t&&t.textStyleId===s.id?'on':''} ${defaultStyleId===s.id?'default':''}" data-id="${esc(s.id)}"><span class="ts-sample" style="background:${s.replaceFrame?'transparent':esc(s.bg)};border-color:${s.replaceFrame?'transparent':esc(s.border)};color:${esc(s.color)};border-radius:${Number(s.radius)||0}px;box-shadow:${s.replaceFrame?'none':esc(s.shadow||'none')};font-family:${esc(s.fontFamily||'inherit')};font-weight:${s.fontWeight||400}">${esc(tr('示例文字'))}</span><span class="ts-name">${esc(s._builtin?tr(s.name):s.name)}</span><span class="ts-scope">${esc(tr(s._builtin?'内置样式':'自定义样式'))}</span></button>`).join('');
  q('.ts-grid').querySelectorAll('.ts-card').forEach(b=>{
/* EN: Process each item in q('.ts-grid').querySelectorAll('.ts-card').forEach: call getStyle, renderArt, b.querySelector.
   ZH: 逐项处理 q('.ts-grid').querySelectorAll('.ts-card').forEach 中的元素：调用 getStyle、renderArt、b.querySelector。 */
const s=getStyle(b.dataset.id);if(s.layers&&s.layers.length)renderArt(b.querySelector('.ts-sample'),s);});
  q('.ts-grid').onclick=e=>{
/* EN: q('.ts-grid').onclick in refresh: call e.target.closest, selected, q; update q('.ts-status').textContent.
   ZH: refresh 中的 q('.ts-grid').onclick：调用 e.target.closest、selected、q；更新 q('.ts-status').textContent。 */
const b=e.target.closest('[data-id]');if(!b)return;const a=selected();if(!a.length){q('.ts-status').textContent='请先选中一个或多个文本框';return;}const preset=getStyle(b.dataset.id);a.forEach(id=>{
/* EN: Process each item in a.forEach: call applyStyleRecord, render.
   ZH: 逐项处理 a.forEach 中的元素：调用 applyStyleRecord、render。 */
applyStyleRecord(TB[id],preset,TB[id]._nodeAttach?'node':placementMode);render([id]);});layout();markDirty();refresh();};
  q('.ts-grid').oncontextmenu=e=>{
/* EN: q('.ts-grid').oncontextmenu in refresh: call e.target.closest, e.preventDefault, e.stopPropagation.
   ZH: refresh 中的 q('.ts-grid').oncontextmenu：调用 e.target.closest、e.preventDefault、e.stopPropagation。 */
const b=e.target.closest('[data-id]');if(!b)return;e.preventDefault();e.stopPropagation();openStyleMenu(getStyle(b.dataset.id),e.clientX,e.clientY);};
 }
 if(currentTab==='font'){const f=t.textFormat||{},currentFont=f.fontFamily||'system-ui';q('[data-page="font"]').innerHTML=`<div class="ts-section"><h4>选中文本框的字体</h4><label class="ts-row"><span>字体</span><select data-f="fontFamily">${fontOptions(currentFont)}</select></label><label class="ts-row"><span>字号</span><input data-f="fontSize" type="number" min="8" max="96" value="${f.fontSize||14}"></label><label class="ts-row"><span>文字颜色</span><input data-f="color" type="color" value="${esc(f.color||'#2c3140')}"></label><label class="ts-row"><span>对齐</span><select data-f="align"><option value="left">左对齐</option><option value="center">居中</option><option value="right">右对齐</option></select></label><label class="ts-row"><span>字间距</span><input data-f="letterSpacing" type="number" min="-2" max="20" step="0.5" value="${f.letterSpacing||0}"></label><label class="ts-row"><span>行高</span><input data-f="lineHeight" type="number" min="0.8" max="3" step="0.1" value="${f.lineHeight||1.4}"></label><div class="ts-checks"><label><input data-f="bold" type="checkbox" ${f.bold?'checked':''}> 粗体</label><label><input data-f="italic" type="checkbox" ${f.italic?'checked':''}> 斜体</label><label><input data-f="underline" type="checkbox" ${f.underline?'checked':''}> 下划线</label><label><input data-f="wrap" type="checkbox" ${f.wrap!==false?'checked':''}> 自动换行</label></div><p class="ts-hint">字体列表来自当前 Windows 系统，不随应用打包字体文件。</p></div>`;const page=q('[data-page="font"]');page.querySelector('[data-f="fontFamily"]').value=currentFont;page.querySelector('[data-f="align"]').value=f.align||'left';page.onchange=e=>{
/* EN: page.onchange in refresh: call e.target.closest, patch; update v.
   ZH: refresh 中的 page.onchange：调用 e.target.closest、patch；更新 v。 */
const el=e.target.closest('[data-f]');if(!el)return;let v=el.type==='checkbox'?el.checked:el.value;if(el.type==='number')v=Number(v);patch('textFormat',el.dataset.f,v);};}
 if(currentTab==='rules'){const r=effectiveRules(t),z=effectiveSizing(t),s=styleData(t),b=renderBounds(s),fallbackAspect=Math.max(.1,(b.w*9)/(b.h*5.2)),rawUiMode=z.mode||s.textSizing&&s.textSizing.mode||'',storedUiAspect=Number(z.aspect||s.textSizing&&s.textSizing.aspect),aspect=rawUiMode==='auto'?fallbackAspect:(storedUiAspect>0?storedUiAspect:fallbackAspect);q('[data-page="rules"]').innerHTML=`<div class="ts-section"><h4>尺寸策略</h4><label class="ts-row"><span>尺寸策略</span><select data-z="mode"><option value="uniform">固定长宽比</option><option value="stretch">自由拉伸</option></select></label><label class="ts-row" data-aspect-row><span>长宽比</span><input data-z="aspect" type="number" min="0.1" max="20" step="0.01" value="${Math.round(aspect*100)/100}"></label><p class="ts-hint">文字会先使用画出的文字区域；只有放不下时，文本框和全部图层才一起扩大（最大 ${MAX_TEXT_STYLE_DIMENSION}px）。</p></div><div class="ts-section"><h4>输入限制</h4><label class="ts-row"><span>最多字符</span><input data-r="maxLength" type="number" min="0" max="${MAX_TEXT_RULE_CHARACTERS}" value="${r.maxLength||0}" placeholder="0=不限"></label><label class="ts-row"><span>允许类型</span><select data-r="type"><option value="any">任意文字</option><option value="number">仅数字</option><option value="letter">仅字母</option><option value="chinese">仅中文</option><option value="alnum">数字和字母</option><option value="regex">自定义正则</option></select></label><label class="ts-row"><span>自定义正则</span><input data-r="pattern" value="${esc(r.pattern||'')}" placeholder="例如 [A-Z0-9]*"></label><label class="ts-row"><span>空值</span><select data-r="required"><option value="false">允许为空</option><option value="true">不允许为空</option></select></label><p class="ts-hint">按 Unicode code point 计数；正则始终匹配完整文本，错误正则会阻止应用。</p></div>`;const p=q('[data-page="rules"]'),modeSelect=p.querySelector('[data-z="mode"]'),syncModeUi=()=>{
/* EN: syncModeUi in refresh: call p.querySelector; update p.querySelector('[data-aspect-row]').hidden.
   ZH: refresh 中的 syncModeUi：调用 p.querySelector；更新 p.querySelector('[data-aspect-row]').hidden。 */
p.querySelector('[data-aspect-row]').hidden=modeSelect.value!=='uniform';};modeSelect.value=sizingMode(z.mode);p.querySelector('[data-r="type"]').value=r.type||'any';p.querySelector('[data-r="required"]').value=String(!!r.required);syncModeUi();p.onchange=e=>{
/* EN: p.onchange in refresh: call e.target.closest, el.setCustomValidity, el.reportValidity; update v.
   ZH: refresh 中的 p.onchange：调用 e.target.closest、el.setCustomValidity、el.reportValidity；更新 v。 */
let el=e.target.closest('[data-z],[data-r]');if(!el)return;let v=el.type==='number'?Number(el.value):el.value;if(el.dataset.r==='required')v=v==='true';if(el.dataset.r==='maxLength')v=Math.max(0,Math.min(MAX_TEXT_RULE_CHARACTERS,Number(v)||0));if(el.dataset.r==='pattern'&&v){try{new RegExp(`^(?:${v})$`,'u');el.setCustomValidity('');}catch(err){el.setCustomValidity('正则表达式无效：'+err.message);el.reportValidity();return;}}patch(el.dataset.z?'textSizing':'textRules',el.dataset.z||el.dataset.r,v);if(el.dataset.z==='mode')syncModeUi();};}
 q('.ts-status').textContent=selected().length?(typeof window.t==='function'?window.t('已选中文本框：'):'已选中文本框：')+selected().length:(typeof window.t==='function'?window.t('请选择文本框'):'请选择文本框');if(typeof window.translateNode==='function')window.translateNode(q('#textStylePanel'));}
function allowed(text,r){
/* EN: Apply character type, full-match regex and maximum-length rules to prospective input.
   ZH: 对待输入文本执行字符类型、正则全文匹配与最大长度规则。 */
return window.KnotJotTextInputRules.allowed(text,r);}
function textSelection(inner){
/* EN: Evaluate prospective edits against input rules and schedule text-frame fitting after accepted input.
   ZH: 按输入规则评估待编辑内容，并在接受输入后安排文字外框自适应。 */
const cur=inner.textContent||'',sel=getSelection();if(!sel||!sel.rangeCount)return{cur,start:cur.length,end:cur.length};const range=sel.getRangeAt(0);if(!inner.contains(range.startContainer)||!inner.contains(range.endContainer))return{cur,start:cur.length,end:cur.length};try{const head=document.createRange();head.selectNodeContents(inner);head.setEnd(range.startContainer,range.startOffset);const start=head.toString().length;return{cur,start,end:start+range.toString().length};}catch(_){return{cur,start:cur.length,end:cur.length};}}
function proposedText(inner,inserted){
/* EN: Evaluate prospective edits against input rules and schedule text-frame fitting after accepted input.
   ZH: 按输入规则评估待编辑内容，并在接受输入后安排文字外框自适应。 */
const x=textSelection(inner);return x.cur.slice(0,x.start)+String(inserted==null?'':inserted)+x.cur.slice(x.end);}
function rejectInput(inner){
/* EN: Evaluate prospective edits against input rules and schedule text-frame fitting after accepted input.
   ZH: 按输入规则评估待编辑内容，并在接受输入后安排文字外框自适应。 */
inner.animate([{background:'#ffd8d8'},{background:''}],{duration:240});}
document.addEventListener('beforeinput',e=>{
/* EN: Handle beforeinput on document.addEventListener: call e.target.closest, effectiveRules, e.inputType.startsWith.
   ZH: 处理 document.addEventListener 的 beforeinput 事件或通道：调用 e.target.closest、effectiveRules、e.inputType.startsWith。 */
const inner=e.target.closest&&e.target.closest('.card-inner[contenteditable="true"]'),id=editing;if(!inner||!id||!TB[id])return;const r=effectiveRules(TB[id]);if(e.inputType&&e.inputType.startsWith('delete'))return;if(e.inputType==='insertFromPaste'&&e.data==null)return;const inserted=e.inputType==='insertParagraph'||e.inputType==='insertLineBreak'?'\n':e.data||'';if(!allowed(proposedText(inner,inserted),r)){e.preventDefault();rejectInput(inner);}},true);
document.addEventListener('paste',e=>{
/* EN: Handle paste on document.addEventListener: call e.target.closest, e.clipboardData.getData, allowed.
   ZH: 处理 document.addEventListener 的 paste 事件或通道：调用 e.target.closest、e.clipboardData.getData、allowed。 */
const inner=e.target.closest&&e.target.closest('.card-inner[contenteditable="true"]'),id=editing;if(!inner||!id||!TB[id])return;const text=e.clipboardData&&e.clipboardData.getData('text/plain')||'';if(!allowed(proposedText(inner,text),effectiveRules(TB[id]))){e.preventDefault();e.stopImmediatePropagation();rejectInput(inner);}},true);
let editFitFrame=0,pendingFitId=null;
function scheduleEditingFit(id){
/* EN: Evaluate prospective edits against input rules and schedule text-frame fitting after accepted input.
   ZH: 按输入规则评估待编辑内容，并在接受输入后安排文字外框自适应。 */
pendingFitId=id;if(editFitFrame)return;editFitFrame=requestAnimationFrame(()=>{
/* EN: Callback for requestAnimationFrame: call window.applyTextStyleToNode, layout; update editFitFrame, pendingFitId.
   ZH: requestAnimationFrame 的回调：调用 window.applyTextStyleToNode、layout；更新 editFitFrame、pendingFitId。 */
editFitFrame=0;const current=pendingFitId;pendingFitId=null;if(current&&editing===current&&TB[current]&&els[current]){window.applyTextStyleToNode(current);layout();}});}
document.addEventListener('input',e=>{
/* EN: Handle input on document.addEventListener: call e.target.closest, effectiveRules, e.inputType.startsWith; update inner.textContent, inner._tsLastAccepted.
   ZH: 处理 document.addEventListener 的 input 事件或通道：调用 e.target.closest、effectiveRules、e.inputType.startsWith；更新 inner.textContent、inner._tsLastAccepted。 */
const inner=e.target.closest&&e.target.closest('.card-inner[contenteditable="true"]'),id=editing;if(!inner||!id||!TB[id])return;const value=inner.textContent||'',r=effectiveRules(TB[id]),deleting=e.inputType&&e.inputType.startsWith('delete');if(!deleting&&!allowed(value,r)){inner.textContent=inner._tsLastAccepted==null?(TB[id].text||''):inner._tsLastAccepted;const range=document.createRange();range.selectNodeContents(inner);range.collapse(false);const sel=getSelection();sel.removeAllRanges();sel.addRange(range);rejectInput(inner);}else inner._tsLastAccepted=value;scheduleEditingFit(id);},true);
async function loadExternal(){
/* EN: loadExternal in module: call api.loadTextStyles, localStorage.getItem, JSON.parse; update r, q('.ts-status').textContent, customStyles.
   ZH: module 中的 loadExternal：调用 api.loadTextStyles、localStorage.getItem、JSON.parse；更新 r、q('.ts-status').textContent、customStyles。 */
try{let r;if(window.api&&api.loadTextStyles)r=await api.loadTextStyles();else{const x=localStorage.getItem('bmap.textStyles');r=x?JSON.parse(x):null;}if(r&&r.error){if(q('.ts-status'))q('.ts-status').textContent=r.error;return;}if(r&&Array.isArray(r.styles)){customStyles=r.styles.filter(x=>/* EN: Test an item for r.styles.filter: return x&&x.id. ZH: 判断 r.styles.filter 的元素条件：返回 x&&x.id。 */ x&&x.id);defaultStyleId=r.defaultStyleId||'classic';styleSettings=r.styleSettings||{};externalPath=r.path||'';rebuildStyles();applyAll();if(q('#textStylePanel'))refresh();}}catch(e){if(q('.ts-status'))q('.ts-status').textContent='样式库读取失败：'+e.message;} }
function init(){
/* EN: init in module: call shell, loadSystemFonts, q; update btn.onclick, window.__textRulesCommitWrapped, startEdit.
   ZH: module 中的 init：调用 shell、loadSystemFonts、q；更新 btn.onclick、window.__textRulesCommitWrapped、startEdit。 */
shell();loadSystemFonts();const btn=q('#textStyleBtn');if(btn)btn.onclick=e=>{
/* EN: btn.onclick in init: call e.stopPropagation, open.
   ZH: init 中的 btn.onclick：调用 e.stopPropagation、open。 */
e.stopPropagation();open('styles');};if(!window.__textRulesCommitWrapped){window.__textRulesCommitWrapped=true;const baseStart=startEdit,baseCommit=commitEdit;startEdit=function(id){
/* EN: Enter or commit inline textbox editing while respecting locks, input rules and history.
   ZH: 在遵循锁定、输入规则和历史记录的前提下进入或提交文本框行内编辑。 */
baseStart(id);const inner=els[id]&&els[id]._inner;if(editing===id&&inner)inner._tsLastAccepted=inner.textContent||'';};commitEdit=function(id){
/* EN: Enter or commit inline textbox editing while respecting locks, input rules and history.
   ZH: 在遵循锁定、输入规则和历史记录的前提下进入或提交文本框行内编辑。 */
const t=TB[id],inner=els[id]&&els[id]._inner;if(t&&inner){const r=effectiveRules(t),value=inner.textContent||'';if((r.required&&!value.trim())||!allowed(value,r)){inner.textContent=t.text||'';rejectInput(inner);}}return baseCommit(id);};}if(window.api&&api.onTextStylesChanged)api.onTextStylesChanged(()=>/* EN: Callback for api.onTextStylesChanged: call loadExternal; return loadExternal(). ZH: api.onTextStylesChanged 的回调：调用 loadExternal；返回 loadExternal()。 */ loadExternal());loadExternal();}
window.TextStyleFeature={open,close,refresh,loadExternal,BUILTINS,captureState,restoreState,getDefaultStyleId:()=>/* EN: getDefaultStyleId in module: return defaultStyleId. ZH: module 中的 getDefaultStyleId：返回 defaultStyleId。 */ defaultStyleId,createAtStructuralNode:ref=>/* EN: createAtStructuralNode in module: call window.createStructuralNodeTextbox; return window.createStructuralNodeTextbox&&window.createStructuralNodeTextbox(ref,defaultStyleId,true). ZH: module 中的 createAtStructuralNode：调用 window.createStructuralNodeTextbox；返回 window.createStructuralNodeTextbox&&window.createStructuralNodeTextbox(ref,defaultStyleId,true)。 */ window.createStructuralNodeTextbox&&window.createStructuralNodeTextbox(ref,defaultStyleId,true),openForNode:id=>{
/* EN: openForNode in module: call selectTb, open.
   ZH: module 中的 openForNode：调用 selectTb、open。 */
if(TB[id])selectTb(id);open('styles',TB[id]._nodeAttach?'node':'normal');},applyStyleToAll:id=>/* EN: Apply the selected style to target textboxes or structural nodes with inherited rule settings. ZH: 将所选样式及继承规则应用到目标文本框或结构节点。 */ applyStyleToAll(getStyle(id)),placeStyleOnAllNodes:id=>/* EN: Apply the selected style to target textboxes or structural nodes with inherited rule settings. ZH: 将所选样式及继承规则应用到目标文本框或结构节点。 */ placeStyleOnAllNodes(getStyle(id))};
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();
