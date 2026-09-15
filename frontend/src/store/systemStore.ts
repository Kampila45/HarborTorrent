import { create } from 'zustand';

interface SystemState {
  isUpdating: boolean;
  updateProgress: number;
  updateComplete: boolean;
  
  setIsUpdating: (isUpdating: boolean) => void;
  setUpdateProgress: (progress: number) => void;
  setUpdateComplete: (complete: boolean) => void;
}

export const useSystemStore = create<SystemState>((set) => ({
  isUpdating: false,
  updateProgress: 0,
  updateComplete: false,

  setIsUpdating: (isUpdating) => set({ isUpdating }),
  setUpdateProgress: (updateProgress) => set({ updateProgress }),
  setUpdateComplete: (updateComplete) => set({ updateComplete }),
}));
