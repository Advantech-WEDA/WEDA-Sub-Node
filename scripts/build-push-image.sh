#!/usr/bin/env bash
# Build and push a SubNode example (or tool) image to Harbor.
#
# The build context is ALWAYS the repository root — every example .csproj references
# ../../src/Weda.SubNode.*, so building from inside the example directory cannot work.
# This script enforces that, so callers never have to remember it.
#
# Usage:
#   scripts/build-push-image.sh <example-name>                 # multi-arch, push
#   PLATFORMS=linux/arm64 scripts/build-push-image.sh opcua-basic
#   PUSH=false PLATFORMS=linux/amd64 scripts/build-push-image.sh modbus-wise4012   # local --load
#   PATH_PREFIX=tools scripts/build-push-image.sh simulator-host                   # a tool, not an example
#
# Env overrides: IMAGE_REPO, IMAGE_TAG, PLATFORMS, PUSH, PATH_PREFIX, BUILDER_NAME.

set -euo pipefail

NAME="${1:?usage: $0 <example-name>}"
PATH_PREFIX="${PATH_PREFIX:-examples}"
IMAGE_REPO="${IMAGE_REPO:-harbor.arfa.wise-paas.com/edge-coa/${NAME}}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
PLATFORMS="${PLATFORMS:-linux/amd64,linux/arm64}"
PUSH="${PUSH:-true}"
BUILDER_NAME="${BUILDER_NAME:-weda-multiarch}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOCKERFILE="${REPO_ROOT}/${PATH_PREFIX}/${NAME}/Dockerfile"

[[ -f "${DOCKERFILE}" ]] || { echo "ERROR: no Dockerfile at ${PATH_PREFIX}/${NAME}/" >&2; exit 1; }

# A docker-container builder is required for multi-arch; the default `docker` driver cannot
# produce a manifest list. network=host lets BuildKit use the host's DNS/proxy to reach mcr.
if ! docker buildx inspect "${BUILDER_NAME}" >/dev/null 2>&1; then
  echo ">> creating buildx builder '${BUILDER_NAME}'"
  docker buildx create --name "${BUILDER_NAME}" --driver docker-container \
    --driver-opt network=host --bootstrap >/dev/null
fi
docker buildx use "${BUILDER_NAME}"

# Cross-arch emulation, needed once per host for arm64 builds on an amd64 machine.
if [[ "${PLATFORMS}" == *arm* ]]; then
  docker run --privileged --rm tonistiigi/binfmt --install arm64 >/dev/null 2>&1 || true
fi

OUTPUT="--push"
if [[ "${PUSH}" != "true" ]]; then
  [[ "${PLATFORMS}" == *,* ]] && { echo "ERROR: PUSH=false needs a single --platform" >&2; exit 1; }
  OUTPUT="--load"
fi

echo ">> ${IMAGE_REPO}:${IMAGE_TAG}  [${PLATFORMS}]  ${OUTPUT}"
docker buildx build \
  --platform "${PLATFORMS}" \
  -f "${DOCKERFILE}" \
  -t "${IMAGE_REPO}:${IMAGE_TAG}" \
  ${OUTPUT} \
  "${REPO_ROOT}"

if [[ "${PUSH}" == "true" ]]; then
  echo ">> manifest:"
  docker buildx imagetools inspect "${IMAGE_REPO}:${IMAGE_TAG}"
fi
