const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = (name) => /* EN: read in module: call fs.readFileSync, path.join; return fs.readFileSync(path.join(root, name), 'utf8'). ZH: module 中的 read：调用 fs.readFileSync、path.join；返回 fs.readFileSync(path.join(root, name), 'utf8')。 */ fs.readFileSync(path.join(root, name), 'utf8');

test('V1.0.0 uses KnotJot as the visible product brand', () => {
/* EN: Verify V1.0.0 uses KnotJot as the visible product brand: call [read('index.html'), read('README.md')].join, read, assert.doesNotMatch.
   ZH: 验证回归场景 V1.0.0 uses KnotJot as the visible product brand：调用 [read('index.html'), read('README.md')].join、read、assert.doesNotMatch。 */

  const visible = [read('index.html'), read('README.md')].join('\n');
  assert.doesNotMatch(visible, /\bBAMP\b/i);
  assert.doesNotMatch(visible, /BMAP\s+(?:Mind|思维|主程序)/i);
  assert.match(read('package.json'), /"productName"\s*:\s*"KnotJot"/);
});

test('new file names are defaults while legacy BMAP files remain readable', () => {
/* EN: Verify new file names are defaults while legacy BMAP files remain readable: call read, assert.match.
   ZH: 验证回归场景 new file names are defaults while legacy BMAP files remain readable：调用 read、assert.match。 */

  const source = read('src/file-extensions.js');
  assert.match(source, /mapExtensions\s*=\s*\[\s*['"]\.knotjot['"]/);
  assert.match(source, /mapDefaultExtension:\s*mapExtensions\[0\]/);
  assert.match(source, /['"]\.bmap['"]/);
});

test('UI editor handshake uses KnotJot flag and retains the legacy alias', () => {
/* EN: Verify UI editor handshake uses KnotJot flag and retains the legacy alias: call read, assert.match.
   ZH: 验证回归场景 UI editor handshake uses KnotJot flag and retains the legacy alias：调用 read、assert.match。 */

  const source = read('main.js');
  assert.match(source, /--knotjot-ui-skins-location-file=/);
  assert.match(source, /--bmap-ui-skins-location-file=/);
});
