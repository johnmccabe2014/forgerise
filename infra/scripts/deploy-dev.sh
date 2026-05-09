#!/usr/bin/env bash
# Manual dev deploy — mirror of .github/workflows/deploy.yml apply step for
# bootstrap before the self-hosted runner is registered. Run from repo root.
#
# Prereqs:
#   - kubectl configured against the target cluster
#   - infra/scripts/create-ghcr-pull-secret.sh has been run for forgerise-dev
#   - forgerise-postgres + forgerise-api Secret already created
#
# Usage:
#   IMAGE_TAG=cba8ae2ae2f260c4dca39512dbef3cfcce515e88 \
#     infra/scripts/deploy-dev.sh
set -euo pipefail

NAMESPACE="${NAMESPACE:-forgerise-dev}"
PUBLIC_HOST="${PUBLIC_HOST:-dev.forgerise.cloudproject.dev}"
TLS_ISSUER="${TLS_ISSUER:-letsencrypt-prod-traefik}"
REPLICAS="${REPLICAS:-1}"
IMAGE_TAG="${IMAGE_TAG:?IMAGE_TAG required (full git sha pushed to ghcr)}"
REGISTRY="${REGISTRY:-ghcr.io}"
IMAGE_PREFIX="${IMAGE_PREFIX:-johnmccabe2014/forgerise}"

export NAMESPACE PUBLIC_HOST TLS_ISSUER REPLICAS IMAGE_TAG
export API_INTERNAL_URL="http://forgerise-api.${NAMESPACE}.svc.cluster.local"
export WEB_ORIGIN="https://${PUBLIC_HOST}"

mkdir -p .rendered
IMAGE_REGISTRY="${REGISTRY}/${IMAGE_PREFIX}-api" envsubst < infra/k8s/apps/api.yaml > .rendered/api.yaml
IMAGE_REGISTRY="${REGISTRY}/${IMAGE_PREFIX}-web" envsubst < infra/k8s/apps/web.yaml > .rendered/web.yaml
envsubst < infra/k8s/apps/ingress.yaml > .rendered/ingress.yaml

kubectl -n "$NAMESPACE" diff -f .rendered/ || true
kubectl -n "$NAMESPACE" apply -f .rendered/

kubectl -n "$NAMESPACE" rollout status deployment/forgerise-api --timeout=180s
kubectl -n "$NAMESPACE" rollout status deployment/forgerise-web --timeout=180s

echo "ok: deployed $IMAGE_TAG to $NAMESPACE"
echo "smoke:"
curl -fsS --max-time 10 -o /dev/null -w "  web    %{http_code}\n" "https://${PUBLIC_HOST}/" || true
curl -fsS --max-time 10 -o /dev/null -w "  proxy  %{http_code}\n" "https://${PUBLIC_HOST}/api/proxy/health" || true
