#!/usr/bin/env bash
# Deploy one example to a WEDA device through container-management, then score the Act-4 function
# checks SEPARATELY and capture the device's log.
#
# The scenario this implements: docs/wiki/en/07-testing/01-e2e-test-scenario-example-images.md
#
# A container reported `running` satisfies 4.1 and nothing else, so 4.2, 4.3, 4.4 and 4.5 each
# need their own observation. 4.4 is the framework heartbeat, which every SubNode emits whether or
# not its own device is reachable; 4.5 is what this example exists to do, and the two are scored
# apart because the heartbeat alone was passing examples that had never received a single reading.
#
# Everything but the example name comes from examples/<name>/e2e-expect.json, so a re-run needs
# no arguments that can drift from the file.
#
# Usage:
#   scripts/deploy-verify.sh <example>
#   scripts/deploy-verify.sh <example> --from-log <file>   # score an existing log, no device
#
# Required env (not for --from-log):
#   WEDA_TOKEN_FILE  file holding a Bearer JWT     (mint via the weda-login skill)
#   WEDA_BASE        e.g. https://weda-sit-k3s.weda.dev/central/weda
#   WEDA_ORG         org id
#   WEDA_STACK       stack-config id to revise
#   WEDA_DEVICE      target deviceId
#   WEDA_SSH         ssh destination for the device (e.g. an ~/.ssh/config alias)
# Optional env:
#   WEDA_CACERT      pin a self-signed cert instead of disabling verification
#   SUBNODE_NAME     passed to the stack config; the compose otherwise names the SubNode after
#                    the device id the agent injects
#   SystemConfig__WedaNode__Password
#                    passed to the stack config; only needed when the image was built for a
#                    different WedaNode than the one on this device
#   OUTDIR           where to write <example>.log / <example>.result (default: ./artifacts/e2e)
#   WAIT_TRIES       container-appear polls, 10s apart (default 45)

set -uo pipefail

EX="${1:?usage: $0 <example> [--from-log <file>]}"
FROM_LOG=""
if [[ "${2:-}" == "--from-log" ]]; then
  FROM_LOG="${3:?usage: $0 <example> --from-log <file>}"
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC="${REPO_ROOT}/examples/${EX}"
EXPECT="${SRC}/e2e-expect.json"
[[ -f "${EXPECT}" ]] || { echo "ERROR: no examples/${EX}/e2e-expect.json" >&2; exit 1; }

# Pick an interpreter that actually runs: on Windows `python3` resolves to a Microsoft Store stub
# that is found by `command -v` and then produces nothing at all, which reads as an empty field
# rather than as an error.
PY=""
for cand in python3 python; do
  if command -v "$cand" >/dev/null 2>&1 && "$cand" -c 'pass' >/dev/null 2>&1; then
    PY="$(command -v "$cand")"; break
  fi
done
[[ -n "${PY}" ]] || { echo "ERROR: a working python is required to read e2e-expect.json" >&2; exit 1; }
# tr -d strips the carriage return a Windows python puts on every line, which would otherwise
# ride along into a grep pattern and match nothing.
field() { "${PY}" -c "import json,sys;d=json.load(open(sys.argv[1],encoding='utf-8'));print(eval(sys.argv[2],{'d':d}))" "${EXPECT}" "$1" | tr -d '\r'; }

COMPOSE="${SRC}/$(field "d['compose']")"
CNAME="$(field "d['container']")"
MODE="$(field "d['business']['mode']")"
OUTDIR="${OUTDIR:-artifacts/e2e}"; WAIT_TRIES="${WAIT_TRIES:-45}"
mkdir -p "$OUTDIR"
LOGFILE="$OUTDIR/$EX.log"; RESFILE="$OUTDIR/$EX.result"

echo "===== $EX ====="

ACT="(not deployed)"
STATE=""
if [[ -n "${FROM_LOG}" ]]; then
  # Offline: score a log captured earlier. This exists so the expectations themselves can be
  # checked without a device - a pattern that no longer matches is a defect in the expectation.
  [[ -f "${FROM_LOG}" ]] || { echo "ERROR: no such log: ${FROM_LOG}" >&2; exit 1; }
  cp "${FROM_LOG}" "${LOGFILE}"
  STATE="status=(from-log) restarts=(from-log)"
  echo "  scoring ${FROM_LOG} (no device involved)"
else
  : "${WEDA_TOKEN_FILE:?set WEDA_TOKEN_FILE}"; : "${WEDA_BASE:?set WEDA_BASE}"
  : "${WEDA_ORG:?set WEDA_ORG}"; : "${WEDA_STACK:?set WEDA_STACK}"
  : "${WEDA_DEVICE:?set WEDA_DEVICE}"; : "${WEDA_SSH:?set WEDA_SSH}"
  CA=""; [[ -n "${WEDA_CACERT:-}" ]] && CA="--cacert ${WEDA_CACERT}"
  TOK="$(cat "$WEDA_TOKEN_FILE")"
  SSH="ssh -o ConnectTimeout=15 -o BatchMode=yes $WEDA_SSH"
  api() { curl -sS $CA -H "Authorization: Bearer $TOK" "$@"; }

  # A stack config carries an environment map beside the compose, and that map is what ${...} in
  # the compose resolves against - the device has no .env. Most deployments need nothing in it:
  # the image carries its own WedaNode connection and the SubNode name defaults to one that is
  # unique per device.
  REV=$("${PY}" -c "
import base64,json,os,sys
env={k:v for k,v in (
    ('SUBNODE_NAME', os.environ.get('SUBNODE_NAME','')),
    ('SystemConfig__WedaNode__Password', os.environ.get('SystemConfig__WedaNode__Password','')),
) if v}
print(json.dumps({'composeFileContent': base64.b64encode(open(sys.argv[1],'rb').read()).decode(),
                  'environment': env,
                  'description': 'verify ' + sys.argv[2]}))" "$COMPOSE" "$EX" \
    | api -X PATCH "$WEDA_BASE/api/v1/orgs/$WEDA_ORG/stack-configs/$WEDA_STACK" \
        -H 'Content-Type: application/json' --data @- \
    | "${PY}" -c "import sys,json;print(json.load(sys.stdin).get('stackRevisionId',''))" 2>/dev/null)
  if [[ -z "$REV" ]]; then echo "  RESULT: FAIL - could not create revision"; exit 1; fi

  # Deploying over a stack whose service was renamed desyncs cloud and device, and the recovery
  # command cannot bridge the two. Always delete the device copy first; the deploy then reports
  # `add` rather than `skip`.
  api -X DELETE "$WEDA_BASE/api/v1/devices/$WEDA_DEVICE/docker/stacks?stackConfigId=$WEDA_STACK" -o /dev/null
  sleep 12

  ACT=$(api -X POST "$WEDA_BASE/api/v1/orgs/$WEDA_ORG/stack-configs/$WEDA_STACK/revisions/$REV:deploy" \
        -H 'Content-Type: application/json' -d "{\"deviceIds\":[\"$WEDA_DEVICE\"]}" \
        | "${PY}" -c "import sys,json;print(json.load(sys.stdin).get('deviceActions',{}).get('$WEDA_DEVICE','?'))" 2>/dev/null)
  echo "  E2E-EXD-3.2 deploy action: $ACT"

  for _ in $(seq 1 "$WAIT_TRIES"); do
    $SSH "docker ps --format '{{.Names}}' | grep -qx $CNAME" 2>/dev/null && break
    sleep 10
  done
  sleep 25

  STATE=$($SSH "docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' $CNAME 2>/dev/null" 2>/dev/null)
  $SSH "docker logs $CNAME 2>&1" > "$LOGFILE" 2>/dev/null
fi

echo "  E2E-EXD-4.1 ${STATE:-<no container>}  ($(wc -l < "$LOGFILE") log lines -> $LOGFILE)"

# Expect strings come from section 6 of the scenario doc - keep them in sync with it.
ev() { grep -m1 -iE "$1" "$LOGFILE" 2>/dev/null | sed 's/^[[:space:]]*//' | cut -c1-200; }
E42=$(ev 'NATS connection verified|NATS service discovered')
E43=$(ev 'Configuration uploaded successfully|Found existing SubNode registration|Device registered:')
E44=$(ev 'Reporting health|Health report sent successfully')
ERR=$(grep -m2 -iE 'Code=409|Device name already in use|Authorization Violation|Unhandled exception|Address already in use|Failed to register|Hosting failed|MissingMethodException|BadInternalError' "$LOGFILE" 2>/dev/null | tr '\n' ' ' | cut -c1-200)

ok() { [[ -n "$1" ]] && echo PASS || echo FAIL; }
if [[ -n "${FROM_LOG}" ]]; then R41="SKIP"; else
  R41=$(grep -q 'status=running' <<<"$STATE" && echo PASS || echo FAIL); fi
R42=$(ok "$E42"); R43=$(ok "$E43"); R44=$(ok "$E44")

# 4.5 - what this example exists to do. Only `auto` is scored: the rest have no successful sample
# to match against yet, and calling that a pass is how the heartbeat came to stand in for the job.
E45=""; R45="NOT-SCORED"
if [[ "$MODE" == "auto" ]]; then
  R45=PASS
  while IFS= read -r pat; do
    [[ -z "$pat" ]] && continue
    hit=$(ev "$pat")
    if [[ -z "$hit" ]]; then R45=FAIL; E45="no line matching: $pat"; break; fi
    [[ -z "$E45" ]] && E45="$hit"
  done < <("${PY}" -c "import json,sys;[print(p) for p in json.load(open(sys.argv[1],encoding='utf-8'))['business']['patterns']]" "${EXPECT}" | tr -d '\r')
else
  E45="$(field "d['business']['note']")"
fi

echo "  E2E-EXD-4.2 $R42  ${E42:-<no matching line>}"
echo "  E2E-EXD-4.3 $R43  ${E43:-<no matching line>}"
echo "  E2E-EXD-4.4 $R44  ${E44:-<no matching line>}   (heartbeat only)"
echo "  E2E-EXD-4.5 $R45  [$MODE] ${E45:-<no matching line>}"
[[ -n "$ERR" ]] && echo "  errors: $ERR"

{ echo "example=$EX"; echo "deploy=$ACT"; echo "state=$STATE"; echo "mode=$MODE"
  echo "r41=$R41"; echo "r42=$R42"; echo "r43=$R43"; echo "r44=$R44"; echo "r45=$R45"
  echo "e42=$E42"; echo "e43=$E43"; echo "e44=$E44"; echo "e45=$E45"; echo "err=$ERR"; } > "$RESFILE"

# A NOT-SCORED 4.5 is not a pass: the example reached the platform but was never seen doing its
# job, which is a PARTIAL, and the report has to say what would close it.
if [[ "$R41 $R42 $R43 $R44 $R45" == *FAIL* || -n "$ERR" ]]; then
  echo "  VERDICT: NOT-PASS - see $RESFILE"; exit 1
elif [[ "$R45" == "NOT-SCORED" ]]; then
  echo "  VERDICT: PARTIAL - 4.5 unproven ($MODE) - see $RESFILE"; exit 2
else
  echo "  VERDICT: PASS"; exit 0
fi
