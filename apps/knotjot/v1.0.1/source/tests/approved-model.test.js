// EN: Verify hierarchy rejection and raster limits without allocating oversized canvases.
// ZH: 验证层级拒绝和位图上限，不分配巨大画布。
const test=require('node:test'),assert=require('node:assert/strict');
const path=require('node:path');
const source=process.env.KNOTJOT_TEST_SOURCE||path.join(__dirname,'..');
const model=require(path.join(source,'src/document-model'));
const exporter=require(path.join(source,'src/export-scene'));
function chain(count){
  // EN: Build a valid flat-record hierarchy for boundary and cycle tests.
  // ZH: 构造合法平面记录层级以测试边界和循环。
  const s={roots:['t0'],textboxes:{},braces:{}};
  for(let i=0;i<count;i++){s.textboxes['t'+i]={id:'t'+i,text:'Node',parentBrace:i?'b'+(i-1):null,brace:i<count-1?'b'+i:null};if(i<count-1)s.braces['b'+i]={parentTb:'t'+i,children:['t'+(i+1)]};}return s;
}
test('tree cycle is rejected before activation',()=>{const s=chain(2);s.textboxes.t1.brace='b1';s.textboxes.t0.parentBrace='b1';s.braces.b1={parentTb:'t1',children:['t0']};s.roots=[];assert.throws(()=>model.validateSheet(s,'cycle'));});
test('contradictory ownership is rejected',()=>{const s=chain(2);s.textboxes.t1.parentBrace=null;assert.throws(()=>model.validateSheet(s,'ownership'));});
test('duplicate children and roots are rejected',()=>{const s=chain(2);s.braces.b0.children.push('t1');assert.throws(()=>model.validateSheet(s,'duplicate'));const r=chain(1);r.roots.push('t0');assert.throws(()=>model.validateSheet(r,'roots'));});
test('excessive hierarchy depth is rejected',()=>assert.throws(()=>model.validateSheet(chain(1001),'depth')));
test('256-level hierarchy boundary remains valid',()=>assert.ok(model.validateSheet(chain(256),'depth')));
test('spider graph cycles and legacy round trip remain valid',()=>{const s=chain(3);s.docType='spider';s.links={a:{a:'t0',b:'t1'},b:{a:'t1',b:'t2'},c:{a:'t2',b:'t0'}};const d=model.validateAndNormalizeDocument({app:'brace-mindmap',...s});assert.deepEqual(model.validateAndNormalizeDocument(JSON.parse(JSON.stringify(d))),d);});
test('JPG raster has hard 8192 side cap below ratio 0.1',()=>{for(const [w,h] of [[1000000,200000],[200,1000000],[100,100]]){const r=exporter.safeSingleRatio(w,h,2);assert.ok(r>0&&Math.ceil(w*r)<=8192&&Math.ceil(h*r)<=8192);}});
