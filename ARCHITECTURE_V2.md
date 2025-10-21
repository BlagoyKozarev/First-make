# FirstMake Agent v2.0 - Architecture Document

## Project Overview
Desktop application for automated КСС (Bill of Quantities) processing with multi-file support, iterative optimization, and operator-controlled workflow.

## Key Requirements

### 1. Multi-File Processing
- **Указания (Instructions)**: 1-2 Word/PDF files
- **Единични цени (Price Base)**: 1-2 Excel files  
- **КСС файлове**: Up to 25 Excel files
- **Шаблон (Template)**: 1 Excel file for output structure

### 2. Workflow
```
Upload Files → Parse & Extract → Match Positions → 
Operator Review → Iteration 1 → Review Results → 
Iteration 2 → ... → Export All Iterations
```

### 3. Iteration System
- Each iteration generates new output files
- Files named: `KSS_<original>_1.xlsx`, `KSS_<original>_2.xlsx`, etc.
- History preserved for all iterations
- Operator triggers each iteration manually

### 4. Operator Interactions
All requiring modal dialogs with Yes/No buttons:
- Coefficient > 2.0 detected
- No match found for position
- Multiple partial matches available
- Position with value 0
- Different units for same name

### 5. Mathematical Model
- **Constraint**: `(Прогнозна стойност) - (Предложена цена) ≥ 0`
- **Objective**: Minimize total gap while keeping coefficients close to 1.0
- **Unified coefficients**: Same (Name, Unit) pair → same coefficient across all files
- **Bounds**: `0.4 ≤ Coefficient ≤ 2.0`

## Architecture Changes

### Data Models

#### Project Session
```typescript
interface ProjectSession {
  id: string
  objectName: string        // Име на обект
  employee: string          // Служител
  date: string             // Дата
  
  // Input files
  instructionsFiles: File[]      // Указания (1-2)
  priceBaseFiles: File[]         // Единични цени (1-2)
  kssFiles: File[]               // КСС (up to 25)
  templateFile: File | null      // Шаблон за изход
  
  // Extracted data
  forecastValues: StageForecasts  // Прогнозни стойности от Указания
  priceBase: PriceEntry[]         // База с цени
  boqs: BoqDocument[]             // Всички КСС документи
  
  // Iterations
  iterations: IterationResult[]
  currentIteration: number
  
  createdAt: Date
  updatedAt: Date
}
```

#### BoQ Document (per file)
```typescript
interface BoqDocument {
  id: string
  fileName: string
  sourceFile: string
  stages: Stage[]
  items: BoqItem[]
}

interface BoqItem {
  id: string
  stageCode: string
  name: string              // Наименование
  unit: string              // Мярка
  quantity: number          // К-во
  sourceRow: number
  sourceSheet: string
}
```

#### Price Base
```typescript
interface PriceEntry {
  name: string              // Наименование
  unit: string              // Мярка
  basePrice: number         // Единична цена
  sourceFile: string
  sourceRow: number
}
```

#### Stage Forecasts (from Указания)
```typescript
interface StageForecasts {
  stages: {
    [stageCode: string]: {
      name: string
      forecast: number      // Прогнозна стойност
    }
  }
  totalForecast: number     // Обща прогнозна стойност
  sourceFile: string
}
```

#### Iteration Result
```typescript
interface IterationResult {
  iterationNumber: number
  timestamp: Date
  
  // Coefficients (unified across all BOQs)
  coefficients: {
    [key: string]: {       // key = `${name}|${unit}`
      name: string
      unit: string
      coefficient: number
      basePrice: number
    }
  }
  
  // Results per BOQ file
  boqResults: {
    [fileName: string]: {
      stages: StageResult[]
      totalProposed: number
      totalForecast: number
      totalGap: number
    }
  }
  
  // Overall results
  overallProposed: number
  overallForecast: number
  overallGap: number
  
  // Optimization metadata
  solverStatus: string
  solveDurationMs: number
  objective: number
  
  // Generated files
  outputFiles: {
    fileName: string
    path: string
  }[]
}

interface StageResult {
  stageCode: string
  stageName: string
  forecast: number          // Прогнозна стойност
  proposed: number          // Предложена цена
  gap: number               // Прогнозна - Предложена
  ok: boolean               // gap ≥ 0
  items: ItemResult[]
}

interface ItemResult {
  name: string
  unit: string
  quantity: number
  basePrice: number
  coefficient: number
  workPrice: number         // basePrice × coefficient
  value: number             // quantity × workPrice
}
```

### Backend Changes

#### New Endpoints

```
POST /api/session/create
Body: { objectName, employee, date }
Response: { sessionId }

POST /api/session/{id}/upload/instructions
MultipartForm: files[]
Response: { fileIds[], extractedForecasts }

POST /api/session/{id}/upload/pricebase
MultipartForm: files[]
Response: { fileIds[], priceEntries[] }

POST /api/session/{id}/upload/kss
MultipartForm: files[] (up to 25)
Response: { fileIds[], boqDocuments[] }

POST /api/session/{id}/upload/template
MultipartForm: file
Response: { fileId, templateStructure }

POST /api/session/{id}/match
Body: { manualOverrides? }
Response: { matchedItems[], unmatchedItems[], conflicts[] }

POST /api/session/{id}/confirm-matches
Body: { confirmedMatches[], resolvedConflicts[] }
Response: { ready: true }

POST /api/session/{id}/iterate
Body: { lambda?, minCoeff?, maxCoeff? }
Response: { iteration: IterationResult }

GET /api/session/{id}/iterations
Response: { iterations: IterationResult[] }

GET /api/session/{id}/export/{iterationNumber}
Response: application/zip (all output files)

GET /api/session/{id}/compare?iterations=1,2,3
Response: { comparison: ComparisonData }
```

#### New Services

**MultiFileParser**
- Parses multiple КСС Excel files
- Merges into unified structure
- Validates consistency

**InstructionsExtractor**
- Parses Word/PDF Указания
- Extracts forecast values using LLM
- Maps stages to КСС stages

**PriceBaseLoader**
- Loads Excel price base
- Validates structure
- Creates searchable index

**IterationManager**
- Stores iteration history
- Manages iteration state
- Generates iteration comparisons

**ExcelTemplateApplier**
- Loads template structure
- Applies formatting to output
- Preserves formulas

### Frontend Changes

#### New Pages

**1. ProjectSetupPage** (`/setup`)
```
Form:
- Име на обект
- Служител (dropdown + free text)
- Дата (date picker)
→ Creates session
```

**2. UploadPage** (`/upload`) - Redesigned
```
Four upload zones:
1. Указания (1-2 files, Word/PDF)
2. Единични цени (1-2 files, Excel)
3. КСС файлове (up to 25 files, Excel)
4. Шаблон (1 file, Excel, optional)

Each zone shows:
- File list with names, sizes
- Remove button per file
- Progress during upload
- Extracted metadata preview
```

**3. MatchReviewPage** (`/match-review`)
```
Table of all positions from all КСС:
- Grouped by (Name, Unit)
- Shows source files
- Match confidence score
- Suggested price from base

Actions:
- Auto-accept high-confidence matches
- Manual selection for conflicts
- Mark as "No match" (error)
- Add custom price

Modal dialogs:
- "Multiple matches found for X" → Select from list
- "No match for X" → Enter manually / Skip
- "Different units for same name" → Confirm / Fix
```

**4. IterationPage** (`/iterate`) - Redesigned
```
Left panel:
- Current iteration number
- Lambda, MinCoeff, MaxCoeff controls
- "Стартирай нова итерация" button

Center panel:
- Per-stage results table:
  - Stage name
  - Прогнозна стойност
  - Предложена цена  
  - Gap (with color: green ≥ 0, red < 0)
  - Status (✓ OK / ✗ Over budget)

- Overall summary:
  - Total forecast
  - Total proposed
  - Total gap
  - Average coefficient
  - Coefficients distribution chart

Right panel:
- Iteration history list
- Compare iterations button
- Download iteration files

Warnings (modal dialogs):
- "Coefficient > 2.0 detected for 5 positions. Continue?" → Yes/No
- "Gap < 0 for Stage 2. Continue?" → Yes/No
```

**5. ComparisonPage** (`/compare`)
```
Select iterations: [1] [2] [3] (checkboxes)

Comparison table:
| Position | Iteration 1 Coeff | Iteration 2 Coeff | Iteration 3 Coeff | Change |
|----------|-------------------|-------------------|-------------------|--------|
| Бетон    | 1.05             | 0.98             | 1.02             | -0.03  |

Stage-by-stage comparison chart
Gap evolution chart
```

**6. ExportPage** (`/export`) - Enhanced
```
Select iterations: [1] [2] [3] (checkboxes)

Export options:
- ☑ Include all КСС files
- ☑ Split by stage
- ☑ Include comparison report
- ☑ Include log files

Button: "Свали изходни файлове"
→ Downloads ZIP with structure:
  iteration_1/
    KSS_Приложение_1_1.xlsx
    KSS_Приложение_2_1.xlsx
    ...
  iteration_2/
    KSS_Приложение_1_2.xlsx
    ...
  comparison_report.xlsx
  log.txt
```

#### New Components

**FileUploadZone**
```tsx
<FileUploadZone
  title="Прикачи файлове КСС"
  accept=".xlsx"
  maxFiles={25}
  files={kssFiles}
  onFilesChange={setKssFiles}
  showPreview={true}
/>
```

**ConfirmDialog**
```tsx
<ConfirmDialog
  open={showDialog}
  title="Коефициент над 2.0"
  message="Позиция 'Бетон C16/20' има коефициент 2.15. Продължи?"
  onConfirm={() => handleContinue()}
  onCancel={() => handleAbort()}
/>
```

**IterationComparisonChart**
```tsx
<IterationComparisonChart
  iterations={[1, 2, 3]}
  data={comparisonData}
  metric="gap" // or "coefficient" or "proposed"
/>
```

### Desktop App (Electron)

#### Setup
```
src/
  electron/
    main.ts              # Electron main process
    preload.ts           # IPC bridge
    menu.ts              # App menu
  desktop/
    installer.ts         # Installer config
    auto-updater.ts      # Auto-update logic
```

#### Features
- Native file dialogs
- Local storage (SQLite)
- No internet required after install
- Auto-updates (optional)
- Native notifications for errors

#### Build
```bash
npm run build:desktop
# Outputs:
# dist/FirstMake-Setup-1.0.0.exe (Windows)
# dist/FirstMake-1.0.0.dmg (macOS)
# dist/FirstMake-1.0.0.AppImage (Linux)
```

## Implementation Phases

### Phase 1: Data Models & Backend (Day 1-2)
- [ ] New models (ProjectSession, BoqDocument, etc.)
- [ ] MultiFileParser service
- [ ] InstructionsExtractor service
- [ ] PriceBaseLoader service
- [ ] IterationManager service
- [ ] New API endpoints

### Phase 2: Frontend Core (Day 2-3)
- [ ] ProjectSetupPage
- [ ] Multi-file UploadPage
- [ ] MatchReviewPage
- [ ] ConfirmDialog component
- [ ] FileUploadZone component

### Phase 3: Iteration System (Day 3-4)
- [ ] Enhanced IterationPage
- [ ] Iteration history UI
- [ ] Comparison functionality
- [ ] Warning/error dialogs

### Phase 4: Excel Export (Day 4-5)
- [ ] Template-based export
- [ ] Multi-file generation
- [ ] Coefficient column
- [ ] Work price column
- [ ] Gap calculation per stage

### Phase 5: Desktop Wrapper (Day 5-6)
- [ ] Electron setup
- [ ] IPC communication
- [ ] Native dialogs
- [ ] Build pipeline
- [ ] Installers

### Phase 6: Testing & Polish (Day 6-7)
- [ ] End-to-end testing with real files
- [ ] Error handling
- [ ] Performance optimization
- [ ] Documentation
- [ ] User guide

## Technology Stack

### Backend
- .NET 8 Web API
- SQLite for sessions/iterations
- EPPlus for Excel
- iText7 for PDF
- OpenXML for Word
- Google OR-Tools for LP

### Frontend
- React 18 + TypeScript
- Vite
- Tailwind CSS + shadcn/ui
- Recharts for visualizations
- React Hook Form for forms

### Desktop
- Electron 28+
- electron-builder for packaging
- electron-updater for auto-updates

### AI/ML
- BG GPT API for Instructions extraction
- Tesseract for OCR (if needed)

## File Structure (Updated)

```
src/
  Api/
    Controllers/
      SessionController.cs         # New
      IterationController.cs       # New
    Services/
      MultiFileParserService.cs    # New
      InstructionsExtractorService.cs  # New
      PriceBaseLoaderService.cs    # New
      IterationManagerService.cs   # New
      ExcelTemplateService.cs      # New
  Core.Engine/
    Models/
      ProjectSession.cs            # New
      BoqDocument.cs              # New
      IterationResult.cs          # New
      StageForecasts.cs           # New
    Services/
      LpOptimizer.cs              # Enhanced
      FuzzyMatcher.cs             # Enhanced
  UI/
    src/
      pages/
        ProjectSetupPage.tsx       # New
        UploadPage.tsx             # Redesigned
        MatchReviewPage.tsx        # New
        IterationPage.tsx          # Redesigned
        ComparisonPage.tsx         # New
        ExportPage.tsx             # Enhanced
      components/
        FileUploadZone.tsx         # New
        ConfirmDialog.tsx          # New
        IterationHistoryList.tsx   # New
        ComparisonChart.tsx        # New
  electron/                        # New
    main.ts
    preload.ts
```

## Migration Strategy

1. **Preserve existing code** - Create v2 branch
2. **Parallel implementation** - New endpoints alongside old
3. **Feature flags** - Toggle between v1/v2 UI
4. **Data migration** - Convert old session format if needed
5. **Gradual rollout** - Test with pilot users first

## Success Criteria

- [ ] Can upload 25 КСС files simultaneously
- [ ] Extracts forecast from Указания automatically
- [ ] Shows operator dialogs at correct moments
- [ ] Generates correct Excel output with all columns
- [ ] Tracks iteration history correctly
- [ ] Desktop app installs and runs offline
- [ ] Export creates properly formatted ZIP
- [ ] Gap ≥ 0 constraint always satisfied
- [ ] Same positions get same coefficients across files

---

**Version**: 2.0.0  
**Status**: Architecture Design  
**Next Step**: Implement Phase 1 after file analysis
