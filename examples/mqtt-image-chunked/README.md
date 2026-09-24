---
title: "MQTT Image (Chunked) Example"
description: "MQTT image ingestion with chunked telemetry and a browser-based receiver viewer, deployable with Docker only"
version: "0.0.2"
author: "Rain Hu"
date: "2026-09-09"
lang: "en"
---

# MQTT Image (Chunked) Example

Receive binary image data over MQTT, chunk it through the SubNode transform pipeline, publish it to
WedaNode (NATS), and re-assemble it in a browser viewer.

Three moving parts:

1. **MqttImageSimulator** (`Program.cs`) — a hosted service from `Weda.SubNode.Simulators` that
   publishes embedded MNIST PNGs (`digit_0.png` … `digit_9.png`, 167–255 bytes each) to
   `sensor/image/mnist` every 30 s.
2. **ImageSensorDevice** (`ImageSensorDevice.cs`) — subclasses `MqttImageDevice`; subscribes to that
   topic, surfaces each frame as an `image/png` telemetry measure, and logs a Base64 preview. The
   `chunking` transform in `devicecfg.json` splits oversized Base64 values before they are reported.
3. **Receiver** (`Receiver/`) — a standalone ASP.NET app; subscribes on NATS, reassembles chunks by
   `transferId`, verifies the CRC32 checksum, and serves a live viewer at <http://localhost:5100/>.

Only the first two run in Docker Compose. The Receiver is a verification tool you run on your own
machine — section 5 shows how to do that **without installing .NET**.

## 1. Prerequisites

| Requirement | Why | Notes |
|---|---|---|
| **Docker Engine 20.10+** with **Compose v2** (`docker compose`, not `docker-compose`) | Runs the MQTT broker and the SubNode app | Docker Desktop on macOS/Windows also works |
| A full checkout of this repo | The SubNode image is **built locally** from the repo's Dockerfile — no registry access needed | `docker compose up --build` builds it on first start |
| Network reach to a **WedaNode (NATS)** endpoint | Telemetry destination | Default port `4224`, credentials from your admin |
| Free TCP ports `1883`, `9001` (broker) and `5100` (viewer) | Local bind | `9001` is the MQTT-over-WebSocket listener |
| **A Linux host** for the default compose file | `image-sensor` uses `network_mode: host` | On macOS/Windows see the note below |

**No .NET runtime or SDK is required** to run this example. .NET 10 SDK is only needed if you want
to build the container image yourself (section 6) or run the apps from source.

> **`network_mode: host` on macOS/Windows.** The `image-sensor` service shares the host network stack
> so it can reach `127.0.0.1:1883` and a WedaNode on the LAN. Docker Desktop only honours this when
> host networking is enabled (Settings → Resources → Network). If you cannot enable it, remove
> `network_mode: host` from the service, add `ports: ["1883:1883"]`-style mappings as needed, and set
> `mqtt://mqtt:1883` as the broker URL in `devicecfg.json` so the app resolves the broker by service
> name instead.

## 2. What is in this directory

```
mqtt-image-chunked/
├── docker-compose.yml          # mqtt (Mosquitto) + image-sensor services
├── .env.sample                 # copy to .env, fill in WedaNode credentials
├── devicecfg.json              # SubNode identity, sensor, MQTT topic, chunking transform
├── systemcfg.json              # WedaNode address + Record quota (compose overrides via env)
├── customcfg.json              # example-specific switches (currently empty)
├── appsettings.json            # Serilog settings
├── Dockerfile                  # multi-arch build of the SubNode app (used by compose)
├── build.sh                    # optional: buildx wrapper to publish to your own registry
├── mosquitto/config/mosquitto.conf
├── commands/                   # sample cloud command payloads (report.data, report.historical)
├── Program.cs, ImageSensorDevice.cs, Sensors/TypedCatalog.cs
└── Receiver/                   # NATS subscriber + web viewer (not part of compose)
```

To deploy on a target machine you only need the runtime files — copy these to e.g. `/opt/image-sensor/`:

```
docker-compose.yml
.env                    # created from .env.sample
appsettings.json
customcfg.json
devicecfg.json
systemcfg.json
mosquitto/config/mosquitto.conf
```

Compose creates the rest on first start: `mosquitto/data/`, `mosquitto/log/` (broker persistence and
logs) and `.weda/` — which holds `subnode.registration.json`, the registration cache the Receiver
reads to learn the telemetry subject. Keep `.weda/` across restarts so the SubNode reuses its identity.

The **only file you must edit is `.env`**. Every WedaNode setting, Record quota and SubNode identity
field is passed into the container as an environment variable with a default baked into
`docker-compose.yml`, so `systemcfg.json` and `devicecfg.json` can stay as shipped.

## 3. Configure `.env`

```bash
cd examples/mqtt-image-chunked      # or your deployment directory
cp .env.sample .env
```

Required fields:

| Variable | Description | Example |
|---|---|---|
| `WEDA_NODE_URL` | WedaNode (NATS) address — `host:port`, no scheme | `10.0.1.50:4224` |
| `WEDA_NODE_AUTH_STRATEGY` | `None` / `UserPassword` / `Token` / `TlsCert` / `CredFile` | `UserPassword` |
| `WEDA_NODE_USERNAME` | Username (for `UserPassword`) | `advantech_nats` |
| `WEDA_NODE_PASSWORD` | Password (for `UserPassword`) | (get from your admin) |
| `SUBNODE_NAME` | Unique name for this deployment — set it per host | `ImageSensor-Floor3` |

Optional (defaults in `docker-compose.yml` apply when unset): `IMAGE_SENSOR_IMAGE`, `WEDA_NODE_NAME`,
`WEDA_NODE_SERIALIZER_TYPE`, `WEDA_NODE_TOKEN`, `WEDA_NODE_CRED_FILE`, `WEDA_NODE_TLS_*`,
`SUBNODE_TYPE`, `SUBNODE_MANUFACTURER`, `SUBNODE_MODEL`, `SUBNODE_SW_VERSION`,
`RECORD_MIN_FREE_DISK_SPACE_MB`, `RECORD_MAX_STORAGE_SIZE_MB`, `RECORD_RETENTION_DAYS`. All are
documented inline in [.env.sample](.env.sample).

> `.env.sample` and `systemcfg.json` ship with a placeholder password. Replace it — never commit real
> credentials.

## 4. Start the SubNode

```bash
docker compose up -d --build
```

The `--build` builds the `image-sensor` image locally from the repo's [Dockerfile](Dockerfile)
on first run; later `docker compose up -d` reuses it. No registry login is needed. (If you set
`IMAGE_SENSOR_IMAGE` in `.env` to an image in your own registry, drop `--build` and it is pulled
instead.)

Two containers come up:

| Service | Container | Image |
|---|---|---|
| `mqtt` | `image-sensor-mqtt` | `eclipse-mosquitto:2` (pulled from Docker Hub) |
| `image-sensor` | `image-sensor-app` | `${IMAGE_SENSOR_IMAGE:-image-sensor:local}` — built locally by default |

Verify:

```bash
docker compose logs -f image-sensor
```

Every 30 s you should see the simulator line `Published digit_X.png (NNN bytes) → sensor/image/mnist`
followed by the device handler line `Image received: ResourceId=..., ContentType=image/png, ...`.

Once registration succeeds, `.weda/subnode.registration.json` appears in the deployment directory.

## 5. Verify with the Receiver (no .NET required)

The Receiver is not in `docker-compose.yml` — run it on the machine whose browser you will use. It
needs the repo checked out (it is built from source), but not a local .NET install: run it inside the
official .NET SDK image.

```bash
cd examples/mqtt-image-chunked/Receiver
cp ../.env.sample .env        # first time only — same WedaNode credentials

docker run --rm -it \
  -p 5100:5100 \
  -v "$(cd .. && pwd)":/work \
  -v "$HOME/.nuget/packages":/root/.nuget/packages \
  -w /work/Receiver \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run
```

- The mounted directory is the **example root**, so the container sees both `/work/Receiver` and
  `/work/.weda/subnode.registration.json` — which is where the Receiver looks for the telemetry
  subject (`Receiver:ImageSensorRoot` defaults to `..`).
- The NuGet cache mount is optional; it just avoids re-downloading packages on every run.
- `WEDA_NODE_URL` must be reachable **from inside the container**. A LAN or cloud address works as is.
  If WedaNode runs on your own machine, use `host.docker.internal:4224` (Docker Desktop) or swap
  `-p 5100:5100` for `--network host` on Linux.

Then open <http://localhost:5100/> — the latest MNIST digit appears in the top panel and the last 10
images line up underneath (`Receiver:MaxImages`, default 10 in `Receiver/appsettings.json`).

If you do have the .NET 10 SDK, the same thing without Docker:

```bash
cd examples/mqtt-image-chunked/Receiver
cp ../.env.sample .env
dotnet run
```

> `Registration file not found` means the SubNode has not registered with WedaNode yet. Wait for
> `docker compose logs -f image-sensor` to show a successful registration, then start the Receiver again.

REST endpoints (for scripted checks):

| Method | Path | Description |
|---|---|---|
| GET | `/api/latest` | Most recent reassembled image (JSON + Base64) |
| GET | `/api/images` | List of cached images |
| GET | `/api/images/{id}` | Download a specific image as PNG |

## 6. Publish the image to your own registry (optional)

`docker compose up --build` (section 4) already builds the image locally, so this is only needed to
**publish** a multi-arch image to a registry you control — for a fleet that pulls rather than builds.
[`build.sh`](build.sh) wraps `docker buildx` and needs the full repo checkout (the build context is
the repo root):

```bash
PUSH=false ./build.sh                            # local single-arch build (--load), no registry

# To publish, point IMAGE_REPO at a registry you can push to:
IMAGE_REPO=docker.io/youruser/image-sensor ./build.sh          # Docker Hub, after `docker login`
IMAGE_REPO=harbor.example.com/team/image-sensor ./build.sh     # private Harbor, after `docker login`
IMAGE_TAG=v1.0.0 IMAGE_REPO=docker.io/youruser/image-sensor ./build.sh   # custom tag
```

Then set `IMAGE_SENSOR_IMAGE` in `.env` to that image so `docker compose up -d` pulls it.

The script creates a `image-sensor-builder` buildx builder on first run (`docker-container` driver on
the host network — the default `docker` driver cannot produce multi-arch manifests) and recreates it
if it is not on the host network. Override `IMAGE_REPO`, `IMAGE_TAG`, `PLATFORMS`, `PUSH` or
`BUILDER_NAME` via environment variables.

The [Dockerfile](Dockerfile) builds with `--platform=$BUILDPLATFORM`, maps `TARGETARCH` to a .NET RID
(`linux-x64` / `linux-arm64` / `linux-arm`), publishes self-contained, and runs on
`mcr.microsoft.com/dotnet/runtime:10.0-noble`.

To run or publish from source instead (.NET 10 SDK required):

```bash
cd examples/mqtt-image-chunked
dotnet run                                                       # current OS/arch
dotnet publish -c Release -r linux-arm64 --self-contained true   # ARM64
dotnet publish -c Release -r osx-arm64   --self-contained true   # Apple Silicon
dotnet publish -c Release -r win-x64     --self-contained true   # Windows x64
```

## 7. Cloud commands

`commands/` holds sample payloads for the built-in commands the SubNode exposes, useful when driving
the device from a NATS client:

| File | Command | Purpose |
|---|---|---|
| `commands/report-data.json` | `report.data` | Re-report one sensor value by short resource ID |
| `commands/report-historical.json` | `report.historical` | Replay recorded data over a time range |

`report.historical` returns nothing here unless recording is enabled — `Record.Enabled` is `false` for
the `MnistCamera` sensor in `devicecfg.json`.

## 8. Graceful shutdown

Both apps use the .NET generic host, which turns `Ctrl+C` / `SIGTERM` into cooperative cancellation
and runs every `IHostedService.StopAsync` before exiting: the simulator finishes its in-flight publish
and disconnects, and the device drains its NATS subscriptions.

```bash
docker compose stop -t 30 image-sensor   # SIGTERM with a 30 s grace period
docker compose down                      # tear everything down
```

For a longer default drain window, add to the `image-sensor` service in `docker-compose.yml`:

```yaml
init: true                # tini as PID 1 — ensures SIGTERM reaches the .NET process
stop_signal: SIGTERM
stop_grace_period: 30s
```

Avoid `docker compose kill` (SIGKILL) unless the container is wedged — it skips the shutdown hooks.

## 9. Known issue: `chunkSize` is below the allowed minimum

`devicecfg.json` configures the transform with `"chunkSize": 128`, but `ChunkingParameters.ChunkSize`
is annotated `[Range(1024, 768000)]` and `TransformFactory` validates it when the pipeline is built
for a report. As shipped, the first report therefore throws:

```
Transform 'chunking': parameter validation failed — The field ChunkSize must be between 1024 and 768000.
```

Two ways out, depending on what you want from the example:

- **See chunking work.** Relax the range on `ChunkingParameters` (`src/Weda.SubNode.Core/Transforms/ChunkingTransform.cs`)
  so a sub-KB `chunkSize` is legal. The MNIST PNGs are 167–255 bytes, i.e. roughly 224–340 Base64
  characters, so only a chunk size well under 1 KB splits them at all.
- **See the pipeline run end to end.** Set `"chunkSize": 1024` in `devicecfg.json`. Values then pass
  through unchunked, and the Receiver handles them on its non-chunked path — the viewer still works,
  but the chunk reassembly path is not exercised.

## Reference

- [`examples/README.md`](../README.md) — index of all SubNode examples
- [Connect to WedaCore](../../docs/wiki/en/02-getting-started/04-connect-to-wedacore.md) — credential handling
