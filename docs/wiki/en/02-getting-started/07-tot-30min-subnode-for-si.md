---
sidebar_position: 7
sidebar_label: 'TOT: SubNode for SI (30 min)'
hide_title: true
title: 'TOT Session — SubNode for System Integrators (30 min) | SubNode SDK'
keywords: ['SubNode', 'TOT', 'Training', 'System Integrator', 'SI', 'Onboarding', 'Enablement', 'Agenda']
description: 'A timeboxed 30-minute Transfer of Technology run-of-show that explains what SubNode is and demonstrates the System Integrator workflow end to end.'
---

# TOT Session — SubNode for System Integrators (30 min)

> A facilitator's run-of-show for a 30-minute Transfer of Technology session: what SubNode is, where the SI fits, and a live demo of the integration loop.

## Overview

This page is the script for a single 30-minute meeting, not a reference article. It exists so that anyone on the team can deliver a consistent SubNode introduction to a System Integrator audience without rebuilding the deck each time.

The session answers two questions and nothing else: **what is SubNode**, and **what does an SI actually do with it**. Everything deeper -- register maps, data pipelines, custom protocols, cloud authentication -- is deliberately left to the linked articles and a follow-up session. Thirty minutes buys you one clear mental model and one working demo; trying to buy more is how these sessions overrun.

## What You'll Learn

After reading this article, you (as the facilitator) will be able to:

- Run the session to time using a fixed five-segment agenda
- Explain the SubNode aggregation model and the SI/Advantech division of labour
- Deliver a hardware-free live demo that produces real telemetry in under ten minutes
- Answer the questions this audience reliably asks
- Send attendees away with three concrete next step

---

## Before the Meeting (Facilitator Pre-Flight)

Do this **the day before**, not five minutes before. The first `docker compose up` builds two images and takes a couple of minutes -- long enough to swallow a third of your demo slot.

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node/examples/wise-4012

# Pre-build both images so the live run starts instantly
docker compose -f docker-compose-sim.yml build
```

Then edit `examples/wise-4012/devicecfg.json` and point the device at the simulator, because the example ships pointing at a real hardware IP:

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

Rehearse once end to end:

- [ ] `docker compose -f docker-compose-sim.yml up` prints changing sensor values within a few seconds
- [ ] `docker compose -f docker-compose-sim.yml down` cleans up
- [ ] Terminal font size is readable on a shared screen (16pt+)
- [ ] Wiki open in a second tab at [SI Integration Guide](./05-si-integration-guide.md)
- [ ] `systemcfg.json` is **not** on screen during the demo -- the examples ship with a hard-coded lab NATS credential in that file. Do not project it, and tell attendees to replace it with an environment variable before any real deployment.

---

## Run of Show

| Time | Segment | Goal |
|------|---------|------|
| 0:00 – 0:03 | Welcome & framing | Set the two questions the session answers |
| 0:03 – 0:11 | **What is SubNode?** | One mental model: aggregation root + JSON |
| 0:11 – 0:16 | **Where the SI fits** | Who does what; the loop |
| 0:16 – 0:26 | **Live demo** | Telemetry on screen, no hardware |
| 0:26 – 0:30 | Next steps & Q&A | Three actions, then questions |

> **Running late?** Cut the DTDL/digital-twin aside in Segment 1 and the "point at real hardware" step in the demo. Never cut the demo itself -- it is the only part attendees remember.

---

## Segment 1 — What is SubNode? (8 min)

### The one-sentence definition

> SubNode is an edge application that collects data from your industrial devices and delivers it to the WedaCore cloud -- described almost entirely in a JSON file rather than written in code.

### The picture to draw

```text
┌──────────────────┐        ┌──────────────────┐        ┌──────────────────┐
│  Physical Device │        │     SubNode      │        │    WedaCore      │
│                  │ Modbus │   Application    │  NATS  │    (Cloud)       │
│   PLC / Sensor   │◀──────▶│  Device → Sensor │◀──────▶│  Digital Twin    │
│   Meter / Camera │  MQTT  │     Pipeline     │        │  IoT DB          │
│                  │ OPC UA │   Cloud Service  │        │  Remote Commands │
└──────────────────┘        └──────────────────┘        └──────────────────┘
```

Two directions of travel, and both matter to an SI:

1. **Uplink (telemetry)** — device is read on an interval → values pass through a processing pipeline → published to WedaCore.
2. **Downlink (commands & config)** — the cloud sends a command or a configuration change → SubNode applies it to the device, without redeploying anything.

### The aggregation model

This is the single most important concept, and it maps directly onto how an SI thinks about a site:

```text
SubNode (1)  ──  the application on this box, one per site/panel/machine
   └── Device (N)   ──  one physical box each: a PLC, a meter, a WISE module
          └── Sensor (N)  ──  one reading each: temperature, voltage, channel_0
```

A SubNode is the **aggregation root**: it owns its devices, and each device owns its sensors. When you configure SubNode, you are filling in exactly this tree in `devicecfg.json`.

### What you get for free

| Capability | Why the SI cares |
|-----------|------------------|
| Protocols built in | Modbus TCP/RTU, MQTT, OPC UA, HTTP REST -- no driver work |
| Scheduled collection | Per-sensor reporting interval, set in JSON |
| Data processing | Calibration, unit conversion, smoothing (moving average, Kalman) -- also JSON |
| Reliability | Reconnection, circuit breaker and retry are already in the box |
| Cloud integration | Telemetry upload, remote commands, and configuration sync over NATS |
| Digital twin model | The device model (DTDL) is generated from your configuration automatically |

> **Optional aside (cut this first if short on time):** SubNode generates a DTDL model from your JSON, which is what gives you a typed digital twin in WedaCore rather than a bag of untyped numbers.

### The connection detail that always comes up

SubNode does **not** dial the cloud directly. It talks to a local **WedaNode** (a NATS proxy on `127.0.0.1:4224`, installed by the Device Activator), and WedaNode talks to WedaCore. So the SI's networking job is local, not internet-facing.

```text
SubNode ──NATS──▶ WedaNode (127.0.0.1:4224) ──NATS──▶ WedaCore
```

---

## Segment 2 — Where the SI Fits (5 min)

### Division of labour

Make this explicit early; it is what the audience is really there to find out.

| Advantech provides | The SI provides |
|--------------------|-----------------|
| The SDK and the pre-built container images | The device, and its register/topic map |
| Ready-made device types (Modbus, MQTT, OPC UA, HTTP) | `devicecfg.json` describing devices and sensors |
| The pipeline, reliability and cloud plumbing | Network access from the box to the device |
| WedaNode + the Device Activator | Site deployment and commissioning |
| 11 working examples to copy from | Verification that the values are correct |

### There is no compiling in the SI path

```text
  ┌───────────────┐   edit JSON   ┌───────────────┐   docker compose   ┌───────────────┐
  │ devicecfg.json│ ────────────▶ │   Container    │ ─────────────────▶ │   Telemetry    │
  │ (your device) │               │ (SubNode app)  │                    │  in the logs   │
  └───────────────┘               └───────────────┘                     └───────────────┘
           ▲                                                                     │
           └───────────────────────  adjust & re-run  ◀─────────────────────────┘
```

Say it plainly: **no .NET SDK, no IDE, no source changes.** Docker and a text editor are the whole toolchain. Configuration files are mounted into the container, so a config-only change needs no rebuild.

### When code *is* needed

Be honest about the boundary, or you will get an unhappy escalation later:

- Your device speaks Modbus, MQTT, OPC UA or HTTP → **JSON only**.
- Your device speaks something proprietary → a small amount of C# implementing `ICommunication` / `IProtocolParser`. That is a developer task, covered in [Custom Device](../09-customization/01-custom-device.md).
- You need a calculation across several devices, or on-edge maths → also code, and there are examples to copy.

---

## Segment 3 — Live Demo: The SI Loop (10 min)

Narrate what you are doing at each step; the commands are short enough that silence is worse than over-explaining.

### 3a. Show the input (2 min)

Open `examples/wise-4012/devicecfg.json` and walk the tree out loud, tying it back to the aggregation model:

```json
{
  "SubNode": {
    "Name": "MySite-Line1",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": { "Host": "127.0.0.1", "Port": 5020 },
      "Properties": { "SlaveId": 1 },
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
          "Report": { "Enabled": true, "Interval": 5000 }
        }
      ]
    }
  }
}
```

Point out the four things an SI fills in, and nothing else: **where the device is** (`DeviceCommunication`), **which register to read** (`Parameters`), **what to call it** (`Name`), and **how often** (`Report.Interval`).

### 3b. Run it (3 min)

```bash
cd examples/wise-4012
docker compose -f docker-compose-sim.yml up
```

This starts a **Modbus simulator on port 5020** alongside the SubNode application, so the demo needs no hardware. Within a few seconds:

```text
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
```

✅ **Checkpoint to call out:** changing values in the log means the toolchain works end to end. This is exactly what the SI should see on their own machine.

### 3c. Change something and re-run (3 min)

This is the beat that sells the whole model. Edit the interval in `devicecfg.json`:

```json
"Report": { "Enabled": true, "Interval": 1000 }
```

Re-run, and let them watch the log speed up:

```bash
docker compose -f docker-compose-sim.yml up
```

Say the line out loud: **nothing was recompiled.** Adding a second sensor is the same move -- another object in the `Sensors` array.

### 3d. Point at the cloud (2 min)

Show, but do not run, how the cloud connection is configured -- environment variables in the Compose file, not source code:

```yaml
services:
  wise-4012:
    environment:
      - SystemConfig__WedaNode__Url=127.0.0.1:4224
      - SystemConfig__WedaNode__AuthStrategy=UserPassword
      - SystemConfig__WedaNode__Username=<from your WedaNode administrator>
      - SystemConfig__WedaNode__Password=<from your WedaNode administrator>
```

Note the `__` (double underscore) level separator, and that five authentication strategies are supported (`None`, `UserPassword`, `Token`, `CredFile`, `TlsCert`). Credentials come from whoever runs the WedaNode -- they are never invented by the SI.

Close the demo:

```bash
docker compose -f docker-compose-sim.yml down
```

---

## Segment 4 — Next Steps & Q&A (4 min)

Give exactly three actions. More than three and none of them happen.

1. **Install Docker** and run the demo yourself, following the [SI Integration Guide](./05-si-integration-guide.md).
2. **Send us your device's register or topic map** -- that is the input we need to help you write `devicecfg.json`.
3. **Pick your closest starting example** from the [Feature Map](../08-examples/00-feature-map.md), and copy it.

Then open the floor.

---

## Anticipated Questions

| Question | Short answer | Follow-up link |
|----------|--------------|----------------|
| Do I have to write code? | Not for Modbus, MQTT, OPC UA or HTTP devices. JSON only. | [SI Integration Guide](./05-si-integration-guide.md) |
| Which protocols are supported? | Modbus TCP/RTU, MQTT, OPC UA, HTTP REST, plus custom. | [Feature Map](../08-examples/00-feature-map.md) |
| My device uses a proprietary protocol. | Supported, but it needs a small custom device class. | [Custom Device](../09-customization/01-custom-device.md) |
| Can I test without hardware? | Yes -- built-in simulators, plus MockCloud for offline work. | [Start with Example](./02-start-with-example.md) |
| How do I scale/calibrate a raw value? | Transform and DSP pipelines, configured in JSON. | [Pipeline Overview](../05-data-pipeline/01-overview.md) |
| How does it reach the cloud? | Through a local WedaNode NATS proxy, not directly. | [Connect to WedaCore](./04-connect-to-wedacore.md) |
| Where do credentials come from? | The WedaNode administrator / Device Activator. Never hard-code them. | [Connect to WedaCore](./04-connect-to-wedacore.md) |
| Which SDK version do I need for feature X? | Check the **Since** column in the feature map. | [Feature Map](../08-examples/00-feature-map.md) |
| I already have an Edge AI container. | It is just another data source (REST or MQTT). | [Integrate an Edge AI Container](./06-integrate-edge-ai-container.md) |
| What if my values look wrong? | Almost always a `DataType` or `RegisterCount` mismatch. | [SI Integration Guide → Troubleshooting](./05-si-integration-guide.md#troubleshooting) |

---

## Facilitator Notes

- **Lead with the demo if the room is impatient.** Segments 1 and 2 land better after people have seen numbers move. The agenda above is the safe order, not the only one.
- **Do not open the SDK source.** This audience does not want to see C#, and showing it undercuts the "no code" message.
- **Resist the deep dive.** Register-map questions about a specific device will eat the session. Take them offline: "send me the map and we will do it together."
- **One terminal, one file.** Every extra window costs comprehension on a shared screen.
- **The follow-up session** (60 min, developer audience) covers custom devices, pipelines, commands and recording. Mention it exists; do not preview it in detail.

## See Also

- [SI Integration Guide (Docker-only)](./05-si-integration-guide.md) -- the hands-on article attendees should read next
- [What is SubNode?](../01-introduction/01-what-is-subnode.md) -- the longer conceptual background for Segment 1
- [Terminology](../01-introduction/03-terminology.md) -- definitions of SubNode, Device, Sensor
- [Connect to WedaCore](./04-connect-to-wedacore.md) -- all five authentication strategies
- [Feature Map](../08-examples/00-feature-map.md) -- pick the closest example to copy
- [Integrate an Edge AI Container](./06-integrate-edge-ai-container.md) -- the recipe for AI-container data sources

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-07-28 | Kevin.Chien | Doc created -- 30-minute TOT run-of-show for SI onboarding. |
