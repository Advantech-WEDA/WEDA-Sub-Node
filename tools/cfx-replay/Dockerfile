# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY . .

WORKDIR /src/tools/cfx-replay
RUN dotnet restore

RUN set -e; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

RUN RID=$(cat /rid.txt) && \
    dotnet publish CfxReplay.csproj \
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

# The captured payloads double as the replay corpus and as the parser's test fixtures; baked in so
# the image is self-contained, with CFX_PAYLOAD_DIR available to point at a mounted set instead.
COPY tests/Weda.SubNode.Core.Tests/Protocols/Cfx/Fixtures/ ./payloads/

ENV CFX_PAYLOAD_DIR=/app/payloads

ENTRYPOINT ["/app/CfxReplay"]
