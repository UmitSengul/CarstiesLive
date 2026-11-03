# Carsties — Yapılanlar ve Yol Haritası

> Son güncelleme: **02.11.2026** · Canlı: <https://umitsengul.online/carsties/> · Kök: <https://umitsengul.online/>
> Operasyon notları: [`deploy/README-vps.md`](deploy/README-vps.md) · Kurulum: [`README.md`](README.md)

Bu dosya iki bölümden oluşur: **yapılanlar** (tamamlanmış işler, doğrulama notlarıyla) ve
**yapılacaklar** (önceliklendirilmiş backlog). Bir madde tamamlandığında `[x]` yapıp altına
tarih + kısa not düşün.

---

## 1. Canlı sistemin bugünkü durumu

| Bileşen | Durum |
|---|---|
| Yayın adresi | `https://umitsengul.online` (tek alan adı, path tabanlı) |
| Uygulama | `https://umitsengul.online/carsties` — Next.js 16.4.4, `basePath=/carsties` |
| Gateway | `https://umitsengul.online/carsties/api` — YARP (önek silinir) |
| Identity | `https://umitsengul.online/carsties/id` — Duende IdentityServer, `PathBase=/carsties/id` |
| Issuer | `https://umitsengul.online/carsties/id` |
| better-auth callback | `https://umitsengul.online/carsties/api/auth/callback/duende` |
| TLS | Let's Encrypt (HTTP-01 / webroot), geçerlilik **31.01.2027** · otomatik yenileme: `carsties-certs.timer` |
| Edge | `nginx:1.29-alpine` (statik config, `deploy/nginx/conf.d/`) — HSTS + WebSocket upgrade |
| İmajlar | `ghcr.io/umitsengul/carstieslive-*:sha-<commit>` — **yerelde build**, VPS'e `docker save \| ssh \| docker load` ile taşınır (registry'den çekilmez) |
| CI/CD | **GitHub Actions yok**: `./deploy/quality.sh` (kapı) → `./deploy/release.sh` (build + taşı + deploy). GitHub yalnız private repo |
| Sunucu | AlmaLinux 9.8 · 2 vCPU / 3.5 GiB RAM + 4 GiB swap · `/opt/carsties` (git klonu) |

---

## 2. Yapılanlar

### 2.1 Tek alan adı + path tabanlı yayına geçiş (02.11.2026)

Eski yapı `app.` / `api.` / `id.umitsengul.online` alt alan adlarıydı; tek alan adı + path modeline taşındı.

- [x] **Edge nginx** yazıldı (`deploy/nginx/conf.d/default.conf`): landing `/`, `/carsties/`, `/carsties/api/auth/` (web-app), `/carsties/api/` (gateway, önek silinir), `/carsties/id/` (identity, önek korunur), WebSocket upgrade + HSTS + güvenlik başlıkları.
- [x] **Landing page** eklendi (`deploy/landing/index.html`) — self-contained, açık/koyu tema uyumlu, Carsties CTA'lı.
- [x] **Next.js `basePath`** desteği (`next.config.ts`, `Dockerfile` build arg `NEXT_PUBLIC_BASE_PATH`).
- [x] **Identity `UsePathBase`** (`src/IdentityService/HostingExtensions.cs`, `PathBase` config) + `IssuerUri` path'li hale getirildi; discovery/cookie/redirect'ler önekli üretiliyor.
- [x] **Gateway/auction/bid** bağlantıları: `ClientApp=https://umitsengul.online` (CORS origin'de path olamaz), `IdentityServiceUrl=http://identity-svc/carsties/id`.
- [x] `deploy/docker-compose.prod.yml` yeniden yazıldı: statik edge, `pull_policy: always`, iç portlar kapatıldı, `restart: unless-stopped`, log rotasyonu.
- [x] `deploy/certs.sh` tek domain'e uyarlandı + **`bootstrap`** komutu (edge sertifikasız açılamaz ↔ certbot edge'e ihtiyaç duyar) ve `nginx -s reload`.
- [x] Doğrulandı: landing **200**, `/carsties` **200**, `/carsties/api/auctions` **200**, `/carsties/id/` **200**, discovery'de tüm endpoint'ler `https://umitsengul.online/carsties/id/...`.

### 2.2 better-auth 1.6.26 → 1.7.15 migrasyonu (kırıcı değişiklikler)

- [x] `genericOAuthClient` client plugin'i **kaldırıldı** (1.7'de yok) → akış `authClient.signIn.social({ provider: "duende" })`.
- [x] `genericOAuth` config'inden `issuer` alanı çıkarıldı (1.7'de yok); endpoint'ler açıkça verildi.
- [x] `getAccessToken({ providerId })` → `listUserAccounts()` + `getAccessToken({ accountId })` (`src/lib/fetch-wrapper.ts`).
- [x] Callback yolu 1.7 varsayılanına alındı: `/api/auth/callback/duende` (`src/IdentityService/Config.cs` redirect URI).
- [x] `basePath` hem sunucu (`auth.ts`) hem client (`auth-client.ts`) tarafında `/carsties/api/auth` olarak ayarlandı.
- [x] **Next.js `basePath`'i `req.url`'den siliyor** → `src/app/api/auth/[...all]/route.ts` içinde istek yolu yeniden öneklendi (aksi halde auth uçları 404).
- [x] Doğrulandı: `/carsties/api/auth/ok` → `{"ok":true}`, sign-in/social → geçerli PKCE'li authorize URL'i, authorize → `303 → /carsties/id/Account/Login`, ROPC token → gateway'de **200**, token'sız **401**.

### 2.3 Bağımlılık güvenliği

| Değişiklik | Gerekçe / sonuç |
|---|---|
| `next` 16.3.0 → **16.4.4** | Image Optimization RCE/SSRF (5 CRITICAL) — `next/image` kullanıldığı için gerçek risk |
| `sharp` → **0.35.5** (override) | RCE zincirinin kökü |
| `better-auth` 1.6.26 → **1.7.15** | magic-link/OAuth state + oauthProxy açıkları |
| `shadcn` → **devDependencies** | express/body-parser/qs/undici/systeminformation runtime'a girmesin |
| `overrides`: `source-map-js`, `nanoid`, `postcss-selector-parser`, `js-yaml`, `fast-uri` | dev zinciri temizliği |
| `Microsoft.EntityFrameworkCore.Tools` 10.0.1 → **10.0.14** | Pkcs 9.0.0 transitif açığı |
| `meilisearch` → **v1.56.0**, edge → **nginx:1.29-alpine** | çalışan sürümlere sabitleme |

- [x] Doğrulandı: `npm audit --omit=dev` → **0 vulnerabilities**; `dotnet package list --vulnerable --include-transitive` → **temiz**.
- [x] CI'da kapılar: `npm audit --omit=dev --audit-level=high` ve NuGet vulnerability gate (JSON + `jq`).

### 2.4 CI/CD — yerel release + SSH (GitHub Actions yok)

- [x] ~~`.github/workflows/pipeline.yml` (quality → GHCR push → SSH deploy)~~ **kaldırıldı** (03.11.2026): GHCR push ve deploy job'ları silindi; dosya yalnız **elle tetiklenen** `quality (manuel)` workflow'una indirildi (push/PR'da hiçbir şey çalışmaz).
- [x] `deploy/quality.sh`: eski CI `quality` job'unun yerel hâli — `dotnet restore/build` + NuGet gate, `npm audit --omit=dev`, `npm run lint`, prod `basePath` ile `next build`, `routes-manifest.json` doğrulaması.
- [x] `deploy/release.sh`: **dev makinesinde** build → `docker save | zstd/gzip | ssh | docker load` → VPS'te `deploy.sh`. `--only <servis>`, `--tag`, `--no-build`, `--host` seçenekleri; kirli ağaçta durur, push edilmemiş commit'i otomatik push eder.
- [x] `deploy/deploy.sh`: `IMAGE_SOURCE=registry|local` (local'de pull yok, `docker-compose.local-images.yml` ile `pull_policy: never`), `SKIP_GIT=1`, kısmi release'te eksik servisi önceki imajla `docker tag` eder, `git reset` sonrası kendini yeniden başlatır (bash eski inode'u okuma tuzağı).
- [x] Inter fontu repoya gömüldü (`next/font/local`, `src/app/fonts/InterVariable.woff2`): build'de `fonts.googleapis.com` bağımlılığı yok, Türkçe karakterler dahil.
- [x] Doğrulandı (03.11.2026): `deploy/release.sh --only web-app` uçtan uca başarılı → `IMAGE_TAG=sha-9f983a3`, 7 servis yeniden oluştu, smoke uyarısız, font yerelden `200 font/woff2`.
- [x] `.github/dependabot.yml`: duruyor (PR üretir, workflow tetiklemez) — istenirse silinebilir.
- [x] `main` dalının farklı bir hatta (kurs spec'leri) olduğu tespit edildi → Carsties kodu **`path-migration`** dalında.
- [x] VPS'te `/opt/carsties` rsync kopyasından **git klonuna** çevrildi (deploy key + `deploy/.env` korunarak; yedek: `/opt/carsties.rsync-bak-20261102-2302`).

### 2.5 VPS operasyonu & TLS

- [x] `deploy/.env` yeni şemaya geçirildi (`DOMAIN`, `IMAGE_TAG`; eski `APP_HOST/API_HOST/ID_HOST` kaldırıldı), izin `600`.
- [x] Let's Encrypt sertifikası alındı (`carsties-prod` lineage) ve edge'e yazıldı; HTTP → HTTPS **301**, HSTS aktif.
- [x] DNS: `umitsengul.online` A kaydı `185.93.70.199` (yayılım Cloudflare/Google DoH ile doğrulandı).
- [x] Firewall doğrulandı: `http https ssh` + `51820/udp` (WireGuard).

### 2.6 Yayın sırasında bulunup düzeltilen hatalar

| # | Belirti | Kök neden | Çözüm |
|---|---|---|---|
| 1 | web-app CI build fail | `build-push-action` `file` yolunu **workspace köküne** göre çözüyor | `dockerfile: frontend/web-app/Dockerfile` |
| 2 | `deploy.sh` "unknown revision origin/main" | Varsayılan dal `main`; VPS'te o dal yok | Varsayılan `path-migration` (CI `DEPLOY_BRANCH` geçiyor) |
| 3 | Landing `/` → **404** | `location = /` içinde `index` internal redirect yapıyor, `location /` (404) yakalıyor | `try_files /index.html =404` |
| 4 | `/carsties` ↔ `/carsties/` yönlendirme döngüsü | nginx 301 + Next 308 | nginx 301 kaldırıldı, istek Next'e gidiyor |
| 5 | Config değişikliği edge'e yansımıyor | Tek dosya bind mount; `git reset` yeni inode → container eski dosyayı görüyor | `deploy/nginx/conf.d/` **dizin mount** |
| 6 | Discovery iç adres üretiyor (`http://carsties_identity/...`) | nginx'te `proxy_set_header` mirası "hepsi ya da hiçbiri": location'da `Upgrade` tanımlıysa server seviyesindeki `Host`/`X-Forwarded-*` miras alınmıyor | `includes/proxy-headers.conf` her proxy location'da include |
| 7 | better-auth uçları 404 | Next.js `req.url`'den `basePath`'i siliyor, better-auth tam yol bekliyor | `[...all]/route.ts` içinde önek yeniden eklendi |

---

## 3. Yapılacaklar

Öncelik: **P1** = kullanıcıyı/operasyonu doğrudan etkiler · **P2** = kalite/teknik borç · **P3** = iyileştirme.
Efor: S (≤ yarım gün) · M (1–2 gün) · L (3+ gün).

### 3.1 Frontend — UX / UI (P1)

- [x] **Responsive tasarım** — liste grid'i, listing detayı, form ve navigasyon mobil/tablet kırılımları. Şu an 49 `.tsx` dosyasının yalnız **8**'inde breakpoint var. · `M`
  - ✅ 02.11.2026: kart ızgarası `grid-cols-1 → sm:2 → lg:3 → xl:4`; `CarImage` `w-full h-auto` + `sizes`; kart rozetleri akışa alındı (`truncate`, `max-w-[calc(100%-1rem)]`); detay sayfası `lg:flex-row` + kolonlar `w-full lg:w-1/2`; meta kutuları `flex-wrap`; header `flex-wrap` + logo metni `md` altında gizli; arama `w-full sm:w-1/2`; filtreler mobilde ikon-only; pagination mobilde sadeleştirildi (`justify-center sm:justify-between` → tek başına kalınca ortalı); `main` `px-4 sm:px-6 lg:px-8 pb-24`; sign-in/empty/error kartları `w-full max-w-md`; form alanları `grid-cols-1 sm:grid-cols-2`; bid panel yükseklikleri `lg` kırılımına bağlandı.
  - Kalan: mobilde hamburger/drawer menü (aşağıdaki madde), 360px'te görsel tur.
- [ ] **Koyu/açık tema seçici** — header'a `Sun/Moon` toggle. CSS altyapısı **hazır**: `src/app/globals.css` içinde `@custom-variant dark (&:is(.dark *))`, `:root` ve `.dark` değişken setleri mevcut; eksik olan `.dark` sınıfını yöneten katman. · `S`
  - `npm i next-themes` → `ThemeProvider` (`attribute="class"`, `defaultTheme="system"`, `enableSystem`, `disableTransitionOnChange`)
  - `src/app/layout.tsx`: `<html lang="tr" suppressHydrationWarning>` + provider sarmalama
  - `src/components/theme-toggle.tsx`: `useTheme()` + `Sun`/`Moon` (lucide-react), mounted kontrolü ile hydration uyuşmazlığını önle
  - **Flash (FOUC) önleme**: `next-themes` inline script'i kullan; alternatif olarak `theme` cookie'si + `cookies()` ile SSR'da `className={theme}` (cookie kalıcılığı istenirse)
  - `prefers-color-scheme` takibi (sistem teması) varsayılan olsun; kullanıcı seçimi sistemin önüne geçsin
- [ ] **Mobil navigasyon** — hamburger + sheet/drawer; `NavBar`'daki linkler mobilde gizli kalıyor. · `S`
- [ ] **Yükleniyor / boş durumlar** — `loading.tsx`, iskelet kartlar, boş liste durumu, hata durumunda `error.tsx` (şu an sadece global `error.tsx` var). · `S/M`
- [ ] **Erişilebilirlik (a11y)** — odak halkaları, `aria-label`'lar, `alt` metinleri, renk kontrastı (koyu temada ayrıca test), klavye ile tam akış, `prefers-reduced-motion`. · `M`
- [ ] **Dil / i18n** — `<html lang="en">` → `tr`; arayüz metinlerini TR/EN sözlüğe taşı (`next-intl` veya basit `dictionaries`). · `M`
- [ ] **Görsel optimizasyon** — `next/image` için `sizes`/`priority`, `placeholder="blur"`, `remotePatterns` daraltma (şu an `cdn.pixabay.com`, `loremflickr.com`). · `S`
- [ ] **Form UX** — alan bazlı hata gösterimi (react-hook-form + zod), submit sırasında disabled state, iyimser (optimistic) bid güncellemesi. · `M`
- [ ] **Bildirim tercihleri** — SignalR toast'ları için sessize alma / kapatma; bildirim geçmişi. · `M`

### 3.2 Frontend — Teknik kalite (P2)

- [ ] **Test altyapısı** — `vitest` + `@testing-library/react` (bileşen/unit), `@playwright/test` (E2E: login → listing → bid → realtime toast). CI'a `npm test` + headless E2E adımı. · `M`
- [ ] **API mock** — `msw` ile `fetchWrapper` çağrılarını izole et (test/Storybook). · `S`
- [ ] **CSP + güvenlik başlıkları** — `next.config.ts` `headers()` ile `Content-Security-Policy` (önce **Report-Only**), `X-Frame-Options`, `Permissions-Policy`. `basePath` nedeniyle nonce/hash stratejisini test et. · `M`
- [ ] **Hata izleme** — Sentry (client+server), `error.tsx` içinden `captureException`, source map yükleme. · `M`
- [ ] **Performans** — `@next/bundle-analyzer`, ağır bileşenlerde `dynamic()`, `web-vitals` raporu (LCP/CLS). · `M`
- [ ] **Lint borcu** — `npm run lint` şu an **0 error / 19 warning** (kullanılmayan import'lar: `BidPanel.tsx`, `CreditCardIcon`, `app-confirm-dialog.tsx`). `--max-warnings=0` öncesi temizle. · `S`
- [ ] **a11y otomasyonu** — `eslint-plugin-jsx-a11y` + Playwright içinde `axe`. · `S`
- [ ] **(P3) Storybook** — `ui/` bileşen kataloğu + görsel regresyon. · `L`

### 3.3 Backend / servisler (P1–P2)

- [ ] **Test projelerini geri getir** — `tests/AuctionService.Tests` ve `tests/SearchService.Tests` altında yalnız `bin/obj` var; `.csproj` ve kaynak **yok**. Testleri yazıp CI'a `dotnet test` ekle (şu an bilinçli olarak yok). · `M`
- [ ] **`ASPNETCORE_ENVIRONMENT`** — prod'da tüm .NET servisleri **`Development`**; Identity `UseDeveloperExceptionPage()` açıyor (stack trace sızabilir). `Production`'a al + `appsettings.Production.json`. · `S` **(güvenlik)**
- [ ] **Healthcheck'ler** — `/health` (liveness/readiness) + compose `healthcheck` (şu an yalnız postgres/rabbitmq/meili için var), gateway'de aggregate health. · `S`
- [ ] **OpenAPI / Swagger** — gateway'de birleşik doküman (`/carsties/api/openapi`). · `M`
- [ ] **Migration stratejisi** — startup'ta otomatik migrate yerine ayrı "migrate" adımı; geri alma ve seed kontrolü. · `M`
- [ ] **Gözlemlenebilirlik** — OpenTelemetry (trace/metric/log), uçtan uca `correlation-id`, Serilog → Loki/Grafana Cloud. · `M`
- [ ] **Rate limiting / brute-force** — Identity `/connect/token` + login uçlarında `RateLimiter`. · `M`
- [ ] **Idempotency** — bid POST'larında `Idempotency-Key`, çift teklif koruması. · `M`
- [ ] **Yarış koşulları** — bid → auction güncellemesinde optimistic concurrency (`RowVersion`), outbox/inbox pattern. · `L`
- [ ] **SignalR ölçeklenebilirlik** — auth (`accessTokenFactory`), reconnect/backoff, çok instance için Redis backplane. · `M`
- [ ] **Identity tamamlanması** — e-posta doğrulama, şifre sıfırlama, hesap kilitleme, (opsiyonel) 2FA. · `L`
- [ ] **gRPC sertleştirme** — servisler arası TLS/mTLS, deadline/timeout politikaları. · `M`

### 3.4 CI/CD ve operasyon (P1)

- [x] ~~**GitHub secrets** (`VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `GHCR_USER`, `GHCR_PAT`)~~ **gerekmiyor**: deploy `deploy/release.sh` ile dev makinesinden yapılıyor (GHCR push yok, PAT yok).
- [x] ~~**Deploy job'ı doğrula**~~ **gerekmiyor**: GitHub'da deploy job'ı kalmadı; uçtan uca akış `deploy/release.sh` ile doğrulandı.
- [ ] **Staging ortamı** — ayrı compose/env (staging path veya subdomain), merge öncesi otomatik deploy. · `L`
- [ ] **Trivy + SBOM** — imaj taraması (CRITICAL/HIGH gate), `sbom: true`, (opsiyonel) cosign imzası. · `M`
- [ ] **Branch protection** — `path-migration` için required checks (quality), PR şablonu, CODEOWNERS. · `S`
- [ ] **Dependabot otomasyonu** — patch auto-merge; aylık `npm audit` / `dotnet list package --vulnerable` rutini. · `S`
- [ ] **Yedekleme** — `pg_dump` cron (auctions/bids/identity) + offsite (S3/rclone) + **restore tatbikatı**. · `M`
- [ ] **İzleme & alarm** — uptime (healthchecks.io/Uptime Kuma), disk %80, RAM/swap, restart sayacı; Telegram/e-posta bildirimi. · `M`
- [ ] **Log yönetimi** — `json-file` rotasyonu var (10m×3); merkezi toplama (Loki/Grafana Cloud). · `M`
- [ ] **Sertifika hijyeni** — `systemctl list-timers` ile `carsties-certs.timer` doğrula, eski `carsties` lineage'ini temizle, periyodik `renew --dry-run`. · `S`
- [ ] **Rollback tatbikatı** — `ssh vps 'cd /opt/carsties && IMAGE_SOURCE=local IMAGE_TAG=sha-<eski> SKIP_GIT=1 ./deploy/deploy.sh'` prosedürünü bir kez prova et. · `S`
- [ ] **Sunucu sertleştirme** — SSH key-only + `fail2ban`, root login kapalı (CI için ayrı deploy kullanıcısı), `docker system prune` cron, swap/overcommit (3.5 GiB RAM). · `M`
- [ ] **(Opsiyonel) CI → WireGuard** — 22 portunu internetten kapatıp deploy'u WireGuard üzerinden yapmak. · `M`

### 3.5 Dokümantasyon (P2)

- [ ] `specs/web-app.md` ve `specs/identity-service.md`'yi yeni URL şeması / `PathBase` / callback yolu ile gözden geçir (kısmen güncellendi).
- [ ] README'ye **Mermaid** mimari diyagramı (edge → web-app/gateway/identity → servisler).
- [x] **ADR**'ler: (1) neden path tabanlı tek domain, (2) neden statik edge nginx (acme-companion yerine), (3) neden registry'siz yerel build + `docker save|ssh|load` (VPS'te build yok — kapasite).
- [ ] Runbook: sertifika yenileme, rollback, DB restore, incident müdahalesi.
- [ ] API dokümanı (`/openapi`) hazır olduğunda README'ye link.

---

## 4. Bilinen teknik borç ve riskler

| Konu | Etki | Öneri | Öncelik |
|---|---|---|---|
| Prod'da `ASPNETCORE_ENVIRONMENT=Development` | Ayrıntılı hata sayfası / log sızıntısı | `Production` + `appsettings.Production.json` | **P1** |
| Otomatik test yok (`tests/*` boş) | Regresyon riski, güvenli refactor imkânsız | dotnet test + Playwright smoke | **P1** |
| `main` dalı farklı bir hatta (kurs spec'leri) | Yanlış dal deploy riski, karışıklık | Branch stratejisini ADR ile yazılı hale getir | **P1** |
| GitHub secrets yok | Otomatik deploy çalışmıyor | Secrets ekle | **P1** |
| Yedekleme yok | Veri kaybı | `pg_dump` + offsite + restore provası | **P1** |
| Tek VPS / 3.5 GiB RAM | OOM, tek nokta arıza | Swap + kaynak limitleri + alarm | P2 |
| CSP yok | XSS yüzeyi | Report-Only ile başla | P2 |
| `deploy/.env` elle yönetiliyor | Secret rotasyonu yok | Rotasyon takvimi / sır yöneticisi | P2 |
| Eski `app./api./id.` sertifika lineage'i | Karışıklık, gereksiz yenileme | Temizle | P3 |
| `next/image` `remotePatterns` geniş | Optimizer üzerinden SSRF yüzeyi | Yalnız kullanılan host'lar | P3 |
| **Public repo + canlı demo hesapları** (`alice`/`bob`, parolalar README'de) | Herkes giriş yapıp ilan/teklif açabilir, kendi ilanını silebilir | Hesapları kilitle veya parolaları değiştir; README'den kaldır (bkz. §6) | **P1** |
| **Brute-force koruması yok** (login/token + SSH) | Parola denemesi, log/kaynak tüketimi (24s'de 74 başarısız SSH denemesi) | nginx `limit_req` + ASP.NET `RateLimiter` + fail2ban | **P1** |
| **Public repoda altyapı bilgisi** (VPS IP, `root`, port 22, WireGuard) | Hedef keşfi kolaylaşır | Dokümanı placeholder'a çevir / private doc'a taşı (bkz. §6) | P2 |
| Client secret repoda sabit (`NotASecret`) + ROPC açık | Kullanıcı adı bilinirse token endpoint'inden parola denemesi | Secret'ı env'e taşı, ROPC'yi prod'da kapat | P2 |
| Issuer değişikliği (02.11.2026) | Eski token/oturumlar geçersiz | Bilinçli kırıcı değişiklik — dokümante edildi | ✅ |

---

## 5. Önerilen sıra (ilk 2 hafta)

1. GitHub secrets → deploy job'ı yeşil, uçtan uca otomatik deploy provası. (P1 · S)
2. `ASPNETCORE_ENVIRONMENT=Production` + `UseDeveloperExceptionPage` kapatma. (P1 · S)
3. **Responsive tasarım** + **koyu/açık tema seçici** — kullanıcıya görünen en hızlı kazanım. (P1 · M)
4. Test iskeleti: `dotnet test` + Playwright login→bid smoke, CI'a bağla. (P1 · M)
5. Yedekleme + izleme/alarm. (P1 · M)
6. CSP (Report-Only) + güvenlik başlıkları, lint borcunun temizlenmesi. (P2 · M)

---

## 6. Public repo & GHCR — maruz kalma değerlendirmesi (02.11.2026)

Repo ve GHCR paketleri **public** yapıldı. Aşağıdaki değerlendirme 02.11.2026'da yapıldı;
maddeler **bilinçli olarak uygulanmadı**, karar bekliyor (bkz. 6.4).

### 6.1 Kontrol edildi — temiz

| Kontrol | Sonuç |
|---|---|
| Git geçmişinde sır | `.env.example` (yalnız placeholder) + geçmişte `frontend/web-app/.env.production` (içeriği yalnız `https://id.carsties.test`, `https://api.carsties.test`) → sır yok |
| Repoda anahtar/token deseni | `ghp_`, `github_pat_`, `AKIA…`, `BEGIN … PRIVATE KEY` → **0 eşleşme** |
| İmaj içeriğinde sır | `web-app` imaj ENV yalnız `NODE_ENV/PORT/HOSTNAME/NODE_VERSION`; `BETTER_AUTH_SECRET` build ARG'ı ENV'e yazılmıyor |
| GHCR public olması | Kaynak zaten public, imajda sır yok; **yazma yetkisi** yalnız CI'da (`GITHUB_TOKEN`) → `latest` dışarıdan ezilemez |
| `deploy/.env` | Hiç commit edilmemiş; VPS'te `chmod 600` |
| SSH sertleştirme | `passwordauthentication no`, `permitrootlogin without-password`, `pubkeyauthentication yes` |

### 6.2 Tespit edilen riskler (karar bekliyor)

| # | Risk | Kanıt | Ciddiyet |
|---|---|---|---|
| 1 | Canlı **demo hesapları** ve parolaları public | `identity` DB'de `alice`, `bob`; `README.md`'de “alice / Pass123$”; ROPC ile token alınabildiği doğrulandı | **Yüksek** |
| 2 | **Altyapı bilgisi ifşası** | `deploy/README-vps.md` + bu dosyada VPS public IP, WireGuard IP, `root`, port 22, sağlayıcı | Orta |
| 3 | **Brute-force / rate limit yok** | nginx'te `limit_req` yok, ASP.NET `RateLimiter` yok, fail2ban kurulu değil; son 24 saatte 74 başarısız SSH denemesi | Orta |
| 4 | **Client secret repoda sabit** + ROPC açık | `src/IdentityService/Config.cs`: `new Secret("NotASecret")`, `GrantTypes.ResourceOwnerPassword` (`bruno` client) | Orta |
| 5 | Prod'da `ASPNETCORE_ENVIRONMENT=Development` | Identity `UseDeveloperExceptionPage()` açık → stack trace sızabilir | Orta |
| 6 | Kaynak kodun görünürlüğü | Endpoint/framework sürümleri haritalanabilir | Düşük (kabul edilebilir) |

> Not: “public repo” tek başına açık değildir. Riski yaratan kombinasyon: **zayıf demo kimlik bilgileri + rate limit yokluğu + ayrıntılı hata sayfası**.

### 6.3 Riskleri kapatma adımları (uygulanmadı — onay bekliyor)

- [ ] **Demo hesapları devre dışı bırak** (canlı veri etkisi var!)
  ```sql
  -- alice/bob mevcut ilanların sahibiyse o ilanlar düzenlenemez hale gelir (admin yok)
  UPDATE "AspNetUsers" SET "LockoutEnd" = '9999-12-31 00:00:00+00', "LockoutEnabled" = true
  WHERE "UserName" IN ('alice','bob');
  ```
  Alternatif: parolaları güçlü rastgele değerlerle değiştir, hesapları silme (veri bütünlüğü).
- [ ] `README.md`'den “Test hesapları: alice / Pass123$” satırını kaldır (public repoda parola yayınlama).
- [ ] `deploy/README-vps.md` ve bu dosyadaki **public IP / WireGuard IP / kullanıcı adı / port** bilgilerini `<VPS_IP>` benzeri yer tutuculara çevir; gerçek değerleri repo dışında (private not) tut.
- [ ] **nginx rate limit** (`/carsties/id/connect/token`, `/carsties/id/Account/Login`):
  ```nginx
  # http seviyesinde
  limit_req_zone $binary_remote_addr zone=login:10m rate=10r/m;
  # ilgili location'larda
  limit_req zone=login burst=5 nodelay;
  ```
- [ ] **Identity tarafında** `AddRateLimiter` + login/token uçlarına politika; başarısız giriş sayacı ve kilitleme (`MaxFailedAccessAttempts`).
- [ ] **fail2ban** kur (`dnf install fail2ban`, `sshd` jail) — 22 portundaki bot denemelerini sınırla.
- [ ] Client secret'ı **env'den** oku (`config["NextAppClientSecret"]`), güçlü rastgele değer üret, `bruno`/ROPC client'ını yalnız `Development` ortamında kaydet.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` + `appsettings.Production.json` (bkz. §4).
- [ ] GitHub ayarları: *Settings → Actions → Fork pull request workflows: Require approval*, *Settings → Code security: Private vulnerability reporting* + **Secret scanning / Push protection** (public repoda ücretsiz), Dependabot alerts.

### 6.4 Karar durumu

| Tarih | Karar |
|---|---|
| 02.11.2026 | **Hiçbir değişiklik uygulanmadı.** Sahibi (Ümit Şengül) riskleri bilerek şimdilik kabul etti; 6.3'teki maddeler daha sonra tek tek ele alınacak. |

---

## 7. Hızlı doğrulama komutları

```bash
# Canlı uçlar
curl -sI https://umitsengul.online/ | head -2
curl -sI https://umitsengul.online/carsties | head -2
curl -s  https://umitsengul.online/carsties/id/.well-known/openid-configuration | head -c 200
curl -s -o /dev/null -w '%{http_code}\n' https://umitsengul.online/carsties/api/auctions

# Sertifika
echo | openssl s_client -connect umitsengul.online:443 -servername umitsengul.online 2>/dev/null \
  | openssl x509 -noout -subject -issuer -dates

# VPS
ssh vps 'cd /opt/carsties && docker compose --env-file deploy/.env \
  -f docker-compose.yml -f deploy/docker-compose.prod.yml ps'
ssh vps 'docker exec nginx-proxy nginx -t'
ssh vps 'git -C /opt/carsties log --oneline -3; systemctl list-timers "carsties-*" --no-pager'

# Yerel geliştirme / kalite kapıları
docker compose up -d --build
cd frontend/web-app && npm ci && npm run lint && npm run build
cd ../.. && dotnet build Carsties.sln && dotnet package list --vulnerable --include-transitive
```

---

## 8. Değişiklik günlüğü

| Tarih | Not |
|---|---|
| 02.11.2026 | Tek domain + path tabanlı yayına geçiş; statik edge nginx + landing sayfası; Identity `PathBase`; better-auth 1.7 migrasyonu; bağımlılık güvenlik yükseltmeleri (runtime 0 açık); GHCR + GitHub Actions CI/CD; Let's Encrypt sertifikası; yayında bulunan 7 hata düzeltildi. |
| 02.11.2026 | Repo + GHCR public yapıldı → **maruz kalma değerlendirmesi** eklendi (§6). Sır sızıntısı yok; 5 risk tespit edildi, kapatma adımları **onay bekliyor** (bilinçli olarak uygulanmadı). |
| 02.11.2026 | **Responsive düzeltmeleri** (12 dosya): kart ızgarası, görsel ölçekleme, kart rozetleri, detay sayfası kolonları, header/arama/filtreler, pagination, form ızgaraları, sign-in/empty/error kartları. `npm run lint` 0 error, `npm run build` başarılı. |
| 03.11.2026 | **GitHub Actions bağımlılığı kaldırıldı**: `deploy/release.sh` (yerel build → `docker save \| ssh \| docker load` → VPS deploy, registry/GHCR/PAT yok), `deploy/quality.sh` (kalite kapısı), `IMAGE_SOURCE=local` desteği + `docker-compose.local-images.yml`, workflow yalnız manuel kaliteye indirildi. Inter fontu repoya gömüldü (`next/font/local`) — build'de Google Fonts'a ağ bağımlılığı kalmadı. |



