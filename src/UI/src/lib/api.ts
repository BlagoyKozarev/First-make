// API client for FirstMake Agent
const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';
const AI_GATEWAY_BASE = import.meta.env.VITE_AI_GATEWAY_URL || 'http://localhost:5001';

export interface BoqItem {
  stage: string;
  name: string;
  unit: string;
  quantity: number;
  evidence?: {
    sourceFile: string;
    page?: number;
    cell?: string;
  };
}

export interface BoqData {
  projectName: string;
  projectCode?: string;
  stages: {
    code: string;
    name: string;
    forecast: number;
  }[];
  items: BoqItem[];
}

export interface MatchedItem {
  boqItemName: string;
  candidates: {
    catalogueItemName: string;
    basePrice: number;
    unit: string;
    score: number;
  }[];
}

export interface OptimizationRequest {
  boq: BoqData;
  priceBase: any[];
  matches: Record<string, string>;
  lambda?: number;
  coeffBounds?: { min: number; max: number };
}

export interface OptimizationResult {
  coefficients: Record<string, number>;
  stageSummaries: {
    stageCode: string;
    totalCost: number;
    forecast: number;
    deviation: number;
  }[];
  objectiveValue: number;
  solverStatus: string;
}

export interface OperationObservation {
  operationType: string;
  success: boolean;
  durationMs: number;
  timestamp?: string;
  sourceFileName?: string;
  inputHash?: string;
  errorMessage?: string;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  metadata?: Record<string, any>;
}

export interface ObservationMetrics {
  totalOperations: number;
  successfulOperations: number;
  failedOperations: number;
  duplicateOperations: number;
  averageDurationMs: number;
  operationsByType: Record<string, number>;
  averageMatchScore?: number;
  averageMatchCandidates?: number;
  averageOptimizationObjective?: number;
  optimizationSuccessRate?: number;
  since: string;
  until: string;
}

// File upload and parsing
export async function uploadAndParse(file: File): Promise<any> {
  const formData = new FormData();
  formData.append('file', file);

  // Try deterministic parsers first
  const ext = file.name.split('.').pop()?.toLowerCase();
  
  if (ext === 'xlsx') {
    const response = await fetch(`${AI_GATEWAY_BASE}/xlsx/parse`, {
      method: 'POST',
      body: formData,
    });
    return response.json();
  } else if (ext === 'docx') {
    const response = await fetch(`${AI_GATEWAY_BASE}/docx/parse`, {
      method: 'POST',
      body: formData,
    });
    return response.json();
  } else if (ext === 'pdf') {
    const response = await fetch(`${AI_GATEWAY_BASE}/pdf/layout`, {
      method: 'POST',
      body: formData,
    });
    return response.json();
  }
  
  throw new Error(`Unsupported file type: ${ext}`);
}

// Extract BoQ from parsed data using LLM
export async function extractBoq(parsedData: any): Promise<BoqData> {
  const response = await fetch(`${API_BASE}/extract`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(parsedData),
  });
  
  if (!response.ok) {
    throw new Error(`Extraction failed: ${response.statusText}`);
  }
  
  return response.json();
}

// Fuzzy match BoQ items to price catalogue
export async function matchItems(boq: BoqData, priceBase: any[]): Promise<MatchedItem[]> {
  const response = await fetch(`${API_BASE}/match`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ boq, priceBase }),
  });
  
  if (!response.ok) {
    throw new Error(`Matching failed: ${response.statusText}`);
  }
  
  return response.json();
}

// Run LP optimization
export async function optimize(request: OptimizationRequest): Promise<OptimizationResult> {
  const response = await fetch(`${API_BASE}/optimize`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  
  if (!response.ok) {
    throw new Error(`Optimization failed: ${response.statusText}`);
  }
  
  return response.json();
}

// Export results to XLSX or ZIP
export async function exportResults(
  result: OptimizationResult, 
  boq: BoqData,
  projectName?: string,
  splitByStage: boolean = false
): Promise<Blob> {
  const response = await fetch(`${API_BASE}/export`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      result,
      boq,
      projectName: projectName || boq.projectName,
      splitByStage
    }),
  });
  
  if (!response.ok) {
    throw new Error(`Export failed: ${response.statusText}`);
  }
  
  return response.blob();
}

// Log operation observation
export async function logObservation(observation: OperationObservation): Promise<{
  observationId: string;
  isDuplicate: boolean;
  originalSessionId?: string;
}> {
  const response = await fetch(`${API_BASE}/observations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(observation),
  });
  
  if (!response.ok) {
    throw new Error(`Failed to log observation: ${response.statusText}`);
  }
  
  return response.json();
}

// Get metrics
export async function getMetrics(since?: Date): Promise<ObservationMetrics> {
  const url = new URL(`${API_BASE}/observations/metrics`);
  if (since) {
    url.searchParams.set('since', since.toISOString());
  }
  
  const response = await fetch(url.toString());
  
  if (!response.ok) {
    throw new Error(`Failed to get metrics: ${response.statusText}`);
  }
  
  return response.json();
}

// Get recent observations
export async function getRecentObservations(limit: number = 100): Promise<OperationObservation[]> {
  const url = new URL(`${API_BASE}/observations/recent`);
  url.searchParams.set('limit', limit.toString());
  
  const response = await fetch(url.toString());
  
  if (!response.ok) {
    throw new Error(`Failed to get observations: ${response.statusText}`);
  }
  
  return response.json();
}

// Health checks
export async function checkApiHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE}/healthz`);
    return response.ok;
  } catch {
    return false;
  }
}

export async function checkAiGatewayHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${AI_GATEWAY_BASE}/healthz`);
    return response.ok;
  } catch {
    return false;
  }
}
