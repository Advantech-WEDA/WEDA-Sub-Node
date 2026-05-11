---
sidebar_position: 1
sidebar_label: 'Overview'
hide_title: true
title: 'ROS 2 Bridge — Overview | SubNode SDK'
keywords: ['SubNode', 'ROS 2', 'ROS2', 'DDS', 'rclnet', 'rosbridge', 'Bridge', 'Robotics']
description: 'Design overview for bridging ROS 2 robotics nodes into a SubNode using the Pub/Sub device pattern, exposing topics as telemetry and services as commands.'
---

# ROS 2 Bridge — Overview

> Bring ROS 2 robotics nodes into the SubNode SDK as a first-class Pub/Sub device, exposing ROS topics as telemetry and ROS services as commands.

## Status

This section is a **design specification**. Source code under `src/Weda.SubNode.Core/Communication/Ros2`, `src/Weda.SubNode.Core/Protocols/Ros2`, and `src/Weda.SubNode.Devices/Generic/Ros2BrokerDevice.cs` is to be added; the design follows the same layering as the existing MQTT/ISensing and OPC UA Pub/Sub devices and is intended to be implementable without changes to `DeviceBase` or `PubSubDeviceBase`.

## What You'll Learn

After reading this section you will be able to:

- Understand how a ROS 2 deployment maps onto the SubNode SDK Pub/Sub device pattern.
- Choose between native rclnet and rosbridge transports for your environment.
- Configure ROS topics as telemetry sensors and ROS services as remote commands via `devicecfg.json`.
- Apply the SIL2 / IEC 62443 safety rules required for any ROS-driven control path.

---

## Why a ROS 2 Bridge?

Robotics and AMR (autonomous mobile robot) workloads at the edge already standardise on ROS 2 for inter-process messaging. A SubNode that natively understands ROS 2 lets a fleet of robots stream telemetry and accept remote commands through the same WedaCore pipeline that already handles Modbus, MQTT, and OPC UA devices — without writing a custom shim per robot model.

| Use case | Today (without bridge) | With ROS 2 bridge |
|----------|-----------------------|-------------------|
| Stream battery / pose / joint state to cloud | Custom Python node + MQTT republisher | One SubNode device, sensor-per-topic |
| Cloud-issued mode change (`SetMode`) | Custom REST shim + ROS service relay | `DeviceCommand` → ROS service call |
| Aggregate metrics across multiple robots | One republisher per robot | One SubNode, one ROS node per robot device |

## Clarifying "ROS 2 Message Broker"

ROS 2 is **not** a centralised broker like MQTT. It uses DDS (Data Distribution Service) as a decentralised peer-to-peer middleware via the `rmw` layer. The "broker" in this design is therefore conceptual: the SubNode itself becomes a participating ROS 2 node on a configured `ROS_DOMAIN_ID`, talking peer-to-peer to other nodes (robots, drivers, planners) over DDS.

This has three consequences the rest of the design must respect:

1. **Discovery is automatic** — there is no broker URL to point at. Configuration centres on `DomainId` and node identity.
2. **QoS is per-topic, not per-connection** — Reliability, Durability, History, and Liveliness must be configurable per sensor.
3. **Network isolation is by `ROS_DOMAIN_ID`** — required for IEC 62443 zoning between safety-critical and non-safety traffic.

## Transport Options

The SDK already abstracts transport via `IPubSub` (see `src/Weda.SubNode.Abstractions/Communication/ICommunication.cs`). Two transports are viable for ROS 2:

| Option | Native dependency | Latency | QoS fidelity | Recommended use |
|--------|-------------------|---------|--------------|-----------------|
| **rclnet** (ros2-dotnet, P/Invoke into `librcl`) | Yes — must ship matching `librmw_*` per platform | Lowest | Full DDS QoS, services, actions | Production deployments |
| **rosbridge** (`rosbridge_suite` over WebSocket, JSON) | No — pure .NET | Higher (JSON + extra hop) | Limited (no liveliness, simplified QoS) | PoC, dev environments, networks where DDS multicast is blocked |

**Default recommendation:** rclnet. The rosbridge path is documented as a fallback only.

## Scope of v1

The first deliverable scopes to telemetry + synchronous commands. Long-running goals are deferred:

| Feature | v1 | Later |
|---------|----|-------|
| Topic subscribe (telemetry) | ✅ | — |
| Topic publish (fire-and-forget command) | ✅ | — |
| Service call (request/response command) | ✅ | — |
| Action client (goal + feedback + result) | ❌ | v2 via `IStreamingCommunication` |
| Parameter API (get/set node parameters) | ❌ | v2 |
| Lifecycle node management | ❌ | v2 |

## How It Fits the SDK

The ROS 2 bridge uses three existing SDK extension points and adds zero changes to the framework core:

```
ROS 2 concept           SubNode SDK abstraction                     File to add
────────────────────────────────────────────────────────────────────────────────
DDS Topic (subscribe) → IPubSub.SubscribeAsync                    ┐
DDS Topic (publish)   → IPubSub.PublishAsync                      ├ Ros2Communication.cs
ROS Node lifecycle    → CommunicationBase.ConnectAsync            ┘
Topic→Sensor mapping  → IPubSubProtocolParser.OnTelemetryReceived ┐
Service call          → IPubSubProtocolParser.ExecuteCommandAsync ├ Ros2PubSubParser.cs
QoS resolution        → DeviceConfiguration.DeviceCommunication   ┘
Device class          → PubSubDeviceBase subclass                  ─ Ros2BrokerDevice.cs
```

User code on top of this layer remains as small as the existing `MqttISensingDevice` example.

## Reading Order

| # | Page | Purpose |
|---|------|---------|
| 02 | [Architecture](./02-architecture.md) | Class layering, lifecycle, sequence diagrams |
| 03 | [Telemetry Interface](./03-telemetry.md) | Topic-to-sensor mapping, message types, QoS, sensor cache |
| 04 | [Command Interface](./04-commands.md) | Service vs publish vs action, `DeviceCommand` schema |
| 05 | [Configuration Reference](./05-configuration.md) | `devicecfg.json` shape, validation rules, full example |
| 06 | [Composite Messages](./06-composite-messages.md) | `application/json` mode + DTDL Object schema auto-gen for nested ROS messages |

## See Also

- [Pipeline Overview](../05-data-pipeline/01-overview.md) — telemetry data flow once it leaves the parser.
- [Event System](../09-customization/02-event-system.md) — `DataReceived` / `DataProcessed` hooks usable in a ROS 2 device subclass.
- [Architecture Overview](../01-introduction/02-architecture.md) — the `DeviceBase → PubSubDeviceBase` lineage this design plugs into.

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 0.1.0 | 2026-05-08 | Kevin Chien | Initial design draft. |
