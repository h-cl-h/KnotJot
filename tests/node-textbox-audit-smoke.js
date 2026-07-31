const { app, BrowserWindow } = require('electron');
const path = require('path');
const os = require('os');
const fs = require('fs');

const sourceLibraryPath = path.join(__dirname, '..', 'resources', 'text-styles', 'custom-text-styles.json');
const sourceLibraryExisted = fs.existsSync(sourceLibraryPath);
const sourceLibraryBeforeTest = sourceLibraryExisted ? fs.readFileSync(sourceLibraryPath) : null;
let sourceLibraryRestored = false;
function restoreSourceLibrary() {
  if (sourceLibraryRestored) return;
  sourceLibraryRestored = true;
  if (sourceLibraryExisted) fs.writeFileSync(sourceLibraryPath, sourceLibraryBeforeTest);
  else if (fs.existsSync(sourceLibraryPath)) fs.unlinkSync(sourceLibraryPath);
}

app.commandLine.appendSwitch('disable-gpu');
process.env.KNOTJOT_INTEGRATION_TEST = '1';
app.setPath('userData', path.join(os.tmpdir(), `knotjot-node-textbox-audit-${process.pid}`));
require('../main');

function fail(message, details) {
  restoreSourceLibrary();
  console.error('NODE_TEXTBOX_AUDIT_FAILED', message, details || '');
  app.exit(1);
}

app.whenReady().then(async () => {
  const watchdog = setTimeout(() => fail('测试超时'), 30000);
  const win = new BrowserWindow({
    show: false,
    width: 1440,
    height: 900,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, '..', 'preload.js')
    }
  });

  try {
    await win.loadFile(path.join(__dirname, '..', 'index.html'));
    const result = await win.webContents.executeJavaScript(`(async () => {
      const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
      const check = (value, message, details) => {
        if (!value) throw new Error(message + (details == null ? '' : ' | ' + JSON.stringify(details)));
      };
      const menuLabels = () => [...ctx.querySelectorAll('.item')].map(x => x.textContent.trim());
      const styleMenu = id => {
        const card = document.querySelector('.ts-card[data-id="' + id + '"]');
        check(card, '样式卡片不存在', id);
        card.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 900, clientY: 240 }));
        return document.querySelector('.ts-card-menu');
      };
      const attached = () => Object.values(TB).filter(t => t && t._nodeAttach);
      const attachKey = t => structuralNodeRefKey(t._nodeAttach);
      const centered = t => {
        const p = structuralNodePoint(t._nodeAttach);
        return !!p && Math.abs(t._x + t._w / 2 - p.x) < 0.11 && Math.abs(t._cy - p.y) < 0.11;
      };

      TB = {}; BR = {}; RB = {}; FG = {}; LK = {}; roots = []; idc = 0;
      if (typeof loadExtras === 'function') loadExtras({});
      update();

      await api.saveTextStyles({
        format: 'knotjot-text-styles', version: 1, defaultStyleId: 'node-a',
        styles: [
          { id: 'node-a', name: '节点样式 A', scope: 'node', bg: '#eaf4ff', border: '#4d93e6', color: '#174f8f', radius: 12 },
          { id: 'node-b', name: '节点样式 B', scope: 'node', bg: '#eafff6', border: '#37b77b', color: '#176644', radius: 18 }
        ]
      });
      await TextStyleFeature.loadExternal();

      const structures = ['timeline', 'fishbone', 'tree', 'orgD', 'treetable', 'brace', 'matrix'];
      const refsByStructure = {};
      structures.forEach((st, index) => {
        const root = newTextbox(st + ' 根');
        TB[root].x = 600 + (index % 3) * 1300;
        TB[root].y = 450 + Math.floor(index / 3) * 950;
        TB[root].struct = st;
        const bid = nid('b');
        TB[root].brace = bid;
        BR[bid] = { id: bid, locked: false, parentTb: root, children: [] };
        refsByStructure[st] = [];
        for (let i = 0; i < 2; i++) {
          const child = newTextbox(st + ' 子项 ' + (i + 1));
          TB[child].parentBrace = bid;
          BR[bid].children.push(child);
          refsByStructure[st].push({ braceId: bid, childId: child });
        }
        roots.push(root);
      });
      update(); layout();

      const supported = ['timeline', 'fishbone', 'tree', 'orgD', 'treetable'];
      const refs = structuralNodeRefs();
      check(refs.length === supported.length * 2, '可放置节点引用数量错误', { actual: refs.length, expected: supported.length * 2 });
      check(!refs.some(r => refsByStructure.brace.some(x => structuralNodeRefKey(x) === structuralNodeRefKey(r))), '大括号不应暴露结构节点');
      check(!refs.some(r => refsByStructure.matrix.some(x => structuralNodeRefKey(x) === structuralNodeRefKey(r))), '矩阵不应暴露结构节点');

      for (const st of supported) {
        for (const ref of refsByStructure[st]) {
          const hit = document.querySelector('.struct-node[data-node-brace="' + ref.braceId + '"][data-node-child="' + ref.childId + '"]');
          check(hit, '结构节点没有独立命中元素', { st, ref });
          hit.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 650, clientY: 300 }));
          const labels = menuLabels();
          check(labels.some(x => x.includes('在节点上放置文本框')), '节点右键菜单缺少默认样式放置', { st, labels });
          check(labels.some(x => x.includes('选择样式并放置')), '节点右键菜单缺少指定样式放置', { st, labels });
          closeMenu();
        }
      }

      const singleRef = refsByStructure.timeline[0];
      const singleHit = document.querySelector('.struct-dot[data-node-brace="' + singleRef.braceId + '"][data-node-child="' + singleRef.childId + '"]');
      check(singleHit, '时间轴圆点不存在');
      singleHit.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 650, clientY: 300 }));
      [...ctx.querySelectorAll('.item')].find(x => x.textContent.includes('在节点上放置文本框')).click();
      if (editing) commitEdit(editing);
      let one = attached().filter(t => attachKey(t) === structuralNodeRefKey(singleRef));
      check(one.length === 1 && one[0].textStyleId === 'node-a', '默认样式节点文本框放置失败', one);

      singleHit.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 650, clientY: 300 }));
      [...ctx.querySelectorAll('.item')].find(x => x.textContent.includes('在节点上放置文本框')).click();
      if (editing) commitEdit(editing);
      one = attached().filter(t => attachKey(t) === structuralNodeRefKey(singleRef));
      check(one.length === 1, '同一节点再次放置产生重复文本框', one.map(t => t.id));

      singleHit.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 650, clientY: 300 }));
      [...ctx.querySelectorAll('.item')].find(x => x.textContent.includes('选择样式并放置')).click();
      check(document.body.classList.contains('ts-panel-open'), '选择样式并放置没有打开样式面板');
      document.querySelector('.ts-card[data-id="node-b"]').click();
      one = attached().filter(t => attachKey(t) === structuralNodeRefKey(singleRef));
      check(one.length === 1 && one[0].textStyleId === 'node-b' && one[0].textStylePlacement === 'node', '指定样式没有应用到既有节点文本框', one);
      const overrideProbe = one[0];
      overrideProbe.textRules = { type: 'number', maxLength: 2 };
      overrideProbe._textRulesOverride = true;
      overrideProbe.textSizing = { mode: 'fixed', width: 91, height: 37 };
      overrideProbe._textSizingOverride = true;

      const menu = styleMenu('node-a');
      const actions = [...menu.querySelectorAll('button')].map(x => ({ action: x.dataset.a, text: x.textContent.trim(), disabled: x.disabled }));
      check(actions[0] && actions[0].action === 'default' && actions[0].text === '设为默认文本框样式', '样式菜单第一项错误', actions);
      check(actions[1] && actions[1].action === 'apply-all' && actions[1].text === '应用于所有文本框', '样式菜单第二项错误', actions);
      check(actions[2] && actions[2].action === 'place-nodes' && actions[2].text === '在所有节点上放置此样式文本框', '样式菜单第三项错误', actions);
      menu.querySelector('[data-a="place-nodes"]').click();
      check(attached().length === refs.length, '所有节点放置数量错误', { attached: attached().length, refs: refs.length });
      check(attached().every(t => t.textStyleId === 'node-a' && t.textStylePlacement === 'node' && centered(t)), '所有节点样式或定位错误');
      const bulkRestyleClearedOverrides = overrideProbe.textRules == null && overrideProbe.textSizing == null &&
        overrideProbe._textRulesOverride == null && overrideProbe._textSizingOverride == null;

      styleMenu('node-b').querySelector('[data-a="place-nodes"]').click();
      check(attached().length === refs.length, '再次批量放置产生重复文本框', { attached: attached().length, refs: refs.length });
      check(attached().every(t => t.textStyleId === 'node-b' && centered(t)), '再次批量放置没有更新既有节点样式');

      styleMenu('node-a').querySelector('[data-a="apply-all"]').click();
      check(Object.values(TB).filter(t => !t._ghost).every(t => t.textStyleId === 'node-a'), '应用于所有文本框没有覆盖全部文本框');
      styleMenu('node-b').querySelector('[data-a="default"]').click();
      await pause(30);
      check(TextStyleFeature.getDefaultStyleId() === 'node-b', '设为默认样式失败');
      const regular = newTextbox('默认样式检查');
      check(TB[regular].textStyleId === 'node-b', '新建普通文本框没有采用默认样式', TB[regular]);
      TB[regular].x = 5200; TB[regular].y = 2600; roots.push(regular); update();

      const beforeSave = new Map(attached().map(t => [attachKey(t), { id: t.id, style: t.textStyleId, text: t.text }]));
      check(attached().every(centered), '保存前存在未以节点为中心的文本框');
      const json = serialize();
      const parsed = JSON.parse(json);
      check(Object.values(parsed.sheets[0].textboxes).filter(t => t._nodeAttach).length === refs.length, '序列化丢失节点文本框');
      check(applyLoadedDocument(parsed, '节点文本框审计'), '重新打开序列化文档失败');
      await pause(80);
      const afterLoadRefs = structuralNodeRefs();
      check(afterLoadRefs.length === refs.length, '重新打开后结构节点引用数量变化');
      check(attached().length === refs.length, '重新打开后节点文本框数量变化');
      check(attached().every(t => beforeSave.has(attachKey(t)) && t.textStyleId === beforeSave.get(attachKey(t)).style && centered(t)), '重新打开后样式、关联或定位错误');
      const idsBeforeRepeat = attached().map(t => t.id).sort().join(',');
      const reportedCount = placeTextStyleOnAllStructuralNodes('node-b');
      const idsAfterRepeat = attached().map(t => t.id).sort().join(',');
      check(reportedCount === refs.length && idsAfterRepeat === idsBeforeRepeat, '重开后再次放置没有正确去重', { reportedCount, before: idsBeforeRepeat, after: idsAfterRepeat });

      const cnTree = aiParseOutline('# AI 时间轴\\n- 阶段一 {节点: 里程碑}\\n- 阶段二 {node: Gate}');
      const aiSheet = aiBuildSheet(cnTree, 'timeline', 'AI 节点审计');
      const aiAttached = Object.values(aiSheet.textboxes).filter(t => t._nodeAttach);
      check(aiAttached.length === 2, 'AI 没有按中英文节点标记生成节点文本框', aiAttached);
      check(aiAttached.some(t => t.text === '里程碑') && aiAttached.some(t => t.text === 'Gate'), 'AI 节点文本内容错误', aiAttached.map(t => t.text));
      check(aiAttached.every(t => t.textStylePlacement === 'node' && t.textStyleId === 'node-b'), 'AI 节点文本框没有采用默认样式', aiAttached);
      const aiNoNode = aiBuildSheet(cnTree, 'brace', 'AI 大括号审计');
      check(!Object.values(aiNoNode.textboxes).some(t => t._nodeAttach), '大括号结构不应生成不存在的结构节点文本框');

      const lifecycleRef = structuralNodeRefs()[0];
      const lifecycleAttached = attached().find(t => attachKey(t) === structuralNodeRefKey(lifecycleRef));
      deleteTb(lifecycleRef.childId); update();
      const orphanAfterChildDelete = !!TB[lifecycleAttached.id];

      return {
        supportedStructures: supported,
        structuralRefCount: refs.length,
        rightClickChecked: refs.length,
        deDuplicated: idsAfterRepeat === idsBeforeRepeat,
        serializedAndCentered: attached().filter(t => t.id !== lifecycleAttached.id).every(centered),
        aiNodeCount: aiAttached.length,
        bulkRestyleClearedOverrides,
        orphanAfterChildDelete
      };
    })()`);

    console.log('NODE_TEXTBOX_AUDIT_RESULT', JSON.stringify(result));
    if (!result.bulkRestyleClearedOverrides || result.orphanAfterChildDelete) {
      clearTimeout(watchdog);
      return fail('节点文本框生命周期检查失败', result);
    }
    clearTimeout(watchdog);
    restoreSourceLibrary();
    app.exit(0);
  } catch (err) {
    clearTimeout(watchdog);
    fail(err.stack || String(err));
  }
});
