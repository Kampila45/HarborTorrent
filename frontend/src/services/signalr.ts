import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

// Desktop build: connects without a token — no authentication required.
export function createTorrentHubConnection() {
  // In development, use the relative path to route through the Vite proxy (avoiding CORS/preflight issues).
  // In production (Tauri), the absolute backend URL must be used since Tauri doesn't proxy.
  const hubUrl = import.meta.env.DEV 
    ? '/hubs/torrents' 
    : 'http://localhost:5000/hubs/torrents';

  return new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}