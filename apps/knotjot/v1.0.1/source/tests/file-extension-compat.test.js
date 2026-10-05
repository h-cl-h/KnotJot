'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const extensions = require('../src/file-extensions');

test('accepts new and legacy files only in their own categories', () => {
/* EN: Verify accepts new and legacy files only in their own categories: call assert.equal, extensions.isMapFile, extensions.isSkinFile.
   ZH: 验证回归场景 accepts new and legacy files only in their own categories：调用 assert.equal、extensions.isMapFile、extensions.isSkinFile。 */

  assert.equal(extensions.isMapFile('plan.knotjot'), true);
  assert.equal(extensions.isMapFile('legacy.bmap'), true);
  assert.equal(extensions.isMapFile('skin.knotjot-ui'), false);
  assert.equal(extensions.isSkinFile('skin.knotjot-ui'), true);
  assert.equal(extensions.isSkinFile('legacy.bmapui'), true);
  assert.equal(extensions.isThemeFile('palette.knotjot-theme'), true);
  assert.equal(extensions.isThemeFile('legacy.bmaptheme'), true);
  assert.equal(extensions.removeMapExtension('legacy.bmap'), 'legacy');
  assert.equal(extensions.removeMapExtension('plan.knotjot'), 'plan');
});
