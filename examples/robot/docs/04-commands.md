---
sidebar_position: 4
sidebar_label: 'Command Interface'
hide_title: true
title: 'ROS 2 Bridge — Command Interface | SubNode SDK'
keywords: ['SubNode', 'ROS 2', 'Command', 'Service', 'Action', 'DeviceCommand', 'SetMode']
description: 'How cloud-issued DeviceCommand instances are dispatched to ROS 2 services or topic publishes, including error mapping and timeout handling.'
---

# ROS 2 Bridge — Command Interface

> Cloud-issued `DeviceCommand` payloads are dispatched by the parser to a ROS 2 **service** (preferred) or **topic publish** (escape hatch). Actions are deferred to v2.

## Why Service First

The SDK's command contract is request/response. `IPubSubProtocolParser.ExecuteCommandAsync` returns `ErrorOr<object>` (`src/Weda.SubNode.Abstractions/Protocols/IPubSubProtocolParser.cs`). ROS 2 services match this shape directly:

| Concern | ROS 2 Service | ROS 2 Topic Publish |
|---------|---------------|---------------------|
| Typed response | ✅ — `srv` defines request + response | ❌ — fire-and-forget |
| Availability check | ✅ — `WaitForServiceAsync` | ❌ — silent if no subscriber |
| Timeout semantics | ✅ — request awaitable with `CancellationToken` | ❌ — `publish()` returns immediately |
| Backpressure | n/a | publish queue may drop |
| Fits SIL2 fail-safe pattern | ✅ | ❌ — must add app-level ack |

**Rule:** any cloud-issued command that can change a control set-point or safety mode **must** use the service path. Topic publishes are reserved for low-stakes broadcasts (e.g. updating a display string).

## Command Dispatch Table

`Ros2PubSubParser.ExecuteCommandAsync` switches on `command.DeviceCmd`:

| `DeviceCmd` | Path | Returns | Notes |
|-------------|------|---------|-------|
| `"CallService"` | ROS service client | Service response object on success; `Error.Failure` on timeout/unavailable/exception | Default for any control command. |
| `"PublishTopic"` | `Ros2Communication.PublishAsync` | `{ success: true, topic, bytes }` | No ack. Use only when fire-and-forget is acceptable. |
| `"SendGoal"` (v2) | Action client | Goal handle | Not implemented in v1. |
| anything else | — | `Error.Failure("Command.NotSupported", ...)` | Mirrors `ISensingPubSubParser` behaviour. |

## `CallService` Payload

The cloud sends a standard `DeviceCommand` (`src/Weda.SubNode.Abstractions/Commands/Contracts/DeviceCommand.cs`). The bridge expects these `Parameters`:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service` | string | yes | Fully-qualified ROS service name, e.g. `/robot1/set_mode`. |
| `serviceType` | string | yes | Canonical service type, e.g. `robot_msgs/srv/SetMode`. Must be in the type registry. |
| `request` | object | yes | Request fields, JSON-shaped. Bridge serialises to CDR per `serviceType`. |

### Example

Cloud → SubNode payload:

```json
{
  "deviceCmd": "CallService",
  "respTopic": "weda/.../resp",
  "timeout": 10,
  "service": "/robot1/set_mode",
  "serviceType": "robot_msgs/srv/SetMode",
  "request": { "mode": "AUTO" }
}
```

Parser flow:

1. Validate parameters → `Error.Validation("CallService.MissingService", ...)` if any missing (mirrors `ISensingPubSubParser.ExecuteDigitalOutputControlAsync`).
2. Resolve `serviceType` from `Ros2TypeRegistry`. Unknown type → `Error.Failure("Command.UnknownServiceType", ...)`.
3. `await client.WaitForServiceAsync(timeout)`. Unavailable → `Error.Unavailable("Command.ServiceUnavailable", ...)`.
4. `await client.SendRequestAsync(request, ct)` with `ct = command.Timeout`. Cancellation → `Error.Failure("Command.Timeout", ...)`.
5. On success, return the deserialised response object — the framework serialises it back to the cloud.

## `PublishTopic` Payload

Same envelope, different parameters:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `topic` | string | yes | Target topic. Must already be configured for publish (see below). |
| `messageType` | string | yes | Type registered for this topic. |
| `message` | object | yes | Message fields, JSON-shaped. Serialised to CDR. |

A **publish-allowed-list** is required: only topics declared in `DeviceCommunication.PublishableTopics` can be targeted. This is the analogue of MQTT's per-topic ACL — without it, a compromised cloud channel could publish to any DDS topic on the domain.

```json
"DeviceCommunication": {
  "NodeName": "weda_subnode_robot1",
  "DomainId": 0,
  "PublishableTopics": [
    { "Topic": "/robot1/display/text", "MessageType": "std_msgs/msg/String" }
  ]
}
```

A publish call to a topic not in the list returns `Error.Forbidden("Command.TopicNotPublishable", ...)`.

## Error Mapping

The bridge uses the existing `ErrorOr` codes from `IPubSubProtocolParser` so that the cloud-side command pipeline doesn't need ROS-specific handling.

| Condition | Error code | When emitted |
|-----------|-----------|--------------|
| Required parameter missing | `Command.MissingParameter` | Validation, before any DDS call |
| Service / message type unknown | `Command.UnknownServiceType` / `Command.UnknownMessageType` | After parameter parse, before DDS call |
| Service not present on domain | `Command.ServiceUnavailable` | `WaitForServiceAsync` timeout |
| Service call exceeded `timeout` | `Command.Timeout` | `OperationCanceledException` from token |
| Topic not in publish allow-list | `Command.TopicNotPublishable` | Validation |
| DDS / rcl native error | `Command.PublishFailed` | Caught generic `Exception` (mirrors `ISensingPubSubParser` pattern) |
| Unknown `DeviceCmd` | `Command.NotSupported` | Default switch arm |

## Watchdog and Cancellation

Every command flows under a single `CancellationToken` derived from `command.Timeout` and the framework's parent token. The parser must not perform any blocking call without a token:

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
cts.CancelAfter(TimeSpan.FromSeconds(command.Timeout > 0 ? command.Timeout : 30));

var response = await client.SendRequestAsync(request, cts.Token);
```

Failure to thread the token has been the root cause of two prior incidents in MQTT command paths — the bridge implementation must satisfy `/safety-validate` before merge.

## Idempotency

ROS services are not idempotent by default. The bridge attaches `command.SeqId` and `command.ReqSeqId` (already on `DeviceCommand`) to the request as metadata when the service type contains a `string request_id` field. Robot teams that need at-least-once-with-dedupe semantics should reserve that field in their `.srv` files; the bridge will populate it automatically.

For commands without a `request_id` field, the bridge logs a warning at first use and proceeds — it does not silently retry.

## Worked Example — Set Robot Mode

End-to-end flow assuming the robot already runs a service `/robot1/set_mode` of type `robot_msgs/srv/SetMode { string mode → bool success, string message }`:

1. Cloud sends `DeviceCommand` with `deviceCmd = "CallService"` and the parameters shown above.
2. `CommandPipeline` routes to `Ros2Device.HandleCommandAsync` (inherited from `DeviceBase`), which calls `_parser.ExecuteCommandAsync(command, ct)`.
3. Parser:
   - `Ros2TypeRegistry.GetService("robot_msgs/srv/SetMode")` → typed request/response codecs.
   - `client.WaitForServiceAsync(2 s)` → ready.
   - `client.SendRequestAsync({ mode = "AUTO" }, cts.Token)` → response `{ success = true, message = "switched" }`.
4. Returns `ErrorOr<object>.Value = { success = true, message = "switched" }`.
5. Framework serialises to `CommandResponse` and publishes on `respTopic`.

If the service is offline, the cloud sees `Command.ServiceUnavailable` within `timeout` seconds — no half-complete state.

## Action Support (Deferred)

Actions add three flows that don't fit `IPubSubProtocolParser`:

- Goal accept/reject (separate from final result).
- Periodic feedback during execution.
- Cancel goal mid-flight.

These map naturally onto `IStreamingCommunication<TRequest, TResponse>` (`src/Weda.SubNode.Abstractions/Communication/ICommunication.cs`). v2 plan: introduce `Ros2ActionDevice : StreamingDeviceBase` alongside `Ros2Device`. Same `Ros2Communication` instance, same node — different parser.

Until then, long-running operations should be modelled as a service that returns a goal id, with progress polled via a separate sensor.

## See Also

- [Telemetry Interface](./03-telemetry.md) — inbound side of the same parser.
- [Configuration Reference](./05-configuration.md) — `PublishableTopics` and validation rules.
- `src/Weda.SubNode.Abstractions/Commands/Contracts/DeviceCommand.cs` — envelope schema.
- `src/Weda.SubNode.Core/Protocols/ISensing/ISensingPubSubParser.cs` — reference command-dispatch implementation.

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 0.1.0 | 2026-05-08 | Kevin Chien | Initial design draft. |
