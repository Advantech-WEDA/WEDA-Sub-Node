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
  "Name": "heartbeat",
  "Dtmi": "dtmi:com:advantech:weda:Heartbeat;1",
  "SensorGroup": "SYS",
  "SensorInfo": {
    "Schema": "boolean",
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
| `Dtmi` | **Yes** | Must be exactly `dtmi:com:advantech:weda:Heartbeat;1`. **This is the identity** — the SDK recognises the heartbeat by this value and nothing else |
| `Name` | Yes | Free choice. Only a label; it plays no part in identification |
| `Report.Enabled` | Yes | `true` to beat. See [Disabling stops liveness](#disabling-stops-liveness) |
| `Report.Interval` | Yes | Beat interval `T` in milliseconds. **60000 (60 s)** for this release. Values below 1000 ms are clamped to 1000 ms |
| `Record.Enabled` | Recommended `false` | Keeps the beat out of local recording — see [Why not record it](#why-it-is-not-recorded) |
| `SensorGroup` | Yes | `SYS` |
| `SensorInfo.Schema` | Yes | `boolean` |
| `Parameters` | **No** | The heartbeat takes none. It is never read from hardware |

:::note The `Dtmi` field is a deliberate exception
SDK v1.2 introduced an **auto-gen-only DTMI policy**: ordinary sensor entries must not carry a
`Dtmi`, because the SDK assigns sensor-type DTMIs itself. The heartbeat is the one sanctioned
exception — its DTMI *is* its identity, and it is platform-owned rather than derived from a
sensor type. The SDK skips it during typed sensor dispatch so the value is preserved.
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

### Where it appears

Because it is declared as a real sensor, the heartbeat gets a `ResourceId` and flows through the
normal capability path — it appears in the generated DTDL Interface and in the capability upload,
and registers in the device-management sensor registry:

```json
// DTDL Interface contents[]
{ "@id": "dtmi:com:advantech:weda:Heartbeat;1", "@type": "Telemetry",
  "name": "heartbeat", "schema": "boolean" }

// deviceCapabilities.sensors[]
{ "resourceId": "a1b536a7-…", "dtmi": "dtmi:com:advantech:weda:Heartbeat;1",
  "name": "heartbeat", "sensorGroup": "SYS" }
```

You can confirm this for your own application without a cloud connection:

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
      "timestamp": 1785000000000,
      "metadata": { "hb": true }
    }
  ]
}
```

The `metadata.hb` marker is how the platform identifies a liveness measure. It is present on the
raw message, so detection happens before enrichment and needs no sensor-registry or IAM lookup.

:::info Why a marker rather than a well-known `sensorId`
The wire `sensorId` is the last five characters of the sensor's `ResourceId`, which is a UUID5
over (SubNode DeviceId, device name, sensor name). It therefore differs for every SubNode and
cannot be a fixed literal, so the reserved marker carries the identification instead.
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
warn: Heartbeat sensor 'heartbeat' was disabled by configuration; liveness reporting has
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
| No beats at all | No device declares a sensor with the reserved `Dtmi`. Check the DTMI is exact, including the `;1` version suffix |
| Beats stopped, no device fault | The sensor was disabled — check device logs for the warning above, and whether a cloud config update changed `Report.Enabled` |
| `Disconnected` right after start | Normal until the SubNode completes cloud registration; the first beat follows immediately after |
| Beating faster than configured | `Interval` was below the 1000 ms floor and was clamped; a warning names the effective value |

## See Also

- [Configuration Reference](./configuration-reference.md) — all sensor options
- [Configuration via JSON](./configuration-via-json.md) — JSON configuration
- `examples/system-agent/devicecfg.json` — a working declaration

import Revision from '@site/src/components/Revision';

<Revision date="Jul-22, 2026" version="v1.2.0" />
