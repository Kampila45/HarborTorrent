import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import { BarChart3, CheckSquare, Download, LayoutDashboard, Menu, Moon, Plus, Search, Sun, X, Settings, Anchor, Rss } from 'lucide-react';
import { AddTorrentModal } from '@/features/torrents/components/AddTorrentModal';
import { useUiStore } from '@/store/uiStore';
import { useTorrentSignalR } from '@/features/torrents/hooks/useTorrentSignalR';
import { useSystemSignalR } from '@/features/system/hooks/useSystemSignalR';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { settingsApi } from '@/features/settings/api';

const navigationItems = [
  { label: 'Dashboard', navLabel: 'Dashboard', path: '/' },
  { label: 'Search', navLabel: 'Search', path: '/search' },
  { label: 'Downloads', navLabel: 'Downloads', path: '/downloads' },
  { label: 'Completed', navLabel: 'Completed', path: '/completed' },
  { label: 'Automation', navLabel: 'Automation', path: '/rss' },
  { label: 'Settings', navLabel: 'Settings', path: '/settings' },
] as const;

const routeIcons: Record<string, LucideIcon> = {
  Dashboard: LayoutDashboard,
  Search: Search,
  Downloads: Download,
  Completed: CheckSquare,
  Statistics: BarChart3,
  Automation: Rss,
  Settings: Settings,
};

const routes = navigationItems.map((item) => ({
  ...item,
  icon: routeIcons[item.label] ?? LayoutDashboard,
}));

export function DashboardShell() {
  const { sidebarOpen, mobileNavOpen, toggleMobileNav, closeMobileNav, openAddTorrent, theme, setTheme } = useUiStore();
  const { pathname } = useLocation();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();

  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: settingsApi.getSettings });
  
  const updateSettingsMutation = useMutation({
    mutationFn: settingsApi.updateSettings,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings'] }),
  });

  const toggleAnchorMode = () => {
    if (!settings) return;
    updateSettingsMutation.mutate({ ...settings, anchorModeEnabled: !settings.anchorModeEnabled });
  };

  // Establish SignalR connection for live torrent updates
  useTorrentSignalR();
  useSystemSignalR();

  // Close mobile drawer on route change
  useEffect(() => { closeMobileNav(); }, [pathname]); // eslint-disable-line react-hooks/exhaustive-deps

  const pathSegments = pathname.split('/').filter(Boolean);
  const primarySegment = pathSegments[0] ?? '';
  const currentPageLabel =
    pathSegments.length === 0
      ? 'Dashboard'
      : primarySegment === 'torrent'
        ? 'Downloads'
        : primarySegment.charAt(0).toUpperCase() + primarySegment.slice(1);

  const currentDetailLabel =
    primarySegment === 'torrent' && pathSegments[1]
      ? pathSegments[1]
      : null;


  useEffect(() => {
    const titleParts = [];
    if (currentDetailLabel && currentDetailLabel !== 'Overview') {
      titleParts.push(currentDetailLabel);
    }
    titleParts.push(currentPageLabel);
    titleParts.push('HarborTorrent');
    document.title = titleParts.join(' | ');
  }, [currentPageLabel, currentDetailLabel]);

  const [localSearch, setLocalSearch] = useState(searchParams.get('q') ?? '');
  
  // Sync local search input with URL when navigating
  useEffect(() => {
    setLocalSearch(searchParams.get('q') ?? '');
  }, [searchParams]);

  const navigate = useNavigate();

  // Debounce user typing and auto-navigate without pressing enter
  useEffect(() => {
    const handler = setTimeout(() => {
      const currentQuery = searchParams.get('q') ?? '';
      const newQuery = localSearch.trim();
      
      if (newQuery !== currentQuery) {
        if (newQuery) {
          navigate(`${pathname}?q=${encodeURIComponent(newQuery)}`, { replace: true });
        } else {
          navigate(pathname, { replace: true });
        }
      }
    }, 250); // 250ms debounce
    
    return () => clearTimeout(handler);
  }, [localSearch, pathname, navigate, searchParams]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // Submission is handled automatically by the debounce effect above, 
    // Retain this to prevent form reload on enter key.
  };

  // Shared sidebar nav content used in both the desktop sidebar and mobile drawer
  const SidebarContent = () => (
    <>
      <div className="mb-4 px-2.5">
        <button
          type="button"
          className="flex w-full items-center gap-2 rounded border border-[#EDEDEB] bg-white dark:bg-[#2A2A2A] dark:border-[#333333] px-3 py-2 text-[14px] text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#333333]"
          onClick={() => { closeMobileNav(); openAddTorrent(); }}
        >
          <Plus size={18} />
          <span>New Torrent</span>
        </button>
      </div>

      <nav className="flex-1 space-y-[2px] overflow-y-auto px-3" aria-label="Primary navigation">
        {routes.map((item) => (
          <NavLink
            key={item.label}
            to={item.path}
            end={item.path === '/'}
            className={({ isActive }) => [
              'flex items-center gap-2 rounded px-4 py-1.5 text-[14px] transition-colors',
              isActive
                ? 'bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)] text-[#37352F] dark:text-white font-medium'
                : 'text-[#5F5E5B] dark:text-[#E9E9E7] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]',
            ].join(' ')}
          >
            <item.icon size={18} />
            <span>{item.navLabel}</span>
          </NavLink>
        ))}
      </nav>
    </>
  );

  return (
    <div className="min-h-screen bg-white dark:bg-[#111111] text-[#37352F] dark:text-[#E9E9E7]">

      {/* ── Desktop sidebar (hidden on mobile) ── */}
      <aside className={`hidden md:flex fixed left-0 top-0 z-50 h-screen flex-col border-r border-[#EDEDEB] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A] py-3 transition-[width] ${sidebarOpen ? 'w-60' : 'w-[84px]'}`}>
        <div className="mb-5 flex items-center gap-2 rounded px-4 py-2 transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]">
          <div className="grid h-7 w-7 shrink-0 place-items-center overflow-hidden rounded-md bg-transparent">
            <img src="/logo.png" alt="Harbor Logo" className="h-full w-full object-cover" />
          </div>
          <div className={sidebarOpen ? '' : 'hidden'}>
            <h1 className="text-[14px] font-semibold leading-none text-[#37352F] dark:text-[#E9E9E7]">HarborTorrent</h1>
          </div>
        </div>

        <div className="mb-4 px-2.5">
          <button
            type="button"
            className="flex w-full items-center gap-2 rounded border border-[#EDEDEB] bg-white px-3 py-2 text-[14px] text-[#37352F] shadow-sm transition-colors hover:bg-[#F7F7F5]"
            onClick={() => openAddTorrent()}
          >
            <Plus size={18} />
            <span className={sidebarOpen ? '' : 'hidden'}>New Torrent</span>
          </button>
        </div>

        <nav className="flex-1 space-y-[2px] overflow-y-auto px-3" aria-label="Primary navigation">
          {routes.map((item) => (
            <NavLink
              key={item.label}
              to={item.path}
              end={item.path === '/'}
              className={({ isActive }) => [
                'flex items-center gap-2 rounded px-4 py-1.5 text-[14px] transition-colors',
                isActive
                  ? 'bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)] text-[#37352F] dark:text-white font-medium'
                  : 'text-[#5F5E5B] dark:text-[#E9E9E7] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]',
                sidebarOpen ? '' : 'justify-center px-0',
              ].join(' ')}
            >
              <item.icon size={18} />
              <span className={sidebarOpen ? '' : 'hidden'}>{item.navLabel}</span>
            </NavLink>
          ))}
        </nav>
      </aside>

      {/* ── Mobile drawer overlay ── */}
      {mobileNavOpen && (
        <div
          className="fixed inset-0 z-40 bg-[rgba(55,53,47,0.22)] backdrop-blur-md md:hidden"
          onClick={closeMobileNav}
          aria-hidden="true"
        />
      )}

      {/* ── Mobile drawer ── */}
      <aside
        className={`fixed left-0 top-0 z-50 flex h-screen w-72 flex-col border-r border-[#EDEDEB] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A] py-3 transition-transform md:hidden ${mobileNavOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        <div className="flex items-center justify-between px-4 mb-2">
          <div className="flex items-center gap-2">
            <div className="grid h-7 w-7 place-items-center overflow-hidden rounded-md">
              <img src="/logo.png" alt="Harbor Logo" className="h-full w-full object-cover" />
            </div>
            <span className="text-[14px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">HarborTorrent</span>
          </div>
          <button
            type="button"
            onClick={closeMobileNav}
            className="rounded p-1 text-[#73726F] dark:text-[#787774] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            aria-label="Close navigation"
          >
            <X size={18} />
          </button>
        </div>
        <SidebarContent />
      </aside>

      {/* ── Main content ── */}
      <main className={`min-h-screen transition-[margin] ${sidebarOpen ? 'md:ml-60' : 'md:ml-[84px]'}`}>
        {/* Topbar */}
        <header className={`fixed right-0 top-0 z-40 flex h-12 items-center justify-between border-b border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#111111] px-4 md:px-6 transition-[left] left-0 ${sidebarOpen ? 'md:left-60' : 'md:left-[84px]'}`}>
          <div className="flex items-center gap-3">
            {/* Hamburger — mobile only */}
            <button
              type="button"
              onClick={toggleMobileNav}
              className="md:hidden rounded p-1.5 text-[#5F5E5B] dark:text-[#E9E9E7] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              aria-label="Open navigation"
            >
              <Menu size={20} />
            </button>

            <div className="flex items-center gap-2 text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">
              <span className="cursor-pointer rounded px-2 py-1 hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]">{currentPageLabel}</span>
              {currentDetailLabel ? <span className="opacity-30">/</span> : null}
              {currentDetailLabel ? <span className="font-medium text-[#37352F] dark:text-[#E9E9E7]">{currentDetailLabel}</span> : null}
            </div>
          </div>

          <div className="flex items-center gap-2 md:gap-4">
            <button
              type="button"
              className={`rounded p-1.5 transition-colors ${settings?.anchorModeEnabled ? 'bg-[#2383E2] text-white hover:bg-[#1a73cc]' : 'text-[#5F5E5B] dark:text-[#E9E9E7] hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]'}`}
              onClick={toggleAnchorMode}
              disabled={updateSettingsMutation.isPending}
              aria-label="Toggle Anchor Mode"
              title={settings?.anchorModeEnabled ? 'Anchor Mode ON (Throttled)' : 'Anchor Mode OFF'}
            >
              <Anchor size={18} />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]"
              onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
              aria-label="Toggle theme"
            >
              {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <form onSubmit={handleSearchSubmit} className="flex items-center gap-2 rounded px-2 py-1 transition-all hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)] focus-within:ring-1 focus-within:ring-[#2383E2] focus-within:bg-white dark:focus-within:bg-[#111111]">
              <Search size={18} />
              <input
                type="search"
                value={localSearch}
                onChange={(event) => setLocalSearch(event.target.value)}
                placeholder="Search"
                className="w-24 md:w-40 border-0 bg-transparent p-0 text-[13px] text-[#4B4A47] dark:text-[#C4C4C4] outline-none placeholder:text-[#5F5E5B] dark:placeholder:text-[#8E8D8A]"
              />
            </form>

          </div>
        </header>

        <div className="pt-12">
          <Outlet />
        </div>
      </main>

      <AddTorrentModal />
    </div>
  );
}
