# Fluxgate — Production-Grade API Gateway

A full-stack API Gateway built with .NET 8, YARP, Redis, and React 18. Supports dynamic routing, dual authentication (JWT RS256 + API Key), rate limiting, Polly-based resilience, response caching, structured logging, and a live admin dashboard.

---

## Architecture

```
  Browser / API Client
        │
        ├──► Auth Service  :5100   POST /auth/login → RS256 JWT
        │                          POST /auth/apikeys → raw API key (shown once)
        │                          GET  /.well-known/jwks.json
        │
        └──► React Admin UI  :5173
                    │
                    ▼
             API Gateway  :5000
                    │  ┌────────────────────────────────────────────────┐
                    │  │  Middleware Pipeline (outermost → innermost)   │
                    │  │  1. Serilog request logging                    │
                    │  │  2. Prometheus MetricsMiddleware               │
                    │  │  3. Authentication (JWT RS256 / API Key)       │
                    │  │  4. RouteAuthorizationMiddleware               │
                    │  │  5. RateLimitMiddleware (Redis sliding window) │
                    │  │  6. ResponseCacheMiddleware (Redis TTL)        │
                    │  │  7. ResilienceMiddleware (Polly retry + CB)    │
                    │  │  8. YARP Reverse Proxy                         │
                    │  └────────────────────────────────────────────────┘
                    │
                    ├──► Downstream Services    (YARP dynamic proxy)
                    ├──► PostgreSQL :5432       (routes, users, api_keys)
                    ├──► Redis      :6379       (rate limit counters, response cache)
                    └──► Seq        :5341       (structured logs via Serilog)

Logs:    Gateway → Serilog → Seq → GET /logs (Seq HTTP API) → Admin UI
Metrics: Gateway → prometheus-net /metrics → Prometheus :9090 → Grafana :3001
                                           → GET /metrics/summary → Admin UI
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Quick Start

### 1. Start infrastructure

```bash
docker compose up -d
```

Services started:

| Service    | URL                        |
|------------|----------------------------|
| PostgreSQL | localhost:5432             |
| Redis      | localhost:6379             |
| Seq        | http://localhost:5341      |
| Prometheus | http://localhost:9090      |
| Grafana    | http://localhost:3001      |

### 2. Apply database migrations

```bash
dotnet ef database update --project src/Gateway.Infrastructure --startup-project src/Gateway.API --context GatewayDbContext
dotnet ef database update --project src/Gateway.Infrastructure --startup-project src/Gateway.API --context AuthDbContext
```

### 3. Start backend services

```bash
# Terminal 1 — Gateway
dotnet run --project src/Gateway.API

# Terminal 2 — Auth Service
dotnet run --project src/Gateway.AuthService
```

### 4. Start frontend

```bash
cd frontend
npm install
npm run dev
```

Open [http://localhost:5173](http://localhost:5173). Log in with the seeded admin user.

---

## Service Endpoints

| Service         | URL                        | Notes                        |
|-----------------|----------------------------|------------------------------|
| Gateway API     | http://localhost:5000      | Dynamic proxy + management   |
| Auth Service    | http://localhost:5100      | JWT + API key issuance       |
| React Admin UI  | http://localhost:5173      | Dev server                   |
| Seq Logs UI     | http://localhost:5341      |                              |
| Prometheus      | http://localhost:9090      |                              |
| Grafana         | http://localhost:3001      | admin / admin                |
| PostgreSQL      | localhost:5432             | fluxgate / fluxgate          |
| Redis           | localhost:6379             |                              |

---

## API Reference

### Auth Service (`:5100`)

| Method | Path                     | Auth          | Description                          |
|--------|--------------------------|---------------|--------------------------------------|
| POST   | `/auth/register`         | —             | Register a new user                  |
| POST   | `/auth/login`            | —             | Get JWT access token                 |
| POST   | `/auth/apikeys`          | Bearer admin  | Create API key (rawKey shown once)   |
| GET    | `/.well-known/jwks.json` | —             | RS256 public key (JWKS format)       |

### Gateway (`:5000`)

| Method | Path                    | Auth          | Description                          |
|--------|-------------------------|---------------|--------------------------------------|
| GET    | `/health`               | —             | Health check                         |
| GET    | `/metrics`              | —             | Prometheus metrics scrape endpoint   |
| GET    | `/metrics/summary`      | Bearer        | JSON metrics summary for the UI      |
| GET    | `/logs`                 | Bearer        | Paginated logs (queries Seq API)     |
| GET    | `/gateway/routes`       | Bearer        | List all routes                      |
| POST   | `/gateway/routes`       | Bearer admin  | Create a route                       |
| PUT    | `/gateway/routes/{id}`  | Bearer admin  | Update a route                       |
| DELETE | `/gateway/routes/{id}`  | Bearer admin  | Delete a route                       |
| `*`    | `/{any}`                | Per-route     | YARP dynamic reverse proxy           |

#### Route body fields

```json
{
  "path": "/api/orders",
  "method": "GET",
  "destination": "http://orders-service:8080",
  "authRequired": true,
  "roles": ["admin"],
  "rateLimit": { "limit": 100, "windowSeconds": 60 },
  "cacheTtlSeconds": 30,
  "isActive": true
}
```

### Authentication

- **JWT Bearer**: `Authorization: Bearer <token>` — RS256, verified via JWKS (no Auth Service round-trip per request)
- **API Key**: `X-Api-Key: <raw-key>` — stored as SHA-256 hash; raw key is shown once at creation

---

## Running Tests

```bash
dotnet test
```

Tests include unit, integration (YARP, Polly, response cache, request transforms), observability, and route service tests.

---

## Project Structure

```
src/
  Gateway.API/             ASP.NET Core — YARP proxy, middleware pipeline
    Auth/                  ApiKey handler, route authorization middleware
    Metrics/               prometheus-net registry + /metrics/summary controller
    Middleware/            Rate limit, response cache, Prometheus middleware
    Proxy/                 DatabaseProxyConfigProvider, GatewayTransformProvider
    Resilience/            Polly ResilienceMiddleware (retry + circuit breaker)
  Gateway.AuthService/     ASP.NET Core — token issuance, user management
  Gateway.Core/            Domain models, interfaces, DTOs, FluentValidation
  Gateway.Infrastructure/  EF Core, repositories, Seq client
  Gateway.Tests/           xUnit — unit + integration tests
frontend/                  Vite React 18 app (Zustand, Axios, Recharts, Tailwind)
docker-compose.yml
prometheus.yml
```

---

## Phase Highlights

| Phase | Feature                                           |
|-------|---------------------------------------------------|
| 1     | Solution structure, EF Core, PostgreSQL           |
| 2     | Route CRUD API + FluentValidation                 |
| 3     | Auth Service — RS256 JWT + API key issuance       |
| 4     | YARP dynamic proxy from database routes           |
| 5     | JWT + API key enforcement, role-based access      |
| 6     | Redis sliding-window rate limiting                |
| 7     | Serilog → Seq, prometheus-net, Grafana            |
| 8     | React 18 frontend foundation (Zustand, Axios)     |
| 9     | Routes management UI (CRUD with optimistic UX)    |
| 10    | Metrics & Logs dashboard (Recharts, pagination)   |
| 11    | Polly resilience, response cache, role-based UI   |

---

## Resilience (Phase 11)

Every proxied route is wrapped in a Polly v8 `ResiliencePipeline` (keyed by route ID):

- **Retry**: max 3 attempts, exponential backoff starting at 200ms
- **Circuit Breaker**: opens after 5 failures in a 30-second window; half-open probe after 60s

Routes with `cacheTtlSeconds > 0` are cached in Redis:
- Key: `cache:{METHOD}:{path}:{queryHash-8chars}`
- Bypassed on `Cache-Control: no-cache`
- Only `GET`/`HEAD` 2xx responses are cached

Request transforms injected by `GatewayTransformProvider` for every proxied request:
- `X-Forwarded-User` — authenticated username (empty for anonymous)
- `X-Request-Id` — ASP.NET Core `TraceIdentifier`
- `X-Gateway-Version` — `fluxgate/1.0`

---

## Scaling Notes

- **Horizontal gateway scaling**: Redis rate limit and cache are shared; multiple Gateway instances are safe behind a load balancer.
- **Auth service**: Stateless JWT validation (JWKS cached in-memory per instance). Auth Service is not on the hot request path.
- **YARP hot-reload**: Route changes are pushed via `RouteChangeNotifier` without restarting the process.
- **Resilience registry**: `ResiliencePipelineRegistry<string>` is a singleton; circuit-breaker state is per-instance. For distributed circuit breaking, use a shared state store.
- **Prometheus metrics**: Scrape interval 10s. High-cardinality route labels are truncated in `MetricsMiddleware`.

---

## Trade-Off Log

| Decision                     | Choice                        | Rationale                                                    |
|------------------------------|-------------------------------|--------------------------------------------------------------|
| Auth architecture            | Separate Auth Service         | Decoupled; realistic microservice boundary                   |
| Token format                 | RS256 JWT                     | Asymmetric; Gateway validates without calling Auth Service   |
| API key storage              | SHA-256 hash only             | Raw key shown once; safe under DB breach                     |
| Log storage                  | Seq HTTP API                  | Avoids duplicating logs in Postgres; Seq has retention/search|
| Rate limit algorithm         | Redis sliding window          | Accurate; distributed-safe across instances                  |
| Proxy engine                 | YARP                          | Microsoft-supported; native ASP.NET DI integration           |
| Metrics storage              | prometheus-net (in-process)   | No DB overhead; standard Prometheus pull model               |
| Frontend state               | Zustand                       | Lightweight; no Redux boilerplate                            |
| Circuit breaker state        | In-process (Polly registry)   | Simple; distributed CB would need Redis Lua scripts          |
| Response cache invalidation  | TTL only (no active purge)    | Sufficient for read-heavy proxy use cases                    |

---
