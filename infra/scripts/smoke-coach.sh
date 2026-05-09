#!/usr/bin/env bash
# End-to-end coach smoke against a deployed ForgeRise environment.
#
# Exercises:
#   register -> /auth/me -> create team -> create player -> list players
#
# Uses the Next.js /api/proxy passthrough (which injects X-CSRF-Token from
# the fr_csrf cookie on unsafe methods), so the only auth state we need to
# carry between calls is the cookie jar.
#
# Usage:
#   infra/scripts/smoke-coach.sh [host]
#
# Default host: dev.forgerise.cloudproject.dev
set -euo pipefail

HOST="${1:-dev.forgerise.cloudproject.dev}"
BASE="https://${HOST}/api/proxy"
JAR="$(mktemp)"
trap 'rm -f "$JAR"' EXIT

stamp="$(date +%s)-$$"
EMAIL="coach+${stamp}@example.com"
PASSWORD="correcthorsebatterystaple"
DISPLAY_NAME="Coach ${stamp}"
TEAM_NAME="Smoke FC ${stamp}"
TEAM_CODE="smoke-${stamp}"
PLAYER_NAME="Smoke Player ${stamp}"

# JSON parser: prefer jq, fall back to python.
extract() {
  local field="$1"
  if command -v jq >/dev/null 2>&1; then
    jq -r ".${field} // empty"
  else
    python3 -c "import sys,json;d=json.load(sys.stdin);
parts='${field}'.split('.');
cur=d
for p in parts:
    cur = cur.get(p) if isinstance(cur,dict) else None
print(cur if cur is not None else '')"
  fi
}

# call <METHOD> <PATH> [json-body]
# Echoes the response body to stdout; checks the HTTP code matches expected.
call() {
  local method="$1" path="$2" body="${3:-}" expected="${4:-2}"
  local out code
  out="$(mktemp)"
  if [[ -n "$body" ]]; then
    code=$(curl -sS -o "$out" -w '%{http_code}' \
      -X "$method" "${BASE}${path}" \
      -b "$JAR" -c "$JAR" \
      -H 'Content-Type: application/json' \
      -d "$body")
  else
    code=$(curl -sS -o "$out" -w '%{http_code}' \
      -X "$method" "${BASE}${path}" \
      -b "$JAR" -c "$JAR")
  fi
  if [[ "${code:0:1}" != "$expected" ]]; then
    echo "FAIL: $method $path -> $code" >&2
    cat "$out" >&2
    rm -f "$out"
    exit 1
  fi
  cat "$out"
  rm -f "$out"
}

echo "==> host: ${HOST}"
echo "==> coach: ${EMAIL}"

echo "==> register"
REG=$(call POST /auth/register "{\"email\":\"${EMAIL}\",\"password\":\"${PASSWORD}\",\"displayName\":\"${DISPLAY_NAME}\"}")
USER_ID=$(echo "$REG" | extract "user.id")
[[ -n "$USER_ID" ]] || { echo "FAIL: missing user.id in register response: $REG" >&2; exit 1; }
echo "    user.id = $USER_ID"

echo "==> /auth/me"
ME=$(call GET /auth/me)
ME_EMAIL=$(echo "$ME" | extract "email")
[[ "$ME_EMAIL" == "$EMAIL" ]] || { echo "FAIL: /auth/me email mismatch ($ME_EMAIL vs $EMAIL)" >&2; exit 1; }
echo "    email = $ME_EMAIL"

echo "==> POST /teams"
TEAM=$(call POST /teams "{\"name\":\"${TEAM_NAME}\",\"code\":\"${TEAM_CODE}\"}")
TEAM_ID=$(echo "$TEAM" | extract "id")
[[ -n "$TEAM_ID" ]] || { echo "FAIL: missing team.id: $TEAM" >&2; exit 1; }
echo "    team.id = $TEAM_ID"

echo "==> POST /teams/$TEAM_ID/players"
PLAYER=$(call POST "/teams/${TEAM_ID}/players" "{\"displayName\":\"${PLAYER_NAME}\",\"jerseyNumber\":7,\"position\":\"FW\"}")
PLAYER_ID=$(echo "$PLAYER" | extract "id")
[[ -n "$PLAYER_ID" ]] || { echo "FAIL: missing player.id: $PLAYER" >&2; exit 1; }
echo "    player.id = $PLAYER_ID"

echo "==> GET /teams/$TEAM_ID/players"
PLAYERS=$(call GET "/teams/${TEAM_ID}/players")
if ! echo "$PLAYERS" | grep -q "$PLAYER_ID"; then
  echo "FAIL: created player not present in list" >&2
  echo "$PLAYERS" >&2
  exit 1
fi
echo "    list contains created player"

echo "==> POST /auth/logout"
call POST /auth/logout >/dev/null
echo "    logged out"

echo
echo "OK: coach end-to-end smoke passed against ${HOST}"
