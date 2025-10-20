import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight, Settings, TrendingUp } from 'lucide-react'
import { Button } from '../components/ui/button'
import { optimize, type BoqData, type OptimizationRequest, type OptimizationResult } from '../lib/api'

export default function OptimizePage() {
  const navigate = useNavigate()
  const [boqData, setBoqData] = useState<BoqData | null>(null)
  const [matches, setMatches] = useState<Record<string, string>>({})
  const [lambda, setLambda] = useState(1000)
  const [coeffMin, setCoeffMin] = useState(0.4)
  const [coeffMax, setCoeffMax] = useState(2.0)
  const [isOptimizing, setIsOptimizing] = useState(false)
  const [result, setResult] = useState<OptimizationResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const storedBoq = sessionStorage.getItem('boqData')
    const storedMatches = sessionStorage.getItem('matches')
    
    if (storedBoq && storedMatches) {
      setBoqData(JSON.parse(storedBoq))
      setMatches(JSON.parse(storedMatches))
    } else {
      navigate('/')
    }
  }, [navigate])

  const handleOptimize = async () => {
    if (!boqData) return

    setIsOptimizing(true)
    setError(null)

    try {
      const request: OptimizationRequest = {
        boq: boqData,
        priceBase: [], // TODO: Load actual price base
        matches: matches,
        lambda: lambda,
        coeffBounds: { min: coeffMin, max: coeffMax }
      }

      const optimizationResult = await optimize(request)
      setResult(optimizationResult)
      sessionStorage.setItem('optimizationResult', JSON.stringify(optimizationResult))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Грешка при оптимизация')
    } finally {
      setIsOptimizing(false)
    }
  }

  const handleProceedToExport = () => {
    navigate('/export')
  }

  if (!boqData) {
    return <div className="text-center">Зареждане...</div>
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      <div>
        <h2 className="text-3xl font-bold tracking-tight">Оптимизация</h2>
        <p className="text-muted-foreground mt-2">
          Настройте параметрите на линейното програмиране
        </p>
      </div>

      {/* Settings Panel */}
      <div className="bg-card rounded-lg border p-6 space-y-6">
        <div className="flex items-center gap-2 mb-4">
          <Settings className="w-5 h-5" />
          <h3 className="text-lg font-semibold">Параметри на оптимизацията</h3>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div>
            <label className="block text-sm font-medium mb-2">
              Lambda (тегло на L1 penalty)
            </label>
            <input
              type="number"
              value={lambda}
              onChange={(e) => setLambda(Number(e.target.value))}
              className="w-full px-3 py-2 border rounded-md"
              min="0"
              step="100"
            />
            <p className="text-xs text-muted-foreground mt-1">
              По-висока стойност → коефициенти по-близо до 1.0
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium mb-2">
              Минимален коефициент
            </label>
            <input
              type="number"
              value={coeffMin}
              onChange={(e) => setCoeffMin(Number(e.target.value))}
              className="w-full px-3 py-2 border rounded-md"
              min="0"
              max="1"
              step="0.1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Долна граница за c_g (обикновено 0.4)
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium mb-2">
              Максимален коефициент
            </label>
            <input
              type="number"
              value={coeffMax}
              onChange={(e) => setCoeffMax(Number(e.target.value))}
              className="w-full px-3 py-2 border rounded-md"
              min="1"
              max="5"
              step="0.1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Горна граница за c_g (обикновено 2.0)
            </p>
          </div>
        </div>

        <div className="flex justify-end">
          <Button
            size="lg"
            onClick={handleOptimize}
            disabled={isOptimizing}
          >
            {isOptimizing ? (
              <>
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2" />
                Оптимизиране...
              </>
            ) : (
              <>
                <TrendingUp className="w-4 h-4 mr-2" />
                Стартирай оптимизация
              </>
            )}
          </Button>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-4">
          <p className="font-medium text-destructive">Грешка: {error}</p>
        </div>
      )}

      {/* Results Panel */}
      {result && (
        <div className="bg-card rounded-lg border p-6 space-y-6">
          <div className="flex items-center gap-2 mb-4">
            <TrendingUp className="w-5 h-5 text-green-500" />
            <h3 className="text-lg font-semibold">Резултати</h3>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-muted rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Статус на solver</p>
              <p className="text-xl font-bold mt-1">{result.solverStatus}</p>
            </div>
            <div className="bg-muted rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Objective Value</p>
              <p className="text-xl font-bold mt-1">{result.objectiveValue.toFixed(2)}</p>
            </div>
            <div className="bg-muted rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Брой етапи</p>
              <p className="text-xl font-bold mt-1">{result.stageSummaries.length}</p>
            </div>
            <div className="bg-muted rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Брой коефициенти</p>
              <p className="text-xl font-bold mt-1">{Object.keys(result.coefficients).length}</p>
            </div>
          </div>

          {/* Stage Summaries */}
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-4 py-3 text-left text-sm font-medium">Етап</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Разходи (лв)</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Прогноза (лв)</th>
                  <th className="px-4 py-3 text-right text-sm font-medium">Отклонение</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {result.stageSummaries.map((summary, idx) => (
                  <tr key={idx} className="hover:bg-muted/50">
                    <td className="px-4 py-3 text-sm font-mono">{summary.stageCode}</td>
                    <td className="px-4 py-3 text-sm text-right">{summary.totalCost.toFixed(2)}</td>
                    <td className="px-4 py-3 text-sm text-right">{summary.forecast.toFixed(2)}</td>
                    <td className={`px-4 py-3 text-sm text-right font-medium ${
                      Math.abs(summary.deviation) < 0.01 ? 'text-green-500' : 'text-orange-500'
                    }`}>
                      {summary.deviation >= 0 ? '+' : ''}{summary.deviation.toFixed(2)}
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
        <Button variant="outline" onClick={() => navigate('/review')}>
          Назад
        </Button>
        {result && (
          <Button size="lg" onClick={handleProceedToExport}>
            Експорт на резултати
            <ArrowRight className="w-4 h-4 ml-2" />
          </Button>
        )}
      </div>
    </div>
  )
}

