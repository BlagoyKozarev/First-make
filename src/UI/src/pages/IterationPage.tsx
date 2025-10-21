import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Play, CheckCircle, AlertTriangle, TrendingUp } from 'lucide-react';
import {
  runOptimization,
  getLatestIteration,
  type IterationResult,
} from '../lib/api';

export default function IterationPage() {
  const navigate = useNavigate();
  const [projectId, setProjectId] = useState<string | null>(null);
  const [isOptimizing, setIsOptimizing] = useState(false);
  const [iteration, setIteration] = useState<IterationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedFile, setExpandedFile] = useState<string | null>(null);

  useEffect(() => {
    const storedProjectId = sessionStorage.getItem('currentProjectId');
    if (!storedProjectId) {
      navigate('/');
    } else {
      setProjectId(storedProjectId);
      // Try to load existing iteration
      loadLatestIteration(storedProjectId);
    }
  }, [navigate]);

  const loadLatestIteration = async (id: string) => {
    try {
      const latest = await getLatestIteration(id);
      setIteration(latest);
    } catch (err) {
      // No iteration yet, that's OK
      console.log('No iteration found yet');
    }
  };

  const handleOptimize = async () => {
    if (!projectId) return;

    setIsOptimizing(true);
    setError(null);

    try {
      const result = await runOptimization(projectId);
      setIteration(result);
    } catch (err) {
      console.error('Optimization failed:', err);
      const error = err as { response?: { data?: { message?: string } } };
      setError(error.response?.data?.message || 'Грешка при оптимизация');
    } finally {
      setIsOptimizing(false);
    }
  };

  const allStagesValid = iteration
    ? iteration.stageBreakdown.every((stage) => stage.gap >= 0)
    : false;

  return (
    <div className="max-w-7xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">Оптимизация</h1>
        <p className="text-muted-foreground">
          Изпълнете линейна оптимизация за изравняване на бюджета по етапи
        </p>
      </div>

      {/* Optimization Button */}
      {!iteration && (
        <div className="bg-card border rounded-lg p-6 mb-6 text-center">
          <TrendingUp className="w-16 h-16 mx-auto text-primary mb-4" />
          <h3 className="text-xl font-semibold mb-2">Готови за оптимизация</h3>
          <p className="text-muted-foreground mb-6">
            Стартирайте линейната оптимизация за изчисляване на коефициенти
          </p>
          <button
            onClick={handleOptimize}
            disabled={isOptimizing}
            className="px-8 py-3 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2 mx-auto"
          >
            {isOptimizing ? (
              <>
                <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white" />
                Оптимизация...
              </>
            ) : (
              <>
                <Play className="w-5 h-5" />
                Стартирай Оптимизация
              </>
            )}
          </button>
        </div>
      )}

      {/* Loading State */}
      {isOptimizing && (
        <div className="bg-primary/10 border border-primary/20 rounded-lg p-4 flex items-center gap-3 mb-6">
          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary" />
          <p className="text-sm font-medium">Изпълнява се LP оптимизация с OR-Tools GLOP solver...</p>
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-4 flex items-start gap-3 mb-6">
          <AlertTriangle className="w-5 h-5 text-destructive flex-shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-medium text-destructive">Грешка</p>
            <p className="text-sm text-destructive/80 mt-1">{error}</p>
          </div>
        </div>
      )}

      {/* Iteration Results */}
      {iteration && (
        <>
          {/* Success/Warning Message */}
          {allStagesValid ? (
            <div className="bg-green-50 border border-green-200 rounded-lg p-6 mb-6">
              <div className="flex items-start gap-4">
                <CheckCircle className="w-8 h-8 text-green-600 flex-shrink-0" />
                <div>
                  <h3 className="text-lg font-semibold text-green-900">Оптимизацията завърши успешно!</h3>
                  <p className="text-sm text-green-700 mt-1">
                    Всички етапи са в рамките на бюджета. Overall Gap: {iteration.overallGap.toFixed(2)} лв
                  </p>
                </div>
              </div>
            </div>
          ) : (
            <div className="bg-orange-50 border border-orange-200 rounded-lg p-6 mb-6">
              <div className="flex items-start gap-4">
                <AlertTriangle className="w-8 h-8 text-orange-600 flex-shrink-0" />
                <div>
                  <h3 className="text-lg font-semibold text-orange-900">Има етапи с отрицателен Gap</h3>
                  <p className="text-sm text-orange-700 mt-1">
                    Някои етапи надвишават прогнозата. Разгледайте детайлите по-долу.
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* Summary Cards */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Overall Gap</p>
              <p className={`text-2xl font-bold mt-1 ${iteration.overallGap >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {iteration.overallGap >= 0 ? '+' : ''}{iteration.overallGap.toFixed(2)} лв
              </p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Предложена</p>
              <p className="text-2xl font-bold mt-1">{iteration.totalProposed.toFixed(2)} лв</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Прогноза</p>
              <p className="text-2xl font-bold mt-1">{iteration.totalForecast.toFixed(2)} лв</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Време на solver</p>
              <p className="text-2xl font-bold mt-1">{iteration.solverTimeMs} ms</p>
            </div>
          </div>

          {/* Stage Breakdown Table */}
          <div className="bg-card border rounded-lg p-6 mb-6">
            <h2 className="text-xl font-semibold mb-4">Разбивка по етапи</h2>
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

          {/* File Breakdown (Expandable) */}
          <div className="bg-card border rounded-lg p-6 mb-6">
            <h2 className="text-xl font-semibold mb-4">Разбивка по файлове ({iteration.fileBreakdown.length} файла)</h2>
            <div className="space-y-2">
              {iteration.fileBreakdown.map((file, idx) => (
                <div key={idx} className="border rounded-lg">
                  <button
                    onClick={() => setExpandedFile(expandedFile === file.fileName ? null : file.fileName)}
                    className="w-full px-4 py-3 text-left hover:bg-muted/50 transition-colors flex items-center justify-between"
                  >
                    <span className="font-medium">{file.fileName}</span>
                    <span className="text-sm text-muted-foreground">
                      Общо: {file.totalProposed.toFixed(2)} лв
                    </span>
                  </button>
                  {expandedFile === file.fileName && (
                    <div className="px-4 py-3 border-t bg-muted/20">
                      <p className="text-sm font-medium mb-2">Gap по етапи:</p>
                      <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-sm">
                        {Object.entries(file.stageGaps).map(([stage, gap]) => (
                          <div key={stage} className="flex justify-between">
                            <span className="text-muted-foreground">{stage}:</span>
                            <span className={`font-semibold ${gap >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                              {gap >= 0 ? '+' : ''}{gap.toFixed(2)}
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* Re-optimize Button */}
          <div className="flex items-center justify-center mb-6">
            <button
              onClick={handleOptimize}
              disabled={isOptimizing}
              className="px-6 py-2 border rounded-md hover:bg-muted transition-colors flex items-center gap-2"
            >
              <Play className="w-4 h-4" />
              Изпълни отново
            </button>
          </div>
        </>
      )}

      {/* Action Buttons */}
      <div className="flex justify-between">
        <button
          onClick={() => navigate('/match')}
          className="px-4 py-2 border rounded-md hover:bg-muted transition-colors"
        >
          Назад
        </button>

        <button
          onClick={() => navigate('/export')}
          disabled={!iteration || !allStagesValid}
          className="px-6 py-2 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          Към Експорт
        </button>
      </div>
    </div>
  );
}
