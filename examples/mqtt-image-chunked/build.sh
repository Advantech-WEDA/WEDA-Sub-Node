#!/usr/bin/env bash
# Multi-arch build & push for the ImageSensor example.
#
# Usage:
#   ./build.sh                              # build & push :latest for amd64+arm64
#   IMAGE_TAG=v1.0.0 ./build.sh             # custom tag
#   PLATFORMS=linux/arm64 ./build.sh        # single-arch
#   PUSH=false ./build.sh                   # local build (single platform only)

set -euo pipefail

IMAGE_REPO="${IMAGE_REPO:-harbor.arfa.wise-paas.com/edge-coa/image-sensor}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
PLATFORMS="${PLATFORMS:-linux/amd64,linux/arm64}"
PUSH="${PUSH:-true}"
BUILDER_NAME="${BUILDER_NAME:-image-sensor-builder}"

# Repo root = two levels up from this script.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Ensure a buildx builder with the docker-container driver exists (default `docker` driver cannot do multi-arch).
# `network=host` lets the BuildKit container share the host's network stack — required when the host needs
# corporate DNS / VPN / proxy to reach upstream registries (e.g. mcr.microsoft.com).
needs_recreate=false
if docker buildx inspect "${BUILDER_NAME}" >/dev/null 2>&1; then
  if ! docker inspect "buildx_buildkit_${BUILDER_NAME}0" --format '{{.HostConfig.NetworkMode}}' 2>/dev/null | grep -q '^host$'; then
    echo ">> Existing builder '${BUILDER_NAME}' is not on host network — recreating"
    needs_recreate=true
  fi
else
  needs_recreate=true
fi

if [[ "${needs_recreate}" == "true" ]]; then
  docker buildx rm "${BUILDER_NAME}" >/dev/null 2>&1 || true
  echo ">> Creating buildx builder '${BUILDER_NAME}' (driver=docker-container, network=host)"
  docker buildx create \
    --name "${BUILDER_NAME}" \
    --driver docker-container \
    --driver-opt network=host \
    --bootstrap >/dev/null
fi
docker buildx use "${BUILDER_NAME}"

OUTPUT_FLAG="--push"
if [[ "${PUSH}" != "true" ]]; then
  # --load only supports a single platform.
  if [[ "${PLATFORMS}" == *,* ]]; then
    echo "ERROR: PUSH=false requires a single platform (got '${PLATFORMS}')." >&2
    exit 1
  fi
  OUTPUT_FLAG="--load"
fi

echo ">> Building ${IMAGE_REPO}:${IMAGE_TAG} for ${PLATFORMS} (${OUTPUT_FLAG})"
docker buildx build \
  --platform "${PLATFORMS}" \
  --file "${SCRIPT_DIR}/Dockerfile" \
  --tag "${IMAGE_REPO}:${IMAGE_TAG}" \
  ${OUTPUT_FLAG} \
  "${REPO_ROOT}"
