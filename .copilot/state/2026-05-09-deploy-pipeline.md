# State: Deploy pipeline (k3s + self-hosted runner)

**Date:** 2026-05-09
**Owner:** devops
**Domain:** forgerise.cloudproject.dev (k3s on 185.239.238.3)

## Goal

Wire ForgeRise to deploy on the existing k3s cluster via a self-hosted GitHub
runner (label `forgerise-k3s`), with build/scan/SBOM/push happening on
GitHub-hosted runners and the deploy step running on the cluster host.

## Iter 1 — what shipped

- `web/Dockerfile`, `api/Dockerfile`, `web/.dockerignore`, `api/.dockerignore`,
  root `.dockerignore`. Multi-stage, non-root (uid/gid 10001), readOnlyRootFs,
  HEALTHCHECK, drops all caps.
- `web/next.config.mjs` → `output: "standalone"` so the runtime image only
  ships server.js + needed bundles (~150 MB vs ~700 MB).
- `infra/k8s/apps/{api,web,ingress}.yaml` — Deployment+Service per workload,
  Traefik ingress with cert-manager TLS. All values templated via `${VAR}`
  for envsubst at deploy time.
- `.github/workflows/docker-build.yml` — matrix build with per-component
  context + Dockerfile path, pinned actions, GHCR push only off PRs, Trivy
  HIGH/CRITICAL gate, Syft SBOM uploaded as artifact.
- `.github/workflows/deploy.yml`:
  - `workflow_run` trigger on successful `docker-build` (auto-deploys dev).
  - `workflow_dispatch` for staging/prod promotion (with optional `image_tag`).
  - `resolve` job → per-env namespace/host/replicas/issuer.
  - `validate` job → envsubst + kubeconform on rendered manifests, uploads
    them as an artifact.
  - `deploy` job on `[self-hosted, forgerise-k3s]` → downloads rendered
    manifests, `kubectl diff` (informational), `apply`, `rollout status`,
    smoke tests `https://${HOST}/` and `https://${HOST}/api/proxy/health`,
    prints rollback commands on failure.
- `infra/README.md` — runner setup, cert-manager bootstrap, Traefik
  redirect-https middleware, secret creation, deploy + rollback flow.
- `.github/workflows/security.yml` — dropped `continue-on-error` from
  hadolint now that real Dockerfiles exist.

## Cluster prerequisites (must run once on 185.239.238.3)

1. `kubectl apply -f infra/k8s/namespaces.yaml`
2. Install cert-manager + `letsencrypt-prod` ClusterIssuer.
3. Install Traefik `redirect-https` Middleware in `kube-system`.
4. Create `forgerise-api` Secret in each namespace (`ConnectionStrings__Postgres`, `Jwt__Key`, AI keys).
5. Register the self-hosted runner with label `forgerise-k3s`; ensure it can
   read kubeconfig and has `kubectl` + `curl` + `envsubst` on PATH.
6. DNS: `*.forgerise.cloudproject.dev` (or three A records) → `185.239.238.3`.

## Open follow-ups (next iter)

- Pin base images by digest (currently pinned to minor tags).
- NetworkPolicy: web → api only, api → external Postgres only.
- Secrets via sealed-secrets or external-secrets (currently created
  out-of-band).
- Branch protection on `main` requiring `ci`, `security`, `docker-build` green.
- CSP header on Next responses (master prompt §10).
- OTel collector wiring + Grafana on cluster.

## Rollback (always available)

```bash
kubectl -n forgerise-<env> rollout undo deployment/forgerise-api
kubectl -n forgerise-<env> rollout undo deployment/forgerise-web
```
