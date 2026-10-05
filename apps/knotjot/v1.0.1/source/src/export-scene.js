(function (root, factory) {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: call factory; update module.exports, root.KnotJotExportScene.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：调用 factory；更新 module.exports、root.KnotJotExportScene。 */

  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotExportScene = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: complete without changing state.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：完成且不修改状态。 */

  'use strict';
  const MAX_TILE_PIXELS = 16 * 1024 * 1024;
  const MAX_TILE_SIDE = 4096;
  const MAX_SINGLE_PIXELS = 48 * 1024 * 1024;
  const MAX_SINGLE_SIDE = 8192;

  function collectCssText(doc, includeSkin = true) {
/* EN: Collect accessible styles and hide editor-only controls in exported scenes.
   ZH: 收集可访问样式，并在导出场景中隐藏仅供编辑的控件。 */

    const chunks = [];
    for (const sheet of Array.from(doc.styleSheets || [])) {
      if (!includeSkin && sheet.ownerNode && sheet.ownerNode.hasAttribute && sheet.ownerNode.hasAttribute('data-uiskin')) continue;
      try { chunks.push(Array.from(sheet.cssRules || []).map(r => /* EN: Derive the next value for Array.from(sheet.cssRules || []).map: return r.cssText. ZH: 计算 Array.from(sheet.cssRules || []).map 的下一项结果：返回 r.cssText。 */ r.cssText).join('\n')); } catch (_) {}
    }
    chunks.push('.sel,.primary{outline:none!important}.acts,.anchor,.link-handle,.resize-handle,.ts-card-menu,#marquee,#insLine,.align-guide,#snapHint{display:none!important}');
    return chunks.join('\n').replace(/<\/style/gi, '<\\/style');
  }

  function liveContentBounds(ctx) {
/* EN: Convert visible DOM bounds to world coordinates, including the finite canvas background.
   ZH: 将可见 DOM 边界转换到世界坐标，同时计入有限画布背景。 */

    const { document: doc, canvas, world, scale, panX, panY } = ctx;
    const canvasRect = canvas.getBoundingClientRect();
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    const addRect = rect => {
/* EN: addRect in liveContentBounds: update minX, minY, maxX.
   ZH: liveContentBounds 中的 addRect：更新 minX、minY、maxX。 */

      if (!rect || !rect.width || !rect.height) return;
      const left = (rect.left - canvasRect.left - panX) / scale;
      const top = (rect.top - canvasRect.top - panY) / scale;
      const right = (rect.right - canvasRect.left - panX) / scale;
      const bottom = (rect.bottom - canvasRect.top - panY) / scale;
      minX = Math.min(minX, left); minY = Math.min(minY, top); maxX = Math.max(maxX, right); maxY = Math.max(maxY, bottom);
    };
    world.querySelectorAll('#nodes>.node,#braces>*,#ovsvg>*,#overlayLayer>*,#legendBox').forEach(el => {
/* EN: Process each item in world.querySelectorAll('#nodes>.node,#braces>*,#ovsvg>*,#overlayLayer>*,#legendBox').forEach: call getComputedStyle, addRect, el.getBoundingClientRect.
   ZH: 逐项处理 world.querySelectorAll('#nodes>.node,#braces>*,#ovsvg>*,#overlayLayer>*,#legendBox').forEach 中的元素：调用 getComputedStyle、addRect、el.getBoundingClientRect。 */

      const cs = getComputedStyle(el); if (cs.display !== 'none' && cs.visibility !== 'hidden') addRect(el.getBoundingClientRect());
    });
    const limited = doc.getElementById('tcCanvasLimited');
    if (limited && getComputedStyle(limited).display !== 'none') addRect(limited.getBoundingClientRect());
    if (!Number.isFinite(minX)) return { minX: 0, minY: 0, maxX: 1, maxY: 1 };
    return { minX, minY, maxX, maxY };
  }

  function buildScene(ctx) {
/* EN: Clone the rendered world and preserve the enabled unlimited sheet color for current-style exports; original-style and disabled backgrounds keep their page fallback.
   ZH: 克隆渲染后的世界，当前风格导出保留已启用的无限画布独立底色；原版风格及禁用背景仍使用页面回退色。 */

    const pad = Number.isFinite(ctx.padding) ? ctx.padding : 48;
    const bounds = liveContentBounds(ctx);
    const W = Math.max(1, Math.ceil(bounds.maxX - bounds.minX + pad * 2));
    const H = Math.max(1, Math.ceil(bounds.maxY - bounds.minY + pad * 2));
    const ox = pad - bounds.minX, oy = pad - bounds.minY;
    const clone = ctx.world.cloneNode(true);
    clone.removeAttribute('style');
    clone.style.position = 'absolute'; clone.style.left = '0'; clone.style.top = '0';
    clone.style.width = '6000px'; clone.style.height = '3600px';
    clone.style.transformOrigin = '0 0'; clone.style.transform = `translate(${ox}px,${oy}px)`;
    clone.querySelectorAll('.sel,.primary').forEach(el => /* EN: Process each item in clone.querySelectorAll('.sel,.primary').forEach: call el.classList.remove; return el.classList.remove('sel','primary'). ZH: 逐项处理 clone.querySelectorAll('.sel,.primary').forEach 中的元素：调用 el.classList.remove；返回 el.classList.remove('sel','primary')。 */ el.classList.remove('sel','primary'));
    clone.querySelectorAll('.acts,.anchor,.link-handle,.resize-handle,#marquee,#insLine,.align-guide,#snapHint').forEach(el => /* EN: Callback for clone.querySelectorAll('.acts,.anchor,.link-handle,.resize-handle,#marquee,#insLine,.align-guide,#snapHint').f: call el.remove; return el.remove(). ZH: clone.querySelectorAll('.acts,.anchor,.link-handle,.resize-handle,#marquee,#insLine,.align-guide,#snapHint').f 的回调：调用 el.remove；返回 el.remove()。 */ el.remove());
    clone.setAttribute('xmlns', 'http://www.w3.org/1999/xhtml');
    const serializer = new XMLSerializer();
    const worldMarkup = serializer.serializeToString(clone);
    const state = ctx.appearance || {}, bg = state.background || {};
    const sheetColor = ctx.includeSkin !== false && bg.enabled && bg.mode === 'unlimited' && /^#[0-9a-f]{6}$/i.test(bg.color || '') ? bg.color : null;
    let background = `background:${sheetColor || ctx.pageBackground || '#fff'};`;
    if (bg.enabled && bg.mode === 'unlimited' && bg.imageData) {
      const iw = Math.max(1, Number(bg.imageWidth) * Number(bg.imageScale || 100) / 100);
      const ih = Math.max(1, Number(bg.imageHeight) * Number(bg.imageScale || 100) / 100);
      const px = ox + Number(bg.positionX || 0) / 100 * iw, py = oy + Number(bg.positionY || 0) / 100 * ih;
      background += `background-image:url(&quot;${bg.imageData}&quot;);background-repeat:repeat;background-size:${iw}px ${ih}px;background-position:${px}px ${py}px;`;
    }
    const css = collectCssText(ctx.document, ctx.includeSkin !== false) + '\n' + String(ctx.extraCss || '');
    const vars=String(ctx.rootVariables||'').replace(/["<>]/g,'');
    return { W, H, bounds, markup: `<div xmlns="http://www.w3.org/1999/xhtml" style="position:relative;width:${W}px;height:${H}px;overflow:hidden;${vars};${background}"><style>${css}</style>${worldMarkup}</div>` };
  }

  function svgFor(scene, tile) {
/* EN: Wrap the scene in an SVG foreignObject and optionally restrict the viewBox to a tile.
   ZH: 将场景包装为 SVG foreignObject，并可将 viewBox 限制到单个分片。 */

    const t = tile || { x: 0, y: 0, width: scene.W, height: scene.H };
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${t.width}" height="${t.height}" viewBox="${t.x} ${t.y} ${t.width} ${t.height}"><foreignObject x="0" y="0" width="${scene.W}" height="${scene.H}">${scene.markup}</foreignObject></svg>`;
  }

  function tilePlan(W, H, ratio, maxSide = MAX_TILE_SIDE) {
/* EN: Bound export raster dimensions by selecting tiles or a safe single-image scale.
   ZH: 通过分片或安全单图缩放比例限制导出位图尺寸。 */

    const worldTile = Math.max(1, Math.floor(maxSide / ratio));
    const out = [];
    for (let y = 0; y < H; y += worldTile) for (let x = 0; x < W; x += worldTile) out.push({ x, y, width: Math.min(worldTile, W - x), height: Math.min(worldTile, H - y) });
    return out;
  }

  function safeSingleRatio(W, H, requested) {
/* EN: Enforce side and pixel caps even below ten percent; reject invalid geometry rather than allocate an unbounded raster.
   ZH: 即使倍率低于百分之十也遵守边长和像素上限；拒绝无效几何，避免分配无界位图。 */

    if (![W,H,requested].every(value => Number.isFinite(value) && value > 0)) throw new Error('Invalid export dimensions');
    return Math.min(requested, MAX_SINGLE_SIDE / W, MAX_SINGLE_SIDE / H, Math.sqrt(MAX_SINGLE_PIXELS / (W * H)));
  }

  return { MAX_TILE_PIXELS, MAX_TILE_SIDE, buildScene, svgFor, tilePlan, safeSingleRatio, liveContentBounds, collectCssText };
});
