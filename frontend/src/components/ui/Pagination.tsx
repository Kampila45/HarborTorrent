import { ChevronLeft, ChevronRight } from 'lucide-react';

export interface PaginationProps {
  currentPage: number;
  totalItems: number;
  itemsPerPage: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ currentPage, totalItems, itemsPerPage, onPageChange }: PaginationProps) {
  const totalPages = Math.ceil(totalItems / itemsPerPage);

  if (totalPages <= 1) {
    return null;
  }

  // Generate page numbers to show (max 5 pages visible around current page)
  const getPageNumbers = () => {
    const pages = [];
    let startPage = Math.max(1, currentPage - 2);
    let endPage = Math.min(totalPages, startPage + 4);

    if (endPage - startPage < 4) {
      startPage = Math.max(1, endPage - 4);
    }

    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }
    return pages;
  };

  const pages = getPageNumbers();

  return (
    <div className="flex items-center justify-between border-t border-[#E9E9E7] px-5 py-4 dark:border-[#333333] bg-white/50 dark:bg-[#1A1A1A]/50 backdrop-blur-md">
      <div className="text-[13px] font-medium text-[#5F5E5B] dark:text-[#C4C4C4]">
        Showing <span className="font-bold text-[#37352F] dark:text-[#E9E9E7]">{Math.min((currentPage - 1) * itemsPerPage + 1, totalItems)}</span> to{' '}
        <span className="font-bold text-[#37352F] dark:text-[#E9E9E7]">{Math.min(currentPage * itemsPerPage, totalItems)}</span> of{' '}
        <span className="font-bold text-[#37352F] dark:text-[#E9E9E7]">{totalItems}</span> results
      </div>

      <div className="flex items-center gap-1">
        <button
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage === 1}
          className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-[#5F5E5B] transition-colors hover:bg-[rgba(55,53,47,0.08)] disabled:pointer-events-none disabled:opacity-30 dark:text-[#C4C4C4] dark:hover:bg-[rgba(255,255,255,0.1)]"
          aria-label="Previous page"
        >
          <ChevronLeft size={16} />
        </button>

        {pages.map((page) => (
          <button
            key={page}
            onClick={() => onPageChange(page)}
            className={`inline-flex h-8 w-8 items-center justify-center rounded-lg text-[13px] font-semibold transition-all ${
              currentPage === page
                ? 'bg-[#2383E2] text-white shadow-sm dark:bg-[#2383E2]'
                : 'text-[#5F5E5B] hover:bg-[rgba(55,53,47,0.08)] dark:text-[#C4C4C4] dark:hover:bg-[rgba(255,255,255,0.1)]'
            }`}
          >
            {page}
          </button>
        ))}

        <button
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage === totalPages}
          className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-[#5F5E5B] transition-colors hover:bg-[rgba(55,53,47,0.08)] disabled:pointer-events-none disabled:opacity-30 dark:text-[#C4C4C4] dark:hover:bg-[rgba(255,255,255,0.1)]"
          aria-label="Next page"
        >
          <ChevronRight size={16} />
        </button>
      </div>
    </div>
  );
}
