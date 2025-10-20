import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Download, FileSpreadsheet, CheckCircle2 } from 'lucide-react'
import { Button } from '../components/ui/button'
import { exportResults, type OptimizationResult, type BoqData } from '../lib/api'

export default function ExportPage() {
  const navigate = useNavigate()
  const [result, setResult] = useState<OptimizationResult | null>(null)
  const [boq, setBoq] = useState<BoqData | null>(null)
  const [isExporting, setIsExporting] = useState(false)

  useEffect(() => {
    const storedResult = sessionStorage.getItem('optimizationResult')
    const storedBoq = sessionStorage.getItem('boqData')
    
    if (storedResult && storedBoq) {
      setResult(JSON.parse(storedResult))
      setBoq(JSON.parse(storedBoq))
    } else {
      navigate('/')
    }
  }, [navigate])

  const handleExport = async (splitByStage: boolean = false) => {
    if (!result || !boq) return

    setIsExporting(true)
    try {
      const blob = await exportResults(result, boq, boq.projectName, splitByStage)
      
      // Create download link
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `КСС_Резултат_${new Date().toISOString().split('T')[0]}.xlsx`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error('Export failed:', err)
    } finally {
      setIsExporting(false)
    }
  }

  if (!result) {
    return <div className="text-center">Зареждане...</div>
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h2 className="text-3xl font-bold tracking-tight">Експорт на резултати</h2>
        <p className="text-muted-foreground mt-2">
          Изтеглете готовия КСС файл с оптимизирани коефициенти
        </p>
      </div>

      {/* Success Message */}
      <div className="bg-green-50 border border-green-200 rounded-lg p-6">
        <div className="flex items-start gap-4">
          <CheckCircle2 className="w-8 h-8 text-green-500 flex-shrink-0" />
          <div className="flex-1">
            <h3 className="text-lg font-semibold text-green-900">Оптимизацията завърши успешно!</h3>
            <p className="text-sm text-green-700 mt-1">
              Всички етапи са оптимизирани в рамките на зададените ограничения.
            </p>
          </div>
        </div>
      </div>

      {/* Summary Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="bg-card border rounded-lg p-4">
          <p className="text-sm text-muted-foreground">Solver статус</p>
          <p className="text-xl font-bold mt-1">{result.solverStatus}</p>
        </div>
        <div className="bg-card border rounded-lg p-4">
          <p className="text-sm text-muted-foreground">Objective Value</p>
          <p className="text-xl font-bold mt-1">{result.objectiveValue.toFixed(2)}</p>
        </div>
        <div className="bg-card border rounded-lg p-4">
          <p className="text-sm text-muted-foreground">Брой етапи</p>
          <p className="text-xl font-bold mt-1">{result.stageSummaries.length}</p>
        </div>
        <div className="bg-card border rounded-lg p-4">
          <p className="text-sm text-muted-foreground">Коефициенти</p>
          <p className="text-xl font-bold mt-1">{Object.keys(result.coefficients).length}</p>
        </div>
      </div>

      {/* Export Options */}
      <div className="bg-card border rounded-lg p-6 space-y-4">
        <h3 className="text-lg font-semibold">Формат на експорт</h3>
        
        <div className="space-y-3">
          <div className="flex items-start gap-3 p-4 border rounded-lg hover:bg-muted/50 cursor-pointer">
            <FileSpreadsheet className="w-5 h-5 text-green-600 flex-shrink-0 mt-0.5" />
            <div className="flex-1">
              <p className="font-medium">Excel (.xlsx)</p>
              <p className="text-sm text-muted-foreground">
                КСС файл с оптимизирани коефициенти, готов за въвеждане в система
              </p>
            </div>
            <Button onClick={() => handleExport(false)} disabled={isExporting}>
              {isExporting ? (
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
              ) : (
                <>
                  <Download className="w-4 h-4 mr-2" />
                  Изтегли
                </>
              )}
            </Button>
          </div>
        </div>
      </div>

      {/* Stage Summaries Preview */}
      <div className="bg-card border rounded-lg p-6">
        <h3 className="text-lg font-semibold mb-4">Преглед на етапи</h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left text-sm font-medium">Етап</th>
                <th className="px-4 py-3 text-right text-sm font-medium">Разходи</th>
                <th className="px-4 py-3 text-right text-sm font-medium">Прогноза</th>
                <th className="px-4 py-3 text-right text-sm font-medium">Отклонение</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {result.stageSummaries.map((summary, idx) => (
                <tr key={idx} className="hover:bg-muted/50">
                  <td className="px-4 py-3 text-sm font-mono">{summary.stageCode}</td>
                  <td className="px-4 py-3 text-sm text-right">{summary.totalCost.toFixed(2)} лв</td>
                  <td className="px-4 py-3 text-sm text-right">{summary.forecast.toFixed(2)} лв</td>
                  <td className={`px-4 py-3 text-sm text-right font-medium ${
                    Math.abs(summary.deviation) < 0.01 ? 'text-green-600' : 'text-orange-600'
                  }`}>
                    {summary.deviation >= 0 ? '+' : ''}{summary.deviation.toFixed(2)} лв
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex justify-between">
        <Button variant="outline" onClick={() => navigate('/optimize')}>
          Назад към оптимизация
        </Button>
        <Button onClick={() => {
          sessionStorage.clear()
          navigate('/')
        }}>
          Нов проект
        </Button>
      </div>
    </div>
  )
}

