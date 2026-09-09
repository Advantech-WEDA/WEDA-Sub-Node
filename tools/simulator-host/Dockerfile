# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY . .

RUN dotnet restore tools/simulator-host/Weda.SubNode.SimulatorHost.csproj

RUN set -e; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

RUN RID=$(cat /rid.txt) && \
    dotnet publish tools/simulator-host/Weda.SubNode.SimulatorHost.csproj \
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

# Default simulator profile (Modbus TCP on :5020), so the image runs standalone when deployed
# by container-management. Override by bind-mounting or by SIMULATOR__ env vars.
COPY tools/simulator-host/appsettings.json /app/appsettings.json

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["/app/Weda.SubNode.SimulatorHost"]
