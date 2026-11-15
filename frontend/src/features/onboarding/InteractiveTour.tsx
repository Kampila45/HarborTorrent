import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { LucideIcon } from 'lucide-react';
import {
  Anchor,
  Compass,
  Download,
  Moon,
  Plus,
  Rss,
  Search,
  Settings,
  Sun,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { settingsApi, type SettingsDto } from '@/features/settings/api';
import { useUiStore } from '@/store/uiStore';

type TourStep = {
  id: string;
  title: string;
  description: string;
  target?: string;
  route?: string;
  icon: LucideIcon;
  hint?: string;
};

const TOUR_STEPS: TourStep[] = [
  {
    id: 'welcome',
    title: 'Welcome to HarborTorrent',
    description:
      'Take a quick interactive tour of your workspace. We will show you where to find torrents, monitor downloads, automate feeds, and control bandwidth.',
    route: '/',
    icon: Compass,
  },
  {
    id: 'new-torrent',
    title: 'Add Torrents & Magnet Links',
    description:
      'Click "+ New Torrent" anytime to drop a .torrent file or paste a magnet link, pick a destination folder, and start transferring right away.',
    target: 'new-torrent',
    route: '/',
    icon: Plus,
  },
  {
    id: 'search',
    title: 'Built-in Torrent Search',
    description:
      'Search across public trackers directly inside HarborTorrent. Clicking "Get" lets you pick your download location and start in one step.',
    target: 'nav-search',
    route: '/search',
    icon: Search,
  },
  {
    id: 'downloads',
    title: 'Live Downloads & Streaming',
    description:
      'Track transfer speeds, inspect peers and trackers, pause or reprioritize files, and stream video torrents immediately while they download.',
    target: 'nav-downloads',
    route: '/downloads',
    icon: Download,
  },
  {
    id: 'automation',
    title: 'RSS Feed Automation',
    description:
      'Subscribe to RSS feeds with pattern filters so new episodes or releases are automatically queued into dedicated folders.',
    target: 'nav-rss',
    route: '/rss',
    icon: Rss,
  },
  {
    id: 'anchor-mode',
    title: 'One-Click Anchor Mode',
    description:
      'Need full network bandwidth for a call or game? Toggle Anchor Mode in the top bar to instantly throttle download and upload speeds.',
    target: 'anchor-mode',
    icon: Anchor,
    hint: 'Try toggling Anchor Mode below to watch the header button respond live.',
  },
  {
    id: 'settings',
    title: 'Storage, Limits & Engine',
    description:
      'Configure your default download directory, global speed limits, Anchor Mode thresholds, and DHT / PEX / LPD peer discovery in Settings.',
    target: 'nav-settings',
    route: '/settings',
    icon: Settings,
  },
];

type Rect = {
  top: number;
  left: number;
  width: number;
  height: number;
  right: number;
  bottom: number;
};

function findVisibleTargetRect(targetName: string): Rect | null {
  const candidates = document.querySelectorAll<HTMLElement>(`[data-tour="${targetName}"]`);
  for (const el of candidates) {
    const r = el.getBoundingClientRect();
    if (r.width > 0 && r.height > 0) {
      return {
        top: r.top,
        left: r.left,
        width: r.width,
        height: r.height,
        right: r.right,
        bottom: r.bottom,
      };
    }
  }
  return null;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), Math.max(min, max));
}

export function InteractiveTour() {
  const { onboardingOpen, completeOnboarding, theme, setTheme, sidebarOpen, openAddTorrent } = useUiStore();
  const [stepIndex, setStepIndex] = useState(0);
  const [targetRect, setTargetRect] = useState<Rect | null>(null);
  const [cardSize, setCardSize] = useState({ width: 360, height: 260 });
  const cardRef = useRef<HTMLDivElement | null>(null);
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const queryClient = useQueryClient();

  const { data: settings } = useQuery({
    queryKey: ['settings'],
    queryFn: settingsApi.getSettings,
    enabled: onboardingOpen,
  });

  const anchorMutation = useMutation({
    mutationFn: (next: SettingsDto) => settingsApi.updateSettings(next),
    onSuccess: (updated) => {
      queryClient.setQueryData(['settings'], updated);
    },
  });

  const step = TOUR_STEPS[stepIndex] ?? TOUR_STEPS[0];
  const isFirst = stepIndex === 0;
  const isLast = stepIndex === TOUR_STEPS.length - 1;

  // Reset to step 0 each time the tour opens.
  useEffect(() => {
    if (onboardingOpen) {
      setStepIndex(0);
    }
  }, [onboardingOpen]);

  // Navigate to the step's showcase route when entering a step.
  useEffect(() => {
    if (!onboardingOpen || !step?.route) return;
    if (pathname !== step.route) {
      navigate(step.route);
    }
  }, [onboardingOpen, step?.route, pathname, navigate]);

  const measureTarget = useCallback(() => {
    if (!onboardingOpen || !step?.target) {
      setTargetRect(null);
      return;
    }
    setTargetRect(findVisibleTargetRect(step.target));
  }, [onboardingOpen, step?.target]);

  useLayoutEffect(() => {
    if (!onboardingOpen) return;
    measureTarget();
    // Re-measure after layout/sidebar transitions settle.
    const timer = window.setTimeout(measureTarget, 80);
    window.addEventListener('resize', measureTarget);
    window.addEventListener('scroll', measureTarget, true);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener('resize', measureTarget);
      window.removeEventListener('scroll', measureTarget, true);
    };
  }, [onboardingOpen, stepIndex, pathname, sidebarOpen, measureTarget]);

  useLayoutEffect(() => {
    if (!onboardingOpen || !cardRef.current) return;
    const rect = cardRef.current.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
      setCardSize({ width: rect.width, height: rect.height });
    }
  }, [onboardingOpen, stepIndex, settings?.anchorModeEnabled, theme]);

  const handleFinish = useCallback(
    (destination?: string, openModalAfter?: boolean) => {
      completeOnboarding();
      if (destination) {
        navigate(destination);
      }
      if (openModalAfter) {
        window.setTimeout(() => openAddTorrent(), 50);
      }
    },
    [completeOnboarding, navigate, openAddTorrent]
  );

  // Keyboard navigation: Escape to dismiss, Left/Right arrows to step.
  useEffect(() => {
    if (!onboardingOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        completeOnboarding();
      } else if (e.key === 'ArrowRight') {
        e.preventDefault();
        if (stepIndex < TOUR_STEPS.length - 1) {
          setStepIndex((i) => i + 1);
        }
      } else if (e.key === 'ArrowLeft') {
        e.preventDefault();
        if (stepIndex > 0) {
          setStepIndex((i) => i - 1);
        }
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onboardingOpen, stepIndex, completeOnboarding]);

  if (!onboardingOpen || !step) return null;

  const StepIcon = step.icon;
  const padding = 6;
  const gap = 14;
  const viewportPadding = 16;

  const padded = targetRect
    ? {
        top: targetRect.top - padding,
        left: targetRect.left - padding,
        width: targetRect.width + padding * 2,
        height: targetRect.height + padding * 2,
        right: targetRect.right + padding,
        bottom: targetRect.bottom + padding,
      }
    : null;

  let popoverStyle: React.CSSProperties | undefined;
  if (padded) {
    const vw = typeof window !== 'undefined' ? window.innerWidth : 1200;
    const vh = typeof window !== 'undefined' ? window.innerHeight : 800;
    const cardW = Math.min(360, vw - viewportPadding * 2);
    const cardH = cardSize.height;

    const fitsRight = padded.right + gap + cardW + viewportPadding <= vw;
    const fitsBottom = padded.bottom + gap + cardH + viewportPadding <= vh;
    const fitsLeft = padded.left - gap - cardW - viewportPadding >= 0;

    let top = viewportPadding;
    let left = viewportPadding;

    // Prefer placing right of sidebar items, below header items.
    if (padded.top < 80 && fitsBottom) {
      top = padded.bottom + gap;
      left = clamp(padded.right - cardW, viewportPadding, vw - cardW - viewportPadding);
    } else if (fitsRight) {
      left = padded.right + gap;
      top = clamp(padded.top - 8, viewportPadding, vh - cardH - viewportPadding);
    } else if (fitsBottom) {
      top = padded.bottom + gap;
      left = clamp(padded.left, viewportPadding, vw - cardW - viewportPadding);
    } else if (fitsLeft) {
      left = padded.left - gap - cardW;
      top = clamp(padded.top - 8, viewportPadding, vh - cardH - viewportPadding);
    } else {
      top = clamp(padded.top - gap - cardH, viewportPadding, vh - cardH - viewportPadding);
      left = clamp(padded.left, viewportPadding, vw - cardW - viewportPadding);
    }

    popoverStyle = { top, left, width: cardW };
  }

  return (
    <div
      className="fixed inset-0 z-[70]"
      role="dialog"
      aria-modal="true"
      aria-labelledby="interactive-tour-title"
    >
      {/* Dimmed backdrop when no spotlight target is active (intro step or mobile fallback) */}
      {!padded && (
        <div
          className="fixed inset-0 bg-[rgba(15,15,15,0.58)] backdrop-blur-[2px] transition-opacity"
          aria-hidden="true"
        />
      )}

      {/* Spotlight cutout overlay */}
      {padded && (
        <>
          <div className="fixed inset-0" aria-hidden="true" />
          <div
            className="pointer-events-none fixed rounded-lg ring-2 ring-[#2383E2] transition-all duration-300 ease-out"
            style={{
              top: padded.top,
              left: padded.left,
              width: padded.width,
              height: padded.height,
              boxShadow: '0 0 0 9999px rgba(15, 15, 15, 0.58)',
            }}
            aria-hidden="true"
          />
        </>
      )}

      {/* Tour Card (floating next to target or centered) */}
      <div
        className={
          padded
            ? 'fixed z-[71] transition-all duration-300 ease-out'
            : 'fixed inset-0 z-[71] flex items-center justify-center p-4'
        }
        style={popoverStyle}
      >
        <div
          ref={cardRef}
          className="w-full max-w-[360px] rounded-xl border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#1E1E1E] p-5 shadow-2xl"
        >
          {/* Header */}
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[#2383E2]/10 text-[#2383E2]">
                <StepIcon size={17} />
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wider text-[#2383E2]">
                  Step {stepIndex + 1} of {TOUR_STEPS.length}
                </p>
                <h2
                  id="interactive-tour-title"
                  className="text-[16px] font-semibold leading-tight text-[#37352F] dark:text-[#E9E9E7]"
                >
                  {step.title}
                </h2>
              </div>
            </div>
            <button
              type="button"
              onClick={() => completeOnboarding()}
              className="rounded p-1 text-[#9B9A97] transition-colors hover:bg-[#F7F7F5] hover:text-[#37352F] dark:hover:bg-[#2A2A2A] dark:hover:text-[#E9E9E7]"
              aria-label="Close tour"
            >
              <X size={16} />
            </button>
          </div>

          {/* Body */}
          <p className="mt-3 text-[13.5px] leading-relaxed text-[#5F5E5B] dark:text-[#B5B5B5]">
            {step.description}
          </p>

          {/* Interactive controls embedded in relevant steps */}
          {step.id === 'welcome' && (
            <div className="mt-4 flex items-center justify-between rounded-lg border border-[#EDEDEB] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#252525] px-3 py-2">
              <span className="text-[12.5px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                Appearance
              </span>
              <div className="flex items-center gap-1.5">
                <button
                  type="button"
                  onClick={() => setTheme('light')}
                  className={`inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-[12px] font-medium transition-colors ${
                    theme === 'light'
                      ? 'bg-white text-[#2383E2] shadow-sm ring-1 ring-[#EDEDEB]'
                      : 'text-[#73726F] hover:text-[#37352F] dark:text-[#A4A4A4] dark:hover:text-[#E9E9E7]'
                  }`}
                >
                  <Sun size={13} />
                  Light
                </button>
                <button
                  type="button"
                  onClick={() => setTheme('dark')}
                  className={`inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-[12px] font-medium transition-colors ${
                    theme === 'dark'
                      ? 'bg-[#111111] text-[#2383E2] shadow-sm ring-1 ring-[#333333]'
                      : 'text-[#73726F] hover:text-[#37352F] dark:text-[#A4A4A4] dark:hover:text-[#E9E9E7]'
                  }`}
                >
                  <Moon size={13} />
                  Dark
                </button>
              </div>
            </div>
          )}

          {step.id === 'anchor-mode' && settings && (
            <div className="mt-4 space-y-2">
              {step.hint && (
                <p className="text-[12px] text-[#73726F] dark:text-[#9A9A9A]">{step.hint}</p>
              )}
              <button
                type="button"
                disabled={anchorMutation.isPending}
                onClick={() =>
                  anchorMutation.mutate({
                    ...settings,
                    anchorModeEnabled: !settings.anchorModeEnabled,
                  })
                }
                className={`flex w-full items-center justify-between rounded-lg border px-3 py-2 text-[13px] font-medium transition-colors ${
                  settings.anchorModeEnabled
                    ? 'border-[#2383E2] bg-[#2383E2]/10 text-[#2383E2]'
                    : 'border-[#EDEDEB] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#252525] text-[#37352F] dark:text-[#E9E9E7] hover:bg-[#EFEFED] dark:hover:bg-[#2E2E2E]'
                }`}
              >
                <span className="flex items-center gap-2">
                  <Anchor size={15} />
                  Anchor Mode
                </span>
                <span className="text-[12px] font-semibold">
                  {settings.anchorModeEnabled ? 'ON (Throttled)' : 'OFF'}
                </span>
              </button>
            </div>
          )}

          {/* Progress Dots */}
          <div className="mt-4 flex items-center gap-1.5">
            {TOUR_STEPS.map((s, idx) => (
              <button
                key={s.id}
                type="button"
                onClick={() => setStepIndex(idx)}
                aria-label={`Go to step ${idx + 1}: ${s.title}`}
                className={`h-1.5 rounded-full transition-all ${
                  idx === stepIndex
                    ? 'w-5 bg-[#2383E2]'
                    : idx < stepIndex
                      ? 'w-2 bg-[#2383E2]/45'
                      : 'w-2 bg-[#E3E2DF] dark:bg-[#3A3A3A]'
                }`}
              />
            ))}
          </div>

          {/* Footer Controls */}
          <div className="mt-4 flex items-center justify-between gap-2 border-t border-[#EDEDEB] dark:border-[#2E2E2E] pt-3">
            <button
              type="button"
              onClick={() => completeOnboarding()}
              className="text-[12.5px] font-medium text-[#73726F] hover:text-[#37352F] dark:text-[#9A9A9A] dark:hover:text-[#E9E9E7] transition-colors"
            >
              Skip tour
            </button>

            <div className="flex items-center gap-2">
              {!isFirst && (
                <Button size="sm" variant="secondary" onClick={() => setStepIndex((i) => i - 1)}>
                  Back
                </Button>
              )}

              {!isLast ? (
                <Button size="sm" variant="primary" onClick={() => setStepIndex((i) => i + 1)}>
                  {isFirst ? 'Start Tour' : 'Next'}
                </Button>
              ) : (
                <>
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => handleFinish('/', true)}
                  >
                    Add Torrent
                  </Button>
                  <Button size="sm" variant="primary" onClick={() => handleFinish('/')}>
                    Finish
                  </Button>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

