# Schemas Documentation

JSON Schemas и YAML конфигурации за FirstMake Agent data structures.

## 📁 Structure

```
/Schemas
├── boq.schema.json           # Bill of Quantities (КСС) schema
├── pricebase.schema.json     # Price catalogue schema
├── result.schema.json        # LP optimization result schema
├── units.yaml                # Unit normalization aliases
├── examples/
│   └── sample_boq.yaml       # Example BoQ data
├── playbooks/
│   └── extract.yaml          # Extraction pipeline configuration
└── prompts/
    ├── extract.system.txt    # LLM system prompt for extraction
    └── extract.user.txt      # LLM user prompt template
```

## 🎯 Schemas Overview

### boq.schema.json
Defines the normalized structure for КСС (Bill of Quantities) data:
- **Project metadata**: name, date, employee
- **Stages**: construction phases with forecasted budgets
- **Items**: line items with stage, name, unit, quantity, and source provenance

**Example:**
```json
{
  "project": {
    "name": "Жилищна сграда - бул. Витоша",
    "date": "2025-10-15",
    "employee": "Иван Петров"
  },
  "stages": [
    {
      "code": "S1",
      "name": "Груб строеж",
      "forecast": 150000.00
    }
  ],
  "items": [
    {
      "stage": "S1",
      "name": "Изкопни работи",
      "unit": "м3",
      "qty": 250.5,
      "source": {
        "file": "КСС_Stage1.xlsx",
        "page": 1,
        "cell": "A5"
      }
    }
  ]
}
```

### pricebase.schema.json
Catalogue of base prices for matching:
- **name**: canonical item name (normalized)
- **unit**: canonical unit
- **basePrice**: price before coefficient application
- **aliases**: alternative names for fuzzy matching

**Example:**
```json
[
  {
    "name": "Изкопни работи - ръчни",
    "unit": "м3",
    "basePrice": 45.50,
    "category": "Земни работи",
    "aliases": ["изкоп ръчен", "ръчен изкоп"]
  }
]
```

### result.schema.json
LP optimization output:
- **coeffs**: optimized coefficients per (name, unit) pair
- **kssExports**: per-КСС export data with computed values
- **summary**: per-stage budget status and feasibility

**Example:**
```json
{
  "coeffs": [
    {
      "name": "Изкопни работи",
      "unit": "м3",
      "c": 1.15,
      "basePrice": 45.50
    }
  ],
  "summary": {
    "perStage": [
      {
        "stage": "S1",
        "forecast": 150000,
        "proposed": 148750,
        "gap": 1250,
        "ok": true
      }
    ],
    "ok": true
  }
}
```

## ⚙️ Configuration Files

### units.yaml
Maps variant unit spellings to canonical forms:
- Handles Bulgarian/English variants
- Supports abbreviations and full names
- Used for normalization during extraction and matching

### playbooks/extract.yaml
Defines the extraction pipeline:
1. Deterministic parsers (XLSX, DOCX, PDF)
2. OCR fallback (Tesseract)
3. LLM extraction (BG GPT-7B)
4. Schema validation with auto-retry
5. Evidence tagging

### prompts/
LLM prompts for BG GPT-7B:
- **extract.system.txt**: System-level instructions (JSON-only output, quality rules)
- **extract.user.txt**: User prompt template with placeholders for schema and text chunks

## 🔄 Usage in Code

### C# (.NET)
```csharp
// Load and validate against schema
var schemaJson = File.ReadAllText("Schemas/boq.schema.json");
var schema = JsonSchema.FromText(schemaJson);
var boq = JsonSerializer.Deserialize<BoqDto>(inputJson);
var isValid = schema.Validate(boq);
```

### TypeScript (UI)
```typescript
import boqSchema from '@/Schemas/boq.schema.json';
import { z } from 'zod';

// Runtime validation with Zod (convert from JSON Schema)
const BoqSchema = zodFromJsonSchema(boqSchema);
const validatedBoq = BoqSchema.parse(data);
```

## 📝 Validation

All schemas use **JSON Schema Draft 2020-12**.

To validate data:
```bash
# Using ajv-cli
npm install -g ajv-cli
ajv validate -s Schemas/boq.schema.json -d data.json
```

## 🌐 Schema IDs

Internal schema URIs use the `firstmake.local` domain for local resolution without external dependencies.
