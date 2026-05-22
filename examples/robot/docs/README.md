---
sidebar_position: 0
sidebar_label: 'ROS 2 Bridge'
hide_title: true
title: 'ROS 2 Bridge | SubNode SDK'
keywords: ['SubNode', 'ROS 2', 'ROS2', 'Bridge', 'Robotics', 'DDS']
description: 'Design specification for the SubNode ROS 2 bridge: telemetry from ROS topics, commands via ROS services, built on the existing PubSubDeviceBase pattern.'
---

# ROS 2 Bridge

> **Status:** design specification (v0.1.0). Source code is to be added under `src/Weda.SubNode.Core/{Communication,Protocols}/Ros2` and `src/Weda.SubNode.Devices/Generic/Ros2BrokerDevice.cs`.

A SubNode that participates as a ROS 2 node on a configured `ROS_DOMAIN_ID`, exposing **ROS topics as telemetry sensors** and **ROS services as remote commands**. Built on the existing `PubSubDeviceBase` extension point — no framework changes required.

## Pages

| # | Page | What's inside |
|---|------|---------------|
| 01 | [Overview](./01-overview.md) | Motivation, scope of v1, transport options (rclnet vs rosbridge), layer mapping. |
| 02 | [Architecture](./02-architecture.md) | Three new components (Communication / Parser / Device), lifecycle alignment, sequence diagrams. |
| 03 | [Telemetry Interface](./03-telemetry.md) | Topic-to-sensor mapping, message type registry, QoS profiles, sensor-cache flow. |
| 04 | [Command Interface](./04-commands.md) | Service-first dispatch, `DeviceCommand` payload schema, error mapping, deferred action support. |
| 05 | [Configuration Reference](./05-configuration.md) | Full `devicecfg.json` schema, validation rules, Docker runtime requirements. |
| 06 | [Composite Messages](./06-composite-messages.md) | When messages ship as `application/json` and DTDL Object schemas are auto-generated from `.msg` IDL. |

## At a Glance

```
ROS 2 concept           SubNode SDK                Where it lives
──────────────────────────────────────────────────────────────────────────────
DDS Topic (in)        →  IPubSub.SubscribeAsync   →  Ros2Communication
DDS Topic (out)       →  IPubSub.PublishAsync     →  Ros2Communication
Topic→Sensor mapping  →  IPubSubProtocolParser    →  Ros2PubSubParser
ROS Service (request) →  ExecuteCommandAsync      →  Ros2PubSubParser
Device class          →  PubSubDeviceBase         →  Ros2BrokerDevice
```

## Key Design Decisions

1. **rclnet over rosbridge** for production. Native `librcl` P/Invoke gives full DDS QoS and lowest latency; rosbridge is documented as a fallback only.
2. **Service over topic publish** for any control command. Services give typed responses, availability checks, and timeout semantics — required for SIL2 fail-safe.
3. **One ROS node per device** (not per SubNode). Cleaner lifecycle, easier to scope `LivelinessChanged → DeviceStatus.Degraded`.
4. **Static type registry built at init**. Untrusted cloud-supplied `MessageType` strings are never resolved at runtime.
5. **Explicit `DomainId`, no default**. Required for IEC 62443 zoning between safety-critical and non-safety traffic.
6. **Publish allow-list** for `PublishTopic` commands. Mirrors MQTT topic ACLs.
7. **Actions deferred to v2** via `IStreamingCommunication`. Long-running operations should be modelled as a service returning a goal id, with progress polled via a sensor.
8. **Composite messages ship as `application/json`** with an auto-generated DTDL `Object` schema derived from the `.msg` IDL. Primitive `FieldPath` extraction is still the default; composite mode activates when `FieldPath` resolves to a struct or is omitted entirely. See [06-composite-messages](./06-composite-messages.md).

## Reference Implementations to Mirror

When implementing, the closest existing patterns are:

| New file | Mirror |
|----------|--------|
| `Ros2Communication.cs` | `src/Weda.SubNode.Core/Communication/Mqtt/MqttCommunication.cs` |
| `Ros2PubSubParser.cs` | `src/Weda.SubNode.Core/Protocols/ISensing/ISensingPubSubParser.cs` |
| `Ros2BrokerDevice.cs` | `src/Weda.SubNode.Devices/Generic/MqttISensingDevice.cs` and `TcpOpcUaPubSubDevice.cs` |
| `examples/ros2-device/` | `examples/wise-4012-isensing/` |

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 0.1.0 | 2026-05-08 | Kevin Chien | Initial design draft (5 pages). |
