const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');

// EN: Keep profiles, fixture documents and measured results beside the patch preview; never touch historical source or user preferences.
// ZH: 将测试配置、样本文档和测量结果保存在补丁预览目录，不修改历史源码或用户设置。
const artifacts = path.resolve(__dirname, '../../preview/testing/layout');
const tag = process.env.KNOTJOT_LAYOUT_TAG || 'current';
const sourceDir = process.env.KNOTJOT_LAYOUT_SOURCE || path.resolve(__dirname, '..');
fs.mkdirSync(artifacts, { recursive: true });
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData', path.join(artifacts, 'profile-' + tag + '-' + process.pid));
process.env.KNOTJOT_TEXT_STYLES_DIR = path.join(artifacts, 'styles-' + process.pid);
process.env.KNOTJOT_INTEGRATION_TEST = '1';
require(path.join(sourceDir, 'main.js'));

async function runLayoutRegression() {
  // EN: Execute real renderer layout and menu actions, then inspect DOM rectangles and serialized documents independently of sizing helpers.
  // ZH: 执行真实渲染器布局和菜单操作，再独立检查 DOM 矩形及序列化文档，不复用尺寸计算函数。
  const timer = setTimeout(() => app.exit(2), 45000);
  const win = new BrowserWindow({ show: false, width: 1440, height: 900, webPreferences: { offscreen: true, backgroundThrottling: false, contextIsolation: true, nodeIntegration: false, preload: path.join(sourceDir, 'preload.js') } });
  try {
    await win.loadFile(path.join(sourceDir, 'index.html'));
    const result = await win.webContents.executeJavaScript(`(async () => {
      // EN: Fixtures deliberately mix uneven, multiline and nested branches; all geometry comes from rendered cards and SVG frames.
      // ZH: 样本刻意混合不均匀、多行和嵌套分支，全部几何来自已渲染卡片和 SVG 框架。
      const results = [], fixtures = {}, metrics = {};
      hideStart();
      const pause = () => new Promise(resolve => setTimeout(resolve, 120));
      const check = (condition, message, details) => { if (!condition) throw new Error(message + ' | ' + JSON.stringify(details || {})); };
      const reset = () => { clearActiveDom(); TB={};BR={};RB={};FG={};LK={};roots=[];idc=0;selSet.clear();loadExtras({});docType='brace';applyDocType();AppearanceEditor.loadDocumentAppearance({background:{enabled:false}}); };
      const add = (text, parent, st) => {
        const id=newTextbox(text);TB[id].x=1000;TB[id].y=1200;if(st)TB[id].struct=st;
        if(parent){let bid=TB[parent].brace;if(!bid){bid=nid('b');TB[parent].brace=bid;BR[bid]={id:bid,parentTb:parent,children:[],locked:false};}TB[id].parentBrace=bid;BR[bid].children.push(id);}else roots.push(id);return id;
      };
      const descendants = id => [id,...(tbKids(id)||[]).flatMap(descendants)];
      const rect = id => {const r=els[id]._card.getBoundingClientRect();return {id,x:r.x,y:r.y,right:r.right,bottom:r.bottom,width:r.width,height:r.height};};
      const overlaps = (a,b) => Math.min(a.right,b.right)-Math.max(a.x,b.x)>.5 && Math.min(a.bottom,b.bottom)-Math.max(a.y,b.y)>.5;
      const run = async (name, fn) => {try{await fn();results.push({name,pass:true});}catch(error){results.push({name,pass:false,error:error.message});}};
      await run('brace-spider-menu-roundtrip', async () => {
        reset();const root=add('Switch root');const child=add('Keep text and media',root);add('Nested child',child);add('Second branch',root);
        TB[root]._offX=45;TB[root]._offY=-35;TB[child]._offX=20;
        TB[child].tags=['preserved'];TB[child].note='Keep this note';TB[child].img={src:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=',w:40,h:40};
        update();await pause();const hierarchySnapshot=()=>JSON.stringify(Object.values(BR).map(b=>({id:b.id,parentTb:b.parentTb,children:b.children,locked:!!b.locked})).sort((a,b)=>a.id.localeCompare(b.id)));const hierarchy=hierarchySnapshot();const nodeIds=Object.keys(TB);const before=nodeIds.map(id=>({id,x:TB[id]._x,y:TB[id]._cy}));
        selSet.clear();selSet.add(root);openStructMenu();const entry=structMenu.querySelector('[data-struct="spider"]');
        check(entry && getComputedStyle(entry).display!=='none','Visible spider entry missing');entry.click();await pause();
        check(docType==='spider'&&document.body.classList.contains('spider'),'Did not enter existing spider document mode');
        check(before.every(p=>Math.abs(TB[p.id]._x-p.x)<.1&&Math.abs(TB[p.id]._cy-p.y)<.1),'Switch moved existing cards');
        check(Object.keys(LK).length===nodeIds.length-1,'Hierarchy edges were not converted to spider links',Object.keys(LK));
        check(getComputedStyle(structBtn).display!=='none','Structure menu is hidden in spider mode');
        fixtures.spider=serialize();check(applyLoadedDocument(JSON.parse(fixtures.spider),'layout-spider'),'Spider document reload failed');await pause();
        check(docType==='spider'&&Object.keys(TB).length===nodeIds.length,'Reload lost spider mode or nodes');
        openStructMenu();structMenu.querySelector('[data-struct="brace"]').click();await pause();
        check(docType==='brace','Did not return to brace mode');
        check(hierarchySnapshot()===hierarchy,'Roundtrip changed hierarchy',{before:hierarchy,after:hierarchySnapshot()});
        check(before.every(p=>Math.abs(TB[p.id]._x-p.x)<.1&&Math.abs(TB[p.id]._cy-p.y)<.1),'Roundtrip changed card coordinates with stored manual offsets',{before,after:nodeIds.map(id=>({id,x:TB[id]._x,y:TB[id]._cy}))});
        check(TB[child].tags[0]==='preserved'&&TB[child].note==='Keep this note'&&TB[child].img.w===40,'Roundtrip lost metadata or image');
        fixtures.brace=serialize();metrics.switching={nodes:nodeIds.length,links:Object.keys(LK).length,positionsPreserved:true};
      });
      await run('spider-cycle-forest-and-switch-history', async () => {
        reset();const a=add('A'),b=add('B'),c=add('C'),isolated=add('Isolated');docType='spider';applyDocType();
        LK={l1:{id:'l1',a,b,aAng:0,bAng:Math.PI,locked:false},l2:{id:'l2',a:b,b:c,aAng:0,bAng:Math.PI,locked:false},l3:{id:'l3',a:c,b:a,aAng:0,bAng:Math.PI,locked:true,text:'Keep cross-link'}};
        update();initHistory(true);setStruct(a,'brace');await pause();
        const visited=roots.flatMap(descendants);check(visited.length===4&&new Set(visited).size===4&&roots.includes(isolated),'Free graph conversion lost a node or introduced a cycle',{roots,visited});
        check(Object.keys(LK).length===3&&LK.l3.text==='Keep cross-link'&&LK.l3.locked,'Free graph conversion discarded a cross-link');
        undo();await pause();check(docType==='spider'&&!Object.keys(BR).length,'Undo did not restore native spider graph');redo();await pause();check(docType==='brace'&&Object.keys(BR).length>0,'Redo did not restore converted forest');
        setStruct(a,'spider');setStruct(a,'spider');check(Object.keys(LK).length===3,'Repeated conversion duplicated links');
        readonly=true;setStruct(a,'brace');readonly=false;check(docType==='spider','Read-only mode permitted conversion');
        metrics.nativeSpider={nodes:visited.length,links:Object.keys(LK).length,history:true,readonly:true};
      });
      await run('converted-spider-finite-canvas-keeps-relative-positions', async () => {
        // EN: Finite-canvas correction must translate every free spider card equally, including former hierarchy descendants.
        // ZH: 有限画布修正必须等量平移全部蜘蛛网自由卡片，包括原层级后代。
        reset();const root=add('Finite root'),child=add('Child',root);add('Grandchild',child);update();setStruct(root,'spider');
        const before=Object.values(TB).map(node=>({id:node.id,x:node._x,y:node._cy}));AppearanceEditor.loadDocumentAppearance({background:{enabled:true,mode:'limited',canvasWidth:1200,canvasHeight:800}});layout();
        const translations=before.map(node=>({id:node.id,x:TB[node.id]._x-node.x,y:TB[node.id]._cy-node.y}));check(translations.every(delta=>Math.abs(delta.x-translations[0].x)<.1&&Math.abs(delta.y-translations[0].y)<.1),'Finite correction changed relative spider positions',translations);
        metrics.finiteSpider={nodes:before.length,translations};
      });
      await run('timeline-nested-opposite-branches', async () => {
        reset();const root=add('Timeline',null,'timeline');
        for(let i=0;i<5;i++){const branch=add('Branch '+i,root,['orgD','matrix','tree','timeline','logicR'][i]);for(let j=0;j<i+2;j++){const child=add('Item '+i+'-'+j+'\\n'+('wide '.repeat(j+1)),branch);if(j===0)for(let k=0;k<3;k++)add('Nested '+k+'\\nDetail\\nMore detail',child,'logicR');}}
        update();await pause();const children=tbKids(root);const parentRect=rect(root),axis=(parentRect.y+parentRect.bottom)/2;const violations=[];
        children.forEach((id,i)=>descendants(id).forEach(cid=>{const r=rect(cid);if((i%2===0&&r.bottom>=axis-.5)||(i%2===1&&r.y<=axis+.5))violations.push({kind:'crosses-main-axis',branch:i,card:r,axis});}));
        const cards=Object.keys(TB).map(rect);for(let i=0;i<cards.length;i++)for(let j=i+1;j<cards.length;j++)if(overlaps(cards[i],cards[j]))violations.push({kind:'card-overlap',a:cards[i],b:cards[j]});
        markDirty(false);fixtures.timeline=serialize();metrics.timeline={nodes:cards.length,violations,rectangles:cards};
        check(!violations.length,'Timeline subtree crosses the main axis or another card',violations.slice(0,5));
      });
      await run('timeline-connectors-clear-node-bodies', async () => {
        // EN: Sample real SVG axis connectors and compare screen coordinates against card interiors, excluding the intended border contact.
        // ZH: 采样真实 SVG 时间轴连线，将屏幕坐标与卡片内部比较，排除预期的边框接触点。
        const crossings=[];const cards=Object.keys(TB).map(rect);
        for(const bid of Object.keys(BR)){if(!['timeline','fishbone'].includes(effStruct(BR[bid].parentTb)))continue;const paths=svg.querySelectorAll('path[data-brace-visual="'+bid+'"]');for(const line of paths){const length=line.getTotalLength();const transform=line.getScreenCTM();for(let d=0;d<=length;d+=3){const point=line.getPointAtLength(d);const p=new DOMPoint(point.x,point.y).matrixTransform(transform);for(const card of cards){if(p.x>card.x+3&&p.x<card.right-3&&p.y>card.y+3&&p.y<card.bottom-3){crossings.push({brace:bid,node:card.id,x:p.x,y:p.y});break;}}if(crossings.length>10)break;}}}
        metrics.timeline.connectorCrossings=crossings;check(!crossings.length,'Axis connector crosses a card interior',crossings);
      });
      await run('matrix-descendants-inside-owning-frames', async () => {
        reset();const root=add('Matrix',null,'matrix');
        for(let i=0;i<5;i++){const branch=add('Group '+i,root);for(let j=0;j<i+1;j++){const child=add('Cell '+i+'-'+j+'\\n'+('long '.repeat(j+1)),branch);if(j===0)for(let k=0;k<3;k++)add('Deep '+k+'\\nNested content',child);}}
        update();await pause();const violations=[];const frames=[];
        for(const id of Object.keys(TB)){if(!TB[id].brace)continue;const frame=document.querySelector('path.struct-grid[data-brace-visual="'+TB[id].brace+'"]');check(frame,'Missing matrix frame',{id});const r=frame.getBoundingClientRect();frames.push({id,x:r.x,y:r.y,right:r.right,bottom:r.bottom});for(const cid of descendants(id).slice(1)){const card=rect(cid);if(card.x<r.x-.1||card.y<r.y-.1||card.right>r.right+.1||card.bottom>r.bottom+.1)violations.push({owner:id,child:cid,card,frame:{x:r.x,y:r.y,right:r.right,bottom:r.bottom}});}}
        // EN: Infer allocated cell edges from the visible grid and child count; ensure descendants never spill into a neighboring owner's cell.
        // ZH: 从可见网格及直属节点数量推导单元格边界，确保后代不会越入相邻分支的单元格。
        for(const id of Object.keys(TB)){const children=tbKids(id);if(!children)continue;const frame=document.querySelector('path.struct-grid[data-brace-visual="'+TB[id].brace+'"]');const r=frame.getBoundingClientRect(),cols=Math.ceil(Math.sqrt(children.length)),rows=Math.ceil(children.length/cols);children.forEach((cid,index)=>{const column=index%cols,row=Math.floor(index/cols);const cell={x:r.x+r.width*column/cols,right:r.x+r.width*(column+1)/cols,y:r.y+r.height*row/rows,bottom:r.y+r.height*(row+1)/rows};for(const descendant of descendants(cid)){const card=rect(descendant);if(card.x<cell.x-.1||card.y<cell.y-.1||card.right>cell.right+.1||card.bottom>cell.bottom+.1)violations.push({kind:'neighbor-cell',owner:id,child:cid,descendant,card,cell});}});}
        const cards=Object.keys(TB).map(rect);for(let i=0;i<cards.length;i++)for(let j=i+1;j<cards.length;j++)if(overlaps(cards[i],cards[j]))violations.push({kind:'card-overlap',a:cards[i].id,b:cards[j].id});
        markDirty(false);fixtures.matrix=serialize();metrics.matrix={nodes:cards.length,frames,violations};check(!violations.length,'Matrix descendant escapes its owning frame or overlaps another card',violations.slice(0,5));
      });
      return {results,metrics,fixtures};
    })()`);
    fs.writeFileSync(path.join(artifacts, tag + '-results.json'), JSON.stringify(result, null, 2));
    for (const [name, fixture] of Object.entries(result.fixtures)) fs.writeFileSync(path.join(artifacts, tag + '-' + name + '.knotjot'), JSON.stringify(JSON.parse(fixture), null, 2));
    // EN: Capture the same persisted fixtures at fitted zoom for visual review after the independent geometry assertions complete.
    // ZH: 独立几何断言结束后，将相同持久化样本按适应视图缩放截图，供视觉审查。
    for (const name of ['timeline', 'matrix', 'spider']) {
      if (!result.fixtures[name]) continue;
      await win.webContents.executeJavaScript(`applyLoadedDocument(JSON.parse(${JSON.stringify(result.fixtures[name])}), 'layout-visual');hideStart();layout();{const bounds=contentBBox();scale=Math.min(1,canvas.clientWidth/(bounds.maxX-bounds.minX+200),canvas.clientHeight/(bounds.maxY-bounds.minY+200));}centerContent();`);
      await new Promise(resolve => setTimeout(resolve, 150));
      fs.writeFileSync(path.join(artifacts, tag + '-' + name + '.png'), (await win.webContents.capturePage()).toPNG());
    }
    console.log('LAYOUT_REGRESSION_RESULT', JSON.stringify(result.results));
    clearTimeout(timer);app.exit(result.results.every(item => item.pass) ? 0 : 1);
  } catch (error) { console.error(error.stack);clearTimeout(timer);app.exit(2); }
}
app.whenReady().then(runLayoutRegression);
