#!/usr/bin/env bash
# Build the system-agent Windows release package inside a container.
#
# Cross-compiles from the Linux .NET SDK image — no Windows host, no local
# .NET SDK, no Windows container required. Emits a versioned .zip plus its
# SHA-256 sum, exported straight to disk (nothing is loaded into the image
# store). Container counterpart of the Linux image built by
# examples/system-agent/azure-pipelines.yml.
#
# Usage:
#   examples/system-agent/scripts/build-windows-package.sh
#   examples/system-agent/scripts/build-windows-package.sh --version 1.2.0
#   examples/system-agent/scripts/build-windows-package.sh --rid win-arm64
#   examples/system-agent/scripts/build-windows-package.sh --output /tmp/drop
#
# Options:
#   --version VER   Package version, stamped into the assembly and the file name.
#                   Default: the <Version> in common.props.
#   --rid RID       Target runtime identifier (default: win-x64).
#   --output DIR    Directory to export into (default: <repo>/artifacts).
#   --sdk-image REF .NET SDK image used for the build stage
#                   (default: mcr.microsoft.com/dotnet/sdk:10.0). Pin by digest
#                   for reproducible releases — the SDK image's runtime pack
#                   drifts between tag refreshes.
#   --keep-xml      Keep the SDK XML doc files (~1 MB, not needed at runtime).
#   --no-cache      Build without the buildx layer cache.
#   --dry-run, -n   Print the resolved plan and the docker command; build nothing.
#   --help, -h      Show this help.
#
# Notes:
#   The build context is the repository root: nuget.config and the bundled
#   ./nuget feed (Weda.Dtdl, CFX.CFXSDK) live there and restore fails without them.
#
#   The resulting binary runs on Windows, but system-agent is Linux-first —
#   /proc-backed metrics (CPU utilisation, load averages, disk I/O, GPU, detailed
#   RAM) come back empty, system.reboot/system.shutdown are rejected, and hardware
#   access needs the Advantech SUSI driver installed on the target.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PROJECT="examples/system-agent/SystemAgentDevice.csproj"

RID="win-x64"
VERSION=""
OUTPUT_DIR=""
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
KEEP_XML=0
NO_CACHE=0
DRY_RUN=0

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

die() { echo -e "${RED}Error: $*${NC}" >&2; exit 1; }

usage() {
    # Print the leading comment block (everything after the shebang, up to the
    # first non-comment line) as the usage text.
    awk 'NR==1 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "${BASH_SOURCE[0]}"
    exit "${1:-0}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --version)    shift; [ $# -gt 0 ] || die "--version requires a value"; VERSION="$1" ;;
        --rid)        shift; [ $# -gt 0 ] || die "--rid requires a value"; RID="$1" ;;
        --output)     shift; [ $# -gt 0 ] || die "--output requires a directory"; OUTPUT_DIR="$1" ;;
        --sdk-image)  shift; [ $# -gt 0 ] || die "--sdk-image requires an image reference"; SDK_IMAGE="$1" ;;
        --keep-xml)   KEEP_XML=1 ;;
        --no-cache)   NO_CACHE=1 ;;
        --dry-run|-n) DRY_RUN=1 ;;
        --help|-h)    usage 0 ;;
        *)            die "Unknown option: $1 (try --help)" ;;
    esac
    shift
done

case "$RID" in
    win-x64|win-arm64) ;;
    *) die "Unsupported RID: $RID (expected win-x64 or win-arm64)" ;;
esac

command -v docker > /dev/null 2>&1 || die "docker not found on PATH"
docker buildx version > /dev/null 2>&1 || die "docker buildx not available — install the buildx plugin"
docker info > /dev/null 2>&1 || die "cannot reach the Docker daemon (is it running, and are you in the docker group?)"

[ -f "$REPO_ROOT/$PROJECT" ] || die "project not found: $REPO_ROOT/$PROJECT"

if [ -z "$VERSION" ]; then
    VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO_ROOT/common.props" | head -1)"
    [ -n "$VERSION" ] || die "could not read <Version> from common.props — pass --version explicitly"
fi

OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/artifacts}"
PACKAGE="system-agent-$VERSION-$RID"

BUILD_ARGS=(
    --build-arg "SDK_IMAGE=$SDK_IMAGE"
    --build-arg "VERSION=$VERSION"
    --build-arg "RID=$RID"
    --build-arg "PROJECT=$PROJECT"
    --build-arg "KEEP_XML=$KEEP_XML"
)
[ "$NO_CACHE" -eq 1 ] && BUILD_ARGS+=(--no-cache)

echo -e "${BLUE}system-agent Windows package${NC}"
echo "  project    : $PROJECT"
echo "  version    : $VERSION"
echo "  rid        : $RID"
echo "  sdk image  : $SDK_IMAGE"
echo "  context    : $REPO_ROOT"
echo "  output     : $OUTPUT_DIR/$PACKAGE.zip"
echo

if [ "$DRY_RUN" -eq 1 ]; then
    echo -e "${YELLOW}Dry run — would execute:${NC}"
    echo "  docker buildx build -f - ${BUILD_ARGS[*]} --target export \\"
    echo "    --output type=local,dest=$OUTPUT_DIR $REPO_ROOT"
    exit 0
fi

mkdir -p "$OUTPUT_DIR" || die "cannot create output directory: $OUTPUT_DIR"

# Dockerfile on stdin: the build stage cross-publishes self-contained, the pack
# stage zips it (the SDK image has no zip), and the scratch export stage writes
# the zip + checksum to the host without producing an image.
docker buildx build \
    -f - \
    "${BUILD_ARGS[@]}" \
    --target export \
    --output "type=local,dest=$OUTPUT_DIR" \
    "$REPO_ROOT" <<'DOCKERFILE' || die "container build failed for $PACKAGE"
# syntax=docker/dockerfile:1.6
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0

FROM --platform=$BUILDPLATFORM ${SDK_IMAGE} AS build
ARG VERSION
ARG RID
ARG PROJECT
ARG KEEP_XML
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    set -eu; \
    PKG="system-agent-$VERSION-$RID"; \
    dotnet publish "$PROJECT" \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=false \
      -p:DebugType=none \
      -p:Version="$VERSION" \
      -o "/stage/$PKG"; \
    if [ "$KEEP_XML" != "1" ]; then rm -f "/stage/$PKG"/*.xml; fi; \
    test -f "/stage/$PKG/SystemAgentDevice.exe"

FROM alpine:3.20 AS pack
ARG VERSION
ARG RID
RUN apk add --no-cache zip
COPY --from=build /stage /stage
RUN set -eu; \
    PKG="system-agent-$VERSION-$RID"; \
    cd /stage; \
    zip -qr9 "$PKG.zip" "$PKG"; \
    sha256sum "$PKG.zip" > "$PKG.zip.sha256"; \
    rm -rf "$PKG"

FROM scratch AS export
COPY --from=pack /stage/ /
DOCKERFILE

ZIP="$OUTPUT_DIR/$PACKAGE.zip"
[ -f "$ZIP" ] || die "build reported success but $ZIP is missing"

echo
echo -e "${GREEN}Package built${NC}"
echo "  $ZIP  ($(du -h "$ZIP" | cut -f1))"
echo "  $(cut -d' ' -f1 < "$OUTPUT_DIR/$PACKAGE.zip.sha256")"
