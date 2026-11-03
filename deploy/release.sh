#!/usr/bin/env bash
# ============================================================
# Carsties - Release (GitHub Actions YOK)
#
# Nerede calisir: gelistirici makinesi (repo kokunde). VPS'te build YAPILMAZ:
# VPS 2 vCPU / 2 GB RAM ile canli yigini barindirdigi icin build orada riskli.
#
# Akis:
#   1) on kontroller (docker, ssh, git durumu; commit edilmemis degisiklik varsa durur)
#   2) yerelde imajlari build eder (onbellek sayesinde tekrar build'ler hizli)
#   3) imajlari SSH uzerinden VPS'e tasir (docker save | zstd/gzip | ssh | docker load)
#   4) VPS'te deploy/deploy.sh (IMAGE_SOURCE=local): git reset -> up -d -> nginx reload -> smoke
#
# Kullanim:
#   ./deploy/release.sh                        # tum servisler, tag = sha-<kisa HEAD>
#   ./deploy/release.sh --only web-app         # tek servis (en hizli yol)
#   ./deploy/release.sh --only web-app,identity-svc
#   ./deploy/release.sh --tag sha-abc1234 --no-build   # build etme, mevcut imaji tasi
#   ./deploy/release.sh --host vps-wg --branch path-migration
#   ./deploy/release.sh -y                     # onay sormadan
#
# Geri alma (VPS'te eski imaj duruyorsa):
#   ssh vps 'cd /opt/carsties && IMAGE_SOURCE=local IMAGE_TAG=sha-<eski> SKIP_GIT=1 ./deploy/deploy.sh'
# ============================================================
set -euo pipefail

HOST="${DEPLOY_HOST:-vps}"
REMOTE_DIR="${REMOTE_DIR:-/opt/carsties}"
BRANCH="${DEPLOY_BRANCH:-path-migration}"
DOMAIN="${DOMAIN:-umitsengul.online}"
TAG=""
SERVICES=()
NO_BUILD=0
NO_PUSH=0
ASSUME_YES=0

ALL_SERVICES=(auction-svc bid-svc search-svc notify-svc identity-svc gateway-svc web-app)

usage() { sed -n '2,24p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --host) HOST="$2"; shift 2 ;;
    --remote-dir) REMOTE_DIR="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    --domain) DOMAIN="$2"; shift 2 ;;
    --tag) TAG="$2"; shift 2 ;;
    --only) IFS=',' read -r -a SERVICES <<< "$2"; shift 2 ;;
    --no-build) NO_BUILD=1; shift ;;
    --no-push) NO_PUSH=1; shift ;;
    -y|--yes) ASSUME_YES=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Bilinmeyen arguman: $1" >&2; usage >&2; exit 2 ;;
  esac
done

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${REPO_DIR}"

log()  { printf '\n[%s] %s\n' "$(date '+%F %T')" "$*"; }
fail() { printf '\nHATA: %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- arguman dogrulama
if [[ ${#SERVICES[@]} -eq 0 ]]; then
  SERVICES=("${ALL_SERVICES[@]}")
else
  for s in "${SERVICES[@]}"; do
    [[ " ${ALL_SERVICES[*]} " == *" ${s} "* ]] || fail "Bilinmeyen servis: ${s} (gecerli: ${ALL_SERVICES[*]})"
  done
fi

# ---------------------------------------------------------------- 1) on kontroller
log "On kontroller"
docker info >/dev/null 2>&1 || fail "Docker calismiyor (yerel)."
ssh -o BatchMode=yes -o ConnectTimeout=10 "${HOST}" true 2>/dev/null \
  || fail "SSH baglantisi kurulamadi: ${HOST} (ssh config alias'i ya da --host ver)."
ssh -o BatchMode=yes "${HOST}" "test -d '${REMOTE_DIR}/.git'" \
  || fail "${HOST}:${REMOTE_DIR} bir git klonu degil (deploy/deploy.sh orayi bekliyor)."

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ "${CURRENT_BRANCH}" != "${BRANCH}" ]]; then
  fail "Aktif dal '${CURRENT_BRANCH}', deploy dali '${BRANCH}'. Gec: git switch ${BRANCH} (ya da --branch ver)."
fi

if [[ -n "$(git status --porcelain)" ]]; then
  fail "Commit edilmemis degisiklik var; VPS yalnizca origin/${BRANCH} icerigini alir. Once: git add -A && git commit -m '...'"
fi

AHEAD="$(git rev-list --count "origin/${BRANCH}..HEAD" 2>/dev/null || echo 0)"
if [[ "${AHEAD}" != "0" ]]; then
  if [[ "${NO_PUSH}" == "1" ]]; then
    fail "${AHEAD} commit push edilmemis (--no-push verildi). VPS bu commit'leri goremez."
  fi
  log "Push edilmemis ${AHEAD} commit var -> git push origin ${BRANCH}"
  git push origin "${BRANCH}"
fi

SHA="$(git rev-parse --short=7 HEAD)"
[[ -n "${TAG}" ]] || TAG="sha-${SHA}"

# Build sirasinda compose'un istedigi degiskenler: yalnizca build arg'lari gercek
# deger tasir (NEXT_PUBLIC_*, BETTER_AUTH_URL); runtime sirlari placeholder'dir.
BUILD_ENV="deploy/.env.build"
cat > "${BUILD_ENV}" <<EOF
# Uretildi: deploy/release.sh (git'e girmez; yalnizca build icin)
DOMAIN=${DOMAIN}
IMAGE_TAG=${TAG}
BETTER_AUTH_SECRET=build-time-placeholder-not-used-at-runtime
POSTGRES_PASSWORD=build-placeholder
RABBITMQ_PASS=build-placeholder
MEILI_MASTER_KEY=build-placeholder-key-16b
EOF
trap 'rm -f "${BUILD_ENV}"' EXIT

export IMAGE_TAG="${TAG}"
COMPOSE=(docker compose --env-file "${BUILD_ENV}" -f docker-compose.yml -f deploy/docker-compose.prod.yml)

log "Ozet: ${#SERVICES[@]} servis | ${BRANCH} @ ${SHA} | tag ${TAG} | hedef ${HOST}:${REMOTE_DIR}"
if [[ "${ASSUME_YES}" != "1" ]]; then
  read -r -p "Devam edilsin mi? [e/H] " answer
  [[ "${answer}" =~ ^[eEyY]$ ]] || fail "Iptal edildi."
fi

# ---------------------------------------------------------------- 2) yerel build
IMAGES=()
for s in "${SERVICES[@]}"; do
  IMAGES+=("ghcr.io/umitsengul/carstieslive-${s}:${TAG}")
done

if [[ "${NO_BUILD}" == "1" ]]; then
  log "Build atlandi (--no-build)"
  for img in "${IMAGES[@]}"; do
    docker image inspect "${img}" >/dev/null 2>&1 || fail "Imaj yok: ${img}"
  done
else
  log "Yerel build (${#SERVICES[@]} servis): ${SERVICES[*]}"
  "${COMPOSE[@]}" build "${SERVICES[@]}"
fi

log "Imaj boyutlari"
for img in "${IMAGES[@]}"; do
  size="$(docker image inspect -f '{{.Size}}' "${img}" 2>/dev/null || echo 0)"
  printf '  %-58s %s MB\n' "${img}" "$((size / 1024 / 1024))"
done

# ---------------------------------------------------------------- 3) VPS'e tasi
if ssh -o BatchMode=yes "${HOST}" 'command -v zstd >/dev/null 2>&1' && command -v zstd >/dev/null 2>&1; then
  COMPRESS=(zstd -3 -T0 -c); DECOMPRESS=zstd
  log "Transfer: zstd"
else
  COMPRESS=(gzip -1 -c); DECOMPRESS=gzip
  log "Transfer: gzip (VPS'te zstd yok; hizlandirmak icin: ssh ${HOST} 'sudo dnf install -y zstd')"
fi

log "Imajlar ${HOST} uzerine aktariliyor (docker save | ${COMPRESS[0]} | ssh | docker load)"
docker save "${IMAGES[@]}" | "${COMPRESS[@]}" \
  | ssh -o BatchMode=yes "${HOST}" "${DECOMPRESS} -d | docker load"

# ---------------------------------------------------------------- 4) VPS'te deploy
# NOT: git esitleme BURADA yapilir (deploy.sh icinde degil): deploy.sh'in kendisi
# guncellenmisse, git reset sonrasi calisan surec hala ESKI dosyayi okur (bash fd).
# Bu yuzden once esitliyoruz, sonra guncel betigi SKIP_GIT=1 ile cagiriyoruz.
log "VPS deploy (git esitle + deploy/deploy.sh, IMAGE_SOURCE=local)"
ssh -o BatchMode=yes -o ServerAliveInterval=15 "${HOST}" \
  "set -e; cd '${REMOTE_DIR}'; git fetch --all --prune; git reset --hard 'origin/${BRANCH}'; \
   IMAGE_SOURCE=local IMAGE_TAG='${TAG}' SKIP_GIT=1 DEPLOY_BRANCH='${BRANCH}' ./deploy/deploy.sh"

# ---------------------------------------------------------------- 5) ozet
log "Release tamam"
printf '\n  tag        : %s\n  commit     : %s (%s)\n  servisler  : %s\n  canli      : https://%s/carsties/\n' \
  "${TAG}" "${SHA}" "${BRANCH}" "${SERVICES[*]}" "${DOMAIN}"
printf '\n  Rollback   : ssh %s "cd %s && IMAGE_SOURCE=local IMAGE_TAG=<eski-tag> SKIP_GIT=1 ./deploy/deploy.sh"\n' "${HOST}" "${REMOTE_DIR}"
printf '  Imaj listesi: ssh %s "docker image ls --format \\"{{.Repository}}:{{.Tag}} {{.CreatedSince}}\\" | grep carstieslive"\n\n' "${HOST}"
