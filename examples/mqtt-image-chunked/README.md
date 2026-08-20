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

## 1. Deployment prerequisites

Copy the following files/directories to the target machine (e.g. `/opt/image-sensor/`):

```
image-sensor/
├── docker-compose.yml          # service definitions
├── .env                        # copy from .env.sample, fill in WedaNode credentials
├── devicecfg.json              # sensor config (MQTT topic, chunk size)
├── appsettings.json            # logging settings
├── customcfg.json              # custom config (currently empty)
├── systemcfg.json              # system config (can be empty {})
└── mosquitto/
    └── config/
        └── mosquitto.conf      # MQTT broker config
```

All WedaNode connection settings, Record quota, and SubNode identity are controlled via `.env` (with sensible defaults in `docker-compose.yml`). The only file you **must** edit is `.env` — see section 3 for which fields to fill in.

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
cd examples/mqtt-image-chunked
dotnet run                                            # current OS/arch
dotnet publish -c Release -r linux-arm64 --self-contained true   # ARM64
dotnet publish -c Release -r osx-arm64   --self-contained true   # Apple Silicon
dotnet publish -c Release -r win-x64     --self-contained true   # Windows x64
```

## 3. Deploy and start

On the target machine, navigate to the deployment directory:

```bash
# One-time: log in to Harbor (private registry)
docker login harbor.arfa.wise-paas.com

# First time: create .env from sample
cp .env.sample .env
```

Edit `.env` — the required fields:

| Variable | Description | Example |
|----------|-------------|--------|
| `WEDA_NODE_URL` | WedaNode (NATS) address | `10.0.1.50:4224` |
| `WEDA_NODE_AUTH_STRATEGY` | Auth mode: `None` / `UserPassword` / `Token` / `TlsCert` / `CredFile` | `UserPassword` |
| `WEDA_NODE_USERNAME` | Username (for UserPassword) | `advantech_nats` |
| `WEDA_NODE_PASSWORD` | Password (for UserPassword) | (get from admin) |
| `SUBNODE_NAME` | Unique name for this deployment | `ImageSensor-Floor3` |

Other optional variables are documented in [.env.sample](.env.sample).

Then start:

```bash
docker compose up -d
```

This brings up two containers:

- `image-sensor-mqtt` — Mosquitto MQTT broker
- `image-sensor-app` — ImageSensor application

Verify:

```bash
docker compose logs -f image-sensor
```

You should see `Published digit_X.png ...` (simulator) and `Image received: ...` (device handler) every 30s.

## 4. Verify with the Receiver

The Receiver is a small NATS subscriber + web viewer. It is **not** part of compose — run it on the host so you can open the page in a browser:

```bash
cd examples/mqtt-image-chunked/Receiver
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

If you need a longer drain window by default, add to the `mqtt-image-chunked` service in `docker-compose.yml`:

```yaml
init: true                # tini as PID 1 — ensures SIGTERM reaches the .NET process
stop_signal: SIGTERM
stop_grace_period: 30s    # bump if your WedaNode unsubscribe takes longer
```

Avoid `docker compose kill` (SIGKILL) unless the container is wedged — it skips the shutdown hooks entirely.
