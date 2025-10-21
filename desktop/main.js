const { app, BrowserWindow, ipcMain, dialog, Tray, Menu } = require('electron');
const path = require('path');
const { spawn } = require('child_process');
const log = require('electron-log');
const Store = require('electron-store');

const store = new Store();
let mainWindow = null;
let backendProcess = null;
let tray = null;

// Configure logging
log.transports.file.level = 'info';
log.info('FirstMake Desktop starting...');

// Backend management
function startBackend() {
  return new Promise((resolve, reject) => {
    const isDev = process.env.NODE_ENV === 'development';
    
    let backendPath;
    let backendArgs = [];
    
    if (isDev) {
      // Development: use dotnet run
      backendPath = 'dotnet';
      backendArgs = ['run', '--project', path.join(__dirname, '../src/Api/Api.csproj'), '--no-build'];
    } else {
      // Production: use published backend
      const exeName = process.platform === 'win32' ? 'Api.exe' : 'Api';
      backendPath = path.join(process.resourcesPath, 'backend', exeName);
    }

    log.info(`Starting backend: ${backendPath} ${backendArgs.join(' ')}`);

    backendProcess = spawn(backendPath, backendArgs, {
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: isDev ? 'Development' : 'Production',
        ASPNETCORE_URLS: 'http://localhost:5085'
      }
    });

    backendProcess.stdout.on('data', (data) => {
      log.info(`[Backend] ${data.toString()}`);
      if (data.toString().includes('Now listening on')) {
        log.info('Backend started successfully');
        resolve();
      }
    });

    backendProcess.stderr.on('data', (data) => {
      log.error(`[Backend Error] ${data.toString()}`);
    });

    backendProcess.on('error', (error) => {
      log.error('Failed to start backend:', error);
      reject(error);
    });

    backendProcess.on('close', (code) => {
      log.info(`Backend process exited with code ${code}`);
      backendProcess = null;
    });

    // Timeout fallback
    setTimeout(() => {
      if (backendProcess && backendProcess.pid) {
        log.info('Backend startup timeout - assuming success');
        resolve();
      } else {
        reject(new Error('Backend failed to start'));
      }
    }, 10000);
  });
}

function stopBackend() {
  if (backendProcess) {
    log.info('Stopping backend...');
    backendProcess.kill('SIGTERM');
    backendProcess = null;
  }
}

// Window management
function createWindow() {
  const windowState = store.get('windowState', {
    width: 1400,
    height: 900,
    x: undefined,
    y: undefined
  });

  mainWindow = new BrowserWindow({
    width: windowState.width,
    height: windowState.height,
    x: windowState.x,
    y: windowState.y,
    minWidth: 1024,
    minHeight: 768,
    title: 'FirstMake v2.0',
    icon: path.join(__dirname, 'assets', 'icon.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  // Save window state on close
  mainWindow.on('close', () => {
    const bounds = mainWindow.getBounds();
    store.set('windowState', bounds);
  });

  // Load the app
  const isDev = process.env.NODE_ENV === 'development';
  const startUrl = isDev
    ? 'http://localhost:5173'
    : `file://${path.join(__dirname, '../src/UI/dist/index.html')}`;

  log.info(`Loading URL: ${startUrl}`);
  mainWindow.loadURL(startUrl);

  // Open DevTools in development
  if (isDev) {
    mainWindow.webContents.openDevTools();
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  // Create system tray
  createTray();
}

function createTray() {
  const iconPath = path.join(__dirname, 'assets', 'icon.png');
  tray = new Tray(iconPath);

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Покажи FirstMake',
      click: () => {
        if (mainWindow) {
          mainWindow.show();
          mainWindow.focus();
        }
      }
    },
    { type: 'separator' },
    {
      label: 'Изход',
      click: () => {
        app.quit();
      }
    }
  ]);

  tray.setToolTip('FirstMake v2.0');
  tray.setContextMenu(contextMenu);

  tray.on('click', () => {
    if (mainWindow) {
      if (mainWindow.isVisible()) {
        mainWindow.hide();
      } else {
        mainWindow.show();
        mainWindow.focus();
      }
    }
  });
}

// IPC Handlers
ipcMain.handle('select-files', async (event, options) => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile', 'multiSelections'],
    filters: options.filters || []
  });
  return result.filePaths;
});

ipcMain.handle('select-folder', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openDirectory']
  });
  return result.filePaths[0];
});

ipcMain.handle('save-file', async (event, options) => {
  const result = await dialog.showSaveDialog(mainWindow, options);
  return result.filePath;
});

ipcMain.handle('get-app-version', () => {
  return app.getVersion();
});

ipcMain.handle('get-backend-status', () => {
  return backendProcess !== null;
});

// App lifecycle
app.whenReady().then(async () => {
  try {
    log.info('App ready - starting backend...');
    await startBackend();
    log.info('Backend started - creating window...');
    createWindow();
  } catch (error) {
    log.error('Failed to initialize app:', error);
    dialog.showErrorBox(
      'Грешка при стартиране',
      'Неуспешно стартиране на backend сървъра. Моля, проверете логовете.'
    );
    app.quit();
  }
});

app.on('window-all-closed', () => {
  // Keep app running in system tray (except on macOS)
  if (process.platform !== 'darwin') {
    // Don't quit immediately - keep backend running
  }
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow();
  }
});

app.on('before-quit', () => {
  log.info('App quitting - stopping backend...');
  stopBackend();
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
  log.error('Uncaught exception:', error);
});

process.on('unhandledRejection', (reason, promise) => {
  log.error('Unhandled rejection at:', promise, 'reason:', reason);
});
