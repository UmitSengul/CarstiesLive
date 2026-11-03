#!/usr/bin/env bash
# ============================================================
# Carsties - VPS deploy betigi
#
# Kullanim (VPS'te /opt/carsties icinde):
#   ./deploy/deploy.sh                          # IMAGE_SOURCE=registry (varsayilan)
#   IMAGE_SOURCE=local IMAGE_TAG=sha-abc1234 ./deploy/deploy.sh
#
# IMAGE_SOURCE:
#   registry -> imajlar GHCR'den cekilir (docker compose pull)
#   local    -> imajlar disarida (dev makinesinde) build edilip `docker load` ile
#               yuklenmistir; pull ATLANIR ve pull_policy kapatilir
#               (bkz. deploy/docker-compose.local-images.yml)
#
# Yaptigi isler:
#   1) depoyu origin/<DEPLOY_BRANCH> ile esitler (varsayilan: path-migration;
#      bu repoda Carsties kodu o dalda yasar) - deploy/.env izlenmez, korunur
#      SKIP_GIT=1 verilirse bu adim atlanir
#   2) GHCR'ye login (yalniz registry modunda; GHCR_USER/GHCR_PAT verilirse)
#   3) imajlari ceker (registry) ya da yerelde varligini dogrular (local)
#   4) servisleri gunceller (build YOK)
#   5) edge nginx'i yeniden yukler + smoke test
#
# Geri alma: git reset --hard <eski-commit> && ./deploy/deploy.sh
# ============================================================
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${REPO_DIR}"

BRANCH="${DEPLOY_BRANCH:-path-migration}"
REQUESTED_IMAGE_TAG="${IMAGE_TAG:-}"
IMAGE_SOURCE="${IMAGE_SOURCE:-registry}"
SKIP_GIT="${SKIP_GIT:-0}"

case "${IMAGE_SOURCE}" in
  registry|local) ;;
  *) echo "HATA: IMAGE_SOURCE 'registry' ya da 'local' olmali (verilen: ${IMAGE_SOURCE})" >&2; exit 2 ;;
esac

log() { printf '[%s] %s\n' "$(date '+%F %T')" "$*"; }

if [[ "${SKIP_GIT}" == "1" ]]; then
  log "Git esitleme atlandi (SKIP_GIT=1)"
else
  SCRIPT_PATH="$(readlink -f "${BASH_SOURCE[0]}")"
  SUM_BEFORE="$(sha1sum "${SCRIPT_PATH}" | cut -d' ' -f1)"

  log "Depo guncelleniyor (origin/${BRANCH})"
  git fetch --all --prune
  git reset --hard "origin/${BRANCH}"

  # Betik dosyasi git reset ile degistiyse, calisan surec hala ESKI icerigi okur
  # (bash dosyayi fd ile acmis tutar). Bu durumda yeni surumle yeniden baslat;
  # aksi halde deploy eski mantikla devam eder (orn. pull_policy/pull davranisi).
  if [[ -z "${DEPLOY_REEXEC:-}" && "$(sha1sum "${SCRIPT_PATH}" | cut -d' ' -f1)" != "${SUM_BEFORE}" ]]; then
    log "deploy.sh guncellendi -> yeni surumle yeniden calisiyor"
    exec env DEPLOY_REEXEC=1 SKIP_GIT=1 IMAGE_SOURCE="${IMAGE_SOURCE}" \
      IMAGE_TAG="${REQUESTED_IMAGE_TAG:-}" DEPLOY_BRANCH="${BRANCH}" "${SCRIPT_PATH}" "$@"
  fi
fi

SHA="$(git rev-parse --short=7 HEAD)"
IMAGE_TAG="${REQUESTED_IMAGE_TAG:-sha-${SHA}}"
export IMAGE_TAG

COMPOSE=(docker compose --env-file deploy/.env -f docker-compose.yml -f deploy/docker-compose.prod.yml)
if [[ "${IMAGE_SOURCE}" == "local" ]]; then
  COMPOSE+=(-f deploy/docker-compose.local-images.yml)
fi

APP_IMAGES=(
  "ghcr.io/umitsengul/carstieslive-auction-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-bid-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-search-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-notify-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-identity-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-gateway-svc:${IMAGE_TAG}"
  "ghcr.io/umitsengul/carstieslive-web-app:${IMAGE_TAG}"
)

if [[ "${IMAGE_SOURCE}" == "local" ]]; then
  log "Yerel imajlar dogrulaniyor (IMAGE_TAG=${IMAGE_TAG})"
  for img in "${APP_IMAGES[@]}"; do
    if docker image inspect "${img}" >/dev/null 2>&1; then
      continue
    fi
    # Kismi release (release.sh --only <servis>) durumunda bu servis icin yeni imaj
    # tasinmamis olabilir: mevcut en son imaji istenen etiketle isaretle, servis
    # boylece calismaya devam eder (yalnizca degisen servisler guncellenir).
    repo="${img%:*}"
    src="$(docker image ls --format '{{.Repository}}:{{.Tag}}' "${repo}" 2>/dev/null | grep -v ":${IMAGE_TAG}$" | head -1 || true)"
    if [[ -n "${src}" ]]; then
      log "Etiketlendi: ${src} -> ${img}"
      docker tag "${src}" "${img}"
    else
      log "EKSIK IMAJ: ${img} (baska surum de yok)"
      MISSING=1
    fi
  done
  if [[ "${MISSING:-0}" == "1" ]]; then
    log "HATA: eksik imajlar var. Dev makinesinde ./deploy/release.sh calistir (imajlar SSH ile tasinir)."
    exit 3
  fi
else
  if [[ -n "${GHCR_PAT:-}" ]]; then
    log "GHCR login (${GHCR_USER:-umitsengul})"
    printf '%s' "${GHCR_PAT}" | docker login ghcr.io -u "${GHCR_USER:-umitsengul}" --password-stdin
  fi

  log "Imajlar cekiliyor (IMAGE_TAG=${IMAGE_TAG})"
  "${COMPOSE[@]}" pull
fi

log "Servisler guncelleniyor"
"${COMPOSE[@]}" up -d --remove-orphans

log "Edge nginx yeniden yukleniyor"
if docker exec nginx-proxy nginx -t 2>/dev/null; then
  docker exec nginx-proxy nginx -s reload || docker restart nginx-proxy
else
  log "UYARI: edge nginx hazir degil; yeniden baslatiliyor"
  docker restart nginx-proxy >/dev/null || true
fi

docker image prune -f >/dev/null 2>&1 || true

# Disk uyarisi: her deploy yeni bir imaj seti birakir (yerel modda ~2 GB)
AVAIL_KB="$(df -Pk /var/lib/docker 2>/dev/null | awk 'NR==2 {print $4}' || echo 0)"
if [[ -n "${AVAIL_KB}" && "${AVAIL_KB}" -lt 5242880 ]]; then
  log "UYARI: /var/lib/docker altinda 5 GB'tan az yer kaldi ($((AVAIL_KB / 1024)) MB). Temizlik: docker image prune -a --filter 'until=168h'"
fi

DOMAIN_VALUE="$(grep -E '^DOMAIN=' deploy/.env 2>/dev/null | tail -1 | cut -d= -f2- | tr -d '\"' || true)"
DOMAIN_VALUE="${DOMAIN_VALUE:-umitsengul.online}"
log "Smoke test: https://${DOMAIN_VALUE}/ ve /carsties/"
if ! curl -fsS -m 20 "https://${DOMAIN_VALUE}/" >/dev/null; then
  log "UYARI: kok alan adi yanit vermedi (sertifika henuz alinmadi olabilir: ./deploy/certs.sh issue)"
fi
if ! curl -fsS -m 20 "https://${DOMAIN_VALUE}/carsties/" >/dev/null; then
  log "UYARI: /carsties/ yanit vermedi; kontrol: docker compose ps + docker logs carsties-web-app-1"
fi
if [[ "${SMOKE_STRICT:-0}" == "1" ]]; then
  curl -fsS -m 20 "https://${DOMAIN_VALUE}/carsties/" >/dev/null
fi
log "Deploy tamam (IMAGE_TAG=${IMAGE_TAG}, IMAGE_SOURCE=${IMAGE_SOURCE})"
