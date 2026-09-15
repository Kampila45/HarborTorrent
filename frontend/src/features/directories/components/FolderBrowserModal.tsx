import { useState, useEffect } from 'react';
import { X, Folder, ChevronLeft, ChevronRight, ArrowUp, Loader2 } from 'lucide-react';
import { useDirectoriesQuery } from '../hooks/useDirectoriesQuery';


interface FolderBrowserModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (path: string) => void;
  initialPath?: string;
}

export function FolderBrowserModal({ isOpen, onClose, onSelect, initialPath }: FolderBrowserModalProps) {
  const [history, setHistory] = useState<(string | undefined)[]>([]);
  const [historyIndex, setHistoryIndex] = useState(-1);

  // Reset state when modal opens
  useEffect(() => {
    if (isOpen) {
      setHistory([initialPath || undefined]);
      setHistoryIndex(0);
    }
  }, [isOpen, initialPath]);

  const currentPath = history[historyIndex];

  const pushHistory = (path: string | undefined) => {
    if (path === currentPath) return;
    const newHistory = history.slice(0, historyIndex + 1);
    newHistory.push(path);
    setHistory(newHistory);
    setHistoryIndex(newHistory.length - 1);
  };

  const { data: directories, isLoading, isError } = useDirectoriesQuery(currentPath);

  if (!isOpen) return null;

  const navigateUp = () => {
    if (!currentPath) return;

    // Handle both Unix and Windows paths
    const separator = currentPath.includes('\\') ? '\\' : '/';
    const parts = currentPath.split(separator).filter(Boolean);
    
    if (parts.length <= 1) {
      // If at the root of a drive (e.g. C:) or root (/), going up means showing all drives/roots
      pushHistory(undefined);
    } else {
      parts.pop();
      const parentPath = currentPath.startsWith(separator) 
        ? separator + parts.join(separator) 
        : parts.join(separator) + (parts.length === 1 && currentPath.includes(':') ? separator : '');
      pushHistory(parentPath);
    }
  };

  const goBack = () => {
    if (historyIndex > 0) setHistoryIndex(historyIndex - 1);
  };

  const goForward = () => {
    if (historyIndex < history.length - 1) setHistoryIndex(historyIndex + 1);
  };

  const handleSelect = () => {
    if (currentPath) {
      onSelect(currentPath);
      onClose();
    }
  };

  return (
    <div 
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
      onClick={(e) => {
        e.stopPropagation();
        onClose();
      }}
    >
      <div 
        className="w-full max-w-lg overflow-hidden rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] shadow-xl flex flex-col max-h-[80vh]"
        role="dialog"
        aria-modal="true"
        aria-labelledby="folder-browser-title"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-[#E9E9E7] dark:border-[#333333] px-5 py-4">
          <h2 id="folder-browser-title" className="text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">
            Select Folder
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-[rgba(55,53,47,0.45)] dark:text-[#666666] transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A] hover:text-[#37352F] dark:hover:text-[#E9E9E7]"
          >
            <X size={18} />
          </button>
        </div>

        {/* Path navigation */}
        <div className="bg-[#F7F7F5] dark:bg-[#1A1A1A] px-4 py-3 border-b border-[#E9E9E7] dark:border-[#333333] flex items-center gap-2">
          <div className="flex items-center gap-0.5">
            <button
              type="button"
              onClick={goBack}
              disabled={historyIndex <= 0}
              className="rounded p-1.5 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-[#E9E9E7] dark:hover:bg-[#2A2A2A] hover:text-[#37352F] dark:hover:text-[#E9E9E7] disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[#5F5E5B] dark:disabled:hover:text-[#8E8D8A]"
              title="Back"
            >
              <ChevronLeft size={18} />
            </button>
            <button
              type="button"
              onClick={goForward}
              disabled={historyIndex >= history.length - 1}
              className="rounded p-1.5 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-[#E9E9E7] dark:hover:bg-[#2A2A2A] hover:text-[#37352F] dark:hover:text-[#E9E9E7] disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[#5F5E5B] dark:disabled:hover:text-[#8E8D8A]"
              title="Forward"
            >
              <ChevronRight size={18} />
            </button>
            <button
              type="button"
              onClick={navigateUp}
              disabled={!currentPath}
              className="rounded p-1.5 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-[#E9E9E7] dark:hover:bg-[#2A2A2A] hover:text-[#37352F] dark:hover:text-[#E9E9E7] disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[#5F5E5B] dark:disabled:hover:text-[#8E8D8A]"
              title="Up one folder"
            >
              <ArrowUp size={18} />
            </button>
          </div>
          <div className="flex-1 font-mono text-[13px] text-[#37352F] dark:text-[#E9E9E7] overflow-hidden text-ellipsis whitespace-nowrap bg-white dark:bg-[#111111] border border-[#E9E9E7] dark:border-[#333333] rounded px-3 py-1.5">
            {currentPath || 'Root / This PC'}
          </div>
        </div>

        {/* Directory List */}
        <div className="flex-1 overflow-y-auto p-2">
          {isLoading ? (
            <div className="flex items-center justify-center p-8 text-[rgba(55,53,47,0.45)] dark:text-[#E9E9E7]">
              <Loader2 className="animate-spin" size={24} />
            </div>
          ) : isError ? (
            <div className="p-4 text-center text-red-500 text-[14px]">
              Failed to load directories. The path might be inaccessible.
            </div>
          ) : !directories || directories.length === 0 ? (
            <div className="p-8 text-center text-[14px] text-[rgba(55,53,47,0.45)] dark:text-[#E9E9E7]">
              No folders found.
            </div>
          ) : (
            <ul className="space-y-0.5">
              {currentPath && (
                <li>
                  <button
                    type="button"
                    onClick={navigateUp}
                    className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-left transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A]"
                  >
                    <Folder size={18} className="text-[#2383E2] dark:text-[#52a8ff]" fill="currentColor" fillOpacity={0.2} />
                    <span className="truncate text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                      ..
                    </span>
                  </button>
                </li>
              )}
              {directories.map((dir) => (
                <li key={dir.path}>
                  <button
                    type="button"
                    onClick={() => pushHistory(dir.path)}
                    className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-left transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A]"
                  >
                    <Folder size={18} className="text-[#2383E2] dark:text-[#52a8ff]" fill="currentColor" fillOpacity={0.2} />
                    <span className="truncate text-[14px] text-[#37352F] dark:text-[#E9E9E7]">
                      {dir.name}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-[#E9E9E7] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A] px-5 py-4 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-[4px] border border-[#DCDAD7] dark:border-[#444444] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#EDEDEB] dark:hover:bg-[#333333] active:scale-[0.98]"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleSelect}
            disabled={!currentPath}
            className="rounded bg-[#2383E2] px-4 py-2 text-[14px] font-medium text-white shadow-sm transition-opacity hover:opacity-90 disabled:opacity-50"
          >
            Select this folder
          </button>
        </div>
      </div>
    </div>
  );
}
