// V2.0 API Client for Multi-file КСС Processing
import axios from 'axios';

// API Base URL (proxied through Vite dev server)
const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Types matching backend models
export interface ProjectMetadata {
  objectName: string;
  employee: string;
  date: string;
}

export interface ProjectSession {
  projectId: string;
  metadata: ProjectMetadata;
  kssFilesCount: number;
  ukazaniaFilesCount: number;
  priceBaseFilesCount: number;
  hasTemplate: boolean;
  hasMatchingResults: boolean;
  hasOptimizationResults: boolean;
  createdAt: string;
}

export interface MatchResult {
  isMatched: boolean;
  priceEntry?: {
    name: string;
    unit: string;
    price: number;
  };
  score?: number;
}

export interface UnifiedCandidate {
  unifiedKey: string;
  itemName: string;
  itemUnit: string;
  occurrenceCount: number;
  topCandidates: Array<{
    name: string;
    unit: string;
    price: number;
    score: number;
  }>;
}

export interface MatchStatistics {
  totalItems: number;
  matchedItems: number;
  unmatchedItems: number;
  uniquePositions: number;
  averageScore: number;
}

export interface IterationResult {
  iterationId: string;
  timestamp: string;
  overallGap: number;
  totalProposed: number;
  totalForecast: number;
  stageBreakdown: Array<{
    stage: string;
    gap: number;
    proposed: number;
    forecast: number;
  }>;
  fileBreakdown: Array<{
    fileName: string;
    totalProposed: number;
    stageGaps: Record<string, number>;
  }>;
  solverTimeMs: number;
}

// Project Management
export const createProject = async (metadata: ProjectMetadata): Promise<ProjectSession> => {
  const response = await api.post<ProjectSession>('/projects', metadata);
  return response.data;
};

export const getProject = async (projectId: string): Promise<ProjectSession> => {
  const response = await api.get<ProjectSession>(`/projects/${projectId}`);
  return response.data;
};

export const deleteProject = async (projectId: string): Promise<void> => {
  await api.delete(`/projects/${projectId}`);
};

// File Upload
export const uploadKssFiles = async (projectId: string, files: File[]): Promise<void> => {
  const formData = new FormData();
  files.forEach((file) => formData.append('files', file));
  await api.post(`/projects/${projectId}/files/kss`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
};

export const uploadUkazaniaFiles = async (projectId: string, files: File[]): Promise<void> => {
  const formData = new FormData();
  files.forEach((file) => formData.append('files', file));
  await api.post(`/projects/${projectId}/files/ukazania`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
};

export const uploadPriceBaseFiles = async (projectId: string, files: File[]): Promise<void> => {
  const formData = new FormData();
  files.forEach((file) => formData.append('files', file));
  await api.post(`/projects/${projectId}/files/pricebase`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
};

export const uploadTemplateFile = async (projectId: string, file: File): Promise<void> => {
  const formData = new FormData();
  formData.append('file', file);
  await api.post(`/projects/${projectId}/files/template`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
};

// Matching
export const triggerMatching = async (projectId: string): Promise<MatchStatistics> => {
  const response = await api.post<MatchStatistics>(`/projects/${projectId}/match`);
  return response.data;
};

export const getUnmatchedCandidates = async (projectId: string): Promise<UnifiedCandidate[]> => {
  const response = await api.get<UnifiedCandidate[]>(`/projects/${projectId}/match/candidates`);
  return response.data;
};

export const overrideMatch = async (
  projectId: string,
  unifiedKey: string,
  priceEntryName: string
): Promise<void> => {
  await api.post(`/projects/${projectId}/match/override`, {
    unifiedKey,
    priceEntryName,
  });
};

// Optimization
export const runOptimization = async (projectId: string): Promise<IterationResult> => {
  const response = await api.post<IterationResult>(`/projects/${projectId}/optimize`);
  return response.data;
};

export const getLatestIteration = async (projectId: string): Promise<IterationResult> => {
  const response = await api.get<IterationResult>(`/projects/${projectId}/iterations/latest`);
  return response.data;
};

// Export
export const exportResults = async (projectId: string): Promise<Blob> => {
  const response = await api.get(`/projects/${projectId}/export`, {
    responseType: 'blob',
  });
  return response.data;
};

export const previewExportFile = async (projectId: string, fileId: string): Promise<Blob> => {
  const response = await api.get(`/projects/${projectId}/export/preview/${fileId}`, {
    responseType: 'blob',
  });
  return response.data;
};

export default api;
