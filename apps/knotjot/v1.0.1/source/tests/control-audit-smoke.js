const { app, BrowserWindow } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const out = path.resolve(__dirname, '../../preview/testing/controls');
fs.mkdirSync(out, { recursive: true });
process.env.KNOTJOT_INTEGRATION_TEST = '1';
app.commandLine.appendSwitch('disable-gpu');
app.setPath('userData', path.join(out, 'profile'));
require('../main');

// EN: Audit real menu/control wiring and representative editor actions against a retained synthetic document.
// ZH: 使用保留的合成导图核查实际菜单控件绑定和代表性编辑操作。
app.whenReady().then(async () => {
  const timer = setTimeout(() => app.exit(1), 60000);
  try {
    const win = new BrowserWindow({ show: false, width: 1400, height: 960, webPreferences: { offscreen: true, backgroundThrottling: false, contextIsolation: true, nodeIntegration: false, preload: path.join(__dirname, '../preload.js') } });
    await win.loadFile(path.join(__dirname, '../index.html'));
    const result = await win.webContents.executeJavaScript(`(async () => {
      const checks={},menus={};
      dirty=false;await doNew('brace');hideStart();
      const root=roots[0]||addRootAt(3000,1800);if(editing)commitEdit(editing);TB[root].text='功能核查测试';update();selectTb(root);initHistory(true);
      const initial=scale;document.getElementById('zoomIn').click();checks.zoomIn=scale>initial;
      document.getElementById('zoomOut').click();checks.zoomOut=scale<initial*1.3;
      document.getElementById('zoomReset').click();checks.zoomReset=scale===1;
      document.getElementById('readBtn').click();checks.readOnly=readonly;
      document.getElementById('readBtn').click();checks.readOnlyExit=!readonly;
      openSettings();checks.settingsOpen=getComputedStyle(document.getElementById('settingsMask')).display!=='none';
      document.getElementById('settingsClose').click();checks.settingsClose=getComputedStyle(document.getElementById('settingsMask')).display==='none';
      openNote(root);document.getElementById('noteArea').value='保留的备注测试';document.getElementById('noteSave').click();checks.noteSave=TB[root].note==='保留的备注测试';
      toggleMarker(root,'p1');toggleMarker(root,'p2');checks.markerExclusive=TB[root].markers.includes('p2')&&!TB[root].markers.includes('p1');
      setLink(root);document.getElementById('linkInput').value='https://example.com';document.getElementById('linkDone').click();checks.linkSave=TB[root].link==='https://example.com/';
      if(TB[root].link==='https://example.com') checks.linkSave=true;
      openTaskPanel(root,100,100);document.getElementById('tpWho').value='测试负责人';document.getElementById('tpWho').dispatchEvent(new Event('input',{bubbles:true}));document.getElementById('tpStart').value='2026-09-05';document.getElementById('tpStart').dispatchEvent(new Event('change',{bubbles:true}));document.getElementById('tpEnd').value='2026-09-07';document.getElementById('tpEnd').dispatchEvent(new Event('change',{bubbles:true}));document.getElementById('tpDone').click();checks.taskSave=TB[root].task?.who==='测试负责人'&&TB[root].task?.start==='2026-09-05';
      toggleOutline(true);checks.outlineOpen=outlineOpen;document.getElementById('outlineClose').click();checks.outlineClose=!outlineOpen;
      toggleGantt(true);checks.ganttOpen=ganttOpen;toggleGantt(false);checks.ganttClose=!ganttOpen;
      selectTb(root);for(const mode of ['brace','spider']){docType=mode;applyDocType();menus[mode]={};for(const key of ['edit','insert','view','window','help']) menus[mode][key]=menuItems(key).filter(x=>!x.sep).map(x=>({label:x.label,callable:typeof x.fn==='function',disabled:!!x.dis}));openMenu(root,60,120);menus[mode].context=ctx.innerText;closeMenu();}
      docType='brace';applyDocType();checks.menuFunctions=Object.values(menus).every(group=>Object.entries(group).filter(([k])=>k!=='context').every(([,a])=>a.every(x=>x.callable)));
      checks.summaryRemoved=Object.values(menus).every(group=>group.insert.every(x=>!/概要|摘要/.test(x.label)));
      const count=sheets.length;addSheet('brace');checks.addSheet=sheets.length===count+1;renameSheet(curSheet,'保留的第二画布');checks.renameSheet=sheets[curSheet].name==='保留的第二画布';switchSheet(0);checks.switchSheet=curSheet===0&&TB[root].note==='保留的备注测试';
      const controls=[...document.querySelectorAll('button,input,select,[data-act],[data-tbk],.mb-item')].map(el=>({id:el.id,label:(el.textContent||'').trim().slice(0,180),title:el.title||'',action:el.dataset.act||el.dataset.tbk||'',type:el.type||el.tagName}));
      return {checks,menus,controls,document:serialize()};
    })()`);
    fs.writeFileSync(path.join(out, 'control-audit.knotjot'), result.document);
    delete result.document;
    fs.writeFileSync(path.join(out, 'control-audit-result.json'), JSON.stringify(result, null, 2));
    fs.writeFileSync(path.join(out, 'control-audit.png'), (await win.webContents.capturePage()).toPNG());
    const failed=Object.entries(result.checks).filter(([,pass])=>!pass);
    console.log('CONTROL_AUDIT',JSON.stringify({checks:result.checks,controls:result.controls.length,failed}));
    clearTimeout(timer);app.exit(failed.length?1:0);
  } catch(err) { console.error(err.stack);clearTimeout(timer);app.exit(1); }
});
