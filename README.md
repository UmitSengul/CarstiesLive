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

### Şu anki akış — GitHub Actions **yok** (03.11.2026'dan itibaren)

CI/CD **GitHub Actions'a bağlı değildir**; yalnızca kaynak kod private repo'da durur.
Release, geliştirici makinesinde çalışır ve imajlar SSH ile VPS'e taşınır (registry/PAT yok):

```bash
./deploy/quality.sh                    # kalite kapısı (dotnet build + NuGet/npm audit + lint + next build)
./deploy/release.sh                     # yerel build -> docker save | ssh | docker load -> VPS deploy
./deploy/release.sh --only web-app      # tek servis (en hızlı yol)
```

VPS yalnızca `up -d` yapar (sunucuda build yok: 2 vCPU / 2 GB RAM canlı yığını barındırıyor).

Carsties kodu **`path-migration`** dalında yaşar (`main` bu repoda başka bir hatta — kurs
spec'leri). `deploy/release.sh` VPS'teki klonu bu dala eşitler.

```bash
# VPS'te elle deploy / güncelleme (imajlar zaten yüklüyse)
ssh vps 'cd /opt/carsties && IMAGE_SOURCE=local IMAGE_TAG=sha-<kisa> ./deploy/deploy.sh'

# geri alma (imaj etiketi ile)
ssh vps 'cd /opt/carsties && IMAGE_SOURCE=local IMAGE_TAG=sha-<eski-sha> SKIP_GIT=1 ./deploy/deploy.sh'
```

### Önceki akış — GitHub Actions + GHCR (02.11.2026 – 03.11.2026, artık kullanılmıyor)

Bu tarihe kadar deploy, `.github/workflows/pipeline.yml` içindeki 3 aşamalı GitHub Actions
pipeline'ı ile yapılıyordu:

| Aşama | Nerede | Ne yapıyordu |
|---|---|---|
| `quality` | GitHub runner | `dotnet restore/build` + NuGet zafiyet kapısı, `npm ci`, `npm audit --omit=dev`, `npm run lint`, prod `basePath` ile `next build`, `jq` ile `routes-manifest.json` doğrulaması |
| `build-and-push` | GitHub runner (matrix, 7 iş) | Her servis için `ghcr.io/umitsengul/carstieslive-<servis>` imajını build edip GHCR'ye **push** (`latest` + `sha-<7>`, `type=gha` cache) |
| `deploy` | GitHub runner → SSH | `appleboy/ssh-action` ile VPS'e bağlanıp `deploy/deploy.sh` çalıştırıyordu (`DEPLOY_BRANCH=<push edilen dal>`) |

Akış: `path-migration`/`main` dalına **push** → quality → GHCR'ye 7 imaj → VPS'te
`docker compose pull && up -d` (sunucuda build yok, `pull_policy: always`).

Gereken GitHub **secrets**: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` (private key), `GHCR_USER`,
`GHCR_PAT` (`read:packages`). İmajlar GHCR'de public paket olarak duruyordu.

**Neden bırakıldı (03.11.2026):**
- `deploy` job'ı secret'lar tanımlanmadığı için hiç yeşile dönmedi → her push'ta kırmızı pipeline;
  deploy pratikte elle yapılıyordu, yani pipeline "yarım" çalışıyordu.
- Deploy'un tamamı GitHub'a bağımlıydı: imaj build kuyruğu, GHCR push/pull, PAT yönetimi,
  runner → VPS SSH. GitHub tarafında yaşanan kesinti/kota/secret sorunları doğrudan yayını etkiliyordu.
- İmaj başına ~400 MB'ı GHCR'ye push edip oradan çekmek, tek sunuculuk bu kurulum için gereğinden
  fazla hareketli parça (registry + kimlik doğrulama + paket görünürlüğü) getiriyordu.
- Dev makinesi 16 vCPU / 29 GiB; VPS 2 vCPU / 3.5 GiB. Build'i bu kadar güçlü bir makinede yapıp
  sonucu doğrudan taşımak daha hızlı (ölçüldü: 400 MB imaj → ~18 s transfer) ve GitHub'a bağımsız.

**Bugün GitHub'da kalan:** private repo + (isteğe bağlı, elle tetiklenen) `quality (manuel)`
workflow'u + `dependabot.yml`. Push/PR olaylarında **hiçbir şey çalışmaz**.

**Eski model hâlâ mümkün:** `IMAGE_SOURCE=registry` modu korundu. İmajları herhangi bir yolla
(GHCR, self-hosted registry, `docker push`) bir registry'ye koyup
`ssh vps 'cd /opt/carsties && IMAGE_SOURCE=registry IMAGE_TAG=<tag> ./deploy/deploy.sh'`
ile eski "pull" davranışına dönebilirsin.

### Karşılaştırma

| | Eski (GitHub Actions + GHCR) | Yeni (yerel release + SSH) |
|---|---|---|
| Tetikleme | GitHub'a push | `./deploy/release.sh` (yerelde) |
| Build yeri | GitHub runner (7 paralel iş) | Dev makinesi (16 vCPU) |
| İmaj dağıtımı | GHCR push → VPS pull | `docker save \| zstd \| ssh \| docker load` |
| GitHub bağımlılığı | Workflow + secret + PAT + paket izni | **Yok** (yalnız kod barındırma) |
| Kalite kapısı | `quality` job | `./deploy/quality.sh` (aynı adımlar) |
| Deploy süresi | ~6–9 dk (queue + build + push + pull) | ~1–2 dk (cache'li build + transfer) |
| Gereken sırlar | 5 GitHub secret + PAT | Sadece SSH anahtarı (deploy key) |
| Rollback | `IMAGE_TAG=sha-<eski>` + pull | `IMAGE_TAG=sha-<eski>` + `IMAGE_SOURCE=local`, imaj VPS'te durur |
| Ölçeklenebilirlik | Birden fazla sunucu/ortam için uygun | Tek sunucu için ideal (çoklu ortamda registry eklenmeli) |

Ayrıntılar: [`deploy/README-vps.md`](deploy/README-vps.md)

## Yol haritası

Neler tamamlandı, neler sırada? (responsive tasarım, koyu/açık tema seçici, test altyapısı,
izleme/yedekleme, güvenlik sertleştirmeleri…): [`ROADMAP.md`](ROADMAP.md)

## Dizinler

```
src/                 .NET servisleri (Identity, Gateway, Auction, Bid, Search, Notification, Contracts)
frontend/web-app/    Next.js uygulaması
deploy/              prod compose override, edge nginx, landing, certs.sh, .env.example
                     deploy.sh (VPS deploy) · release.sh (yerel build+transfer) · quality.sh (kalite kapısı)
specs/               servis bazlı teknik dokümanlar
ROADMAP.md           yapılanlar + yapılacaklar (önceliklendirilmiş backlog)
```
