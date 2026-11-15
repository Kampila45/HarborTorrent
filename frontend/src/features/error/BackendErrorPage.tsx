import { useRef, useState } from 'react';
import { Check, Copy, RefreshCw } from 'lucide-react';

interface BackendErrorPageProps {
  errorReason?: string | null;
  onRetry?: () => void;
}

export function BackendErrorPage({ errorReason, onRetry }: BackendErrorPageProps) {
  const imgRef = useRef<HTMLImageElement>(null);
  const [restarting, setRestarting] = useState(false);
  const [restartError, setRestartError] = useState<string | null>(null);
  const [copiedDiagnostics, setCopiedDiagnostics] = useState(false);

  const handleCopyDiagnostics = async () => {
    try {
      const lines: string[] = [
        `Reason: ${errorReason ?? 'Unknown backend startup failure'}`,
        `UserAgent: ${navigator.userAgent}`,
      ];
      if ('__TAURI_INTERNALS__' in window) {
        const { invoke } = await import('@tauri-apps/api/core');
        const logs = await invoke<string[]>('get_sidecar_diagnostics');
        if (logs.length > 0) {
          lines.push('--- Sidecar Logs ---', ...logs);
        }
      }
      await navigator.clipboard.writeText(lines.join('\n'));
      setCopiedDiagnostics(true);
      window.setTimeout(() => setCopiedDiagnostics(false), 2500);
    } catch {
      // Ignore clipboard failures in restricted contexts
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLElement>) => {
    if (!imgRef.current) return;
    const xAxis = (window.innerWidth / 2 - e.pageX) / 45;
    const yAxis = (window.innerHeight / 2 - e.pageY) / 45;
    imgRef.current.style.transform = `rotateY(${xAxis}deg) rotateX(${yAxis}deg) translateY(-10px)`;
  };

  const handleMouseLeave = () => {
    if (!imgRef.current) return;
    imgRef.current.style.transform = `rotateY(0deg) rotateX(0deg) translateY(0)`;
  };

  const handleRestart = async () => {
    setRestarting(true);
    setRestartError(null);
    try {
      if ('__TAURI_INTERNALS__' in window) {
        const { invoke } = await import('@tauri-apps/api/core');
        await invoke('restart_api_sidecar');
      }

      if (onRetry) {
        onRetry();
      } else {
        window.location.reload();
      }
    } catch (err) {
      setRestartError(err instanceof Error ? err.message : String(err));
      setRestarting(false);
    }
  };

  return (
    <section
      className="flex min-h-0 flex-1 flex-col items-center justify-center relative overflow-hidden bg-[#fef9ef] dark:bg-[var(--bg-primary)]"
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      style={{ minHeight: '100vh', width: '100vw' }}
    >
      <style>
        {`
          @keyframes fadeInUp {
            from {
              opacity: 0;
              transform: translateY(20px);
            }
            to {
              opacity: 1;
              transform: translateY(0);
            }
          }
          .animate-fade-in-up {
            animation: fadeInUp 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
          }
        `}
      </style>

      {/* Subtle Atmospheric Background Circles */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-[#005da9]/5 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-0 right-0 w-[400px] h-[400px] bg-[#93000a]/5 rounded-full blur-[80px] pointer-events-none"></div>

      <div className="max-w-2xl w-full text-center relative z-10 animate-fade-in-up px-6">
        {/* Error Illustration Container */}
        <div className="mb-10 flex justify-center">
          <div className="relative group perspective-1000">
            {/* Glow effect */}
            <div className="absolute inset-0 bg-[#93000a]/10 rounded-full blur-3xl scale-125 group-hover:scale-150 transition-transform duration-700"></div>
            <img
              ref={imgRef}
              className="relative w-72 h-72 md:w-96 md:h-96 object-contain transform group-hover:translate-y-[-10px] transition-transform duration-500 ease-out"
              src="/engine-failure-illustration.png"
              style={{ filter: 'drop-shadow(0 20px 30px rgba(0,0,0,0.08))' }}
              alt="Backend Error Anchor"
            />
          </div>
        </div>

        {/* Typography & Messaging */}
        <div className="space-y-4">
          <h2 className="text-[24px] leading-[32px] tracking-[-0.02em] font-semibold text-[#D4403A]">Engine Failure</h2>
          <h3 className="text-[18px] leading-[24px] tracking-[-0.01em] font-semibold text-[#1d1c16] dark:text-[#E9E9E7]">The harbor engine room is unresponsive</h3>
          <p className="text-[15px] leading-[24px] font-normal text-[#5F5E5B] dark:text-[#E9E9E7] max-w-md mx-auto">
            {errorReason ?? 'The background engine did not respond in time. This usually happens if another HarborTorrent instance is already running or a system component was blocked.'}
          </p>
          {restartError && (
            <p className="text-[13px] text-[#D4403A] max-w-md mx-auto">{restartError}</p>
          )}
        </div>

        {/* Actions */}
        <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
          <button
            type="button"
            disabled={restarting}
            className="w-full sm:w-auto px-6 py-3 border border-transparent bg-[#37352F] dark:bg-[#E9E9E7] text-white dark:text-[#111111] rounded-lg font-medium text-[12px] flex items-center justify-center gap-2 shadow-sm hover:opacity-90 active:scale-95 transition-all disabled:opacity-60"
            onClick={handleRestart}
          >
            <RefreshCw size={18} className={restarting ? 'animate-spin' : ''} />
            {restarting ? 'Restarting engine…' : 'Try starting again'}
          </button>
          <button
            type="button"
            className="w-full sm:w-auto px-5 py-3 border border-[rgba(55,53,47,0.2)] dark:border-[#444444] bg-white dark:bg-[#1A1A1A] text-[#37352F] dark:text-[#E9E9E7] rounded-lg font-medium text-[12px] flex items-center justify-center gap-2 hover:bg-[#F7F7F5] dark:hover:bg-[#252525] transition-all"
            onClick={handleCopyDiagnostics}
          >
            {copiedDiagnostics ? <Check size={16} className="text-[#0B6E4F]" /> : <Copy size={16} />}
            {copiedDiagnostics ? 'Diagnostics copied' : 'Copy diagnostics'}
          </button>
        </div>
      </div>
    </section>
  );
}
