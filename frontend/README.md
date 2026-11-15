# HarborTorrent - Frontend

HarborTorrent is a modern torrent management dashboard built with React and TypeScript. It provides a real-time interface for monitoring, adding, and controlling torrents with a clean dark-mode-first design.

---

## Tech Stack

| Technology | Purpose |
|---|---|
| React 18 + TypeScript | UI framework |
| Vite 8 | Build tool & dev server |
| TailwindCSS v4 | Utility-first styling |
| React Router v6 | Client-side routing |
| TanStack Query v5 | Server state, caching & background refetch |
| Axios | HTTP client |
| Zustand | Lightweight global UI state |
| Microsoft SignalR | Real-time WebSocket updates |
| Lucide React | Icon library |
| Sonner | Toast notifications |
| Inter + JetBrains Mono | Typography |

---

## Project Structure

```
src/
├── app/              # App entry, routing (App.tsx), providers, global listeners & types
├── components/       # Shared UI (Pagination, PageSkeleton, ConfirmDeleteModal, ui/)
├── features/
│   ├── dashboard/    # Overview page with metrics & active transfers
│   ├── downloads/    # Full torrent list with search, filter & pagination
│   ├── completed/    # Completed/seeding torrents
│   ├── statistics/   # Network statistics page
│   ├── torrents/     # Torrent details, streaming player, hooks, AddTorrentModal
│   ├── rss/          # RSS feed automation (feeds, filters, hooks)
│   ├── search/       # Built-in torrent search (Apibay, YTS)
│   ├── settings/     # Engine settings (speed caps, DHT/PEX/LPD, Anchor Mode)
│   ├── directories/  # Folder browser modal for save path selection
│   ├── onboarding/   # Interactive product tour
│   ├── system/       # Backend readiness & updater hooks
│   ├── layout/       # DashboardShell (sidebar + topbar layout wrapper)
│   └── error/        # Backend failure, 404 NotFoundPage & 500 error pages
├── services/         # API & transport (http, signalr, session, feature APIs)
├── store/            # Zustand stores (uiStore for theme, systemStore)
└── utils/            # Formatters (bytes, speed, duration, status)
```

---

## Features

- **Dashboard** — Live metrics (active, paused, completed, speeds, storage) and a snapshot of active transfers
- **Downloads** — Paginated, filterable, searchable torrent list with per-torrent actions (start, pause, remove)
- **Torrent Details** — Progress, speeds, file tree with individual file progress and priority, plus peer & tracker tabs
- **Video Streaming** — Stream partially-downloaded video files directly in-app via a scoped, short-lived media token
- **Add Torrent** — Support for magnet links and `.torrent` file uploads with a folder picker
- **RSS Automation** — Subscribe to feeds, add filter rules, and auto-download matching releases
- **Built-in Search** — Query Apibay and YTS from within the app
- **Settings** — Manage speed caps, default save path, DHT/PEX/LPD toggles, and Anchor Mode
- **Statistics** — Network throughput and session statistics
- **Real-time Updates** — SignalR hubs push live torrent state changes and connection status to the UI
- **Automatic Updates** — Signed Tauri updater with in-app update checks and progress feedback
- **Dark / Light Mode** — Full theme toggle, persisted via Zustand
- **Onboarding Tour** — Interactive walkthrough for first-time users
- **Error Pages** — Backend failure screen plus standalone full-screen 404 and 500 error pages
- **Confirmation Modals** — Destructive actions (delete) require user confirmation

---

## Getting Started

### Prerequisites
- Node.js 20+
- The [HarborTorrent Backend](../backend/README.md) running locally (or bundled as the Tauri sidecar)

### Installation

```bash
# UI only (Vite dev server, proxies API calls to the backend)
npm install
npm run dev

# Full desktop app in development mode (launches the Tauri shell + sidecar)
npm run tauri dev
```

The standalone dev server runs at `http://localhost:5173`.

---

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start the Vite development server |
| `npm run build` | Type-check and build for production |
| `npm run preview` | Preview the production build locally |
| `npm run tauri dev` | Run the full Tauri desktop app in development |
| `npm run tauri build` | Build the packaged desktop application |
| `npx tsc --noEmit` | Type-check without emitting files |
