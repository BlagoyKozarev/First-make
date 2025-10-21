const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('electron', {
  // File dialogs
  selectFiles: (options) => ipcRenderer.invoke('select-files', options),
  selectFolder: () => ipcRenderer.invoke('select-folder'),
  saveFile: (options) => ipcRenderer.invoke('save-file', options),
  
  // App info
  getAppVersion: () => ipcRenderer.invoke('get-app-version'),
  getBackendStatus: () => ipcRenderer.invoke('get-backend-status'),
  
  // Platform info
  platform: process.platform,
  isElectron: true
});

// Log that preload script ran
console.log('FirstMake preload script loaded');
