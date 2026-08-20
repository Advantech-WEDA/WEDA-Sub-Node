---
sidebar_position: 7
sidebar_label: 'Recipe: Run the CFX SubNode'
hide_title: true
title: 'Run the CFX SubNode for Telemetry Upload | SubNode SDK'
keywords: ['SubNode', 'IPC-CFX', 'CFX', 'MQTT', 'SMT', 'Telemetry', 'Docker Compose', 'Replay', 'WedaCore']
description: 'Bring the IPC-CFX SubNode up against a line bridge and a WedaNode, verify that telemetry lands, and smoke-test the deployment with replayed captures while the line is idle.'
---

# Recipe: Run the CFX SubNode for Telemetry Upload

> Bring the IPC-CFX SubNode up against a line bridge and a WedaNode, verify that telemetry actually lands, and smoke-test the deployment with replayed captures while the line is idle.

## Overview

IPC-CFX is the SMT industry's equipment-messaging standard: endpoints on a production line publish typed messages -- units arrived, work started, fault occurred -- onto a CFX network. The `cfx-endpoint` example subscribes to those messages through an AMQP-to-MQTT bridge and forwards each message type to WedaCore as a JSON sensor reading.

This recipe is the operating procedure for that deployment. Unlike the scalar-protocol examples, a CFX message body is a document rather than a reading, so every sensor here reports `application/json` and carries the message body untouched.

## What You'll Learn

After reading this article, you will be able to:

- Configure the CFX SubNode against a CFX AMQP-to-MQTT bridge and a WedaNode
- Keep broker and NATS credentials out of the committed configuration files
- Verify from the logs that registration succeeded and telemetry is flowing
- Smoke-test the whole path with captured CFX traffic while the line is idle
- Recognise the failure modes that look like success

## Prerequisites

- Completed [Prerequisites](./01-prerequisites.md)
- Docker and the Compose plugin. The image builds from source, so no local .NET SDK is required
- **A CFX AMQP-to-MQTT bridge already running and reachable** -- RabbitMQ with the `rabbitmq_mqtt` plugin. The SDK does not ship one; the example's compose file has no broker service
- A WedaNode (`dmagent`) reachable over NATS, unless you run with the mock cloud (Step 4)

---

## The Path Telemetry Takes

| Hop | Component | Address in the shipped config |
|-----|-----------|-------------------------------|
| 1 | CFX endpoints on the line | CFX handle, e.g. `SUNJSONG.SLD880A.0001` |
| 2 | AMQP-to-MQTT bridge (**not** in this repo) | `127.0.0.1:1884` |
| 3 | `cfx-subnode` -- this application | 17 sensors, all `SYS` group |
| 4 | WedaNode over NATS | `172.22.160.197:4224` |

Bridged CFX topics are built from the publishing endpoint's handle and the message's position in the CFX type namespace:

```
{Vendor}/{Model}/{Serial}/CFX/[<namespace path>/]{MessageName}
└──── CFX handle, dots replaced by slashes ────┘
```

The namespace path is between zero and two segments deep depending on the message, so topic depth is not fixed. The subscriber matches with a multi-level wildcard and resolves the message type from the envelope's `MessageName` -- the envelope is authoritative, the topic is a routing convenience.

---

## Step 1 — Supply the Credentials

Both passwords are supplied as environment variables. Environment variables are applied *after* the JSON configuration providers, so they override the file values rather than being committed alongside them. `.env` is gitignored.

```bash
cd examples/cfx-endpoint
cp .env.example .env
```

Fill in the two values, reading them from the running services rather than guessing:

```bash
# CFX_BROKER_PASSWORD -- from the bridge
docker inspect cfx-rabbitmq --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | grep RABBITMQ_DEFAULT_PASS

# WEDA_NATS_PASSWORD -- from the dmagent on the device
ssh <device> 'docker inspect dmagent --format "{{range .Config.Env}}{{println .}}{{end}}"' \
  | grep NATS_
```

Both variables are declared in the compose file with the `:?` guard, so a missing `.env` fails `docker compose up` immediately with a named error instead of starting a container that cannot authenticate.

---

## Step 2 — Point the SubNode at the Bridge

Edit `devicecfg.json`, under `DeviceConfigs.CfxEndpoint.DeviceCommunication`. Leave the password out of this file -- Step 1 supplies it.

| Key | Shipped value | Meaning |
|-----|---------------|---------|
| `BrokerHost` | `127.0.0.1` | The container uses host networking, so this is the host's own broker |
| `BrokerPort` | `1884` | MQTT listener on the bridge |
| `Username` | `cfx` | Bridge account |
| `ClientId` | `wiseiot-cfx-sub-001` | Must be unique per connected subscriber |
| `HandleSegments` | `3` | Matches CFX handles shaped `Vendor.Model.Serial` |

To subscribe to a single endpoint rather than every endpoint with a matching handle shape, set `CfxHandle` to that endpoint's handle instead of relying on `HandleSegments`.

---

## Step 3 — Point It at the WedaNode

Edit `systemcfg.json`. Leave `Password` empty; it comes from `WEDA_NATS_PASSWORD`.

```json
{
  "WedaNode": {
    "Url": "172.22.160.197:4224",
    "AuthStrategy": "UserPassword",
    "Username": "advantech_nats",
    "Password": ""
  }
}
```

---

## Step 4 — Choose the Cloud Target

One image, two behaviours, selected by `customcfg.json`. For a real telemetry-upload run this must be `false`.

```json
{ "UseMockCloud": false }
```

- `false` -- publish to the WedaNode configured in `systemcfg.json`
- `true` -- log telemetry locally; no WedaNode needed

This is the switch to check first when the application runs cleanly but nothing reaches the cloud.

---

## Step 5 — Run It

```bash
docker compose up -d --build
docker compose logs -f cfx-subnode
```

The container restarts unless stopped, mounts the four JSON configuration files read-only, and persists its registration state to `./weda-data`.

---

## Step 6 — Verify Telemetry Is Flowing

Look for these four signals, in order:

| Signal | Log line | Proves |
|--------|----------|--------|
| Cloud target | `Cloud: WedaNode at 172.22.160.197:4224` | Mock mode is off |
| Broker | `MQTT connection: Disconnected -> Connected` | Bridge address and credentials are good |
| Registration | `weda-data/subnode.registration.json` appears with `"registrationStatus": "accepted"` | The WedaNode accepted this SubNode and assigned it a `telemetryTopic` of `eco1j.weda.<deviceId>.telemetry` |
| Data | One line per message, e.g. `cfx_work_started from SUNJSONG.SLD880A.0001: TransactionID=...` | Messages are being parsed and reported |

If the first three appear but no data lines follow, the SubNode is connected and registered but nothing is being published on the line. Use Step 7 to prove the rest of the path.

---

## Step 7 — Smoke-Test with Replayed Captures

The `cfx-replay` tool publishes recorded CFX payloads onto the same bridge, so the deployment can be verified end-to-end while the production line is idle. It sits behind a Compose profile so it never joins the standing deployment by accident -- and the same flag is needed to stop it.

```bash
docker compose --profile replay up -d cfx-replay
# ...watch the cfx-subnode logs, then stop it again:
docker compose --profile replay stop cfx-replay
```

It loops all 17 payloads at 700 ms intervals with a 5 s pause between cycles. The replay corpus is the CFX parser's own test fixture set, and it covers **every one of the 17 configured sensors** -- so a complete cycle should produce exactly one log line per sensor, which makes "did every sensor path work?" a countable check.

---

## What Gets Uploaded

Seventeen sensors, one per CFX message type, all identically shaped: `SensorGroup` `SYS`, `Schema` `application/json`, reporting enabled at a 1000 ms interval. `Dtdl.AutoGenEnabled` is `true`, so the model uploaded to the cloud is generated from this sensor list rather than authored by hand.

| Lifecycle stage | Sensors |
|-----------------|---------|
| Endpoint | `cfx_endpoint_connected`, `cfx_endpoint_shutting_down`, `cfx_station_online`, `cfx_station_state_changed` |
| Material flow | `cfx_magazine_arrived`, `cfx_units_arrived`, `cfx_units_initialized`, `cfx_units_loaded`, `cfx_units_unloaded`, `cfx_units_departed`, `cfx_magazine_departed` |
| Process | `cfx_work_started`, `cfx_work_completed`, `cfx_units_inspected`, `cfx_identifiers_read` |
| Faults | `cfx_fault_occurred`, `cfx_fault_cleared` |

Each reported value is the JSON document built from the CFX envelope: its routing fields plus the untouched message body under `body`. Bodies range from two fields to a 24-slot magazine, which is why the console prints a summary of the identifying fields rather than the whole document.

---

## Troubleshooting

### Replayed telemetry lands weeks in the past

The captures carry the timestamps they were recorded with, and the SubNode reports publisher time. `CFX_RESTAMP=true` in the compose file rewrites each envelope's `TimeStamp` to publish time. Without it the data uploads successfully but no recent-window query will ever show it -- the failure looks exactly like success. Set it to `false` only for byte-exact parser testing.

### A renamed SubNode still reports under its old identity

`weda-data/` is gitignored local state holding the accepted registration. After changing `SubNode.Name` in `devicecfg.json`, delete `weda-data/` so the SubNode registers afresh instead of continuing to report against the previous device.

### Nothing appears, and the broker is definitely busy

CFX messages arriving for types no sensor is bound to are logged at `Debug`. On a shared broker those lines are the fastest way to see which endpoints and message types are actually live. Raise the Serilog minimum level in `appsettings.json` to surface them, then add sensors for the types you need.

### The container cannot reach a broker that works from your shell

Both services run with `network_mode: host`, so `127.0.0.1` means the host. Addresses that work from your shell work from the container -- but a broker bound only to a Docker bridge network will not be reachable.

---

## Summary

- The CFX SubNode is a **subscriber**: it takes bridged CFX messages off MQTT and forwards each message type to WedaCore as a JSON sensor.
- Credentials live in `.env` and override the JSON config; the committed files never carry a password.
- Four configuration files decide everything: `devicecfg.json` (the bridge and the sensors), `systemcfg.json` (the WedaNode), `customcfg.json` (real cloud or mock), `appsettings.json` (logging).
- Verification is a four-signal check in the logs -- cloud target, broker connection, registration accepted, data lines.
- `cfx-replay` proves the full path without the line running, and its corpus covers every configured sensor.
- The failure mode to watch for is **silent success**: telemetry that uploads correctly but carries the wrong timestamp, or reports under a stale device identity.

## See Also

- [SI Integration Guide (Docker-only)](./05-si-integration-guide.md) -- the no-code Docker workflow
- [Recipe: Integrate an Edge AI Container](./06-integrate-edge-ai-container.md) -- the MQTT/REST integration recipe this one parallels
- [Connect to WedaCore](./04-connect-to-wedacore.md) -- credentials and environment variables
- [Configuration via JSON](../04-configuration/02-configuration-via-json.md) -- sensor fields and `Parameters`
- Reference example: [`cfx-endpoint`](https://github.com/Advantech-Containers/WEDA-Sub-Node/tree/main/examples/cfx-endpoint)

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-08-13 | Kevin.Chien | Doc created -- operating procedure for the CFX SubNode: bridge and WedaNode configuration, log-based verification, replay smoke test, and the silent-failure modes. |
