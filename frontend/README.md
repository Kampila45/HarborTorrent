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
| Geist Font | Typography |

---

## Project Structure

```
src/
├── app/              # App entry, routing (App.tsx), global types
├── components/       # Shared UI (Pagination, PageSkeleton, ConfirmDeleteModal)
├── features/
│   ├── dashboard/    # Overview page with metrics & active transfers
│   ├── downloads/    # Full torrent list with search, filter & pagination
│   ├── completed/    # Completed/seeding torrents
│   ├── statistics/   # Network statistics page
│   ├── torrents/     # Torrent detail page, hooks, AddTorrentModal
│   ├── directories/  # Folder browser modal for save path selection
│   ├── layout/       # DashboardShell (sidebar + topbar layout wrapper)
│   └── error/        # 404 NotFoundPage & 500 InternalServerErrorPage
├── services/         # API service functions (torrents, directories)
├── store/            # Zustand stores (uiStore for theme, modal state)
└── utils/            # Formatters (bytes, speed, duration, status)
```

---

## Features

- **Dashboard** — Live metrics (active, paused, completed, speeds, storage) and a snapshot of active transfers
- **Downloads** — Paginated, filterable, searchable torrent list with per-torrent actions (start, pause, remove)
- **Torrent Details** — Progress, speeds, file tree with individual file progress and priority
- **Add Torrent** — Support for magnet links and `.torrent` file uploads with a folder picker
- **Real-time Updates** — SignalR hub pushes live torrent state changes to the frontend
- **Dark / Light Mode** — Full theme toggle, persisted via Zustand
- **Error Pages** — Standalone full-screen 404 and 500 error pages
- **Confirmation Modals** — Destructive actions (delete) require user confirmation

---

## Getting Started

### Prerequisites
- Node.js 20+
- The [Harbor Backend](../Harbor%20-%20Backend/README.md) running locally

### Installation

```bash
npm install
npm run dev
```

The dev server runs at `http://localhost:5173` and proxies API calls to the backend.

---

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start development server |
| `npm run build` | Type-check and build for production |
| `npm run preview` | Preview production build locally |
