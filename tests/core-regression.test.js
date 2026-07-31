'use strict';
const assert=require('assert');
const fs=require('fs');
const os=require('os');
const path=require('path');
const Security=require('../src/security');
const DocumentModel=require('../src/document-model');
const AI=require('../src/ai-client');
const ExportScene=require('../src/export-scene');
const History=require('../src/history-store');
const {writeAtomicText}=require('../src/atomic-store');

async function main(){
  assert.match(Security.parseSafeExternalUrl('example.com'),/^https:\/\/example\.com\/?$/);
  for(const bad of ['javascript:alert(1)','file:///c:/secret','data:text/html,x'])assert.throws(()=>Security.parseSafeExternalUrl(bad));
  assert.equal(Security.normalizeGradient('135deg,#AABBCC 0%,#112233 100%'),'135deg,#aabbcc 0%,#112233 100%');
  assert.equal(Security.normalizeGradient('135deg,#fff,url(https://bad)'),null);
  assert.throws(()=>Security.validateUserCss('@import url(https://bad);'));
  assert.throws(()=>Security.validateUserCss('#aiMask{display:none}'));
  assert.equal(Security.validateUserCss('body{color:#123456}'),'body{color:#123456}');

  const valid={app:'brace-mindmap',version:2,name:'ok',curSheet:0,sheets:[{name:'A',docType:'brace',idc:1,roots:['t1'],textboxes:{t1:{id:'t1',text:'x',x:1,y:2}},braces:{},rbraces:{},fgroups:{},links:{},boundaries:{},summaries:{},callouts:{},relations:{},view:{}}],appearance:{background:null},aiChat:[]};
  const normalized=DocumentModel.validateAndNormalizeDocument(valid);assert.notStrictEqual(normalized,valid);assert.equal(normalized.sheets[0].textboxes.t1.text,'x');
  const broken=JSON.parse(JSON.stringify(valid));broken.sheets[0].roots=['missing'];assert.throws(()=>DocumentModel.validateAndNormalizeDocument(broken),/不存在/);assert.equal(valid.sheets[0].roots[0],'t1');

  const text='中文🙂',bytes=new TextEncoder().encode(text),decoder=new TextDecoder();let decoded='';for(const byte of bytes)decoded+=decoder.decode(Uint8Array.of(byte),{stream:true});decoded+=decoder.decode();assert.equal(decoded,text);
  const state={buf:''};let delta='';AI.sseLines(state,'data: {"choices":[{"delta":{"content":"中"}}]}',j=>delta+=j.choices[0].delta.content,false);AI.sseLines(state,'\n',j=>delta+=j.choices[0].delta.content,true);assert.equal(delta,'中');
  assert.equal(ExportScene.tilePlan(9000,5000,1).length,6);assert.ok(ExportScene.safeSingleRatio(20000,10000,2)<1);
  assert.equal(History.chooseUndo({seq:1},{seq:2}),'file');assert.equal(History.chooseRedo({seq:1},{seq:2}),'sheet');

  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'knotjot-core-')),file=path.join(dir,'store.json');await writeAtomicText(file,'old');await writeAtomicText(file,'new');assert.equal(fs.readFileSync(file,'utf8'),'new');fs.rmSync(dir,{recursive:true,force:true});

  const protocol=JSON.parse(fs.readFileSync(path.resolve(__dirname,'..','..','..','..','shared','ui-skin-protocol.json'),'utf8'));assert.equal(protocol.format,'knotjot-ui-skins');assert.equal(protocol.limits.cssBytes,Security.LIMITS.cssBytes);
  JSON.parse(fs.readFileSync(path.resolve(__dirname,'..','package.json'),'utf8'));
  const html=fs.readFileSync(path.resolve(__dirname,'..','index.html'),'utf8'),match=/<script>\s*"use strict";([\s\S]*?)<\/script>/.exec(html);assert.ok(match,'main inline script');new Function('"use strict";'+match[1]);
  console.log('core-regression: ok');
}
main().catch(err=>{console.error(err);process.exitCode=1;});
