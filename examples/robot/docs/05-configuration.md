---
sidebar_position: 5
sidebar_label: 'Configuration Reference'
hide_title: true
title: 'ROS 2 Bridge — Configuration Reference | SubNode SDK'
keywords: ['SubNode', 'ROS 2', 'Configuration', 'devicecfg.json', 'DomainId', 'QoS', 'Validation']
description: 'Full devicecfg.json schema for the ROS 2 bridge, including DomainId, QoS profiles, publish allow-list, validation rules, and runtime considerations.'
---

# ROS 2 Bridge — Configuration Reference

> Complete `devicecfg.json` schema for the ROS 2 bridge, plus validation rules and Docker runtime requirements.

## Top-Level Shape

The bridge reuses the standard SubNode `devicecfg.json` layout (`src/Weda.SubNode.Abstractions/Devices/DeviceConfiguration.cs`). Only the `DeviceCommunication` block and per-sensor `Parameters` differ from other protocols.

```json
{
  "SubNode": {
    "Name": "MyRobotSubNode",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "Advantech",
    "Model": "Ros2Bridge",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "Robot1": {
      "Enabled": true,
      "DeviceCommunication": { /* see below */ },
      "Sensors":              [ /* see below */ ],
      "ConnectionSettings":   { /* optional, standard SDK shape */ }
    }
  }
}
```

## `DeviceCommunication` Schema

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `NodeName` | string | yes | — | rcl node name. Must be unique on the domain. Recommended pattern: `weda_subnode_<robot-id>`. |
| `DomainId` | int | yes | — | `ROS_DOMAIN_ID` for DDS isolation. **No default** — must be explicit per IEC 62443 zoning. |
| `Transport` | string | no | `rclnet` | `rclnet` (native) or `rosbridge` (WebSocket fallback). |
| `RmwImplementation` | string | no | `rmw_fastrtps_cpp` | Only honoured when `Transport = rclnet`. |
| `RosbridgeUrl` | string | conditional | — | Required when `Transport = rosbridge`. Example: `ws://192.168.1.50:9090`. |
| `QoS` | object | no | see [Default QoS](#default-qos) | Device-level QoS, used when a sensor has no per-sensor override. |
| `PublishableTopics` | array | no | `[]` | Allow-list for `PublishTopic` commands. See [Publish Allow-List](#publish-allow-list). |
| `LivelinessLeaseDuration` | duration | no | `"10s"` | DDS liveliness lease applied to the node. Loss → `DeviceStatus.Degraded`. |
| `AllowDynamicTypes` | bool | no | `false` | When `true`, message types missing from the generated codec registry fall back to `rosidl_dynamic_typesupport` (runtime IDL introspection). See [Telemetry — Type Registry](./03-telemetry.md#type-registry). Leave off in production unless you ship customer-specific `.msg` files. |
| `AllowXTypesDiscovery` | bool | no | `false` | When `true` (and `AllowDynamicTypes = true`), the bridge accepts type definitions discovered over the DDS-XTypes wire protocol for messages absent from disk. **Off by default** — wire-supplied types are unauthenticated. See [Composite Messages — Wire-Level Type Discovery](./06-composite-messages.md#wire-level-type-discovery-xtypes). |

### Default QoS

Used when neither `Sensor.Parameters.QoS` nor `DeviceCommunication.QoS` is set:

```json
{
  "Reliability": "Reliable",
  "History":     "KeepLast",
  "Depth":       10,
  "Durability":  "Volatile",
  "Liveliness":  "Automatic"
}
```

| QoS field | Allowed values |
|-----------|----------------|
| `Reliability` | `Reliable`, `BestEffort` |
| `History` | `KeepLast`, `KeepAll` |
| `Depth` | int ≥ 1 |
| `Durability` | `Volatile`, `TransientLocal` |
| `Liveliness` | `Automatic`, `ManualByTopic` |
| `Deadline` | duration string (`"500ms"`, `"1s"`) |

### Publish Allow-List

`PublishTopic` commands are rejected unless the target topic appears here. This is the bridge's analogue of an MQTT topic ACL — without it, a compromised cloud channel could write to any DDS topic on the domain.

```json
"PublishableTopics": [
  { "Topic": "/robot1/display/text", "MessageType": "std_msgs/msg/String" },
  { "Topic": "/robot1/cmd_led",      "MessageType": "std_msgs/msg/Bool"   }
]
```

`CallService` commands are not gated by an allow-list at the parser layer because services are invoked by name and rclnet enforces type matching at bind time; pair this with cloud-side authorisation for set-point services.

## Sensor `Parameters` Schema

Repeated from [Telemetry Interface](./03-telemetry.md#sensor-parameters) for a single source of truth:

| Key | Required | Type | Description |
|-----|----------|------|-------------|
| `Topic` | yes | string | Fully-qualified ROS topic. |
| `MessageType` | yes | string | Canonical ROS type (`<package>/msg/<Name>`). |
| `FieldPath` | no | string | Dotted path into the message. If omitted/empty or resolves to a struct/array, the bridge ships the whole sub-message as `application/json` ([composite mode](./06-composite-messages.md)). |
| `QoS` | no | object | Per-sensor override of the device QoS. |
| `Unit` | no | string | Carried into telemetry metadata; not used by the parser. |

## Full Worked Example

Single robot, four telemetry points (two off the same topic), one publishable LED topic, one cloud-callable mode service:

```json
{
  "SubNode": {
    "Name": "Robot1SubNode",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "Advantech",
    "Model": "Ros2Bridge",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "Robot1": {
      "Enabled": true,
      "DeviceCommunication": {
        "NodeName": "weda_subnode_robot1",
        "DomainId": 7,
        "Transport": "rclnet",
        "RmwImplementation": "rmw_fastrtps_cpp",
        "QoS": {
          "Reliability": "Reliable",
          "History":     "KeepLast",
          "Depth":       10,
          "Durability":  "Volatile",
          "Liveliness":  "Automatic"
        },
        "LivelinessLeaseDuration": "5s",
        "PublishableTopics": [
          { "Topic": "/robot1/display/text", "MessageType": "std_msgs/msg/String" }
        ]
      },
      "Sensors": [
        {
          "Name": "BatteryVoltage",
          "SensorGroup": "AI",
          "Parameters": {
            "Topic": "/robot1/battery_state",
            "MessageType": "sensor_msgs/msg/BatteryState",
            "FieldPath": "voltage",
            "Unit": "V"
          },
          "Report": { "Enabled": true, "Interval": 1000 }
        },
        {
          "Name": "BatteryPercentage",
          "SensorGroup": "AI",
          "Parameters": {
            "Topic": "/robot1/battery_state",
            "MessageType": "sensor_msgs/msg/BatteryState",
            "FieldPath": "percentage",
            "Unit": "%"
          },
          "Report": { "Enabled": true, "Interval": 1000 }
        },
        {
          "Name": "PoseX",
          "SensorGroup": "AI",
          "Parameters": {
            "Topic": "/robot1/pose",
            "MessageType": "geometry_msgs/msg/PoseStamped",
            "FieldPath": "pose.position.x",
            "Unit": "m"
          },
          "Report": { "Enabled": true, "Interval": 200 }
        },
        {
          "Name": "EmergencyStop",
          "SensorGroup": "DI",
          "Parameters": {
            "Topic": "/robot1/safety/estop",
            "MessageType": "std_msgs/msg/Bool",
            "FieldPath": "data",
            "QoS": {
              "Reliability": "Reliable",
              "Durability":  "TransientLocal",
              "Depth":       1
            }
          },
          "Report": { "Enabled": true, "Interval": 100 }
        }
      ]
    }
  }
}
```

## Validation Rules

The bridge implements `ValidateConfigurationUpdateAsync` (Phase 1 of the two-phase update — see `src/Weda.SubNode.Abstractions/Devices/IDevice.cs`). The following are hard validation failures and reject the config before any state change:

| # | Rule | Error code |
|---|------|------------|
| 1 | `NodeName` empty or contains characters disallowed by ROS naming rules. | `Config.InvalidNodeName` |
| 2 | `DomainId` missing or outside `0..232`. | `Config.InvalidDomainId` |
| 3 | `Transport = rosbridge` but `RosbridgeUrl` missing. | `Config.MissingRosbridgeUrl` |
| 4 | Sensor `MessageType` not resolvable by the registry — neither generated nor (when `AllowDynamicTypes = true`) the dynamic typesupport path returns a codec. | `Config.UnknownMessageType` |
| 5a | `FieldPath` is non-empty and `codec.TryResolveField(FieldPath)` returns false. Empty/omitted `FieldPath` is allowed (composite mode, whole message). | `Config.InvalidFieldPath` |
| 5b | A composite field has a leaf the codec cannot map to a DTDL primitive (e.g. opaque blob, unsupported variant). | `Config.UnsupportedCompositeField` |
| 5c | A resolved field is an array and `Sensor.Report.Aggregation` is non-`None` — array aggregation is not implemented in v1. | `Config.UnsupportedAggregation` |
| 6 | Sensor in `SensorGroup ∈ { DI, AI }` configured with `BestEffort` reliability. | `Config.UnsafeQosForGroup` |
| 7 | `PublishableTopics` entry references unknown `MessageType`. | `Config.UnknownMessageType` |
| 8 | Two sensors share `(Topic, MessageType)` but disagree on `QoS`. | `Config.QosConflict` |

Rule 6 implements the SIL2 requirement that any safety-relevant input use reliable transport. Rule 8 prevents silently dropping one of two conflicting QoS profiles when sensors share a subscription.

## Programmatic Configuration

For users following the [Configuration via Code](../04-sensor-configuration/configuration-via-code.md) path, expose a strongly-typed builder (mirroring `TcpModbusDeviceConfiguration`):

```csharp
var cfg = new Ros2DeviceConfiguration
{
    DeviceName = "Robot1",
    NodeName = "weda_subnode_robot1",
    DomainId = 7,
    QoS = Ros2QoS.Default with { Depth = 20 }
};
cfg.AddSensor(new Ros2SensorReporturation
{
    Name = "BatteryVoltage",
    SensorGroup = SensorGroup.AI,
    Topic = "/robot1/battery_state",
    MessageType = "sensor_msgs/msg/BatteryState",
    FieldPath = "voltage",
    Config = { Interval = 1000 }
});
```

The builder serialises into the same shape `Ros2BrokerDevice(IWedaApplicationContext, DeviceConfiguration)` accepts, so JSON and code paths are interchangeable.

## Runtime / Docker

The bridge image must layer on a ROS 2 base. Mirror `examples/opcua-device/Dockerfile` but inherit from a ROS distro:

```dockerfile
FROM ros:humble-ros-base AS runtime
ENV ROS_DOMAIN_ID=7
ENV RMW_IMPLEMENTATION=rmw_fastrtps_cpp

# .NET runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    dotnet-runtime-8.0 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY publish/ ./
ENTRYPOINT ["bash", "-c", "source /opt/ros/humble/setup.bash && dotnet MyRos2Robot.dll"]
```

`docker-compose.yml` snippet for host-network DDS discovery (required when peer robots are on the LAN):

```yaml
services:
  ros2-subnode:
    build: .
    network_mode: host           # DDS discovery is multicast, host-net is simplest
    environment:
      - ROS_DOMAIN_ID=7
      - RMW_IMPLEMENTATION=rmw_fastrtps_cpp
    volumes:
      - ./devicecfg.json:/app/devicecfg.json:ro
```

If host networking is not acceptable, configure FastDDS for unicast peer discovery and mount a `fastdds.xml` profile.

## See Also

- [Telemetry Interface](./03-telemetry.md) — sensor parameter semantics and field extraction.
- [Command Interface](./04-commands.md) — `CallService` / `PublishTopic` payload schemas.
- [Configuration via JSON](../04-sensor-configuration/configuration-via-json.md) — the parent schema this page extends.
- `src/Weda.SubNode.Abstractions/Devices/DeviceConfiguration.cs` — top-level config model.

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 0.1.0 | 2026-05-08 | Kevin Chien | Initial design draft. |
