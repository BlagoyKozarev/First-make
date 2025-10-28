# FirstMake Agent

Project: First-Make (repository: GitRaicommerce/First-make)

**Local-first BoQ Processing and LP Optimization for Construction Offers**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6)](https://www.typescriptlang.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## 🎯 Overview

FirstMake Agent е **desktop приложение** за обработка на Количествено-стойностни сметки (КСС) и оптимизация на строителни оферти. Системата позволява автоматизирано извличане, съпоставяне и оптимизация на данни от разнородни източници (XLSX, DOCX, PDF, сканирани документи).

### Основни функционалности

- 🖥️ **Desktop App** - Electron wrapper с auto-start на backend
- 📄 **Интелигентно парсиране** - Поддръжка на XLSX, DOCX, PDF + OCR за сканирани документи
- 🔍 **Fuzzy matching** - Автоматично съпоставяне на позиции с ценови каталог
- 📊 **LP оптимизация** - Google OR-Tools за оптимално ценообразуване
- 📈 **Метрики и телеметрия** - Real-time dashboard за проследяване на операции
- 💾 **Локално съхранение** - SQLite база данни, без cloud dependencies
- 🎨 **Модерен UI** - React с Tailwind CSS и shadcn/ui компоненти
- 📑 **Excel експорт** - Генериране на КСС файлове с формули и форматиране
- 🔔 **System Tray** - Фоново изпълнение с tray icon

## 🧭 Последни промени (резюме на извършеното)

- Поправки в парсера за по-надеждно числено парсване (EU/US десетична/хилядна сепарация).
- Добавена логика за дедупликация при зареждане на ценови бази (PriceBaseLoader).
- Исправен frontend lint/fast-refresh проблем (компоненти/споделен модул рефакторинг).
- Добавен GitHub Actions workflow за CI (build + тестове за backend и frontend) — `.github/workflows/ci.yml`.
- Стартирани и преминали unit тестове локално (dotnet tests green: 31 passed, 1 skipped в последния run).
- Frontend production build успешно (Vite) — артефакти в `src/UI/dist` при локално пускане.
- Подготвени и интегрирани страници за Upload / Match / Export в `src/UI/src/pages` (готови за употреба).
- Отворен и мърджнат PR с фиксовете (branch/PR беше синхронизиран с `main`) и таг `v1.0.0` създаден.
- Бързо security sweep за комитнати секрети — намерени само placeholder-и в deployment/docs; потребителят оттегли краткотраен PAT.

> Всички тези промени са налични в текущия `main` клон. Ако желаете, мога да помогна с push/release/CI публично публикуване (трябва да дадете разрешение `allow push`).

## 🔍 Какво остава да се направи (high-level Roadmap)

Следните задачи са систематизирани и сортирани по приоритет, за да завършим приложението и да осигурим публичен тест линк:

1) Критично (за стабилно публично тестване)
  - Deep secret scan през git history (truffleHog/detect-secrets) и поправка/rotate на потенциални секрети.
  - Конфигуриране на CI publish job: build + docker image + push към GHCR или друг registry.
  - Подготовка на production Docker images и docker-compose.prod.yml проверка (env vars, volumes, healthchecks).

2) Висок приоритет (UX / developer experience)
  - Финално полиране на UI страниците (износ на повторяеми компоненти, малки тестове, accessibility).
  - Address TypeScript / ESLint warning (pin или upgrade @typescript-eslint / TS) за по-чиста CI линтирана среда.
  - Добавяне на basic frontend tests (vite + vitest / react-testing-library) за UploadZone и Match flows.

3) Среден приоритет (release readiness)
  - Написване на integration smoke tests, pipeline за release и GitHub Release (publish artifacts и release notes).
  - Подготовка на devcontainer/Codespaces файл за по-лесен onboarding.

4) По желание (optimize / audit)
  - Performance tuning и coverage за критични LP операции.
  - Automated secret scanning on PR и pre-commit hooks.

За всяка задача мога да подготвя конкретен PR с промени и/или GitHub Actions jobs — кажете коя от стъпките да приоритизирам.

## 📎 Как да дам тест линк (опции)

Има няколко бързи варианта да споделите работеща версия за тест:

- Вариант A — Web UI (само frontend): publish `src/UI/dist` към GitHub Pages / Netlify / Vercel. Подходящо, ако искате само визуален тест на UI (без backend). Изисква `npm run build` и upload на `dist/`.

- Вариант B — Пълно приложение чрез Docker: build на backend + ai-gateway + frontend в docker images и пускане с `docker-compose.prod.yml` на тест VM. След това споделяте публичен адрес (или използвате ngrok/ssh tunnel). Подходящо за end-to-end тест.

- Вариант C — GitHub Actions: добавям workflow, който при tag `v1.0.0` билдва images и публикува на GHCR, след което стартира тест deployment на кратък-lived host (e.g., DigitalOcean App Platform / ephemeral host). Това изисква registry credentials (GHCR token) в GitHub Secrets.

Кратки команди за локално тестване (end-to-end):

```bash
# Start API
cd src/Api
dotnet run --urls "http://localhost:5085"

# Start AI Gateway (optional)
cd ../AiGateway
dotnet run --urls "http://localhost:5001"

# Start UI (dev)
cd ../../src/UI
npm install
npm run dev

# Open http://localhost:5173 (или адрес от Vite output)
```

За бърз, временен публичен линк до локално работещ backend/UI може да използвате `ngrok` или `cloudflared` (аз мога да помогна да го завъртим и да споделя публичния URL, ако разрешите). 

---

## 🏗️ Архитектура

```
┌─────────────┐      ┌──────────────┐      ┌───────────────┐
│   Browser   │─────▶│  React UI    │─────▶│   API (5000)  │
│  (User)     │      │  (Vite 5174) │      │  (.NET 8 Web) │
└─────────────┘      └──────────────┘      └───────┬───────┘
                                                    │
                     ┌──────────────────────────────┴────────────┐
                     │                                           │
              ┌──────▼──────┐                           ┌───────▼────────┐
              │ AiGateway   │                           │ Core.Engine    │
              │ (5001)      │                           │ (Class Lib)    │
              │             │                           │                │
              │ • PDF Parse │                           │ • LP Optimizer │
              │ • OCR       │                           │ • FuzzyMatcher │
              │ • LLM       │                           │ • Normalizers  │
              └─────────────┘                           └────────────────┘
                     │
              ┌──────▼──────────────┐
              │  External Services  │
              │                     │
              │ • BG GPT API        │
              │ • Tesseract OCR     │
              └─────────────────────┘
```

### Компоненти

#### Frontend (React + TypeScript)
- **Stack**: Vite, React 18, TypeScript, Tailwind CSS
- **Routing**: React Router v6
- **Components**: shadcn/ui (Card, Badge, Button)
- **State**: SessionStorage за workflow данни
- **Pages**: Upload, Review, Optimize, Export, Metrics

#### Backend API (.NET 8 Minimal API)
- **Endpoints**: 10 REST JSON endpoints
- **Database**: SQLite с EF Core 8
- **Services**: ExcelExportService (EPPlus), ObservationService
- **Middleware**: Error handling, request limits, timeouts
- **Validation**: Comprehensive input validation

#### AI Gateway (.NET 8 Minimal API)
- **Parsers**: iText7 (PDF), OpenXML (XLSX/DOCX)
- **OCR**: Tesseract wrapper (Bulgarian + English)
- **LLM**: BG GPT integration (api.raicommerce.net)

#### Core.Engine (.NET 8 Class Library)
- **LP Solver**: Google OR-Tools 9.11 (GLOP)
- **Fuzzy Matching**: Fastenshtein (Levenshtein distance)
- **Normalization**: Text и Unit normalizers
- **Models**: 11 DTOs за типизиран data flow

## 🚀 Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- SQLite3 (вграден в .NET)

### Desktop App (Production)

```bash
# 1. Download installer from Releases
# Windows: FirstMake-Setup-2.0.0.exe
# Linux: FirstMake-2.0.0.AppImage or firstmake_2.0.0_amd64.deb

# 2. Install and run
# Backend стартира автоматично
# App достъпен от System Tray
```

### Desktop App (Development)

```bash
# 1. Clone repository
git clone https://github.com/BlagoyKozarev/First-make.git
cd First-make

# 2. Install dependencies
dotnet restore
cd desktop && npm install
cd ../src/UI && npm install

# 3. Run in dev mode (auto-starts backend + frontend + Electron)
cd ../../desktop
./dev-start.sh  # Linux/Mac
# or
dev-start.bat   # Windows

# Or manually:
# Terminal 1 - Backend
cd src/Api && dotnet run --urls "http://localhost:5085"

# Terminal 2 - Frontend
cd src/UI && npm run dev

# Terminal 3 - Electron
cd desktop && NODE_ENV=development npm start
```

### Web Mode (Legacy)

```bash
# Start API manually
cd src/Api
dotnet run --urls "http://localhost:5085"

# Start UI
cd ../UI
npm run dev
# Open http://localhost:5173
```

### Using Docker Compose

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

## 📖 API Documentation

### Base URLs

- **API**: `http://localhost:5000`
- **AI Gateway**: `http://localhost:5001`
- **UI**: `http://localhost:5174`

### API Endpoints

#### Health Check
```http
GET /healthz
```

#### File Upload & Parsing
```http
POST /parse
Content-Type: multipart/form-data

# Response: Parsed file structure
```

#### BoQ Extraction
```http
POST /extract
Content-Type: application/json

{
  "parsedData": { ... }
}
```

#### Fuzzy Matching
```http
POST /match
Content-Type: application/json

{
  "boq": { "project": {...}, "stages": [...], "items": [...] },
  "priceBase": [...]
}

# Response: Matched items with scores
```

#### LP Optimization
```http
POST /optimize
Content-Type: application/json

{
  "matchedItems": [...],
  "stages": [...],
  "lambda": 1000.0,
  "minCoeff": 0.4,
  "maxCoeff": 2.0
}

# Response: Optimized coefficients and stage summaries
```

#### Export КСС
```http
POST /export
Content-Type: application/json

{
  "result": { ... },
  "boq": { ... },
  "projectName": "Project Name",
  "splitByStage": false
}

# Response: XLSX file or ZIP archive
```

#### Observations & Metrics
```http
# Log operation
POST /observations

# Get metrics
GET /observations/metrics?since=2025-10-13T00:00:00Z

# Get recent operations
GET /observations/recent?limit=100
```

### AI Gateway Endpoints

#### PDF Layout Analysis
```http
POST /pdf/layout
Content-Type: multipart/form-data

# Response: Text blocks with positions
```

#### XLSX Parsing
```http
POST /xlsx/parse
Content-Type: multipart/form-data

# Response: Sheets, rows, cells
```

#### DOCX Parsing
```http
POST /docx/parse
Content-Type: multipart/form-data

# Response: Paragraphs and tables
```

## ⚙️ Configuration

### API Settings (`src/Api/appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=firstmake.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### AI Gateway Settings (`src/AiGateway/appsettings.json`)

```json
{
  "BgGpt": {
    "BaseUrl": "https://api.raicommerce.net/v1",
    "ApiKey": "YOUR_API_KEY",
    "Model": "gpt-4o-mini",
    "Temperature": 0.0
  },
  "Tesseract": {
    "DataPath": "/usr/share/tesseract-ocr/5/tessdata",
    "Languages": "bul+eng"
  }
}
```

### Environment Variables

```bash
# API
export ASPNETCORE_URLS="http://localhost:5000"
export ConnectionStrings__DefaultConnection="Data Source=firstmake.db"

# AI Gateway
export BgGpt__ApiKey="your-api-key-here"

# UI
export VITE_API_URL="http://localhost:5000"
export VITE_AI_GATEWAY_URL="http://localhost:5001"
```

## 🧪 Testing

### Run Unit Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific project
dotnet test tests/Core.Engine.Tests
```

### Current Test Results

- **Total Tests**: 32
- **Passing**: 31
- **Skipped**: 1
- **Coverage**: Core business logic

> Note: These are the latest local CI results run in the development environment. For up-to-date CI status, check the GitHub Actions page.

## 📊 Database Schema

### Tables

#### CachedMatch
```sql
CREATE TABLE CachedMatches (
    Id INTEGER PRIMARY KEY,
    BoqItemName TEXT NOT NULL,
    Unit TEXT NOT NULL,
    CandidatesJson TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME
);
CREATE INDEX IX_CachedMatch_BoqItemName_Unit ON CachedMatches(BoqItemName, Unit);
CREATE INDEX IX_CachedMatch_CreatedAt ON CachedMatches(CreatedAt);
```

#### UserApproval
```sql
CREATE TABLE UserApprovals (
    Id INTEGER PRIMARY KEY,
    SessionId TEXT NOT NULL,
    BoqItemId TEXT NOT NULL,
    BoqItemName TEXT NOT NULL,
    SelectedCatalogueItemId TEXT NOT NULL,
    SelectedCatalogueItemName TEXT NOT NULL,
    BasePrice DECIMAL NOT NULL,
    Unit TEXT NOT NULL,
    MatchScore REAL NOT NULL,
    ApprovedAt DATETIME NOT NULL
);
CREATE INDEX IX_UserApproval_SessionId ON UserApprovals(SessionId);
CREATE INDEX IX_UserApproval_BoqItemId ON UserApprovals(BoqItemId);
```

#### SessionData
```sql
CREATE TABLE SessionData (
    Id INTEGER PRIMARY KEY,
    SessionId TEXT NOT NULL,
    SourceFileName TEXT NOT NULL,
    BoqDataJson TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    CompletedAt DATETIME
);
CREATE INDEX IX_SessionData_CreatedAt ON SessionData(CreatedAt);
```

## 🔒 Security

- **Local-first**: Всички данни се съхраняват локално
- **Input validation**: Comprehensive validation на всички входни данни
- **File size limits**: 50MB за файлове, 100MB за request body
- **Request timeouts**: 5 минути default timeout
- **Error handling**: Global exception middleware без експозиране на sensitive данни

## 📦 Deployment

### Desktop App Production Build

```bash
# Full automated build (backend + frontend + Electron)
cd desktop
./build-all.sh  # Linux/Mac
# or
build-all.bat   # Windows

# Output:
# - Windows: desktop/dist/FirstMake-Setup-2.0.0.exe (NSIS installer)
#            desktop/dist/FirstMake-2.0.0-win.zip (portable)
# - Linux:   desktop/dist/FirstMake-2.0.0.AppImage
#            desktop/dist/firstmake_2.0.0_amd64.deb

# Manual build steps:
# 1. Build backend
cd src/Api
dotnet publish -c Release -o ../../desktop/backend-build

# 2. Build frontend
cd ../UI
npm run build  # Output: dist/

# 3. Build Electron
cd ../../desktop
npm install
npm run build
```

### Web Deployment (Legacy)

```bash
# Backend
cd src/Api
dotnet publish -c Release -o ./publish

cd ../AiGateway
dotnet publish -c Release -o ./publish

# Frontend
cd ../UI
npm run build
# Output: dist/
```

### Docker Deployment

```bash
# Build images
docker-compose build

# Run in production mode
docker-compose -f docker-compose.prod.yml up -d
```

### System Requirements

#### Desktop App
- **OS**: Windows 10+ / Ubuntu 20.04+ / macOS 10.15+
- **CPU**: 2+ cores (LP optimization е CPU-intensive)
- **RAM**: 4GB minimum, 8GB recommended
- **Disk**: 500MB за app + storage за database
- **.NET Runtime**: Bundled (self-contained)

#### Web Mode (Development)
- **CPU**: 2+ cores
- **RAM**: 4GB minimum, 8GB recommended
- **Disk**: 1GB за application + storage за database
- **OS**: Linux, macOS, Windows (cross-platform)
- **.NET SDK**: 8.0+ required

## 🤝 Contributing

Вижте [CONTRIBUTING.md](CONTRIBUTING.md) за детайли как да допринесете към проекта.

### Development Workflow

1. Fork repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## 📝 License

Този проект е лицензиран под MIT License - вижте [LICENSE](LICENSE) файла за детайли.

## 👥 Authors

- **Blagoy Kozarev** - *Initial work* - [BlagoyKozarev](https://github.com/BlagoyKozarev)

## 🙏 Acknowledgments

- **Google OR-Tools** - LP optimization engine
- **EPPlus** - Excel file generation
- **iText7** - PDF text extraction
- **shadcn/ui** - UI component library
- **BG GPT** - Bulgarian LLM integration

## � Performance

FirstMake Agent е оптимизиран за висока производителност:

- **API Response Times (P95)**:
  - Health check: < 10ms
  - Parse: < 500ms
  - Match: < 100ms
  - Optimize: < 500ms
  - Export: < 300ms

- **Throughput**: 50+ concurrent requests на match endpoint
- **Memory**: < 500MB per service под нормално натоварване
- **Resource Optimization**: Server GC, response caching, connection pooling

### Performance Testing

Вграден е пълен suite от performance тестове:

```bash
# Run all performance tests
cd tests/Performance
./run-performance-tests.sh

# Individual benchmarks
dotnet run -c Release -- --filter "*FuzzyMatcher*"

# Load testing
k6 run load-tests/api-endpoints.js
```

За детайли вижте [Performance Testing Guide](docs/PERFORMANCE_TESTING.md).

## 🖥️ Desktop App Architecture

FirstMake v2.0 е пълноценно **Electron desktop приложение** с embedded backend.

### Features

- **Auto-Start Backend**: Electron автоматично стартира .NET API при отваряне
- **System Tray**: Persistent background mode с tray icon (click to show/hide)
- **Native Dialogs**: File picker чрез IPC bridge (selectFiles, selectFolder, saveFile)
- **Window State**: Saved position/size чрез electron-store
- **Logging**: Unified logs (main + renderer + backend) с electron-log
- **Cross-Platform**: Windows (NSIS installer), Linux (AppImage, deb)

### Architecture

```
┌────────────────────────────────────────────────────────┐
│                  Electron Main Process                  │
│                                                         │
│  ┌──────────────┐          ┌──────────────────────┐   │
│  │ startBackend │ ────────▶│  .NET API Process    │   │
│  │              │          │  (port 5085)         │   │
│  │ • spawn()    │          │  • Auto-start        │   │
│  │ • monitor    │          │  • stdout logging    │   │
│  │ • SIGTERM    │          │  • SIGTERM on quit   │   │
│  └──────────────┘          └──────────────────────┘   │
│                                                         │
│  ┌──────────────┐          ┌──────────────────────┐   │
│  │ System Tray  │          │  IPC Handlers        │   │
│  │ • Show/Hide  │          │  • selectFiles       │   │
│  │ • Exit       │          │  • selectFolder      │   │
│  └──────────────┘          │  • saveFile          │   │
│                            └──────────────────────┘   │
└────────────────────────────────────────────────────────┘
                        │
                        ▼
┌────────────────────────────────────────────────────────┐
│              Renderer Process (React UI)               │
│                                                         │
│  window.electron.selectFiles()  ─────▶  IPC Bridge    │
│  window.electron.getBackendStatus() ──▶  Preload      │
└────────────────────────────────────────────────────────┘
```

### Backend Lifecycle

**Development Mode:**
```bash
NODE_ENV=development npm start
# Backend: dotnet run --project ../src/Api/Api.csproj
# Frontend: http://localhost:5173 (Vite dev server)
```

**Production Mode:**
```bash
npm run build
# Backend: Bundled exe in resources/backend/
# Frontend: Bundled dist/ files
```

**Startup Sequence:**
1. Electron main process starts
2. `startBackend()` spawns .NET API
3. Monitors stdout for "Now listening on: http://localhost:5085"
4. Creates window after backend ready (or 10s timeout)
5. Loads React UI

**Shutdown Sequence:**
1. User clicks Exit or closes window
2. `stopBackend()` sends SIGTERM to API process
3. Electron waits for graceful shutdown
4. App exits

### Desktop-Specific APIs

React code може да използва desktop APIs чрез IPC bridge:

```typescript
// Check if running in Electron
if (window.electron?.isElectron) {
  // Select multiple files
  const files = await window.electron.selectFiles({
    filters: [{ name: 'Excel', extensions: ['xlsx'] }],
    properties: ['openFile', 'multiSelections']
  });

  // Select folder
  const folder = await window.electron.selectFolder();

  // Save file
  const path = await window.electron.saveFile({
    defaultPath: 'result.xlsx',
    filters: [{ name: 'Excel', extensions: ['xlsx'] }]
  });

  // Get backend status
  const status = await window.electron.getBackendStatus();
  // { running: true, port: 5085, pid: 12345 }
}
```

### Directory Structure

```
desktop/
├── main.js              # Electron main process (260 lines)
├── preload.js           # IPC bridge with context isolation
├── package.json         # electron-builder config
├── dev-start.sh         # Dev mode helper script
├── build-all.sh         # Full build automation
├── build-all.bat        # Windows build script
├── README.md            # Desktop app documentation
├── assets/
│   ├── icon.png         # App icon (512x512)
│   └── icon.ico         # Windows icon
├── backend-build/       # Backend build output (ignored)
├── dist/                # Electron build output (ignored)
└── node_modules/        # Dependencies (ignored)
```

За детайли вижте [Desktop README](desktop/README.md).

## �📚 Additional Resources

- [User Manual (BG)](docs/USER_MANUAL.md) - Пълно ръководство за потребителя
- [Schemas Documentation](Schemas/README.md) - JSON schemas и конфигурация
- [API Reference](docs/API.md) - Comprehensive API documentation
- [Architecture Guide](docs/ARCHITECTURE.md) - Technical architecture details
- [Deployment Guide](docs/DEPLOYMENT.md) - Production deployment instructions
- [Performance Testing](docs/PERFORMANCE_TESTING.md) - Benchmarking and load testing
- [Release Notes](RELEASE_NOTES.md) - v1.0.0 release information

---

**Built with ❤️ for the Bulgarian construction industry**
