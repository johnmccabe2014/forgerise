#!/usr/bin/env bash
# Create (or refresh) the ghcr-pull imagePullSecret in a ForgeRise namespace.
#
# Usage:
#   GHCR_USER=johnmccabe2014 GHCR_TOKEN=ghp_xxx \
#     infra/scripts/create-ghcr-pull-secret.sh forgerise-dev
#
# The token must have at least `read:packages` scope and be permitted to read
# the ghcr.io/johnmccabe2014/forgerise-{api,web} packages. Generate one at:
#   https://github.com/settings/tokens?type=beta
set -euo pipefail

NS="${1:-forgerise-dev}"
: "${GHCR_USER:?GHCR_USER not set (your GitHub username)}"
: "${GHCR_TOKEN:?GHCR_TOKEN not set (PAT with read:packages)}"

kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -

kubectl -n "$NS" create secret docker-registry ghcr-pull \
  --docker-server=ghcr.io \
  --docker-username="$GHCR_USER" \
  --docker-password="$GHCR_TOKEN" \
  --docker-email="$GHCR_USER@users.noreply.github.com" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "ok: ghcr-pull secret created/updated in $NS"
