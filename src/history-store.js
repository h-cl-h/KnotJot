(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;if(root)root.KnotJotHistoryStore=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
  'use strict';
  function chooseUndo(sheetEntry,fileEntry){if(!sheetEntry)return fileEntry?'file':null;if(!fileEntry)return 'sheet';return fileEntry.seq>sheetEntry.seq?'file':'sheet';}
  function chooseRedo(sheetEntry,fileEntry){if(!sheetEntry)return fileEntry?'file':null;if(!fileEntry)return 'sheet';return fileEntry.seq<sheetEntry.seq?'file':'sheet';}
  return {chooseUndo,chooseRedo};
});
