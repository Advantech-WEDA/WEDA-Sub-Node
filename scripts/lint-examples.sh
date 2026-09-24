#!/usr/bin/env bash
# Check the examples and the dotnet new templates against the rules that deploying them to a
# device established. Every rule here is a defect that reached a device at least once.
#
# Needs nothing but a checkout and the docker CLI - no daemon beyond compose parsing, no device,
# no registry. Runs in seconds, so it belongs on every pull request.
#
# Usage: scripts/lint-examples.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

FAIL=0
fail() { printf '  FAIL  %s\n' "$*"; FAIL=1; }
rule() { printf '\n== %s\n' "$*"; }

# system-agent ships as a product component: it does not bake its configuration and reads the
# device's own copy under /opt/Advantech, so the rules about baked config do not apply to it.
NO_BAKED_CONFIG="system-agent"

EXAMPLES=$(find examples -mindepth 1 -maxdepth 1 -type d -exec test -f {}/docker-compose.yml \; -print | sort)

# ---------------------------------------------------------------------------
rule "compose files parse"
# Every subcommand interpolates the file, so a compose that needs a variable to parse cannot even
# be stopped without it. Dummy values stand in for anything still required.
for f in examples/*/docker-compose*.yml templates/*/docker-compose.yml; do
  [[ -f "$f" ]] || continue
  out=$(CFX_BROKER_PASSWORD=x WEDA_NATS_PASSWORD=x docker compose -f "$f" config -q 2>&1)
  [[ $? -eq 0 ]] || fail "$f does not parse: $(head -1 <<<"$out")"
done

# ---------------------------------------------------------------------------
rule "no WedaNode outside the device it runs on"
# An example that defaults to a developer's machine works there and nowhere else, and baking the
# configuration into the image made that fatal rather than merely wrong.
for f in examples/*/systemcfg.json templates/*/systemcfg.json; do
  [[ -f "$f" ]] || continue
  url=$(grep -o '"Url"[[:space:]]*:[[:space:]]*"[^"]*"' "$f" | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  [[ -z "$url" || "$url" == "127.0.0.1:4224" ]] || fail "$f points at $url, not 127.0.0.1:4224"
done

# ---------------------------------------------------------------------------
rule "no placeholder credentials, no password in the repository"
for f in $(grep -rl "REPLACE_WITH_" examples templates 2>/dev/null); do
  fail "$f still ships a REPLACE_WITH_ placeholder"
done
# The value goes in at build time (ARG WEDA_NATS_PASSWORD), so the checked-in file holds none.
for f in examples/*/systemcfg.json templates/*/systemcfg.json; do
  [[ -f "$f" ]] || continue
  grep -q '"Password"' "$f" || continue
  grep -q '"Password"[[:space:]]*:[[:space:]]*""' "$f" || \
    fail "$f carries a non-empty password; it belongs in the build argument"
done

# ---------------------------------------------------------------------------
rule "the cloud target is configuration-driven"
# Hardcoding the mock cloud produces an example that can never reach a WedaNode, which looks
# healthy in every log until someone asks where the telemetry went.
for f in examples/*/Program.cs templates/*/Program.cs; do
  [[ -f "$f" ]] || continue
  # Comment lines mention it to say how to switch; only code counts.
  code=$(grep -vE '^[[:space:]]*//' "$f")
  if grep -qE 'UseMockCloud|Cloud\.Mock' <<<"$code" && ! grep -q 'useMockCloud' <<<"$code"; then
    fail "$f selects the mock cloud unconditionally"
  fi
done

# ---------------------------------------------------------------------------
rule "configuration is baked into the image"
# A device has no checkout, so an image that expects its JSON to be mounted starts with none.
for d in $EXAMPLES templates/subnode templates/wedabuilder; do
  name=$(basename "$d")
  [[ "$name" == "$NO_BAKED_CONFIG" ]] && continue
  [[ -f "$d/Dockerfile" ]] || continue
  for cfg in devicecfg systemcfg customcfg appsettings; do
    grep -q "COPY .*${cfg}.json /app/${cfg}.json" "$d/Dockerfile" || \
      fail "$d/Dockerfile never copies ${cfg}.json into /app"
  done
done

# ---------------------------------------------------------------------------
rule "no exact version pin on an apt package"
# Ubuntu drops superseded point releases, so `pkg=1.2.3` stops resolving without anything in the
# repository changing, and the build fails outright. Assert a floor instead.
for f in examples/*/Dockerfile templates/*/Dockerfile; do
  [[ -f "$f" ]] || continue
  line=$(grep -nE 'apt-get install[^&|]*[a-z0-9.+-]+=[0-9]' "$f" | head -1)
  [[ -z "$line" ]] || fail "$f pins an exact apt version: ${line}"
done

# ---------------------------------------------------------------------------
rule "host networking is not mixed with a bridge-networked sibling"
# The agent then programs the project network's veth into the host namespace and the host-mode
# container is created but never starts, emitting no logs at all.
# -sim composes pair an example with a simulator on a development machine and are never deployed;
# the failure this rule guards is the edge agent programming a veth into the host namespace.
for f in examples/*/docker-compose.yml examples/*/docker-compose.deploy.yml; do
  [[ -f "$f" ]] || continue
  if grep -q 'network_mode: host' "$f" && grep -qE '^\s+ports:' "$f"; then
    fail "$f mixes network_mode: host with a published port"
  fi
done

# ---------------------------------------------------------------------------
rule "the registration directory is persisted"
# Without it the accepted registration dies with the container and the SubNode collides with its
# own previous identity: the stack works exactly once.
for f in examples/*/docker-compose.yml examples/*/docker-compose.deploy.yml templates/*/docker-compose.yml; do
  [[ -f "$f" ]] || continue
  grep -q 'WEDA_DATA_PATH' "$f" || fail "$f does not persist /app/.weda through WEDA_DATA_PATH"
done

# ---------------------------------------------------------------------------
rule "the deployment compose is deployable"
for d in $EXAMPLES; do
  f="$d/docker-compose.deploy.yml"
  [[ -f "$f" ]] || { fail "$d has no docker-compose.deploy.yml"; continue; }
  grep -q '^\s*image: [^$]' "$f" || fail "$f has no literal image reference"
  grep -q 'container_name:' "$f" || fail "$f names no container"
  grep -q 'build:' "$f" && fail "$f still builds; a device does not compile"
  # An entry written =${VAR:-} expands to an empty string when nothing is set, and an empty
  # environment variable beats the JSON file - wiping the password the image was built with.
  grep -q 'SystemConfig__WedaNode__Password=' "$f" && \
    fail "$f assigns the password; use the bare key so it is passed through only when set"
done

# ---------------------------------------------------------------------------
rule "every example declares what it should be seen doing"
for d in $EXAMPLES; do
  [[ -f "$d/e2e-expect.json" ]] || fail "$d has no e2e-expect.json"
done

echo
if [[ $FAIL -eq 0 ]]; then echo "PASS - no rule broken"; else echo "FAILED - see above"; fi
exit $FAIL
