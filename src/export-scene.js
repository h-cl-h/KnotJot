(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotExportScene = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const MAX_TILE_PIXELS = 16 * 1024 * 1024;
  const MAX_TILE_SIDE = 4096;
  const MAX_SINGLE_PIXELS = 48 * 1024 * 1024;
  const MAX_SINGLE_SIDE = 8192;

  function collectCssText(doc, includeSkin = true) {
    const chunks = [];
    for (const sheet of Array.from(doc.styleSheets || [])) {
      if (!includeSkin && sheet.ownerNode && sheet.ownerNode.hasAttribute && sheet.ownerNode.hasAttribute('data-uiskin')) continue;
      try { chunks.push(Array.from(sheet.cssRules || []).map(r => r.cssText).join('\n')); } catch (_) {}
    }
    chunks.push('.sel,.primary{outline:none!important}.acts,.anchor,.link-handle,.resize-handle,.ts-card-menu,#marquee,#insLine,.align-guide,#snapHint{display:none!important}');
    return chunks.join('\n').replace(/<\/style/gi, '<\\/style');
  }

  function liveContentBounds(ctx) {
    const { document: doc, canvas, world, scale, panX, panY } = ctx;
    const canvasRect = canvas.getBoundingClientRect();
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    const addRect = rect => {
      if (!rect || !rect.width || !rect.height) return;
      const left = (rect.left - canvasRect.left - panX) / scale;
      const top = (rect.top - canvasRect.top - panY) / scale;
      const right = (rect.right - canvasRect.left - panX) / scale;
      const bottom = (rect.bottom - canvasRect.top - panY) / scale;
      minX = Math.min(minX, left); minY = Math.min(minY, top); maxX = Math.max(maxX, right); maxY = Math.max(maxY, bottom);
    };
    world.querySelectorAll('#nodes>.node,#braces>*,#ovsvg>*,#overlayLayer>*,#legendBox').forEach(el => {
      const cs = getComputedStyle(el); if (cs.display !== 'none' && cs.visibility !== 'hidden') addRect(el.getBoundingClientRect());
    });
    const limited = doc.getElementById('tcCanvasLimited');
    if (limited && getComputedStyle(limited).display !== 'none') addRect(limited.getBoundingClientRect());
    if (!Number.isFinite(minX)) return { minX: 0, minY: 0, maxX: 1, maxY: 1 };
    return { minX, minY, maxX, maxY };
  }

  function buildScene(ctx) {
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
    clone.querySelectorAll('.sel,.primary').forEach(el => el.classList.remove('sel','primary'));
    clone.querySelectorAll('.acts,.anchor,.link-handle,.resize-handle,#marquee,#insLine,.align-guide,#snapHint').forEach(el => el.remove());
    clone.setAttribute('xmlns', 'http://www.w3.org/1999/xhtml');
    const serializer = new XMLSerializer();
    const worldMarkup = serializer.serializeToString(clone);
    const state = ctx.appearance || {}, bg = state.background || {};
    let background = `background:${ctx.pageBackground || '#fff'};`;
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
    const t = tile || { x: 0, y: 0, width: scene.W, height: scene.H };
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${t.width}" height="${t.height}" viewBox="${t.x} ${t.y} ${t.width} ${t.height}"><foreignObject x="0" y="0" width="${scene.W}" height="${scene.H}">${scene.markup}</foreignObject></svg>`;
  }

  function tilePlan(W, H, ratio, maxSide = MAX_TILE_SIDE) {
    const worldTile = Math.max(1, Math.floor(maxSide / ratio));
    const out = [];
    for (let y = 0; y < H; y += worldTile) for (let x = 0; x < W; x += worldTile) out.push({ x, y, width: Math.min(worldTile, W - x), height: Math.min(worldTile, H - y) });
    return out;
  }

  function safeSingleRatio(W, H, requested) {
    return Math.max(0.1, Math.min(requested, MAX_SINGLE_SIDE / W, MAX_SINGLE_SIDE / H, Math.sqrt(MAX_SINGLE_PIXELS / (W * H))));
  }

  return { MAX_TILE_PIXELS, MAX_TILE_SIDE, buildScene, svgFor, tilePlan, safeSingleRatio, liveContentBounds, collectCssText };
});
