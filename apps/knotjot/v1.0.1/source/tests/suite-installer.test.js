'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const sourceRoot = path.resolve(__dirname, '..');
const versionRoot = path.resolve(sourceRoot, '..');
const workspaceRoot = path.resolve(sourceRoot, '..', '..', '..', '..');
const suiteScript = path.join(sourceRoot, 'packaging', 'KnotJotSuite.nsi');

const artifacts = [
  {
    label: 'main',
    name: 'KnotJot-Setup-1.0.1.exe',
    file: path.join(versionRoot, 'installers', 'KnotJot-Setup-1.0.1.exe'),
    sums: path.join(versionRoot, 'installers', 'SHA256SUMS.txt'),
  },
  {
    label: 'ui',
    name: 'KnotJot-UI-Editor-Setup-1.0.1.exe',
    file: path.join(workspaceRoot, 'apps', 'knotjot-ui-editor', 'v1.0.1', 'installers', 'KnotJot-UI-Editor-Setup-1.0.1.exe'),
    sums: path.join(workspaceRoot, 'apps', 'knotjot-ui-editor', 'v1.0.1', 'installers', 'SHA256SUMS.txt'),
  },
  {
    label: 'text',
    name: 'KnotJot-Text-Style-Editor-Setup-1.0.1.exe',
    file: path.join(workspaceRoot, 'apps', 'knotjot-text-style-editor', 'v1.0.1', 'installers', 'KnotJot-Text-Style-Editor-Setup-1.0.1.exe'),
    sums: path.join(workspaceRoot, 'apps', 'knotjot-text-style-editor', 'v1.0.1', 'installers', 'SHA256SUMS.txt'),
  },
];

function sha256(file) {
/* EN: sha256 in module: call crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase, crypto.createHash('sha256').update(fs.readFileSync(file)).digest, crypto.createHash('sha256').update.
   ZH: module 中的 sha256：调用 crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase、crypto.createHash('sha256').update(fs.readFileSync(file)).digest、crypto.createHash('sha256').update。 */

  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
}

function checksumFor(sumFile, targetName) {
/* EN: checksumFor in module: call fs.readFileSync(sumFile, 'utf8').split(/\r?\n/).filter, fs.readFileSync(sumFile, 'utf8').split, fs.readFileSync.
   ZH: module 中的 checksumFor：调用 fs.readFileSync(sumFile, 'utf8').split(/\r?\n/).filter、fs.readFileSync(sumFile, 'utf8').split、fs.readFileSync。 */

  const entries = fs.readFileSync(sumFile, 'utf8').split(/\r?\n/).filter(Boolean);
  const match = entries.map((line) => /* EN: Derive the next value for entries.map: call line.match; return line.match(/^([0-9A-F]{64})\s+(.+)$/i). ZH: 计算 entries.map 的下一项结果：调用 line.match；返回 line.match(/^([0-9A-F]{64})\s+(.+)$/i)。 */ line.match(/^([0-9A-F]{64})\s+(.+)$/i))
    .find((parts) => /* EN: Test an item for entries.map((line) => line.match(/^([0-9A-F]{64})\s+(.+)$/i)) .find: call parts[2].trim; return parts && parts[2].trim() === targetName. ZH: 判断 entries.map((line) => line.match(/^([0-9A-F]{64})\s+(.+)$/i)) .find 的元素条件：调用 parts[2].trim；返回 parts && parts[2].trim() === targetName。 */ parts && parts[2].trim() === targetName);
  assert.ok(match, `${targetName} is missing from ${sumFile}`);
  return match[1].toUpperCase();
}

function escapeRegExp(value) {
/* EN: escapeRegExp in module: call value.replace.
   ZH: module 中的 escapeRegExp：调用 value.replace。 */

  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function sectionBlock(script, sectionLabel, sectionId) {
/* EN: sectionBlock in module: call script.match, escapeRegExp, assert.ok.
   ZH: module 中的 sectionBlock：调用 script.match、escapeRegExp、assert.ok。 */

  const match = script.match(new RegExp(`Section "${escapeRegExp(sectionLabel)}" ${escapeRegExp(sectionId)}([\\s\\S]*?)SectionEnd`));
  assert.ok(match, `missing NSIS section ${sectionId}`);
  return match[0];
}

function macroBlock(script, macroName) {
/* EN: macroBlock in module: call script.match, escapeRegExp, assert.ok.
   ZH: module 中的 macroBlock：调用 script.match、escapeRegExp、assert.ok。 */

  const match = script.match(new RegExp(`!macro ${escapeRegExp(macroName)}[^\\r\\n]*([\\s\\S]*?)!macroend`));
  assert.ok(match, `missing NSIS macro ${macroName}`);
  return match[0];
}

function assertRunInstallerFailureFlow(macro) {
/* EN: assertRunInstallerFailureFlow in module: call macro.indexOf, assert.ok; update previousIndex.
   ZH: module 中的 assertRunInstallerFailureFlow：调用 macro.indexOf、assert.ok；更新 previousIndex。 */

  const requiredSteps = [
    'ClearErrors',
    'ExecWait \'"$PLUGINSDIR\\${FILE_NAME}" /S\' $ChildExitCode',
    '${If} ${Errors}',
    'StrCpy $ChildExitCode 1',
    '${EndIf}',
    '${If} $ChildExitCode != 0',
    'SetErrorLevel $ChildExitCode',
    'MessageBox MB_ICONSTOP|MB_OK "$(INSTALL_FAILED)',
    'Quit',
    '${EndIf}',
  ];
  let previousIndex = -1;
  for (const step of requiredSteps) {
    const index = macro.indexOf(step, previousIndex + 1);
    assert.ok(index > previousIndex, `RunInstaller must execute ${step} after the preceding failure-handling step`);
    previousIndex = index;
  }
}

test('all three V1.0.1 input installers match their published SHA256 values', () => {
/* EN: Verify all three V1.0.1 input installers match their published SHA256 values: call assert.equal, fs.existsSync, sha256.
   ZH: 验证回归场景 all three V1.0.1 input installers match their published SHA256 values：调用 assert.equal、fs.existsSync、sha256。 */

  for (const artifact of artifacts) {
    assert.equal(fs.existsSync(artifact.file), true, `${artifact.label} installer is missing`);
    assert.equal(sha256(artifact.file), checksumFor(artifact.sums, artifact.name), `${artifact.label} installer checksum mismatch`);
  }
});

test('suite installer keeps the bilingual component flow and failure boundary', () => {
/* EN: Verify suite installer keeps the bilingual component flow and failure boundary: call fs.readFileSync, macroBlock, assertRunInstallerFailureFlow.
   ZH: 验证回归场景 suite installer keeps the bilingual component flow and failure boundary：调用 fs.readFileSync、macroBlock、assertRunInstallerFailureFlow。 */

  const script = fs.readFileSync(suiteScript, 'utf8');
  const components = [
    {
      id: 'SecMain',
      file: 'KnotJot-Setup-1.0.1.exe',
      label: 'KnotJot V1.0.1 (required)',
      displayName: 'KnotJot V1.0.1',
      descriptionId: 'DESC_SecMain',
      description: 'KnotJot 主程序，必须安装。',
      required: true,
    },
    {
      id: 'SecUiEditor',
      file: 'KnotJot-UI-Editor-Setup-1.0.1.exe',
      label: 'KnotJot UI Editor V1.0.1',
      displayName: 'KnotJot UI Editor V1.0.1',
      descriptionId: 'DESC_SecUiEditor',
      description: '设计并同步 KnotJot 的整套 UI 外观。',
      required: false,
    },
    {
      id: 'SecTextEditor',
      file: 'KnotJot-Text-Style-Editor-Setup-1.0.1.exe',
      label: 'KnotJot Text Style Editor V1.0.1',
      displayName: 'KnotJot Text Style Editor V1.0.1',
      descriptionId: 'DESC_SecTextEditor',
      description: '设计并同步 KnotJot 的文本框样式。',
      required: false,
    },
  ];

  const runInstaller = macroBlock(script, 'RunInstaller');
  assertRunInstallerFailureFlow(runInstaller);

  assert.match(script, /!insertmacro\s+MUI_LANGUAGE\s+"SimpChinese"/);
  assert.match(script, /!define\s+PRODUCT_NAME\s+"KnotJot Suite 1\.0\.1"/);
  assert.match(script, /MessageBox\s+MB_ICONSTOP\|MB_OK\s+"\$\(INSTALL_FAILED\)/);
  assert.match(script, /OutFile\s+"\.\.\\\.\.\\installers\\KnotJot-Suite-Setup-1\.0\.1\.exe"/);
  assert.match(script, /!insertmacro\s+MUI_LANGUAGE\s+"English"/);
  assert.match(script, /!insertmacro\s+MUI_LANGDLL_DISPLAY/);
  assert.match(script, /RequestExecutionLevel\s+user/);
  assert.match(script, /SetCompress\s+off/);

  for (const component of components) {
    const section = sectionBlock(script, component.label, component.id);
    assert.match(section, new RegExp(`File\\s+/oname=${escapeRegExp(component.file)}\\s+"[^"\\r\\n]+"`));
    assert.match(section, new RegExp(`!insertmacro\\s+RunInstaller\\s+"${escapeRegExp(component.file)}"\\s+"${escapeRegExp(component.displayName)}"`));
    assert.match(script, new RegExp(`LangString\\s+${escapeRegExp(component.descriptionId)}\\s+\\$\\{LANG_SIMPCHINESE\\}\\s+"${escapeRegExp(component.description)}"`));
    if (component.required) assert.match(section, /SectionIn\s+RO/);
    else assert.doesNotMatch(section, /SectionIn\s+RO|Section\s+\/o/);
  }

  const sectionPositions = components.map(({ id }) => /* EN: Derive the next value for components.map: call script.indexOf; return script.indexOf(` ${id}`). ZH: 计算 components.map 的下一项结果：调用 script.indexOf；返回 script.indexOf(` ${id}`)。 */ script.indexOf(` ${id}`));
  assert.ok(sectionPositions[0] < sectionPositions[1]);
  assert.ok(sectionPositions[1] < sectionPositions[2]);
  const callPositions = components.map(({ file, displayName }) => /* EN: Derive the next value for components.map: call script.indexOf; return script.indexOf(`!insertmacro RunInstaller "${file}" "${displayName}"`). ZH: 计算 components.map 的下一项结果：调用 script.indexOf；返回 script.indexOf(`!insertmacro RunInstaller "${file}" "${displayName}"`)。 */ script.indexOf(`!insertmacro RunInstaller "${file}" "${displayName}"`));
  assert.ok(callPositions.every((position) => /* EN: Test an item for callPositions.every: return position !== -1. ZH: 判断 callPositions.every 的元素条件：返回 position !== -1。 */ position !== -1));
  assert.ok(callPositions[0] < callPositions[1]);
  assert.ok(callPositions[1] < callPositions[2]);
});
