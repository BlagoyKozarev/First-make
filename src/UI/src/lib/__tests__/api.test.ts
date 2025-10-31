import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Use vi.hoisted to ensure mocks are initialized before imports
const { mockApiInstance } = vi.hoisted(() => {
  const mockPost = vi.fn();
  const mockGet = vi.fn();
  const mockDelete = vi.fn();
  
  const mockApiInstance = {
    post: mockPost,
    get: mockGet,
    delete: mockDelete,
  };
  
  return { mockApiInstance, mockPost, mockGet, mockDelete };
});

// Mock axios module
vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => mockApiInstance),
  },
}));

import {
  createProject,
  getProject,
  deleteProject,
  uploadKssFiles,
  uploadUkazaniaFiles,
  uploadPriceBaseFiles,
  uploadTemplateFile,
  triggerMatching,
  getUnmatchedCandidates,
  overrideMatch,
  runOptimization,
  getLatestIteration,
  exportResults,
  previewExportFile,
  type ProjectMetadata,
  type ProjectSession,
  type MatchStatistics,
  type UnifiedCandidate,
  type IterationResult,
} from '../api';

describe('API Client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('Project Management', () => {
    it('creates a new project', async () => {
      const metadata: ProjectMetadata = {
        objectName: 'Test Project',
        employee: 'John Doe',
        date: '2024-01-01',
      };

      const mockSession: ProjectSession = {
        projectId: 'proj-123',
        metadata,
        kssFilesCount: 0,
        ukazaniaFilesCount: 0,
        priceBaseFilesCount: 0,
        hasTemplate: false,
        hasMatchingResults: false,
        hasOptimizationResults: false,
        createdAt: '2024-01-01T00:00:00Z',
      };

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockSession });

      const result = await createProject(metadata);

      expect(result).toEqual(mockSession);
      expect(mockApiInstance.post).toHaveBeenCalledWith('/projects', metadata);
    });

    it('gets project by ID', async () => {
      const mockSession: ProjectSession = {
        projectId: 'proj-123',
        metadata: {
          objectName: 'Test',
          employee: 'John',
          date: '2024-01-01',
        },
        kssFilesCount: 2,
        ukazaniaFilesCount: 1,
        priceBaseFilesCount: 1,
        hasTemplate: true,
        hasMatchingResults: false,
        hasOptimizationResults: false,
        createdAt: '2024-01-01T00:00:00Z',
      };

      (mockApiInstance.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockSession });

      const result = await getProject('proj-123');

      expect(result).toEqual(mockSession);
      expect(mockApiInstance.get).toHaveBeenCalledWith('/projects/proj-123');
    });

    it('deletes a project', async () => {
      (mockApiInstance.delete as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await deleteProject('proj-123');

      expect(mockApiInstance.delete).toHaveBeenCalledWith('/projects/proj-123');
    });
  });

  describe('File Upload', () => {
    it('uploads KSS files', async () => {
      const file1 = new File(['content1'], 'file1.xlsx');
      const file2 = new File(['content2'], 'file2.xlsx');
      const files = [file1, file2];

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await uploadKssFiles('proj-123', files);

      expect(mockApiInstance.post).toHaveBeenCalledWith(
        '/projects/proj-123/files/kss',
        expect.any(FormData),
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
    });

    it('uploads Ukazania files', async () => {
      const file = new File(['content'], 'ukazania.xlsx');

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await uploadUkazaniaFiles('proj-123', [file]);

      expect(mockApiInstance.post).toHaveBeenCalledWith(
        '/projects/proj-123/files/ukazania',
        expect.any(FormData),
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
    });

    it('uploads PriceBase files', async () => {
      const file = new File(['content'], 'pricebase.xlsx');

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await uploadPriceBaseFiles('proj-123', [file]);

      expect(mockApiInstance.post).toHaveBeenCalledWith(
        '/projects/proj-123/files/pricebase',
        expect.any(FormData),
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
    });

    it('uploads template file', async () => {
      const file = new File(['content'], 'template.xlsx');

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await uploadTemplateFile('proj-123', file);

      expect(mockApiInstance.post).toHaveBeenCalledWith(
        '/projects/proj-123/files/template',
        expect.any(FormData),
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
    });
  });

  describe('Matching', () => {
    it('triggers matching process', async () => {
      const mockStats: MatchStatistics = {
        totalItems: 100,
        matchedItems: 85,
        unmatchedItems: 15,
        uniquePositions: 50,
        averageScore: 0.92,
      };

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockStats });

      const result = await triggerMatching('proj-123');

      expect(result).toEqual(mockStats);
      expect(mockApiInstance.post).toHaveBeenCalledWith('/projects/proj-123/match');
    });

    it('gets unmatched candidates', async () => {
      const mockCandidates: UnifiedCandidate[] = [
        {
          unifiedKey: 'key1',
          itemName: 'Item 1',
          itemUnit: 'm2',
          occurrenceCount: 5,
          topCandidates: [
            { name: 'Candidate 1', unit: 'm2', price: 10.5, score: 0.85 },
          ],
        },
      ];

      (mockApiInstance.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockCandidates });

      const result = await getUnmatchedCandidates('proj-123');

      expect(result).toEqual(mockCandidates);
      expect(mockApiInstance.get).toHaveBeenCalledWith('/projects/proj-123/match/candidates');
    });

    it('overrides a match', async () => {
      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({});

      await overrideMatch('proj-123', 'unified-key-1', 'Price Entry Name');

      expect(mockApiInstance.post).toHaveBeenCalledWith('/projects/proj-123/match/override', {
        unifiedKey: 'unified-key-1',
        priceEntryName: 'Price Entry Name',
      });
    });
  });

  describe('Optimization', () => {
    it('runs optimization', async () => {
      const mockResult: IterationResult = {
        iterationId: 'iter-1',
        timestamp: '2024-01-01T00:00:00Z',
        overallGap: 5000,
        totalProposed: 100000,
        totalForecast: 95000,
        stageBreakdown: [
          { stage: 'Stage 1', gap: 2000, proposed: 50000, forecast: 48000 },
        ],
        fileBreakdown: [
          {
            fileName: 'file1.xlsx',
            totalProposed: 50000,
            stageGaps: { 'Stage 1': 2000 },
          },
        ],
        solverTimeMs: 1250,
      };

      (mockApiInstance.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResult });

      const result = await runOptimization('proj-123');

      expect(result).toEqual(mockResult);
      expect(mockApiInstance.post).toHaveBeenCalledWith('/projects/proj-123/optimize');
    });

    it('gets latest iteration', async () => {
      const mockResult: IterationResult = {
        iterationId: 'iter-1',
        timestamp: '2024-01-01T00:00:00Z',
        overallGap: 5000,
        totalProposed: 100000,
        totalForecast: 95000,
        stageBreakdown: [],
        fileBreakdown: [],
        solverTimeMs: 1250,
      };

      (mockApiInstance.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResult });

      const result = await getLatestIteration('proj-123');

      expect(result).toEqual(mockResult);
      expect(mockApiInstance.get).toHaveBeenCalledWith('/projects/proj-123/iterations/latest');
    });
  });

  describe('Export', () => {
    it('exports results as blob', async () => {
      const mockBlob = new Blob(['zip content'], { type: 'application/zip' });

      (mockApiInstance.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockBlob });

      const result = await exportResults('proj-123');

      expect(result).toEqual(mockBlob);
      expect(mockApiInstance.get).toHaveBeenCalledWith('/projects/proj-123/export', {
        responseType: 'blob',
      });
    });

    it('previews export file as blob', async () => {
      const mockBlob = new Blob(['file content'], { type: 'application/pdf' });

      (mockApiInstance.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockBlob });

      const result = await previewExportFile('proj-123', 'file-1');

      expect(result).toEqual(mockBlob);
      expect(mockApiInstance.get).toHaveBeenCalledWith('/projects/proj-123/export/preview/file-1', {
        responseType: 'blob',
      });
    });
  });
});

