export function PageSkeleton() {
  return (
    <div className="flex w-full flex-col gap-4 p-8">
      <div className="h-10 w-1/4 animate-pulse rounded bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)]" />
      <div className="h-6 w-1/3 animate-pulse rounded bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)]" />
      <div className="mt-8 flex flex-col gap-2">
        <div className="h-12 w-full animate-pulse rounded bg-[rgba(55,53,47,0.04)] dark:bg-[rgba(255,255,255,0.04)]" />
        <div className="h-12 w-full animate-pulse rounded bg-[rgba(55,53,47,0.04)] dark:bg-[rgba(255,255,255,0.04)]" />
        <div className="h-12 w-full animate-pulse rounded bg-[rgba(55,53,47,0.04)] dark:bg-[rgba(255,255,255,0.04)]" />
      </div>
    </div>
  );
}
