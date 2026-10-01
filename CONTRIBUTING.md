# Contributing to HarborTorrent

Thank you for your interest in contributing. Please read this guide before opening a pull request.

---

## Development Setup

### Prerequisites

- [Node.js 20+](https://nodejs.org)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Rust toolchain](https://rustup.rs)

### Running locally

```bash
# Backend
cd backend
dotnet run --project HarborTorrent.Api

# Frontend (in a separate terminal)
cd frontend
npm install
npm run tauri dev
```

---

## Architecture

HarborTorrent follows **Clean Architecture** with **CQRS** (via MediatR) in the backend.

```
backend/
├── HarborTorrent.Domain          # Entities, value objects — no dependencies
├── HarborTorrent.Application     # Use cases (Commands, Queries, Handlers) — depends on Domain
├── HarborTorrent.Infrastructure  # MonoTorrent, SignalR, search providers — depends on Application
├── HarborTorrent.Persistence     # EF Core, SQLite — depends on Application
└── HarborTorrent.Api             # Minimal API endpoints, middleware — depends on all layers
```

The frontend is feature-driven:

```
frontend/src/
├── features/<feature>/
│   ├── api.ts          # Feature-scoped API functions
│   ├── hooks/          # React hooks for queries and mutations
│   ├── components/     # Feature-specific components
│   └── <Feature>Page.tsx
├── services/           # Shared services: http.ts, signalr.ts, session.ts
├── store/              # Zustand global stores
└── app/                # Root app shell, providers, global listeners
```

---

## Coding Principles

Please follow these rules to keep the codebase consistent.

### Backend (.NET)

- All types are `internal sealed` unless they must be public for cross-project use.
- Use `required` properties and `init`-only setters on DTOs and response contracts.
- Commands and queries must be `sealed record` types. Handlers must be `sealed class` types.
- Do not use `var` where the type is not obvious from the right-hand side.
- Do not log secrets — specifically, never log the launch token.

### Frontend (TypeScript / React)

- Feature code belongs in `src/features/<feature>/`. Do not add feature logic to `src/app/`.
- Global state is managed via Zustand stores in `src/store/`. Do not use React context for global state.
- API calls are wrapped in feature-scoped `hooks/` using TanStack Query.
- TypeScript strict mode is enabled. All code must pass `npx tsc --noEmit` with no errors.

---

## Branch Naming

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feat/<short-description>` | `feat/rss-filtering` |
| Bug fix | `fix/<short-description>` | `fix/magnet-link-resolution` |
| Chore / CI | `chore/<short-description>` | `chore/pin-action-shas` |
| Documentation | `docs/<short-description>` | `docs/architecture-diagram` |

---

## Pull Request Expectations

- Keep pull requests focused on a single concern. Avoid mixing unrelated changes.
- All `.NET` code must build without errors or warnings: `dotnet build`.
- All TypeScript code must type-check: `npx tsc --noEmit`.
- Update `backend/CHANGELOG.md` with a user-facing description of the change.
- If you are adding a new endpoint, add it to the Scalar/Swagger explorer.
- Do not commit secrets, tokens, or private keys.

---

## Commit Messages

Follow the [Conventional Commits](https://www.conventionalcommits.org) format:

```
feat(rss): add filter rules for RSS automation
fix(magnet): resolve DHT timeout when tracker is unavailable
chore(ci): pin GitHub Actions to immutable SHAs
docs(readme): add RPM installation instructions
```
