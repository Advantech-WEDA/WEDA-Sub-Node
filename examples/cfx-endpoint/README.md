# CFX Endpoint Example

A SubNode that captures **IPC-CFX** traffic from an SMT production line and forwards each message
type to WedaCore as a JSON sensor reading.

```
CFX endpoints ──AMQP──> cfx-broker (RabbitMQ + rabbitmq_mqtt) ──MQTT──> CFX SubNode ──NATS──> WedaNode
 SUNJSONG.SLD880A.0001   :5672 in, :1884 out (bundled in this stack)    (this example)      (cloud)
```

The SubNode is **subscribe-only**: it never publishes onto the CFX network, it only observes it.

## What makes this example different

Every other transport example reports scalars — a temperature, a coil state. A CFX message body is a
*document*: `UnitsInspected` carries measurements and defect lists, `MagazineArrived` carries 24
slots of unit data. So here:

- Each sensor is bound to **one CFX message type**, and that binding is the whole configuration —
  there is no register address or width (`Parameters.MessageName`).
- Every sensor reports `Schema: application/json`, and the value is the CFX envelope's routing
  fields plus the untouched message body under `body`.
- The console prints a *summary* of each body (identifying fields, array lengths) rather than the
  document, which would be unreadable in a log.

## Files

| File | Holds |
|---|---|
| [`Program.cs`](Program.cs) | Host bootstrap; picks the real WedaNode or the mock cloud from `customcfg.json` |
| [`MyCfxEndpointDevice.cs`](MyCfxEndpointDevice.cs) | `MqttCfxDevice` subclass — logs received data, connection changes, and unmapped message types |
| [`Sensors/TypedCatalog.cs`](Sensors/TypedCatalog.cs) | Typed capability descriptors for the `mqtt-cfx` device kind and its `cfx-message` sensor kind |
| [`devicecfg.json`](devicecfg.json) | The bridge connection and the 17 sensors |
| [`systemcfg.json`](systemcfg.json) | WedaNode address and NATS credentials |
| [`customcfg.json`](customcfg.json) | `UseMockCloud` switch |
| [`appsettings.json`](appsettings.json) | Serilog settings |
| [`docker-compose.yml`](docker-compose.yml) | The bundled RabbitMQ bridge and the SubNode (Harbor images, `build:` fallback), plus the `replay` profile |
| [`.env.example`](.env.example) | Template for the two passwords; copy to `.env` (gitignored) |

`weda-data/` is gitignored local state — the accepted registration and the recording store.

## Prerequisites

- Docker with the Compose plugin, plus access to `harbor.arfa.wise-paas.com` to pull the images (or
  a local build — either way no .NET SDK is needed), or the .NET 10 SDK for `dotnet run`.
- A CFX AMQP-to-MQTT bridge. **The compose file now ships one** — a `cfx-broker` service running
  RabbitMQ with the `rabbitmq_mqtt` and `rabbitmq_amqp1_0` plugins enabled (the stock image enables
  neither), publishing MQTT on 1884, AMQP on 5672 and the management UI on 15672. Point
  `devicecfg.json` at an existing bridge instead if the line already has one.
- A WedaNode (`dmagent`) reachable over NATS — unless you run against the mock cloud.

## Quick start

### 1. Supply the credentials

Passwords stay out of the committed JSON. Environment variables are applied *after* the JSON
providers, so they override the file values.

```bash
cd examples/cfx-endpoint
cp .env.example .env
```

Read the two values from the running services rather than guessing them — the commands are in
[`.env.example`](.env.example). Both are declared in compose with the `:?` guard, so a missing `.env`
fails `docker compose up` immediately instead of starting a container that cannot authenticate.

### 2. Point it at the bridge

`devicecfg.json` → `DeviceConfigs.CfxEndpoint.DeviceCommunication`:

| Key | Shipped value | Meaning |
|---|---|---|
| `BrokerHost` | `127.0.0.1` | Host networking is used, so this is the host's own broker |
| `BrokerPort` | `1884` | MQTT listener on the bridge |
| `Username` | `cfx` | Bridge account (password comes from `.env`) |
| `ClientId` | `wiseiot-cfx-sub-001` | Must be unique per connected subscriber |
| `HandleSegments` | `3` | Matches CFX handles shaped `Vendor.Model.Serial` |

To watch a single endpoint instead of every endpoint of that handle shape, set `CfxHandle` to that
endpoint's handle. `BrokerUrl` (e.g. `mqtt://host:1883`) and `UseTls` are also accepted.

### 3. Choose the cloud target

`customcfg.json`:

```json
{ "UseMockCloud": false }
```

- `false` — publish to the WedaNode in `systemcfg.json`
- `true` — log telemetry locally; no WedaNode needed

This is the first thing to check when the application runs cleanly but nothing reaches the cloud.

### 4. Run

```bash
docker compose up -d
docker compose logs -f cfx-subnode
```

RabbitMQ takes about thirty seconds to boot. `cfx-broker` carries a healthcheck and the SubNode
`depends_on` it with `condition: service_healthy`, so the first MQTT connect does not race the
broker — `docker compose up -d` simply takes that long before the SubNode starts.

Both services are pinned to the images published on Harbor
(`harbor.arfa.wise-paas.com/edge-coa/cfx-endpoint` and `.../cfx-replay`), so nothing is compiled on
the target host. To run your own build of this working tree instead, add `--build` — or point
`CFX_SUBNODE_IMAGE` / `CFX_REPLAY_IMAGE` at a different tag.

Or without Docker (host must reach the bridge itself):

```bash
cd examples/cfx-endpoint
DeviceConfig__DeviceConfigs__CfxEndpoint__DeviceCommunication__Password=<broker-pw> \
SystemConfig__WedaNode__Password=<nats-pw> \
dotnet run
```

## Verify telemetry is flowing

Four signals, in order:

| Signal | Log line | Proves |
|---|---|---|
| Cloud target | `Cloud: WedaNode at 172.22.160.197:4224` | Mock mode is off |
| Broker | `MQTT connection: Disconnected -> Connected` | Bridge address and credentials are good |
| Registration | `weda-data/subnode.registration.json` appears with `"registrationStatus": "accepted"` | The WedaNode accepted this SubNode and assigned a telemetry topic |
| Data | `cfx_work_started from SUNJSONG.SLD880A.0001: TransactionID=…` | Messages are being parsed and reported |

If the first three appear but no data lines follow, the SubNode is fine and the line is simply quiet.
Prove the rest of the path with the replay tool.

## Smoke-test with replayed captures

[`cfx-replay`](../../tools/cfx-replay/) publishes recorded CFX payloads onto the same bridge, so the
deployment can be verified end-to-end while the line is idle. It sits behind a Compose profile so it
never joins the standing deployment by accident — and the same flag is needed to stop it:

```bash
docker compose --profile replay up -d cfx-replay
# ...watch the cfx-subnode logs, then:
docker compose --profile replay stop cfx-replay
```

It loops all 17 payloads at 700 ms intervals with a 5 s pause between cycles. The corpus is the CFX
parser's own fixture set and covers **every one of the 17 configured sensors**, so a complete cycle
should produce exactly one log line per sensor — which makes "did every sensor path work?" a
countable check.

## What gets uploaded

Seventeen sensors, one per CFX message type, all `SensorGroup: SYS`, `Schema: application/json`,
reporting at 1000 ms. `Dtdl.AutoGenEnabled` is `true`, so the cloud-side model is generated from this
sensor list rather than authored by hand.

| Lifecycle stage | Sensors |
|---|---|
| Endpoint | `cfx_endpoint_connected`, `cfx_endpoint_shutting_down`, `cfx_station_online`, `cfx_station_state_changed` |
| Material flow | `cfx_magazine_arrived`, `cfx_units_arrived`, `cfx_units_initialized`, `cfx_units_loaded`, `cfx_units_unloaded`, `cfx_units_departed`, `cfx_magazine_departed` |
| Process | `cfx_work_started`, `cfx_work_completed`, `cfx_units_inspected`, `cfx_identifiers_read` |
| Faults | `cfx_fault_occurred`, `cfx_fault_cleared` |

Adding a message type is one entry in `devicecfg.json` — no code change:

```json
{
  "Name": "cfx_units_scrapped",
  "SensorGroup": "SYS",
  "Parameters": { "MessageName": "CFX.Production.UnitsScrapped" },
  "SensorInfo": { "DisplayName": "Units Scrapped", "Schema": "application/json" },
  "Report": { "Enabled": true, "Interval": 1000 }
}
```

## How the topics map

Bridged CFX topics are built from the publishing endpoint's handle and the message's position in the
CFX type namespace:

```
{Vendor}/{Model}/{Serial}/CFX/[<namespace path>/]{MessageName}
└──── CFX handle, dots replaced by slashes ────┘
```

The namespace path is zero to two segments deep depending on the message, so topic depth is not
fixed. The subscriber matches with a multi-level wildcard and resolves the type from the envelope's
`MessageName` — **the envelope is authoritative, the topic is a routing convenience.**

## Troubleshooting

| Symptom | Cause |
|---|---|
| Replayed telemetry lands weeks in the past | The captures carry their recorded timestamps. `CFX_RESTAMP=true` (compose default) rewrites each envelope's `TimeStamp` to publish time. Without it the upload succeeds but no recent-window query shows it — failure that looks like success. |
| A renamed SubNode still reports under its old identity | `weda-data/` holds the accepted registration. After changing `SubNode.Name`, delete `weda-data/` so it registers afresh. |
| Nothing appears, but the broker is definitely busy | Messages for unbound types are logged at `Debug` (`Unmapped CFX message …`). Lower the Serilog minimum level in `appsettings.json` to see which types are live, then add sensors for them. |
| The container cannot reach a broker that works from your shell | The SubNode uses `network_mode: host`, so `127.0.0.1` is the host. `cfx-broker` deliberately stays on the default bridge network and *publishes* 1884, which is what makes it reachable; a broker bound only inside a Docker network without published ports is not. |
| `Connection refused` on the first start | RabbitMQ is still booting. The healthcheck gate normally prevents this; if it was removed, wait for `cfx-broker` to report healthy in `docker compose ps`. |
| The bridge is up but no CFX messages arrive | `cfx-broker` starts empty — it carries no traffic of its own. Either connect real CFX endpoints to AMQP on 5672, or use the `replay` profile below. |

## See also

- [Recipe: Run the CFX SubNode for Telemetry Upload](../../docs/wiki/en/02-getting-started/07-run-cfx-subnode-telemetry.md) — the full operating procedure
- [`tools/cfx-replay`](../../tools/cfx-replay/) — the replay tool
- [Examples index](../README.md) — pick an example by transport or by SDK feature
