# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY . .

# Where this project sits inside the build context. The .csproj references the SDK as
# ../../src/Weda.SubNode.*, so the project has to be two levels below the repository root -
# apps/<name> or examples/<name>. Override when it is not apps/.
ARG PROJECT_DIR=apps/WedaBuilder
WORKDIR /src/${PROJECT_DIR}
RUN dotnet restore

RUN set -e; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

RUN RID=$(cat /rid.txt) && \
    dotnet publish WedaBuilder.csproj \
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

# Bake the configuration so the image runs standalone: a device has no checkout to mount
# the four JSON files from. docker-compose.yml mounts them read-only for development, and
# any value can still be overridden at run time by its DeviceConfig__/SystemConfig__
# environment variable.
ARG PROJECT_DIR=apps/WedaBuilder
COPY ${PROJECT_DIR}/devicecfg.json /app/devicecfg.json
COPY ${PROJECT_DIR}/systemcfg.json /app/systemcfg.json
COPY ${PROJECT_DIR}/customcfg.json /app/customcfg.json
COPY ${PROJECT_DIR}/appsettings.json /app/appsettings.json

# An image built to be handed on can carry the WedaNode password; built without this, it
# ships the empty one above and the compose supplies the value instead.
ARG WEDA_NATS_PASSWORD=""
RUN if [ -n "$WEDA_NATS_PASSWORD" ]; then \
      sed -i "s|\"Password\": \"\"|\"Password\": \"$WEDA_NATS_PASSWORD\"|" /app/systemcfg.json; \
    fi

# Drop this when the device speaks OPC UA: the stack builds an X.509 application certificate
# at startup and the X.500 name handling needs the full ICU data, so under invariant
# globalization the server aborts with BadInternalError.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["/app/WedaBuilder"]
