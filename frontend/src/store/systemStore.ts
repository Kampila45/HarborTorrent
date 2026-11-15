import { create } from 'zustand';

export type SignalrConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

interface SystemState {
  isUpdating: boolean;
  updateProgress: number;
  updateComplete: boolean;
  updateError: string | null;
  signalrStatus: SignalrConnectionStatus;

  setIsUpdating: (isUpdating: boolean) => void;
  setUpdateProgress: (progress: number) => void;
  setUpdateComplete: (complete: boolean) => void;
  setUpdateError: (error: string | null) => void;
  setSignalrStatus: (status: SignalrConnectionStatus) => void;
}

export const useSystemStore = create<SystemState>((set) => ({
  isUpdating: false,
  updateProgress: 0,
  updateComplete: false,
  updateError: null,
  signalrStatus: 'connecting',

  setIsUpdating: (isUpdating) => set({ isUpdating }),
  setUpdateProgress: (updateProgress) => set({ updateProgress }),
  setUpdateComplete: (updateComplete) => set({ updateComplete }),
  setUpdateError: (updateError) => set({ updateError }),
  setSignalrStatus: (signalrStatus) => set({ signalrStatus }),
}));
