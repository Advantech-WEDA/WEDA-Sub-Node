#!/usr/bin/env bash
# Deploy one example's compose stack to a WEDA device through container-management,
# then score the four Act-4 function checks SEPARATELY and capture the device's log.
#
# The scenario this implements: docs/wiki/en/07-testing/01-e2e-test-scenario-example-images.md
#
# A container reported `running` satisfies E2E-EXD-4.1 and nothing else. 4.2, 4.3 and 4.4
# each need their own observation, which is why this script greps for each independently
# and records the matching line rather than emitting one "Function: PASS".
#
# Usage:
#   scripts/deploy-verify.sh <example> <compose-file> <container-name>
#
# Required env:
#   WEDA_TOKEN_FILE  file holding a Bearer JWT     (mint via the weda-login skill)
#   WEDA_BASE        e.g. https://weda-sit-k3s.weda.dev/central/weda
#   WEDA_ORG         org id
#   WEDA_STACK       stack-config id to revise
#   WEDA_DEVICE      target deviceId
#   WEDA_SSH         ssh destination for the device (e.g. an ~/.ssh/config alias)
# Optional env:
#   WEDA_CACERT      pin a self-signed cert instead of disabling verification
#   OUTDIR           where to write <example>.log / <example>.result (default: ./artifacts/e2e)
#   WAIT_TRIES       container-appear polls, 10s apart (default 45)

set -uo pipefail

EX="${1:?usage: $0 <example> <compose-file> <container-name>}"
COMPOSE="${2:?usage: $0 <example> <compose-file> <container-name>}"
CNAME="${3:?usage: $0 <example> <compose-file> <container-name>}"

: "${WEDA_TOKEN_FILE:?set WEDA_TOKEN_FILE}"; : "${WEDA_BASE:?set WEDA_BASE}"
: "${WEDA_ORG:?set WEDA_ORG}"; : "${WEDA_STACK:?set WEDA_STACK}"
: "${WEDA_DEVICE:?set WEDA_DEVICE}"; : "${WEDA_SSH:?set WEDA_SSH}"
OUTDIR="${OUTDIR:-artifacts/e2e}"; WAIT_TRIES="${WAIT_TRIES:-45}"
CA=""; [[ -n "${WEDA_CACERT:-}" ]] && CA="--cacert ${WEDA_CACERT}"

mkdir -p "$OUTDIR"
LOGFILE="$OUTDIR/$EX.log"; RESFILE="$OUTDIR/$EX.result"
TOK="$(cat "$WEDA_TOKEN_FILE")"
SSH="ssh -o ConnectTimeout=15 -o BatchMode=yes $WEDA_SSH"
api() { curl -sS $CA -H "Authorization: Bearer $TOK" "$@"; }

echo "===== $EX ====="
REV=$(python3 -c "import base64,json,sys;print(json.dumps({'composeFileContent':base64.b64encode(open(sys.argv[1],'rb').read()).decode(),'description':'verify '+sys.argv[2]}))" "$COMPOSE" "$EX" \
  | api -X PATCH "$WEDA_BASE/api/v1/orgs/$WEDA_ORG/stack-configs/$WEDA_STACK" \
      -H 'Content-Type: application/json' --data @- \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('stackRevisionId',''))" 2>/dev/null)
if [[ -z "$REV" ]]; then echo "  RESULT: FAIL — could not create revision"; exit 1; fi

# D-09: deploying over a stack whose service was renamed desyncs cloud and device.
# Always delete the device copy first; the deploy then reports `add` rather than `skip`.
api -X DELETE "$WEDA_BASE/api/v1/devices/$WEDA_DEVICE/docker/stacks?stackConfigId=$WEDA_STACK" -o /dev/null
sleep 12

ACT=$(api -X POST "$WEDA_BASE/api/v1/orgs/$WEDA_ORG/stack-configs/$WEDA_STACK/revisions/$REV:deploy" \
      -H 'Content-Type: application/json' -d "{\"deviceIds\":[\"$WEDA_DEVICE\"]}" \
      | python3 -c "import sys,json;print(json.load(sys.stdin).get('deviceActions',{}).get('$WEDA_DEVICE','?'))" 2>/dev/null)
echo "  E2E-EXD-3.2 deploy action: $ACT"

for _ in $(seq 1 "$WAIT_TRIES"); do
  $SSH "docker ps --format '{{.Names}}' | grep -qx $CNAME" 2>/dev/null && break
  sleep 10
done
sleep 25

STATE=$($SSH "docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' $CNAME 2>/dev/null" 2>/dev/null)
$SSH "docker logs $CNAME 2>&1" > "$LOGFILE" 2>/dev/null
echo "  E2E-EXD-4.1 ${STATE:-<no container>}  ($(wc -l < "$LOGFILE") log lines -> $LOGFILE)"

# Expect strings come from section 6 of the scenario doc — keep them in sync with it.
ev() { grep -m1 -iE "$1" "$LOGFILE" 2>/dev/null | sed 's/^[[:space:]]*//' | cut -c1-200; }
E42=$(ev 'NATS connection verified|NATS service discovered')
E43=$(ev 'Configuration uploaded successfully|Found existing SubNode registration|Device registered:')
E44=$(ev 'Reporting health|Health report sent successfully')
ERR=$(grep -m2 -iE 'Code=409|Device name already in use|Unhandled exception|Address already in use|Failed to register|Hosting failed|MissingMethodException|BadInternalError' "$LOGFILE" 2>/dev/null | tr '\n' ' ' | cut -c1-200)

ok() { [[ -n "$1" ]] && echo PASS || echo FAIL; }
R41=$(grep -q 'status=running' <<<"$STATE" && echo PASS || echo FAIL)
R42=$(ok "$E42"); R43=$(ok "$E43"); R44=$(ok "$E44")

echo "  E2E-EXD-4.2 $R42  ${E42:-<no matching line>}"
echo "  E2E-EXD-4.3 $R43  ${E43:-<no matching line>}"
echo "  E2E-EXD-4.4 $R44  ${E44:-<no matching line>}"
[[ -n "$ERR" ]] && echo "  errors: $ERR"

{ echo "example=$EX"; echo "deploy=$ACT"; echo "state=$STATE"
  echo "r41=$R41"; echo "r42=$R42"; echo "r43=$R43"; echo "r44=$R44"
  echo "e42=$E42"; echo "e43=$E43"; echo "e44=$E44"; echo "err=$ERR"; } > "$RESFILE"

if [[ "$R41 $R42 $R43 $R44" == "PASS PASS PASS PASS" && -z "$ERR" ]]; then
  echo "  VERDICT: PASS"; exit 0
else
  echo "  VERDICT: NOT-PASS — see $RESFILE"; exit 1
fi
