const {app,BrowserWindow,session}=require('electron');
const fs=require('fs'),path=require('path');
const output=path.resolve(process.argv[2]);fs.mkdirSync(output,{recursive:true});
app.setPath('userData',path.join(output,'electron-profile'));app.commandLine.appendSwitch('disable-background-networking');app.commandLine.appendSwitch('force-device-scale-factor','1');
// EN: Exercise the main app's unfiltered box-shadow variable with independent Chromium pixels and computed acceptance. ZH: 用独立 Chromium 像素和计算后接受状态验证主程序未过滤的 box-shadow 变量。
const cases=[
 {id:'outer-transparent',shadow:'8px 12px 0 3px #00000080'},
 {id:'inset-solid',shadow:'inset 9px 6px 0 4px #f008',bg:'#ffffff'},
 {id:'inset-transparent',shadow:'inset -9px -6px 0 4px rgb(0 60 255 / 60%)'},
 {id:'rounded-mixed',shadow:'inset 9px 6px 8px 4px #f008, -12px 9px 10px 3px #06f9, 6px -4px 0 -2px #0a0',radius:24,bg:'#ffffff80',border:'#222',borderWidth:3},
 {id:'negative-spread',shadow:'-13px 7px 0 -8px rebeccapurple',radius:20},
 {id:'first-on-top',shadow:'0 8px 0 8px #ff000080, 0 12px 0 12px blue'},
 {id:'hsl-em-calc',shadow:'hsl(120deg 100% 25% / 75%) calc(1em + 2px) -0.5em 4px 2px'},
 {id:'current-color',shadow:'3px 9px 2px',color:'#7030a0'},
 {id:'many-layers',shadow:Array.from({length:12},(_,i)=>`${i-6}px ${i+2}px 0 1px rgba(20,80,200,.2)`).join(',')},
 {id:'wide-length',shadow:'600px 0 0 0 red, inset 6px 0 0 blue'},
 {id:'none',shadow:'none',bg:'#ffffff80',radius:20},
 {id:'invalid-whole-value',shadow:'inset 0 0 0 8px red, 1px garbage blue'},
 {id:'negative-blur-invalid',shadow:'0 0 -3px red'},
 {id:'duplicate-color-invalid',shadow:'red blue 3px 4px'},
 {id:'variable',shadow:'inset 0 0 0 9px var(--ts-color)',color:'#e03020'},
 {id:'inherited-none',shadow:'inherit'},
 {id:'rgb-percent',shadow:'6px 4px 3px rgb(100% 20% 0% / 35%)'},
 {id:'hwb-color',shadow:'inset 0 0 0 7px hwb(240 10% 15% / .5)'},
 {id:'oklch-color',shadow:'-8px 6px 4px oklch(60% .15 30 / .6)'},
 {id:'color-mix',shadow:'inset 3px 2px 4px 3px color-mix(in srgb, red 60%, blue)'},
 {id:'uppercase-inset',shadow:'INSET 3px 6px 0 2px RED',radius:18},
 {id:'inset-cover',shadow:'inset 0 0 0 100px #0066ff80',radius:30},
 {id:'inset-negative-spread',shadow:'inset 8px 14px 6px -5px #900b',radius:22,bg:'#ccc7'},
 {id:'oversized-radius',shadow:'-4px -6px 4px 5px #308d',radius:300},
 {id:'physical-lengths',shadow:'.1in -3pt 2mm .1cm #3060a080'},
 {id:'relative-rem',shadow:'1rem .5em 2px 1px #6099'},
 {id:'clamp-lengths',shadow:'clamp(2px,1em,30px) min(12px,2em) max(1px,3px) red'},
 {id:'unitless-nonzero-invalid',shadow:'3 5 blue'},
 {id:'percentage-invalid',shadow:'10% 0 blue'},
 {id:'unresolved-variable',shadow:'0 5px var(--missing)'},
 {id:'variable-fallback',shadow:'inset 0 0 0 8px var(--missing, green)'},
 {id:'css-comment',shadow:'0 /* offset */ 6px 3px red'},
];
// EN: Keep browser requests local and use the actual main CSS rule, while making card geometry explicit for pixel comparison. ZH: 阻止浏览器外部请求并使用真实主程序 CSS 规则，同时明确卡片几何以比较像素。
app.whenReady().then(async()=>{
 session.defaultSession.webRequest.onBeforeRequest((details,callback)=>callback({cancel:!details.url.startsWith('data:')&&!details.url.startsWith('about:')}));
 const mainRoot=path.resolve(__dirname,'../../../../../knotjot/v1.0.1/source');
 const css=fs.readFileSync(path.join(mainRoot,'src/renderer/text-styles.css'),'utf8');
 const index=fs.readFileSync(path.join(mainRoot,'index.html'),'utf8');
 const context=index.match(/:root\s*\{[^}]+\}/)[0]+index.match(/html,body\{[^}]+\}/)[0];
 const rule=css.match(/\.ts-custom \.card\{[^}]+\}/)[0];
 fs.writeFileSync(path.join(output,'main-frame-rule.css'),rule);
 const win=new BrowserWindow({show:false,transparent:true,backgroundColor:'#00000000',width:320,height:240,webPreferences:{offscreen:true,contextIsolation:true,nodeIntegration:false}});
 const results=[];
 for(const item of cases){
  Object.assign(item,{bg:item.bg||'transparent',border:item.border||'transparent',borderWidth:item.borderWidth||0,radius:item.radius||0,color:item.color||'#000000',fontSize:14});
  const html=`<!doctype html><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'"><style>${context}html,body{margin:0;background:transparent} .card{position:absolute!important;left:80px;top:80px;box-sizing:border-box;width:160px;height:80px;border-style:solid}${rule}</style><div class="ts-custom"><div class="card"></div></div>`;
  fs.writeFileSync(path.join(output,item.id+'.html'),html);
  await win.loadURL('data:text/html;charset=utf-8,'+encodeURIComponent(html));
  const computed=await win.webContents.executeJavaScript(`(()=>{const e=document.querySelector('.ts-custom'),s=${JSON.stringify(item)};for(const [k,v] of Object.entries({'--ts-bg':s.bg,'--ts-border':s.border,'--ts-border-width':s.borderWidth+'px','--ts-radius':s.radius+'px','--ts-color':s.color,'--ts-shadow':s.shadow}))e.style.setProperty(k,v);return getComputedStyle(document.querySelector('.card')).boxShadow})()`);
  await win.webContents.executeJavaScript('new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))');
  const png=await win.webContents.capturePage({x:0,y:0,width:320,height:240});fs.writeFileSync(path.join(output,item.id+'.png'),png.toPNG());results.push({...item,computed});
 }
 // EN: Retain portable editor fixtures alongside browser evidence so the controller can deliver them in the desktop test folder.
 // ZH: 在浏览器证据旁保留可移植编辑器样本，供控制任务交付到桌面测试目录。
 fs.writeFileSync(path.join(output,'cases.json'),JSON.stringify(results,null,2));
 fs.writeFileSync(path.join(output,'frame-shadow-cases.knotjot-textstyle'),JSON.stringify({format:'knotjot-text-styles',version:1,styles:results.map(s=>({id:'shadow-'+s.id,name:s.id,bg:s.bg,border:s.border,borderWidth:s.borderWidth,color:s.color,fontSize:s.fontSize,radius:s.radius,shadow:s.shadow,replaceFrame:false,layers:[]}))},null,2));
 win.destroy();app.quit();
}).catch(error=>{console.error(error);app.exit(1)});
