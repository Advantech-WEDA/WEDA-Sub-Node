#!/bin/bash
# Build NatsCheck for the current platform
# Usage: build-tools.sh <repo-root>
#
# Auto-detects OS/arch and publishes the binary to ~/.claude/skills/subnode/tools/

set -euo pipefail

REPO_ROOT="${1:?Usage: build-tools.sh <repo-root>}"
PROJECT="$REPO_ROOT/tools/nats-check/NatsCheck.csproj"
OUTPUT_DIR="$(cd "$(dirname "$0")/.." && pwd)/tools"

if [ ! -f "$PROJECT" ]; then
    echo "FAIL: NatsCheck project not found at $PROJECT"
    exit 1
fi

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
            armv7l)  RID="linux-arm" ;;
            *)       echo "FAIL: Unsupported Linux arch: $ARCH"; exit 1 ;;
        esac
        ;;
    MINGW*|MSYS*|CYGWIN*|Windows_NT)
        case "$ARCH" in
            x86_64|AMD64)  RID="win-x64" ;;
            aarch64|ARM64) RID="win-arm64" ;;
            *)             echo "FAIL: Unsupported Windows arch: $ARCH"; exit 1 ;;
        esac
        ;;
    *)
        echo "FAIL: Unsupported OS: $OS"
        exit 1
        ;;
esac

echo "Building NatsCheck for $RID..."
mkdir -p "$OUTPUT_DIR"

dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:DebugType=none \
    -o "$OUTPUT_DIR" \
    --nologo -v q

echo "OK: $OUTPUT_DIR/NatsCheck ($RID)"
