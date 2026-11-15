import { httpClient } from '@/services/http';

export interface VersionInfo {
  version: string;
  latestVersion?: string;
}

export const systemApi = {
  getChangelog: async (): Promise<string> => {
    const response = await httpClient.get<{ changelog: string }>('/system/changelog');
    return response.data.changelog;
  },

  getVersion: async (): Promise<VersionInfo> => {
    const response = await httpClient.get<VersionInfo>('/system/version');
    return response.data;
  },
};
