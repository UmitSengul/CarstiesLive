#!/usr/bin/env bash
# ============================================================
# Carsties - VPS deploy betigi (CI/CD tarafindan cagrilir)
#
# Kullanim (VPS'te /opt/carsties icinde):
#   ./deploy/deploy.sh
#
# Yaptigi isler:
#   1) depoyu origin/<DEPLOY_BRANCH> ile esitler (varsayilan: path-migration;
#      bu repoda Carsties kodu o dalda yasar) - deploy/.env izlenmez, korunur
#   2) GHCR'ye login (GHCR_USER/GHCR_PAT verilirse)
#   3) imajlari ceker (IMAGE_TAG yoksa sha-<kisa commit>)
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

log() { printf '[%s] %s\n' "$(date '+%F %T')" "$*"; }

log "Depo guncelleniyor (origin/${BRANCH})"
git fetch --all --prune
git reset --hard "origin/${BRANCH}"

SHA="$(git rev-parse --short=7 HEAD)"
IMAGE_TAG="${REQUESTED_IMAGE_TAG:-sha-${SHA}}"
export IMAGE_TAG

if [[ -n "${GHCR_PAT:-}" ]]; then
  log "GHCR login (${GHCR_USER:-umitsengul})"
  printf '%s' "${GHCR_PAT}" | docker login ghcr.io -u "${GHCR_USER:-umitsengul}" --password-stdin
fi

COMPOSE=(docker compose --env-file deploy/.env -f docker-compose.yml -f deploy/docker-compose.prod.yml)

log "Imajlar cekiliyor (IMAGE_TAG=${IMAGE_TAG})"
"${COMPOSE[@]}" pull

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
log "Deploy tamam (IMAGE_TAG=${IMAGE_TAG})"
