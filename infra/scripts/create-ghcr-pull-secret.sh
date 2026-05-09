#!/usr/bin/env bash
# Create (or refresh) the ghcr-credentials imagePullSecret in a ForgeRise
# namespace by copying it from a known-good source namespace on the cluster
# (default: anp). Other namespaces on the shared k3s cluster already use this
# pattern, so we just replicate the same secret.
#
# Usage:
#   infra/scripts/create-ghcr-pull-secret.sh [target-namespace] [source-namespace]
#
# Examples:
#   infra/scripts/create-ghcr-pull-secret.sh forgerise-dev
#   infra/scripts/create-ghcr-pull-secret.sh forgerise-staging anp
#
# If you need to create a fresh secret instead of copying, set GHCR_USER +
# GHCR_TOKEN (PAT with read:packages) and pass --new as the third argument.
set -euo pipefail

NS="${1:-forgerise-dev}"
SRC_NS="${2:-anp}"
MODE="${3:-copy}"
SECRET_NAME="ghcr-credentials"

kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -

if [ "$MODE" = "--new" ]; then
  : "${GHCR_USER:?GHCR_USER not set}"
  : "${GHCR_TOKEN:?GHCR_TOKEN not set (PAT with read:packages)}"
  kubectl -n "$NS" create secret docker-registry "$SECRET_NAME" \
    --docker-server=ghcr.io \
    --docker-username="$GHCR_USER" \
    --docker-password="$GHCR_TOKEN" \
    --docker-email="$GHCR_USER@users.noreply.github.com" \
    --dry-run=client -o yaml | kubectl apply -f -
else
  kubectl -n "$SRC_NS" get secret "$SECRET_NAME" -o json \
    | jq '{apiVersion,kind,type,data,metadata:{name:.metadata.name}}' \
    | kubectl -n "$NS" apply -f -
fi

echo "ok: $SECRET_NAME present in $NS"
