#!/usr/bin/env bash
# Register a GitHub Actions self-hosted runner for the ForgeRise repo.
# Run on the k3s box (185.239.238.3) as user `john`.
#
# Prereqs:
#   - sudo access (for installing the systemd unit)
#   - A registration token from
#     https://github.com/johnmccabe2014/forgerise/settings/actions/runners/new
#     (tokens expire ~1h after generation).
#
# Usage:
#   RUNNER_TOKEN=AAAA... infra/scripts/register-runner.sh
set -euo pipefail

: "${RUNNER_TOKEN:?RUNNER_TOKEN not set}"

REPO_URL="${REPO_URL:-https://github.com/johnmccabe2014/forgerise}"
RUNNER_DIR="${RUNNER_DIR:-$HOME/forgerise-runner}"
RUNNER_NAME="${RUNNER_NAME:-forgerise-k3s-1}"
RUNNER_LABELS="${RUNNER_LABELS:-self-hosted,forgerise-k3s,linux,x64}"
RUNNER_VERSION="${RUNNER_VERSION:-2.320.0}"

mkdir -p "$RUNNER_DIR"
cd "$RUNNER_DIR"

if [ ! -x ./config.sh ]; then
  curl -fsSL -o actions-runner.tgz \
    "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
  tar xzf actions-runner.tgz
  rm actions-runner.tgz
fi

if [ -f .runner ]; then
  echo "Runner already configured. Re-run with RUNNER_DIR=/path/to/clean/dir to add another."
else
  ./config.sh \
    --url "$REPO_URL" \
    --token "$RUNNER_TOKEN" \
    --name "$RUNNER_NAME" \
    --labels "$RUNNER_LABELS" \
    --work _work \
    --unattended \
    --replace
fi

# Make the k3s kubeconfig readable to the runner. The cluster file is
# rw------- root:john, so we either fix perms (preferred) or copy to ~/.kube.
if [ ! -r /etc/rancher/k3s/k3s.yaml ]; then
  echo "Granting group john read on /etc/rancher/k3s/k3s.yaml"
  sudo chmod 0640 /etc/rancher/k3s/k3s.yaml
  sudo chown root:john /etc/rancher/k3s/k3s.yaml
fi

# Install + start as systemd service so it survives reboot.
sudo ./svc.sh install john
sudo ./svc.sh start
sudo ./svc.sh status | head -10

echo
echo "ok: runner registered as $RUNNER_NAME with labels [$RUNNER_LABELS]"
echo "verify: gh api /repos/johnmccabe2014/forgerise/actions/runners --jq '.runners[] | {name,labels:[.labels[].name],status}'"
