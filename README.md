# Carsties

Açık artırma (auction) demosu: **.NET 10 mikroservisleri** + **Next.js 16** arayüzü, tek alan adı
üzerinde path bazlı yayınlanır.

| Ortam | Adres |
|---|---|
| Canlı | <https://umitsengul.online/carsties/> |
| Kök sayfa | <https://umitsengul.online/> (statik karşılama sayfası) |

## Mimari

```
Browser
  │  https://umitsengul.online
  ▼
edge nginx (TLS sonlandırma, path yönlendirme)
  ├── /                          -> statik landing (deploy/landing)
  ├── /carsties/                 -> web-app     (Next.js, basePath=/carsties)
  ├── /carsties/api/auth/        -> web-app     (better-auth route handler)
  ├── /carsties/api/             -> gateway-svc (YARP; önek silinir)
  └── /carsties/id/              -> identity-svc (Duende IdentityServer, PathBase)

web-app ──HTTP──> gateway-svc ──> auction-svc / bid-svc / search-svc / notify-svc
                        │                        │
                        └── (JWT doğrulama) ───> identity-svc (issuer: /carsties/id)
postgres · rabbitmq · meilisearch
```

| Servis | Teknoloji | Sorumluluk |
|---|---|---|
| `web-app` | Next.js 16 (App Router, standalone) | UI + better-auth (OIDC client) |
| `identity-svc` | Duende IdentityServer 8 + ASP.NET Identity | OIDC/OAuth2 sağlayıcı, login/register Razor Pages |
| `gateway-svc` | YARP | Tek giriş noktası, CORS, JWT doğrulama |
| `auction-svc` | .NET + WolverineFx + gRPC | Açık artırma CRUD + `AuctionCreated/Finished` olayları |
| `bid-svc` | .NET + Dapper + gRPC | Teklifler, kazanan bildirimi |
| `search-svc` | .NET + Meilisearch | Arama indeksi (RabbitMQ tüketicisi) |
| `notify-svc` | .NET + SignalR | Gerçek zamanlı bildirimler |

## Lokal geliştirme

```bash
# 1) Altyapı + servisler (Docker)
docker compose up -d --build

# 2) Frontend (hot reload)
cd frontend/web-app
npm ci
npm run dev          # http://localhost:3000
```

Lokal geliştirmede `NEXT_PUBLIC_BASE_PATH` **boştur** → uygulama kök dizinden çalışır.
Prod'da `/carsties` altına taşınır (bkz. `frontend/web-app/.env.production`).
Servis portları (host): gateway `6001`, identity `5001`, auction `7001` (+gRPC `7777`),
search `7002`, bid `7003`, notify `7004`, postgres `5432`, rabbitmq `15672`, meili `7700`.

Test hesapları: `alice` / `Pass123$` · `bob` / `Pass123$`.

## Yayın (production)

İmajlar GitHub Actions ile build edilip **GHCR**'ye (`ghcr.io/umitsengul/carstieslive-*`) push edilir,
VPS yalnızca `docker compose pull && up -d` yapar (sunucuda build yok).

```bash
# VPS'te elle deploy / güncelleme
cd /opt/carsties && ./deploy/deploy.sh
```

Ayrıntılar: [`deploy/README-vps.md`](deploy/README-vps.md)

## Dizinler

```
src/                 .NET servisleri (Identity, Gateway, Auction, Bid, Search, Notification, Contracts)
frontend/web-app/    Next.js uygulaması
deploy/              prod compose override, edge nginx, landing, certs.sh, deploy.sh, .env.example
specs/               servis bazlı teknik dokümanlar
```
