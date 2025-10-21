import { AlertTriangle, Info, CheckCircle, XCircle } from 'lucide-react';

interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'danger' | 'warning' | 'info' | 'success';
  isLoading?: boolean;
}

export default function ConfirmDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Потвърди',
  cancelText = 'Отказ',
  variant = 'warning',
  isLoading = false,
}: ConfirmDialogProps) {
  if (!isOpen) return null;

  const variantStyles = {
    danger: {
      icon: <XCircle className="w-12 h-12 text-red-600" />,
      bg: 'bg-red-50',
      border: 'border-red-200',
      textColor: 'text-red-900',
      buttonBg: 'bg-red-600 hover:bg-red-700',
    },
    warning: {
      icon: <AlertTriangle className="w-12 h-12 text-orange-600" />,
      bg: 'bg-orange-50',
      border: 'border-orange-200',
      textColor: 'text-orange-900',
      buttonBg: 'bg-orange-600 hover:bg-orange-700',
    },
    info: {
      icon: <Info className="w-12 h-12 text-blue-600" />,
      bg: 'bg-blue-50',
      border: 'border-blue-200',
      textColor: 'text-blue-900',
      buttonBg: 'bg-blue-600 hover:bg-blue-700',
    },
    success: {
      icon: <CheckCircle className="w-12 h-12 text-green-600" />,
      bg: 'bg-green-50',
      border: 'border-green-200',
      textColor: 'text-green-900',
      buttonBg: 'bg-green-600 hover:bg-green-700',
    },
  };

  const style = variantStyles[variant];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-white rounded-lg shadow-xl max-w-md w-full mx-4 overflow-hidden">
        {/* Header */}
        <div className={`${style.bg} ${style.border} border-b p-6`}>
          <div className="flex items-start gap-4">
            {style.icon}
            <div>
              <h2 className={`text-xl font-bold ${style.textColor}`}>{title}</h2>
              <p className={`text-sm mt-2 ${style.textColor}/80`}>{message}</p>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="p-6 flex gap-3 justify-end bg-gray-50">
          <button
            onClick={onClose}
            disabled={isLoading}
            className="px-4 py-2 border rounded-md hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {cancelText}
          </button>
          <button
            onClick={onConfirm}
            disabled={isLoading}
            className={`px-4 py-2 text-white rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 ${style.buttonBg}`}
          >
            {isLoading && (
              <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
            )}
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
}
