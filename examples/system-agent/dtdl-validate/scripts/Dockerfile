# syntax=docker/dockerfile:1.6
#
# WEDA System-Agent DTDL validator.
#
# Build (from this directory):
#   docker build -t weda-dtdl-validate .
#
# Run (mount docs/Metrics/ as /work):
#   docker run --rm -v "$(pwd)/..:/work:ro" weda-dtdl-validate
#
# Run only one validator:
#   docker run --rm -v "$(pwd)/..:/work:ro" weda-dtdl-validate \
#       node /app/validate-dtdl.js
#
# The image bakes in the parser (~12 MB on top of node:20-alpine) and the
# two validator scripts (schema + config). The DTDL files and sample-config
# fixtures are NEVER baked in -- mount them at runtime so the same image
# evaluates any branch without rebuilds.

# --- Stage 1: install deps in a separate layer so script edits don't bust
# the npm cache ---
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund --loglevel=error

# --- Stage 2: minimal runtime image ---
FROM node:20-alpine
WORKDIR /app

# Run as non-root for safety; node base image ships a 'node' user (UID 1000).
USER node

COPY --chown=node:node --from=deps /app/node_modules ./node_modules
COPY --chown=node:node \
     validate-dtdl.js validate-configs.js \
     config-rules.js \
     ./

# Validators read DTDLs / samples-config relative to this path.
ENV DTDL_BASE=/work

# Bind-mount your docs/Metrics/ at /work when running.
WORKDIR /work

# Default: run both validators in sequence; non-zero exit if either fails.
CMD ["sh", "-c", "node /app/validate-dtdl.js && node /app/validate-configs.js"]
