# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY . .

WORKDIR /src/examples/mqtt-image-chunked
RUN dotnet restore

RUN set -e; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

RUN RID=$(cat /rid.txt) && \
    dotnet publish MqttImageChunked.csproj \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=false \
      -p:PublishReadyToRun=false \
      -p:DebugType=none \
      -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS final

WORKDIR /app
COPY --from=build /app/publish/ ./

# Default configuration, so the image runs standalone when deployed by container-management
# (no local files to bind-mount there). docker-compose bind-mounts override these read-only,
# and any value can be overridden at run time by its DeviceConfig__/SystemConfig__ env var.
COPY examples/mqtt-image-chunked/devicecfg.json /app/devicecfg.json
COPY examples/mqtt-image-chunked/systemcfg.json /app/systemcfg.json
COPY examples/mqtt-image-chunked/customcfg.json /app/customcfg.json
COPY examples/mqtt-image-chunked/appsettings.json /app/appsettings.json

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["/app/MqttImageChunked"]
