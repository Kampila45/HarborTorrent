import { create } from 'zustand';
import { persist } from 'zustand/middleware';

type UiState = {
  theme: 'light' | 'dark';
  sidebarOpen: boolean;
  mobileNavOpen: boolean;
  addTorrentOpen: boolean;
  addTorrentInitialValue: string | null;
  addTorrentInitialFile: { name: string; base64: string } | null;
  setTheme: (theme: 'light' | 'dark') => void;
  setSidebarOpen: (value: boolean) => void;
  toggleSidebar: () => void;
  toggleMobileNav: () => void;
  closeMobileNav: () => void;
  openAddTorrent: (initialValue?: string, initialFile?: { name: string; base64: string }) => void;
  closeAddTorrent: () => void;
};

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'light',
      sidebarOpen: true,
      mobileNavOpen: false,
      addTorrentOpen: false,
      addTorrentInitialValue: null,
      addTorrentInitialFile: null,
      setTheme: (theme) => set({ theme }),
      setSidebarOpen: (value) => set({ sidebarOpen: value }),
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
      toggleMobileNav: () => set((state) => ({ mobileNavOpen: !state.mobileNavOpen })),
      closeMobileNav: () => set({ mobileNavOpen: false }),
      openAddTorrent: (initialValue, initialFile) => set({ addTorrentOpen: true, addTorrentInitialValue: initialValue ?? null, addTorrentInitialFile: initialFile ?? null }),
      closeAddTorrent: () => set({ addTorrentOpen: false, addTorrentInitialValue: null, addTorrentInitialFile: null }),
    }),
    {
      name: 'ui-storage',
      partialize: (state) => ({ theme: state.theme, sidebarOpen: state.sidebarOpen }),
    }
  )
);