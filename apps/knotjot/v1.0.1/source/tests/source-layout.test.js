'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const exists = (...parts) => /* EN: exists in module: call fs.existsSync, path.join; return fs.existsSync(path.join(root, ...parts)). ZH: module 中的 exists：调用 fs.existsSync、path.join；返回 fs.existsSync(path.join(root, ...parts))。 */ fs.existsSync(path.join(root, ...parts));
const read = (...parts) => /* EN: read in module: call fs.readFileSync, path.join; return fs.readFileSync(path.join(root, ...parts), 'utf8'). ZH: module 中的 read：调用 fs.readFileSync、path.join；返回 fs.readFileSync(path.join(root, ...parts), 'utf8')。 */ fs.readFileSync(path.join(root, ...parts), 'utf8');
const assertRootPathExists = (relativePath, label = relativePath) => {
/* EN: assertRootPathExists in module: call String(relativePath).split, assert.equal, exists.
   ZH: module 中的 assertRootPathExists：调用 String(relativePath).split、assert.equal、exists。 */

  const cleanPath = String(relativePath).split(/[?#]/, 1)[0];
  assert.equal(exists(cleanPath), true, `${label} must resolve to an existing path`);
};

test('uses the compact source layout', () => {
/* EN: Verify uses the compact source layout: call assert.equal, exists, requiredFile.join.
   ZH: 验证回归场景 uses the compact source layout：调用 assert.equal、exists、requiredFile.join。 */

  for (const directory of ['resources', 'src', 'tests']) {
    assert.equal(exists(directory), true, `${directory} must exist`);
  }

  for (const legacyDirectory of [
    'assets',
    'build',
    'libs',
    'text-styles',
    'themes',
    'THIRD_PARTY_LICENSES'
  ]) {
    assert.equal(exists(legacyDirectory), false, `${legacyDirectory} must be consolidated`);
  }

  for (const requiredFile of [
    ['resources', 'icons', 'icon.ico'],
    ['resources', 'icons', 'icon-256.png'],
    ['resources', 'licenses', 'Electron-LICENSE.txt'],
    ['resources', 'skins', 'xp', 'XP.css'],
    ['resources', 'text-styles', 'custom-text-styles.json'],
    ['resources', 'themes', '01-经典蓝.knotjot-theme'],
    ['resources', 'vendor', 'jspdf.umd.min.js'],
    ['src', 'renderer', 'appearance.css'],
    ['src', 'renderer', 'appearance.js'],
    ['src', 'renderer', 'text-styles.css'],
    ['src', 'renderer', 'text-styles.js']
  ]) {
    assert.equal(exists(...requiredFile), true, requiredFile.join('/') + ' must exist');
  }

  assert.equal(exists('installer.nsi'), false, 'obsolete installer.nsi must be removed');
});

test('entry documents reference local files that exist', () => {
/* EN: Verify entry documents reference local files that exist: call read, html.matchAll, tag[0].match.
   ZH: 验证回归场景 entry documents reference local files that exist：调用 read、html.matchAll、tag[0].match。 */

  const html = read('index.html');
  for (const tag of html.matchAll(/<(?:link|script|img)\b[^>]*>/gi)) {
    const attribute = tag[0].match(/\b(?:href|src)=["']([^"']+)["']/i);
    if (!attribute) continue;
    const reference = attribute[1];
    if (/^(?:[a-z]+:|#|data:)/i.test(reference)) continue;
    assertRootPathExists(reference, `index.html reference ${reference}`);
  }

  const readme = read('README.md');
  const readmeIcon = readme.match(/<img\s+src=["']([^"']+)["']/i);
  assert.notEqual(readmeIcon, null, 'README.md must contain a local icon');
  assertRootPathExists(readmeIcon[1], `README.md icon ${readmeIcon[1]}`);
});

test('electron-builder inputs resolve to existing source paths', () => {
/* EN: Verify electron-builder inputs resolve to existing source paths: call JSON.parse, read, assert.equal.
   ZH: 验证回归场景 electron-builder inputs resolve to existing source paths：调用 JSON.parse、read、assert.equal。 */

  const packageJson = JSON.parse(read('package.json'));
  assert.equal(packageJson.scripts['test:source-layout'], 'node --test tests/source-layout.test.js');

  assertRootPathExists(packageJson.build.directories.buildResources, 'buildResources');
  for (const configuredPath of packageJson.build.files) {
    const sourcePath = configuredPath.replace(/\/\*\*\/\*$/, '');
    assertRootPathExists(sourcePath, `build.files entry ${configuredPath}`);
  }

  for (const resource of packageJson.build.extraResources) {
    assertRootPathExists(resource.from, `extraResources source ${resource.from}`);
  }

  for (const iconPath of [
    packageJson.build.win.icon,
    packageJson.build.nsis.installerIcon,
    packageJson.build.nsis.uninstallerIcon,
    packageJson.build.nsis.installerHeaderIcon,
    ...packageJson.build.fileAssociations.map((association) => /* EN: Derive the next value for packageJson.build.fileAssociations.map: return association.icon. ZH: 计算 packageJson.build.fileAssociations.map 的下一项结果：返回 association.icon。 */ association.icon)
  ]) {
    assertRootPathExists(iconPath, `configured icon ${iconPath}`);
  }
});
