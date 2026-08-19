---
sidebar_position: 4
sidebar_label: 'Liveness Heartbeat'
hide_title: true
title: 'SubNode Liveness Heartbeat'
keywords: ['SubNode', 'Heartbeat', 'Liveness', 'Connectivity', 'Sensor', 'Configuration']
description: 'Enable the SubNode liveness heartbeat so the platform can derive Connected / Abnormal / Disconnected'
---

# Liveness Heartbeat

> Let the platform tell whether this SubNode is still alive, by declaring one reserved sensor.

## What it is

The heartbeat is a small telemetry measure the SubNode emits on a fixed interval. It carries a
constant `true` and says one thing: *this SubNode was alive and reporting when the platform
received it*.

The platform derives connectivity — `Connected`, `Abnormal`, `Disconnected` — from **when the
last beat arrived**, never from the value and never from a device-reported timestamp. Nothing is
pushed to the SubNode and the SubNode is never polled; the state is computed when an operator
asks for it.

## Enabling it

Add one reserved sensor to any device in `devicecfg.json`. **Declaring the sensor is the
switch** — there is no separate flag, no builder call and no `systemcfg.json` entry.

```jsonc
// devicecfg.json → DeviceConfigs.<yourDevice>.Sensors[]
{
  "Name": "hb",
  "SensorGroup": "SYS",
  "SensorInfo": {
    "DisplayName": "Heartbeat"
  },
  "Report": {
    "Enabled": true,
    "Interval": 60000
  },
  "Record": {
    "Enabled": false
  }
}
```

That is the whole change. `Program.cs` is untouched:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry();      // the heartbeat rides the telemetry uplink
```

**Disabled by default.** A SubNode whose `devicecfg.json` declares no such sensor emits no
heartbeat and pays nothing for the feature. Upgrading the SDK never starts one on its own.

## Field reference

| Field | Required | Notes |
|-------|----------|-------|
| `Name` | **Yes** | Must be exactly `hb` (case-insensitive). **This is the identity** — the SDK recognises the heartbeat by its name |
| `Report.Enabled` | Yes | `true` to beat. See [Disabling stops liveness](#disabling-stops-liveness) |
| `Report.Interval` | Yes | Beat interval `T` in milliseconds. **60000 (60 s)** for this release. Values below 1000 ms are clamped to 1000 ms |
| `Record.Enabled` | Recommended `false` | Keeps the beat out of local recording — see [Why it is not recorded](#why-it-is-not-recorded) |
| `SensorGroup` | Yes | `SYS` |
| `SensorInfo.DisplayName` | Optional | Cosmetic only |
| `Dtmi` | **Do not set** | Supplied by the SDK — see below |
| `SensorInfo.Schema` | **Do not set** | Supplied by the SDK as `boolean` — see below |
| `Parameters` | **No** | The heartbeat takes none. It is never read from hardware |

:::note The SDK supplies the DTMI and the schema
The heartbeat's DTMI (`dtmi:com:advantech:weda:Heartbeat;1`) and its `boolean` schema are part of
a platform contract the application does not get to vary, so the SDK stamps both onto the declared
sensor before DTDL generation. From there it flows through the ordinary auto-generation path.

Anything you write in those two fields is **overwritten** — a configured DTMI logs a warning first.
The value the SDK sends is a constant `true`, so a schema of, say, `double` would produce a model
that disagrees with the telemetry.

Identity is the *name* for a practical reason: it is the one field you necessarily write. An
earlier design keyed identity on the DTMI, which meant deleting a single line silently disabled
liveness with no error anywhere. There is now no line to delete.
:::

:::caution `hb` is reserved platform-wide
A sensor named `hb` is treated as the heartbeat: excluded from polling, with its DTMI and schema
overwritten. Do not use that name for an application sensor.
:::

## How it behaves

**One beat per SubNode.** The measure is sent on the SubNode's own DeviceId, not the owning
device's, so a SubNode running ten devices still produces exactly one liveness signal. Declaring
the sensor on more than one device logs a warning and uses the first.

**It is never polled.** No protocol parser is asked to read it — the SDK synthesises the value.
That is why it needs no `Parameters`, and why it works identically on Modbus, OPC UA, MQTT and
custom devices, none of which could produce a liveness reading themselves.

**It starts beating once the SubNode is registered.** The first beat is sent immediately after
cloud registration rather than after a full interval, so a restarted SubNode is seen as
`Connected` within seconds.

**A transport fault does not stop it.** A failed send is logged and the next beat is the
recovery. The loop is never torn down by an uplink error, because a stopped heartbeat would
leave a perfectly healthy SubNode reading as `Disconnected`.

### Confirming it is running

Two lines are logged once per process, at **Warning** level so they survive the default
`appsettings.json`, which pins Serilog to `Warning`:

```
[WRN] SubNode heartbeat ARMED from sensor 'hb' (interval 60000 ms)
[WRN] SubNode heartbeat CONFIRMED: first beat accepted by the cloud uplink (sensor 'hb', every 60000 ms from here)
```

`ARMED` means the loop started; `CONFIRMED` means a beat was actually accepted, which `ARMED`
alone cannot tell you. If no heartbeat is declared you get the opposite, also at Warning:

```
[WRN] SubNode heartbeat is DISABLED: no enabled sensor named 'hb' was found. This SubNode
      publishes no liveness signal and the platform will read it as Disconnected.
```

Logging healthy startup at Warning is unusual and deliberate: heartbeat silence is
indistinguishable from a dead node, so you must not have to raise the log level to tell the two
apart. Individual beats after the first stay at `Debug`.

### Where it appears

Because it is declared as a real sensor, the heartbeat gets a `ResourceId` and flows through the
normal capability path — it appears in the generated DTDL Interface, in the capability upload,
and in the device-management sensor registry:

```json
// DTDL Interface contents[] — auto-generated, no configuration needed
{ "@id": "dtmi:com:advantech:weda:Heartbeat;1", "@type": "Telemetry",
  "name": "hb", "schema": "boolean" }

// deviceCapabilities.sensors[]
{ "resourceId": "a1b536a7-…", "dtmi": "dtmi:com:advantech:weda:Heartbeat;1",
  "name": "hb", "sensorGroup": "SYS" }

// refModelsMap.configs[] — the heartbeat supplies its own Interface
{ "@id": "dtmi:com:advantech:weda:Heartbeat;1", "@type": "Interface",
  "extends": "dtmi:advantech:weda:sensor:base;1", "displayName": "Heartbeat" }
```

That last entry matters. A sensor's DTMI is resolved against `refModelsMap`, and ordinary sensors
get there through the sensor-type registry — which the heartbeat never enters, because it is
skipped during typed dispatch to preserve its reserved DTMI. It therefore contributes its own
Interface, extending the universal sensor envelope and carrying no `Parameters`.

You can confirm all of this for your own application without a cloud connection:

```bash
dotnet run --project tools/Weda.SubNode.CapabilityDump -- examples/system-agent out.json
```

### On the wire

The beat is one measure in the ordinary real-time telemetry message:

```json
{
  "measures": [
    {
      "sensorId": "d731c",
      "value": true,
      "timestamp": 1785000000000
    }
  ]
}
```

The platform identifies a liveness measure by the sensor's **DTMI**, which the enrichment stage
already resolves for every measure — so recognising the beat costs no extra lookup.

:::danger Never attach metadata to a heartbeat measure
`TelemetryMeasure.Metadata` is the framework's chunked-transfer descriptor, not a free-form bag.
The WedaNode telemetry proxy validates it whenever it is present and rejects the **entire
message** when `transferId` is missing:

```
measures[0].metadata.transferId: transfer ID is required
```

A liveness beat has nothing to chunk, so it must leave metadata unset. This applies to any
custom parser you write, not just the heartbeat.
:::

:::info Why not a well-known `sensorId`
The wire `sensorId` is the last five characters of the sensor's `ResourceId`, which is a UUID5
over (SubNode DeviceId, device name, sensor name). It therefore differs for every SubNode and
cannot be a fixed literal, which is why identification is by DTMI instead. One consequence: the
match happens *after* enrichment, not on the raw message.
:::

## Two things to know before enabling

### Disabling stops liveness

Because the heartbeat is an ordinary sensor entry, anything that disables it stops liveness
reporting — setting `Report.Enabled: false`, disabling the owning device, **or a configuration
update pushed from the cloud that does either**.

When that happens the platform sees silence and reports the SubNode as `Disconnected`, while
nothing on the device is actually wrong. The SDK logs a warning when the beat stops for this
reason, so the cause is visible in device logs:

```
[WRN] Heartbeat sensor 'hb' was disabled by configuration; liveness reporting has
      stopped and the platform will read this SubNode as Disconnected
```

The platform is expected to filter the heartbeat out of the customer-facing telemetry-configs
surface so it cannot be switched off by mistake. That control is platform-side and cannot be
enforced by the SDK.

### Why it is not recorded

Set `Record.Enabled: false`. At a 60 s cadence the heartbeat is **1,440 samples per day of a
constant `true`**, and slot-indexed recording consumes a slot per interval regardless of value.
Nothing reads it back: liveness is defined on *arrival*, so a replayed heartbeat asserts nothing
true about the present.

## Choosing the interval

`Interval` is the cadence `T`. The platform's thresholds are expressed as multiples of `T`, so
changing it without a matching platform-side change shifts when a SubNode is judged `Abnormal` or
`Disconnected`.

| Setting | Value |
|---------|-------|
| `T` (beat interval) | 60 s |
| `N` (missed windows before `Abnormal`) | 3 |
| `M` (grace before `Disconnected`) | 10 min |

Leave `Interval` at `60000` unless you have agreed a different `T` with the platform team.

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| No beats at all | No device declares a sensor named `hb`. Look for the `heartbeat is DISABLED` warning in device logs |
| `ARMED` logged but never `CONFIRMED` | The uplink is rejecting the beat. Check the WedaNode agent log for telemetry validation errors, and confirm nothing added metadata to the measure |
| Beats stopped, no device fault | The sensor was disabled — check device logs for the warning above, and whether a cloud config update changed `Report.Enabled` |
| `Disconnected` right after start | Normal until the SubNode completes cloud registration; the first beat follows immediately after |
| Beating faster than configured | `Interval` was below the 1000 ms floor and was clamped; a warning names the effective value |
| Cloud cannot resolve the heartbeat's DTMI | The SubNode is on an SDK build predating `refModelsMap` support; the Interface is not in the uploaded catalog |

## See Also

- [Configuration Reference](./configuration-reference.md) — all sensor options
- [Configuration via JSON](./configuration-via-json.md) — JSON configuration
- `examples/system-agent/devicecfg.json` — a working declaration

import Revision from '@site/src/components/Revision';

<Revision date="Aug-19, 2026" version="v1.2.1" />
