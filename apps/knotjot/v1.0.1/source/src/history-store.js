(function(root,factory){
/* EN: Callback for function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(r: call factory; update module.exports, root.KnotJotHistoryStore.
   ZH: function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(r 的回调：调用 factory；更新 module.exports、root.KnotJotHistoryStore。 */
const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(root)root.KnotJotHistoryStore=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
/* EN: Callback for function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(r: return {chooseUndo,chooseRedo}.
   ZH: function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(r 的回调：返回 {chooseUndo,chooseRedo}。 */

  'use strict';
  function chooseUndo(sheetEntry,fileEntry){
/* EN: Choose the newest sequence across sheet and document history for undo.
   ZH: 比较画布与文档历史序号，选择最近操作撤销。 */
if(!sheetEntry)return fileEntry?'file':null;if(!fileEntry)return 'sheet';return fileEntry.seq>sheetEntry.seq?'file':'sheet';}
  function chooseRedo(sheetEntry,fileEntry){
/* EN: Choose the oldest pending sequence across sheet and document history for redo.
   ZH: 比较画布与文档待重做历史序号，先重做较早操作。 */
if(!sheetEntry)return fileEntry?'file':null;if(!fileEntry)return 'sheet';return fileEntry.seq<sheetEntry.seq?'file':'sheet';}
  return {chooseUndo,chooseRedo};
});
