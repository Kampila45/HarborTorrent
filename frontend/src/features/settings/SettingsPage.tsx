import { useEffect, useState } from 'react';
import { Save, AlertCircle, DownloadCloud, CheckCircle, RotateCcw } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { settingsApi, type SettingsDto } from './api';
import { FolderBrowserModal } from '@/features/directories/components/FolderBrowserModal';
import { systemApi } from '@/features/system/api';
import { useSystemStore } from '@/store/systemStore';
import { triggerManualUpdate } from '@/features/system/hooks/useSystemUpdater';
import { relaunch } from '@tauri-apps/plugin-process';

const renderMarkdown = (text: string) => {
  return text.split('\n').map((line, index) => {
    if (line.startsWith('# ')) {
      return <h1 key={index} className="text-xl font-bold mb-4 mt-2 text-[#37352F] dark:text-[#E9E9E7]">{line.replace('# ', '')}</h1>;
    }
    if (line.startsWith('## ')) {
      return <h2 key={index} className="text-[16px] font-semibold mb-3 mt-6 text-[#37352F] dark:text-[#E9E9E7] border-b border-[rgba(55,53,47,0.1)] dark:border-[#333] pb-2">{line.replace('## ', '')}</h2>;
    }
    if (line.startsWith('- ')) {
      const content = line.replace('- ', '');
      const parts = content.split(/(\*\*.*?\*\*)/g);
      return (
        <li key={index} className="ml-5 mb-2 list-disc text-[14px] text-[#4B4A47] dark:text-[#D4D4D4]">
          {parts.map((part, i) => {
            if (part.startsWith('**') && part.endsWith('**')) {
              return <span key={i} className="font-semibold text-[#37352F] dark:text-[#E9E9E7]">{part.slice(2, -2)}</span>;
            }
            return <span key={i}>{part}</span>;
          })}
        </li>
      );
    }
    if (line.trim() === '') {
      return <div key={index} className="h-1"></div>;
    }
    return <p key={index} className="mb-2 text-[14px] text-[#4B4A47] dark:text-[#D4D4D4]">{line}</p>;
  });
};

export function SettingsPage() {
  const [settings, setSettings] = useState<SettingsDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isBrowserOpen, setIsBrowserOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<'storage' | 'bandwidth' | 'anchor' | 'network' | 'about'>('storage');

  // System Update State
  const { isUpdating, updateProgress, updateComplete, setIsUpdating, setUpdateProgress, setUpdateComplete } = useSystemStore();

  // Convert UI input bytes to KB/s for clarity.
  const [maxDownloadKbps, setMaxDownloadKbps] = useState<string>('');
  const [maxUploadKbps, setMaxUploadKbps] = useState<string>('');
  const [anchorMaxDownloadKbps, setAnchorMaxDownloadKbps] = useState<string>('');
  const [anchorMaxUploadKbps, setAnchorMaxUploadKbps] = useState<string>('');
  
  const { data: serverSettings, isLoading } = useQuery({ 
    queryKey: ['settings'], 
    queryFn: settingsApi.getSettings 
  });

  const { data: versionInfo } = useQuery({ 
    queryKey: ['system-version'], 
    queryFn: systemApi.getVersion 
  });

  const { data: changelog } = useQuery({ 
    queryKey: ['system-changelog'], 
    queryFn: systemApi.getChangelog 
  });

  useEffect(() => {
    if (serverSettings && !saving) {
      setSettings(serverSettings);
      setMaxDownloadKbps(serverSettings.maxDownloadSpeedBytes > 0 ? Math.round(serverSettings.maxDownloadSpeedBytes / 1024).toString() : '0');
      setMaxUploadKbps(serverSettings.maxUploadSpeedBytes > 0 ? Math.round(serverSettings.maxUploadSpeedBytes / 1024).toString() : '0');
      setAnchorMaxDownloadKbps(serverSettings.anchorMaxDownloadSpeedBytes > 0 ? Math.round(serverSettings.anchorMaxDownloadSpeedBytes / 1024).toString() : '0');
      setAnchorMaxUploadKbps(serverSettings.anchorMaxUploadSpeedBytes > 0 ? Math.round(serverSettings.anchorMaxUploadSpeedBytes / 1024).toString() : '0');
      setError(null);
    }
  }, [serverSettings, saving]);

  const handleSave = async () => {
    if (!settings) return;

    setSaving(true);
    setError(null);

    const downloadSpeed = parseInt(maxDownloadKbps, 10);
    const uploadSpeed = parseInt(maxUploadKbps, 10);
    const anchorDownloadSpeed = parseInt(anchorMaxDownloadKbps, 10);
    const anchorUploadSpeed = parseInt(anchorMaxUploadKbps, 10);

    const payload: SettingsDto = {
      ...settings,
      maxDownloadSpeedBytes: isNaN(downloadSpeed) || downloadSpeed <= 0 ? 0 : downloadSpeed * 1024,
      maxUploadSpeedBytes: isNaN(uploadSpeed) || uploadSpeed <= 0 ? 0 : uploadSpeed * 1024,
      anchorMaxDownloadSpeedBytes: isNaN(anchorDownloadSpeed) || anchorDownloadSpeed <= 0 ? 0 : anchorDownloadSpeed * 1024,
      anchorMaxUploadSpeedBytes: isNaN(anchorUploadSpeed) || anchorUploadSpeed <= 0 ? 0 : anchorUploadSpeed * 1024,
    };

    try {
      await settingsApi.updateSettings(payload);
      setSettings(payload);
      // Optional: Add a toast notification here
    } catch (err) {
      console.error('Failed to save settings:', err);
      setError('Failed to save settings.');
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex h-[calc(100vh-3rem)] items-center justify-center">
        <div className="text-[14px] text-[#73726F]">Loading settings...</div>
      </div>
    );
  }

  if (!settings) {
    return (
      <div className="p-6 md:p-8">
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-600 dark:border-red-900/30 dark:bg-red-900/10">
          <p className="flex items-center gap-2 font-medium"><AlertCircle size={18} /> {error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl p-6 md:p-8 pb-20">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-[#37352F] dark:text-white">Settings</h1>
          <p className="text-[14px] text-[#5F5E5B] dark:text-[#C4C4C4] mt-1">
            Configure global application preferences and network limits.
          </p>
        </div>
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 rounded bg-[#2383E2] px-4 py-2 text-[14px] font-medium text-white transition-colors hover:bg-[#1a73cc] disabled:opacity-50"
        >
          <Save size={16} />
          {saving ? 'Saving...' : 'Save Changes'}
        </button>
      </div>

      {error && (
        <div className="mb-6 rounded border border-red-200 bg-red-50 p-3 text-[14px] text-red-600 dark:border-red-900/30 dark:bg-red-900/10 flex items-center gap-2">
          <AlertCircle size={16} />
          {error}
        </div>
      )}

      <div className="flex flex-col md:flex-row gap-8 items-start">
        <nav className="w-full md:w-56 shrink-0 flex flex-row md:flex-col gap-1 overflow-x-auto pb-2 md:pb-0">
          {[
            { id: 'storage', label: 'Storage & Directories' },
            { id: 'bandwidth', label: 'Bandwidth Limits' },
            { id: 'anchor', label: 'Anchor Mode' },
            { id: 'network', label: 'Network & Engine' },
            { id: 'about', label: 'About & Updates' },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as any)}
              className={`whitespace-nowrap rounded px-3 py-2 text-left text-[14px] font-medium transition-colors ${
                activeTab === tab.id
                  ? 'bg-[rgba(55,53,47,0.08)] text-[#37352F] dark:bg-[rgba(255,255,255,0.1)] dark:text-[#E9E9E7]'
                  : 'text-[#5F5E5B] hover:bg-[rgba(55,53,47,0.04)] dark:text-[#C4C4C4] dark:hover:bg-[rgba(255,255,255,0.05)]'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </nav>

        <div className="flex-1 min-w-0 w-full">
        {/* Directories Section */}
        {activeTab === 'storage' && (
        <section>
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-[#73726F] dark:text-[#787774]">
            Storage & Directories
          </h2>
          <div className="rounded-lg border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#2A2A2A] p-5 shadow-sm">
            <div className="space-y-1.5">
              <label htmlFor="defaultDownloadPath" className="block text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Default Download Path
              </label>
              <div className="flex gap-2">
                <input
                  id="defaultDownloadPath"
                  type="text"
                  value={settings.defaultDownloadPath}
                  onChange={(e) => setSettings({ ...settings, defaultDownloadPath: e.target.value })}
                  className="w-full rounded border border-[#EDEDEB] dark:border-[#444444] bg-transparent px-3 py-2 text-[14px] outline-none transition-colors focus:border-[#2383E2] dark:focus:border-[#2383E2]"
                  placeholder="e.g. C:\Downloads\HarborTorrent"
                />
                <button
                  type="button"
                  onClick={() => setIsBrowserOpen(true)}
                  className="shrink-0 rounded-[4px] border border-[#EDEDEB] dark:border-[#444444] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-sm font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#EDEDEB] dark:hover:bg-[#333333] active:scale-[0.98]"
                >
                  Browse
                </button>
              </div>
              <p className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">
                Where new torrents will be saved by default if you don't pick a specific folder.
              </p>
            </div>
          </div>
        </section>
        )}

        {/* Bandwidth Section */}
        {activeTab === 'bandwidth' && (
        <section>
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-[#73726F] dark:text-[#787774]">
            Bandwidth Limits
          </h2>
          <div className="rounded-lg border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#2A2A2A] p-5 shadow-sm space-y-5">
            
            <div className="space-y-1.5">
              <label htmlFor="maxDownloadSpeed" className="block text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Global Maximum Download Speed (KB/s)
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="maxDownloadSpeed"
                  type="number"
                  min="0"
                  value={maxDownloadKbps}
                  onChange={(e) => setMaxDownloadKbps(e.target.value)}
                  className="w-40 rounded border border-[#EDEDEB] dark:border-[#444444] bg-transparent px-3 py-2 text-[14px] outline-none transition-colors focus:border-[#2383E2] dark:focus:border-[#2383E2]"
                />
                <span className="text-[14px] text-[#73726F] dark:text-[#C4C4C4]">KB/s</span>
              </div>
              <p className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">
                Set to 0 for unlimited speed. Applies immediately.
              </p>
            </div>

            <div className="space-y-1.5">
              <label htmlFor="maxUploadSpeed" className="block text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Global Maximum Upload Speed (KB/s)
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="maxUploadSpeed"
                  type="number"
                  min="0"
                  value={maxUploadKbps}
                  onChange={(e) => setMaxUploadKbps(e.target.value)}
                  className="w-40 rounded border border-[#EDEDEB] dark:border-[#444444] bg-transparent px-3 py-2 text-[14px] outline-none transition-colors focus:border-[#2383E2] dark:focus:border-[#2383E2]"
                />
                <span className="text-[14px] text-[#73726F] dark:text-[#C4C4C4]">KB/s</span>
              </div>
              <p className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">
                Set to 0 for unlimited speed.
              </p>
            </div>

          </div>
        </section>
        )}

        {/* Anchor Mode Section */}
        {activeTab === 'anchor' && (
        <section>
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-[#73726F] dark:text-[#787774]">
            Anchor Mode (Alternative Rate Limits)
          </h2>
          <div className="rounded-lg border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#2A2A2A] p-5 shadow-sm space-y-5">
            
            <label className="flex items-center gap-3 cursor-pointer group mb-2">
              <div className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors duration-200 ease-in-out">
                <input
                  type="checkbox"
                  className="peer sr-only"
                  checked={settings.anchorModeEnabled}
                  onChange={(e) => setSettings({ ...settings, anchorModeEnabled: e.target.checked })}
                />
                <div className="absolute inset-0 rounded-full bg-[#EDEDEB] dark:bg-[#444444] peer-checked:bg-[#2383E2] transition-colors" />
                <div className="absolute left-[2px] top-[2px] h-4 w-4 transform rounded-full bg-white transition-transform peer-checked:translate-x-4 shadow-sm" />
              </div>
              <div className="flex flex-col">
                <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Enable Anchor Mode</span>
                <span className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">Globally throttle traffic to the limits defined below.</span>
              </div>
            </label>

            <div className="space-y-1.5">
              <label htmlFor="anchorMaxDownloadSpeed" className="block text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Anchor Maximum Download Speed (KB/s)
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="anchorMaxDownloadSpeed"
                  type="number"
                  min="0"
                  value={anchorMaxDownloadKbps}
                  onChange={(e) => setAnchorMaxDownloadKbps(e.target.value)}
                  className="w-40 rounded border border-[#EDEDEB] dark:border-[#444444] bg-transparent px-3 py-2 text-[14px] outline-none transition-colors focus:border-[#2383E2] dark:focus:border-[#2383E2]"
                />
                <span className="text-[14px] text-[#73726F] dark:text-[#C4C4C4]">KB/s</span>
              </div>
            </div>

            <div className="space-y-1.5">
              <label htmlFor="anchorMaxUploadSpeed" className="block text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Anchor Maximum Upload Speed (KB/s)
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="anchorMaxUploadSpeed"
                  type="number"
                  min="0"
                  value={anchorMaxUploadKbps}
                  onChange={(e) => setAnchorMaxUploadKbps(e.target.value)}
                  className="w-40 rounded border border-[#EDEDEB] dark:border-[#444444] bg-transparent px-3 py-2 text-[14px] outline-none transition-colors focus:border-[#2383E2] dark:focus:border-[#2383E2]"
                />
                <span className="text-[14px] text-[#73726F] dark:text-[#C4C4C4]">KB/s</span>
              </div>
            </div>

          </div>
        </section>
        )}

        {/* Network & Engine Section */}
        {activeTab === 'network' && (
        <section>
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-[#73726F] dark:text-[#787774]">
            Network & Engine
          </h2>
          <div className="rounded-lg border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#2A2A2A] p-5 shadow-sm space-y-4">
            
            <label className="flex items-center gap-3 cursor-pointer group">
              <div className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors duration-200 ease-in-out">
                <input
                  type="checkbox"
                  className="peer sr-only"
                  checked={settings.enableDht}
                  onChange={(e) => setSettings({ ...settings, enableDht: e.target.checked })}
                />
                <div className="absolute inset-0 rounded-full bg-[#EDEDEB] dark:bg-[#444444] peer-checked:bg-[#2383E2] transition-colors" />
                <div className="absolute left-[2px] top-[2px] h-4 w-4 transform rounded-full bg-white transition-transform peer-checked:translate-x-4 shadow-sm" />
              </div>
              <div className="flex flex-col">
                <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Enable DHT (Distributed Hash Table)</span>
                <span className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">Finds peers without needing a tracker. Highly recommended.</span>
              </div>
            </label>

            <label className="flex items-center gap-3 cursor-pointer group">
              <div className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors duration-200 ease-in-out">
                <input
                  type="checkbox"
                  className="peer sr-only"
                  checked={settings.enablePex}
                  onChange={(e) => setSettings({ ...settings, enablePex: e.target.checked })}
                />
                <div className="absolute inset-0 rounded-full bg-[#EDEDEB] dark:bg-[#444444] peer-checked:bg-[#2383E2] transition-colors" />
                <div className="absolute left-[2px] top-[2px] h-4 w-4 transform rounded-full bg-white transition-transform peer-checked:translate-x-4 shadow-sm" />
              </div>
              <div className="flex flex-col">
                <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Enable Peer Exchange (PEX)</span>
                <span className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">Trade peer lists with connected peers.</span>
              </div>
            </label>

            <label className="flex items-center gap-3 cursor-pointer group">
              <div className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors duration-200 ease-in-out">
                <input
                  type="checkbox"
                  className="peer sr-only"
                  checked={settings.enableLpd}
                  onChange={(e) => setSettings({ ...settings, enableLpd: e.target.checked })}
                />
                <div className="absolute inset-0 rounded-full bg-[#EDEDEB] dark:bg-[#444444] peer-checked:bg-[#2383E2] transition-colors" />
                <div className="absolute left-[2px] top-[2px] h-4 w-4 transform rounded-full bg-white transition-transform peer-checked:translate-x-4 shadow-sm" />
              </div>
              <div className="flex flex-col">
                <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Enable Local Peer Discovery (LPD)</span>
                <span className="text-[12px] text-[#73726F] dark:text-[#C4C4C4]">Discover peers on your local network/LAN.</span>
              </div>
            </label>

          </div>
        </section>
        )}

        {/* System Updates & Changelog */}
        {activeTab === 'about' && (
        <section className="flex flex-col gap-4 border-t-0 pt-0">
          <div className="flex items-center justify-between mb-2">
            <div>
              <h2 className="text-[18px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">About & Updates</h2>
              <p className="mt-1 text-[13px] text-[#5F5E5B] dark:text-[#C4C4C4]">
                View recent changes and update your Harbor installation.
              </p>
            </div>
            
            <div className="flex items-center gap-4">
              <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Current Version: <span className="text-[#2383E2]">{versionInfo?.version || 'Loading...'}</span>
              </span>
              {versionInfo?.version !== versionInfo?.latestVersion && !isUpdating && !updateComplete && (
                <button
                  onClick={async () => {
                    await triggerManualUpdate(setIsUpdating, setUpdateProgress, setUpdateComplete);
                  }}
                  className="flex items-center gap-2 rounded-md border border-[#EDEDEB] dark:border-[#444444] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#E1E1E1] dark:hover:bg-[#333333] active:scale-[0.98]"
                >
                  <DownloadCloud size={16} />
                  Update to v{versionInfo?.latestVersion}
                </button>
              )}
            </div>
          </div>

          {/* Update Progress UI */}
          {isUpdating && !updateComplete && (
            <div className="rounded-xl border border-[rgba(55,53,47,0.16)] dark:border-[#333333] p-5 bg-[#F7F7F5] dark:bg-[#202020]">
              <div className="flex items-center justify-between mb-3">
                <span className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Downloading and installing update...</span>
                <span className="text-[14px] font-bold text-[#2383E2]">{updateProgress}%</span>
              </div>
              <div className="w-full bg-[rgba(55,53,47,0.1)] dark:bg-[#333] rounded-full h-2.5 overflow-hidden">
                <div 
                  className="bg-[#2383E2] h-2.5 rounded-full transition-all duration-300 ease-out" 
                  style={{ width: `${updateProgress}%` }}
                ></div>
              </div>
              <p className="mt-3 text-[12px] text-[#73726F] dark:text-[#C4C4C4]">Please do not close this window during the update process.</p>
            </div>
          )}

          {/* Update Complete UI */}
          {updateComplete && (
            <div className="rounded-xl border border-[rgba(55,53,47,0.16)] dark:border-[#333333] p-5 bg-[#F7F7F5] dark:bg-[#202020] mt-2">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3 text-[#37352F] dark:text-[#E9E9E7]">
                  <CheckCircle size={20} className="text-[#2383E2]" />
                  <span className="text-[14px] font-semibold">Update successfully installed!</span>
                </div>
                <button
                  onClick={() => relaunch()}
                  className="flex items-center gap-2 rounded-md border border-[#EDEDEB] dark:border-[#444444] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#E1E1E1] dark:hover:bg-[#333333] active:scale-[0.98]"
                >
                  <RotateCcw size={16} />
                  Restart App
                </button>
              </div>
            </div>
          )}

          {/* Changelog Viewer */}
          <div className="mt-4 rounded-xl border border-[rgba(55,53,47,0.16)] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1C1C1C] overflow-hidden">
            <div className="bg-[rgba(55,53,47,0.05)] dark:bg-[#262626] px-4 py-3 border-b border-[rgba(55,53,47,0.1)] dark:border-[#333333]">
              <h3 className="text-[14px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">Changelog</h3>
            </div>

            <div className="p-6 max-h-[500px] overflow-y-auto custom-scrollbar">
              {changelog ? (
                <div className="font-sans leading-relaxed">
                  {renderMarkdown(changelog)}
                </div>
              ) : (
                <span className="text-[14px] text-gray-500">Loading changelog...</span>
              )}
            </div>
          </div>
        </section>
        )}
        </div>
      </div>

      <FolderBrowserModal
        isOpen={isBrowserOpen}
        onClose={() => setIsBrowserOpen(false)}
        onSelect={(path) => setSettings({ ...settings, defaultDownloadPath: path })}
        initialPath={settings.defaultDownloadPath}
      />
    </div>
  );
}
