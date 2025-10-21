import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle, AlertTriangle, Search } from 'lucide-react';
import ConfirmDialog from '../components/ConfirmDialog';
import {
  triggerMatching,
  getUnmatchedCandidates,
  overrideMatch,
  type UnifiedCandidate,
  type MatchStatistics,
} from '../lib/api';

export default function MatchPage() {
  const navigate = useNavigate();
  const [projectId, setProjectId] = useState<string | null>(null);
  const [isMatching, setIsMatching] = useState(false);
  const [statistics, setStatistics] = useState<MatchStatistics | null>(null);
  const [candidates, setCandidates] = useState<UnifiedCandidate[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [selectedCandidate, setSelectedCandidate] = useState<UnifiedCandidate | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  
  // Dialog state
  const [showOverrideDialog, setShowOverrideDialog] = useState(false);
  const [pendingOverride, setPendingOverride] = useState<{ candidate: UnifiedCandidate; priceEntryName: string } | null>(null);
  const [isOverriding, setIsOverriding] = useState(false);

  useEffect(() => {
    const storedProjectId = sessionStorage.getItem('currentProjectId');
    if (!storedProjectId) {
      navigate('/');
    } else {
      setProjectId(storedProjectId);
      // Auto-trigger matching on page load
      handleTriggerMatching(storedProjectId);
    }
  }, [navigate]);

  const handleTriggerMatching = async (id: string) => {
    setIsMatching(true);
    setError(null);

    try {
      const stats = await triggerMatching(id);
      setStatistics(stats);

      // Fetch unmatched candidates if any
      if (stats.unmatchedItems > 0) {
        const unmatchedCandidates = await getUnmatchedCandidates(id);
        setCandidates(unmatchedCandidates);
      }
    } catch (err) {
      console.error('Matching failed:', err);
      const error = err as { response?: { data?: { message?: string } } };
      setError(error.response?.data?.message || 'Грешка при съпоставяне');
    } finally {
      setIsMatching(false);
    }
  };

  const handleOverride = async (candidate: UnifiedCandidate, priceEntryName: string) => {
    setPendingOverride({ candidate, priceEntryName });
    setShowOverrideDialog(true);
  };

  const confirmOverride = async () => {
    if (!projectId || !pendingOverride) return;

    setIsOverriding(true);
    try {
      await overrideMatch(projectId, pendingOverride.candidate.unifiedKey, pendingOverride.priceEntryName);
      
      // Refresh statistics and candidates
      await handleTriggerMatching(projectId);
      
      setSelectedCandidate(null);
      setShowOverrideDialog(false);
      setPendingOverride(null);
    } catch (err) {
      console.error('Override failed:', err);
      const error = err as { response?: { data?: { message?: string } } };
      setError(error.response?.data?.message || 'Грешка при корекция');
    } finally {
      setIsOverriding(false);
    }
  };

  const filteredCandidates = candidates.filter((candidate) => {
    const matchesSearch = candidate.itemName.toLowerCase().includes(searchTerm.toLowerCase());
    return matchesSearch;
  });

  const matchPercentage = statistics
    ? Math.round((statistics.matchedItems / statistics.totalItems) * 100)
    : 0;

  return (
    <div className="max-w-7xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">Преглед на Съпоставянията</h1>
        <p className="text-muted-foreground">
          Прегледайте автоматичните съпоставяния и коригирайте при нужда
        </p>
      </div>

      {/* Loading State */}
      {isMatching && (
        <div className="bg-primary/10 border border-primary/20 rounded-lg p-4 flex items-center gap-3 mb-6">
          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary" />
          <p className="text-sm font-medium">Съпоставяне на позиции...</p>
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

      {/* Statistics Cards */}
      {statistics && (
        <>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-6">
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Общо позиции</p>
              <p className="text-2xl font-bold mt-1">{statistics.totalItems}</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Съпоставени</p>
              <p className="text-2xl font-bold text-green-600 mt-1">{statistics.matchedItems}</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Несъпоставени</p>
              <p className="text-2xl font-bold text-orange-600 mt-1">{statistics.unmatchedItems}</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">Уникални</p>
              <p className="text-2xl font-bold mt-1">{statistics.uniquePositions}</p>
            </div>
            <div className="bg-card border rounded-lg p-4">
              <p className="text-sm text-muted-foreground">СреденScore</p>
              <p className="text-2xl font-bold mt-1">{statistics.averageScore.toFixed(2)}</p>
            </div>
          </div>

          {/* Progress Bar */}
          <div className="bg-card border rounded-lg p-4 mb-6">
            <div className="flex justify-between items-center mb-2">
              <span className="text-sm font-medium">Напредък на съпоставянията</span>
              <span className="text-sm font-bold">{matchPercentage}%</span>
            </div>
            <div className="w-full bg-muted rounded-full h-3">
              <div
                className="bg-green-600 h-3 rounded-full transition-all duration-500"
                style={{ width: `${matchPercentage}%` }}
              />
            </div>
          </div>
        </>
      )}

      {/* Unmatched Items Section */}
      {statistics && statistics.unmatchedItems > 0 && (
        <div className="bg-card border rounded-lg p-6 mb-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-semibold">Несъпоставени позиции</h2>
            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <input
                  type="text"
                  placeholder="Търсене..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10 pr-4 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                />
              </div>
            </div>
          </div>

          <div className="space-y-4">
            {filteredCandidates.map((candidate) => (
              <div key={candidate.unifiedKey} className="border rounded-lg p-4 hover:bg-muted/50">
                <div className="flex items-start justify-between mb-3">
                  <div className="flex-1">
                    <h3 className="font-semibold">{candidate.itemName}</h3>
                    <p className="text-sm text-muted-foreground">
                      Мярка: {candidate.itemUnit} • Среща се {candidate.occurrenceCount}x в различни файлове
                    </p>
                  </div>
                  <button
                    onClick={() => setSelectedCandidate(selectedCandidate?.unifiedKey === candidate.unifiedKey ? null : candidate)}
                    className="px-3 py-1 text-sm border rounded-md hover:bg-muted transition-colors"
                  >
                    {selectedCandidate?.unifiedKey === candidate.unifiedKey ? 'Скрий' : 'Избери'}
                  </button>
                </div>

                {selectedCandidate?.unifiedKey === candidate.unifiedKey && (
                  <div className="mt-4 pt-4 border-t">
                    <p className="text-sm font-medium mb-3">Топ 5 предложения:</p>
                    <div className="space-y-2">
                      {candidate.topCandidates.map((topCandidate, idx) => (
                        <div
                          key={idx}
                          className="flex items-center justify-between p-3 bg-muted/50 rounded-md hover:bg-muted"
                        >
                          <div className="flex-1">
                            <p className="font-medium text-sm">{topCandidate.name}</p>
                            <p className="text-xs text-muted-foreground">
                              {topCandidate.unit} • {topCandidate.price.toFixed(2)} лв • Score: {topCandidate.score.toFixed(2)}
                            </p>
                          </div>
                          <button
                            onClick={() => handleOverride(candidate, topCandidate.name)}
                            className="px-3 py-1 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors"
                          >
                            Избери
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>

          {filteredCandidates.length === 0 && (
            <p className="text-center text-muted-foreground py-8">Няма резултати от търсенето</p>
          )}
        </div>
      )}

      {/* Success Message */}
      {statistics && statistics.unmatchedItems === 0 && (
        <div className="bg-green-50 border border-green-200 rounded-lg p-6 mb-6">
          <div className="flex items-start gap-4">
            <CheckCircle className="w-8 h-8 text-green-600 flex-shrink-0" />
            <div>
              <h3 className="text-lg font-semibold text-green-900">Всички позиции са съпоставени!</h3>
              <p className="text-sm text-green-700 mt-1">
                Всички {statistics.totalItems} позиции имат съпоставени ценови записи. Може да продължите към оптимизация.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex justify-between">
        <button
          onClick={() => navigate('/upload')}
          className="px-4 py-2 border rounded-md hover:bg-muted transition-colors"
        >
          Назад
        </button>

        <div className="flex gap-3">
          {statistics && statistics.unmatchedItems > 0 && (
            <button
              onClick={() => projectId && handleTriggerMatching(projectId)}
              className="px-4 py-2 border rounded-md hover:bg-muted transition-colors"
              disabled={isMatching}
            >
              Опресни съпоставянията
            </button>
          )}
          
          <button
            onClick={() => navigate('/iteration')}
            disabled={!statistics || statistics.unmatchedItems > 0}
            className="px-6 py-2 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Към Оптимизация
          </button>
        </div>
      </div>

      {/* Info Section */}
      {statistics && statistics.unmatchedItems > 0 && (
        <div className="mt-8 bg-blue-50 border border-blue-200 rounded-lg p-4">
          <h3 className="font-medium text-blue-900 mb-2">ℹ️ Как да коригирам съпоставяне?</h3>
          <ol className="text-sm text-blue-800 space-y-1 list-decimal list-inside">
            <li>Кликнете "Избери" до позицията, която искате да коригирате</li>
            <li>Прегледайте топ 5 предложенията с автоматичен score</li>
            <li>Изберете най-подходящата позиция от ценовата база</li>
            <li>Корекцията ще се приложи за всички файлове с тази позиция</li>
          </ol>
        </div>
      )}

      {/* Override Confirmation Dialog */}
      <ConfirmDialog
        isOpen={showOverrideDialog}
        onClose={() => {
          setShowOverrideDialog(false);
          setPendingOverride(null);
        }}
        onConfirm={confirmOverride}
        title="Потвърждение на корекция"
        message={`Сигурни ли сте, че искате да замените съпоставянето за "${pendingOverride?.candidate.itemName}"? Промяната ще се приложи за всички ${pendingOverride?.candidate.occurrenceCount} появявания в различните файлове.`}
        confirmText="Да, замени"
        cancelText="Отказ"
        variant="warning"
        isLoading={isOverriding}
      />
    </div>
  );
}
