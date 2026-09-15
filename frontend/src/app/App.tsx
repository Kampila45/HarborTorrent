import { useEffect } from 'react';
import { Route, Routes } from 'react-router-dom';
import { useUiStore } from '@/store/uiStore';
import { DashboardShell } from '@/features/layout/DashboardShell';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { DownloadsPage } from '@/features/downloads/DownloadsPage';
import { StatisticsPage } from '@/features/statistics/StatisticsPage';
import { CompletedPage } from '@/features/completed/CompletedPage';
import { TorrentDetailsPage } from '@/features/torrents/TorrentDetailsPage';
import { NotFoundPage } from '@/features/error/NotFoundPage';
import { InternalServerErrorPage } from '@/features/error/InternalServerErrorPage';
import { StreamPage } from '@/features/torrents/StreamPage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { SearchPage } from '@/features/search/SearchPage';
import { RssPage } from '@/features/rss/RssPage';
import { GlobalNotificationListener } from '@/app/GlobalNotificationListener';

import { Toaster } from 'sonner';

export function App() {
  const { theme } = useUiStore();

  useEffect(() => {
    if (theme === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }, [theme]);

  // Desktop build: no authentication required.
  // The app opens directly to the dashboard.
  return (
    <>
      <Toaster 
        position="bottom-right" 
        theme={theme === 'dark' ? 'dark' : 'light'} 
        toastOptions={{
          style: {
            background: 'var(--bg-primary)',
            color: 'var(--text)',
            border: '1px solid var(--border)',
          },
          className: 'font-sans'
        }}
      />
      <GlobalNotificationListener />
      <Routes>
        <Route element={<DashboardShell />}>
          <Route index element={<DashboardPage />} />
          <Route path="downloads" element={<DownloadsPage />} />
          <Route path="completed" element={<CompletedPage />} />
          <Route path="statistics" element={<StatisticsPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="search" element={<SearchPage />} />
          <Route path="rss" element={<RssPage />} />
          <Route path="torrent/:torrentId" element={<TorrentDetailsPage />} />
          <Route path="torrent/:torrentId/stream/:fileIndex" element={<StreamPage />} />
        </Route>
        <Route path="500" element={<InternalServerErrorPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </>
  );
}