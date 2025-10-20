# FirstMake Agent

**Local-first BoQ Processing and LP Optimization for Construction Offers**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6)](https://www.typescriptlang.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## 🎯 Overview

FirstMake Agent е локално приложение за обработка на Количествено-стойностни сметки (КСС) и оптимизация на строителни оферти. Системата позволява автоматизирано извличане, съпоставяне и оптимизация на данни от разнородни източници (XLSX, DOCX, PDF, сканирани документи).

### Основни функционалности

- 📄 **Интелигентно парсиране** - Поддръжка на XLSX, DOCX, PDF + OCR за сканирани документи
- 🔍 **Fuzzy matching** - Автоматично съпоставяне на позиции с ценови каталог
- 📊 **LP оптимизация** - Google OR-Tools за оптимално ценообразуване
- 📈 **Метрики и телеметрия** - Real-time dashboard за проследяване на операции
- 💾 **Локално съхранение** - SQLite база данни, без cloud dependencies
- 🎨 **Модерен UI** - React с Tailwind CSS и shadcn/ui компоненти
- 📑 **Excel експорт** - Генериране на КСС файлове с формули и форматиране

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

### Development Setup

```bash
# 1. Clone repository
git clone https://github.com/BlagoyKozarev/First-make.git
cd First-make

# 2. Restore .NET dependencies
dotnet restore

# 3. Run database migrations
cd src/Api
dotnet ef database update

# 4. Install UI dependencies
cd ../UI
npm install

# 5. Start all services
# Terminal 1 - API
cd src/Api
dotnet run --urls "http://localhost:5000"

# Terminal 2 - AiGateway
cd src/AiGateway
dotnet run --urls "http://localhost:5001"

# Terminal 3 - UI
cd src/UI
npm run dev
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

- **Total Tests**: 26
- **Passing**: 26 (100%)
- **Coverage**: Core business logic
- **Runtime**: ~128ms

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

### Production Build

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

- **CPU**: 2+ cores (LP optimization е CPU-intensive)
- **RAM**: 4GB minimum, 8GB recommended
- **Disk**: 1GB за application + storage за database
- **OS**: Linux, macOS, Windows (cross-platform)

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

## 📚 Additional Resources

- [User Manual (BG)](docs/USER_MANUAL.md) - Пълно ръководство за потребителя
- [Schemas Documentation](Schemas/README.md) - JSON schemas и конфигурация
- [API Reference](docs/API.md) - Comprehensive API documentation
- [Architecture Guide](docs/ARCHITECTURE.md) - Technical architecture details
- [Deployment Guide](docs/DEPLOYMENT.md) - Production deployment instructions

---

**Built with ❤️ for the Bulgarian construction industry**
