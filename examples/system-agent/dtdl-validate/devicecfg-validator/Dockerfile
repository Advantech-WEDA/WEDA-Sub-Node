# syntax=docker/dockerfile:1.6
#
# devicecfg-validator -- standalone .NET 8 validator for devicecfg.json payloads.
#
# Build (from this directory):
#   docker build -t weda-devicecfg-validator .
#
# Run (mount docs/Metrics/ as /work):
#   docker run --rm -v "$(pwd)/../../docs/Metrics:/work:ro" weda-devicecfg-validator
#   docker run --rm -v "$(pwd)/../../docs/Metrics:/work:ro" weda-devicecfg-validator \
#     devicecfg/samples/invalid.json
#
# No NuGet packages -- the validator uses only System.Text.Json from the
# .NET 8 framework. DTDL schemas and payloads are bind-mounted at runtime.

# --- Stage 1: restore + publish ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY devicecfg-validator.csproj ./
RUN dotnet restore --runtime linux-x64

COPY . ./
RUN dotnet publish devicecfg-validator.csproj \
      -c Release \
      --runtime linux-x64 \
      --self-contained false \
      --no-restore \
      -o /app

# --- Stage 2: minimal runtime image ---
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

RUN useradd --create-home --uid 1000 validator
USER validator

COPY --chown=validator:validator --from=build /app/ ./

# Schemas / payloads are read relative to this path; bind-mount docs/Metrics here.
ENV DTDL_BASE=/work
WORKDIR /work

ENTRYPOINT ["dotnet", "/app/devicecfg-validator.dll"]
