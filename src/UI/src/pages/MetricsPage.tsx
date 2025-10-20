import { useEffect, useState } from 'react';
import { getMetrics, getRecentObservations, ObservationMetrics, OperationObservation } from '../lib/api';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';

export default function MetricsPage() {
  const [metrics, setMetrics] = useState<ObservationMetrics | null>(null);
  const [observations, setObservations] = useState<OperationObservation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadData();
    // Refresh every 30 seconds
    const interval = setInterval(loadData, 30000);
    return () => clearInterval(interval);
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [metricsData, recentObs] = await Promise.all([
        getMetrics(new Date(Date.now() - 7 * 24 * 60 * 60 * 1000)), // Last 7 days
        getRecentObservations(50)
      ]);
      setMetrics(metricsData);
      setObservations(recentObs);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load metrics');
    } finally {
      setLoading(false);
    }
  };

  if (loading && !metrics) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mx-auto mb-4"></div>
          <p className="text-gray-600">Зареждане на метрики...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="container mx-auto p-6">
        <Card className="border-red-200 bg-red-50">
          <CardHeader>
            <CardTitle className="text-red-800">Грешка</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-red-600">{error}</p>
            <button
              onClick={loadData}
              className="mt-4 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
            >
              Опитай отново
            </button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const successRate = metrics
    ? ((metrics.successfulOperations / metrics.totalOperations) * 100).toFixed(1)
    : '0';

  return (
    <div className="container mx-auto p-6 space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold">Метрики и телеметрия</h1>
          <p className="text-gray-600 mt-1">
            Последните 7 дни ({metrics?.since && new Date(metrics.since).toLocaleDateString('bg-BG')} - {metrics?.until && new Date(metrics.until).toLocaleDateString('bg-BG')})
          </p>
        </div>
        <button
          onClick={loadData}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 flex items-center gap-2"
          disabled={loading}
        >
          {loading ? (
            <>
              <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
              Обновяване...
            </>
          ) : (
            '🔄 Обнови'
          )}
        </button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Общо операции</CardDescription>
            <CardTitle className="text-3xl">{metrics?.totalOperations || 0}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-sm text-gray-600">
              <span className="text-green-600">✓ {metrics?.successfulOperations || 0}</span>
              {' • '}
              <span className="text-red-600">✗ {metrics?.failedOperations || 0}</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Успеваемост</CardDescription>
            <CardTitle className="text-3xl">{successRate}%</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-green-600 h-2 rounded-full"
                style={{ width: `${successRate}%` }}
              ></div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Средна продължителност</CardDescription>
            <CardTitle className="text-3xl">{Math.round(metrics?.averageDurationMs || 0)}ms</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-gray-600">На операция</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Дубликати</CardDescription>
            <CardTitle className="text-3xl">{metrics?.duplicateOperations || 0}</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-gray-600">Открити повторения</p>
          </CardContent>
        </Card>
      </div>

      {/* Operations by Type */}
      <Card>
        <CardHeader>
          <CardTitle>Операции по тип</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {Object.entries(metrics?.operationsByType || {}).map(([type, count]) => {
              const percentage = metrics ? ((count / metrics.totalOperations) * 100).toFixed(0) : 0;
              return (
                <div key={type} className="flex items-center gap-4">
                  <div className="w-32 font-medium">{type}</div>
                  <div className="flex-1">
                    <div className="w-full bg-gray-200 rounded-full h-6 flex items-center">
                      <div
                        className="bg-blue-500 h-6 rounded-full flex items-center justify-center text-white text-xs px-2"
                        style={{ width: `${percentage}%`, minWidth: '40px' }}
                      >
                        {count}
                      </div>
                    </div>
                  </div>
                  <div className="w-16 text-right text-sm text-gray-600">{percentage}%</div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Specific Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {metrics?.averageMatchScore !== undefined && (
          <Card>
            <CardHeader>
              <CardTitle>Matching метрики</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="flex justify-between">
                <span className="text-gray-600">Среден match score:</span>
                <span className="font-bold">{(metrics.averageMatchScore * 100).toFixed(1)}%</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Средно кандидати:</span>
                <span className="font-bold">{metrics.averageMatchCandidates?.toFixed(1) || 'N/A'}</span>
              </div>
            </CardContent>
          </Card>
        )}

        {metrics?.averageOptimizationObjective !== undefined && (
          <Card>
            <CardHeader>
              <CardTitle>Оптимизация метрики</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="flex justify-between">
                <span className="text-gray-600">Средна цел. функция:</span>
                <span className="font-bold">{metrics.averageOptimizationObjective.toFixed(2)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Успеваемост:</span>
                <span className="font-bold">
                  {((metrics.optimizationSuccessRate || 0) * 100).toFixed(1)}%
                </span>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Recent Observations */}
      <Card>
        <CardHeader>
          <CardTitle>Последни операции</CardTitle>
          <CardDescription>Последните {observations.length} операции</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-2 max-h-96 overflow-y-auto">
            {observations.map((obs, idx) => (
              <div
                key={idx}
                className="flex items-center justify-between p-3 bg-gray-50 rounded border"
              >
                <div className="flex items-center gap-3">
                  <Badge variant={obs.success ? 'default' : 'destructive'}>
                    {obs.success ? '✓' : '✗'}
                  </Badge>
                  <div>
                    <div className="font-medium">{obs.operationType}</div>
                    {obs.sourceFileName && (
                      <div className="text-sm text-gray-600">{obs.sourceFileName}</div>
                    )}
                    {obs.errorMessage && (
                      <div className="text-sm text-red-600">{obs.errorMessage}</div>
                    )}
                  </div>
                </div>
                <div className="text-right text-sm text-gray-600">
                  <div>{obs.durationMs}ms</div>
                  {obs.timestamp && (
                    <div className="text-xs">
                      {new Date(obs.timestamp).toLocaleTimeString('bg-BG')}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {observations.length === 0 && (
              <div className="text-center text-gray-500 py-8">
                Няма записани операции
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
