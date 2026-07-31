(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.KnotJotTextInputRules=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
  'use strict';
  const MAX_CHARACTERS=10000;
  function count(text){return Array.from(String(text==null?'':text)).length;}
  function pattern(patternText){try{return new RegExp(`^(?:${patternText})$`,'u');}catch(_){return null;}}
  function allowed(text,rules){text=String(text==null?'':text);const r=rules||{},maximum=Math.max(0,Math.min(MAX_CHARACTERS,Number(r.maxLength)||0));if(maximum&&count(text)>maximum)return false;if(r.type==='number'&&!/^\d*$/.test(text))return false;if(r.type==='letter'&&!/^[A-Za-z]*$/.test(text))return false;if(r.type==='chinese'&&!/^[\u3400-\u9fff]*$/.test(text))return false;if(r.type==='alnum'&&!/^[A-Za-z0-9]*$/.test(text))return false;if(r.type==='regex'&&r.pattern){const expression=pattern(r.pattern);if(!expression||!expression.test(text))return false;}return true;}
  function complete(text,rules){text=String(text==null?'':text);const r=rules||{};return !(r.required&&!text.trim())&&allowed(text,r);}
  return{MAX_CHARACTERS,count,pattern,allowed,complete};
});
