#!/usr/bin/env bash
# ============================================================
# Carsties - Kalite kapisi (GitHub Actions'in yerel karsiligi)
#
# Eski .github/workflows/pipeline.yml "quality" job'unun aynisi; artik CI'da degil
# release oncesi yerelde calisir:
#   ./deploy/quality.sh                 # hizli: npm ci atlanir (node_modules varsa)
#   ./deploy/quality.sh --with-install  # temiz kurulum: npm ci calisir
#   ./deploy/quality.sh --skip-dotnet   # yalniz frontend
#   ./deploy/quality.sh --skip-frontend # yalniz .NET
#
# Adimlar:
#   .NET      : restore -> build (Release) -> NuGet zafiyet kapisi (transitif dahil)
#   frontend  : (npm ci) -> npm audit (runtime, high+) -> lint -> next build (prod basePath)
#               -> .next/routes-manifest.json basePath dogrulamasi
# ============================================================
set -euo pipefail

DOMAIN="${DOMAIN:-umitsengul.online}"
BASE_PATH="/carsties"
SKIP_DOTNET=0
SKIP_FRONTEND=0
WITH_INSTALL=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-dotnet) SKIP_DOTNET=1; shift ;;
    --skip-frontend) SKIP_FRONTEND=1; shift ;;
    --with-install) WITH_INSTALL=1; shift ;;
    -h|--help) sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Bilinmeyen arguman: $1" >&2; exit 2 ;;
  esac
done

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${REPO_DIR}"

step() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }

command -v jq >/dev/null 2>&1 || { echo "HATA: jq gerekli (dnf/apt install jq)" >&2; exit 2; }

if [[ "${SKIP_DOTNET}" != "1" ]]; then
  step ".NET restore"
  dotnet restore Carsties.sln

  step ".NET build (Release)"
  dotnet build Carsties.sln --no-restore -c Release

  step "NuGet zafiyet kapisi (bilinen acik varsa kirilir)"
  dotnet package list --vulnerable --include-transitive --format json > /tmp/carsties-vuln.json
  jq -e '[.projects[] | select(.frameworks != null and (.frameworks | length > 0))] | length == 0' \
    /tmp/carsties-vuln.json >/dev/null \
    || { jq -r '.projects[] | select(.frameworks != null) | .frameworks[] | select(.vulnerabilities != null) | .vulnerabilities[] | "  \(.package) \(.resolvedVersion) -> \(.severity)"' /tmp/carsties-vuln.json; exit 1; }
  echo "  temiz"
fi

if [[ "${SKIP_FRONTEND}" != "1" ]]; then
  cd frontend/web-app

  if [[ "${WITH_INSTALL}" == "1" ]]; then
    step "npm ci"
    npm ci --no-audit --no-fund
  fi

  step "npm audit (runtime, high+)"
  npm audit --omit=dev --audit-level=high

  step "eslint"
  npm run lint

  step "next build (prod basePath ile)"
  BETTER_AUTH_SECRET=ci-build-only-secret-not-used-at-runtime \
  BETTER_AUTH_URL="https://${DOMAIN}" \
  NEXT_PUBLIC_BASE_API="https://${DOMAIN}${BASE_PATH}/api" \
  NEXT_PUBLIC_ID_URL="https://${DOMAIN}${BASE_PATH}/id" \
  NEXT_PUBLIC_BASE_PATH="${BASE_PATH}" \
    npm run build

  step "basePath dogrulamasi"
  jq -e --arg bp "${BASE_PATH}" '.basePath == $bp' .next/routes-manifest.json >/dev/null \
    || { echo "HATA: basePath '${BASE_PATH}' degil" >&2; exit 1; }

  cd "${REPO_DIR}"
fi

printf '\n\033[1;32mKalite kapisi gecti.\033[0m Sirada: ./deploy/release.sh\n'
