import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Download, FileText, CheckCircle } from 'lucide-react';
import {
  getProject,
  exportResults,
  getSelectedIteration,
  type ProjectSession,
  type IterationResult,
} from '../lib/api';

export default function ExportPage() {
  const navigate = useNavigate();
  const [projectId, setProjectId] = useState<string | null>(null);
  const [iteration, setIteration] = useState<IterationResult | null>(null);
  const [project, setProject] = useState<ProjectSession | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (id: string) => {
    console.log('ExportPage: Loading data for project:', id);
    try {
      const projectData = await getProject(id);
      console.log('ExportPage: Project loaded:', projectData);
      setProject(projectData);
      
      // Try to load iteration, but don't fail if it doesn't exist
      try {
        const iterationData = await getSelectedIteration(id);
        console.log('ExportPage: Selected iteration loaded:', iterationData);
        setIteration(iterationData);
      } catch (err) {
        console.log('ExportPage: No iteration found - export will use base prices');
        setIteration(null);
      }
    } catch (err) {
      console.error('ExportPage: Failed to load project:', err);
      setError('Грешка при зареждане на проекта');
      navigate('/');
    }
  }, [navigate]);

  useEffect(() => {
    const storedProjectId = sessionStorage.getItem('currentProjectId');
    console.log('ExportPage: useEffect - storedProjectId:', storedProjectId);
    if (!storedProjectId) {
      console.log('ExportPage: No project ID, navigating to home');
      navigate('/');
    } else {
      setProjectId(storedProjectId);
      loadData(storedProjectId);
    }
  }, [navigate, loadData]);

  const handleExport = async () => {
    if (!projectId) return;

    setIsExporting(true);
    setError(null);

    try {
      const blob = await exportResults(projectId);

      // Create download link
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `КСС_Резултат_${project?.metadata.objectName || 'Project'}_${new Date().toISOString().split('T')[0]}.zip`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Export failed:', err);
      const error = err as { response?: { data?: { message?: string } } };
      setError(error.response?.data?.message || 'Грешка при експорт');
    } finally {
      setIsExporting(false);
    }
  };

  if (!project) {
    console.log('ExportPage: Rendering loading state, project is null');
    return (
      <div className="max-w-6xl mx-auto text-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4" />
        <p className="text-muted-foreground">Зареждане...</p>
      </div>
    );
  }

  console.log('ExportPage: Rendering main content, project:', project, 'iteration:', iteration);
  const allStagesValid = iteration ? iteration.stageBreakdown.every((stage) => stage.gap >= 0) : false;

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">Експорт на Резултати</h1>
        <p className="text-muted-foreground">
          Изтеглете ZIP архив с {project.kssFilesCount + 1} файлове ({project.kssFilesCount} КСС + 1 обобщен файл)
        </p>
      </div>

      {/* Success Message */}
      <div className="bg-green-50 border border-green-200 rounded-lg p-6 mb-6">
        <div className="flex items-start gap-4">
          <CheckCircle className="w-8 h-8 text-green-600 flex-shrink-0" />
          <div>
            <h3 className="text-lg font-semibold text-green-900">Готово за експорт!</h3>
            <p className="text-sm text-green-700 mt-1">
              Оптимизацията завърши успешно. Всички коефициенти са изчислени и файловете са готови за изтегляне.
            </p>
          </div>
        </div>
      </div>

      {/* Project Summary */}
      <div className="bg-card border rounded-lg p-6 mb-6">
        <h2 className="text-xl font-semibold mb-4">Информация за проекта</h2>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-muted-foreground">Име на обект</p>
            <p className="font-semibold">{project.metadata.objectName}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Служител</p>
            <p className="font-semibold">{project.metadata.employee}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Дата</p>
            <p className="font-semibold">{new Date(project.metadata.date).toLocaleDateString('bg-BG')}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Създаден на</p>
            <p className="font-semibold">{new Date(project.createdAt).toLocaleString('bg-BG')}</p>
          </div>
        </div>
      </div>

      {/* Optimization Summary */}
      {iteration ? (
        <>
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
            <div className="flex items-center gap-2">
              <div className="text-blue-600">ℹ️</div>
              <p className="text-sm font-medium text-blue-900">
                Експортът ще използва резултатите от <strong>Итерация #{iteration.iterationNumber}</strong>
              </p>
              <button
                onClick={() => navigate('/iteration')}
                className="ml-auto text-sm text-blue-600 hover:text-blue-800 underline"
              >
                Промяна
              </button>
            </div>
          </div>
          
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Overall Gap</p>
              <p className={`text-2xl font-bold mt-1 ${iteration.overallGap >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {iteration.overallGap >= 0 ? '+' : ''}{iteration.overallGap.toFixed(2)} лв
              </p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Етапи</p>
              <p className="text-2xl font-bold mt-1">{iteration.stageBreakdown.length}</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Изходни файлове</p>
              <p className="text-2xl font-bold mt-1">{project.kssFilesCount + 2}</p>
              <p className="text-xs text-muted-foreground mt-1">КСС + 2 помощни</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Статус</p>
              <p className="text-2xl font-bold mt-1">
                {allStagesValid ? (
                  <span className="text-green-600">✓</span>
                ) : (
                  <span className="text-orange-600">⚠</span>
                )}
              </p>
            </div>
          </div>
        </>
      ) : (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-6">
          <div className="flex items-start gap-3">
            <div className="text-blue-600 mt-0.5">ℹ️</div>
            <div>
              <p className="font-medium text-blue-900">Оптимизацията не е изпълнена</p>
              <p className="text-sm text-blue-700 mt-1">
                Експортът ще използва базови цени с коефициент 1.0. 
                За да получите оптимизирани коефициенти, върнете се назад и изпълнете оптимизацията.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Export Options */}
      <div className="bg-card border rounded-lg p-6 mb-6">
        <h2 className="text-xl font-semibold mb-4">Експорт</h2>
        
        <div className="bg-muted/50 rounded-lg p-6 mb-4">
          <div className="flex items-start gap-4">
            <FileText className="w-12 h-12 text-primary flex-shrink-0" />
            <div className="flex-1">
              <h3 className="text-lg font-semibold mb-2">ZIP архив с всички файлове</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Съдържа {project.kssFilesCount + 2} файлове ({project.kssFilesCount} КСС + 2 помощни файла):
              </p>
              <ul className="text-sm text-muted-foreground space-y-1 list-disc list-inside mb-4">
                <li><strong>00_ОБОБЩЕНИЕ.xlsx</strong> - Обобщени резултати по етапи</li>
                <li><strong>01_ПРОВЕРКА_ЦЕНИ.xlsx</strong> - Работни цени по етапи и позиции</li>
                <li><strong>КСС файлове</strong> - Попълнени с коефициенти, работни цени и стойности</li>
              </ul>
              <button
                onClick={handleExport}
                disabled={isExporting}
                className="px-6 py-3 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
              >
                {isExporting ? (
                  <>
                    <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white" />
                    Генериране...
                  </>
                ) : (
                  <>
                    <Download className="w-5 h-5" />
                    Изтегли ZIP архив
                  </>
                )}
              </button>
            </div>
          </div>
        </div>

        {error && (
          <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-4 text-sm text-destructive">
            {error}
          </div>
        )}
      </div>

      {/* Stage Results Preview */}
      {iteration && (
        <div className="bg-card border rounded-lg p-6 mb-6">
          <h2 className="text-xl font-semibold mb-4">Преглед на резултатите</h2>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-4 py-3 text-left text-sm font-medium">Етап</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Предложена</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Прогноза</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Gap</th>
                  <th className="px-4 py-3 text-center text-sm font-medium">Статус</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {iteration.stageBreakdown.map((stage, idx) => (
                  <tr key={idx} className="hover:bg-muted/50">
                    <td className="px-4 py-3 text-sm font-mono">{stage.stage}</td>
                    <td className="px-4 py-3 text-sm text-right">{stage.proposed.toFixed(2)} лв</td>
                    <td className="px-4 py-3 text-sm text-right">{stage.forecast.toFixed(2)} лв</td>
                    <td className={`px-4 py-3 text-sm text-right font-semibold ${
                      stage.gap >= 0 ? 'text-green-600' : 'text-red-600'
                    }`}>
                      {stage.gap >= 0 ? '+' : ''}{stage.gap.toFixed(2)} лв
                    </td>
                    <td className="px-4 py-3 text-center">
                      {stage.gap >= 0 ? (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">
                          ✓ OK
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">
                          ⚠ Превишение
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex justify-between">
        <button
          onClick={() => navigate('/iteration')}
          className="px-4 py-2 border rounded-md hover:bg-muted transition-colors"
        >
          Назад към оптимизация
        </button>

        <button
          onClick={() => {
            sessionStorage.clear();
            navigate('/');
          }}
          className="px-6 py-2 border rounded-md hover:bg-muted transition-colors"
        >
          Нов Проект
        </button>
      </div>
    </div>
  );
}

