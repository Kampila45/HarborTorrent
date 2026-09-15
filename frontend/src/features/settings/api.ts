import { httpClient } from '@/services/http';

export interface SettingsDto {
  maxDownloadSpeedBytes: number;
  maxUploadSpeedBytes: number;
  defaultDownloadPath: string;
  enableDht: boolean;
  enablePex: boolean;
  enableLpd: boolean;
  anchorModeEnabled: boolean;
  anchorMaxDownloadSpeedBytes: number;
  anchorMaxUploadSpeedBytes: number;
}

export const settingsApi = {
  getSettings: async (): Promise<SettingsDto> => {
    const response = await httpClient.get<SettingsDto>('/settings');
    return response.data;
  },

  updateSettings: async (settings: SettingsDto): Promise<void> => {
    await httpClient.put('/settings', settings);
  }
};
