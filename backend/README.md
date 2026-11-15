# HarborTorrent — Backend

The .NET 10 REST API that powers the Harbor torrent management platform. Built with Clean Architecture and CQRS, it bridges the [MonoTorrent](https://github.com/alanmcgovern/monotorrent) download engine with an embedded SQLite database and exposes a typed HTTP API consumed by the [React frontend](../frontend/README.md).

---

## Tech Stack

| Technology | Purpose |
|---|---|
| .NET 10 / ASP.NET Core | Minimal API host |
| Entity Framework Core 10 | ORM & schema migrations |
| SQLite (Embedded) | Persistent data store |
| MediatR | CQRS command/query dispatch |
| FluentValidation | Request validation pipeline |
| MonoTorrent 3.x | Download engine runtime |
| SignalR | Real-time push hub (`/hubs/torrents`, `/hubs/system`) |
| Scalar | Interactive API documentation |
| xUnit | Unit & integration tests |

---

## Architecture

Clean Architecture with a strict unidirectional dependency flow:

```
HarborTorrent.Domain/          → Core entities (Torrent, TorrentStatus, etc.), no dependencies
HarborTorrent.Application/     → CQRS commands & queries, MediatR handlers, abstractions
HarborTorrent.Infrastructure/  → MonoTorrent engine, runtime coordinator, lifecycle service
HarborTorrent.Persistence/     → EF Core DbContext, repositories, migrations
HarborTorrent.Api/             → Minimal API endpoints, middleware, request/response contracts
```

### Key Design Patterns

- **CQRS via MediatR** — Every operation is a `IRequest<T>` dispatched through a handler. Reads and writes are fully separated.
- **Repository pattern** — `ITorrentRepository` / `ISettingsRepository` abstract all DB access so handlers never touch EF Core directly.
- **Runtime Coordinator** — `MonoTorrentRuntimeCoordinator` manages the live engine state in memory, serialising access via a `SemaphoreSlim(1,1)` gate to prevent race conditions.
- **Background Services** — `TorrentLifecycleHostedService` syncs live MonoTorrent state (speed, progress, ETA) back into the database, and `RssPollingHostedService` periodically polls subscribed feeds for matching releases.
- **Result pattern** — All commands return `Result` / `Result<T>` instead of throwing exceptions for expected failures.
- **Layered request pipeline** — Middleware runs in order: `RequestContextMiddleware` (correlation ID) → `SecurityHeadersMiddleware` (CSP & hardening headers) → `ExceptionHandlingMiddleware` (structured errors) → CORS → `LaunchTokenAuthenticationMiddleware` (per-session auth, bypassed only for `/health`).
- **Scoped media tokens** — `StreamTokenService` issues short-lived HMAC-signed tokens so the WebView `<video>` element can stream a specific torrent file without exposing the launch token in the URL.

---

## Project Structure

```
HarborTorrent.Api/
├── Endpoints/                  # Route registration (Torrent, Dashboard, Files, Settings, Search, Rss, System, Directory)
├── Middleware/                 # RequestContext, SecurityHeaders, ExceptionHandling, LaunchTokenAuthentication
├── Security/                   # StreamTokenService (scoped media tokens)
├── Contracts/                  # API request/response models
└── Program.cs                  # App composition root (CORS, middleware pipeline, hub mapping)

HarborTorrent.Application/
├── Abstractions/               # Runtime, Persistence & Rss interfaces
├── Behaviors/                  # MediatR pipeline (validation)
├── Contracts/                  # Shared DTOs (TorrentDto, SettingsDto, DashboardMetricsDto, …)
└── Features/                   # One folder per use case:
    ├── Torrents/               # Add, List, Get, Files, Peers, Trackers, Start, Pause, Stop, Remove
    ├── Dashboard/GetMetrics/
    ├── Directories/ListDirectories/
    ├── Rss/                    # AddFeed, AddFilter, DeleteFeed, DeleteFilter, GetFeeds
    ├── Search/SearchTorrents/
    ├── Settings/               # GetSettings, UpdateSettings
    └── System/GetChangelog/

HarborTorrent.Domain/
├── Torrents/                   # Torrent aggregate, TorrentStatus enum
└── Common/                     # Result<T>, shared interfaces

HarborTorrent.Infrastructure/
├── Runtime/                    # MonoTorrentRuntimeCoordinator, TorrentLifecycleHostedService
├── BackgroundJobs/             # RssPollingHostedService, RssPollStatusTracker
├── Realtime/                   # TorrentHub, SystemHub, LaunchTokenHubFilter
├── Search/                     # ApibaySearchProvider, YtsSearchProvider
└── DependencyInjection.cs      # Engine, search & realtime service registration

HarborTorrent.Persistence/
├── HarborDbContext.cs
├── Repositories/               # TorrentRepository, SettingsRepository
└── Migrations/

HarborTorrent.Tests/            # xUnit tests (auth, streaming, lifecycle, engine settings, RSS, dashboard, settings)
```

---

## API Reference

### Torrents — `/api/v1/torrents`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/torrents` | Paginated torrent list. Params: `page`, `pageSize`, `status`, `q` |
| `GET` | `/api/v1/torrents/{id}` | Get a single torrent by ID |
| `GET` | `/api/v1/torrents/{id}/files` | Get individual file list with progress & priority |
| `PUT` | `/api/v1/torrents/{id}/files/priority` | Update per-file download priority |
| `POST` | `/api/v1/torrents` | Add torrent via magnet link or `.torrent` file (base64) |
| `POST` | `/api/v1/torrents/{id}/start` | Start or resume a torrent |
| `POST` | `/api/v1/torrents/{id}/pause` | Pause a torrent |
| `POST` | `/api/v1/torrents/{id}/stop` | Stop a torrent |
| `DELETE` | `/api/v1/torrents/{id}` | Remove a torrent |
| `GET` | `/api/v1/torrents/{id}/peers` | Live connected peers |
| `GET` | `/api/v1/torrents/{id}/trackers` | Configured trackers |
| `POST` | `/api/v1/torrents/{id}/trackers` | Add a tracker to a torrent |

### Files — `/api/v1/files`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/files/download` | Download a completed file (containment-validated against torrent identity) |
| `POST` | `/api/v1/files/stream-token` | Issue a short-lived scoped media token for streaming |
| `GET` | `/api/v1/files/stream` | Stream a partially-downloaded file with HTTP range support |

### Dashboard — `/api/v1/dashboard`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/dashboard/metrics` | Aggregate metrics (active count, total speed, storage) |

### Settings — `/api/v1/settings`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/settings` | Current engine settings (speed caps, default path, DHT/PEX/LPD, Anchor Mode) |
| `PUT` | `/api/v1/settings` | Update engine settings |

### Directories — `/api/v1/directories`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/directories` | List filesystem directories for save-path selection |

### Search — `/api/v1/search`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/search` | Search torrent providers (Apibay, YTS). Param: `q` |

### RSS — `/api/v1/rss`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/rss` | List subscribed feeds (includes last poll status) |
| `POST` | `/api/v1/rss` | Add a feed |
| `DELETE` | `/api/v1/rss/{id}` | Delete a feed |
| `POST` | `/api/v1/rss/{id}/poll` | Poll a feed immediately |
| `POST` | `/api/v1/rss/{id}/filters` | Add a filter rule to a feed |
| `DELETE` | `/api/v1/rss/filters/{filterId}` | Delete a filter rule |

### System — `/api/v1/system`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/system/changelog` | Embedded `CHANGELOG.md` content |
| `GET` | `/api/v1/system/version` | Current application version |
| `POST` | `/api/v1/system/shutdown` | Gracefully stop the sidecar (used on app exit) |

### Other

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Health check (DB status, version, commit SHA, uptime) |
| `GET /scalar` | Interactive API docs (Scalar UI) |
| `GET /swagger` | OpenAPI document |

### Real-time — SignalR hubs

| Hub | Purpose |
|-----|---------|
| `/hubs/torrents` | Live torrent state (progress, speed, ETA, status changes) |
| `/hubs/system` | System-level events (engine lifecycle, updater notifications) |

Both hubs are authenticated with the same per-session launch token as the REST API.

All responses are wrapped in a consistent envelope:
```json
{
  "success": true,
  "data": { ... },
  "requestId": "req_abc123",
  "timestamp": "2026-07-12T18:00:00Z"
}
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- No database setup required — SQLite runs embedded out of the box.

### Setup

```bash
# 1. Navigate to the backend
cd backend

# 2. Run — EF Core migrations are applied automatically on startup
dotnet run --project HarborTorrent.Api
```

| URL | Description |
|-----|-------------|
| `http://localhost:5000` | REST API (override with `HARBOR_API_PORT`) |
| `http://localhost:5000/scalar` | Interactive API docs |
| `http://localhost:5000/health` | Health check |

> **Environment variables** — when launched by the Tauri host the sidecar receives `HARBOR_LAUNCH_TOKEN` (per-session auth token) and `HARBOR_API_PORT` (loopback port). When run standalone in the `Development` environment these fall back to a dev token and port `5000`; in packaged/`Production` builds a missing `HARBOR_LAUNCH_TOKEN` is a fatal startup error.

---

## Database Migrations

```bash
# Apply migrations (Development)
dotnet ef database update --project HarborTorrent.Persistence --startup-project HarborTorrent.Api

# Apply migrations (Production)
ASPNETCORE_ENVIRONMENT=Production dotnet ef database update \
  --project HarborTorrent.Persistence \
  --startup-project HarborTorrent.Api

# Create a new migration
dotnet ef migrations add <MigrationName> \
  --project HarborTorrent.Persistence \
  --startup-project HarborTorrent.Api
```

---

## Commands

| Command | Description |
|---------|-------------|
| `dotnet run --project HarborTorrent.Api` | Start the API server |
| `dotnet build` | Build the entire solution |
| `dotnet test HarborTorrent.slnx` | Run the xUnit test suite |
| `dotnet ef migrations add <Name>` | Scaffold a new EF Core migration |
| `dotnet ef database update` | Apply pending migrations |

---

## Testing

Tests live in [`HarborTorrent.Tests`](./HarborTorrent.Tests) and run with xUnit:

```bash
dotnet test HarborTorrent.slnx
```

Coverage includes launch-token authentication (HTTP & SignalR), scoped stream tokens, domain pause/resume transitions, torrent lifecycle restoration, engine (DHT/PEX) settings, dashboard aggregation, RSS feed/link/regex handling, and settings validation.
