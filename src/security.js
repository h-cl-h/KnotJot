(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotSecurity = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const LIMITS = Object.freeze({
    documentBytes: 64 * 1024 * 1024,
    themeBytes: 2 * 1024 * 1024,
    skinBytes: 16 * 1024 * 1024,
    cssBytes: 2 * 1024 * 1024,
    imageBytes: 12 * 1024 * 1024,
    imagePixels: 40 * 1000 * 1000,
    imageSide: 16384,
    dataUrlBytes: 16 * 1024 * 1024
  });

  function byteLength(value) {
    const s = String(value == null ? '' : value);
    if (typeof Buffer !== 'undefined') return Buffer.byteLength(s, 'utf8');
    return new TextEncoder().encode(s).byteLength;
  }

  function parseSafeExternalUrl(value, allowMailto) {
    let input = String(value || '').trim();
    if (!input) throw new Error('链接不能为空');
    if (!/^[a-z][a-z0-9+.-]*:/i.test(input)) input = 'https://' + input;
    let parsed;
    try { parsed = new URL(input); } catch (_) { throw new Error('链接格式无效'); }
    const allowed = allowMailto ? ['http:', 'https:', 'mailto:'] : ['http:', 'https:'];
    if (!allowed.includes(parsed.protocol)) throw new Error('只允许打开 HTTP 或 HTTPS 链接');
    if ((parsed.protocol === 'http:' || parsed.protocol === 'https:') && !parsed.hostname) throw new Error('链接缺少有效域名');
    if (parsed.username || parsed.password) throw new Error('链接不得包含明文账号或密码');
    return parsed.href;
  }

  function normalizeGradient(value, fallback) {
    const raw = String(value || fallback || '').trim();
    const parts = raw.split(',').map(x => x.trim()).filter(Boolean);
    if (parts.length < 3 || parts.length > 5 || !/^(?:0|[1-9]\d{0,2})(?:\.\d+)?deg$/i.test(parts[0])) return null;
    const angle = Number(parts[0].slice(0, -3));
    if (!Number.isFinite(angle) || angle < 0 || angle > 360) return null;
    const stops = parts.slice(1);
    for (const stop of stops) {
      if (!/^#[0-9a-f]{6}(?:\s+(?:0|[1-9]\d?|100)(?:\.\d+)?%)?$/i.test(stop)) return null;
    }
    return `${angle}deg,${stops.map(x => x.toLowerCase().replace(/\s+/g, ' ')).join(',')}`;
  }

  function validateUserCss(value) {
    const css = String(value || '');
    if (!css.trim()) throw new Error('UI CSS 不能为空');
    if (byteLength(css) > LIMITS.cssBytes) throw new Error('UI CSS 超过 2 MB 上限');
    if (/\0/.test(css)) throw new Error('UI CSS 包含非法空字符');
    if (/@(?:import|charset|namespace|document)\b/i.test(css)) throw new Error('UI CSS 不允许 @import 等外部规则');
    if (/(?:expression\s*\(|behavior\s*:|-moz-binding\s*:|javascript\s*:|vbscript\s*:|file\s*:)/i.test(css)) throw new Error('UI CSS 包含危险语法');
    const urls = css.match(/url\s*\(([^)]*)\)/gi) || [];
    for (const token of urls) {
      const body = token.replace(/^url\s*\(|\)$/gi, '').trim().replace(/^['"]|['"]$/g, '');
      if (!/^data:image\/(?:png|jpeg|webp|gif);base64,[a-z0-9+/=]+$/i.test(body) || byteLength(body) > 512 * 1024) {
        throw new Error('UI CSS 只允许不超过 512 KB 的内嵌图片 URL');
      }
    }
    let depth = 0, maxDepth = 0;
    for (const ch of css) {
      if (ch === '{') { depth++; maxDepth = Math.max(maxDepth, depth); }
      else if (ch === '}') { depth--; if (depth < 0) throw new Error('UI CSS 大括号不匹配'); }
    }
    if (depth !== 0) throw new Error('UI CSS 大括号不匹配');
    if (maxDepth > 8 || (css.match(/{/g) || []).length > 10000) throw new Error('UI CSS 嵌套或规则数量过大');
    const protectedSelector = /#(?:exporting-mask|aiCfg|aiMask|settingsMask|noteMask|ganttMask|uiGalleryMask)\b[^{}]*\{[^{}]*(?:display\s*:\s*none|visibility\s*:\s*hidden|opacity\s*:\s*0|pointer-events\s*:\s*none)/i;
    if (protectedSelector.test(css)) throw new Error('UI CSS 不得隐藏或禁用安全与确认界面');
    return css;
  }

  function dimensionsFromBytes(input) {
    const b = input instanceof Uint8Array ? input : new Uint8Array(input || 0);
    if (b.length >= 24 && b[0] === 0x89 && b[1] === 0x50 && b[2] === 0x4e && b[3] === 0x47) {
      const v = new DataView(b.buffer, b.byteOffset, b.byteLength);
      return { type: 'png', width: v.getUint32(16), height: v.getUint32(20) };
    }
    if (b.length >= 12 && b[0] === 0xff && b[1] === 0xd8) {
      let p = 2;
      while (p + 9 < b.length) {
        if (b[p] !== 0xff) { p++; continue; }
        const marker = b[p + 1];
        if (marker === 0xd8 || marker === 0xd9) { p += 2; continue; }
        const len = (b[p + 2] << 8) | b[p + 3];
        if (len < 2 || p + len + 2 > b.length) break;
        if ([0xc0,0xc1,0xc2,0xc3,0xc5,0xc6,0xc7,0xc9,0xca,0xcb,0xcd,0xce,0xcf].includes(marker)) {
          return { type: 'jpeg', width: (b[p + 7] << 8) | b[p + 8], height: (b[p + 5] << 8) | b[p + 6] };
        }
        p += 2 + len;
      }
    }
    if (b.length >= 30 && String.fromCharCode(...b.slice(0, 4)) === 'RIFF' && String.fromCharCode(...b.slice(8, 12)) === 'WEBP') {
      const kind = String.fromCharCode(...b.slice(12, 16));
      if (kind === 'VP8X') return { type: 'webp', width: 1 + b[24] + (b[25] << 8) + (b[26] << 16), height: 1 + b[27] + (b[28] << 8) + (b[29] << 16) };
      if (kind === 'VP8 ' && b.length >= 30) return { type: 'webp', width: ((b[27] << 8) | b[26]) & 0x3fff, height: ((b[29] << 8) | b[28]) & 0x3fff };
      if (kind === 'VP8L' && b.length >= 25) {
        const bits = b[21] | (b[22] << 8) | (b[23] << 16) | (b[24] << 24);
        return { type: 'webp', width: (bits & 0x3fff) + 1, height: ((bits >>> 14) & 0x3fff) + 1 };
      }
    }
    return null;
  }

  function validateImageDimensions(dim) {
    if (!dim || !Number.isFinite(dim.width) || !Number.isFinite(dim.height) || dim.width < 1 || dim.height < 1) throw new Error('无法读取图片尺寸');
    if (dim.width > LIMITS.imageSide || dim.height > LIMITS.imageSide || dim.width * dim.height > LIMITS.imagePixels) {
      throw new Error(`图片尺寸过大（上限 ${LIMITS.imageSide}×${LIMITS.imageSide} 且不超过 ${Math.round(LIMITS.imagePixels / 1000000)} 百万像素）`);
    }
    return dim;
  }

  return { LIMITS, byteLength, parseSafeExternalUrl, normalizeGradient, validateUserCss, dimensionsFromBytes, validateImageDimensions };
});
