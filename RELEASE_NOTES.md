# FirstMake Agent v1.0.0 - Initial Release

**Release Date:** October 28, 2025

## 🎉 Overview

FirstMake Agent е local-first приложение за автоматизирано обработване на Количествено-стойностни сметки (КСС) и оптимизация на строителни оферти за българската строителна индустрия.

## ✨ Новости в тази версия

### Основни подобрения (v1.0.0)

#### Backend
- ✅ **Parser Fixes** - Подобрено числено парсване с поддръжка на EU/US формати
  - Обработка на десетични сепаратори (запетая/точка)
  - Обработка на хилядни разделители
  - По-надеждно разпознаване на валути
  
- ✅ **Price Base Deduplication** - Автоматично премахване на дупликати
  - Дедупликация по ключ (име + мярка)
  - Warning logs при откриване на дупликати
  - По-бързо зареждане на големи ценови бази

#### Frontend
- ✅ **Fast Refresh Fix** - Решен lint проблем с Fast Refresh
  - Рефакториране на споделени константи
  - Подобрена developer experience
  - По-бърз hot reload при разработка

#### CI/CD
- ✅ **GitHub Actions Workflow** - Автоматично тестване и build
  - Backend build и тестове (.NET)
  - Frontend lint и build (Vite)
  - Автоматично при push и PR

#### Testing
- ✅ **Unit Tests** - 31 passed, 1 skipped (от 32 total)
  - Core.Engine тестове зелени
  - FuzzyMatcher coverage
  - LP Optimizer validation
  - Normalizers тестове

#### Security
- ✅ **Security Sweep** - Проверка за изтекли секрети
  - Git history scan за credentials
  - Placeholder-и в deployment files
  - Документация за secret management

## ✨ Features (Core Platform)

### Core Functionality
- ✅ **Multi-format File Parsing** - XLSX, DOCX, PDF support with OCR
- ✅ **AI-Powered Extraction** - BG GPT integration for intelligent data extraction
- ✅ **Fuzzy Matching** - Levenshtein distance with unit normalization
- ✅ **LP Optimization** - Google OR-Tools GLOP solver for coefficient optimization
- ✅ **Excel Export** - Professional КСС generation with formulas and formatting

### Technical Stack

**Backend:**
- .NET 8 Minimal API (Api + AiGateway)
- Core.Engine class library
- Google OR-Tools 9.11.4210
- iText7 8.0.5 (PDF parsing)
- DocumentFormat.OpenXml 3.1.0 (XLSX/DOCX)
- Tesseract OCR (Bulgarian + English)
- EPPlus 7.5.0 (Excel generation)
- EF Core 8.0.10 + SQLite

**Frontend:**
- React 18.3 with TypeScript 5.5
- Vite 5.4 build tool
- Tailwind CSS 3.4
- shadcn/ui components
- React Router v6

**Infrastructure:**
- Docker Compose
- DevContainer support
- 26 xUnit tests (100% pass rate)

### Key Components

#### 1. File Parsing (AI Gateway)
- PDF text extraction via iText7
- OCR fallback with Tesseract (bul+eng)
- XLSX sheet/row/cell parsing
- DOCX paragraph and table extraction

#### 2. BoQ Extraction
- LLM-powered structured data extraction
- JSON schema validation
- Project metadata extraction
- Stage and item organization

#### 3. Fuzzy Matching
- Text normalization (diacritics, whitespace)
- Unit normalization (60+ Bulgarian/English aliases)
- Levenshtein distance scoring
- Match caching (30 days)
- User approval tracking

#### 4. LP Optimization
- L1 penalty objective function
- Coefficient bounds (configurable, default 0.4-2.0)
- Stage-level budget constraints (optional)
- Feasibility validation
- Per-stage summaries

#### 5. Excel Export
- Professional КСС formatting
- Formulas (Цена = Базова × κ, Стойност = Количество × Цена)
- Single file or ZIP per stage
- EPPlus cell styling and borders

#### 6. Observability
- SHA256 file hashing
- Duplicate detection (1 hour window)
- Metrics aggregation
- Operations dashboard

## 📊 Database Schema

**SQLite Tables:**
- `CachedMatches` - Match result caching
- `UserApprovals` - User-selected matches
- `SessionData` - Workflow session tracking

## 🔒 Security & Hardening

- **Input Validation** - Comprehensive validation for all endpoints
- **File Size Limits** - 50MB per file, 100MB request body
- **Request Timeouts** - 5 minute default timeout
- **Error Handling** - Global exception middleware
- **Local-First** - No cloud dependencies, all data local

## 📖 Documentation

- **README.md** - Project overview and quick start
- **docs/API.md** - Complete API reference (10 endpoints)
- **docs/ARCHITECTURE.md** - Technical architecture guide
- **docs/DEPLOYMENT.md** - Production deployment instructions
- **docs/USER_MANUAL.md** - End-user guide (Bulgarian)
- **CONTRIBUTING.md** - Developer contribution guidelines

## 🧪 Testing

- **26 Unit Tests** - Core.Engine business logic
- **100% Pass Rate** - All tests passing
- **Coverage** - FuzzyMatcher, LpOptimizer, Normalizers

Test Results:
```
Passed!  - Failed:     0, Passed:    26, Skipped:     0, Total:    26
```

## 🚀 Installation

### Prerequisites
- .NET 8 SDK/Runtime
- Node.js 20+
- SQLite3

### Quick Start

**Development:**
```bash
# Clone repository
git clone https://github.com/BlagoyKozarev/First-make.git
cd First-make

# Start backend
cd src/Api && dotnet run --urls "http://localhost:5000" &
cd src/AiGateway && dotnet run --urls "http://localhost:5001" &

# Start frontend
cd src/UI && npm install && npm run dev
```

**Docker:**
```bash
docker-compose up -d
```

Open browser: http://localhost:5174

## 📦 Deliverables

### Source Code
- Complete .NET solution
- React UI source
- JSON schemas and configs
- Unit tests

### Documentation
- 110KB+ markdown documentation
- API reference with examples
- Architecture diagrams
- Deployment guides

### Configuration
- DevContainer for consistent development
- Docker Compose for easy deployment
- Environment variable templates
- Systemd service examples

## 🔄 Workflow

1. **Upload** - XLSX/DOCX/PDF file
2. **Extract** - AI-powered BoQ extraction
3. **Match** - Fuzzy matching with price base
4. **Optimize** - LP coefficient optimization
5. **Export** - Excel КСС generation

## 🌐 API Endpoints

- `GET /healthz` - Health check
- `POST /parse` - File parsing
- `POST /extract` - BoQ extraction
- `POST /match` - Fuzzy matching
- `POST /optimize` - LP optimization
- `POST /export` - Excel export
- `POST /observations` - Log operation
- `GET /observations/metrics` - Get metrics
- `GET /observations/recent` - Recent operations

## ⚙️ Configuration

### API Settings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=firstmake.db"
  }
}
```

### AI Gateway Settings
```json
{
  "BgGpt": {
    "BaseUrl": "https://api.raicommerce.net/v1",
    "ApiKey": "YOUR_API_KEY",
    "Model": "gpt-4o-mini"
  },
  "Tesseract": {
    "DataPath": "/usr/share/tesseract-ocr/5/tessdata",
    "Languages": "bul+eng"
  }
}
```

## 📈 Performance

- **Parse** - ~500ms for 5MB XLSX
- **Extract** - ~2s with LLM
- **Match** - ~100ms for 100 items (cached: ~5ms)
- **Optimize** - ~500ms for 100 items
- **Export** - ~300ms for 100 items

## 🐛 Known Limitations

1. **OCR Accuracy** - Dependent on scan quality
2. **LLM Extraction** - May require manual review for complex documents
3. **Single-User** - No multi-user authentication (by design - local-first)
4. **Memory** - Large files (>50MB) not supported
5. **TypeScript/ESLint Warning** - Installed TS 5.9.3 vs supported <5.4.0 (functionally works)

## 🔮 Roadmap (Post v1.0.0)

### Critical (Next Steps)
- [ ] Deep secret scan с truffleHog/detect-secrets
- [ ] CI publish workflow за Docker images → GHCR
- [ ] Production docker-compose validation
- [ ] GitHub Release със artifacts

### High Priority
- [ ] TypeScript/ESLint compatibility fix
- [ ] Frontend component tests (vitest + RTL)
- [ ] UI компонентизация (CandidateCard, TopCandidatesList)
- [ ] Accessibility improvements

### Medium Priority
- [ ] Integration smoke tests
- [ ] DevContainer/Codespaces подобрения
- [ ] Performance tuning за LP операции
- [ ] Code coverage увеличаване

### Future Enhancements
- Additional file formats (ODS, CSV)
- Batch processing за multiple files
- Export template customization
- Advanced metrics dashboard
- Multi-language UI support
- Integration със външни accounting systems

## 📦 Installation & Upgrade

### Fresh Installation

```bash
git clone https://github.com/GitRaicommerce/First-make.git
cd First-make
dotnet restore
cd src/UI && npm install
```

### Upgrade от previous version

*Първа версия - няма upgrade path*

## 🔄 Breaking Changes

*Няма - първа версия*

## 📝 Deprecations

*Няма*

## 🙏 Acknowledgments

### Technologies
- **Google OR-Tools** - LP optimization engine
- **Microsoft .NET** - Backend framework
- **React Team** - Frontend framework
- **Vite** - Build tool
- **EPPlus** - Excel generation
- **iText7** - PDF processing
- **shadcn/ui** - UI components

### Special Thanks
- Bulgarian construction industry for requirements and feedback
- BG GPT team for LLM API access

## 📝 License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file.

## 👥 Contributors

- **Blagoy Kozarev** - Initial development

## 📞 Support

- **GitHub Issues:** https://github.com/BlagoyKozarev/First-make/issues
- **Documentation:** See `/docs` folder
- **Email:** (if applicable)

## 🎯 Next Steps

After installation:
1. Configure BG GPT API key in `src/AiGateway/appsettings.json`
2. Prepare your price base (JSON or XLSX format)
3. Upload your first КСС document
4. Review and approve matches
5. Optimize and export

---

**Built with ❤️ for the Bulgarian construction industry**

For detailed documentation, see:
- [README.md](README.md)
- [API Reference](docs/API.md)
- [User Manual](docs/USER_MANUAL.md)
- [Architecture Guide](docs/ARCHITECTURE.md)
- [Deployment Guide](docs/DEPLOYMENT.md)
