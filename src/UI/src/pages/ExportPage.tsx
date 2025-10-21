import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Download, FileText, CheckCircle } from 'lucide-react';
import {
  exportResults,
  getLatestIteration,
  getProject,
  type IterationResult,
  type ProjectSession,
} from '../lib/api';

export default function ExportPage() {
  const navigate = useNavigate();
  const [projectId, setProjectId] = useState<string | null>(null);
  const [iteration, setIteration] = useState<IterationResult | null>(null);
  const [project, setProject] = useState<ProjectSession | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (id: string) => {
    try {
      const [iterationData, projectData] = await Promise.all([
        getLatestIteration(id),
        getProject(id),
      ]);
      setIteration(iterationData);
      setProject(projectData);
    } catch (err) {
      console.error('Failed to load data:', err);
      navigate('/iteration');
    }
  }, [navigate]);

  useEffect(() => {
    const storedProjectId = sessionStorage.getItem('currentProjectId');
    if (!storedProjectId) {
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

  if (!iteration || !project) {
    return (
      <div className="max-w-6xl mx-auto text-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4" />
        <p className="text-muted-foreground">Зареждане...</p>
      </div>
    );
  }

  const allStagesValid = iteration.stageBreakdown.every((stage) => stage.gap >= 0);

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">Експорт на Резултати</h1>
        <p className="text-muted-foreground">
          Изтеглете ZIP архив с {project.kssFilesCount} попълнени КСС файлове
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
          <p className="text-sm text-muted-foreground">КСС файлове</p>
          <p className="text-2xl font-bold mt-1">{project.kssFilesCount}</p>
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

      {/* Export Options */}
      <div className="bg-card border rounded-lg p-6 mb-6">
        <h2 className="text-xl font-semibold mb-4">Експорт</h2>
        
        <div className="bg-muted/50 rounded-lg p-6 mb-4">
          <div className="flex items-start gap-4">
            <FileText className="w-12 h-12 text-primary flex-shrink-0" />
            <div className="flex-1">
              <h3 className="text-lg font-semibold mb-2">ZIP архив с всички файлове</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Съдържа {project.kssFilesCount} попълнени Excel файла с изчислените коефициенти и работни цени.
                Всеки файл включва:
              </p>
              <ul className="text-sm text-muted-foreground space-y-1 list-disc list-inside mb-4">
                <li>Колона "Коеф." с изчислените коефициенти (0.4 - 2.0)</li>
                <li>Колона "Работна цена" (базова цена × коефициент)</li>
                <li>Попълнени колони "цена" и "стойност"</li>
                <li>Запазено оригинално форматиране</li>
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

