import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '../../test/test-utils';
import IterationPage from '../IterationPage';

// Mock the API module
vi.mock('../../lib/api', () => ({
  runOptimization: vi.fn(),
  getLatestIteration: vi.fn(),
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

describe('IterationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
  });

  it('redirects to home if no projectId in session', () => {
    render(<IterationPage />);

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('renders page heading when projectId exists', async () => {
    sessionStorage.setItem('currentProjectId', 'test-project-123');

    render(<IterationPage />);

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /Оптимизация/i, level: 1 })).toBeInTheDocument();
    });
  });
});
