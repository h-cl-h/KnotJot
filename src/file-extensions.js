'use strict';

(function(root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.KnotJotFileExtensions = api;
})(globalThis, function() {
  const mapExtensions = ['.knotjot', '.bmap'];
  const skinExtensions = ['.knotjot-ui', '.bmapui'];
  const themeExtensions = ['.knotjot-theme', '.bmaptheme'];
  const matches = (name, extensions) => typeof name === 'string' && extensions.some(extension => name.toLowerCase().endsWith(extension));

  return {
    mapExtensions,
    skinExtensions,
    themeExtensions,
    mapDefaultExtension: mapExtensions[0],
    isMapFile: name => matches(name, mapExtensions),
    isSkinFile: name => matches(name, skinExtensions),
    isThemeFile: name => matches(name, themeExtensions),
    removeMapExtension: name => String(name || '').replace(/\.(?:knotjot|bmap)$/i, '')
  };
});
