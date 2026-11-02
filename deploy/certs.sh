#!/usr/bin/env bash
# ============================================================
# Carsties - Let's Encrypt sertifika yonetimi (VPS)
#
#   ./deploy/certs.sh issue   # ilk sertifika (app/api/id tek SAN sertifikasi)
#   ./deploy/certs.sh renew   # periyodik yenileme (carsties-certs.timer cagirir)
#   ./deploy/certs.sh dry-run # staging uzerinde uctan uca test (limit yakmaz)
#
# 80/HTTP notu: VPS'te 80 disariya kapalidir; issue/renew/dry-run sirasinda
# firewalld ile gecici olarak acilir ve is bitince kapatilir (bkz. fw_open_http).
#
# Neden acme-companion degil?
#   nginx-proxy 1.11 imajinda PID 1 "forego". acme-companion sertifika
#   kurulumundan sonra nginx-proxy'ye SIGHUP gonderiyor; forego bunu kapanis
#   sinyali sayip proxy'yi durduruyor (Exited (0)). Bu yuzden sertifikalar
#   certbot (HTTP-01 / webroot) ile alinip nginx-proxy'nin bekledigi yola
#   (<host>.crt / <host>.key) yazilir; nginx-proxy'nin icindeki docker-gen
#   bir ag olayi ile tetiklenip "nginx -s reload" yapar.
# ============================================================
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${DEPLOY_DIR}/.env"
if [[ ! -f "${ENV_FILE}" ]]; then
  echo "HATA: ${ENV_FILE} bulunamadi (deploy/.env.example'i kopyalayip doldur)" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
. "${ENV_FILE}"
set +a

: "${APP_HOST:?deploy/.env icinde APP_HOST gerekli}"
: "${API_HOST:?deploy/.env icinde API_HOST gerekli}"
: "${ID_HOST:?deploy/.env icinde ID_HOST gerekli}"
: "${ACME_EMAIL:?deploy/.env icinde ACME_EMAIL gerekli}"

PROJECT="${COMPOSE_PROJECT_NAME:-carsties}"
HTML_VOL="${PROJECT}_nginx-html"
CERT_VOL="${PROJECT}_nginx-certs"
LE_VOL="${PROJECT}_letsencrypt"
NETWORK="${PROJECT}_default"
CERT_NAME="carsties"
PROXY="nginx-proxy"
HOSTS=("${APP_HOST}" "${API_HOST}" "${ID_HOST}")

STAGING=()
if [[ "${LETSENCRYPT_TEST:-false}" == "true" ]]; then
  STAGING=(--staging)
fi

log() { printf '[%s] %s\n' "$(date '+%F %T')" "$*"; }

# --- 80/HTTP politikasi -------------------------------------------------
# VPS'te 80 disariya KAPALI tutulur (normalde yalniz 22 + 443 acik).
# HTTP-01 dogrulamasi icin 80'e Let's Encrypt sunuculari erismek zorunda oldugundan
# sertifika islemi suresince http servisi gecici olarak acilir, EXIT trap'i ile
# yeniden kapatilir. firewalld yoksa (ornegin laptop) bu adim sessizce atlanir.
FW_OPENED=false
fw_open_http() {
  command -v firewall-cmd >/dev/null 2>&1 || return 0
  firewall-cmd --state >/dev/null 2>&1 || return 0
  if firewall-cmd --query-service=http >/dev/null 2>&1; then
    log "firewalld: http zaten acik (kalici ayar degismedi)"
    return 0
  fi
  log "firewalld: HTTP-01 dogrulamasi icin http gecici aciliyor"
  firewall-cmd --add-service=http >/dev/null
  FW_OPENED=true
}

fw_close_http() {
  [[ "${FW_OPENED}" == "true" ]] || return 0
  firewall-cmd --remove-service=http >/dev/null 2>&1 || true
  FW_OPENED=false
  log "firewalld: http yeniden kapatildi"
}

trap fw_close_http EXIT

issue() {
  log "Sertifika isteniyor: ${HOSTS[*]}"
  local args=(certonly --webroot -w /var/www/html
              --cert-name "${CERT_NAME}"
              --email "${ACME_EMAIL}" --agree-tos --no-eff-email -n)
  local d
  for d in "${HOSTS[@]}"; do args+=(-d "${d}"); done
  docker run --rm \
    -v "${LE_VOL}:/etc/letsencrypt" \
    -v "${HTML_VOL}:/var/www/html" \
    certbot/certbot "${args[@]}" "${STAGING[@]}"
}

renew() {
  log "Sertifika yenileme kontrolu"
  docker run --rm \
    -v "${LE_VOL}:/etc/letsencrypt" \
    -v "${HTML_VOL}:/var/www/html" \
    certbot/certbot renew --webroot -w /var/www/html --quiet "${STAGING[@]}"
}

install_certs() {
  log "PEM'ler nginx-certs volume'una yaziliyor (<host>.crt / <host>.key)"
  docker run --rm \
    -v "${LE_VOL}:/etc/letsencrypt:ro" \
    -v "${CERT_VOL}:/certs" \
    -e "HOSTS=${HOSTS[*]}" \
    -e "CERT_NAME=${CERT_NAME}" \
    alpine:3 sh -c '
      set -e
      for h in $HOSTS; do
        cp "/etc/letsencrypt/live/${CERT_NAME}/fullchain.pem" "/certs/${h}.crt"
        cp "/etc/letsencrypt/live/${CERT_NAME}/privkey.pem"   "/certs/${h}.key"
        echo "  -> /certs/${h}.crt + /certs/${h}.key"
      done
      chmod 600 /certs/*.key'
}

reload_proxy() {
  log "docker-gen tetikleniyor (ag connect/disconnect olayi)"
  docker rm -f carsties-cert-trigger >/dev/null 2>&1 || true
  docker run -d --rm --name carsties-cert-trigger --network "${NETWORK}" alpine:3 sleep 8 >/dev/null
  sleep 4
  docker rm -f carsties-cert-trigger >/dev/null 2>&1 || true
  sleep 3

  if ! docker exec "${PROXY}" sh -c 'grep -q ssl_certificate /etc/nginx/conf.d/default.conf'; then
    log "UYARI: docker-gen config'i guncellemedi; ${PROXY} yeniden baslatiliyor"
    docker restart "${PROXY}" >/dev/null
    sleep 4
  fi
  docker exec "${PROXY}" nginx -t
}

case "${1:-}" in
  issue)
    fw_open_http
    issue
    install_certs
    reload_proxy
    log "Bitti. Kontrol: https://${APP_HOST} / https://${API_HOST}/auctions / https://${ID_HOST}"
    ;;
  renew)
    fw_open_http
    renew
    install_certs
    reload_proxy
    log "Bitti."
    ;;
  dry-run)
    # Staging uzerinde uctan uca dogrulama: Let's Encrypt limitlerini yakmaz,
    # sertifikayi degistirmez. 80'in gercekten acilip kapandigini test etmek icin.
    fw_open_http
    docker run --rm \
      -v "${LE_VOL}:/etc/letsencrypt" \
      -v "${HTML_VOL}:/var/www/html" \
      certbot/certbot renew --webroot -w /var/www/html --dry-run \
        --no-random-sleep-on-renew "${STAGING[@]}"
    log "Dry-run basarili (sertifika degismedi)."
    ;;
  *)
    echo "Kullanim: $0 {issue|renew|dry-run}" >&2
    exit 2
    ;;
esac
