# TODO — Config Validation Support for ShadowAgent

Assigned to: SubNode SDK developer
Requested by: ShadowAgent team (rain.hu)
Priority: BLOCKING — ShadowAgent cloud-side config validation cannot ship without these changes
Reference SD doc: `~/advantech/documents/wiki/content/Product/WEDA--Microservices/Data-Management(What)/V1.2/SD/SD-Telemetry-Config-Validation.md`

---

## Background

ShadowAgent is adding schema validation to its `UpdateSysConfigAsync` endpoint.
The `Weda.Dtdl` package (`WedaDtValidatorRegistry`) validates incoming `desired.devicecfg`
payloads against DTDL Interfaces published by SubNode.

The existing DTDL coverage is already comprehensive:
- `sensor:base;1`   → Name, SensorGroup, Report, Record, SensorInfo (as ConfigConstraint Properties) ✓
- SensorType Interface → Parameters (as ConfigConstraint Properties) ✓
- DeviceType Interface  → Communication, Properties (as ConfigConstraint Properties) ✓
- Transform / DspFilter / Command → each has its own Interface ✓

Only two things are missing.

---

## Task 1 — Add `minimum: 1` to `interval` fields in `sensor:base` (small fix)

**File:** `src/Weda.SubNode.Abstractions/Telemetry/SensorBase.cs`

`Report.interval` and `Record.interval` are currently declared as plain `"integer"` with no
range constraint. A value of `0` or negative is not a valid sampling/recording interval.

**Change:** add `"minimum": 1` to both fields.

```csharp
// Report schema (around line 80) — BEFORE
Field("interval", "integer", required: false, description: "Sampling interval (ms)."),

// AFTER
Field("interval", "integer", required: false, minimum: 1, description: "Sampling interval (ms)."),
```

```csharp
// Record schema (around line 92) — BEFORE
Field("interval", "integer", required: false, description: "Recording interval (ms)."),

// AFTER
Field("interval", "integer", required: false, minimum: 1, description: "Recording interval (ms)."),
```

You will also need to add the `minimum` parameter to the private `Field(...)` helper:

```csharp
private static JsonObject Field(
    string name, string schema, bool required,
    int? minimum = null,                            // ← add
    string? description = null)
{
    var f = new JsonObject { ... };
    if (minimum.HasValue) f["minimum"] = minimum.Value;   // ← add
    ...
}
```

**Why this matters:** without `minimum: 1`, a caller can submit `Report.Interval = -1` or `0`
and ShadowAgent passes it through to the device. The device then sees a nonsensical poll
interval. This constraint is the most important validation rule in the entire config schema.

---

## Task 2 — Add `DeviceConfigs[]` category to `cap.doc` (BLOCKING)

**Confirmed missing via NATS** (device `54777925790076928`). The published `deviceCap` has
`Commands`, `Devices`, `DspFilters`, `SensorTypes`, `Sensors`, `Transforms` — no `DeviceConfigs`.

Without this, `registry.Validate(deviceId, "MyFirstDeviceConfig", payload)` returns
`SubjectNotFound` because there is no Cap entry mapping the config instance name to a DTMI.

### 2a — Add `DeviceConfigs` to `DeviceCapDto` (non-breaking)

**File:** `src/Weda.SubNode.Abstractions/Cloud/Clients/DeviceManagement/Contracts/ConfigurationUploadRequest.cs`

`DeviceCapDto` is a positional record shared with device-agent. **Do NOT add to the positional
constructor** — that would break all existing call sites and the agreed contract shape.

Instead, add a non-positional `init` property in the record body. This leaves the constructor
signature completely unchanged; the property defaults to `[]` when not set.

```csharp
// Positional constructor — unchanged, do not touch
public record DeviceCapDto(
    ...
    [property: JsonPropertyName("commands")] IReadOnlyList<CatalogRefDto> Commands)
{
    // Non-positional init property: doesn't enter the constructor,
    // defaults to empty list → device-agent sees an empty array or omits the field
    // (depending on serializer settings). Existing new DeviceCapDto(...) calls unaffected.
    [JsonPropertyName("deviceConfigs")]
    public IReadOnlyList<CatalogRefDto> DeviceConfigs { get; init; } = [];
}
```

`CatalogRefDto` is `{ name, dtmi }` — already exists, no new DTO needed.

### 2b — Populate `DeviceConfigs[]` in the mapping layer

**File:** `src/Weda.SubNode.Core/Cloud/Clients/DeviceManagement/Mapping/DeviceConfigurationMappingExtensions.cs`

In `ToDeviceCapabilitiesDto(...)`, add:

```csharp
// Each enabled DeviceConfiguration instance → one CatalogRefDto.
// name  = the DeviceConfigs key (instance name, e.g. "MyFirstDeviceConfig")
// dtmi  = the DeviceType DTMI for that device's type (e.g. "dtmi:advantech:weda:device:tcp-modbus;1")
var deviceConfigs = enabledConfigs
    .Select(cfg =>
    {
        var reg = DeviceTypeRegistry.Get(cfg.DeviceTypeName ?? "");
        return reg is null ? null : new CatalogRefDto(cfg.DeviceName, reg.Dtmi);
    })
    .Where(r => r is not null)
    .Cast<CatalogRefDto>()
    .ToList();

return new DeviceCapDto(
    ...
    Commands: commands)
{
    DeviceConfigs = deviceConfigs    // ← set via object initializer, not positional arg
};
```

`cfg.DeviceName` is the config key (set from `DeviceConfigs` section key during host init).
`reg.Dtmi` is the already-emitted DeviceType DTDL DTMI — no new DTDL emission needed.

---

## Expected result

After these two changes, `cap.doc` will include:

```json
{
  "deviceConfigs": [
    {
      "name": "MyFirstDeviceConfig",
      "dtmi": "dtmi:advantech:weda:device:tcp-modbus;1"
    }
  ]
}
```

And `Weda.Dtdl` will be able to:
1. Resolve `"MyFirstDeviceConfig"` → `dtmi:advantech:weda:device:tcp-modbus;1`
2. Validate `DeviceCommunication` and `Properties` against the DeviceType Interface
3. Validate each `Sensors[]` entry against the sensor's SensorType Interface,
   which inherits `Report`, `Record`, `Name`, `SensorGroup`, `SensorInfo` from `sensor:base;1`
   (and `sensor:base;1` now enforces `interval >= 1` after Task 1)

---

## Verify via NATS after deployment

```bash
# DeviceConfigs[] should now appear in cap.doc
nats req eco1j.weda.<deviceId>.dm.shadow.get.rel \
  | jq '.data.reported.deviceCap.deviceConfigs'

# Report.interval minimum should appear in sensor:base in dtdl.doc
nats req eco1j.weda.<deviceId>.dm.shadow.get.rel \
  | jq '.data.reported.dtdlModel.refModels[]
        | select(.["@id"] == "dtmi:advantech:weda:sensor:base;1")
        | .schemas[]
        | select(.["@id"] | test("Report"))
        | .fields[]
        | select(.name == "interval")'
# Expected: { "name": "interval", "schema": "integer", "minimum": 1, ... }
```
