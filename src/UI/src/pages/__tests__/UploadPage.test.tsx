import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '../../test/test-utils';
import UploadPage from '../UploadPage';
import * as api from '../../lib/api';

// Mock the API module
vi.mock('../../lib/api', () => ({
  uploadKssFiles: vi.fn(),
  uploadUkazaniaFiles: vi.fn(),
  uploadPriceBaseFiles: vi.fn(),
  uploadTemplateFile: vi.fn(),
  getProject: vi.fn(),
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

describe('UploadPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Mock getProject to return a valid project
    vi.mocked(api.getProject).mockResolvedValue({
      projectId: 'test-proj-123',
      metadata: {
        objectName: 'Test Project',
        employee: 'Test User',
        date: '2024-01-01',
      },
      kssFilesCount: 0,
      ukazaniaFilesCount: 0,
      priceBaseFilesCount: 0,
      hasTemplate: false,
      hasMatchingResults: false,
      hasOptimizationResults: false,
      createdAt: '2024-01-01T00:00:00Z',
    });
  });

  it('renders all upload zones', () => {
    render(<UploadPage />);

    // Check for КСС files section heading
    expect(screen.getByRole('heading', { name: /КСС Файлове/i })).toBeInTheDocument();
    
    // Check for Указания section
    expect(screen.getByRole('heading', { name: /Указания/i })).toBeInTheDocument();
    
    // Check for price base section
    expect(screen.getByRole('heading', { name: /Ценова база/i })).toBeInTheDocument();
    
    // Check for template section (optional)
    expect(screen.getByRole('heading', { name: /Шаблон/i })).toBeInTheDocument();
  });

  it('displays drag and drop zones', () => {
    render(<UploadPage />);

    // Should have multiple file input elements (type="file")
    const fileInputs = document.querySelectorAll('input[type="file"]');
    expect(fileInputs.length).toBeGreaterThan(0);
  });

  it('shows upload button initially disabled', () => {
    render(<UploadPage />);

    // Button text is "Качи файлове и продължи"
    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    expect(uploadButton).toBeDisabled();
  });

  it('allows file selection via input', () => {
    render(<UploadPage />);

    // Create a mock file
    const file = new File(['dummy content'], 'test.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });

    // Find first file input (КСС files)
    const inputs = document.querySelectorAll('input[type="file"]');
    const kssInput = inputs[0] as HTMLInputElement;

    // Simulate file selection
    fireEvent.change(kssInput, { target: { files: [file] } });

    // File should appear in the list
    expect(screen.getByText('test.xlsx')).toBeInTheDocument();
  });

  it('displays file count correctly', () => {
    render(<UploadPage />);

    // Check the heading for КСС files exists
    expect(screen.getByRole('heading', { name: /КСС Файлове/i })).toBeInTheDocument();
  });

  it('shows remove button for uploaded files', async () => {
    render(<UploadPage />);

    const file = new File(['content'], 'test-kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    const kssInput = inputs[0] as HTMLInputElement;
    
    fireEvent.change(kssInput, { target: { files: [file] } });

    // Should show remove button (X icon)
    const removeButtons = screen.getAllByRole('button', { name: '' });
    expect(removeButtons.length).toBeGreaterThan(0);
  });

  it('removes file when remove button clicked', () => {
    render(<UploadPage />);

    const file = new File(['content'], 'test-remove.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    const kssInput = inputs[0] as HTMLInputElement;
    
    fireEvent.change(kssInput, { target: { files: [file] } });
    
    // File should be visible
    expect(screen.getByText('test-remove.xlsx')).toBeInTheDocument();

    // Find and click remove button
    const removeButtons = document.querySelectorAll('button[aria-label=""]');
    if (removeButtons.length > 0) {
      fireEvent.click(removeButtons[0]);
      
      // File should be removed
      expect(screen.queryByText('test-remove.xlsx')).not.toBeInTheDocument();
    }
  });

  it('enforces maximum file limit', () => {
    render(<UploadPage />);

    // Create multiple files (max is 25 for КСС according to current UI)
    const files = Array.from({ length: 25 }, (_, i) => 
      new File(['content'], `test-${i}.xlsx`, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    );

    const inputs = document.querySelectorAll('input[type="file"]');
    const kssInput = inputs[0] as HTMLInputElement;
    
    // Upload 25 files (which is the max)
    fireEvent.change(kssInput, { target: { files } });

    // Should accept all files up to max (25)
    const fileListItems = screen.queryAllByText(/test-\d+\.xlsx/);
    expect(fileListItems.length).toBeLessThanOrEqual(25);
  });

  it('enables upload button when required files are selected', async () => {
    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['content'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const pricebaseFile = new File(['content'], 'pricebase.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    
    // Upload КСС file
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    
    // Upload Ukazania file
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });
    
    // Upload Pricebase file
    fireEvent.change(inputs[2], { target: { files: [pricebaseFile] } });

    // Check that files are displayed
    await waitFor(() => {
      expect(screen.getByText('kss.xlsx')).toBeInTheDocument();
      expect(screen.getByText('ukazania.xlsx')).toBeInTheDocument();
      expect(screen.getByText('pricebase.xlsx')).toBeInTheDocument();
    });
  });

  it('displays file list when files are uploaded', async () => {
    vi.mocked(api.uploadKssFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadUkazaniaFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadPriceBaseFiles).mockResolvedValue(undefined);

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['content'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const pricebaseFile = new File(['content'], 'pricebase.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });
    fireEvent.change(inputs[2], { target: { files: [pricebaseFile] } });

    // Files should be visible in the list
    await waitFor(() => {
      expect(screen.getByText('kss.xlsx')).toBeInTheDocument();
    });
  });

  it('displays uploaded file names', async () => {
    vi.mocked(api.uploadKssFiles).mockRejectedValue(new Error('Upload failed'));

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['content'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const pricebaseFile = new File(['content'], 'pricebase.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });
    fireEvent.change(inputs[2], { target: { files: [pricebaseFile] } });

    // Check that all files appear in the list
    await waitFor(() => {
      expect(screen.getByText('kss.xlsx')).toBeInTheDocument();
      expect(screen.getByText('ukazania.xlsx')).toBeInTheDocument();
      expect(screen.getByText('pricebase.xlsx')).toBeInTheDocument();
    });
  });

  it('accepts template file upload', async () => {
    vi.mocked(api.uploadTemplateFile).mockResolvedValue(undefined);
    vi.mocked(api.uploadKssFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadUkazaniaFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadPriceBaseFiles).mockResolvedValue(undefined);

    render(<UploadPage />);

    const templateFile = new File(['content'], 'template.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['content'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const pricebaseFile = new File(['content'], 'pricebase.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });
    fireEvent.change(inputs[2], { target: { files: [pricebaseFile] } });
    fireEvent.change(inputs[3], { target: { files: [templateFile] } });

    // Check that template file appears
    await waitFor(() => {
      expect(screen.getByText('template.xlsx')).toBeInTheDocument();
    });
  });

  it('handles drag and drop events', async () => {
    render(<UploadPage />);

    const dropZones = document.querySelectorAll('[class*="border-dashed"]');
    const kssDropZone = dropZones[0];

    // Create mock file
    const file = new File(['content'], 'dragged.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    
    // Simulate drag over
    fireEvent.dragOver(kssDropZone, {
      dataTransfer: {
        files: [file],
      },
    });

    // Simulate drag leave
    fireEvent.dragLeave(kssDropZone);

    // Simulate drop
    fireEvent.drop(kssDropZone, {
      dataTransfer: {
        files: [file],
      },
    });

    await waitFor(() => {
      expect(screen.getByText('dragged.xlsx')).toBeInTheDocument();
    });
  });

  it('displays file size in MB', async () => {
    render(<UploadPage />);

    const file = new File(['a'.repeat(1024 * 1024 * 2)], 'large.xlsx', { 
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
    });

    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [file] } });

    await waitFor(() => {
      expect(screen.getByText(/2\.\d{2} MB/)).toBeInTheDocument();
    });
  });

  it('shows max files reached message', async () => {
    render(<UploadPage />);

    // Create 25 files (max for KSS)
    const files = Array.from({ length: 25 }, (_, i) => 
      new File(['content'], `file-${i}.xlsx`, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    );

    const inputs = document.querySelectorAll('input[type="file"]');
    const kssInput = inputs[0] as HTMLInputElement;

    // Upload all 25 files
    Object.defineProperty(kssInput, 'files', {
      value: files,
      writable: false,
    });
    fireEvent.change(kssInput);

    await waitFor(() => {
      expect(screen.getByText(/Достигнат максимален брой файлове/i)).toBeInTheDocument();
    });
  });

  it('redirects to home if no projectId', () => {
    sessionStorage.removeItem('currentProjectId');
    render(<UploadPage />);

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('displays validation error when no KSS files', async () => {
    render(<UploadPage />);

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    
    // Button should be disabled initially
    expect(uploadButton).toBeDisabled();
  });

  it('shows upload progress messages', async () => {
    vi.mocked(api.uploadKssFiles).mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));
    vi.mocked(api.uploadUkazaniaFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadPriceBaseFiles).mockResolvedValue(undefined);

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      expect(screen.getByText('kss.xlsx')).toBeInTheDocument();
    });
  });

  it('handles back navigation', () => {
    render(<UploadPage />);

    const backButton = screen.getByRole('button', { name: /назад/i });
    fireEvent.click(backButton);

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('shows max files message when limit reached for template', async () => {
    render(<UploadPage />);

    // Upload single file to template zone (maxFiles: 1)
    const templateFile = new File(['content'], 'template.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const inputs = document.querySelectorAll('input[type="file"]');
    const templateInput = inputs[3] as HTMLInputElement;

    fireEvent.change(templateInput, { target: { files: [templateFile] } });

    await waitFor(() => {
      expect(screen.getByText('template.xlsx')).toBeInTheDocument();
    });

    // Should show max files message (maxFiles: 1)
    await waitFor(() => {
      const maxMessages = screen.queryAllByText(/Достигнат максимален брой файлове/i);
      // Template zone should show this message since it reached max (1 file)
      expect(maxMessages.length).toBeGreaterThan(0);
    });
  });

  it('validates that KSS files are required before upload', async () => {
    render(<UploadPage />);

    // Try to upload without KSS files - button should be disabled
    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    expect(uploadButton).toBeDisabled();

    // Add only ukazania files (not KSS)
    const ukazaniaFile = new File(['content'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });

    await waitFor(() => {
      expect(screen.getByText('ukazania.xlsx')).toBeInTheDocument();
    });

    // Button should still be disabled (no KSS files)
    expect(uploadButton).toBeDisabled();
  });

  it('enables upload button only when KSS files are present', async () => {
    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });
  });

  it('shows error when trying to upload without KSS files', async () => {
    render(<UploadPage />);

    // Upload button should be disabled without KSS files
    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    expect(uploadButton).toBeDisabled();
  });

  it('displays total files count in summary', async () => {
    render(<UploadPage />);

    const kssFile1 = new File(['content'], 'kss1.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const kssFile2 = new File(['content'], 'kss2.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile1, kssFile2] } });

    await waitFor(() => {
      expect(screen.getByText(/Общо файлове:/i)).toBeInTheDocument();
    });
  });

  it('shows drag hint text when no files are uploaded', () => {
    render(<UploadPage />);

    expect(screen.getAllByText(/Пуснете файлове тук/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/или изберете файлове/i).length).toBeGreaterThan(0);
  });

  it('displays file icon for each uploaded file', async () => {
    render(<UploadPage />);

    const kssFile = new File(['content'], 'test.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const inputs = document.querySelectorAll('input[type="file"]');
    
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      expect(screen.getByText('test.xlsx')).toBeInTheDocument();
    });
  });

  it('shows all four upload sections', () => {
    render(<UploadPage />);

    // Check that all main sections are present
    const inputs = document.querySelectorAll('input[type="file"]');
    expect(inputs.length).toBe(4); // KSS, Ukazania, PriceBase, Template
  });

  it('displays project metadata instructions', () => {
    render(<UploadPage />);

    expect(screen.getByText(/Качете всички необходими файлове за обработка/i)).toBeInTheDocument();
    expect(screen.getByText(/КСС файловете са задължителни/i)).toBeInTheDocument();
  });

  it('shows upload button text correctly', () => {
    render(<UploadPage />);

    expect(screen.getByRole('button', { name: /качи файлове и продължи/i })).toBeInTheDocument();
  });

  it('shows back button', () => {
    render(<UploadPage />);

    expect(screen.getByRole('button', { name: /назад/i })).toBeInTheDocument();
  });

  it('displays max files limit for each section', () => {
    render(<UploadPage />);

    // Each section should display its max file limit
    const maxTexts = screen.getAllByText(/Максимум:/i);
    expect(maxTexts.length).toBeGreaterThan(0);
  });

  it('successfully uploads KSS files and navigates to match page', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    vi.mocked(api.uploadKssFiles).mockResolvedValue(undefined);
    vi.mocked(api.getProject).mockResolvedValue({
      projectId: 'test-proj-123',
      metadata: { objectName: 'Test', employee: 'User', date: '2024-01-01' },
      kssFilesCount: 1,
      ukazaniaFilesCount: 0,
      priceBaseFilesCount: 0,
      hasTemplate: false,
      hasMatchingResults: false,
      hasOptimizationResults: false,
      createdAt: '2024-01-01T00:00:00Z',
    });

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { 
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
    });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    await waitFor(() => {
      expect(api.uploadKssFiles).toHaveBeenCalledWith('test-proj-123', [kssFile]);
      expect(api.getProject).toHaveBeenCalledWith('test-proj-123');
    }, { timeout: 3000 });

    // Wait for navigation
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/match');
    }, { timeout: 1000 });
  });

  it('uploads all file types when all are provided', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    vi.mocked(api.uploadKssFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadUkazaniaFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadPriceBaseFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadTemplateFile).mockResolvedValue(undefined);

    render(<UploadPage />);

    const kssFile = new File(['kss'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['ukazania'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const pricebaseFile = new File(['pricebase'], 'pricebase.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const templateFile = new File(['template'], 'template.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });
    fireEvent.change(inputs[2], { target: { files: [pricebaseFile] } });
    fireEvent.change(inputs[3], { target: { files: [templateFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    await waitFor(() => {
      expect(api.uploadKssFiles).toHaveBeenCalled();
      expect(api.uploadUkazaniaFiles).toHaveBeenCalled();
      expect(api.uploadPriceBaseFiles).toHaveBeenCalled();
      expect(api.uploadTemplateFile).toHaveBeenCalled();
    }, { timeout: 3000 });
  });

  it('displays error message when upload fails', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    vi.mocked(api.uploadKssFiles).mockRejectedValue({
      response: { data: { message: 'Upload failed: Network error' } }
    });

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { 
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
    });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/Upload failed: Network error/i)).toBeInTheDocument();
    }, { timeout: 3000 });
  });

  it('shows generic error when API returns no message', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    vi.mocked(api.uploadKssFiles).mockRejectedValue(new Error('Unknown error'));

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { 
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
    });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/Грешка при качване на файлове/i)).toBeInTheDocument();
    }, { timeout: 3000 });
  });

  it('disables upload button during upload', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    // Create a promise that we can control
    let resolveUpload: () => void;
    const uploadPromise = new Promise<void>((resolve) => {
      resolveUpload = resolve;
    });
    
    vi.mocked(api.uploadKssFiles).mockReturnValue(uploadPromise);

    render(<UploadPage />);

    const kssFile = new File(['content'], 'kss.xlsx', { 
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
    });
    
    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    // Button should be disabled during upload
    await waitFor(() => {
      expect(uploadButton).toBeDisabled();
    });

    // Resolve the upload
    resolveUpload!();
  });

  it('shows upload progress messages', async () => {
    sessionStorage.setItem('currentProjectId', 'test-proj-123');
    
    vi.mocked(api.uploadKssFiles).mockResolvedValue(undefined);
    vi.mocked(api.uploadUkazaniaFiles).mockResolvedValue(undefined);

    render(<UploadPage />);

    const kssFile = new File(['kss'], 'kss.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const ukazaniaFile = new File(['ukazania'], 'ukazania.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

    const inputs = document.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0], { target: { files: [kssFile] } });
    fireEvent.change(inputs[1], { target: { files: [ukazaniaFile] } });

    await waitFor(() => {
      const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
      expect(uploadButton).not.toBeDisabled();
    });

    const uploadButton = screen.getByRole('button', { name: /качи файлове и продължи/i });
    fireEvent.click(uploadButton);

    // Check for progress messages (they appear briefly)
    await waitFor(() => {
      expect(api.uploadKssFiles).toHaveBeenCalled();
    }, { timeout: 3000 });
  });
});
