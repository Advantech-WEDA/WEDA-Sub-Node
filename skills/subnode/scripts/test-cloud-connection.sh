#!/bin/bash
# Test WedaNode (NATS) connectivity using pre-compiled NatsCheck tool
# Usage: test-cloud-connection.sh <nats-url> [--user <user> --pass <pass>]
# Returns: exit 0 = connected, exit 1 = failed
#
# Auto-selects binary for current OS/arch from pre-built binaries.
#
# Examples:
#   ./test-cloud-connection.sh nats://localhost:4222
#   ./test-cloud-connection.sh nats://10.0.0.1:4222 --user admin --pass secret

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR/../tools"

# Detect RID
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
    Darwin)
        case "$ARCH" in
            arm64)  RID="osx-arm64" ;;
            x86_64) RID="osx-x64" ;;
            *)      echo "FAIL: Unsupported macOS arch: $ARCH"; exit 1 ;;
        esac
        ;;
    Linux)
        case "$ARCH" in
            x86_64)  RID="linux-x64" ;;
            aarch64) RID="linux-arm64" ;;
            *)       echo "FAIL: Unsupported Linux arch: $ARCH"; exit 1 ;;
        esac
        ;;
    MINGW*|MSYS*|CYGWIN*)
        RID="win-x64"
        ;;
    *)
        echo "FAIL: Unsupported OS: $OS"; exit 1 ;;
esac

# Select binary
TOOL="$TOOLS_DIR/$RID/NatsCheck"
[ "$RID" = "win-x64" ] && TOOL="$TOOL.exe"

if [ ! -f "$TOOL" ]; then
    echo "FAIL: NatsCheck binary not found for $RID"
    echo "Expected: $TOOL"
    echo ""
    echo "Available platforms:"
    ls -d "$TOOLS_DIR"/*/ 2>/dev/null | xargs -I{} basename {} | sed 's/^/  /'
    exit 1
fi

exec "$TOOL" "$@"
