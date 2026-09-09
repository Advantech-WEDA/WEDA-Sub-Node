# SubNode SDK examples

> Every example is a runnable SubNode application. Pick the one whose **transport** matches the
> equipment you are integrating, or whose **feature** matches the thing you are trying to learn.

Names lead with the axis you search on, so the directory listing is the index: everything
`modbus-*` speaks Modbus, everything `mqtt-*` speaks MQTT, and everything `feature-*` teaches an
SDK capability rather than a protocol.

## By transport

Start here when you have equipment to connect and you know how it speaks.

| Example | Transport | Data type | Start here if |
|---|---|---|---|
| [`modbus-wise4012`](./modbus-wise4012/) | Modbus TCP | `integer`, `boolean` | You are new to the SDK. The simplest complete device, wired up by hand so nothing is hidden. |
| [`modbus-wise4012-builder`](./modbus-wise4012-builder/) | Modbus TCP | `integer`, `boolean` | Same device as above, hosted through the builder with dependency injection and automatic lifecycle. The shape to copy for production. |
| [`mqtt-isensing-wise4012`](./mqtt-isensing-wise4012/) | MQTT (iSensing) | `double`, `boolean` | Your device publishes Advantech's iSensing JSON over MQTT. |
| [`mqtt-image-chunked`](./mqtt-image-chunked/) | MQTT | `image/png` | You need to move binary payloads, and to understand how large values are chunked for transfer. |
| [`opcua-basic`](./opcua-basic/) | OPC UA | `double` | Your equipment exposes an OPC UA server. Includes a simulator, so it runs with no hardware. |
| [`http-stock-quotes`](./http-stock-quotes/) | HTTP (poll) | `double`, `long` | You are polling a REST API on an interval and mapping fields to sensors. |
| [`http-air-quality`](./http-air-quality/) | HTTP (poll) | `application/json` | You are polling a REST API and reporting the whole document rather than scalar fields. |
| [`daq-collector`](./daq-collector/) | DAQ streaming | `double` | You have a high-rate streaming source and need windowed feature extraction. |
| [`daq-proxy`](./daq-proxy/) | SubNode to SubNode | `double`, `string` | You need one SubNode to consume another's telemetry. Pairs with `daq-collector`. |

## By SDK feature

Start here when the transport is already solved and you want to learn a capability. These happen to
be built on Modbus, but the transport is incidental.

| Example | Teaches |
|---|---|
| [`feature-transform-pipeline`](./feature-transform-pipeline/) | Transforms and DSP filters — unit conversion, calibration, smoothing — applied between reading and reporting. |
| [`feature-custom-commands`](./feature-custom-commands/) | Custom cloud-invocable commands: handler, validator, result, and how commands are exposed in the capability catalogue. |
| [`feature-aggregation`](./feature-aggregation/) | Deriving one sensor from several sources — power computed from voltage and current. |

## Not examples

| Directory | What it actually is |
|---|---|
| [`system-agent`](./system-agent/) | A shipped product component — the host-metrics SubNode that runs on every WEDA Node. Kept here for now, but it is not a sample to copy. |
| [`robot`](./robot/) | Documentation only — design notes on ROS2/DDS integration (topics and QoS, DDS vs NATS, message libraries, fleet patterns). No code, nothing to run. |

---

## Running any example

Every example follows the same four-file configuration shape:

| File | Holds |
|---|---|
| `devicecfg.json` | The device, its sensors, and their reporting settings |
| `systemcfg.json` | Where to send telemetry (the WedaNode address and credentials) |
| `customcfg.json` | Example-specific switches, including whether to use the mock cloud |
| `appsettings.json` | Logging |

Each example's `docker-compose.yml` also persists `/app/.weda` — the accepted SubNode registration
and the recording store — through `WEDA_DATA_PATH`, which defaults to the on-device convention
`/opt/Advantech/weda/node/{containerServiceName}`. Point it somewhere else when you want the state
beside the example instead:

```bash
WEDA_DATA_PATH=./weda-data docker compose up -d
```

Losing that directory makes the SubNode register afresh and collide with its own previous
registration (`409 Device name already in use`), so treat it as state, not cache.

```bash
cd examples/<example-name>
dotnet run
```

Most examples also ship a `Dockerfile` and `docker-compose.yml` if you would rather not install the
.NET SDK locally.

> **Credentials.** `systemcfg.json` ships with placeholder credentials. Supply real ones through
> environment variables rather than editing the file, so secrets never reach git — see
> [Connect to WedaCore](../docs/wiki/en/02-getting-started/04-connect-to-wedacore.md).

## Naming convention

New examples follow the same rule, so the listing stays an index:

- **Folder** is kebab-case, prefixed by transport (`modbus-`, `mqtt-`, `http-`, `opcua-`, `daq-`) or
  by `feature-` when the lesson is transport-agnostic.
- **Project file** is the PascalCase of the folder — `modbus-wise4012/ModbusWise4012.csproj`. No
  `Example` suffix; the directory already says that.

## Where to go next

- [Prerequisites](../docs/wiki/en/02-getting-started/01-prerequisites.md) — development environment setup
- [Start with Example](../docs/wiki/en/02-getting-started/02-start-with-example.md) — a guided walkthrough
- [Configuration via JSON](../docs/wiki/en/04-configuration/02-configuration-via-json.md) — the full sensor reference
