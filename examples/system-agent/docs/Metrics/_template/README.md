# Metric Template — How to Add a New Metric Type

Use this template when you need to add a brand-new `MetricType` (or a new family of `MetricType`s) to the System Agent. It produces the same three-file layout used by `01`–`05`:

1. **`NN_<NAME>_FIELDS.md`** — the configuration field reference (consumed by config authors).
2. **`NN_<NAME>.dtdl.json`** — the DTDL v2 Interface (consumed by cloud-side / SDK code).
3. **`NN_<NAME>_USAGE.md`** — the .NET / C# consumption guide (consumed by integration engineers).

Use the existing metric docs (`01_CPU_NETWORK_*`, `02_MEMORY_DISK_SYSTEM_GPU_*`, etc.) as worked examples whenever the template's placeholders feel ambiguous.

---

## Step-by-step

1. **Pick a number and name.** Files are numerically prefixed (`NN_`) for sort order. Use the next free number. The `<NAME>` is `SNAKE_UPPER` and groups all metric types covered by the file. Examples:
   - One MetricType per file: `06_MODBUS`
   - Multiple related MetricTypes per file: `07_BUS_AND_RPC` (covers `bus`, `rpc`, `mqtt` together)

2. **Copy the three template files** into `docs/Metrics/`:
   ```
   _template/TEMPLATE_FIELDS.md     →  NN_<NAME>_FIELDS.md
   _template/TEMPLATE.dtdl.json     →  NN_<NAME>.dtdl.json
   _template/TEMPLATE_USAGE.md      →  NN_<NAME>_USAGE.md
   ```

3. **Replace the placeholders.** Every placeholder is wrapped in `{{ … }}` so it's grep-friendly. Replace from this single source-of-truth table — never split it across files.

   | Placeholder | Meaning | Example |
   |-------------|---------|---------|
   | `{{NN}}` | Two-digit file number. | `06` |
   | `{{NAME_UPPER}}` | File-name suffix in `SNAKE_UPPER`. | `MODBUS` |
   | `{{Name_Pascal}}` | Same idea in PascalCase, used for the Interface DTMI. | `Modbus` |
   | `{{name_human}}` | Free-form human name for doc titles. | `Modbus` |
   | `{{metric_type_1}}` … `{{metric_type_N}}` | The lowercase `MetricType` string(s) you're introducing. | `modbus_tcp`, `modbus_rtu` |
   | `{{MetricType1Pascal}}` … | PascalCase of each MetricType — used for DTDL Enum `name`, Telemetry DTMIs, C# class names. | `ModbusTcp`, `ModbusRtu` |
   | `{{metric_name_a}}`, `{{metric_name_b}}`, … | Lowercase `MetricName`s within each MetricType. | `coil_value`, `holding_register` |
   | `{{schema_a}}`, `{{schema_b}}`, … | DTDL schema for each MetricName (`double`, `long`, `integer`, `boolean`, `string`). | `integer` |
   | `{{sensor_group}}` | Conventional `SensorGroup` for this metric type (`SYS`, `TEMP`, `PWR`, `DI`, `DO`, `AI`, `AO`). | `AI` |
   | `{{extra_param_name}}` | Name of any required extra `Parameters.*` field (e.g., `MountPoint`, `Interface`, `Source`, `PinId`). Omit when N/A. | `UnitId` |
   | `{{expansion_param_singular}}` / `{{expansion_param_plural}}` | If the type supports v1.1 expansion, the singular + plural parameter names. | `Interface` / `Interfaces` |
   | `{{platform}}` | Hardware / OS dependency note, or `cross-platform`. | `Linux only` |

4. **Decide which optional sections to keep.** Some metric types don't need every section:
   - **No v1.1 expansion?** Delete the *Resolution Modes* and *expansion-aware examples* sub-sections from `FIELDS.md` and the corresponding discriminator parameter from the validator in `USAGE.md`.
   - **Cross-platform?** Delete the *Platform Dependency* section at the bottom of `FIELDS.md`.
   - **No semantic type fits?** Leave Telemetries as plain `@type: "Telemetry"`. DTDL v2 supported semantic types are listed at the bottom of this README.
   - **Boolean-only metric?** Drop the `MonotonicCounterValidator` references in `USAGE.md`; keep only enum-style predicates.

5. **Validate the DTDL** before committing:
   ```bash
   python3 -c "import json; json.load(open('docs/Metrics/{{NN}}_{{NAME_UPPER}}.dtdl.json'))"
   ```
   And, if you have the parser installed in a CI step:
   ```bash
   dotnet test --filter "FullyQualifiedName~{{Name_Pascal}}DtdlTests"
   ```

6. **Wire the new Interface into the agent**:
   - Add a `using SystemAgentExample.Schema;` import wherever sensors get dispatched.
   - Add the `{{Name_Pascal}}Model.LoadAsync()` call alongside the other DTDL loads in `Program.cs` (see `05_HARDWARE_FEATURE_USAGE.md` §10 for the canonical five-Interface wiring).
   - Add a row to the schema-compatibility matrix in `docs/05_DEVICECFG.md` §20 (the top-level reference) if it lists every `(MetricType, MetricName) → schema` mapping.

7. **Update the top-level reference.** `docs/05_DEVICECFG.md` has a per-`MetricType` section for every type. If you added a new `MetricType`, add a corresponding section there with the same `Identity` / `parameters` / `report` / `sensorInfo` four-block structure used by §6–§19.

---

## Checklist before opening a PR

- [ ] Three files exist: `NN_<NAME>_FIELDS.md`, `NN_<NAME>.dtdl.json`, `NN_<NAME>_USAGE.md`.
- [ ] No `{{…}}` placeholder remains in any of the three files.
- [ ] DTDL JSON parses (`python3 -c "import json; json.load(open(...))"`).
- [ ] DTDL parses through the official Microsoft parser (`new ModelParser().ParseAsync(...)` does not throw).
- [ ] Each Telemetry has a `comment` field carrying the validation rule (range / monotonicity / enum / identity-drift).
- [ ] `schemas[]` exposes at least: `MetricType`, one `<Type>MetricName` per included MetricType, `SensorGroup`, `SensorInfoSchema`. Add more reusable Enums when the type warrants it (see `GpioPinLevel` in `05_HARDWARE_FEATURE.dtdl.json`).
- [ ] All MetricName values that should map to a particular `schema` appear in `BuildExpectedSchemaMap()` in the USAGE doc's validator example.
- [ ] If the type is platform-dependent, the *Platform Dependency* note explains the null-suppression contract.
- [ ] If the type supports v1.1 expansion, the FIELDS doc has the three resolution-mode examples (Bound / Explicit list / Auto-detect) and the USAGE doc shows the `discriminator` suffix on `ResourceId`.
- [ ] `docs/05_DEVICECFG.md` schema matrix (if you updated it) lists every new `(MetricType, MetricName) → schema` row.

---

## DTDL v2 semantic-type cheat sheet

When a Telemetry value has a natural physical unit, add the semantic type to `@type` and set `unit` accordingly. The Azure IoT Explorer and most Plug-and-Play tooling auto-format values that carry a unit. Common candidates:

| Domain | Semantic type | Sample units |
|--------|---------------|--------------|
| Temperature | `Temperature` | `degreeCelsius`, `degreeFahrenheit`, `kelvin` |
| Voltage | `Voltage` | `volt`, `millivolt`, `kilovolt` |
| Current | `Current` | `ampere`, `milliampere` |
| Power | `Power` | `watt`, `kilowatt`, `horsepower` |
| Energy | `Energy` | `joule`, `kilowattHour` |
| Frequency | `Frequency` | `hertz`, `kilohertz`, `megahertz` |
| Data size | `DataSize` | `bit`, `byte`, `kibibyte`, `mebibyte`, `gibibyte` |
| Data rate | `DataRate` | `bytePerSecond`, `kibibytePerSecond`, `mebibytePerSecond` |
| Length | `Length` | `metre`, `kilometre`, `millimetre`, `inch`, `foot` |
| Mass | `Mass` | `gram`, `kilogram`, `tonne`, `pound`, `ounce` |
| Velocity | `Velocity` | `metrePerSecond`, `kilometrePerHour`, `milePerHour` |
| Pressure | `Pressure` | `pascal`, `kilopascal`, `bar`, `pound-force per square inch` |
| Time span | `TimeSpan` | `second`, `minute`, `hour`, `day` |
| Volume | `Volume` | `litre`, `millilitre`, `cubicMetre`, `gallon` |
| Humidity | `RelativeHumidity` | `unity` (0–1) — there is no separate `%` unit |

If the natural unit isn't on the list (RPM, packets, dimensionless ratios, counts), leave the Telemetry as plain `"@type": "Telemetry"` and describe the unit in `description` + `comment`.

---

## Anti-patterns to avoid

- **Mixing wire schema and Enum schema.** If a Telemetry value is on-the-wire `integer`, set `schema: "integer"`. Don't set `schema` to an Enum DTMI even though DTDL allows it — the agent's `SensorInfo.Schema` ↔ DTDL schema check breaks. Use Enums in `schemas[]` for consumer-side label resolution (see `GpioPinLevel`).
- **Reusing a DTMI for an incompatible change.** If you remove or downgrade a Telemetry's schema, increment the Interface DTMI version (`;1` → `;2`). Additive changes (new Telemetries, new Enum values) can stay on `;1` per DTDL v2 compatibility rules.
- **Hard-coding the `MetricType` string in C# call sites.** Use the `Enums.MetricType` DTMI + `GetStringEnumValues(...)` to drive routing logic — that way the C# stays in lock-step with the DTDL.
- **Writing PascalCase in the `enumValue` field.** `enumValue` is the on-the-wire string matching `devicecfg.json`; it stays `lower_snake_case`. The `name` field is the DTDL identifier and uses `PascalCase`.
- **Forgetting the SIL2 fail-safe.** Unsupported / missing readings should be **suppressed** (null), never published as `0`. Document this in the Telemetry's `comment` and enforce it in the publisher example.

---

## Related references

- `05_DEVICECFG.md` (top level) — the canonical schema reference; mirrors the four-block (`identity` / `parameters` / `report` / `sensorInfo`) layout this template uses.
- `docs/v1.1/METRIC-TYPES.md` — explains the Explicit list / Auto-detect mode semantics that v1.1 metric types must honour.
- `Protocols/SystemMetricsParser.cs` — the runtime parser dispatch; a new MetricType won't actually publish anything until you wire a branch in here.
- `Models/SystemMetricsRawData.cs` — the raw collector contract; new metric types need a new property here (or a new collector).
