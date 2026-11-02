# Carsties — VPS Operasyon Notları

Sunucu: `VPS-ALMA` · AlmaLinux 9.8 · 2 vCPU / 3.5 GiB RAM + 4 GiB swap · 40 GB disk
IP: `185.93.70.199` · WireGuard: `10.200.0.1` · Repo dizini: `/opt/carsties`

> ⚠️ Kampüs ağından bu IP'ye **TCP 22 ara ara sessizce düşüyor**. SSH için WireGuard yolunu kullan:
> `ssh vps` / `ssh vps-wg` (10.200.0.1). GitHub Actions runner'ları buluttan bağlandığı için
> public yol (`185.93.70.199:22`) kullanılır.

## 1) URL şeması (tek alan adı, path bazlı)

| Dış URL | Arkadaki servis | nginx davranışı |
|---|---|---|
| `https://umitsengul.online/` | statik landing (`deploy/landing`) | dosya |
| `https://umitsengul.online/carsties/…` | `web-app:3000` | önek korunur (`basePath=/carsties`) |
| `https://umitsengul.online/carsties/api/auth/…` | `web-app:3000` | better-auth callback |
| `https://umitsengul.online/carsties/api/…` | `gateway-svc:80` | önek **silinir** (`/auctions`, `/notifications`) |
| `https://umitsengul.online/carsties/id/…` | `identity-svc:80` | önek korunur (`PathBase=/carsties/id`) |

Issuer: `https://umitsengul.online/carsties/id` · Client redirect URI:
`https://umitsengul.online/carsties/api/auth/callback/duende` (better-auth 1.7 callback yolu).

## 2) Konteynerler

| servis | imaj | dış port |
|---|---|---|
| `nginx-proxy` (edge, `container_name` sabit) | `nginx:alpine` + `deploy/nginx/edge.conf` | **80, 443** |
| `carsties-web-app-1` | `ghcr.io/umitsengul/carstieslive-web-app` | `127.0.0.1:3000` |
| `carsties-gateway-svc-1` | `ghcr.io/umitsengul/carstieslive-gateway-svc` | - |
| `carsties-identity-svc-1` | `ghcr.io/umitsengul/carstieslive-identity-svc` | - |
| `carsties-auction-svc-1` / `bid` / `search` / `notify` | `ghcr.io/umitsengul/carstieslive-*` | - |
| `carsties-postgres-1`, `rabbitmq`, `meilisearch` | upstream | - |

`nginx-proxy` artık docker-gen kullanmıyor: tek alan adı için **statik** yapılandırma
(`deploy/nginx/edge.conf`) mount edilir. `VIRTUAL_HOST` / `LETSENCRYPT_HOST` değişkenleri prod'da kullanılmaz.

## 3) Deploy (GitHub Actions)

Akış: `path-migration` (ve `main`) dalına push → **quality** (build + NuGet/npm audit + lint +
next build + basePath doğrulaması) → **build-and-push** (7 imaj, GHCR) → **deploy**
(SSH → `deploy/deploy.sh`, `DEPLOY_BRANCH=<push edilen dal>`).

> Not: Bu repoda `main` dalı **başka bir hatta** (kurs spec'leri / CourseAssets); Carsties
> kodu `path-migration` dalında yaşıyor. Bu yüzden workflow iki dalı da tetikler ve VPS'teki
> klonu push edilen dala eşitler.

`deploy/deploy.sh` ne yapar:
1. `git fetch` + `git reset --hard origin/<DEPLOY_BRANCH>` (`deploy/.env` izlenmediği için korunur)
2. `IMAGE_TAG=sha-<kısa commit>` (imaj etiketi)
3. `docker compose pull` + `up -d --remove-orphans` (build yok)
4. `docker exec nginx-proxy nginx -t && nginx -s reload`
5. smoke test: `https://<DOMAIN>/` ve `https://<DOMAIN>/carsties/`

Gerekli GitHub **secrets**: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `GHCR_USER`, `GHCR_PAT`
(`GHCR_PAT`: `read:packages` yetkili klasik PAT).

Elle deploy:
```bash
ssh vps 'cd /opt/carsties && DEPLOY_BRANCH=path-migration ./deploy/deploy.sh'
# belirli sürüme sabitlemek / geri almak icin:
ssh vps 'cd /opt/carsties && IMAGE_TAG=sha-<eski-sha> ./deploy/deploy.sh'
```

## 4) TLS / sertifikalar

`acme-companion` kullanılmıyor; sertifikalar `deploy/certs.sh` (certbot HTTP-01 / webroot) ile alınır,
`carsties_nginx-certs` volume'una `<host>.crt/.key` olarak yazılır ve edge nginx reload edilir.
Günlük yenileme: `carsties-certs.timer`.

```bash
./deploy/certs.sh bootstrap # ilk kurulum: edge acilabilsin diye gecici self-signed sertifika
./deploy/certs.sh issue     # gercek sertifika (yalniz DOMAIN)
./deploy/certs.sh renew     # yenileme
./deploy/certs.sh dry-run   # staging testi (limit yakmaz)
```

Sertifika hattı (lineage) adı `carsties-prod` (eski `carsties` sertifikası `app/api/id` SAN'larını
taşıdığı için yenisi ayrı isimle alınır; dosyalar yine `<DOMAIN>.crt/.key` olarak yazılır).

Not: HTTP-01 için 80 geçici olarak firewalld'de açılır, iş bitince kapatılır.

## 5) İlk kurulum / geçiş kontrol listesi (VPS)

```bash
# 0) Yedek
cp /opt/carsties/deploy/.env /root/carsties.env.bak

# 1) deploy/.env'i tek domain semasina gore guncelle (sirlar ayni kalir)
#    DOMAIN=umitsengul.online  +  IMAGE_TAG=latest
#    (APP_HOST/API_HOST/ID_HOST satirlarini kaldir)

# 2) Depoyu git klonuna cevir (CI bundan sonra git ile gunceller)
mv /opt/carsties /opt/carsties.rsync-bak
# read-only deploy key (GitHub > Settings > Deploy keys) ile:
git clone git@github.com:UmitSengul/CarstiesLive.git /opt/carsties
cp /root/carsties.env.bak /opt/carsties/deploy/.env && chmod 600 /opt/carsties/deploy/.env

# 3) GHCR login (bir kez)
echo "<PAT>" | docker login ghcr.io -u UmitSengul --password-stdin

# 4) Sertifika + deploy sirasi (edge sertifikasiz acilamaz, certbot da edge'e ihtiyac duyar)
cd /opt/carsties
./deploy/certs.sh bootstrap   # gecici self-signed sertifika (edge acilabilsin)
./deploy/deploy.sh            # stack + edge ayaga kalkar (80/443)
./deploy/certs.sh issue       # gercek Let's Encrypt sertifikasi + nginx reload
./deploy/deploy.sh            # smoke test dogrulamasi icin tekrar calistirilabilir
```

> Eski `app./api./id.umitsengul.online` vhost'ları kaldırılır; DNS kayıtları silinmese de edge
> yalnız `umitsengul.online` için `server_name` tanımladığı için 404/503 döner.

## 6) Doğrulama

```bash
curl -sI https://umitsengul.online/ | head -3                                  # 200 (landing)
curl -sI https://umitsengul.online/carsties/ | head -3                        # 200
curl -sI https://umitsengul.online/carsties/api/auctions | head -3            # 405/200
curl -s  https://umitsengul.online/carsties/id/.well-known/openid-configuration \
  | grep -o 'https://umitsengul.online/carsties/id[^"]*' | head               # issuer + endpoint'ler
ssh vps 'cd /opt/carsties && docker compose --env-file deploy/.env -f docker-compose.yml -f deploy/docker-compose.prod.yml ps'
ssh vps 'docker exec nginx-proxy nginx -T | grep -c ssl_certificate'
```

Tarayıcı akışı: `/carsties/` → `Login` → `https://umitsengul.online/carsties/id/Account/Login` →
`/carsties/api/auth/callback/duende` → `/carsties/session` → SignalR `wss://umitsengul.online/carsties/api/notifications`.

## 7) Bu kurulumda değişen dosyalar

| yol | ne |
|---|---|
| `docker-compose.yml` | imajlar `ghcr.io/umitsengul/carstieslive-*:${IMAGE_TAG:-latest}` + `build:` (lokal) |
| `deploy/docker-compose.prod.yml` | statik edge nginx, `pull_policy: always`, tek domain env'leri, `PathBase` |
| `deploy/nginx/edge.conf` | path bazlı yönlendirme + TLS + WebSocket + HSTS |
| `deploy/landing/index.html` | kök alan adı karşılama sayfası (Carsties linki) |
| `deploy/certs.sh` | tek domain (`DOMAIN`) + `nginx -s reload` |
| `deploy/deploy.sh` | VPS deploy betiği (git reset → pull → up → reload → smoke) |
| `deploy/.env.example` | `DOMAIN`, `IMAGE_TAG`, sırlar |
| `.github/workflows/pipeline.yml` | CI/CD (quality → GHCR push → SSH deploy) |
| `.github/dependabot.yml` | npm / nuget / docker / actions güncellemeleri |
| `frontend/web-app/next.config.ts` | `basePath` (`NEXT_PUBLIC_BASE_PATH`) |
| `frontend/web-app/src/lib/auth*.ts`, `proxy.ts`, `fetch-wrapper.ts` | better-auth 1.7 + basePath uyumu |
| `frontend/web-app/src/app/api/auth/[...all]/route.ts` | Next'in sildiği `basePath` önekini isteğe geri ekler |
| `frontend/web-app/Dockerfile` | yeni build arg'lar (`NEXT_PUBLIC_BASE_PATH` dahil) |
| `frontend/web-app/.dockerignore` | build context küçültme |
| `src/IdentityService/HostingExtensions.cs` | `UsePathBase(PathBase)` |
| `src/IdentityService/Config.cs` | redirect URI → `/api/auth/callback/duende` |
| `src/IdentityService/IdentityService.csproj` | `Microsoft.EntityFrameworkCore.Tools` 10.0.14 (Pkcs açığı) |

## 8) Sırlar / notlar

- `deploy/.env` git'te **yok** (`.gitignore`), chmod 600, yalnız VPS'te tutulur.
- `NEXT_PUBLIC_*` değerleri **build zamanında** imaja gömülür → CI'da doğru URL'lerle build edilir.
- Yerel dosya `frontend/web-app/.env.local` (gitignore) lokal geliştirme içindir.
- Eski sürüme dönüş: `IMAGE_TAG=sha-<eski>` ile `deploy.sh` ya da `git reset --hard <eski-commit>`.
- `carsties-heartbeat.timer` ve `carsties-certs.timer` systemd birimleri değişmedi.
