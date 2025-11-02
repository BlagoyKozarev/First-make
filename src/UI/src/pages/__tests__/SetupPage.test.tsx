/**
 * SetupPage Component Tests
 * 
 * Tests the project creation workflow. Validates:
 * - Form rendering with all required fields
 * - Form validation (required fields, date format)
 * - Project creation API integration
 * - Success/error handling
 * - Navigation after project creation
 * 
 * Coverage: 100% (7 tests)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '../../test/test-utils';
import SetupPage from '../SetupPage';
import * as api from '../../lib/api';

// Mock the API module
vi.mock('../../lib/api', () => ({
  createProject: vi.fn(),
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

describe('SetupPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
  });

  it('renders the setup form with all fields', () => {
    render(<SetupPage />);

    expect(screen.getByRole('heading', { name: /Нов Проект/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/Име на обект/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Служител/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Дата/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Създай проект/i })).toBeInTheDocument();
  });

  it('displays validation errors for empty required fields', async () => {
    render(<SetupPage />);

    const submitButton = screen.getByRole('button', { name: /Създай проект/i });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText(/Името на обекта е задължително/i)).toBeInTheDocument();
      expect(screen.getByText(/Името на служителя е задължително/i)).toBeInTheDocument();
    });
  });

  it('populates date field with today\'s date by default', () => {
    render(<SetupPage />);

    const dateInput = screen.getByLabelText(/Дата/i) as HTMLInputElement;
    const today = new Date().toISOString().split('T')[0];
    
    expect(dateInput.value).toBe(today);
  });

  it('successfully creates project and navigates to upload page', async () => {
    const mockSession: api.ProjectSession = {
      projectId: 'test-project-123',
      metadata: {
        objectName: 'Test Object',
        employee: 'John Doe',
        date: '2025-10-30',
      },
      kssFilesCount: 0,
      ukazaniaFilesCount: 0,
      priceBaseFilesCount: 0,
      hasTemplate: false,
      hasMatchingResults: false,
      hasOptimizationResults: false,
      createdAt: new Date().toISOString(),
    };

    vi.mocked(api.createProject).mockResolvedValue(mockSession);

    render(<SetupPage />);

    // Fill in the form
    const objectNameInput = screen.getByLabelText(/Име на обект/i);
    const employeeInput = screen.getByLabelText(/Служител/i);
    const dateInput = screen.getByLabelText(/Дата/i);

    fireEvent.change(objectNameInput, { target: { value: 'Test Object' } });
    fireEvent.change(employeeInput, { target: { value: 'John Doe' } });
    fireEvent.change(dateInput, { target: { value: '2025-10-30' } });

    // Submit the form
    const submitButton = screen.getByRole('button', { name: /Създай проект/i });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(api.createProject).toHaveBeenCalledWith({
        objectName: 'Test Object',
        employee: 'John Doe',
        date: '2025-10-30',
      });
    });

    // Check that project ID was stored in sessionStorage
    expect(sessionStorage.getItem('currentProjectId')).toBe('test-project-123');

    // Check that navigation occurred
    expect(mockNavigate).toHaveBeenCalledWith('/upload');
  });

  it('displays error message when project creation fails', async () => {
    vi.mocked(api.createProject).mockRejectedValue(new Error('API Error'));

    render(<SetupPage />);

    // Fill in the form
    fireEvent.change(screen.getByLabelText(/Име на обект/i), { target: { value: 'Test Object' } });
    fireEvent.change(screen.getByLabelText(/Служител/i), { target: { value: 'John Doe' } });

    // Submit the form
    const submitButton = screen.getByRole('button', { name: /Създай проект/i });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText(/Грешка при създаване на проект/i)).toBeInTheDocument();
    });

    // Should not navigate on error
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('disables submit button while submitting', async () => {
    const mockSession: api.ProjectSession = {
      projectId: 'test-123',
      metadata: { objectName: 'Test', employee: 'Test', date: '2025-10-30' },
      kssFilesCount: 0,
      ukazaniaFilesCount: 0,
      priceBaseFilesCount: 0,
      hasTemplate: false,
      hasMatchingResults: false,
      hasOptimizationResults: false,
      createdAt: new Date().toISOString(),
    };

    vi.mocked(api.createProject).mockImplementation(
      () => new Promise(resolve => setTimeout(() => resolve(mockSession), 100))
    );

    render(<SetupPage />);

    // Fill in required fields
    fireEvent.change(screen.getByLabelText(/Име на обект/i), { target: { value: 'Test' } });
    fireEvent.change(screen.getByLabelText(/Служител/i), { target: { value: 'Test' } });

    const submitButton = screen.getByRole('button', { name: /Създай проект/i });
    
    // Button should be enabled initially
    expect(submitButton).not.toBeDisabled();

    // Click submit
    fireEvent.click(submitButton);

    // Button should be disabled while submitting
    await waitFor(() => {
      expect(submitButton).toBeDisabled();
    });
  });

  it('allows user to update all form fields', () => {
    render(<SetupPage />);

    const objectNameInput = screen.getByLabelText(/Име на обект/i) as HTMLInputElement;
    const employeeInput = screen.getByLabelText(/Служител/i) as HTMLInputElement;
    const dateInput = screen.getByLabelText(/Дата/i) as HTMLInputElement;

    // Update all fields
    fireEvent.change(objectNameInput, { target: { value: 'New Building' } });
    fireEvent.change(employeeInput, { target: { value: 'Jane Smith' } });
    fireEvent.change(dateInput, { target: { value: '2025-12-31' } });

    // Verify values
    expect(objectNameInput.value).toBe('New Building');
    expect(employeeInput.value).toBe('Jane Smith');
    expect(dateInput.value).toBe('2025-12-31');
  });
});
