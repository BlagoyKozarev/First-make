# FirstMake Desktop App

Electron wrapper for the FirstMake multi-file КСС processing application.

## Features

- ✅ Auto-start .NET backend on app launch
- ✅ Native file dialogs
- ✅ System tray integration
- ✅ Window state persistence
- ✅ Logging with electron-log
- ✅ Production packaging

## Development

```bash
# Install dependencies
npm install

# Start in development mode (requires backend running separately)
NODE_ENV=development npm start

# Build for production
npm run build

# Build for specific platform
npm run build:win
npm run build:linux
```

## Production Build

1. Build backend:
```bash
cd ../src/Api
dotnet publish -c Release -r win-x64 --self-contained
```

2. Build frontend:
```bash
cd ../src/UI
npm run build
```

3. Package desktop app:
```bash
cd ../desktop
npm run build
```

## Directory Structure

```
desktop/
├── main.js          # Electron main process
├── preload.js       # Preload script (IPC bridge)
├── package.json     # Dependencies & build config
├── assets/          # Icons and resources
└── dist/            # Build output (generated)
```

## Backend Integration

The desktop app automatically:
1. Starts the .NET API backend on port 5085
2. Monitors backend health
3. Stops backend on app exit
4. Logs all backend output to electron-log

## System Tray

- Click icon: Show/hide window
- Right-click: Context menu
- Tray stays active when window is closed
