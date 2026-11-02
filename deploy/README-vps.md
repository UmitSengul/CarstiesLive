# Carsties — VPS (Rabisu / VPS-ALMA) Operasyon Notları

Sunucu: `VPS-ALMA` · AlmaLinux 9.8 · 2 vCPU / 3.5 GiB RAM + 4 GiB swap · 40 GB disk
IP: `185.93.70.199` · WireGuard: `10.200.0.1` · Repo dizini: `/opt/carsties`
Domainler: `app.` (Next.js), `api.` (gateway), `id.` (Duende IdentityServer) → `umitsengul.online`

> ⚠️ Kampüs ağından bu IP'ye **TCP 22 ara ara sessizce düşüyor** (ICMP de dalgalı).
> Aynı anda sunucu ayakta ve 80/443 çalışıyor → **SSH için WireGuard yolunu kullan**:
> `ssh vps` / `ssh vps-wg` (10.200.0.1). Public yol yedek: `ssh vps-public`.

## Özet — bu kurulumda ne yapıldı

- `app.` / `api.` / `id.umitsengul.online` üzerinden **HTTPS ile çalışan tam yığın**;
  11 konteyner `restart: unless-stopped`, dışarıya yalnız **22/80/443** açık
  (web-app yalnız `127.0.0.1:3000`).
- Repo'da **4 dosya değişti** → Identity + Gateway'e `ForwardedHeaders`, web-app
  Dockerfile'ına build-time `NEXT_PUBLIC_*` arg'ları, Register sayfasında hata raporlaması.
  Ayrıca **`deploy/` klasörü eklendi** (prod override, `certs.sh`, `.env.example`, bu dosya).
- TLS: **acme-companion kullanılmıyor** (nginx-proxy 1.11 + `forego` SIGHUP sorunu) →
  `deploy/certs.sh` (certbot webroot) + günlük `carsties-certs.timer`.
- VPS'te ek olarak: nabız (`carsties-heartbeat`) timer'ı, kalıcı journal,
  sertifika yenileme timer'ı, sırların tutulduğu `/opt/carsties/deploy/.env` (chmod 600).
- Düzeltilen hata: Register başarısız olsa da "Successfully registered!" gösteriyordu
  (parola politikası hataları yutuluyordu) → artık hata mesajları ekranda.
- Dosya dosya tüm döküm: **§7**.

## 1) Deploy / güncelleme
```bash
# laptop -> VPS  (node_modules/.next/.git hariç ~10 MB)
rsync -az --delete \
  --exclude node_modules --exclude .next --exclude .git \
  --exclude 'src/*/bin' --exclude 'src/*/obj' --exclude .env.local \
  -e "ssh -o ConnectTimeout=10" /Yazilim/Carsties/ vps:/opt/carsties/

# VPS'te
cd /opt/carsties
COMPOSE_PARALLEL_LIMIT=2 docker compose --env-file deploy/.env \
  -f docker-compose.yml -f deploy/docker-compose.prod.yml up -d --build
```
`COMPOSE_PARALLEL_LIMIT=2` → 3.5 GiB RAM'de build sırasında OOM olmaması için.
İlk tam build ~25 dk (imaj indirme + 7 .NET restore/build + Next.js build).

> Arka planda başlatacaksan **tek** komut çalıştır (çift `compose up` aynı isimde
> konteyner çakışması verir):
> `setsid nohup docker compose ... > /tmp/carsties-up.log 2>&1 < /dev/null &`

## 2) Konteynerler
| servis | imaj | dış port |
|---|---|---|
| `nginx-proxy` (container_name sabit) | `nginxproxy/nginx-proxy` | **80, 443** |
| `carsties-web-app-1` | `trycatchlearn/web-app` | `127.0.0.1:3000` |
| `carsties-gateway-svc-1` | `trycatchlearn/gateway-svc` | - |
| `carsties-identity-svc-1` | `trycatchlearn/identity-svc` | - |
| `carsties-auction-svc-1` / `bid` / `search` / `notify` | `trycatchlearn/*` | - |
| `carsties-postgres-1`, `rabbitmq`, `meilisearch` | upstream | - |

Postgres/RabbitMQ/Meilisearch şifreleri `deploy/.env`'den gelir (connection string'ler
prod override'ta buna göre yeniden yazılır). Meili `MEILI_ENV=production` + 48 hex key.

## 3) TLS / sertifikalar (acme-companion YOK)
`nginx-proxy` 1.11 imajında PID 1 `forego`; **SIGHUP'ı kapanış sinyali sayıyor** ve
konteyner `Exited (0)` oluyor. acme-companion sertifika kurulumundan sonra proxy'ye
SIGHUP gönderdiği için bu ikili burada çalışmıyor. Onun yerine:

```bash
./deploy/certs.sh issue    # ilk sertifika: app+api+id (tek SAN sertifikasi, certbot webroot)
./deploy/certs.sh renew    # yenileme (gunde 1 kez systemd timer)
```
Akış: `certbot/certbot` → `carsties_letsencrypt` volume → PEM'ler
`carsties_nginx-certs` volume'una `<host>.crt` / `<host>.key` olarak yazılır →
geçici bir konteyner ağa bağlanıp ayrılır (connect/disconnect olayı) → nginx-proxy
içindeki docker-gen config'i yeniden üretir ve `nginx -s reload` yapar.
Gerekirse script `nginx-proxy`'yi yeniden başlatır (fallback).

Yenileme timer'ı: `systemctl list-timers carsties-certs.timer`
(dosyalar: `/etc/systemd/system/carsties-certs.{service,timer}`).
Test modu: `deploy/.env` içinde `LETSENCRYPT_TEST=true` (staging CA).

## 4) Doğrulama
```bash
curl -sI https://api.umitsengul.online/auctions
curl -s  https://id.umitsengul.online/.well-known/openid-configuration | python3 -m json.tool | head -5
curl -s  https://id.umitsengul.online/.well-known/openid-configuration | grep -o 'https://id[^"]*'
ssh vps 'cd /opt/carsties && docker compose --env-file deploy/.env -f docker-compose.yml -f deploy/docker-compose.prod.yml ps'
ssh vps 'docker exec nginx-proxy nginx -T | grep -c ssl_certificate'
```
Login sayfası: `https://app.umitsengul.online/auth/sign-in` · seed kullanıcılar:
`alice` / `Pass123$`, `bob` / `Pass123$`. (ClientAppUrl = `https://app.umitsengul.online`,
redirect = `/api/auth/oauth2/callback/duende`.)

`X-Forwarded-Proto/For` → `UseForwardedHeaders()` (Identity + Gateway) sayesinde
discovery endpoint'leri `https://` üretir; aksi halde login POST'ları 301 ile GET'e düşer.

## 5) Nabız / "VPS askıya alınıyor mu?" takibi
```bash
ssh vps 'tail -5 /var/log/carsties-heartbeat.log'
ssh vps '/usr/local/bin/carsties-heartbeat-gaps.sh'
ssh vps 'journalctl --list-boots | tail -5; last -x shutdown | head -5'
```
Yorumlama: boşluk + **aynı boot_id** → hypervisor dondurmuş (suspend);
boşluk + **yeni boot_id** + `shutdown` kaydı → düzgün reboot; kayıt yok → sert power-off.
Nabız: `carsties-heartbeat.timer`, 30 sn'de bir `https://1.1.1.1/cdn-cgi/trace` çağırır.

## 6) Bilinen durumlar / notlar
- firewalld `public` zone: http/https/ssh + 51820/udp açık. Dışarıya yalnız 22/80/443.
- SELinux disabled; Docker 29.9.2 + Compose 5.6.1 (`!reset` / `!override` destekli).
- `docker compose config` ile birleşik yapı doğrulanabilir:
  `docker compose --env-file deploy/.env -f docker-compose.yml -f deploy/docker-compose.prod.yml config`.
- Duende IdentityServer 8 lisans uyarısı normaldir (ticari kullanımda lisans gerekir).
- Build sırasında `BETTER_AUTH_SECRET` uyarısı çıkar (better-auth build-time kontrolü);
  runtime'da değer `deploy/.env`'den verilir, işlevsel etkisi yoktur.
- `NU1903` (System.Security.Cryptography.Pkcs 9.0.0) transitive paket uyarısı → upstream.

## 7) Değişiklik dökümü (detay)

Bu kurulum için yapılan tüm değişiklikler. Üç kırılım var: **repo**, **VPS**, **laptop/host**.
Hiçbiri henüz commit edilmedi (`git status` → 4 modified + `deploy/` untracked).

### 7.1 Repo'da değişen dosyalar

| dosya | fark | ne / neden |
|---|---|---|
| `src/IdentityService/HostingExtensions.cs` | +15 | `ForwardedHeaders` kaydı + `app.UseForwardedHeaders()` |
| `src/GatewayService/Program.cs` | +11 | aynı yapılandırma (CORS ve proxy'den önce) |
| `frontend/web-app/Dockerfile` | +10 | `NEXT_PUBLIC_*` / `BETTER_AUTH_URL` build arg'ları |
| `src/IdentityService/Pages/Account/Register/Index.cshtml.cs` | +18 −3 | Identity hatalarını ekrana bas (bug fix) |

#### Identity + Gateway — `ForwardedHeaders`
```csharp
_ = builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();   // trafik yalnız iç docker ağından geliyor
    options.KnownProxies.Clear();
});
// ...
_ = app.UseForwardedHeaders();       // routing / auth'tan ÖNCE
```
**Neden:** TLS nginx-proxy'de sonlandığı için, bu başlıklar olmadan discovery dokümanı
`http://id.umitsengul.online/connect/token` üretiyordu → login POST'u 301 ile GET'e düşüyordu.

#### web-app `Dockerfile`
```dockerfile
ARG NEXT_PUBLIC_BASE_API=https://api.umitsengul.online
ARG NEXT_PUBLIC_ID_URL=https://id.umitsengul.online
ARG BETTER_AUTH_URL=https://app.umitsengul.online

ENV NEXT_PUBLIC_BASE_API=$NEXT_PUBLIC_BASE_API \
    NEXT_PUBLIC_ID_URL=$NEXT_PUBLIC_ID_URL \
    BETTER_AUTH_URL=$BETTER_AUTH_URL
```
`NEXT_PUBLIC_*` değerleri **build zamanında** bundle'a gömülür; değerler prod override'taki
`build.args` ile verilir. (Değişmezse tarayıcı `localhost:5001`'e gitmeye çalışır.)

#### Register sayfası (bug fix)
```csharp
// ÖNCE: hatalar yutuluyor, başarısız kayıtta bile sayfa "Successfully registered!" diyordu
if (result.Succeeded) { await userManager.AddClaimsAsync(...); }
RegisterSuccess = true;

// SONRA
if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        ModelState.AddModelError(string.Empty, error.Description);
    }

    RegisterSuccess = false;

    return Page();
}

await userManager.AddClaimsAsync(user, [new Claim(JwtClaimTypes.Name, Input.FullName!)]);
RegisterSuccess = true;
```
**Belirti:** kayıt "başarılı" görünüyor, DB'de kullanıcı yok → login'de
`User Login Failure / "invalid credentials"`.
**Kök neden:** ASP.NET Identity varsayılan parola politikası (≥6 karakter, büyük + küçük harf,
rakam, alfanümerik olmayan karakter) ve `result.Errors`'ın `ModelState`'e eklenmemesi.

### 7.2 Yeni `deploy/` dosyaları

- **`docker-compose.prod.yml`** (162 satır) — base `docker-compose.yml`'e dokunulmadı, tüm prod
  farkları bu override'da: `ports: !reset []` (iç servisler dışa kapalı; sadece `nginx-proxy`
  80/443 + web-app `127.0.0.1:3000`), `VIRTUAL_HOST`/`VIRTUAL_PORT`, `restart: unless-stopped`,
  `json-file` log rotasyonu (10m × 3), sırların `${...}` ile `deploy/.env`'den verilmesi,
  connection string'lerin buna göre yeniden yazılması, `MEILI_ENV=production`,
  `search-svc`'nin `auction-svc`'ye bağımlılığı. `acme-companion` bilinçli olarak yok (§3).
- **`certs.sh`** — certbot (webroot) → `carsties_letsencrypt` volume → PEM'ler
  `carsties_nginx-certs` volume'una `<host>.crt/.key` → docker-gen `nginx -s reload`.
  Komutlar: `issue` / `renew`; `nginx-proxy` restart fallback'i içerir.
- **`.env.example`** — 10 anahtar: `APP_HOST`, `API_HOST`, `ID_HOST`, `ACME_EMAIL`,
  `LETSENCRYPT_TEST`, `BETTER_AUTH_SECRET`, `POSTGRES_PASSWORD`, `RABBITMQ_USER`,
  `RABBITMQ_PASS`, `MEILI_MASTER_KEY`.
- **`README-vps.md`** — bu dosya.

Git'e girmeyen yerel dosya: `frontend/web-app/.env.local` (`.gitignore` içindeki `.env*` kuralı).

### 7.3 VPS'te oluşturulan / değişen dosyalar

| yol | ne |
|---|---|
| `/opt/carsties/` | repo senkronu (`git`, `rsync` paketleri kuruldu) |
| `/opt/carsties/deploy/.env` | 10 sır, chmod 600 — **git'te yok** |
| `/usr/local/bin/carsties-heartbeat.sh` | 30 sn'de bir `https://1.1.1.1/cdn-cgi/trace` → `/var/log/carsties-heartbeat.log` |
| `/usr/local/bin/carsties-heartbeat-gaps.sh` | log'daki boşlukları raporlar (suspend analizi) |
| `/etc/systemd/system/carsties-heartbeat.{service,timer}` | `enabled` |
| `/etc/systemd/system/carsties-certs.{service,timer}` | `enabled`, günlük sertifika yenileme |
| `/etc/logrotate.d/carsties-heartbeat` | log rotasyonu |
| `/var/log/journal` | kalıcı journal açıldı |
| `/etc/letsencrypt` + `carsties_nginx-certs` volume | `app`/`api`/`id` SAN sertifikası |
| `identity` DB | test kullanıcısı **`umit` / `Test1234!`** |

### 7.4 Laptop / host tarafı

| yol | değişiklik |
|---|---|
| `~/.docker/desktop/settings-store.json` | `FilesharingDirectories: ["/home", "/Yazilim"]` → `docker build` NU1301 hatası çözüldü |
| `~/.ssh/config` | `laptop`, `vps` (10.200.0.1), `vps-wg`, `vps-public`, `laptop-vpn`, `github.com` alias'ları |
| `/etc/nftables.conf` | kalıcı değişiklik yok (denenip geri alındı) |
| `/etc/hosts` | `127.0.0.1 app/api/id.umitsengul.online` geçici eklendi, sonra kaldırıldı |

### 7.5 Değişmeyenler

- Base `docker-compose.yml` hiç değişmedi — tüm prod farkları `deploy/` override'ında.
- `.NET` iş mantığı / endpoint'ler değişmedi (yalnız proxy header'ları + register hata raporlaması).
- Frontend iş mantığı değişmedi (yalnız Dockerfile build arg'ları).

### 7.6 Commit'e hazırlık

```bash
git add deploy src/GatewayService/Program.cs src/IdentityService/HostingExtensions.cs \
        src/IdentityService/Pages/Account/Register/Index.cshtml.cs frontend/web-app/Dockerfile
# .env dosyaları gitignore'da; commit'e girmez
```

