# ForgeRise infra / cluster bootstrap

Target cluster: k3s v1.32 on `185.239.238.3` (host `cloudproject`,
shared with several unrelated workloads — be careful).

## Pipeline state (in repo)

| Workflow | Trigger | Status |
| --- | --- | --- |
| [ci.yml](../.github/workflows/ci.yml) | push / PR | green |
| [security.yml](../.github/workflows/security.yml) | push / PR / weekly | green |
| [docker-build.yml](../.github/workflows/docker-build.yml) | push / PR / tag `v*` | green (publishes `ghcr.io/johnmccabe2014/forgerise-{api,web}:<sha>` + `:latest` on main) |
| [deploy.yml](../.github/workflows/deploy.yml) | `workflow_run` after `docker-build` succeeds, or manual `workflow_dispatch` | **blocked** — needs the self-hosted runner registered (see below) |

The accepted advisories live in [`.trivyignore`](../.trivyignore) (Trivy) and
in `pnpm.auditConfig.ignoreGhsas` of the root [package.json](../package.json)
(pnpm audit). They are timeboxed to `2026-09-01` — re-evaluate then.

## Cluster prerequisites (already satisfied)

- k3s v1.32.6 with Traefik (`LoadBalancer 185.249.197.69`) and local-path
  storage class.
- cert-manager 1.x with `letsencrypt-prod` and `letsencrypt-staging`
  ClusterIssuers.
- `kube-system/redirect-https@kubernetescrd` Traefik middleware (referenced by
  [`infra/k8s/apps/ingress.yaml`](k8s/apps/ingress.yaml)).
- DNS for `dev.forgerise.cloudproject.dev`, `staging.forgerise.cloudproject.dev`,
  and `forgerise.cloudproject.dev` should resolve to `185.249.197.69`. The
  apex `forgerise.cloudproject.dev` already does. Confirm/add the `dev` and
  `staging` CNAMEs/A-records before deploying those environments.

## One manual unlock (still TODO — agent could not perform without secrets)

### Register the self-hosted GitHub Actions runner

Generate a registration token at
`https://github.com/johnmccabe2014/forgerise/settings/actions/runners/new`
(expires ~1h). Then on the k3s box as `john`:

```bash
RUNNER_TOKEN=AAAA... infra/scripts/register-runner.sh
```

The script downloads `actions-runner v2.320.0` into
`~/forgerise-runner`, registers it with labels
`self-hosted,forgerise-k3s,linux,x64`, and installs it as a systemd unit so it
survives reboot. Requires `sudo` for the systemd install and to fix
permissions on `/etc/rancher/k3s/k3s.yaml`.

A runner already exists on this host bound to the `triforge` repo. The script
installs into a separate directory (`~/forgerise-runner`) so they coexist.

### GHCR pull secret (already done — copied from `anp` namespace)

The shared k3s cluster has a `ghcr-credentials` Secret in the `anp` namespace
with a PAT that can read `ghcr.io/johnmccabe2014/*`. Bootstrap copies it into
`forgerise-{dev,staging,prod}`:

```bash
infra/scripts/create-ghcr-pull-secret.sh forgerise-dev
infra/scripts/create-ghcr-pull-secret.sh forgerise-staging
infra/scripts/create-ghcr-pull-secret.sh forgerise-prod
```

The deployment manifests reference `imagePullSecrets: [{name: ghcr-credentials}]`
(matching the convention used by other apps on this cluster). If you ever need
a fresh secret with your own PAT (`read:packages`), pass `--new` and set
`GHCR_USER` + `GHCR_TOKEN`.

## Initial dev rollout

The agent applied steps 1–5. Auto-deploy turns on after the runner is registered.

```bash
# 1. Namespaces (one-time, all envs)
kubectl apply -f infra/k8s/namespaces.yaml

# 2. Postgres credentials secret + DB connection / JWT key
PG_PW=$(openssl rand -hex 24)
JWT_KEY=$(openssl rand -base64 64 | tr -d '\n=+/' | cut -c1-72)

kubectl -n forgerise-dev create secret generic forgerise-postgres \
  --from-literal=username=forgerise \
  --from-literal=password="$PG_PW" \
  --dry-run=client -o yaml | kubectl apply -f -

kubectl -n forgerise-dev create secret generic forgerise-api \
  --from-literal=ConnectionStrings__Postgres="Host=forgerise-postgres;Port=5432;Database=forgerise;Username=forgerise;Password=$PG_PW" \
  --from-literal=Jwt__Key="$JWT_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -

# 3. In-cluster Postgres for dev (emptyDir — ephemeral)
kubectl apply -f infra/k8s/postgres-dev.yaml

# 4. GHCR pull secret (copied from anp namespace)
infra/scripts/create-ghcr-pull-secret.sh forgerise-dev

# 5. App rollout (manual; equivalent to deploy.yml apply step)
IMAGE_TAG=$(git rev-parse origin/main) infra/scripts/deploy-dev.sh
```

After step 5, automated deploys run on every successful `docker-build` via
`deploy.yml`'s `workflow_run` trigger. Manually deploy a different commit with:

```bash
gh workflow run deploy.yml -f environment=dev -f image_tag=$(git rev-parse origin/main)
```

## Known cluster issues observed during bootstrap (2026-05-09)

- Node `cloudproject` had load average ~94 with only 5% CPU usage and many
  pods in `ContainerCreating` for several minutes. Likely IO/D-state
  contention. Helper pods used by `local-path-provisioner` timed out
  (`failed to create volume … create process timeout after 120 seconds`),
  which is why `infra/k8s/postgres-dev.yaml` uses `emptyDir` instead of a
  PVC. When the node recovers, consider switching back to a PVC or a managed
  Postgres.
- `kube-system` shows `NodeNotReady` events and `metrics-server` readiness
  failures. None of these are caused by ForgeRise resources but they will
  affect rollout times.

## Staging / prod

Run the same five steps above against the corresponding namespace
(`forgerise-staging` / `forgerise-prod`), supplying production-grade
credentials and replacing `postgres-dev.yaml` with a PVC-backed StatefulSet
or a managed Postgres. `deploy.yml` already parameterises namespace, host,
replicas, and TLS issuer per environment.

## Files

```
infra/
├── README.md                          this file
├── k8s/
│   ├── namespaces.yaml                forgerise-{dev,staging,prod}
│   ├── postgres-dev.yaml              dev-only postgres (emptyDir)
│   ├── secrets.example.yaml           shape only
│   └── apps/
│       ├── api.yaml                   templated by deploy.yml + envsubst
│       ├── web.yaml                   templated by deploy.yml + envsubst
│       └── ingress.yaml               templated by deploy.yml + envsubst
└── scripts/
    ├── create-ghcr-pull-secret.sh     one-shot pullSecret per namespace
    ├── deploy-dev.sh                  manual deploy mirror of deploy.yml
    └── register-runner.sh             register the forgerise-k3s runner
```
