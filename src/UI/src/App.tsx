import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import './App.css'

// Pages (to be created in Task 7)
import UploadPage from './pages/UploadPage'
import ReviewPage from './pages/ReviewPage'
import OptimizePage from './pages/OptimizePage'
import ExportPage from './pages/ExportPage'
import MetricsPage from './pages/MetricsPage'

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto px-4 py-4">
            <div className="flex justify-between items-center">
              <div>
                <h1 className="text-2xl font-bold">FirstMake Agent</h1>
                <p className="text-sm text-muted-foreground">
                  Локален инструмент за обработка на КСС и оферти
                </p>
              </div>
              <nav className="flex gap-4">
                <a href="/" className="text-sm hover:underline">Качване</a>
                <a href="/review" className="text-sm hover:underline">Преглед</a>
                <a href="/optimize" className="text-sm hover:underline">Оптимизация</a>
                <a href="/export" className="text-sm hover:underline">Експорт</a>
                <a href="/metrics" className="text-sm hover:underline">Метрики</a>
              </nav>
            </div>
          </div>
        </header>
        
        <main className="container mx-auto px-4 py-8">
          <Routes>
            <Route path="/" element={<UploadPage />} />
            <Route path="/review" element={<ReviewPage />} />
            <Route path="/optimize" element={<OptimizePage />} />
            <Route path="/export" element={<ExportPage />} />
            <Route path="/metrics" element={<MetricsPage />} />
          </Routes>
        </main>
      </div>
    </Router>
  )
}

export default App
