import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import ExportPage from '../ExportPage';
import * as api from '../../lib/api';

// Mock API module
vi.mock('../../lib/api', () => ({
  exportResults: vi.fn(),
  getLatestIteration: vi.fn(),
  getProject: vi.fn(),
}));

// Mock useNavigate
const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe('ExportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();

    // Default successful mock responses
    vi.mocked(api.getLatestIteration).mockResolvedValue({
      iterationId: 'iter-1',
      timestamp: '2024-01-15T10:00:00Z',
      overallGap: 500.50,
      totalProposed: 50000,
      totalForecast: 50500.50,
      stageBreakdown: [
        { stage: 'Етап 1', gap: 200, proposed: 10000, forecast: 10200 },
        { stage: 'Етап 2', gap: 300.50, proposed: 15000, forecast: 15300.50 },
      ],
      fileBreakdown: [],
      solverTimeMs: 1200,
    });

    vi.mocked(api.getProject).mockResolvedValue({
      projectId: 'test-project-123',
      metadata: {
        objectName: 'Тестов обект',
        employee: 'Иван Иванов',
        date: '2024-01-15',
      },
      kssFilesCount: 3,
      ukazaniaFilesCount: 1,
      priceBaseFilesCount: 1,
      hasTemplate: true,
      hasMatchingResults: true,
      hasOptimizationResults: true,
      createdAt: '2024-01-15T09:00:00Z',
    });
  });

  it('redirects to home if no projectId in session', () => {
    render(<ExportPage />);
    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('renders page heading and summary when data loads', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /Експорт на Резултати/i, level: 1 })).toBeInTheDocument();
    });

    expect(screen.getByText(/Тестов обект/i)).toBeInTheDocument();
    expect(screen.getByText(/Иван Иванов/i)).toBeInTheDocument();
  });

  it('displays optimization summary with correct data', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByText(/\+500\.50 лв/i)).toBeInTheDocument();
    });

    // Check for number of stages
    expect(screen.getByText('2')).toBeInTheDocument();
    // Check for KSS files count
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('displays stage breakdown table', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByText('Етап 1')).toBeInTheDocument();
    });

    expect(screen.getByText('Етап 2')).toBeInTheDocument();
    expect(screen.getByText(/10000\.00 лв/i)).toBeInTheDocument();
    expect(screen.getByText(/15000\.00 лв/i)).toBeInTheDocument();
  });

  it('exports ZIP file when export button is clicked', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockBlob = new Blob(['test'], { type: 'application/zip' });
    vi.mocked(api.exportResults).mockResolvedValue(mockBlob);

    // Mock URL methods
    const mockUrl = 'blob:mock-url';
    global.URL.createObjectURL = vi.fn(() => mockUrl);
    global.URL.revokeObjectURL = vi.fn();

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Изтегли ZIP архив/i })).toBeInTheDocument();
    });

    const exportButton = screen.getByRole('button', { name: /Изтегли ZIP архив/i });
    fireEvent.click(exportButton);

    await waitFor(() => {
      expect(api.exportResults).toHaveBeenCalledWith('test-project-123');
    });

    expect(global.URL.createObjectURL).toHaveBeenCalledWith(mockBlob);
  });

  it('displays error message when export fails', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    vi.mocked(api.exportResults).mockRejectedValue({
      response: { data: { message: 'Export error' } },
    });

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Изтегли ZIP архив/i })).toBeInTheDocument();
    });

    const exportButton = screen.getByRole('button', { name: /Изтегли ZIP архив/i });
    fireEvent.click(exportButton);

    await waitFor(() => {
      expect(screen.getByText(/Export error/i)).toBeInTheDocument();
    });
  });

  it('navigates back to iteration page when back button clicked', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Назад към оптимизация/i })).toBeInTheDocument();
    });

    const backButton = screen.getByRole('button', { name: /Назад към оптимизация/i });
    fireEvent.click(backButton);

    expect(mockNavigate).toHaveBeenCalledWith('/iteration');
  });

  it('clears session and navigates to home when new project button clicked', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');
    sessionStorage.setItem('otherData', 'test-value');

    render(<ExportPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Нов Проект/i })).toBeInTheDocument();
    });

    const newProjectButton = screen.getByRole('button', { name: /Нов Проект/i });
    fireEvent.click(newProjectButton);

    expect(sessionStorage.getItem('currentProjectId')).toBeNull();
    expect(sessionStorage.getItem('otherData')).toBeNull();
    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('displays loading state while data is being fetched', () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    // Make API calls return pending promise
    vi.mocked(api.getLatestIteration).mockReturnValue(new Promise(() => {}));
    vi.mocked(api.getProject).mockReturnValue(new Promise(() => {}));

    render(<ExportPage />);

    expect(screen.getByText(/Зареждане\.\.\./i)).toBeInTheDocument();
  });
});
