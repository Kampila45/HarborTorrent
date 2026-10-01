import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { formatBytes } from '@/utils/formatters';
import { PageSkeleton } from '@/components/PageSkeleton';

export function StatisticsPage() {
  const { data, isLoading, isError } = useTorrentsQuery();

  if (isLoading || isError || !data) {
    return <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6"><PageSkeleton /></section>;
  }

  const totals = [
    { label: 'Total torrents', value: String(data.items.length) },
    { label: 'Completed torrents', value: String(data.items.filter((item) => item.status === 'Completed').length) },
    { label: 'Downloaded', value: formatBytes(data.items.reduce((total, item) => total + item.downloadedBytes, 0)) },
    { label: 'Uploaded', value: formatBytes(data.items.reduce((total, item) => total + item.uploadedBytes, 0)) },
  ];

  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
      <div className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] p-5 shadow-sm">
        <div className="flex items-center justify-between">
        <div>
            <p className="m-0 text-[11px] font-semibold uppercase tracking-[0.12em] text-[#5F5E5B] dark:text-[#E9E9E7]">Statistics</p>
            <h1 className="m-0 text-[28px] font-bold leading-tight tracking-[-0.01em] text-[#37352F] dark:text-[#E9E9E7]">Activity overview</h1>
          </div>
        </div>

        <div className="mt-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
          {totals.map((item) => (
            <article key={item.label} className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] p-[18px]">
              <p className="m-0 text-[11.5px] font-semibold uppercase tracking-[0.08em] text-[#5F5E5B] dark:text-[#E9E9E7]">{item.label}</p>
              <h3 className="m-0 mt-2.5 text-[26px] font-bold text-[#37352F] dark:text-[#E9E9E7]">{item.value}</h3>
            </article>
          ))}
        </div>

        <div className="mt-6 grid grid-cols-1 gap-5 lg:grid-cols-2">
          <section className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] p-[18px]">
            <p className="m-0 text-[11px] font-semibold uppercase tracking-[0.12em] text-[#5F5E5B] dark:text-[#E9E9E7]">History</p>
            <h2 className="m-0 text-[18px] font-bold text-[#37352F] dark:text-[#E9E9E7]">Download history</h2>
            <div className="mt-4 flex h-[180px] items-center justify-center text-[13px] text-[#5F5E5B] dark:text-[#C4C4C4]">
              Historical data collection is not yet available.
            </div>
          </section>

          <section className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] p-[18px]">
            <p className="m-0 text-[11px] font-semibold uppercase tracking-[0.12em] text-[#5F5E5B] dark:text-[#E9E9E7]">History</p>
            <h2 className="m-0 text-[18px] font-bold text-[#37352F] dark:text-[#E9E9E7]">Upload history</h2>
            <div className="mt-4 flex h-[180px] items-center justify-center text-[13px] text-[#5F5E5B] dark:text-[#C4C4C4]">
              Historical data collection is not yet available.
            </div>
          </section>
        </div>
      </div>
    </section>
  );
}