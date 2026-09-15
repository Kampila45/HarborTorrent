import type { TorrentStatus } from '@/app/types';

const labels: Record<TorrentStatus, string> = {
  Queued: 'Queued',
  Downloading: 'Downloading',
  Paused: 'Paused',
  Stopped: 'Stopped',
  Completed: 'Completed',
  Error: 'Error',
  Seeding: 'Seeding',
};

const classNames: Record<TorrentStatus, string> = {
  Downloading: 'bg-[#D3E5EF] text-[#18445B]',
  Completed: 'bg-[#DBEDDB] text-[#1C4D2D]',
  Seeding: 'bg-[#DBEDDB] text-[#1C4D2D]',
  Paused: 'bg-[#EBECD0] text-[#37352F]',
  Queued: 'bg-[#EBECD0] text-[#37352F]',
  Error: 'bg-[#FFE2DD] text-[#5D1715]',
  Stopped: 'bg-[#EBECD0] text-[#37352F]',
};

export function TorrentStatusBadge({ status }: { status: TorrentStatus }) {
  return <span className={`inline-flex min-w-[88px] items-center justify-center rounded-full px-2 py-1 text-[12px] font-medium leading-4 ${classNames[status]}`}>{labels[status]}</span>;
}