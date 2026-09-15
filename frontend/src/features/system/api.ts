import axios from 'axios';

const api = axios.create({
  baseURL: 'http://127.0.0.1:5000/api/v1/system',
});

export interface VersionInfo {
  version: string;
  latestVersion: string;
}

export const systemApi = {
  getChangelog: async (): Promise<string> => {
    const response = await api.get<{ changelog: string }>('/changelog');
    return response.data.changelog;
  },

  getVersion: async (): Promise<VersionInfo> => {
    const response = await api.get<VersionInfo>('/version');
    return response.data;
  },

  triggerUpdate: async (): Promise<void> => {
    await api.post('/update');
  },
};
