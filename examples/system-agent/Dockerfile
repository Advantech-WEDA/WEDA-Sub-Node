# syntax=docker/dockerfile:1.6

# Build stage: FORCE AMD64 for build to avoid QEMU issues
# The SDK will cross-compile to target architecture
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Declare TARGETARCH (buildx provides this based on --platform flag)
ARG TARGETARCH
ARG TARGETPLATFORM

# Create app directory for build context
WORKDIR /src

# Copy source (note: build context should be repository root)
COPY . .

# Restore dependencies for the system-agent project
WORKDIR /src/examples/system-agent
RUN dotnet restore

# Map TARGETARCH to .NET RID for cross-compilation
RUN set -e; \
    echo "Building on AMD64, targeting: TARGETPLATFORM=${TARGETPLATFORM}, TARGETARCH=${TARGETARCH}"; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    echo "Target RID: $(cat /rid.txt)"

# Cross-compile to target RID from AMD64 host
RUN RID=$(cat /rid.txt) && \
    echo "Cross-compiling from AMD64 to $RID..." && \
    dotnet publish SystemAgentDevice.csproj \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=false \
      -p:PublishReadyToRun=false \
      -p:DebugType=none \
      -o /app/publish

# Runtime stage: Use slim runtime-deps for self-contained apps
# runtime-deps is minimal, designed for self-contained .NET apps
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-bookworm-slim

# Declare TARGETARCH after FROM (buildx automatically provides this)
ARG TARGETARCH

# Update base image and install only required dependencies
# Using --no-install-recommends to minimize image size
# Explicitly install patched zlib1g version to fix CVE-2023-45853
# CVE-2023-45853 is fixed in zlib 1.3 and later, or 1.2.13.1-debian patched versions
RUN apt-get update && \
    apt-get upgrade -y && \
    echo "Checking available zlib1g versions:" && \
    apt-cache policy zlib1g && \
    apt-get install -y --no-install-recommends \
    ca-certificates && \
    (apt-get install -y --no-install-recommends 'zlib1g>=1:1.2.13' || \
     apt-get install -y --no-install-recommends zlib1g) && \
    echo "Installed zlib1g version:" && \
    dpkg -l | grep zlib1g && \
    apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Create app directory
WORKDIR /app

# Copy published files for the correct architecture
COPY --from=build /app/publish/ ./

# Ensure the main executable has execute permission
RUN chmod +x SystemAgentDevice

# Optional: reduce globalization dependencies
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Run as root (required for SUSI Driver hardware access)
USER root

# Entry point
ENTRYPOINT ["./SystemAgentDevice"]
