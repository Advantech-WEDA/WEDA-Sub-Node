# syntax=docker/dockerfile:1.6

# Build stage: FORCE AMD64 for build to avoid QEMU issues
# The SDK will cross-compile to target architecture
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Declare TARGETARCH (buildx provides this based on --platform flag)
ARG TARGETARCH
ARG TARGETPLATFORM

# Create app directory for build context
WORKDIR /src

# Copy source (note: build context should be repository root)
COPY . .

# Restore dependencies for the daq-collector project
WORKDIR /src/examples/daq-collector
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
    dotnet publish DaqCollector.csproj \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=false \
      -p:PublishReadyToRun=false \
      -p:DebugType=none \
      -o /app/publish

# Runtime stage: Use slim runtime-deps for self-contained apps
# runtime-deps is minimal, designed for self-contained .NET apps
# FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final

# Declare TARGETARCH after FROM (buildx automatically provides this)
ARG TARGETARCH

# Create app directory
WORKDIR /app

# Copy published files for the correct architecture
COPY --from=build /app/publish/ ./

# Copy entrypoint script
COPY examples/daq-collector/entrypoint.sh /app/entrypoint.sh

# Copy default configuration files (can be overridden via docker-compose.yml volume mounts)
COPY examples/daq-collector/devicecfg.json /app/devicecfg.json
# The delivered image carries the WedaNode password, so a customer needs only the image and
# the compose file. scripts/build-push-image.sh passes it from the environment; built without
# it, the image ships the empty password the repository holds, and the compose can still
# supply one. Either way the value is readable inside the image - that is the trade for one
# image that runs as delivered. A customer who needs their own password builds their own.
ARG WEDA_NATS_PASSWORD=""
COPY examples/daq-collector/systemcfg.json /app/systemcfg.json
RUN if [ -n "$WEDA_NATS_PASSWORD" ]; then \
      sed -i "s|\"Password\": \"\"|\"Password\": \"$WEDA_NATS_PASSWORD\"|" /app/systemcfg.json; \
    fi
COPY examples/daq-collector/customcfg.json /app/customcfg.json
COPY examples/daq-collector/appsettings.json /app/appsettings.json

# Ensure executables have execute permission
RUN chmod +x DaqCollector /app/entrypoint.sh

# Single layer for all runtime setup including CVE fix.
# Take the archive candidate and ASSERT the CVE floor, rather than pinning an exact
# version. Ubuntu drops superseded point releases, so `=2.4.4-2ubuntu17.4` broke the
# build the moment 17.6 landed. apt has no `>=` operator for install, so the floor is
# enforced explicitly: the build fails if the installed gpgv is older than the fix.
RUN apt-get update && \
    apt-get install -y --no-install-recommends gpgv && \
    dpkg --compare-versions "$(dpkg-query -W -f='${Version}' gpgv)" ge 2.4.4-2ubuntu17.4 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Optional: reduce globalization dependencies
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Run as root (required for SUSI Driver hardware access)
USER root

# Entry point - uses initialization script to copy libraries before starting the application
ENTRYPOINT ["/app/entrypoint.sh"]
