# Infrastructure

ForgeRise targets **Docker + k3s + GitHub Actions** with a self-hosted runner (master prompt §12).

## Layout

```
infra/
├── README.md            # this file
└── k8s/
    ├── namespaces.yaml          # forgerise-{dev,staging,prod}
    ├── secrets.example.yaml     # shape only — real values never committed
    └── (later) deployments/, services/, ingress/, otel/
```

## Namespaces

```bash
kubectl apply -f infra/k8s/namespaces.yaml
```

## Secrets

Create per-environment secrets in the matching namespace. **Never commit real values.**
The example file `secrets.example.yaml` shows the *shape* only.

```bash
# Local example (uses --from-literal; for prod use sealed-secrets or external-secrets)
kubectl -n forgerise-dev create secret generic forgerise-api \
  --from-literal=ConnectionStrings__Default='Host=...;...' \
  --from-literal=Auth__Jwt__SigningKey='...' \
  --from-literal=Ai__OpenAi__ApiKey='...'
```

## Deployment (placeholder, Phase 1)

`deploy.yml` is `workflow_dispatch`-only in Phase 1 and runs on a self-hosted runner labelled
`forgerise-k3s`. It validates manifests with `kubeconform` and prints the planned diff via
`kubectl diff` — it does **not** apply changes until Phase 5.

## Rollback

Until Helm lands in Phase 5:

```bash
# Roll image back to the previous tag
kubectl -n forgerise-<env> rollout undo deployment/forgerise-api
kubectl -n forgerise-<env> rollout undo deployment/forgerise-web

# Or re-apply a known-good manifest revision
git checkout <good-sha> -- infra/k8s
kubectl -n forgerise-<env> apply -f infra/k8s
```

Always confirm with `kubectl -n forgerise-<env> rollout status deployment/<name>`.
