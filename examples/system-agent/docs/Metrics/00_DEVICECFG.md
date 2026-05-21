# `devicecfg.json` — Summary

This page is the one-screen orientation for `devicecfg.json`, the System Agent's runtime configuration file. Read this first; jump to the numbered detail docs in this folder for per-`MetricType` field reference, DTDL, and consumption examples.

| File path on the agent | Role |
|---|---|
| `devicecfg.json` | The canonical runtime config — uses `[]` auto-detect mode (recommended). |
| `devicecfg-explicit-list.json` | Example: explicit list mode and single-bound sensor syntax. |

---

## Top-level shape

```jsonc
{
  "SubNode":       { /* identity of this agent on the bus */ },
  "DeviceConfigs": { /* exactly one entry — SystemAgentDeviceConfig */ }
}
```

### `SubNode`

Static identity block. Set once per device — these values become part of the published DTDL DTMIs and the NATS subject hierarchy.

| Field | Example | Purpose |
|---|---|---|
| `Name` | `"Advantech-System-Agent"` | Human-readable name for dashboards. |
| `SubNodeType` | `"SystemMonitor"` | Subnode role classifier. |
| `Manufacturer` | `"Advantech"` | Vendor. |
| `Model` | `"SystemAgent"` | Model identifier. |
| `SwVersion` | `"1.0.0"` | Agent build version — bump for every release. |

### `DeviceConfigs.SystemAgentDeviceConfig`

| Field | Type | Notes |
|---|---|---|
| `Enabled` | `bool` | Master kill-switch. `false` keeps the binary alive but reports nothing. |
| `Dtdl.AutoGenEnabled` | `bool` | When true, the agent emits a DTDL Interface derived from the `Sensors[]` list at startup (no need to ship a hand-written `.dtdl.json`). |
| `Sensors` | `Sensor[]` | The publishing surface. Each entry produces exactly one Telemetry on the wire. |

---

## Anatomy of one `Sensor`

Every sensor entry has the same four-block layout. Keep all four blocks present even when fields are at defaults — config tooling parses positionally.

```jsonc
{
  "Name":         "cpu_usage",          // identity — must be unique per agent
  "SensorGroup":  "SYS",                // SYS | TEMP | PWR | DI | DO | AI | AO
  "Parameters": {                       // routing — picks the collector
    "MetricType": "cpu",                // one of the supported types (see table below)
    "MetricName": "usage"               // sub-metric within the type
  },
  "Report": {                           // publishing cadence
    "Enabled":  true,
    "Interval": 5000                    // milliseconds between reports
  },
  "SensorInfo": {                       // contract surfaced to consumers
    "Schema":      "double",            // double | long | integer | boolean | string
    "Description": "CPU usage percentage",
    "DisplayName": "CPU Usage",
    "IsReadable":  true,                // agent forwards readings as telemetry — always true
    "IsWritable":  false                // agent rejects writes (SIL2 fail-safe) — always false
  }
}
```

`SensorInfo.Schema` **must** match the wire type the agent emits for the chosen `(MetricType, MetricName)` pair — the agent validates this at startup and refuses to publish on a mismatch. The wire types are documented per metric in each `*_FIELDS.md` reference. The DTDL Interfaces in `docs/Metrics/*.dtdl.json` are config-only (no Telemetry declarations); they constrain `SensorInfo.Schema` to the `SensorInfoSchema` enum and the SensorInfo block shape via a per-Interface `SensorInfo` Object schema, but do not bind `Schema` to a per-metric value.

`SensorInfo.IsReadable` and `SensorInfo.IsWritable` declare the read/write directionality of the sensor. For every MetricType shipped today, `IsReadable` is `true` and `IsWritable` is `false` — the agent is read-direction by design and rejects writes (`WriteSensorDataAsync` returns `false`) and commands (`ExecuteCommandAsync` returns `CommandNotSupported`) per the SIL2 fail-safe contract. A future writable extension (e.g. GPIO output) would ship as its own MetricType with `IsWritable: true`; existing MetricTypes never flip directionality.

---

## Supported `MetricType`s and where each is documented

This `devicecfg.json` currently publishes **33 sensors** spanning **13 MetricTypes** across **4 SensorGroups** (`SYS`×29, `TEMP`×1, `PWR`×1, `DI`×2).

| MetricType | Sensors here | Detail doc |
|---|---|---|
| `cpu` | 5 | [`01_SYS_RES_CPU_NETWORK_FIELDS.md`](01_SYS_RES_CPU_NETWORK_FIELDS.md) · [DTDL](01_SYSTEM_RESOURCE.dtdl.json) · [USAGE](01_SYS_RES_CPU_NETWORK_USAGE.md) |
| `network` | 5 | [`01_SYS_RES_CPU_NETWORK_FIELDS.md`](01_SYS_RES_CPU_NETWORK_FIELDS.md) · [DTDL](01_SYSTEM_RESOURCE.dtdl.json) · [USAGE](01_SYS_RES_CPU_NETWORK_USAGE.md) |
| `memory` | 6 | [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](01_SYS_RES_MEMORY_DISK_FIELDS.md) · [DTDL](01_SYSTEM_RESOURCE.dtdl.json) · [USAGE](01_SYS_RES_MEMORY_DISK_USAGE.md) |
| `disk` | 4 | [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](01_SYS_RES_MEMORY_DISK_FIELDS.md) · [DTDL](01_SYSTEM_RESOURCE.dtdl.json) · [USAGE](01_SYS_RES_MEMORY_DISK_USAGE.md) |
| `system` | 2 | [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](01_SYS_RES_MEMORY_DISK_FIELDS.md) · [DTDL](01_SYSTEM_RESOURCE.dtdl.json) · [USAGE](01_SYS_RES_MEMORY_DISK_USAGE.md) |
| `gpu` | 1 | [`02_GPU_FIELDS.md`](02_GPU_FIELDS.md) · [DTDL](02_GPU_RESOURCE.dtdl.json) · [USAGE](02_GPU_USAGE.md) |
| `hwinfo` | 3 | [`03_HARDWARE_INFO_FIELDS.md`](03_HARDWARE_INFO_FIELDS.md) · [DTDL](03_HARDWARE_INFO.dtdl.json) · [USAGE](03_HARDWARE_INFO_USAGE.md) |
| `temperature` | 1 | [`04_ONBOARD_SENSOR_FIELDS.md`](04_ONBOARD_SENSOR_FIELDS.md) · [DTDL](04_ONBOARD_SENSOR.dtdl.json) · [USAGE](04_ONBOARD_SENSOR_USAGE.md) |
| `voltage` | 1 | [`04_ONBOARD_SENSOR_FIELDS.md`](04_ONBOARD_SENSOR_FIELDS.md) |
| `fanspeed` | 1 | [`04_ONBOARD_SENSOR_FIELDS.md`](04_ONBOARD_SENSOR_FIELDS.md) |
| `gpio` | 2 | [`05_HARDWARE_FEATURE_FIELDS.md`](05_HARDWARE_FEATURE_FIELDS.md) · [DTDL](05_HARDWARE_FEATURE.dtdl.json) · [USAGE](05_HARDWARE_FEATURE_USAGE.md) |
| `watchdog` | 1 | [`05_HARDWARE_FEATURE_FIELDS.md`](05_HARDWARE_FEATURE_FIELDS.md) |
| `thermalprotection` | 1 | [`05_HARDWARE_FEATURE_FIELDS.md`](05_HARDWARE_FEATURE_FIELDS.md) |

Need to add a brand-new `MetricType`? Follow [`_template/README.md`](_template/README.md) — it produces the same three-file layout used above (`FIELDS.md` + `.dtdl.json` + `USAGE.md`).

---

## Full sensor inventory

Snapshot of every sensor declared in the current `devicecfg.json` (33 rows). Use it as a quick cross-reference when wiring dashboards or writing fact-check rules.

| MetricType | MetricName | SensorGroup | Schema | Interval (ms) |
|---|---|---|---|---|
| cpu | usage | SYS | double | 5 000 |
| cpu | load1 | SYS | double | 10 000 |
| cpu | load5 | SYS | double | 10 000 |
| cpu | load15 | SYS | double | 10 000 |
| cpu | context_switches | SYS | long | 10 000 |
| memory | total | SYS | long | 10 000 |
| memory | available | SYS | long | 10 000 |
| memory | used | SYS | long | 10 000 |
| memory | cached | SYS | long | 10 000 |
| memory | swap_total | SYS | long | 30 000 |
| memory | swap_free | SYS | long | 30 000 |
| disk | total | SYS | long | 30 000 |
| disk | available | SYS | long | 30 000 |
| disk | used | SYS | long | 30 000 |
| disk | usage_percent | SYS | double | 30 000 |
| network | bytes_sent | SYS | long | 5 000 |
| network | bytes_received | SYS | long | 5 000 |
| network | packets_sent | SYS | long | 5 000 |
| network | packets_received | SYS | long | 5 000 |
| network | errors | SYS | long | 25 000 |
| system | time | SYS | long | 60 000 |
| system | boot_time | SYS | long | 60 000 |
| gpu | utilization | SYS | integer | 5 000 |
| hwinfo | motherboardname | SYS | string | 60 000 |
| hwinfo | manufacturer | SYS | string | 60 000 |
| hwinfo | biosrevision | SYS | string | 60 000 |
| temperature | therm | TEMP | double | 1 000 |
| voltage | voltage | PWR | double | 5 000 |
| fanspeed | fanspeed | SYS | double | 5 000 |
| gpio | isSupported | DI | boolean | 6 000 |
| gpio | pinState | DI | integer | 6 000 |
| watchdog | isSupported | SYS | boolean | 6 000 |
| thermalprotection | isSupported | SYS | boolean | 6 000 |

Regenerate this table after editing `devicecfg.json`:

```bash
python3 - <<'PY'
import json
sensors = json.load(open("devicecfg.json"))["DeviceConfigs"]["SystemAgentDeviceConfig"]["Sensors"]
print("| MetricType | MetricName | SensorGroup | Schema | Interval (ms) |")
print("|---|---|---|---|---|")
for s in sensors:
    p, r, si = s["Parameters"], s["Report"], s["SensorInfo"]
    print(f"| {p['MetricType']} | {p['MetricName']} | {s['SensorGroup']} | {si['Schema']} | {r['Interval']} |")
PY
```

---

## Common operations

| Task | Edit |
|---|---|
| Disable all reporting without removing the agent | Set `DeviceConfigs.SystemAgentDeviceConfig.Enabled` to `false`. |
| Mute one sensor | Set its `Report.Enabled` to `false`. Keeps the DTDL Telemetry declared but the agent stops emitting samples. |
| Slow a chatty sensor | Increase its `Report.Interval` (milliseconds). Per-sensor; no global throttle. |
| Add an existing `MetricType` for new hardware | Append a `Sensors[]` entry; pick the correct `(MetricType, MetricName, Schema)` triple from the corresponding `…_FIELDS.md`. Restart the agent — config is read once at startup. |
| Add a brand-new `MetricType` | Follow [`_template/README.md`](_template/README.md), wire the parser branch in `Protocols/SystemMetricsParser.cs`, and append your sensors here. |
| Ship the DTDL Interface yourself instead of auto-gen | Set `Dtdl.AutoGenEnabled` to `false` and place your `.dtdl.json` next to the binary. |

---

## Validation

The agent validates `devicecfg.json` at startup and refuses to run on:

- Duplicate `Sensors[*].Name`.
- `SensorInfo.Schema` that disagrees with the DTDL Interface's Telemetry schema for the same `(MetricType, MetricName)`.
- `MetricType` not recognised by the parser (i.e., no branch in `Protocols/SystemMetricsParser.cs`).
- `Report.Interval` ≤ 0.

Quick syntactic check before deploying a new config:

```bash
python3 -c "import json; json.load(open('devicecfg.json'))"
```

End-to-end check (parses through the official Microsoft DTDL parser when auto-gen is on):

```bash
dotnet test --filter "FullyQualifiedName~DevicecfgSchemaTests"
```

---

## Related references

- [`_template/README.md`](_template/README.md) — how to add a new `MetricType` (template files + step-by-step + checklist).
- [`HOWTO_EVALUATE_DTDL.md`](HOWTO_EVALUATE_DTDL.md) — running the Microsoft DTDL parser against the auto-generated Interface.
- `docs/v1.1/METRIC-TYPES.md` — Explicit list / Auto-detect mode semantics for the v1.1 metric types.
- `Models/SystemMetricsRawData.cs` — raw collector contract; every `MetricType` needs a backing property here.
- `Protocols/SystemMetricsParser.cs` — runtime dispatch; a new MetricType won't publish anything until you add a branch.
