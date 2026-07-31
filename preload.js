const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  isDesktop: true,
  saveBegin: (filePath, defaultName) => ipcRenderer.invoke('save-begin', { filePath, defaultName }),
  saveChunk: (id, chunk) => ipcRenderer.invoke('save-chunk', { id, chunk }),
  saveEnd: (id, abort = false) => ipcRenderer.invoke('save-end', { id, abort }),
  open: () => ipcRenderer.invoke('open'),
  openPath: (filePath) => ipcRenderer.invoke('open-path', filePath),
  exportSave: (base64, defaultName, ext) => ipcRenderer.invoke('export-save', { base64, defaultName, ext }),
  openExternal: (url) => ipcRenderer.invoke('open-external', url),
  confirmDiscard: (name) => ipcRenderer.invoke('confirm-discard', name),
  getInitialFile: () => ipcRenderer.invoke('get-initial-file'),
  loadSkins: () => ipcRenderer.invoke('skins-load'),
  saveSkins: (json) => ipcRenderer.invoke('skins-save', json),
  skinsLocation: () => ipcRenderer.invoke('skins-location'),
  onSkinsChanged: (cb) => ipcRenderer.on('ui-skins-changed', () => cb()),
  loadAppearance: () => ipcRenderer.invoke('appearance-load'),
  saveAppearance: (json) => ipcRenderer.invoke('appearance-save', json),
  loadTextStyles: () => ipcRenderer.invoke('text-styles-load'),
  saveTextStyles: (library) => ipcRenderer.invoke('text-styles-save', library),
  textStylesLocation: () => ipcRenderer.invoke('text-styles-location'),
  onTextStylesChanged: (cb) => ipcRenderer.on('text-styles-changed', () => cb()),
  loadAIConf: () => ipcRenderer.invoke('aiconf-load'),
  saveAIConf: (json) => ipcRenderer.invoke('aiconf-save', json),
  aiProbe: (payload) => ipcRenderer.invoke('ai-probe', payload),
  aiModels: (payload) => ipcRenderer.invoke('ai-models', payload),
  aiAbortUtility: (requestId) => ipcRenderer.send('ai-utility-abort', { requestId }),
  aiStream: {
    start: (payload) => ipcRenderer.send('ai-chat-start', payload),
    abort: (reqId) => ipcRenderer.send('ai-chat-abort', { reqId }),
    onRaw: (cb) => ipcRenderer.on('ai-chat-raw', (e, d) => cb(d)),
    onEnd: (cb) => ipcRenderer.on('ai-chat-end', (e, d) => cb(d)),
    onError: (cb) => ipcRenderer.on('ai-chat-error', (e, d) => cb(d))
  },
  onOpenFile: (cb) => ipcRenderer.on('open-file-data', (e, data) => cb(data)),
  onCloseRequest: (cb) => ipcRenderer.on('app-close-request', () => cb()),
  closeConfirmed: (proceed) => ipcRenderer.send('close-confirmed', proceed)
});
