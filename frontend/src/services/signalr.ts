import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { getSessionConfig } from '@/services/session';

/**
 * Creates a SignalR hub connection for the given hub path.
 * The launch token is passed as a query parameter during the initial negotiation
 * request so the backend hub filter can authenticate the connection.
 */
function createHubConnection(hubPath: string) {
  const { apiBaseUrl, launchToken } = getSessionConfig();

  // Derive the hub base from the API base URL (strip /api/v1 suffix if present).
  const baseUrl = apiBaseUrl.replace(/\/api\/v1\/?$/, '');

  // In development, use a relative path to route through the Vite proxy.
  const hubUrl = import.meta.env.DEV
    ? hubPath
    : `${baseUrl}${hubPath}`;

  return new HubConnectionBuilder()
    .withUrl(hubUrl, {
      // Pass the token as a query parameter so the hub filter can validate it.
      accessTokenFactory: () => launchToken,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}

export function createTorrentHubConnection() {
  return createHubConnection('/hubs/torrents');
}

export function createSystemHubConnection() {
  return createHubConnection('/hubs/system');
}