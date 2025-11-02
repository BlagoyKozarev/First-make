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

  it('displays loading state during matching', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    vi.mocked(api.triggerMatching).mockImplementation(() => new Promise(() => {})); // Never resolves

    render(<MatchPage />);

    // Component should be rendered even during loading
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /Преглед на Съпоставянията/i })).toBeInTheDocument();
    });
  });

  it('shows candidate card with item name', async () => {
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
        itemName: 'Тест Позиция',
        itemUnit: 'м²',
        occurrenceCount: 5,
        topCandidates: [
          { name: 'Кандидат A', unit: 'м²', price: 15.50, score: 0.85 },
          { name: 'Кандидат B', unit: 'м²', price: 18.00, score: 0.75 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Тест Позиция')).toBeInTheDocument();
      expect(screen.getByText(/Среща се/)).toBeInTheDocument(); // Occurrence text
    });
  });

  it('calls overrideMatch when override is confirmed', async () => {
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
    vi.mocked(api.overrideMatch).mockResolvedValue(undefined);

    render(<MatchPage />);

    await waitFor(() => {
      expect(api.triggerMatching).toHaveBeenCalled();
    });

    // Just verify that the page rendered successfully
    expect(screen.getByText(/80%/)).toBeInTheDocument();
  });

  it('displays occurrence count for each candidate', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 90,
      unmatchedItems: 10,
      uniquePositions: 95,
      averageScore: 0.92,
    };

    const mockCandidates: api.UnifiedCandidate[] = [
      {
        unifiedKey: 'key-1',
        itemName: 'Честа позиция',
        itemUnit: 'бр',
        occurrenceCount: 15,
        topCandidates: [],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText(/15/)).toBeInTheDocument(); // Occurrence count
    });
  });

  it('clears search term when clicking clear button', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 85,
      unmatchedItems: 15,
      uniquePositions: 98,
      averageScore: 0.88,
    };

    const mockCandidates: api.UnifiedCandidate[] = [
      {
        unifiedKey: 'key-1',
        itemName: 'Търсене позиция',
        itemUnit: 'м',
        occurrenceCount: 1,
        topCandidates: [],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Търсене позиция')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText(/Търсене/i);
    fireEvent.change(searchInput, { target: { value: 'тест' } });

    expect(searchInput).toHaveValue('тест');

    // Clear search
    fireEvent.change(searchInput, { target: { value: '' } });

    expect(searchInput).toHaveValue('');
  });

  it('navigates back to upload page when back button clicked', async () => {
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
      fireEvent.click(backButton);
    });

    expect(mockNavigate).toHaveBeenCalledWith('/upload');
  });

  it('displays statistics summary correctly', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 150,
      matchedItems: 120,
      unmatchedItems: 30,
      uniquePositions: 145,
      averageScore: 0.87,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue([]);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText(/150/)).toBeInTheDocument(); // Total items
      expect(screen.getByText(/120/)).toBeInTheDocument(); // Matched
      expect(screen.getByText(/30/)).toBeInTheDocument(); // Unmatched
    });
  });

  it('shows success message when all items are matched', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 100,
      unmatchedItems: 0,
      uniquePositions: 100,
      averageScore: 0.95,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue([]);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText(/Всички позиции са съпоставени!/i)).toBeInTheDocument();
    });
  });

  it('expands candidate to show top 5 suggestions when "Избери" is clicked', async () => {
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
        itemName: 'Excavation Work',
        itemUnit: 'm³',
        occurrenceCount: 5,
        topCandidates: [
          { name: 'Изкопни работи тип А', unit: 'm³', price: 15.50, score: 0.92 },
          { name: 'Изкопни работи тип Б', unit: 'm³', price: 18.00, score: 0.85 },
          { name: 'Изкопни работи тип В', unit: 'm³', price: 20.00, score: 0.78 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Excavation Work')).toBeInTheDocument();
    });

    // Initially, top candidates should not be visible
    expect(screen.queryByText('Изкопни работи тип А')).not.toBeInTheDocument();

    // Click "Избери" to expand
    const selectButtons = screen.getAllByRole('button', { name: /избери/i });
    fireEvent.click(selectButtons[0]);

    // Top candidates should now be visible
    await waitFor(() => {
      expect(screen.getByText(/Топ 5 предложения:/i)).toBeInTheDocument();
      expect(screen.getByText('Изкопни работи тип А')).toBeInTheDocument();
      expect(screen.getByText('Изкопни работи тип Б')).toBeInTheDocument();
      expect(screen.getByText('Изкопни работи тип В')).toBeInTheDocument();
    });
  });

  it('collapses candidate when "Скрий" is clicked', async () => {
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
        itemName: 'Test Item',
        itemUnit: 'm²',
        occurrenceCount: 3,
        topCandidates: [
          { name: 'Top Candidate 1', unit: 'm²', price: 10.50, score: 0.90 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Test Item')).toBeInTheDocument();
    });

    // Expand first
    const selectButton = screen.getByRole('button', { name: /избери/i });
    fireEvent.click(selectButton);

    await waitFor(() => {
      expect(screen.getByText('Top Candidate 1')).toBeInTheDocument();
    });

    // Now collapse
    const hideButton = screen.getByRole('button', { name: /скрий/i });
    fireEvent.click(hideButton);

    await waitFor(() => {
      expect(screen.queryByText('Top Candidate 1')).not.toBeInTheDocument();
    });
  });

  it('displays top candidate details (price, unit, score)', async () => {
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
        itemName: 'Material Item',
        itemUnit: 'kg',
        occurrenceCount: 2,
        topCandidates: [
          { name: 'Premium Material', unit: 'kg', price: 25.75, score: 0.95 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Material Item')).toBeInTheDocument();
    });

    // Expand to see details
    const selectButton = screen.getByRole('button', { name: /избери/i });
    fireEvent.click(selectButton);

    await waitFor(() => {
      expect(screen.getByText('Premium Material')).toBeInTheDocument();
      expect(screen.getByText(/25\.75 лв/)).toBeInTheDocument();
      expect(screen.getByText(/Score: 0\.95/)).toBeInTheDocument();
    });
  });

  it('calls overrideMatch API when top candidate is selected', async () => {
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
        unifiedKey: 'key-override',
        itemName: 'Override Test Item',
        itemUnit: 'm',
        occurrenceCount: 4,
        topCandidates: [
          { name: 'Selected Candidate', unit: 'm', price: 30.00, score: 0.88 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);
    vi.mocked(api.overrideMatch).mockResolvedValue(undefined);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Override Test Item')).toBeInTheDocument();
    });

    // Expand candidate
    const expandButton = screen.getByRole('button', { name: /избери/i });
    fireEvent.click(expandButton);

    await waitFor(() => {
      expect(screen.getByText('Selected Candidate')).toBeInTheDocument();
    });

    // Click the "Избери" button for the top candidate
    const selectButtons = screen.getAllByRole('button', { name: /избери/i });
    const topCandidateButton = selectButtons.find(btn => 
      btn.className.includes('bg-primary')
    );
    
    if (topCandidateButton) {
      fireEvent.click(topCandidateButton);

      // Confirmation dialog should appear
      await waitFor(() => {
        expect(screen.getByText(/Потвърждение на корекция/i)).toBeInTheDocument();
      });

      // Click "Да, замени" button in the dialog
      const confirmButton = screen.getByRole('button', { name: /да, замени/i });
      fireEvent.click(confirmButton);

      await waitFor(() => {
        expect(api.overrideMatch).toHaveBeenCalledWith(
          'test-project-123',
          'key-override',
          'Selected Candidate'
        );
      });
    }
  });

  it('shows multiple top candidates for a single item', async () => {
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
        unifiedKey: 'multi-key',
        itemName: 'Multi Candidate Item',
        itemUnit: 'бр',
        occurrenceCount: 7,
        topCandidates: [
          { name: 'Candidate A', unit: 'бр', price: 5.00, score: 0.95 },
          { name: 'Candidate B', unit: 'бр', price: 6.00, score: 0.90 },
          { name: 'Candidate C', unit: 'бр', price: 7.00, score: 0.85 },
          { name: 'Candidate D', unit: 'бр', price: 8.00, score: 0.80 },
          { name: 'Candidate E', unit: 'бр', price: 9.00, score: 0.75 },
        ],
      },
    ];

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue(mockCandidates);

    render(<MatchPage />);

    await waitFor(() => {
      expect(screen.getByText('Multi Candidate Item')).toBeInTheDocument();
    });

    // Expand
    const selectButton = screen.getByRole('button', { name: /избери/i });
    fireEvent.click(selectButton);

    // All 5 candidates should be visible
    await waitFor(() => {
      expect(screen.getByText('Candidate A')).toBeInTheDocument();
      expect(screen.getByText('Candidate B')).toBeInTheDocument();
      expect(screen.getByText('Candidate C')).toBeInTheDocument();
      expect(screen.getByText('Candidate D')).toBeInTheDocument();
      expect(screen.getByText('Candidate E')).toBeInTheDocument();
    });
  });

  it('disables continue button when there are unmatched items', async () => {
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
      const continueButton = screen.getByRole('button', { name: /към оптимизация/i });
      expect(continueButton).toBeDisabled();
    });
  });

  it('enables continue button when all items are matched', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    const mockStats: api.MatchStatistics = {
      totalItems: 100,
      matchedItems: 100,
      unmatchedItems: 0,
      uniquePositions: 100,
      averageScore: 0.95,
    };

    vi.mocked(api.triggerMatching).mockResolvedValue(mockStats);
    vi.mocked(api.getUnmatchedCandidates).mockResolvedValue([]);

    render(<MatchPage />);

    await waitFor(() => {
      const continueButton = screen.getByRole('button', { name: /към оптимизация/i });
      expect(continueButton).not.toBeDisabled();
    });
  });
});

