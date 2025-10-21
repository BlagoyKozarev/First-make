import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import './App.css'

// V2.0 Pages - Multi-file КСС Processing
import SetupPage from './pages/SetupPage'
import UploadPage from './pages/UploadPage'
import MatchPage from './pages/MatchPage'
import IterationPage from './pages/IterationPage'
import ExportPage from './pages/ExportPage'

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto px-4 py-4">
            <div className="flex justify-between items-center">
              <div>
                <h1 className="text-2xl font-bold">FirstMake v2.0</h1>
                <p className="text-sm text-muted-foreground">
                  Обработка на множество КСС файлове с унифицирано съпоставяне
                </p>
              </div>
              <nav className="flex gap-4">
                <a href="/" className="text-sm hover:underline">Начало</a>
                <a href="/upload" className="text-sm hover:underline">Качване</a>
                <a href="/match" className="text-sm hover:underline">Съпоставяне</a>
                <a href="/iteration" className="text-sm hover:underline">Оптимизация</a>
                <a href="/export" className="text-sm hover:underline">Експорт</a>
              </nav>
            </div>
          </div>
        </header>
        
        <main className="container mx-auto px-4 py-8">
          <Routes>
            <Route path="/" element={<SetupPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/match" element={<MatchPage />} />
            <Route path="/iteration" element={<IterationPage />} />
            <Route path="/export" element={<ExportPage />} />
          </Routes>
        </main>
      </div>
    </Router>
  )
}

export default App
