import { useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Upload, FileText, CheckCircle, AlertCircle } from 'lucide-react'
import { Button } from '../components/ui/button'
import { uploadAndParse, extractBoq, type BoqData } from '../lib/api'

export default function UploadPage() {
  const navigate = useNavigate()
  const [file, setFile] = useState<File | null>(null)
  const [isDragging, setIsDragging] = useState(false)
  const [isUploading, setIsUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [progress, setProgress] = useState<string>('')

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)
    
    const droppedFile = e.dataTransfer.files[0]
    if (droppedFile) {
      setFile(droppedFile)
      setError(null)
    }
  }, [])

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0]
    if (selectedFile) {
      setFile(selectedFile)
      setError(null)
    }
  }, [])

  const handleUpload = async () => {
    if (!file) return

    setIsUploading(true)
    setError(null)
    setProgress('Качване на файл...')

    try {
      // Step 1: Parse file
      setProgress('Парсиране на документ...')
      const parsedData = await uploadAndParse(file)
      
      // Step 2: Extract BoQ
      setProgress('Извличане на данни с AI...')
      const boqData: BoqData = await extractBoq(parsedData)
      
      // Step 3: Store in session and navigate
      sessionStorage.setItem('boqData', JSON.stringify(boqData))
      sessionStorage.setItem('sourceFile', file.name)
      
      setProgress('Готово!')
      setTimeout(() => navigate('/review'), 500)
      
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Неизвестна грешка')
      setIsUploading(false)
      setProgress('')
    }
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div>
        <h2 className="text-3xl font-bold tracking-tight">Качване на документ</h2>
        <p className="text-muted-foreground mt-2">
          Поддържани формати: XLSX, DOCX, PDF, PNG/JPG (сканирани)
        </p>
      </div>

      {/* Drag & Drop Zone */}
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`
          border-2 border-dashed rounded-lg p-12 text-center transition-colors
          ${isDragging ? 'border-primary bg-primary/5' : 'border-muted-foreground/25'}
          ${file ? 'bg-muted/50' : ''}
        `}
      >
        {file ? (
          <div className="space-y-4">
            <CheckCircle className="w-16 h-16 mx-auto text-green-500" />
            <div>
              <p className="font-medium">{file.name}</p>
              <p className="text-sm text-muted-foreground">
                {(file.size / 1024 / 1024).toFixed(2)} MB
              </p>
            </div>
            <Button variant="outline" onClick={() => setFile(null)}>
              Смени файл
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            <Upload className="w-16 h-16 mx-auto text-muted-foreground" />
            <div>
              <p className="text-lg font-medium">Пуснете файл тук</p>
              <p className="text-sm text-muted-foreground">или</p>
            </div>
            <div>
              <input
                type="file"
                id="file-upload"
                className="hidden"
                accept=".xlsx,.docx,.pdf,.png,.jpg,.jpeg"
                onChange={handleFileSelect}
              />
              <label htmlFor="file-upload">
                <Button asChild variant="secondary">
                  <span>Изберете файл</span>
                </Button>
              </label>
            </div>
          </div>
        )}
      </div>

      {/* Error Display */}
      {error && (
        <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-4 flex items-start gap-3">
          <AlertCircle className="w-5 h-5 text-destructive flex-shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-medium text-destructive">Грешка при обработка</p>
            <p className="text-sm text-destructive/80 mt-1">{error}</p>
          </div>
        </div>
      )}

      {/* Progress Display */}
      {isUploading && (
        <div className="bg-primary/10 border border-primary/20 rounded-lg p-4 flex items-center gap-3">
          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary" />
          <p className="text-sm font-medium">{progress}</p>
        </div>
      )}

      {/* Action Button */}
      <div className="flex justify-end">
        <Button
          size="lg"
          onClick={handleUpload}
          disabled={!file || isUploading}
        >
          <FileText className="w-4 h-4 mr-2" />
          Обработи документ
        </Button>
      </div>

      {/* Info Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-8">
        <div className="bg-muted rounded-lg p-4">
          <h3 className="font-medium mb-2">📊 Автоматично извличане</h3>
          <p className="text-sm text-muted-foreground">
            AI системата автоматично разпознава КСС структури в XLSX, DOCX и PDF файлове.
          </p>
        </div>
        <div className="bg-muted rounded-lg p-4">
          <h3 className="font-medium mb-2">🔒 Локална обработка</h3>
          <p className="text-sm text-muted-foreground">
            Всички данни остават на вашата машина. Никакви външни API извиквания.
          </p>
        </div>
      </div>
    </div>
  )
}

