'use strict';

(function(root, factory) {
/* EN: Callback for function(root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.expo: call factory; update module.exports, root.KnotJotFileExtensions.
   ZH: function(root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.expo 的回调：调用 factory；更新 module.exports、root.KnotJotFileExtensions。 */

  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.KnotJotFileExtensions = api;
})(globalThis, function() {
/* EN: Callback for function(root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.expo: complete without changing state.
   ZH: function(root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.expo 的回调：完成且不修改状态。 */

  const mapExtensions = ['.knotjot', '.bmap'];
  const skinExtensions = ['.knotjot-ui', '.bmapui'];
  const themeExtensions = ['.knotjot-theme', '.bmaptheme'];
  const matches = (name, extensions) => /* EN: matches in module: call extensions.some. ZH: module 中的 matches：调用 extensions.some。 */ typeof name === 'string' && extensions.some(extension => /* EN: Test an item for extensions.some: call name.toLowerCase().endsWith, name.toLowerCase; return name.toLowerCase().endsWith(extension). ZH: 判断 extensions.some 的元素条件：调用 name.toLowerCase().endsWith、name.toLowerCase；返回 name.toLowerCase().endsWith(extension)。 */ name.toLowerCase().endsWith(extension));

  return {
    mapExtensions,
    skinExtensions,
    themeExtensions,
    mapDefaultExtension: mapExtensions[0],
    isMapFile: name => /* EN: isMapFile in module: call matches; return matches(name, mapExtensions). ZH: module 中的 isMapFile：调用 matches；返回 matches(name, mapExtensions)。 */ matches(name, mapExtensions),
    isSkinFile: name => /* EN: isSkinFile in module: call matches; return matches(name, skinExtensions). ZH: module 中的 isSkinFile：调用 matches；返回 matches(name, skinExtensions)。 */ matches(name, skinExtensions),
    isThemeFile: name => /* EN: isThemeFile in module: call matches; return matches(name, themeExtensions). ZH: module 中的 isThemeFile：调用 matches；返回 matches(name, themeExtensions)。 */ matches(name, themeExtensions),
    removeMapExtension: name => /* EN: removeMapExtension in module: call String(name || '').replace; return String(name || '').replace(/\.(?:knotjot|bmap)$/i, ''). ZH: module 中的 removeMapExtension：调用 String(name || '').replace；返回 String(name || '').replace(/\.(?:knotjot|bmap)$/i, '')。 */ String(name || '').replace(/\.(?:knotjot|bmap)$/i, '')
  };
});
