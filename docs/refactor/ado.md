# DTDL v1.2 — Action items for `weda-node` & `transceiver`

> Companion to [`dtdl-refactor-plan.md`](./dtdl-refactor-plan.md)
> Edge branch: `feature/dtdl-emitter` (commit `44129af` and forward)
> Status: Edge side done & green; cloud side action items below

---

## Context: what changed on edge

`ConfigurationUploadRequest` (NATS subject `eco1j.weda.dm.cfg.update.req`) **v1.1 → v1.2**:

```diff
 data: {
   "deviceId": "...",
-  "dtdl":       <Interface object — only deviceConfigs[0].sensors>,
+  "dtdl":       <Interface object — flatten(all deviceConfigs::sensors)>,
+  "refModels":  [ <Interface>, <Interface>, ... ],         // NEW
   "deviceCapabilities": {
     ...,
-    "sensors": [{ resourceId, dtmi, name, sensorGroup, deviceResourceId }]
+    "sensors": [{ resourceId, dtmi, name, sensorGroup, deviceResourceId, unit }]  // unit NEW
   }
 }
```

Key invariants:

- `dtdl` is one DTDL v3 `Interface` (the SubNode wrapper). Maps to `DtdlModel.DeviceModel`.
- `refModels` is the typed catalog (Sensor:base, device-type Interfaces, sensor-type Interfaces, transform / DSP / command parameter Interfaces). Maps to `DtdlModel.RefModels`. Dedup by `@id`.
- `sensors[i].dtmi` is the **Telemetry `@id`** inside `dtdl.contents` — NOT a sensor-type Interface DTMI. The sensor-type linkage lives in `sensorTypes[]` → `refModels[]`.
- `sensors[i].unit` is new. Core DTDL v3 doesn't define `unit` on a bare Telemetry (DTDLParser rejects), so the value moved from the DTDL Telemetry to the SensorDto.
- `dtdl.contents[]` carries **only** `Telemetry` items. No `unit`, no `minValue`/`maxValue`/`pattern` (ConfigStraints) on Telemetry. Those would all fail `DTDLParser` validation under core DTDL v3.

Full design rationale lives in [`dtdl-refactor-plan.md`](./dtdl-refactor-plan.md).
Full cloud-side handshake doc (rationale + tests + open coordination) lives at `docs/.dev/refactor/cloud-team-handshake.md` (not committed; ask edge team for a copy if needed).

---

## For `weda-node` engineers

### Scope

`weda-node`'s server-side `ConfigurationUploadRequest` (the C# DTO that deserializes the NATS payload from edge) **must add the `RefModels` field** so it stops dropping the typed catalog on the floor.

### Acceptance criteria

1. **`ConfigurationUploadRequest.Data.RefModels: IReadOnlyList<JsonObject>` (or equivalent JSON-typed collection) deserializes from the wire.**
   - Field name: `refModels` (lowercase camel)
   - JSON shape: array of DTDL v3 Interface objects (full Interface, not just `@id` references)
   - Order matches edge emission: Sensor:base → device types → sensor types → transforms → DSP filters → command schemas. Dedup'd by `@id`.

2. **`ConfigurationUploadRequest.Data.Dtdl` remains a single `JsonObject`** (mapped to `DtdlModel.DeviceModel`). No type change needed if it's already a `JsonObject`; if today it's typed as `IReadOnlyList<JsonObject>` (mirroring an in-flight edge intermediate), revert to single object.

3. **`SensorDto`** (the type behind `Data.DeviceCapabilities.Sensors[]`) **gains an `Unit: string?` field**.

4. **Both `Dtdl` and `RefModels` must be persisted / fanned out** to whatever downstream consumer needs them (e.g., shadow store, transceiver via `eco1j.{group}.{deviceId}.dm.shadow.dtdl.doc`). Today only `DeviceModel` reaches `UpdateDtdlModelHandler`; `RefModels` need a parallel path.

5. **`SensorDto.Unit` must flow into the KV blob** that transceiver reads (`ServiceConfig.DeviceSensorKVStore` → `DeviceMgmtSensorDto.Unit`). Add the field on `DeviceMgmtSensorDto` if it's missing.

### Verification

Boot edge `examples/testdevice` against a weda-node + transceiver dev stack, publish to `eco1j.weda.dm.cfg.update.req`, and:

- The `RefModels[]` array round-trips into the cloud (visible in shadow / debug logs).
- `dtdl-doc` event fires for the wrapper Interface AND each refModel.
- A custom `[DeviceCmd]` declared on the edge SubNode resolves to a valid command via transceiver (after transceiver's own fixes — see below).

### Why this is non-negotiable

Without `RefModels` deserialization on the weda-node side, every typed sensor's POCO schema, every transform/DSP parameter schema, and every command schema is dropped silently. Front-end can't render configuration forms; Shadow can't validate desired-state; custom `[DeviceCmd]` validation falls through to a hardcoded 4-command allowlist.

---

## For `transceiver` engineers

### Scope (ADO parent **#37106**)

Two coupled problems that BOTH need to be solved before transceiver consumes v1.2:

1. **Command validation is wired to `sensor.Dtmi`** — but in v1.2 (and arguably already in v1.1's intent), `sensor.Dtmi` is a Telemetry `@id`, not an Interface DTMI. Commands belong in `refModels[]`, not in per-sensor lookups.
2. **`UpdateDtdlModelHandler.HandleInputEventCore` pushes refModels into the parser cache one at a time** via `UpdateCacheAsync(jsonText)`. If a sensor-type Interface lands before its `Sensor:base`, `extends` can't resolve. Cleanest fix is one batch `ParseAsync(IEnumerable<string>)` call.

### Children

| ADO | Title | Est | Notes |
|---|---|---|---|
| #37107 | Design — choose command lookup source (parser cache enumerate vs new KV blob vs refModels index) | 3h | Trade-off doc. Gates #37108/#37109. |
| #37108 | Refactor `SubnodeCommandValidationService` — drop sensor iteration | 5h | Replace `foreach(sensors)` + `ExtractCommandsFromDtdlAsync(sensor.Dtmi)` with the lookup chosen in #37107. |
| #37109 | Refactor `SystemCommandValidationService` — same pattern | 4h | Identical bug shape in [`SystemCommandValidationService.cs:116`](../../../transceiver/DigitaltwinTransceiver/src/DigitaltwinTransceiver.Domain/SystemCommands/Services/SystemCommandValidationService.cs#L116). Share helper. |
| #37110 | Update `SystemCommandServiceTests` — drop Interface-DTMI workaround | 3h | Remove `dtmi:Advantech:AIIS_3410P;1` injection (was a workaround that exploited the bug); replace with Telemetry @id matching the v1.2 contract. |
| #37111 | Add positive tests — `sensor.Dtmi = Telemetry @id` with `[DeviceCmd]` commands resolved | 3h | Net-new coverage of the actual happy path. |

### Acceptance criteria

1. **`SubnodeCommandValidationService.IsValidCommandAsync(deviceId, cmd)`** resolves a custom `[DeviceCmd]` command shipped by edge in `refModels[]` — without looking at `sensor.Dtmi`.

2. **`SystemCommandValidationService.IsValidCommandAsync`** same.

3. **`UpdateDtdlModelHandler.HandleInputEventCore`** (recommended, not strict) calls `ParseAsync(allModelsAsBatch)` instead of `UpdateCacheAsync(...)` per model — eliminates the cross-batch `extends` ordering hazard. Confirmed safe by [`V12DtdlParserFeasibilityTests.SensorTypeInterface_Extends_SensorBase_ResolvesAcrossRefModelsEntries`](../../tests/Weda.SubNode.Core.Tests/V12Feasibility/V12DtdlParserFeasibilityTests.cs) on edge side.

4. **`DeviceMgmtSensorDto.Unit`** added so transceiver can read the new field that weda-node will provide (see weda-node section above). Source it from `Sensor.Unit` once available.

5. **No `AllowUndefinedExtensions` change needed for the wrapper.** Edge's `DtdlGenerator` no longer emits `unit` on the bare Telemetry (it moved to `SensorDto.Unit`), so `FileSystemDtmiResolver.ParsingOptions` as-is accepts the wrapper. Verified by [`V12DtdlParserFeasibilityTests.RealisticAutogenInterface_NoUnit_ParsesCleanlyUnderDefaultParserOptions`](../../tests/Weda.SubNode.Core.Tests/V12Feasibility/V12DtdlParserFeasibilityTests.cs).

### Why this is non-negotiable

Today's `IsValidCommandAsync` silently falls through to a hardcoded `report.*` / `system.*` allowlist whenever the DTDL command lookup fails — which it ALWAYS does under v1.2 (and arguably already does under v1.1). Edge SubNodes that declare `[DeviceCmd]`s **cannot get their custom commands executed via the cloud** until this fix lands. Tests for this fall-through path don't exist in transceiver today, so the issue has been masked.

### What edge already proved on its side

11 feasibility tests under [`tests/Weda.SubNode.Core.Tests/V12Feasibility/`](../../tests/Weda.SubNode.Core.Tests/V12Feasibility/) pin:

- 5 enrichment tests: v1.2 payload survives transceiver's `TelemetryEnrichmentManager` replay; `sensor.Dtmi = Telemetry @id` flows through; multi-device flatten produces no dummies; missing-sensor still becomes dummy not drop.
- 6 DTDLParser tests: wrapper + refModels batch-parse with default `ParsingOptions`; `extends Sensor:base` resolves; `[DeviceCmd]` command Interfaces accessible; Enum-based ConfigStraints survive; regression guard against re-adding `unit` to Telemetry.

These are reproducible in the edge_subnode test project — feel free to mirror the fixtures on transceiver side if you want to assert the cloud reads them as expected.

---

## Coordination contacts

- **Edge SubNode contact**: ask in #subnode-eng (or current channel) for `feature/dtdl-emitter` branch questions.
- **Plan / design rationale**: [`dtdl-refactor-plan.md`](./dtdl-refactor-plan.md) §2.5, §4.5, §4.6.
- **Test artifacts**: edge_subnode commit `44129af` and forward.
