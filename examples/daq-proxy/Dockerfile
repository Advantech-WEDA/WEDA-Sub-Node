# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH
ARG TARGETPLATFORM

WORKDIR /src
COPY . .

WORKDIR /src/examples/daq-proxy
RUN dotnet restore

RUN set -e; \
    echo "Building on AMD64, targeting: TARGETPLATFORM=${TARGETPLATFORM}, TARGETARCH=${TARGETARCH}"; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    echo "Target RID: $(cat /rid.txt)"

RUN RID=$(cat /rid.txt) && \
    dotnet publish DaqProxy.csproj \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=false \
      -p:PublishReadyToRun=false \
      -p:DebugType=none \
      -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final

WORKDIR /app
COPY --from=build /app/publish/ ./

RUN chmod +x daq-feature-proxy

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["/app/daq-feature-proxy"]
