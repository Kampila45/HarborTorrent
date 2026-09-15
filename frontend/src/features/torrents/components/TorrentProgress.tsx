import type { TorrentStatus } from '@/app/types';

export function TorrentProgress({ value, status }: { value: number; status: TorrentStatus }) {
  const fillClass =
    status === 'Completed'
      ? 'bg-[#448361]'
      : status === 'Paused'
        ? 'bg-[#ebecd0]'
        : status === 'Error'
          ? 'bg-[#D4403A]'
          : 'bg-[#2383E2]';

  return (
    <div className="flex items-center gap-2">
      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-[#E9E9E7] dark:bg-[#333333]" aria-hidden="true">
        <div className={`h-full rounded-full ${fillClass}`} style={{ width: `${value}%` }} />
      </div>
      <span className="w-10 text-right font-mono text-[12px] leading-4 text-[#37352F] dark:text-[#E9E9E7]">{value}%</span>
    </div>
  );
}