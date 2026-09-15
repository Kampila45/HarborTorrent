# HarborTorrent — Backend

The .NET 10 REST API that powers the Harbor torrent management platform. Built with Clean Architecture and CQRS, it bridges the [MonoTorrent](https://github.com/alanmcgovern/monotorrent) download engine with a PostgreSQL database and exposes a typed HTTP API consumed by the [React frontend](../Harbor%20-%20Frontend/README.md).

---

## Tech Stack

| Technology | Purpose |
|---|---|
| .NET 10 / ASP.NET Core | Minimal API host |
| Entity Framework Core 10 | ORM & schema migrations |
| PostgreSQL (Neon) | Persistent data store |
| MediatR | CQRS command/query dispatch |
| FluentValidation | Request validation pipeline |
| MonoTorrent 3.x | Download engine runtime |
| Scalar | Interactive API documentation |

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
- **Repository pattern** — `ITorrentRepository` abstracts all DB access so handlers never touch EF Core directly.
- **Runtime Coordinator** — `MonoTorrentRuntimeCoordinator` manages the live engine state in memory, serialising access via a `SemaphoreSlim(1,1)` gate to prevent race conditions.
- **Background Service** — `TorrentLifecycleHostedService` ticks every few seconds to sync live MonoTorrent state (speed, progress, ETA) back into the database.
- **Result pattern** — All commands return `Result` / `Result<T>` instead of throwing exceptions for expected failures.

---

## Project Structure

```
HarborTorrent.Api/
├── Endpoints/                  # Route registration (TorrentEndpoints, DashboardEndpoints)
├── Middleware/                 # Global exception handler
├── Contracts/                  # API request/response models
└── Program.cs                  # App composition root

HarborTorrent.Application/
├── Abstractions/Runtime/       # ITorrentRuntimeCoordinator interface
├── Behaviors/                  # MediatR pipeline (validation)
├── Contracts/Torrents/         # Shared DTOs (TorrentDto, TorrentFileDto, etc.)
└── Features/Torrents/          # One folder per use case:
    ├── AddTorrent/
    ├── ListTorrents/
    ├── GetTorrent/
    ├── GetTorrentFiles/
    ├── UpdateFilesPriority/
    ├── StartTorrent/
    ├── PauseTorrent/
    ├── StopTorrent/
    └── RemoveTorrent/

HarborTorrent.Domain/
├── Torrents/                   # Torrent aggregate, TorrentStatus enum
└── Common/                     # Result<T>, shared interfaces

HarborTorrent.Infrastructure/
└── Runtime/
    ├── MonoTorrentRuntimeCoordinator.cs   # Engine management & file operations
    └── TorrentLifecycleHostedService.cs   # Background sync loop

HarborTorrent.Persistence/
├── HarborDbContext.cs
├── Repositories/TorrentRepository.cs
└── Migrations/
```

---

## API Reference

### Torrents — `/api/v1/torrents`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/torrents` | Paginated torrent list. Params: `page`, `pageSize`, `status`, `q` |
| `GET` | `/api/v1/torrents/:id` | Get a single torrent by ID |
| `GET` | `/api/v1/torrents/:id/files` | Get individual file list with progress & priority |
| `PUT` | `/api/v1/torrents/:id/files/priority` | Update per-file download priority |
| `POST` | `/api/v1/torrents` | Add torrent via magnet link or `.torrent` file (base64) |
| `POST` | `/api/v1/torrents/:id/start` | Start or resume a torrent |
| `POST` | `/api/v1/torrents/:id/pause` | Pause a torrent |
| `POST` | `/api/v1/torrents/:id/stop` | Stop a torrent |
| `DELETE` | `/api/v1/torrents/:id` | Remove a torrent |

### Dashboard — `/api/v1/dashboard`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/dashboard/metrics` | Aggregate metrics (active count, total speed, storage) |

### Other

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Health check |
| `GET /scalar` | Interactive API docs (Scalar UI) |

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
- A PostgreSQL database (or a [Neon](https://neon.tech) serverless connection string)

### Setup

```bash
# 1. Clone and navigate to the backend
cd "Harbor - Backend"

# 2. Add your connection string
# Edit HarborTorrent.Api/appsettings.Development.json:
# {
#   "ConnectionStrings": {
#     "HarborTorrent": "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
#   }
# }

# 3. Run — EF Core migrations are applied automatically on startup
dotnet run --project HarborTorrent.Api
```

| URL | Description |
|-----|-------------|
| `http://localhost:5149` | REST API |
| `http://localhost:5149/scalar` | Interactive API docs |
| `http://localhost:5149/health` | Health check |

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
| `dotnet ef migrations add <Name>` | Scaffold a new EF Core migration |
| `dotnet ef database update` | Apply pending migrations |
