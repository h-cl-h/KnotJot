(function(root,factory){
/* EN: Callback for function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else: call factory; update module.exports, root.KnotJotTextInputRules.
   ZH: function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else 的回调：调用 factory；更新 module.exports、root.KnotJotTextInputRules。 */
const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.KnotJotTextInputRules=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
/* EN: Callback for function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else: return {MAX_CHARACTERS,count,pattern,allowed,complete}.
   ZH: function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else 的回调：返回 {MAX_CHARACTERS,count,pattern,allowed,complete}。 */

  'use strict';
  const MAX_CHARACTERS=10000;
  function count(text){
/* EN: Count Unicode code points so surrogate pairs consume one input character.
   ZH: 按 Unicode 码点计数，使代理对只占一个输入字符。 */
return Array.from(String(text==null?'':text)).length;}
  function pattern(patternText){
/* EN: Compile a whole-string Unicode regular expression, returning null for invalid syntax.
   ZH: 编译覆盖完整字符串的 Unicode 正则表达式，语法无效时返回 null。 */
try{return new RegExp(`^(?:${patternText})$`,'u');}catch(_){return null;}}
  function allowed(text,rules){
/* EN: Apply character type, full-match regex and maximum-length rules to prospective input.
   ZH: 对待输入文本执行字符类型、正则全文匹配与最大长度规则。 */
text=String(text==null?'':text);const r=rules||{},maximum=Math.max(0,Math.min(MAX_CHARACTERS,Number(r.maxLength)||0));if(maximum&&count(text)>maximum)return false;if(r.type==='number'&&!/^\d*$/.test(text))return false;if(r.type==='letter'&&!/^[A-Za-z]*$/.test(text))return false;if(r.type==='chinese'&&!/^[\u3400-\u9fff]*$/.test(text))return false;if(r.type==='alnum'&&!/^[A-Za-z0-9]*$/.test(text))return false;if(r.type==='regex'&&r.pattern){const expression=pattern(r.pattern);if(!expression||!expression.test(text))return false;}return true;}
  function complete(text,rules){
/* EN: Apply final required-field validation in addition to the normal input restrictions.
   ZH: 在普通输入限制之外执行提交时的必填验证。 */
text=String(text==null?'':text);const r=rules||{};return !(r.required&&!text.trim())&&allowed(text,r);}
  return{MAX_CHARACTERS,count,pattern,allowed,complete};
});
