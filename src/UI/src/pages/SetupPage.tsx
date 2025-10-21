import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { createProject, ProjectMetadata } from '../lib/api';

// Validation schema
const setupSchema = z.object({
  objectName: z.string().min(1, 'Името на обекта е задължително'),
  employee: z.string().min(1, 'Името на служителя е задължително'),
  date: z.string().min(1, 'Датата е задължителна'),
});

type SetupFormData = z.infer<typeof setupSchema>;

export default function SetupPage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SetupFormData>({
    resolver: zodResolver(setupSchema),
    defaultValues: {
      objectName: '',
      employee: '',
      date: new Date().toISOString().split('T')[0], // Today's date
    },
  });

  const onSubmit = async (data: SetupFormData) => {
    setIsSubmitting(true);
    setError(null);

    try {
      const metadata: ProjectMetadata = {
        objectName: data.objectName,
        employee: data.employee,
        date: data.date,
      };

      const session = await createProject(metadata);
      
      // Store project ID in sessionStorage for other pages
      sessionStorage.setItem('currentProjectId', session.projectId);
      
      // Navigate to upload page
      navigate('/upload');
    } catch (err) {
      console.error('Failed to create project:', err);
      const error = err as { response?: { data?: { message?: string } } };
      setError(error.response?.data?.message || 'Грешка при създаване на проект');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Нов Проект</h1>
        <p className="text-muted-foreground">
          Въведете основна информация за проекта преди да започнете качване на файлове
        </p>
      </div>

      <div className="bg-card p-6 rounded-lg border">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
          {/* Object Name */}
          <div>
            <label htmlFor="objectName" className="block text-sm font-medium mb-2">
              Име на обект <span className="text-destructive">*</span>
            </label>
            <input
              id="objectName"
              type="text"
              {...register('objectName')}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-primary"
              placeholder="Напр. Жилищна сграда - бул. Витоша 123"
            />
            {errors.objectName && (
              <p className="text-sm text-destructive mt-1">{errors.objectName.message}</p>
            )}
          </div>

          {/* Employee Name */}
          <div>
            <label htmlFor="employee" className="block text-sm font-medium mb-2">
              Служител <span className="text-destructive">*</span>
            </label>
            <input
              id="employee"
              type="text"
              {...register('employee')}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-primary"
              placeholder="Напр. Иван Иванов"
            />
            {errors.employee && (
              <p className="text-sm text-destructive mt-1">{errors.employee.message}</p>
            )}
          </div>

          {/* Date */}
          <div>
            <label htmlFor="date" className="block text-sm font-medium mb-2">
              Дата <span className="text-destructive">*</span>
            </label>
            <input
              id="date"
              type="date"
              {...register('date')}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-primary"
            />
            {errors.date && (
              <p className="text-sm text-destructive mt-1">{errors.date.message}</p>
            )}
          </div>

          {/* Error Message */}
          {error && (
            <div className="bg-destructive/10 text-destructive px-4 py-3 rounded-md">
              {error}
            </div>
          )}

          {/* Submit Button */}
          <div className="flex gap-4">
            <button
              type="submit"
              disabled={isSubmitting}
              className="flex-1 bg-primary text-primary-foreground px-6 py-3 rounded-md font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isSubmitting ? 'Създаване...' : 'Създай Проект'}
            </button>
          </div>
        </form>
      </div>

      {/* Info Card */}
      <div className="mt-6 bg-muted/50 p-4 rounded-md">
        <h3 className="font-medium mb-2">ℹ️ Следващи стъпки:</h3>
        <ol className="text-sm text-muted-foreground space-y-1 list-decimal list-inside">
          <li>Качване на КСС файлове (до 25 броя)</li>
          <li>Качване на Указания (до 2 броя)</li>
          <li>Качване на Ценова база (до 2 броя)</li>
          <li>Качване на шаблон (по избор)</li>
          <li>Преглед и корекция на съвпаденията</li>
          <li>Оптимизация на позициите</li>
          <li>Експорт на резултатите</li>
        </ol>
      </div>
    </div>
  );
}
