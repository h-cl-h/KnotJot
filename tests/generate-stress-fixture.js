const fs = require('fs');
const path = require('path');

const SHEET_COUNT = 24;
const NODES_PER_SHEET = 120;

function makeSheet(sheetNumber) {
  const textboxes = {};
  const children = [];
  textboxes.t1 = {
    id: 't1', text: `压力测试画布 ${sheetNumber}`, locked: false,
    parentBrace: null, brace: 'b1', x: 3000, y: 1800
  };
  for (let i = 2; i <= NODES_PER_SHEET; i++) {
    const id = `t${i}`;
    children.push(id);
    textboxes[id] = {
      id, text: `节点 ${sheetNumber}-${i}`, locked: false,
      parentBrace: 'b1', brace: null, x: 0, y: 0
    };
  }
  return {
    name: `画布 ${sheetNumber}`,
    docType: 'brace', idc: NODES_PER_SHEET, focusId: null, roots: ['t1'], textboxes,
    braces: { b1: { id: 'b1', locked: false, parentTb: 't1', children } },
    rbraces: {}, fgroups: {}, links: {}, boundaries: {}, summaries: {}, callouts: {}, relations: {},
    customMarkers: [], legend: { on: false, x: null, y: null, texts: {} },
    view: { numbering: '', compactMode: sheetNumber === 1 ? 2 : 0, scale: 1, panX: -2200, panY: -1300 }
  };
}

const document = {
  app: 'brace-mindmap', version: 2, name: 'KnotJot 压力测试', curSheet: 0,
  sheets: Array.from({ length: SHEET_COUNT }, (_, i) => makeSheet(i + 1))
};

const outputDir = path.join(__dirname, 'fixtures');
const outputPath = path.join(outputDir, 'KnotJot-压力测试-24画布-2880节点.knotjot');
fs.mkdirSync(outputDir, { recursive: true });
fs.writeFileSync(outputPath, JSON.stringify(document), 'utf8');
console.log(outputPath);
