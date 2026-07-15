#!/usr/bin/env bash
# Dump each example's emitted DTDL + device-cap (capabilities upload DTO) as JSON.
#
# Usage:
#   scripts/dump-capabilities.sh                 # all examples → artifacts/capabilities/<name>.json
#   scripts/dump-capabilities.sh <example-name>  # one example → artifacts/capabilities/<name>.json
#   scripts/dump-capabilities.sh --print <name>  # one example, print to stdout
#
# No NATS, no hardware, no device instantiation — pure config-to-DTO walk through
# the real SDK pipeline (DtdlGenerator + ToConfigurationDto).

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TOOL_PROJ="$REPO_ROOT/tools/Weda.SubNode.CapabilityDump/Weda.SubNode.CapabilityDump.csproj"
TOOL_DLL="$REPO_ROOT/tools/Weda.SubNode.CapabilityDump/bin/Release/net10.0/capdump.dll"
OUT_DIR="$REPO_ROOT/artifacts/capabilities"

PRINT_ONLY=0
if [ "${1:-}" = "--print" ] || [ "${1:-}" = "-p" ]; then
    PRINT_ONLY=1
    shift
fi

mkdir -p "$OUT_DIR"

echo "Building capdump..."
dotnet build "$TOOL_PROJ" -c Release --nologo > /dev/null
[ -f "$TOOL_DLL" ] || { echo "Build succeeded but $TOOL_DLL not found" >&2; exit 1; }

dump_one() {
    local example_dir="$1"
    local name; name=$(basename "$example_dir")
    if [ ! -f "$example_dir/devicecfg.json" ]; then
        echo "  -- $name: no devicecfg.json, skip"
        return
    fi
    if [ "$PRINT_ONLY" = "1" ]; then
        dotnet "$TOOL_DLL" "$example_dir"
    else
        local out="$OUT_DIR/$name.json"
        if dotnet "$TOOL_DLL" "$example_dir" "$out" 2>&1 | sed 's/^/    /'; then
            echo "  [OK]   $name -> $out"
        else
            echo "  [FAIL] $name"
        fi
    fi
}

if [ -n "${1:-}" ]; then
    EX="$REPO_ROOT/examples/$1"
    if [ ! -d "$EX" ]; then
        echo "No such example: $1" >&2
        echo "Available:" >&2
        ls "$REPO_ROOT/examples/" >&2
        exit 1
    fi
    dump_one "$EX"
else
    for d in "$REPO_ROOT"/examples/*/; do
        dump_one "$d"
    done
    echo
    echo "Done. Outputs in: $OUT_DIR/"
fi
