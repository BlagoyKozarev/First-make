import { useState, useEffect } from 'react';
import { Calculator, Save, RefreshCw } from 'lucide-react';
import { getAvailableStages, setManualForecasts } from '../lib/api';

interface ManualForecastsInputProps {
  projectId: string;
  onSaved?: () => void;
}

export default function ManualForecastsInput({ projectId, onSaved }: ManualForecastsInputProps) {
  const [stages, setStages] = useState<Array<{
    code: string;
    name: string;
    itemCount: number;
    currentForecast?: number;
  }>>([]);
  const [forecasts, setForecasts] = useState<Record<string, string>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const loadStages = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const stagesData = await getAvailableStages(projectId);
      setStages(stagesData);
      
      // Initialize forecast inputs with current values if any
      const initialForecasts: Record<string, string> = {};
      stagesData.forEach(stage => {
        if (stage.currentForecast !== undefined && stage.currentForecast !== null) {
          initialForecasts[stage.code] = stage.currentForecast.toString();
        } else {
          initialForecasts[stage.code] = '';
        }
      });
      setForecasts(initialForecasts);
    } catch (err) {
      console.error('Failed to load stages:', err);
      setError('Грешка при зареждане на етапите. Моля, качете КСС файлове първо.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadStages();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  const handleForecastChange = (stageCode: string, value: string) => {
    // Allow only numbers and decimal point
    const sanitized = value.replace(/[^\d.]/g, '');
    setForecasts(prev => ({ ...prev, [stageCode]: sanitized }));
    setSuccess(false);
  };

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    setSuccess(false);

    try {
      // Convert string values to numbers, skip empty
      const numericForecasts: Record<string, number> = {};
      let hasValues = false;

      Object.entries(forecasts).forEach(([code, value]) => {
        if (value && value.trim() !== '') {
          numericForecasts[code] = parseFloat(value);
          hasValues = true;
        }
      });

      if (!hasValues) {
        setError('Моля, въведете поне една прогнозна стойност');
        setIsSaving(false);
        return;
      }

      await setManualForecasts(projectId, numericForecasts);
      setSuccess(true);
      
      // Reload to show updated values
      await loadStages();

      if (onSaved) {
        onSaved();
      }

      // Auto-hide success message
      setTimeout(() => setSuccess(false), 3000);
    } catch (err) {
      console.error('Failed to save forecasts:', err);
      setError('Грешка при запазване на прогнозните стойности');
    } finally {
      setIsSaving(false);
    }
  };

  const totalForecast = Object.values(forecasts)
    .filter(v => v && v.trim() !== '')
    .reduce((sum, val) => sum + parseFloat(val), 0);

  if (isLoading) {
    return (
      <div className="bg-card border rounded-lg p-6">
        <div className="flex items-center gap-3">
          <RefreshCw className="w-5 h-5 animate-spin text-primary" />
          <p className="text-sm">Зареждане на етапи...</p>
        </div>
      </div>
    );
  }

  if (stages.length === 0) {
    return (
      <div className="bg-card border rounded-lg p-6">
        <div className="flex items-start gap-3">
          <Calculator className="w-5 h-5 text-muted-foreground" />
          <div>
            <p className="font-medium">Ръчно въвеждане на прогнозни стойности</p>
            <p className="text-sm text-muted-foreground mt-1">
              Моля, качете КСС файлове първо, за да се покажат етапите
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-card border rounded-lg p-6">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-start gap-3">
          <Calculator className="w-5 h-5 text-primary" />
          <div>
            <h3 className="font-semibold">Ръчно въвеждане на прогнозни стойности</h3>
            <p className="text-sm text-muted-foreground mt-1">
              Въведете прогнозни стойности за всеки етап (вместо качване на Указания файл)
            </p>
          </div>
        </div>
        <button
          onClick={loadStages}
          disabled={isLoading}
          className="p-2 hover:bg-muted rounded-md transition-colors"
          title="Презареди етапи"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {error && (
        <div className="bg-destructive/10 border border-destructive/20 rounded-lg p-3 mb-4">
          <p className="text-sm text-destructive">{error}</p>
        </div>
      )}

      {success && (
        <div className="bg-green-50 border border-green-200 rounded-lg p-3 mb-4">
          <p className="text-sm text-green-700">✅ Прогнозните стойности са запазени успешно!</p>
        </div>
      )}

      <div className="space-y-3 mb-4">
        {stages.map((stage) => (
          <div key={stage.code} className="flex items-center gap-3">
            <label className="flex-1 text-sm font-medium">
              {stage.name}
              <span className="text-xs text-muted-foreground ml-2">
                ({stage.itemCount} позиции)
              </span>
            </label>
            <div className="relative">
              <input
                type="text"
                inputMode="decimal"
                placeholder="0.00"
                value={forecasts[stage.code] || ''}
                onChange={(e) => handleForecastChange(stage.code, e.target.value)}
                className="w-40 px-3 py-2 border rounded-md text-right focus:outline-none focus:ring-2 focus:ring-primary"
              />
              <span className="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-muted-foreground pointer-events-none">
                лв
              </span>
            </div>
          </div>
        ))}
      </div>

      <div className="flex items-center justify-between pt-4 border-t">
        <div className="text-sm">
          <span className="text-muted-foreground">Обща прогноза: </span>
          <span className="font-bold text-lg">{totalForecast.toFixed(2)} лв</span>
        </div>
        <button
          onClick={handleSave}
          disabled={isSaving || totalForecast === 0}
          className="px-6 py-2 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
        >
          <Save className="w-4 h-4" />
          {isSaving ? 'Запазване...' : 'Запази прогнози'}
        </button>
      </div>
    </div>
  );
}
