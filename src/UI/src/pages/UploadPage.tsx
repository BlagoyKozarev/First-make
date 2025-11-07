import { useState, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Upload, FileText, CheckCircle, AlertCircle, X } from 'lucide-react';
import {
  uploadKssFiles,
  uploadForecastFiles,
  uploadPriceBaseFiles,
  uploadTemplateFile,
  getProject,
} from '../lib/api';

interface UploadZoneProps {
  title: string;
  description: string;
  accept: string;
  maxFiles: number;
  files: File[];
  onFilesChange: (files: File[]) => void;
  icon: string;
}

function UploadZone({ title, description, accept, maxFiles, files, onFilesChange, icon }: UploadZoneProps) {
  const [isDragging, setIsDragging] = useState(false);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);

    const droppedFiles = Array.from(e.dataTransfer.files);
    const newFiles = [...files, ...droppedFiles].slice(0, maxFiles);
    onFilesChange(newFiles);
  }, [files, maxFiles, onFilesChange]);

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = Array.from(e.target.files || []);
    const newFiles = [...files, ...selectedFiles].slice(0, maxFiles);
    onFilesChange(newFiles);
  }, [files, maxFiles, onFilesChange]);

  const removeFile = (index: number) => {
    const newFiles = files.filter((_, i) => i !== index);
    onFilesChange(newFiles);
  };

  const inputId = `file-input-${title.replace(/\s+/g, '-')}`;

  return (
    <div className="bg-card border rounded-lg p-4">
      <div className="flex items-start gap-3 mb-3">
        <span className="text-2xl">{icon}</span>
        <div className="flex-1">
          <h3 className="font-semibold">{title}</h3>
          <p className="text-sm text-muted-foreground">{description}</p>
          <p className="text-xs text-muted-foreground mt-1">
            Максимум: {maxFiles} {maxFiles === 1 ? 'файл' : 'файла'}
          </p>
        </div>
      </div>

      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`
          border-2 border-dashed rounded-md p-6 text-center transition-colors
          ${isDragging ? 'border-primary bg-primary/5' : 'border-muted-foreground/25'}
          ${files.length >= maxFiles ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
        `}
      >
        {files.length < maxFiles ? (
          <div className="space-y-2">
            <Upload className="w-8 h-8 mx-auto text-muted-foreground" />
            <p className="text-sm">Пуснете файлове тук</p>
            <div>
              <input
                type="file"
                id={inputId}
                className="hidden"
                accept={accept}
                multiple={maxFiles > 1}
                onChange={handleFileSelect}
                disabled={files.length >= maxFiles}
              />
              <label htmlFor={inputId}>
                <span className="text-xs text-primary hover:underline cursor-pointer">
                  или изберете файлове
                </span>
              </label>
            </div>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">Достигнат максимален брой файлове</p>
        )}
      </div>

      {/* File List */}
      {files.length > 0 && (
        <div className="mt-3 space-y-2">
          {files.map((file, index) => (
            <div
              key={index}
              className="flex items-center gap-2 bg-muted/50 px-3 py-2 rounded-md text-sm"
            >
              <FileText className="w-4 h-4 text-muted-foreground flex-shrink-0" />
              <span className="flex-1 truncate">{file.name}</span>
              <span className="text-xs text-muted-foreground">
                {(file.size / 1024 / 1024).toFixed(2)} MB
              </span>
              <button
                onClick={() => removeFile(index)}
                className="text-destructive hover:bg-destructive/10 p-1 rounded"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default function UploadPage() {
  const navigate = useNavigate();
  const [projectId, setProjectId] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uploadProgress, setUploadProgress] = useState<string>('');
  const [filesUploaded, setFilesUploaded] = useState(false); // Track if files are uploaded

  // File states
  const [kssFiles, setKssFiles] = useState<File[]>([]);
  const [ukazaniaFiles, setUkazaniaFiles] = useState<File[]>([]);
  const [priceBaseFiles, setPriceBaseFiles] = useState<File[]>([]);
  const [templateFile, setTemplateFile] = useState<File[]>([]);

  useEffect(() => {
    const storedProjectId = sessionStorage.getItem('currentProjectId');
    console.log('UploadPage: Checking for project ID...', storedProjectId);
    
    if (!storedProjectId) {
      console.warn('UploadPage: No project ID found in sessionStorage, redirecting to setup');
      setError('Моля, първо създайте проект от началната страница');
      setTimeout(() => navigate('/'), 2000);
    } else {
      console.log('UploadPage: Found project ID:', storedProjectId);
      setProjectId(storedProjectId);
    }
  }, [navigate]);

  const handleUploadAll = async () => {
    if (!projectId) {
      setError('Няма активен проект. Моля, създайте проект от началната страница.');
      return;
    }

    // Validation
    if (kssFiles.length === 0) {
      setError('Моля качете поне 1 КСС файл');
      return;
    }

    console.log('Starting upload for project:', projectId);
    setIsUploading(true);
    setError(null);

    try {
      // Upload КСС files
      if (kssFiles.length > 0) {
        setUploadProgress('Качване на КСС файлове...');
        await uploadKssFiles(projectId, kssFiles);
      }

      // Upload Forecast files (прогнозни стойности)
      if (ukazaniaFiles.length > 0) {
        setUploadProgress('Качване на файл с прогнози...');
        await uploadForecastFiles(projectId, ukazaniaFiles);
      }

      // Upload Price Base files
      if (priceBaseFiles.length > 0) {
        setUploadProgress('Качване на Ценова база...');
        await uploadPriceBaseFiles(projectId, priceBaseFiles);
      }

      // Upload Template file
      if (templateFile.length > 0) {
        setUploadProgress('Качване на шаблон...');
        await uploadTemplateFile(projectId, templateFile[0]);
      }

      setUploadProgress('Готово!');
      
      // Fetch updated project info
      await getProject(projectId);

      // Mark files as uploaded
      setFilesUploaded(true);
      setUploadProgress('Файловете са качени успешно!');
    } catch (err) {
      console.error('Upload failed:', err);
      const error = err as { response?: { data?: { error?: string; message?: string; details?: string } } };
      const errorMessage = error.response?.data?.error || 
                          error.response?.data?.message || 
                          'Грешка при качване на файлове';
      const errorDetails = error.response?.data?.details;
      
      setError(errorDetails ? `${errorMessage}\n\nДетайли: ${errorDetails}` : errorMessage);
    } finally {
      setIsUploading(false);
      setUploadProgress('');
    }
  };

  const totalFiles = kssFiles.length + ukazaniaFiles.length + priceBaseFiles.length + templateFile.length;
  const canUpload = kssFiles.length > 0 && !isUploading;

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">Качване на Файлове</h1>
        <p className="text-muted-foreground">
          Качете всички необходими файлове за обработка. КСС файловете са задължителни.
        </p>
      </div>

      {/* Upload Zones Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <UploadZone
          title="КСС Файлове"
          description="Excel файлове (.xlsx) с количествени сметки"
          accept=".xlsx"
          maxFiles={40}
          files={kssFiles}
          onFilesChange={setKssFiles}
          icon="📊"
        />

        <UploadZone
          title="Прогнозни стойности по етапи"
          description="Excel файл (.xlsx) с колони: Етап, Прогноза"
          accept=".xlsx"
          maxFiles={2}
          files={ukazaniaFiles}
          onFilesChange={setUkazaniaFiles}
          icon="�"
        />

        <UploadZone
          title="Ценова База"
          description="Excel файлове (.xlsx) с единични цени"
          accept=".xlsx"
          maxFiles={2}
          files={priceBaseFiles}
          onFilesChange={setPriceBaseFiles}
          icon="💰"
        />

        <UploadZone
          title="Шаблон (опционално)"
          description="Excel файл (.xlsx) шаблон за експорт"
          accept=".xlsx"
          maxFiles={1}
          files={templateFile}
          onFilesChange={setTemplateFile}
          icon="📄"
        />
      </div>

      {/* Error Display */}
      {error && (
        <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-4 flex items-start gap-3 mb-6">
          <AlertCircle className="w-5 h-5 text-destructive flex-shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-medium text-destructive">Грешка</p>
            <p className="text-sm text-destructive/80 mt-1">{error}</p>
          </div>
        </div>
      )}

      {/* Progress Display */}
      {isUploading && (
        <div className="bg-primary/10 border border-primary/20 rounded-lg p-4 flex items-center gap-3 mb-6">
          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary" />
          <p className="text-sm font-medium">{uploadProgress}</p>
        </div>
      )}

      {/* Summary Card */}
      <div className="bg-muted/50 rounded-lg p-4 mb-6">
        <h3 className="font-medium mb-2">Резюме на файловете</h3>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <p className="text-muted-foreground">КСС файлове</p>
            <p className="font-semibold">{kssFiles.length} / 40</p>
          </div>
          <div>
            <p className="text-muted-foreground">Прогнози</p>
            <p className="font-semibold">{ukazaniaFiles.length} / 2</p>
          </div>
          <div>
            <p className="text-muted-foreground">Ценова база</p>
            <p className="font-semibold">{priceBaseFiles.length} / 2</p>
          </div>
          <div>
            <p className="text-muted-foreground">Шаблон</p>
            <p className="font-semibold">{templateFile.length} / 1</p>
          </div>
        </div>
        <div className="mt-3 pt-3 border-t">
          <p className="text-sm">
            <span className="font-medium">Общо файлове:</span> {totalFiles}
          </p>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex justify-between">
        <button
          onClick={() => navigate('/')}
          className="px-4 py-2 border rounded-md hover:bg-muted transition-colors"
          disabled={isUploading}
        >
          Назад
        </button>

        {!filesUploaded ? (
          <button
            onClick={handleUploadAll}
            disabled={!canUpload}
            className="px-6 py-2 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
          >
            {isUploading ? (
              <>
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
                Качване...
              </>
            ) : (
              <>
                <CheckCircle className="w-4 h-4" />
                Качи файлове
              </>
            )}
          </button>
        ) : (
          <button
            onClick={() => navigate('/match')}
            className="px-6 py-2 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 transition-colors flex items-center gap-2"
          >
            <>
              <CheckCircle className="w-4 h-4" />
              Продължи към съпоставяне
            </>
          </button>
        )}
      </div>

      {/* Info Section */}
      <div className="mt-8 bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h3 className="font-medium text-blue-900 mb-2">ℹ️ Важна информация</h3>
        <ul className="text-sm text-blue-800 space-y-1 list-disc list-inside">
          <li>КСС файловете трябва да съдържат колони: Наименование, Мярка, Количество, Етап</li>
          <li>Файлът с прогнози трябва да е Excel (.xlsx) с 2 колони: "Етап" (код) и "Прогноза" (стойност в лева)</li>
          <li>Ценовата база трябва да съдържа: Наименование, Мярка, Единична цена</li>
          <li>Всички файлове се парсват автоматично при качване</li>
          <li>След качването ще преминете към преглед на съпоставянията</li>
        </ul>
      </div>
    </div>
  );
}

