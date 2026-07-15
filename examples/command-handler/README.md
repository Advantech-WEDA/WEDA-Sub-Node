# Command Handler Example

Demonstrates how to add **custom command execution handlers** to a SubNode
application. It defines two commands:

- `sensor.read` — reads the latest cached telemetry value of a named sensor
  and returns it in the command response.
- `tag.set` — attaches metadata tags to a named sensor; its parameters DTO
  carries a **map-typed** payload (`Dictionary<string, string>`), which the
  SDK emits as a DTDL v3 `Map` schema in the command Interface.
- `props.get` — queries runtime device properties; **both sides** of the
  handler are `Dictionary<string, T>` maps: a dynamic option bag as input
  (string → string) and a device-name-keyed map as result data
  (string → `DevicePropsEntry`).

The project runs against the embedded Modbus TCP simulator and dispatches
demo commands through the real SDK command pipeline a few seconds after
startup.

## What it shows

| Concern | File |
|---|---|
| Command definition + parameters (`[DeviceCmd("sensor.read")]`) | `Commands/SensorRead/Models/SensorReadCommand.cs` |
| Map-typed parameters (`Dictionary<string, string>` → DTDL `Map`) | `Commands/SetSensorTags/Models/SetSensorTagsCommand.cs` |
| Maps on both sides (dict input + dict result data) | `Commands/GetDeviceProps/` |
| Structured result (`IResult` + `CommandStatusCode`) | `Commands/SensorRead/Models/SensorReadResult.cs` |
| Custom validators (`ICommandValidator<T>`) | `Commands/*/…CommandValidator.cs` |
| Execution handlers (`ICommandHandler<TCommand, TResult>`) | `Commands/*/…CommandHandler.cs` |
| Simulated cloud→device command dispatch | `Services/CommandDemoService.cs` |

## How command handling works

1. **Define the command** as a class implementing `ICommand` (easiest:
   inherit `CommandData<TParameters>`) and mark it with
   `[DeviceCmd("sensor.read")]`. The attribute value is matched against the
   `deviceCmd` field of the incoming payload.
2. **Implement the handler** as `ICommandHandler<SensorReadCommand, SensorReadResult>`.
   Pipeline behaviors are opted in via attributes on the handler class:
   - `[Validation(typeof(SensorReadCommandValidator))]` — custom validation
   - `[Logging(LogLevel.Information)]` — before/after/error logging
   - `[AutoAck(false)]` — only if the handler sends its own initial ack
3. **Registration is automatic.** At startup, `CommandRegistry` scans the
   entry assembly for `ICommandHandler` implementations and registers them by
   the command type's `[DeviceCmd]` name. No wiring code is needed — the
   handler in this project is discovered just by existing in the assembly.
4. **Dispatch** follows this pipeline (see `CommandDispatcher`):
   DTDL payload validation → deserialization → auto-ack (`Received`) →
   DataAnnotation validation → behaviors (validator, logging) → handler →
   command response (`result` with `Status`/`ResultData`) to `respTopic`.

In production the command arrives from the WedaNode over NATS. The mock cloud
has no command transport, so `CommandDemoService` builds the same
`CommandMessage` envelope the cloud would publish and feeds it to
`CommandDispatcher` directly.

## Command payload

```json
{
  "deviceCmd": "sensor.read",
  "timeout": 10,
  "respTopic": "eco1j.weda.{deviceId}.subnode.cmd.rsp",
  "parameters": {
    "deviceName": "MyFirstDevice",
    "sensorName": "temperature_sensor"
  }
}
```

`deviceName` is optional — when omitted, all registered devices are searched
for the sensor.

Successful response `data`:

```json
{
  "deviceCmd": "sensor.read",
  "msgType": "result",
  "status": 0,
  "message": "Sensor value read successfully",
  "resultData": {
    "deviceName": "MyFirstDevice",
    "sensorName": "temperature_sensor",
    "value": 24.57,
    "displayValue": "24.57616",
    "timestamp": 1783500407221,
    "unit": "celsius"
  }
}
```

Error cases return `status` = `CommandStatusCode.NotFound` (9, unknown
device/sensor or no cached data), `Timeout` (3), or `HardwareError` (4).

## Map-typed parameters: tag.set

`tag.set` shows how to declare a command parameter DTO with a **map type**.
The `tags` property is a `Dictionary<string, string>`, which the SDK's schema
emitter turns into a DTDL v3 `Map` schema (`mapKey: string`,
`mapValue: string`) inside the command Interface uploaded to the cloud — and
the DTDL step-0 validation in `CommandDispatcher` validates incoming payloads
against it:

```csharp
public class SetSensorTagsParameters
{
    [JsonPropertyName("tags")]
    [Required]
    [MinLength(1)]
    public Dictionary<string, string> Tags { get; init; } = [];
    // ...
}
```

Command payload:

```json
{
  "deviceCmd": "tag.set",
  "timeout": 10,
  "respTopic": "eco1j.weda.{deviceId}.subnode.cmd.rsp",
  "parameters": {
    "deviceName": "MyFirstDevice",
    "sensorName": "temperature_sensor",
    "tags": {
      "location": "line-3",
      "zone": "assembly"
    }
  }
}
```

The handler merges the tags into the sensor's runtime `Metadata` (building a
new dictionary and swapping it in, so concurrent readers never observe a
half-merged map) and echoes the applied map back in `resultData.tags`.
Only `Dictionary<string, T>` (string keys) is supported for DTDL Map
emission — see `docs/refactor/typed-poco-dtdl-emit.md`.

## Maps on both sides: props.get

`props.get` uses `Dictionary<string, T>` for **both** the command input and
the result data of `ICommandHandler<TCommand, TResult>`:

```csharp
// Input: dynamic option bag (keys not fixed at compile time)
public class GetDevicePropsParameters
{
    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; init; } = [];
}

// Result data: dynamic keys (one per device), concrete value type
public Dictionary<string, DevicePropsEntry>? ResultData { get; init; }
```

Command payload and response:

```json
{
  "deviceCmd": "props.get",
  "parameters": { "options": { "deviceName": "MyFirstDevice", "includeSensors": "true" } }
}
```

```json
{
  "status": 0,
  "resultData": {
    "MyFirstDevice": {
      "deviceName": "MyFirstDevice",
      "subNodeType": "AdamEthernet",
      "enabled": true,
      "sensorCount": 1,
      "sensors": [ { "name": "temperature_sensor", "group": "TEMP", "intervalMs": 1000, "unit": "celsius" } ]
    }
  }
}
```

### DTDL emission constraints for dictionaries

The registry emits a DTDL Interface for every command at startup
(`CommandRegistry.ScanAssembly`), and `Weda.Dtdl`'s emitter enforces:

1. **A dictionary must be a map-typed property, not the parameter type
   itself.** `class MyCommand : CommandData<Dictionary<string, object>>`
   throws `WedaDtdlEmissionException` at startup — the emitter reflects the
   dictionary's own members (e.g. `Comparer`, an interface type) as if it
   were a POCO. Wrap the map in a parameters DTO instead.
2. **Map values must be a concrete type** (`string`, `double`, a POCO, …).
   `Dictionary<string, object>` cannot be emitted — DTDL has no "any"
   schema. For dynamic values, use string values or a typed entry POCO.

`CommandRegistrationTests` in the test project guards both rules: it scans
this assembly the same way the app does at startup, so a schema-breaking
command type fails the test suite instead of crashing the device at boot.

## Run

```bash
cd examples/command-handler
dotnet run
```

Expected log markers:

```
Modbus TCP Simulator started successfully
MOCK CLOUD SERVICE ACTIVE
temperature_sensor: 25.1
Dispatching demo command 'sensor.read' (registered commands: [..., sensor.read])
>>>>> Executing command SensorReadCommand
sensor.read completed: MyFirstDevice/temperature_sensor = 24.57616 @ ...
Demo command 'sensor.read' succeeded: {"Status":0,...}
```

## Tests

Unit tests live in `tests/CommandHandlerExample.Tests` and cover the handler
(success, device/sensor not found, empty cache, device fault) and the
validator:

```bash
dotnet test tests/CommandHandlerExample.Tests/CommandHandlerExample.Tests.csproj
```

## Adapting to your project

1. Copy the `Commands/` folder structure and rename command/handler/result.
2. Pick a `deviceCmd` routing name (`"<cmdType>.<subCmd>"` convention, e.g.
   `"pump.start"`).
3. Keep result properties concretely typed (no `object` members) — the
   registry emits a DTDL schema for the command from the parameter and result
   types, and the schema is uploaded to the cloud as part of the SubNode's
   capabilities.
4. Return failures through the result's `Status`/`Message` (the response
   reaches the cloud); reserve thrown exceptions for unexpected faults.
