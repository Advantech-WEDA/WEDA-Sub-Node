# syntax=docker/dockerfile:1.6
#
# WEDA System-Agent DTDL validator -- .NET 8 port.
#
# Build (from this directory):
#   docker build -t weda-dtdl-validate-dotnet .
#
# Run (mount docs/Metrics/ as /work):
#   docker run --rm -v "$(pwd)/..:/work:ro" weda-dtdl-validate-dotnet
#
# Run a specific mode:
#   docker run --rm -v "$(pwd)/..:/work:ro" weda-dtdl-validate-dotnet dtdl
#   docker run --rm -v "$(pwd)/..:/work:ro" weda-dtdl-validate-dotnet samples
#
# Image size is ~210 MB on top of the trimmed .NET runtime image. DTDL files
# and sample fixtures are NEVER baked in -- bind-mount them at runtime.

# --- Stage 1: restore + publish ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore separately so package-cache layer is reused across edits.
COPY ValidateDtdl.csproj ./
RUN dotnet restore --runtime linux-x64

COPY . ./
RUN dotnet publish ValidateDtdl.csproj \
      -c Release \
      --runtime linux-x64 \
      --self-contained false \
      --no-restore \
      -o /app

# --- Stage 2: minimal runtime image ---
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Run as a non-root user for safety.
RUN useradd --create-home --uid 1000 validator
USER validator

COPY --chown=validator:validator --from=build /app/ ./

# Validators read DTDLs / samples relative to this path.
ENV DTDL_BASE=/work

# Bind-mount your docs/Metrics/ at /work when running.
WORKDIR /work

# Default mode = "both". Override by appending 'dtdl' or 'samples' to docker run.
ENTRYPOINT ["dotnet", "/app/validate-dtdl.dll"]
