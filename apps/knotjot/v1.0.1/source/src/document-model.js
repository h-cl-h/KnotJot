(function (root, factory) {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: call factory; update module.exports, root.KnotJotDocumentModel.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：调用 factory；更新 module.exports、root.KnotJotDocumentModel。 */

  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.KnotJotDocumentModel = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
/* EN: Callback for function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp: return { validateAndNormalizeDocument, validateSheet, validateAppearance }.
   ZH: function (root, factory) { const api = factory(); if (typeof module === 'object' && module.exports) module.exp 的回调：返回 { validateAndNormalizeDocument, validateSheet, validateAppearance }。 */

  'use strict';
  const DICTS = ['textboxes','braces','rbraces','fgroups','links','boundaries','summaries','callouts','relations'];
  const plain = v => /* EN: plain in module: call Array.isArray, Object.getPrototypeOf. ZH: module 中的 plain：调用 Array.isArray、Object.getPrototypeOf。 */ !!v && typeof v === 'object' && !Array.isArray(v) && (Object.getPrototypeOf(v) === Object.prototype || Object.getPrototypeOf(v) === null);
  const clone = value => /* EN: clone in module: call JSON.parse, JSON.stringify; return JSON.parse(JSON.stringify(value)). ZH: module 中的 clone：调用 JSON.parse、JSON.stringify；返回 JSON.parse(JSON.stringify(value))。 */ JSON.parse(JSON.stringify(value));
  function fail(path, message) {
/* EN: fail in module: complete without changing state.
   ZH: module 中的 fail：完成且不修改状态。 */
 throw new Error(`${path}：${message}`); }
  function finite(value, path, min = -10000000, max = 10000000) {
/* EN: finite in module: call Number.isFinite, fail.
   ZH: module 中的 finite：调用 Number.isFinite、fail。 */

    if (value == null) return;
    if (typeof value !== 'number' || !Number.isFinite(value) || value < min || value > max) fail(path, '数值超出允许范围');
  }
  function text(value, path, max) {
/* EN: text in module: call fail.
   ZH: module 中的 text：调用 fail。 */
 if (value != null && (typeof value !== 'string' || value.length > max)) fail(path, `必须是长度不超过 ${max} 的文本`); }
  function ids(value, path, target, max = 50000) {
/* EN: ids in module: call Array.isArray, fail, value.forEach.
   ZH: module 中的 ids：调用 Array.isArray、fail、value.forEach。 */

    if (!Array.isArray(value) || value.length > max) fail(path, '必须是合理长度的 ID 数组');
    value.forEach((id, i) => {
/* EN: Process each item in value.forEach: call Object.prototype.hasOwnProperty.call, fail.
   ZH: 逐项处理 value.forEach 中的元素：调用 Object.prototype.hasOwnProperty.call、fail。 */
 if (typeof id !== 'string' || !Object.prototype.hasOwnProperty.call(target, id)) fail(`${path}[${i}]`, '引用了不存在的记录'); });
  }
  function dataImage(value, path) {
/* EN: dataImage in module: call /^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test, fail.
   ZH: module 中的 dataImage：调用 /^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test、fail。 */

    if (value == null || value === '') return;
    // EN: Twelve MiB of original bytes needs sixteen MiB of base64 plus the data-URL header.
    // ZH: 12 MiB 原始字节需要 16 MiB Base64 空间及 data URL 头部。
    if (typeof value !== 'string' || value.length > 16 * 1024 * 1024 + 64 || !/^data:image\/(?:png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(value)) fail(path, '图片数据无效或过大');
  }
  function validateSheet(sheet, path) {
/* EN: Validate optional sheet appearance before activation; leave missing legacy records available for file-level migration.
   ZH: 激活前验证可选画布外观；保留旧记录缺失状态，以便从文件级外观迁移。 */
/* EN: Validate node dictionaries, geometry and cross-record references before activating a sheet.
   ZH: 激活画布前验证节点字典、几何值和记录间引用。 */

    if (!plain(sheet)) fail(path, '画布必须是对象');
    const d = clone(sheet);
    if(d.appearance!=null)d.appearance=validateAppearance(d.appearance);
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
      // EN: Optional source dimensions retain original resolution independently of display size.
      // ZH: 可选原图尺寸独立于显示尺寸记录原始分辨率。
      if(item.img){finite(item.img.naturalWidth, `${path}.textboxes.${id}.img.naturalWidth`,1,16384);finite(item.img.naturalHeight, `${path}.textboxes.${id}.img.naturalHeight`,1,16384);}
      for (const field of ['markers','tags']) if (item[field] != null && (!Array.isArray(item[field]) || item[field].length > 1000 || item[field].some(x => /* EN: Test an item for item[field].some: return typeof x !== 'string' || x.length > 1000. ZH: 判断 item[field].some 的元素条件：返回 typeof x !== 'string' || x.length > 1000。 */ typeof x !== 'string' || x.length > 1000))) fail(`${path}.textboxes.${id}.${field}`, '列表无效或过大');
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
    // EN: Optional callout offsets preserve old automatic positions and reject invalid coordinates.
    // ZH: 可选标注偏移保留旧记录的自动位置，并拒绝无效坐标。
    for (const [id,item] of Object.entries(d.callouts)){finite(item.offsetX,`${path}.callouts.${id}.offsetX`);finite(item.offsetY,`${path}.callouts.${id}.offsetY`);}
    for (const [id, item] of Object.entries(d.relations)) if (!plain(item) || !d.textboxes[item.a] || !d.textboxes[item.b]) fail(`${path}.relations.${id}`, '联系端点无效');
    if (d.roots == null) d.roots = [];
    ids(d.roots, `${path}.roots`, d.textboxes);
    validateHierarchy(d, path);
    if (d.focusId != null && !d.textboxes[d.focusId]) fail(`${path}.focusId`, '聚焦项不存在');
    if (d.view != null && !plain(d.view)) fail(`${path}.view`, '视图设置无效');
    if (d.view) for (const key of ['scale','panX','panY','spiderSpacing','timelineSpacing']) finite(d.view[key], `${path}.view.${key}`, -100000, 100000);
    return d;
  }
  function validateHierarchy(d, path) {
    // EN: Check reciprocal tree ownership and iteratively bound depth before recursive layout; free spider links are deliberately excluded.
    // ZH: 递归布局前检查树的双向归属并迭代限制深度；蜘蛛网自由连线不参与树校验。
    const owners = new Map(), depths = new Map();
    for (const [bid, b] of Object.entries(d.braces)) {
      if (!b.parentTb || d.textboxes[b.parentTb].brace !== bid) fail(path, '大括号父节点归属不一致');
      for (const id of b.children || []) {
        if (owners.has(id) || d.textboxes[id].parentBrace !== bid) fail(path, '子节点重复或归属不一致');
        owners.set(id, b.parentTb);
      }
    }
    for (const [id, node] of Object.entries(d.textboxes)) {
      if (node.parentBrace != null && !owners.has(id)) fail(path, '父大括号未包含此子节点');
      if (node.brace != null && d.braces[node.brace].parentTb !== id) fail(path, '节点大括号归属不一致');
      const pending = [], seen = new Set(); let cursor = id;
      while (cursor != null && !depths.has(cursor)) {
        if (seen.has(cursor)) fail(path, '树层级存在循环');
        seen.add(cursor); pending.push(cursor); cursor = owners.get(cursor);
        if (pending.length > 256) fail(path, '树层级超过 256 层');
      }
      let depth = depths.get(cursor) || 0;
      while (pending.length) { if (++depth > 256) fail(path, '树层级超过 256 层'); depths.set(pending.pop(), depth); }
    }
    const roots = new Set();
    for (const id of d.roots) {
      if (roots.has(id) || (d.docType !== 'spider' && owners.has(id))) fail(path, '根节点重复或归属矛盾');
      roots.add(id);
    }
  }
  function validateAppearance(value) {
/* EN: Accept only a plain optional hexadecimal sheet color, never executable CSS or arbitrary style strings.
   ZH: 画布底色仅接受可选十六进制颜色，禁止可执行 CSS 或任意样式字符串。 */
/* EN: Validate optional persisted appearance or conversation data and return a detached normalized copy.
   ZH: 验证可选的持久化外观或对话数据，返回独立的规范副本。 */

    if (value == null) return { background: null };
    if (!plain(value)) fail('appearance', '外观必须是对象');
    const out = clone(value), b = out.background;
    if (b != null) {
      if (!plain(b)) fail('appearance.background', '背景必须是对象');
      if(b.color!=null&&b.color!==''&&(typeof b.color!=='string'||!/^#[0-9a-f]{6}$/i.test(b.color)))fail('appearance.background.color','背景颜色无效');
      if (b.mode != null && !['limited','unlimited'].includes(b.mode)) fail('appearance.background.mode', '背景模式无效');
      dataImage(b.imageData, 'appearance.background.imageData');
      finite(b.imageWidth, 'appearance.background.imageWidth', 0, 16384); finite(b.imageHeight, 'appearance.background.imageHeight', 0, 16384);
      if ((Number(b.imageWidth) || 0) * (Number(b.imageHeight) || 0) > 40000000) fail('appearance.background', '背景图片像素数过大');
      finite(b.canvasWidth, 'appearance.background.canvasWidth', 640, 24000); finite(b.canvasHeight, 'appearance.background.canvasHeight', 480, 16000);
    }
    return out;
  }
  function validateChat(value) {
/* EN: Validate optional persisted appearance or conversation data and return a detached normalized copy.
   ZH: 验证可选的持久化外观或对话数据，返回独立的规范副本。 */

    if (value == null) return [];
    if (!Array.isArray(value) || value.length > 1000) fail('aiChat', 'AI 记录数量无效');
    const out = clone(value);
    out.forEach((m, i) => {
/* EN: Process each item in out.forEach: call plain, fail, text.
   ZH: 逐项处理 out.forEach 中的元素：调用 plain、fail、text。 */
 if (!plain(m)) fail(`aiChat[${i}]`, '记录无效'); for (const k of ['role','content','text']) text(m[k], `aiChat[${i}].${k}`, 1000000); });
    return out;
  }
  function validateAndNormalizeDocument(raw) {
/* EN: Validate the document signature and version, normalize all sheets, and check the active sheet index.
   ZH: 验证文档标识和版本、规范所有画布，并检查活动画布索引。 */

    if (!plain(raw) || raw.app !== 'brace-mindmap') fail('document', '不是有效的 KnotJot 思维导图');
    if (raw.version != null && (!Number.isInteger(raw.version) || raw.version < 1 || raw.version > 2)) fail('version', '文件版本不受支持');
    const sourceSheets = Array.isArray(raw.sheets) ? raw.sheets : [raw];
    if (!sourceSheets.length || sourceSheets.length > 100) fail('sheets', '画布数量必须在 1 到 100 之间');
    const out = { app: 'brace-mindmap', version: 2, name: typeof raw.name === 'string' ? raw.name.slice(0, 200) : '', curSheet: Number.isInteger(raw.curSheet) ? raw.curSheet : 0 };
    out.sheets = sourceSheets.map((s, i) => /* EN: Derive the next value for sourceSheets.map: call validateSheet; return validateSheet(s, `sheets[${i}]`). ZH: 计算 sourceSheets.map 的下一项结果：调用 validateSheet；返回 validateSheet(s, `sheets[${i}]`)。 */ validateSheet(s, `sheets[${i}]`));
    if (out.curSheet < 0 || out.curSheet >= out.sheets.length) fail('curSheet', '当前画布索引越界');
    out.appearance = validateAppearance(raw.appearance);
    out.aiChat = validateChat(raw.aiChat);
    return out;
  }
  return { validateAndNormalizeDocument, validateSheet, validateAppearance };
});
