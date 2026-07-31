(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotDocumentModel = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const DICTS = ['textboxes','braces','rbraces','fgroups','links','boundaries','summaries','callouts','relations'];
  const plain = v => !!v && typeof v === 'object' && !Array.isArray(v) && (Object.getPrototypeOf(v) === Object.prototype || Object.getPrototypeOf(v) === null);
  const clone = value => JSON.parse(JSON.stringify(value));
  function fail(path, message) { throw new Error(`${path}：${message}`); }
  function finite(value, path, min = -10000000, max = 10000000) {
    if (value == null) return;
    if (typeof value !== 'number' || !Number.isFinite(value) || value < min || value > max) fail(path, '数值超出允许范围');
  }
  function text(value, path, max) { if (value != null && (typeof value !== 'string' || value.length > max)) fail(path, `必须是长度不超过 ${max} 的文本`); }
  function ids(value, path, target, max = 50000) {
    if (!Array.isArray(value) || value.length > max) fail(path, '必须是合理长度的 ID 数组');
    value.forEach((id, i) => { if (typeof id !== 'string' || !Object.prototype.hasOwnProperty.call(target, id)) fail(`${path}[${i}]`, '引用了不存在的记录'); });
  }
  function dataImage(value, path) {
    if (value == null || value === '') return;
    if (typeof value !== 'string' || value.length > 16 * 1024 * 1024 || !/^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(value)) fail(path, '图片数据无效或过大');
  }
  function validateSheet(sheet, path) {
    if (!plain(sheet)) fail(path, '画布必须是对象');
    const d = clone(sheet);
    text(d.name, `${path}.name`, 200);
    if (!['brace','spider'].includes(d.docType || 'brace')) fail(`${path}.docType`, '画布类型无效');
    finite(d.idc, `${path}.idc`, 0, 100000000);
    for (const key of DICTS) {
      if (d[key] == null) d[key] = {};
      if (!plain(d[key]) || Object.keys(d[key]).length > 50000) fail(`${path}.${key}`, '必须是合理大小的字典');
    }
    for (const [id, item] of Object.entries(d.textboxes)) {
      if (typeof id !== 'string' || id.length < 1 || id.length > 128 || !plain(item)) fail(`${path}.textboxes`, '文本框 ID 或记录无效');
      if (item.id != null && item.id !== id) fail(`${path}.textboxes.${id}.id`, 'ID 与字典键不一致');
      item.id = id; text(item.text, `${path}.textboxes.${id}.text`, 100000); finite(item.x, `${path}.textboxes.${id}.x`); finite(item.y, `${path}.textboxes.${id}.y`);
      if (item.parentBrace != null && !Object.prototype.hasOwnProperty.call(d.braces, item.parentBrace)) fail(`${path}.textboxes.${id}.parentBrace`, '引用不存在的大括号');
      if (item.brace != null && !Object.prototype.hasOwnProperty.call(d.braces, item.brace)) fail(`${path}.textboxes.${id}.brace`, '引用不存在的大括号');
      if (item.img != null) { if (!plain(item.img)) fail(`${path}.textboxes.${id}.img`, '图片记录无效'); dataImage(item.img.src, `${path}.textboxes.${id}.img.src`); finite(item.img.w, `${path}.textboxes.${id}.img.w`, 1, 10000); finite(item.img.h, `${path}.textboxes.${id}.img.h`, 1, 10000); }
      for (const field of ['markers','tags']) if (item[field] != null && (!Array.isArray(item[field]) || item[field].length > 1000 || item[field].some(x => typeof x !== 'string' || x.length > 1000))) fail(`${path}.textboxes.${id}.${field}`, '列表无效或过大');
    }
    for (const [id, brace] of Object.entries(d.braces)) {
      if (!plain(brace)) fail(`${path}.braces.${id}`, '大括号记录无效');
      if (brace.parentTb != null && !d.textboxes[brace.parentTb]) fail(`${path}.braces.${id}.parentTb`, '引用不存在的文本框');
      ids(brace.children || [], `${path}.braces.${id}.children`, d.textboxes);
    }
    for (const [id, link] of Object.entries(d.links)) {
      if (!plain(link) || !d.textboxes[link.a] || !d.textboxes[link.b]) fail(`${path}.links.${id}`, '连线端点无效');
      if (link.nodes != null && (!Array.isArray(link.nodes) || link.nodes.length > 10000)) fail(`${path}.links.${id}.nodes`, '连线节点无效');
    }
    const memberTargets = [['rbraces','sources'],['fgroups','members'],['boundaries','members'],['summaries','members']];
    for (const [section, field] of memberTargets) for (const [id, item] of Object.entries(d[section])) { if (!plain(item)) fail(`${path}.${section}.${id}`, '记录无效'); ids(item[field] || [], `${path}.${section}.${id}.${field}`, d.textboxes); }
    for (const [id, item] of Object.entries(d.rbraces)) if (item.target != null && !d.textboxes[item.target]) fail(`${path}.rbraces.${id}.target`, '引用不存在的文本框');
    for (const [id, item] of Object.entries(d.callouts)) if (!plain(item) || !d.textboxes[item.tb]) fail(`${path}.callouts.${id}.tb`, '引用不存在的文本框');
    for (const [id, item] of Object.entries(d.relations)) if (!plain(item) || !d.textboxes[item.a] || !d.textboxes[item.b]) fail(`${path}.relations.${id}`, '联系端点无效');
    if (d.roots == null) d.roots = [];
    ids(d.roots, `${path}.roots`, d.textboxes);
    if (d.focusId != null && !d.textboxes[d.focusId]) fail(`${path}.focusId`, '聚焦项不存在');
    if (d.view != null && !plain(d.view)) fail(`${path}.view`, '视图设置无效');
    if (d.view) for (const key of ['scale','panX','panY','spiderSpacing','timelineSpacing']) finite(d.view[key], `${path}.view.${key}`, -100000, 100000);
    return d;
  }
  function validateAppearance(value) {
    if (value == null) return { background: null };
    if (!plain(value)) fail('appearance', '外观必须是对象');
    const out = clone(value), b = out.background;
    if (b != null) {
      if (!plain(b)) fail('appearance.background', '背景必须是对象');
      if (b.mode != null && !['limited','unlimited'].includes(b.mode)) fail('appearance.background.mode', '背景模式无效');
      dataImage(b.imageData, 'appearance.background.imageData');
      finite(b.imageWidth, 'appearance.background.imageWidth', 0, 16384); finite(b.imageHeight, 'appearance.background.imageHeight', 0, 16384);
      if ((Number(b.imageWidth) || 0) * (Number(b.imageHeight) || 0) > 40000000) fail('appearance.background', '背景图片像素数过大');
      finite(b.canvasWidth, 'appearance.background.canvasWidth', 640, 24000); finite(b.canvasHeight, 'appearance.background.canvasHeight', 480, 16000);
    }
    return out;
  }
  function validateChat(value) {
    if (value == null) return [];
    if (!Array.isArray(value) || value.length > 1000) fail('aiChat', 'AI 记录数量无效');
    const out = clone(value);
    out.forEach((m, i) => { if (!plain(m)) fail(`aiChat[${i}]`, '记录无效'); for (const k of ['role','content','text']) text(m[k], `aiChat[${i}].${k}`, 1000000); });
    return out;
  }
  function validateAndNormalizeDocument(raw) {
    if (!plain(raw) || raw.app !== 'brace-mindmap') fail('document', '不是有效的 KnotJot 思维导图');
    if (raw.version != null && (!Number.isInteger(raw.version) || raw.version < 1 || raw.version > 2)) fail('version', '文件版本不受支持');
    const sourceSheets = Array.isArray(raw.sheets) ? raw.sheets : [raw];
    if (!sourceSheets.length || sourceSheets.length > 100) fail('sheets', '画布数量必须在 1 到 100 之间');
    const out = { app: 'brace-mindmap', version: 2, name: typeof raw.name === 'string' ? raw.name.slice(0, 200) : '', curSheet: Number.isInteger(raw.curSheet) ? raw.curSheet : 0 };
    out.sheets = sourceSheets.map((s, i) => validateSheet(s, `sheets[${i}]`));
    if (out.curSheet < 0 || out.curSheet >= out.sheets.length) fail('curSheet', '当前画布索引越界');
    out.appearance = validateAppearance(raw.appearance);
    out.aiChat = validateChat(raw.aiChat);
    return out;
  }
  return { validateAndNormalizeDocument, validateSheet, validateAppearance };
});
