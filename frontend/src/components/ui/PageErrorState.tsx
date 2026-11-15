import { AlertCircle, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/Button';

type PageErrorStateProps = {
  message: string;
  description?: string | undefined;
  onRetry?: (() => void) | undefined;
  retryLabel?: string | undefined;
  tone?: 'error' | 'muted' | undefined;
  actions?: ReactNode | undefined;
  className?: string | undefined;
};

/**
 * Shared centered page error state keeping icon, typography, and retry action consistent across views.
 */
export function PageErrorState({
  message,
  description,
  onRetry,
  retryLabel = 'Retry',
  tone = 'error',
  actions,
  className = '',
}: PageErrorStateProps) {
  return (
    <div
      className={`flex min-h-[60vh] flex-1 flex-col items-center justify-center px-6 py-20 text-center ${className}`.trim()}
    >
      <AlertCircle
        size={36}
        className={`mb-3 ${tone === 'error' ? 'text-[#D4403A]' : 'text-[#73726F]'}`}
      />
      <p className="text-[15px] font-medium text-[#37352F] dark:text-[#E9E9E7]">{message}</p>
      {description ? (
        <p className="mt-1 max-w-md text-[13px] text-[#5F5E5B] dark:text-[#C4C4C4]">{description}</p>
      ) : null}
      {actions ? (
        <div className="mt-4 flex flex-wrap items-center justify-center gap-2">{actions}</div>
      ) : onRetry ? (
        <Button onClick={onRetry} className="mt-4">
          <RefreshCw size={14} />
          {retryLabel}
        </Button>
      ) : null}
    </div>
  );
}
