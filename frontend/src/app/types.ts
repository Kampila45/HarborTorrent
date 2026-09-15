export type ApiErrorItem = {
  code: string;
  message: string;
};

export type ApiResponse<T> = {
  success: boolean;
  data: T | null;
  errors: ApiErrorItem[];
  requestId: string;
};

export type TorrentStatus = 'Queued' | 'Downloading' | 'Paused' | 'Stopped' | 'Completed' | 'Error' | 'Seeding';

export type TorrentSourceKind = 'MagnetLink' | 'TorrentFile';

export type TorrentDto = {
  id: string;
  infoHash: string;
  name: string;
  status: TorrentStatus;
  progress: number;
  downloadedBytes: number;
  uploadedBytes: number;
  downloadSpeedBytesPerSecond: number;
  uploadSpeedBytesPerSecond: number;
  etaSeconds: number | null;
  savePath: string;
  addedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  ratio: number;
  errorMessage: string | null;
  sourceKind: TorrentSourceKind;
  sourceFileName: string | null;
};

export type DashboardMetric = {
  label: string;
  value: string;
  detail: string;
};

export type PagedResultDto<T> = {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
};

export type DashboardMetricsDto = {
  activeDownloads: number;
  completedTorrents: number;
  totalDownloadedBytes: number;
  totalUploadedBytes: number;
  totalDownloadSpeedBytesPerSecond: number;
  totalUploadSpeedBytesPerSecond: number;
};

export type TorrentFileDto = {
  index: number;
  path: string;
  length: number;
  downloadedBytes: number;
  priority: string;
};

export interface FilePriorityUpdateDto {
  fileIndex: number;
  priority: string;
}

export interface PeerDto {
  connectionUri: string;
  clientApp: string;
  downloadSpeedBytesPerSecond: number;
  uploadSpeedBytesPerSecond: number;
  isAmChoking: boolean;
  isAmInterested: boolean;
  isPeerChoking: boolean;
  isPeerInterested: boolean;
}

export interface TrackerDto {
  uri: string;
  status: string;
  seeders: number;
  leechers: number;
  warningMessage: string | null;
  failureMessage: string | null;
  lastAnnounceTime: string | null; // ISO TimeSpan string
}