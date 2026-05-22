---
title: "Image Sensor Example"
description: "MQTT image ingestion with chunked telemetry and a browser-based receiver viewer"
version: "0.0.1"
author: "Rain Hu"
date: "2026-05-22"
lang: "en"
---

# Image Sensor Example

Demo of receiving binary image data over MQTT, chunking it through the SubNode transform pipeline, publishing it to WedaNode (NATS), and re-assembling it in a browser viewer.

Three moving parts:

1. **MqttImageSimulator** — publishes embedded MNIST PNGs to MQTT every 30s.
2. **ImageSensorDevice** — `MqttImageDevice` subclass; subscribes, chunks (128 chars), reports as telemetry.
3. **Receiver** — standalone web app; subscribes on NATS, reassembles, serves a live viewer at <http://localhost:5100/>.

## 1. Configure WedaNode in `docker-compose.yml`

All WedaNode settings are exposed as environment variables with `${VAR:-default}` fallbacks. The fastest path is to copy the provided sample:

```bash
cp .env.sample .env
# then edit .env to point at your WedaNode
```

See [.env.sample](.env.sample) for the full list of supported variables.

Other auth strategies use different vars (all already plumbed into compose):

| Strategy       | Required vars                                                        |
|----------------|----------------------------------------------------------------------|
| `None`         | —                                                                    |
| `UserPassword` | `WEDA_NODE_USERNAME`, `WEDA_NODE_PASSWORD`                           |
| `Token`        | `WEDA_NODE_TOKEN`                                                    |
| `CredFile`     | `WEDA_NODE_CRED_FILE` (path inside the container)                    |
| `TlsCert`      | `WEDA_NODE_TLS_CERT_PATH`, `WEDA_NODE_TLS_KEY_PATH`, `WEDA_NODE_TLS_CA_PATH` |

For local dev, `systemcfg.json` is mounted read-only and used as a fallback — env vars always win.

## 2. Cross-platform build (publish a prebuilt image)

The compose file references a prebuilt image (`harbor.arfa.wise-paas.com/edge-coa/image-sensor:latest` by default), so devices just pull — no per-host build. Use [`build.sh`](build.sh) to produce the multi-arch image with `docker buildx`:

```bash
# One-time: log in to Harbor so `--push` is authorized
docker login harbor.arfa.wise-paas.com

# Build & push :latest for linux/amd64 + linux/arm64 (default)
./build.sh

# Custom tag
IMAGE_TAG=v1.0.0 ./build.sh

# Single-arch local build (no push) — handy for fast iteration
PUSH=false PLATFORMS=linux/arm64 ./build.sh
```

The script creates a `docker-container` buildx builder on first run (the default `docker` driver can't produce multi-arch manifests) and runs `docker buildx build --platform linux/amd64,linux/arm64 --push` against the repo root. Override `IMAGE_REPO`, `IMAGE_TAG`, `PLATFORMS`, or `PUSH` via env vars.

The [Dockerfile](Dockerfile) uses `--platform=$BUILDPLATFORM` and switches the publish RID on `TARGETARCH`, so the same Dockerfile handles every target.

To run without Docker, .NET 10 is cross-platform out of the box:

```bash
cd examples/image-sensor
dotnet run                                            # current OS/arch
dotnet publish -c Release -r linux-arm64 --self-contained true   # ARM64
dotnet publish -c Release -r osx-arm64   --self-contained true   # Apple Silicon
dotnet publish -c Release -r win-x64     --self-contained true   # Windows x64
```

## 3. One-line start

```bash
# If the Harbor project is private, log in first (one-time per host)
docker login harbor.arfa.wise-paas.com

cp .env.sample .env   # first time only — edit to point at your WedaNode
docker compose up -d
```

`pull_policy: always` ensures the latest pushed image is fetched. Override the image via `IMAGE_SENSOR_IMAGE` in `.env` if you want to pin a specific tag or point at a different registry.

This brings up Mosquitto **and** the ImageSensor in the background. Tail logs with:

```bash
docker compose logs -f image-sensor
```

You should see `Published digit_X.png ...` (simulator) and `Image received: ...` (device handler) every 30s.

## 4. Verify with the Receiver

The Receiver is a small NATS subscriber + web viewer. It is **not** part of compose — run it on the host so you can open the page in a browser:

```bash
cd examples/image-sensor/Receiver
cp ../.env.sample .env   # first time only — reuses the same WedaNode credentials
dotnet run
```

The Receiver picks up `WEDA_NODE_URL` / `WEDA_NODE_USERNAME` / `WEDA_NODE_PASSWORD` from `.env` and overlays them on top of `appsettings.json`, so the same `.env` works for both the container and the local Receiver.

Then open <http://localhost:5100/> — the latest MNIST digit appears in the top panel and the last 10 images line up underneath.

> The Receiver reads the NATS telemetry subject from `../weda-data/subnode.registration.json`, which is created on the ImageSensor's first successful WedaNode registration. If you see `Registration file not found`, wait for the ImageSensor to finish registering and try again.

REST endpoints (for scripted checks):

| Method | Path                | Description                                    |
|--------|---------------------|------------------------------------------------|
| GET    | `/api/latest`       | Most recent reassembled image (JSON + base64)  |
| GET    | `/api/images`       | List of cached images                          |
| GET    | `/api/images/{id}`  | Download a specific image as PNG/JPEG          |

## 5. Graceful shutdown

Both the ImageSensor and the Receiver use the .NET generic host, which converts `Ctrl+C` / `SIGTERM` into a cooperative cancellation and runs every `IHostedService.StopAsync` before exiting. In this example that lets the MQTT simulator finish its in-flight publish and disconnect cleanly, and lets the device drain its NATS subscriptions.

Local runs:

```bash
# Foreground — Ctrl+C once, then wait for "MQTT Image Simulator stopped"
dotnet run
```

Docker:

```bash
# SIGTERM with a grace period (default 10s); upgrade if drains take longer
docker compose stop -t 30 image-sensor

# Or tear everything down
docker compose down
```

If you need a longer drain window by default, add to the `image-sensor` service in `docker-compose.yml`:

```yaml
init: true                # tini as PID 1 — ensures SIGTERM reaches the .NET process
stop_signal: SIGTERM
stop_grace_period: 30s    # bump if your WedaNode unsubscribe takes longer
```

Avoid `docker compose kill` (SIGKILL) unless the container is wedged — it skips the shutdown hooks entirely.
