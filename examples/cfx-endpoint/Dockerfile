# syntax=docker/dockerfile:1.6

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src
COPY . .

WORKDIR /src/examples/cfx-endpoint
RUN dotnet restore

RUN set -e; \
    case "${TARGETARCH}" in \
      amd64)  echo "linux-x64"   > /rid.txt ;; \
      arm64)  echo "linux-arm64" > /rid.txt ;; \
      arm)    echo "linux-arm"   > /rid.txt ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

RUN RID=$(cat /rid.txt) && \
    dotnet publish CfxEndpointExample.csproj \
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

# The CFX SDK's TimeSpan and enum converters need the full ICU data, so globalization stays enabled
# here rather than being switched to invariant mode as the scalar-protocol examples do.
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["/app/CfxEndpointExample"]
