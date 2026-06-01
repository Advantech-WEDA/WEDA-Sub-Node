---
sidebar_position: 5
sidebar_label: 'SI Integration Guide (Docker-only)'
hide_title: true
title: 'SI Integration Guide | SubNode SDK'
keywords: ['SubNode', 'System Integrator', 'SI', 'Docker', 'No Code', 'Integration', 'Quick Start']
description: 'A Docker-only, low-code integration path for System Integrators connecting a device to WedaCore without installing .NET or writing code.'
---

# SI Integration Guide (Docker-only)

> A start-to-finish integration path for **System Integrators (SI) with little or no coding experience**. You will install nothing but Docker, edit one JSON file, and run everything with `docker compose`.

## Overview

This guide is a companion to the standard Getting Started articles, written specifically for the SI workflow: **no .NET SDK, no IDE, no code changes**. You bring a device (or use the built-in simulator), edit `devicecfg.json`, and run a container. Every step below has been kept copy-paste safe so you can follow it without prior .NET knowledge.

If you are a .NET developer, the [Start with Example](./02-start-with-example.md) and [Start with Template](./03-start-with-template.md) articles will suit you better.

## What You'll Learn

After reading this article, you will be able to:

- Run a working SubNode container with a bundled simulator -- no hardware, no .NET
- Edit `devicecfg.json` to describe your own device and sensors
- Point the container at a real Modbus device
- Connect to WedaCore from a container using environment variables (no code)
- Diagnose the most common first-run problems

## Prerequisites

- **Docker** installed and running. See [Prerequisites → Option B (Docker)](./01-prerequisites.md#b-docker).
  - Verify: `docker --version` and `docker compose version` both return a version.
- A terminal and a plain-text editor (Notepad, TextEdit, VS Code -- anything that saves `.json`).

> You do **not** need the .NET SDK, Visual Studio, or any programming tools for this guide.

---

## The SI Workflow in One Picture

```text
  ┌──────────────┐   edit JSON   ┌──────────────┐   docker compose   ┌──────────────┐
  │ devicecfg.json│ ───────────▶ │  Container    │ ─────────────────▶ │  Telemetry    │
  │ (your device) │              │ (SubNode app) │                    │  in the logs  │
  └──────────────┘              └──────────────┘                     └──────────────┘
           ▲                                                                  │
           └──────────────────────  adjust & re-run  ◀───────────────────────┘
```

The entire job is a loop: **edit `devicecfg.json` → `docker compose up` → read the logs → repeat.** You never compile anything.

---

## Step 1 — Get the Code

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node/examples/wise-4012
```

The `wise-4012` example is the recommended starting point. It includes a `docker-compose-sim.yml` file that runs a **built-in Modbus simulator together with the SubNode application**, so you can see real telemetry flowing without any hardware.

---

## Step 2 — First Run with the Simulator (No Hardware)

The simulator listens on port `5020`. Open `devicecfg.json` in your editor and make sure the device points at the simulator:

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5020
      }
    }
  }
}
```

> This is the **one edit** required for the first run. The example ships pointing at a real hardware IP; the simulator uses `127.0.0.1:5020`.

Start the simulator and the application together:

```bash
docker compose -f docker-compose-sim.yml up
```

Docker builds the images on first run (this can take a couple of minutes) and then starts both services. Within a few seconds you should see telemetry lines in the log, for example:

```text
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
```

Press `Ctrl+C` to stop. To run it in the background instead and follow the logs:

```bash
docker compose -f docker-compose-sim.yml up -d        # start detached
docker compose -f docker-compose-sim.yml logs -f wise-4012   # watch logs
docker compose -f docker-compose-sim.yml down         # stop and remove
```

✅ **Checkpoint:** if you see changing sensor values in the log, your toolchain works end to end.

---

## Step 3 — Describe Your Own Device

Now make the configuration match the device you want to integrate. Everything happens inside `devicecfg.json` -- there is no code to change.

A minimal sensor entry looks like this:

```json
{
  "SubNode": {
    "Name": "MySite-Line1",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5020
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "SensorGroup": "TEMP",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Report": {
            "Enabled": true,
            "Interval": 5000
          }
        }
      ]
    }
  }
}
```

To add more readings, append more objects to the `Sensors` array. The full field list -- register types, data types, units, calibration, thresholds -- is documented in:

- [Configuration via JSON](../04-configuration/02-configuration-via-json.md) -- walkthrough with examples
- [Data Pipeline](../05-data-pipeline/01-overview.md) -- calibration, unit conversion, smoothing filters

After every edit, re-apply the configuration by running the Compose command again. Files are mounted into the container, so no rebuild is needed for config-only changes:

```bash
docker compose -f docker-compose-sim.yml up
```

---

## Step 4 — Point at a Real Device

When you are ready to talk to physical hardware, set `Host`/`Port` to your device and use the plain `docker-compose.yml` (no simulator):

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "DeviceCommunication": {
        "Host": "192.168.1.100",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      }
    }
  }
}
```

```bash
docker compose up
```

> The container uses host networking, so it reaches any device your host machine can reach. Confirm with `ping 192.168.1.100` from the host first.

---

## Step 5 — Connect to WedaCore (No Code)

In production the application talks to the cloud through a local **WedaNode** (a NATS proxy installed by the Device Activator). You provide the connection details as **environment variables** in the Compose file -- no source changes.

Add an `environment:` block to the application service in `docker-compose.yml`:

```yaml
services:
  wise-4012:
    # ... existing build/volumes/network settings ...
    environment:
      - SystemConfig__WedaNode__Url=127.0.0.1:4224
      - SystemConfig__WedaNode__AuthStrategy=UserPassword
      - SystemConfig__WedaNode__Username=advantech_nats
      - SystemConfig__WedaNode__Password=your_password_hash
```

> Environment variables use `__` (double underscore) as the level separator. These map to the same settings described in [Connect to WedaCore](./04-connect-to-wedacore.md), where all five authentication strategies (`None`, `UserPassword`, `Token`, `CredFile`, `TlsCert`) are documented.

Then bring the stack up as usual:

```bash
docker compose up
```

---

## Troubleshooting

| Symptom | Likely Cause | What to Try |
|---------|--------------|-------------|
| No telemetry in the logs (simulator run) | `Host`/`Port` not set to `127.0.0.1:5020` | Re-check Step 2; re-run `docker compose -f docker-compose-sim.yml up` |
| `Connection refused` to your device | Wrong IP/port, or device unreachable from host | `ping <device-ip>`; confirm Modbus TCP port (usually `502`) is open |
| Values look wrong / scaled | `DataType` or `RegisterCount` mismatch | Check the register map in your device manual against `Parameters` |
| `docker compose: command not found` | Docker Compose v2 not installed | Reinstall Docker Desktop / Docker Engine (see Prerequisites) |
| Cloud `Authentication failed` | `AuthStrategy` or credentials wrong | Verify the values supplied by your WedaNode administrator |

For deeper log detail, raise the log level. Edit `appsettings.json` in the example folder:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

---

## Summary

- The SI workflow is a loop: **edit `devicecfg.json` → `docker compose up` → read logs** -- no .NET, no compiling.
- Use `docker-compose-sim.yml` for a hardware-free first run; use `docker-compose.yml` against real devices.
- Describe sensors entirely in JSON; pipelines (calibration, filtering, thresholds) are also JSON.
- Connect to WedaCore by adding `SystemConfig__WedaNode__*` environment variables to the Compose file.

## See Also

- [Prerequisites](./01-prerequisites.md) -- install Docker (Option B)
- [Start with Example](./02-start-with-example.md) -- the developer-oriented walkthrough of the same example
- [Configuration via JSON](../04-configuration/02-configuration-via-json.md) -- complete `devicecfg.json` reference
- [Connect to WedaCore](./04-connect-to-wedacore.md) -- authentication strategies in detail

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-06-01 | Kevin.Chien | Doc created -- Docker-only SI integration path. |
