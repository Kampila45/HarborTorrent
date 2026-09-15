import { Trash2, X } from 'lucide-react';
import { useEffect } from 'react';

interface ConfirmDeleteModalProps {
  isOpen: boolean;
  torrentName?: string;
  title?: string;
  message?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDeleteModal({ isOpen, torrentName, title, message, onConfirm, onCancel }: ConfirmDeleteModalProps) {
  // Close on Escape key
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onCancel(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isOpen, onCancel]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-modal-title"
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-[rgba(55,53,47,0.22)] backdrop-blur-md"
        onClick={onCancel}
      />

      {/* Modal */}
      <div className="relative z-10 w-full max-w-md rounded-xl border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#1A1A1A] shadow-2xl p-6">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-[#FFE2DD] dark:bg-[#3D1A18]">
              <Trash2 size={18} className="text-[#D4403A]" />
            </div>
            <h2 id="delete-modal-title" className="text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">
              {title ?? 'Remove Torrent'}
            </h2>
          </div>
          <button
            type="button"
            onClick={onCancel}
            className="rounded p-1 text-[#73726F] dark:text-[#787774] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            aria-label="Close"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <p className="text-[14px] text-[#5F5E5B] dark:text-[#C4C4C4] mb-1">
          {message ?? 'Are you sure you want to remove this torrent?'}
        </p>
        <p className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7] truncate mb-6">
          {torrentName}
        </p>

        {/* Actions */}
        <div className="flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onCancel}
            className="px-4 py-2 rounded-lg text-[13px] font-medium border border-[var(--border-strong)] bg-[#F7F7F5] dark:bg-[#2A2A2A] text-[#37352F] dark:text-[#E9E9E7] hover:bg-[#EDEDEB] dark:hover:bg-[#333333] active:scale-95 transition-all"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className="px-4 py-2 rounded-lg text-[13px] font-medium bg-[#D4403A] text-white hover:bg-[#b83530] active:scale-95 transition-all"
          >
            Remove
          </button>
        </div>
      </div>
    </div>
  );
}
