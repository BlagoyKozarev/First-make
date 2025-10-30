import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '../../test/test-utils';
import MatchPage from '../MatchPage';
import * as api from '../../lib/api';

// Mock the API module
vi.mock('../../lib/api', () => ({
  triggerMatching: vi.fn(),
  getUnmatchedCandidates: vi.fn(),
  overrideMatch: vi.fn(),
}));

// Mock react-router-dom navigation
const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe('MatchPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
  });

  it('redirects to home if no projectId in session', () => {
    render(<MatchPage />);

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('triggers matching automatically on mount with valid projectId', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 80,
      unmatchedItems: 20,
      uniquePositions: 95,
      averageScore: 0.85,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue([]);

    render(<MatchPage />);

    await waitFor(() => {
      expect(api.triggerMatching).toHaveBeenCalledWith('test-project-123');
    });
  });

  it('displays match statistics after successful matching', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 85,
      unmatchedItems: 15,
      uniquePositions: 98,
      averageScore: 0.90,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue([]);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText(/85%/)).toBeInTheDocument(); // Match percentage
    });
  });

  it('fetches unmatched candidates when there are unmatched items', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 80,
      unmatchedItems: 20,
      uniquePositions: 95,
      averageScore: 0.85,
    };

    const mockCandidates: api.UnifiedCandidate[] = [
      {
        unifiedKey: 'key-1',
        itemName: 'Тест Позиция 1',
        itemUnit: 'м²',
        occurrenceCount: 3,
        topCandidates: [
          { name: 'Кандидат 1', unit: 'м²', price: 10.50, score: 0.75 },
          { name: 'Кандидат 2', unit: 'м²', price: 12.00, score: 0.65 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(api.getUnmatchedCandidates).toHaveBeenCalledWith('test-project-123');
      expect(screen.getByText('Тест Позиция 1')).toBeInTheDocument();
    });
  });

  it('filters candidates based on search term', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 70,
      unmatchedItems: 30,
      uniquePositions: 92,
      averageScore: 0.80,
    };

    const mockCandidates: api.UnifiedCandidate[] = [
      {
        unifiedKey: 'key-1',
        itemName: 'Тухлена зидария',
        itemUnit: 'м³',
        occurrenceCount: 2,
        topCandidates: [],
      },
      {
        unifiedKey: 'key-2',
        itemName: 'Бетонова подготовка',
        itemUnit: 'м³',
        occurrenceCount: 1,
        topCandidates: [],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Тухлена зидария')).toBeInTheDocument();
      expect(screen.getByText('Бетонова подготовка')).toBeInTheDocument();
    });

    // Type in search box
    const searchInput = screen.getByPlaceholderText(/Търсене/i);
    fireEvent.change(searchInput, { target: { value: 'тухл' } });

    // Should only show matching candidate
    expect(screen.getByText('Тухлена зидария')).toBeInTheDocument();
    expect(screen.queryByText('Бетонова подготовка')).not.toBeInTheDocument();
  });

  it('displays error message when matching fails', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    vi.mocked(api.triggerMatching).mockRejectedValue(new Error('API Error'));

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText(/Грешка при съпоставяне/i)).toBeInTheDocument();
    });
  });

  it('navigates to iteration page when continue button is clicked', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 100,
      unmatchedItems: 0,
      uniquePositions: 100,
      averageScore: 0.95,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);

    render(<MatchPage />);

    // Wait for stats to load
    await waitFor(() => {
      expect(screen.getByText('100%')).toBeInTheDocument();
    });

    const continueButton = screen.getByRole('button', { name: /Към Оптимизация/i });
    fireEvent.click(continueButton);

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/iteration');
    });
  });

  it('shows navigate back button', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 50,
      matchedItems: 50,
      unmatchedItems: 0,
      uniquePositions: 50,
      averageScore: 1.0,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);

    render(<MatchPage />);

    await waitFor(() => {
      const backButton = screen.getByRole('button', { name: /Назад/i });
      expect(backButton).toBeInTheDocument();
    });
  });
});
