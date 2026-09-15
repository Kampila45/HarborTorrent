import { create } from 'zustand';
import { persist } from 'zustand/middleware';

type UiState = {
  theme: 'light' | 'dark';
  sidebarOpen: boolean;
  mobileNavOpen: boolean;
  addTorrentOpen: boolean;
  addTorrentInitialValue: string | null;
  setTheme: (theme: 'light' | 'dark') => void;
  setSidebarOpen: (value: boolean) => void;
  toggleSidebar: () => void;
  toggleMobileNav: () => void;
  closeMobileNav: () => void;
  openAddTorrent: (initialValue?: string) => void;
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
      setTheme: (theme) => set({ theme }),
      setSidebarOpen: (value) => set({ sidebarOpen: value }),
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
      toggleMobileNav: () => set((state) => ({ mobileNavOpen: !state.mobileNavOpen })),
      closeMobileNav: () => set({ mobileNavOpen: false }),
      openAddTorrent: (initialValue) => set({ addTorrentOpen: true, addTorrentInitialValue: initialValue ?? null }),
      closeAddTorrent: () => set({ addTorrentOpen: false, addTorrentInitialValue: null }),
    }),
    {
      name: 'ui-storage',
      partialize: (state) => ({ theme: state.theme, sidebarOpen: state.sidebarOpen }),
    }
  )
);