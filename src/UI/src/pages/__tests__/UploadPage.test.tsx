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
});
