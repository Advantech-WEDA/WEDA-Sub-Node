# Sensor Field Reference — {{name_human}}

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **{{name_human}}** category — covering the MetricType(s) `{{metric_type_1}}`{{, optionally more: `{{metric_type_2}}`}}.

{{Platform note — choose one and delete the other:}}
{{Cross-platform — works on Linux / Windows / macOS.}}
{{Platform-dependent — requires the host to ship a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver, sensors remain defined but emit no value.}}

{{v1.x behaviour — keep the relevant line, delete the rest:}}
{{All MetricTypes in this file behave identically between v1.0 and v1.1 and do not support Sensor Expansion.}}
{{`{{metric_type_1}}` supports v1.1 Sensor Expansion via `{{expansion_param_singular}}` / `{{expansion_param_plural}}`. The remaining MetricTypes are identical between v1.0 and v1.1.}}

---

## Legend

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## {{MetricType1Pascal}} Metric

{{Short description of what this MetricType collects and where the data comes from. Mention any extra Parameters required.}}

### Example

```json
{
  "Name": "{{metric_type_1}}_{{metric_name_a}}",
  "SensorGroup": "{{sensor_group}}",
  "Parameters": {
    "MetricType": "{{metric_type_1}}",
    "MetricName": "{{metric_name_a}}"
    {{, if needed: ,"{{extra_param_name}}": "<value>"}}
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "{{schema_a}}",
    "Description": "{{Human-readable description}}",
    "DisplayName": "{{Dashboard label}}"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `{{metric_type_1}}_{{metric_name_a}}`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique across all sensors. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. `{{metric_type_1}}` conventionally uses `{{sensor_group}}`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`{{, if needed: , and `{{extra_param_name}}`}}. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `{{metric_type_1}}` | Must equal `{{metric_type_1}}` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific metric to collect. | `{{metric_name_a}}`, `{{metric_name_b}}`, … | Enum constraint above. |
{{Optional rows — keep if applicable:}}
{{| `Parameters.{{extra_param_name}}` | string | ❌ | ✅ | <Description of the extra parameter>. | <Allowed-values list> | <Validation>. |}}
{{| `Parameters.{{expansion_param_singular}}` | string | ❌ | ✅ / ❌ | v1.0 single binding; v1.1 bound mode. | e.g. <value> | Non-empty string; must resolve on the host. |}}
{{| `Parameters.{{expansion_param_plural}}` | `oneOf: [array<string>, string]` | ❌ | n/a / ❌ | **v1.1 only.** List of values, or empty for auto-detect. | JSON array / CSV string / empty | Array of strings or single string. |}}
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g. `5000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | See mapping table below. | Must equal the MetricName's native type. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### {{MetricType1Pascal}} MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `{{metric_name_a}}` | `{{schema_a}}` | <unit or `—`> |
| `{{metric_name_b}}` | `{{schema_b}}` | <unit or `—`> |
| `{{metric_name_c}}` | `{{schema_c}}` | <unit or `—`> |

---

{{Repeat the "## {{MetricType<N>Pascal}} Metric" block above for each additional MetricType in this file.}}

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`{{, if applicable: , `Parameters.{{extra_param_name}}`}}, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
{{If the type supports v1.1 expansion, add:}}
{{- For v1.1 `{{metric_type_1}}`, the pair (`{{expansion_param_singular}}`, `{{expansion_param_plural}}`, neither) is collectively optional — at least one mode must resolve at runtime.}}

---

## Platform Dependency

{{Delete this section if the metric is cross-platform.}}
{{This metric type requires <platform / driver>. On unsupported hosts:}}
{{- Sensors remain defined in the JSON config (no validation error).}}
{{- Reports no value at runtime (suppressed null, not zero — SIL2 fail-safe).}}
{{- Does not throw a fatal error — the System Agent continues to operate.}}

---

## Related

- [`{{NN}}_{{NAME_UPPER}}.dtdl.json`](./{{NN}}_{{NAME_UPPER}}.dtdl.json) — DTDL schema for this metric category.
- [`{{NN}}_{{NAME_UPPER}}_USAGE.md`](./{{NN}}_{{NAME_UPPER}}_USAGE.md) — .NET / C# consumption guide.
- [`../05_DEVICECFG.md`](../05_DEVICECFG.md) — top-level `devicecfg.json` reference.
