const { contextBridge, ipcRenderer, webUtils } = require('electron');

contextBridge.exposeInMainWorld('api', {
  isDesktop: true,
  // EN: Resolve only a File's native path for drop/save continuity; synthetic or invalid inputs have no filesystem destination.
  // ZH: 仅解析 File 的原生路径以保持拖入后保存位置；合成或无效输入不具有文件系统目标。
  getPathForFile: file => {
    try { return webUtils.getPathForFile(file); }
    catch { return ''; }
  },
  // EN: Expose bounded app-owned recovery operations, never generic filesystem access.
  // ZH: 暴露有界应用恢复操作，不提供通用文件系统访问。
  recoveryPut: entry => ipcRenderer.invoke('recovery-put',entry),
  recoveryList: () => ipcRenderer.invoke('recovery-list'),
  recoveryRemove: id => ipcRenderer.invoke('recovery-remove',id),
  recoverySaved: document => ipcRenderer.invoke('recovery-saved',document),
  recoveryConfigure: preferences => ipcRenderer.invoke('recovery-configure',preferences),
  recoveryPrompt: name => ipcRenderer.invoke('recovery-prompt',name),
  saveBegin: (filePath, defaultName) => /* EN: saveBegin in module: call ipcRenderer.invoke; return ipcRenderer.invoke('save-begin', { filePath, defaultName }). ZH: module 中的 saveBegin：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('save-begin', { filePath, defaultName })。 */ ipcRenderer.invoke('save-begin', { filePath, defaultName }),
  saveChunk: (id, chunk) => /* EN: saveChunk in module: call ipcRenderer.invoke; return ipcRenderer.invoke('save-chunk', { id, chunk }). ZH: module 中的 saveChunk：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('save-chunk', { id, chunk })。 */ ipcRenderer.invoke('save-chunk', { id, chunk }),
  saveEnd: (id, abort = false) => /* EN: saveEnd in module: call ipcRenderer.invoke; return ipcRenderer.invoke('save-end', { id, abort }). ZH: module 中的 saveEnd：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('save-end', { id, abort })。 */ ipcRenderer.invoke('save-end', { id, abort }),
  open: () => /* EN: open in module: call ipcRenderer.invoke; return ipcRenderer.invoke('open'). ZH: module 中的 open：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('open')。 */ ipcRenderer.invoke('open'),
  openPath: (filePath) => /* EN: Open a selected mind-map file and load its document state into the editor. ZH: 打开指定导图文件，并将其文档状态载入编辑器。 */ ipcRenderer.invoke('open-path', filePath),
  exportSave: (base64, defaultName, ext) => /* EN: Generate or write the requested document export while honoring the selected format. ZH: 按所选格式生成或写入文档导出结果。 */ ipcRenderer.invoke('export-save', { base64, defaultName, ext }),
  openExternal: (url) => /* EN: openExternal in module: call ipcRenderer.invoke; return ipcRenderer.invoke('open-external', url). ZH: module 中的 openExternal：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('open-external', url)。 */ ipcRenderer.invoke('open-external', url),
  confirmDiscard: (name) => /* EN: Resolve unsaved edits before allowing a document-changing action to continue. ZH: 处理未保存修改后，才允许改变当前文档的操作继续。 */ ipcRenderer.invoke('confirm-discard', name),
  getInitialFile: () => /* EN: getInitialFile in module: call ipcRenderer.invoke; return ipcRenderer.invoke('get-initial-file'). ZH: module 中的 getInitialFile：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('get-initial-file')。 */ ipcRenderer.invoke('get-initial-file'),
  loadSkins: () => /* EN: loadSkins in module: call ipcRenderer.invoke; return ipcRenderer.invoke('skins-load'). ZH: module 中的 loadSkins：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('skins-load')。 */ ipcRenderer.invoke('skins-load'),
  saveSkins: (json) => /* EN: saveSkins in module: call ipcRenderer.invoke; return ipcRenderer.invoke('skins-save', json). ZH: module 中的 saveSkins：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('skins-save', json)。 */ ipcRenderer.invoke('skins-save', json),
  skinsLocation: () => /* EN: skinsLocation in module: call ipcRenderer.invoke; return ipcRenderer.invoke('skins-location'). ZH: module 中的 skinsLocation：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('skins-location')。 */ ipcRenderer.invoke('skins-location'),
  onSkinsChanged: (cb) => /* EN: onSkinsChanged in module: call ipcRenderer.on. ZH: module 中的 onSkinsChanged：调用 ipcRenderer.on。 */ ipcRenderer.on('ui-skins-changed', () => /* EN: Handle ui-skins-changed on ipcRenderer.on: call cb; return cb(). ZH: 处理 ipcRenderer.on 的 ui-skins-changed 事件或通道：调用 cb；返回 cb()。 */ cb()),
  loadAppearance: () => /* EN: loadAppearance in module: call ipcRenderer.invoke; return ipcRenderer.invoke('appearance-load'). ZH: module 中的 loadAppearance：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('appearance-load')。 */ ipcRenderer.invoke('appearance-load'),
  saveAppearance: (json) => /* EN: saveAppearance in module: call ipcRenderer.invoke; return ipcRenderer.invoke('appearance-save', json). ZH: module 中的 saveAppearance：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('appearance-save', json)。 */ ipcRenderer.invoke('appearance-save', json),
  loadTextStyles: () => /* EN: loadTextStyles in module: call ipcRenderer.invoke; return ipcRenderer.invoke('text-styles-load'). ZH: module 中的 loadTextStyles：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('text-styles-load')。 */ ipcRenderer.invoke('text-styles-load'),
  saveTextStyles: (library) => /* EN: saveTextStyles in module: call ipcRenderer.invoke; return ipcRenderer.invoke('text-styles-save', library). ZH: module 中的 saveTextStyles：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('text-styles-save', library)。 */ ipcRenderer.invoke('text-styles-save', library),
  textStylesLocation: () => /* EN: textStylesLocation in module: call ipcRenderer.invoke; return ipcRenderer.invoke('text-styles-location'). ZH: module 中的 textStylesLocation：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('text-styles-location')。 */ ipcRenderer.invoke('text-styles-location'),
  onTextStylesChanged: (cb) => /* EN: onTextStylesChanged in module: call ipcRenderer.on. ZH: module 中的 onTextStylesChanged：调用 ipcRenderer.on。 */ ipcRenderer.on('text-styles-changed', () => /* EN: Handle text-styles-changed on ipcRenderer.on: call cb; return cb(). ZH: 处理 ipcRenderer.on 的 text-styles-changed 事件或通道：调用 cb；返回 cb()。 */ cb()),
  loadAIConf: () => /* EN: Read stored AI connection settings and use defaults when no valid record is available. ZH: 读取保存的 AI 连接设置，在没有有效记录时使用默认值。 */ ipcRenderer.invoke('aiconf-load'),
  saveAIConf: (json) => /* EN: Persist the AI connection settings used by subsequent requests. ZH: 保存供后续请求使用的 AI 连接设置。 */ ipcRenderer.invoke('aiconf-save', json),
  aiProbe: (payload) => /* EN: aiProbe in module: call ipcRenderer.invoke; return ipcRenderer.invoke('ai-probe', payload). ZH: module 中的 aiProbe：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('ai-probe', payload)。 */ ipcRenderer.invoke('ai-probe', payload),
  aiModels: (payload) => /* EN: aiModels in module: call ipcRenderer.invoke; return ipcRenderer.invoke('ai-models', payload). ZH: module 中的 aiModels：调用 ipcRenderer.invoke；返回 ipcRenderer.invoke('ai-models', payload)。 */ ipcRenderer.invoke('ai-models', payload),
  aiAbortUtility: (requestId) => /* EN: Cancel the active AI request and release its request controller. ZH: 取消活动 AI 请求，并释放其请求控制器。 */ ipcRenderer.send('ai-utility-abort', { requestId }),
  aiStream: {
    start: (payload) => /* EN: start in module: call ipcRenderer.send; return ipcRenderer.send('ai-chat-start', payload). ZH: module 中的 start：调用 ipcRenderer.send；返回 ipcRenderer.send('ai-chat-start', payload)。 */ ipcRenderer.send('ai-chat-start', payload),
    abort: (reqId) => /* EN: abort in module: call ipcRenderer.send; return ipcRenderer.send('ai-chat-abort', { reqId }). ZH: module 中的 abort：调用 ipcRenderer.send；返回 ipcRenderer.send('ai-chat-abort', { reqId })。 */ ipcRenderer.send('ai-chat-abort', { reqId }),
    onRaw: (cb) => /* EN: onRaw in module: call ipcRenderer.on. ZH: module 中的 onRaw：调用 ipcRenderer.on。 */ ipcRenderer.on('ai-chat-raw', (e, d) => /* EN: Handle ai-chat-raw on ipcRenderer.on: call cb; return cb(d). ZH: 处理 ipcRenderer.on 的 ai-chat-raw 事件或通道：调用 cb；返回 cb(d)。 */ cb(d)),
    onEnd: (cb) => /* EN: onEnd in module: call ipcRenderer.on. ZH: module 中的 onEnd：调用 ipcRenderer.on。 */ ipcRenderer.on('ai-chat-end', (e, d) => /* EN: Handle ai-chat-end on ipcRenderer.on: call cb; return cb(d). ZH: 处理 ipcRenderer.on 的 ai-chat-end 事件或通道：调用 cb；返回 cb(d)。 */ cb(d)),
    onError: (cb) => /* EN: onError in module: call ipcRenderer.on. ZH: module 中的 onError：调用 ipcRenderer.on。 */ ipcRenderer.on('ai-chat-error', (e, d) => /* EN: Handle ai-chat-error on ipcRenderer.on: call cb; return cb(d). ZH: 处理 ipcRenderer.on 的 ai-chat-error 事件或通道：调用 cb；返回 cb(d)。 */ cb(d))
  },
  onOpenFile: (cb) => /* EN: onOpenFile in module: call ipcRenderer.on. ZH: module 中的 onOpenFile：调用 ipcRenderer.on。 */ ipcRenderer.on('open-file-data', (e, data) => /* EN: Handle open-file-data on ipcRenderer.on: call cb; return cb(data). ZH: 处理 ipcRenderer.on 的 open-file-data 事件或通道：调用 cb；返回 cb(data)。 */ cb(data)),
  onCloseRequest: (cb) => /* EN: onCloseRequest in module: call ipcRenderer.on. ZH: module 中的 onCloseRequest：调用 ipcRenderer.on。 */ ipcRenderer.on('app-close-request', () => /* EN: Handle app-close-request on ipcRenderer.on: call cb; return cb(). ZH: 处理 ipcRenderer.on 的 app-close-request 事件或通道：调用 cb；返回 cb()。 */ cb()),
  closeConfirmed: (proceed) => /* EN: closeConfirmed in module: call ipcRenderer.send; return ipcRenderer.send('close-confirmed', proceed). ZH: module 中的 closeConfirmed：调用 ipcRenderer.send；返回 ipcRenderer.send('close-confirmed', proceed)。 */ ipcRenderer.send('close-confirmed', proceed)
});
