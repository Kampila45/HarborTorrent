import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type AddTorrentMetadata = {
  title?: string;
  sizeBytes?: number;
  providerName?: string;
};

type UiState = {
  theme: 'light' | 'dark';
  sidebarOpen: boolean;
  mobileNavOpen: boolean;
  hasCompletedOnboarding: boolean;
  onboardingOpen: boolean;
  addTorrentOpen: boolean;
  addTorrentInitialValue: string | null;
  addTorrentInitialFile: { name: string; base64: string } | null;
  addTorrentMetadata: AddTorrentMetadata | null;
  setTheme: (theme: 'light' | 'dark') => void;
  setSidebarOpen: (value: boolean) => void;
  toggleSidebar: () => void;
  toggleMobileNav: () => void;
  closeMobileNav: () => void;
  openOnboarding: () => void;
  closeOnboarding: () => void;
  completeOnboarding: () => void;
  openAddTorrent: (
    initialValue?: string,
    initialFile?: { name: string; base64: string },
    metadata?: AddTorrentMetadata
  ) => void;
  closeAddTorrent: () => void;
};

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'light',
      sidebarOpen: true,
      mobileNavOpen: false,
      hasCompletedOnboarding: false,
      onboardingOpen: false,
      addTorrentOpen: false,
      addTorrentInitialValue: null,
      addTorrentInitialFile: null,
      addTorrentMetadata: null,
      setTheme: (theme) => set({ theme }),
      setSidebarOpen: (value) => set({ sidebarOpen: value }),
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
      toggleMobileNav: () => set((state) => ({ mobileNavOpen: !state.mobileNavOpen })),
      closeMobileNav: () => set({ mobileNavOpen: false }),
      openOnboarding: () => set({ onboardingOpen: true }),
      closeOnboarding: () => set({ onboardingOpen: false }),
      completeOnboarding: () => set({ hasCompletedOnboarding: true, onboardingOpen: false }),
      openAddTorrent: (initialValue, initialFile, metadata) =>
        set({
          addTorrentOpen: true,
          addTorrentInitialValue: initialValue ?? null,
          addTorrentInitialFile: initialFile ?? null,
          addTorrentMetadata: metadata ?? null,
        }),
      closeAddTorrent: () =>
        set({
          addTorrentOpen: false,
          addTorrentInitialValue: null,
          addTorrentInitialFile: null,
          addTorrentMetadata: null,
        }),
    }),
    {
      name: 'ui-storage',
      partialize: (state) => ({
        theme: state.theme,
        sidebarOpen: state.sidebarOpen,
        hasCompletedOnboarding: state.hasCompletedOnboarding,
      }),
    }
  )
);