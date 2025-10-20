# API Reference

Comprehensive API documentation за FirstMake Agent endpoints.

## Table of Contents

- [Authentication](#authentication)
- [API Endpoints](#api-endpoints)
  - [Health Check](#health-check)
  - [File Parsing](#file-parsing)
  - [BoQ Extraction](#boq-extraction)
  - [Fuzzy Matching](#fuzzy-matching)
  - [LP Optimization](#lp-optimization)
  - [Excel Export](#excel-export)
  - [Observations](#observations)
- [AI Gateway Endpoints](#ai-gateway-endpoints)
- [Error Responses](#error-responses)
- [Rate Limits](#rate-limits)

## Authentication

В момента API е **без authentication** - предназначен за local deployment. За production deployment, добавете JWT authentication или API keys.

## API Endpoints

Base URL: `http://localhost:5000`

### Health Check

Проверка дали API е активен.

**Endpoint:** `GET /healthz`

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-01-13T10:30:00Z",
  "version": "1.0.0"
}
```

**Status Codes:**
- `200 OK` - Service is healthy

---

### File Parsing

Парсиране на XLSX/DOCX/PDF файл.

**Endpoint:** `POST /parse`

**Content-Type:** `multipart/form-data`

**Request:**
```bash
curl -X POST http://localhost:5000/parse \
  -F "file=@document.xlsx" \
  -F "useOcr=false"
```

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| file | File | Yes | XLSX, DOCX, or PDF file (max 50MB) |
| useOcr | boolean | No | Enable OCR for scanned PDFs (default: false) |

**Response:**
```json
{
  "fileName": "document.xlsx",
  "fileType": "xlsx",
  "sheets": [
    {
      "name": "Sheet1",
      "rows": [
        {
          "rowIndex": 1,
          "cells": [
            {
              "columnIndex": 0,
              "value": "Item Name",
              "type": "String"
            }
          ]
        }
      ]
    }
  ]
}
```

**Status Codes:**
- `200 OK` - File parsed successfully
- `400 Bad Request` - Invalid file type or size
- `413 Payload Too Large` - File exceeds 50MB
- `500 Internal Server Error` - Parsing failed

---

### BoQ Extraction

Извличане на структуриран BoQ от parsнат файл чрез LLM.

**Endpoint:** `POST /extract`

**Content-Type:** `application/json`

**Request:**
```json
{
  "parsedData": {
    "fileName": "document.xlsx",
    "sheets": [...]
  }
}
```

**Response:**
```json
{
  "project": {
    "name": "Residential Building Construction",
    "contractor": "ABC Construction Ltd.",
    "location": "Sofia, Bulgaria"
  },
  "stages": [
    {
      "id": "stage-1",
      "name": "Concrete Works",
      "order": 1
    }
  ],
  "items": [
    {
      "id": "item-1",
      "stageId": "stage-1",
      "name": "Concrete C25/30",
      "quantity": 150.0,
      "unit": "м3",
      "basePrice": null
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Extraction successful
- `400 Bad Request` - Invalid parsed data
- `502 Bad Gateway` - LLM service unavailable
- `500 Internal Server Error` - Extraction failed

---

### Fuzzy Matching

Съпоставяне на BoQ позиции с ценова база.

**Endpoint:** `POST /match`

**Content-Type:** `application/json`

**Request:**
```json
{
  "boq": {
    "project": {...},
    "stages": [...],
    "items": [
      {
        "id": "item-1",
        "stageId": "stage-1",
        "name": "Concrete C25/30",
        "quantity": 150.0,
        "unit": "м3"
      }
    ]
  },
  "priceBase": [
    {
      "id": "cat-123",
      "name": "Concrete C25/30 delivered",
      "unit": "m3",
      "basePrice": 120.50
    }
  ]
}
```

**Response:**
```json
[
  {
    "boqItem": {
      "id": "item-1",
      "name": "Concrete C25/30",
      "quantity": 150.0,
      "unit": "м3"
    },
    "candidates": [
      {
        "catalogueItem": {
          "id": "cat-123",
          "name": "Concrete C25/30 delivered",
          "unit": "m3",
          "basePrice": 120.50
        },
        "score": 95.5,
        "unitMatch": true
      }
    ],
    "userSelected": null
  }
]
```

**Matching Algorithm:**
- **Exact match**: Score 100.0
- **Fuzzy match**: Levenshtein distance (normalized)
- **Unit normalization**: "м3" ↔ "m3" considered equivalent
- **Caching**: Results cached in SQLite for 30 days

**Status Codes:**
- `200 OK` - Matching successful
- `400 Bad Request` - Invalid BoQ or price base
- `500 Internal Server Error` - Matching failed

---

### LP Optimization

Linear Programming оптимизация на коефициенти.

**Endpoint:** `POST /optimize`

**Content-Type:** `application/json`

**Request:**
```json
{
  "matchedItems": [
    {
      "boqItem": {...},
      "candidates": [...],
      "userSelected": {
        "catalogueItem": {
          "id": "cat-123",
          "basePrice": 120.50
        }
      }
    }
  ],
  "stages": [
    {
      "id": "stage-1",
      "name": "Concrete Works",
      "order": 1
    }
  ],
  "lambda": 1000.0,
  "minCoeff": 0.4,
  "maxCoeff": 2.0
}
```

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| matchedItems | Array | Yes | 1-10,000 matched items |
| stages | Array | Yes | Stage definitions |
| lambda | number | Yes | L1 penalty weight (0-1,000,000) |
| minCoeff | number | Yes | Min coefficient (typically 0.4) |
| maxCoeff | number | Yes | Max coefficient (typically 2.0) |

**Response:**
```json
{
  "feasible": true,
  "objectiveValue": 12345.67,
  "coeffs": [1.15, 0.95, 1.05, 1.20],
  "stageSummaries": [
    {
      "stageId": "stage-1",
      "stageName": "Concrete Works",
      "totalBase": 18075.00,
      "totalOptimized": 19500.00,
      "avgCoeff": 1.08
    }
  ]
}
```

**Optimization Details:**
- **Solver**: Google OR-Tools GLOP
- **Objective**: Minimize L1 deviation from 1.0
- **Constraints**:
  - `minCoeff ≤ coeff[i] ≤ maxCoeff` for all items
  - Stage sum constraints (optional)
- **L1 Linearization**: `|x - 1| = pos + neg` where `x = 1 + pos - neg`

**Status Codes:**
- `200 OK` - Optimization successful
- `400 Bad Request` - Invalid request or infeasible problem
- `500 Internal Server Error` - Solver error

---

### Excel Export

Експорт на резултат като XLSX или ZIP (per stage).

**Endpoint:** `POST /export`

**Content-Type:** `application/json`

**Request:**
```json
{
  "result": {
    "feasible": true,
    "coeffs": [1.15, 0.95],
    "stageSummaries": [...]
  },
  "boq": {
    "project": {...},
    "stages": [...],
    "items": [...]
  },
  "projectName": "Residential Building",
  "splitByStage": false
}
```

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| result | Object | Yes | Optimization result |
| boq | Object | Yes | Original BoQ data |
| projectName | string | Yes | Project name for filename |
| splitByStage | boolean | No | Generate ZIP with per-stage files (default: false) |

**Response (splitByStage=false):**
- **Content-Type**: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Filename**: `{ProjectName}_КСС.xlsx`
- **Binary**: XLSX file

**Response (splitByStage=true):**
- **Content-Type**: `application/zip`
- **Filename**: `{ProjectName}_КСС_по_етапи.zip`
- **Contents**:
  - `{ProjectName}_КСС_общо.xlsx` (all items)
  - `{StageName}_КСС.xlsx` (per stage)

**Excel Format:**
| Column | Description | Formula |
|--------|-------------|---------|
| № | Row number | - |
| Наименование | Item name | - |
| Ед. | Unit | - |
| Количество | Quantity | - |
| Базова цена | Base price | - |
| Коефициент | Optimized coefficient | - |
| Цена | Final price | `BasePrice × Coefficient` |
| Стойност | Total value | `Quantity × Price` |

**Status Codes:**
- `200 OK` - Export successful
- `400 Bad Request` - Invalid data
- `500 Internal Server Error` - Export failed

---

### Observations

Logging и метрики за операции.

#### Log Operation

**Endpoint:** `POST /observations`

**Content-Type:** `application/json`

**Request:**
```json
{
  "operationType": "parse",
  "fileName": "document.xlsx",
  "fileSize": 1024000,
  "itemCount": 50,
  "success": true,
  "errorMessage": null,
  "metadata": {
    "sheets": 2,
    "usedOcr": false
  }
}
```

**Response:**
```json
{
  "id": "obs-12345",
  "timestamp": "2025-01-13T10:30:00Z"
}
```

**Status Codes:**
- `201 Created` - Observation logged
- `400 Bad Request` - Invalid observation data

#### Get Metrics

**Endpoint:** `GET /observations/metrics?since={timestamp}`

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| since | ISO8601 | No | Filter observations after this timestamp |

**Example:**
```bash
curl "http://localhost:5000/observations/metrics?since=2025-01-01T00:00:00Z"
```

**Response:**
```json
{
  "totalOperations": 150,
  "successRate": 94.67,
  "byType": {
    "parse": 50,
    "extract": 30,
    "match": 30,
    "optimize": 25,
    "export": 15
  },
  "avgFileSize": 2048000,
  "avgItemCount": 75,
  "avgDuration": 1250,
  "topErrors": [
    {
      "errorMessage": "File size exceeds limit",
      "count": 5
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Metrics retrieved
- `400 Bad Request` - Invalid timestamp format

#### Get Recent Operations

**Endpoint:** `GET /observations/recent?limit={count}`

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| limit | integer | No | Number of operations (default: 100, max: 1000) |

**Example:**
```bash
curl "http://localhost:5000/observations/recent?limit=10"
```

**Response:**
```json
[
  {
    "id": "obs-12345",
    "operationType": "optimize",
    "timestamp": "2025-01-13T10:30:00Z",
    "success": true,
    "itemCount": 150,
    "duration": 2500
  }
]
```

**Status Codes:**
- `200 OK` - Operations retrieved
- `400 Bad Request` - Invalid limit

---

## AI Gateway Endpoints

Base URL: `http://localhost:5001`

### PDF Layout Analysis

**Endpoint:** `POST /pdf/layout`

**Content-Type:** `multipart/form-data`

**Request:**
```bash
curl -X POST http://localhost:5001/pdf/layout \
  -F "file=@document.pdf" \
  -F "useOcr=true"
```

**Response:**
```json
{
  "pages": [
    {
      "pageNumber": 1,
      "textBlocks": [
        {
          "text": "Concrete C25/30",
          "x": 100,
          "y": 200,
          "width": 150,
          "height": 20
        }
      ]
    }
  ]
}
```

### XLSX Parsing

**Endpoint:** `POST /xlsx/parse`

**Content-Type:** `multipart/form-data`

**Response:** See `/parse` endpoint (same structure)

### DOCX Parsing

**Endpoint:** `POST /docx/parse`

**Content-Type:** `multipart/form-data`

**Response:**
```json
{
  "paragraphs": [
    {
      "text": "Project: Residential Building",
      "style": "Heading1"
    }
  ],
  "tables": [
    {
      "rows": [
        {
          "cells": ["Item", "Quantity", "Unit"]
        }
      ]
    }
  ]
}
```

---

## Error Responses

Всички endpoints връщат consistent error format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "File size exceeds 50MB limit",
    "details": {
      "fileName": "large-document.pdf",
      "fileSize": 52428800,
      "maxSize": 52428800
    }
  }
}
```

**Error Codes:**
| Code | HTTP Status | Description |
|------|-------------|-------------|
| VALIDATION_ERROR | 400 | Input validation failed |
| FILE_TOO_LARGE | 413 | File exceeds size limit |
| UNSUPPORTED_FORMAT | 400 | File format not supported |
| PARSING_ERROR | 500 | File parsing failed |
| LLM_ERROR | 502 | LLM service error |
| OPTIMIZATION_INFEASIBLE | 400 | LP problem has no solution |
| TIMEOUT | 408 | Request timeout (5 minutes) |
| INTERNAL_ERROR | 500 | Unexpected server error |

---

## Rate Limits

В момента **няма rate limits** за local deployment. За production:

**Препоръки:**
- **Parse/Extract**: 10 requests/minute per IP
- **Optimize**: 5 requests/minute per IP
- **Export**: 20 requests/minute per IP
- **Observations**: 100 requests/minute per IP

**Implementation:**
- Use ASP.NET Core Rate Limiting middleware
- Redis за distributed rate limiting (ако multi-instance)

---

## SDK Examples

### C# Client

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// Parse file
using var content = new MultipartFormDataContent();
content.Add(new StreamContent(File.OpenRead("doc.xlsx")), "file", "doc.xlsx");
var parseResponse = await client.PostAsync("/parse", content);
var parsed = await parseResponse.Content.ReadFromJsonAsync<ParsedData>();

// Match
var matchRequest = new { boq = myBoq, priceBase = myPriceBase };
var matchResponse = await client.PostAsJsonAsync("/match", matchRequest);
var matched = await matchResponse.Content.ReadFromJsonAsync<List<MatchedItem>>();
```

### JavaScript/TypeScript

```typescript
const apiClient = {
  baseUrl: 'http://localhost:5000',
  
  async parse(file: File, useOcr = false) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('useOcr', useOcr.toString());
    
    const response = await fetch(`${this.baseUrl}/parse`, {
      method: 'POST',
      body: formData,
    });
    
    return response.json();
  },
  
  async optimize(request: OptimizationRequest) {
    const response = await fetch(`${this.baseUrl}/optimize`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    
    return response.json();
  },
};
```

### Python

```python
import requests

class FirstMakeClient:
    def __init__(self, base_url="http://localhost:5000"):
        self.base_url = base_url
    
    def parse(self, file_path, use_ocr=False):
        with open(file_path, 'rb') as f:
            files = {'file': f}
            data = {'useOcr': use_ocr}
            response = requests.post(f"{self.base_url}/parse", files=files, data=data)
            return response.json()
    
    def optimize(self, matched_items, stages, lambda_val=1000.0):
        payload = {
            'matchedItems': matched_items,
            'stages': stages,
            'lambda': lambda_val,
            'minCoeff': 0.4,
            'maxCoeff': 2.0
        }
        response = requests.post(f"{self.base_url}/optimize", json=payload)
        return response.json()
```

---

## Versioning

API използва **semantic versioning** (v1.0.0). Future breaking changes ще имат URL path versioning:

- `http://localhost:5000/v1/parse`
- `http://localhost:5000/v2/parse`

За сега всички endpoints са на root path (`/`).

---

**Last Updated:** 2025-01-13
