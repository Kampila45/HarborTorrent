import { Search, Download, HelpCircle } from 'lucide-react';
import { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { searchApi } from './api';
import { useUiStore } from '@/store/uiStore';
import { formatBytes } from '@/utils/formatters';
import { Pagination } from '@/components/Pagination';

export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialQuery = searchParams.get('q') ?? '';
  const [localQuery, setLocalQuery] = useState(initialQuery);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 15;

  const { data: results, isLoading } = useQuery({
    queryKey: ['search', initialQuery],
    queryFn: () => searchApi.searchTorrents(initialQuery),
    enabled: initialQuery.length > 0,
  });

  const { openAddTorrent } = useUiStore();

  // Search-as-you-type with debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      if (localQuery.trim() !== initialQuery) {
        setSearchParams(localQuery.trim() ? { q: localQuery.trim() } : {});
        setCurrentPage(1);
      }
    }, 500);

    return () => clearTimeout(timer);
  }, [localQuery, initialQuery, setSearchParams]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // Handled by debounce, but retained to prevent default form submission reload
  };

  const paginatedResults = results?.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-hidden bg-white dark:bg-[#111111] px-4 sm:px-8 md:px-10 pb-10 pt-6">
      
      {/* Premium Hero Section */}
      <div className="relative mb-6 shrink-0 overflow-hidden rounded-3xl bg-gradient-to-br from-[#2383E2] via-[#3b82f6] to-[#60A5FA] px-8 py-14 text-white shadow-xl dark:from-[#0f172a] dark:via-[#1e3a8a] dark:to-[#3b82f6]">
        {/* Glass overlay and decorative shapes */}
        <div className="absolute inset-0 bg-white/5 mix-blend-overlay backdrop-blur-3xl"></div>
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-white/10 blur-3xl"></div>
        <div className="absolute -bottom-20 -right-20 h-80 w-80 rounded-full bg-[#1e3a8a]/30 blur-3xl"></div>
        
        <div className="relative z-10 flex flex-col items-center text-center">
          <h1 className="mb-3 text-4xl font-extrabold tracking-tight md:text-5xl">Search the Seas</h1>
          <p className="mb-8 text-lg font-medium text-white/90 drop-shadow-sm">Find and download torrents instantly from public trackers.</p>

          <form onSubmit={handleSearchSubmit} className="flex w-full max-w-2xl items-center gap-3 rounded-full border border-white/20 bg-white/10 p-2 shadow-[0_8px_32px_rgba(0,0,0,0.12)] backdrop-blur-xl transition-all duration-300 focus-within:bg-white/20 focus-within:shadow-[0_8px_32px_rgba(0,0,0,0.25)] hover:bg-white/15">
            <Search className="ml-4 shrink-0 text-white/80" size={22} />
            <input
              type="search"
              value={localQuery}
              onChange={(e) => setLocalQuery(e.target.value)}
              placeholder="Search for movies, games, software..."
              className="w-full bg-transparent px-2 py-3 text-lg font-medium text-white placeholder:text-white/60 outline-none"
            />
            <button
              type="submit"
              disabled={isLoading || !localQuery.trim()}
              className="shrink-0 rounded-full bg-white px-8 py-3 font-bold tracking-wide text-[#2383E2] shadow-sm transition-all hover:scale-105 hover:shadow-md active:scale-95 disabled:pointer-events-none disabled:opacity-70 dark:text-[#1e3a8a]"
            >
              {isLoading ? 'Searching...' : 'Search'}
            </button>
          </form>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto rounded-xl border border-[#E9E9E7] bg-white shadow-sm dark:border-[#333333] dark:bg-[#1A1A1A]">
        {!initialQuery ? (
          <div className="flex h-full flex-col items-center justify-center p-12 text-center opacity-70 transition-opacity hover:opacity-100">
            <div className="mb-5 flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-br from-[#F7F7F5] to-[#EDEDEB] shadow-inner dark:from-[#2A2A2A] dark:to-[#111111]">
              <Search className="text-[#37352F] dark:text-[#E9E9E7]" size={36} />
            </div>
            <h3 className="text-xl font-bold text-[#37352F] dark:text-[#E9E9E7]">Ready to Explore</h3>
            <p className="mt-2 max-w-sm text-[15px] leading-relaxed text-[#5F5E5B] dark:text-[#C4C4C4]">Enter a query above to search global public trackers directly from HarborTorrent.</p>
          </div>
        ) : results && results.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center p-12 text-center">
            <div className="mb-5 flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-br from-[#F7F7F5] to-[#EDEDEB] shadow-inner dark:from-[#2A2A2A] dark:to-[#111111]">
              <HelpCircle className="text-[#8A8987] dark:text-[#E9E9E7]/40" size={36} />
            </div>
            <h3 className="text-xl font-bold text-[#37352F] dark:text-[#E9E9E7]">No results found</h3>
            <p className="mt-2 max-w-sm text-[15px] leading-relaxed text-[#5F5E5B] dark:text-[#C4C4C4]">We couldn't find any torrents matching "{initialQuery}".</p>
          </div>
        ) : (
          <div className="flex-1 overflow-y-auto bg-[#F7F7F5] dark:bg-[#0A0A0A] p-4 md:p-6 lg:p-8">
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {isLoading ? (
                Array.from({ length: 12 }).map((_, idx) => (
                  <div key={idx} className="flex h-[180px] flex-col justify-between rounded-2xl border border-[#E9E9E7] bg-white p-5 shadow-sm dark:border-[#333333] dark:bg-[#1A1A1A]">
                    <div className="animate-pulse">
                      <div className="mb-2 h-5 w-3/4 rounded bg-[#E9E9E7] dark:bg-[#333333]"></div>
                      <div className="h-4 w-1/2 rounded bg-[#E9E9E7] dark:bg-[#333333]"></div>
                    </div>
                    <div className="mt-auto flex animate-pulse items-center justify-between">
                      <div className="flex gap-2">
                         <div className="h-8 w-10 rounded-lg bg-[#E9E9E7] dark:bg-[#333333]"></div>
                         <div className="h-8 w-10 rounded-lg bg-[#E9E9E7] dark:bg-[#333333]"></div>
                      </div>
                      <div className="h-9 w-20 rounded-xl bg-[#E9E9E7] dark:bg-[#333333]"></div>
                    </div>
                  </div>
                ))
              ) : (
                paginatedResults?.map((result, idx) => (
                  <div key={idx} className="group relative flex flex-col justify-between overflow-hidden rounded-2xl border border-[rgba(55,53,47,0.1)] bg-white/70 p-5 shadow-[0_2px_10px_rgba(0,0,0,0.02)] backdrop-blur-xl transition-all duration-300 hover:-translate-y-1 hover:shadow-[0_8px_30px_rgba(0,0,0,0.08)] dark:border-[#333333] dark:bg-[#1A1A1A]/70 dark:hover:border-[#444444] dark:hover:shadow-[0_8px_30px_rgba(0,0,0,0.2)]">
                    <div className="mb-4">
                      <h3 className="mb-2 line-clamp-2 text-[15px] font-bold leading-snug text-[#37352F] dark:text-[#E9E9E7]" title={result.title}>
                        {result.title}
                      </h3>
                      <div className="flex items-center gap-2">
                        <span className="inline-flex items-center rounded-md bg-[rgba(55,53,47,0.06)] px-2 py-0.5 text-[11px] font-semibold text-[#4B4A47] dark:bg-[rgba(255,255,255,0.1)] dark:text-[#E9E9E7]">
                          {formatBytes(result.sizeBytes)}
                        </span>
                        <span className="inline-flex items-center rounded-md border border-[rgba(55,53,47,0.08)] bg-transparent px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-[#73726F] dark:border-[#333333] dark:text-[#C4C4C4]">
                          {result.providerName}
                        </span>
                      </div>
                    </div>
                    
                    <div className="mt-auto flex items-end justify-between border-t border-[rgba(55,53,47,0.06)] pt-4 dark:border-[rgba(255,255,255,0.06)]">
                      <div className="flex items-center gap-4">
                        <div className="flex flex-col">
                          <span className="text-[10px] font-bold uppercase tracking-widest text-[#8A8987] dark:text-[#C4C4C4]/60">Seed</span>
                          <span className="text-[15px] font-black text-green-600 dark:text-green-400">{result.seeders}</span>
                        </div>
                        <div className="flex flex-col">
                          <span className="text-[10px] font-bold uppercase tracking-widest text-[#8A8987] dark:text-[#C4C4C4]/60">Leech</span>
                          <span className="text-[15px] font-black text-red-500 dark:text-red-400">{result.leechers}</span>
                        </div>
                      </div>
                      
                      <button
                        type="button"
                        onClick={() => openAddTorrent(result.magnetUri)}
                        className="inline-flex h-9 items-center gap-1.5 rounded-xl bg-[#2383E2] px-4 text-[13px] font-bold text-white shadow-sm transition-all hover:scale-105 hover:bg-[#1a73cc] hover:shadow-md active:scale-95"
                      >
                        <Download size={16} />
                        Get
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
            {results && results.length > itemsPerPage && (
              <Pagination
                currentPage={currentPage}
                totalPages={Math.ceil(results.length / itemsPerPage)}
                onPageChange={setCurrentPage}
              />
            )}
          </div>
        )}
      </div>
    </section>
  );
}
