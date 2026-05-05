#!/bin/bash
# Test WedaNode (NATS) connectivity using NatsCheck tool
# Usage: test-cloud-connection.sh <repo-root> <nats-url> [--user <user> --pass <pass>]
# Returns: exit 0 = connected, exit 1 = failed
#
# Auto-builds NatsCheck from source on first use, then caches the binary.
# Source: {repo-root}/tools/nats-check/
#
# Examples:
#   ./test-cloud-connection.sh ~/edge_subnode nats://localhost:4222
#   ./test-cloud-connection.sh ~/edge_subnode nats://10.0.0.1:4222 --user admin --pass secret

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR/../tools"

if [ $# -lt 2 ]; then
    echo "Usage: test-cloud-connection.sh <repo-root> <nats-url> [--user <user> --pass <pass>]"
    echo ""
    echo "Examples:"
    echo "  ./test-cloud-connection.sh ~/edge_subnode nats://localhost:4222"
    echo "  ./test-cloud-connection.sh ~/edge_subnode nats://10.0.0.1:4222 --user admin --pass secret"
    exit 1
fi

REPO_ROOT="$1"
shift

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
TOOL="$TOOLS_DIR/NatsCheck"
[ "$RID" = "win-x64" ] && TOOL="$TOOL.exe"

# Build on first use if binary doesn't exist
if [ ! -f "$TOOL" ]; then
    PROJECT="$REPO_ROOT/tools/nats-check/NatsCheck.csproj"
    if [ ! -f "$PROJECT" ]; then
        echo "FAIL: NatsCheck project not found at $PROJECT"
        echo "Make sure <repo-root> points to the edge_subnode repository."
        exit 1
    fi

    echo "Building NatsCheck for $RID (first-time setup)..."
    mkdir -p "$TOOLS_DIR"
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:DebugType=none \
        -o "$TOOLS_DIR" \
        --nologo -v q

    echo "NatsCheck built successfully."
    echo ""
fi

exec "$TOOL" "$@"
