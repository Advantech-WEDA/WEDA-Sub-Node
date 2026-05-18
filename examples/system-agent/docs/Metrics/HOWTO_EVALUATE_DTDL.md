# How-To: Evaluate the DTDL Schemas with Example Data

This guide shows how to (1) validate that the five DTDL Interfaces in `docs/Metrics/` are well-formed and (2) validate that example telemetry payloads conform to the schema *and* to the per-metric validation rules carried in each Telemetry's `comment` field.

The same workflow runs locally (developer laptop), in CI, or as a one-off check before shipping a DTDL change.

> **Three layers of validation — keep them separate.**
> - **Schema-level** — does the DTDL file parse? This is what the official Microsoft DTDL parser checks. Pure JSON-structure + identifier-resolution validation; no value-level checks.
> - **Instance-level** — does a published `TelemetryMeasure { ResourceId, Value }` payload satisfy the range / monotonicity / enum rule encoded in the Telemetry's `comment`? The parser does **not** do this; consumer code does, driven by the documented rule.
> - **Config-level** — does a `devicecfg.json` Sensor entry satisfy the per-`MetricType` config shape rules (`Parameters.Interfaces` is string or `string[]`; `Interface` is non-empty; the two are mutually exclusive; etc.)? Neither the DTDL parser nor the telemetry validator checks this — `dtdl-validate/scripts/config-rules.js` does.

---

## 0. Quickstart

The scripts live under `dtdl-validate/scripts/` with their own `package.json`. Run from there:

```bash
cd dtdl-validate/scripts

# 1. One-time install of the Microsoft DTDL parser.
npm install

# 2. Schema-level validation — all five Interfaces in one pass.
npm run validate:dtdl
#   Expected:  6 'OK' lines (5 files + combined), no 'ERR'.

# 3. Instance-level validation — fixture samples against the predicates.
npm run validate:samples
#   Expected:  every sample reported 'OK', final 'ALL 91 SAMPLES PASSED'.

# 4. Config-level validation — Parameters.* shape rules for sensor configs.
npm run validate:configs
#   Expected:  every sensor reported 'OK', final 'ALL 10 CONFIGS PASSED'.

# All three in one go:
npm run validate
```

Green output means **the schemas parse cleanly, every telemetry fixture lands on its declared verdict, AND every sensor-config fixture passes its shape rules** (`valid` samples accepted, `invalid` samples rejected). Extend by:

- Adding telemetry fixtures under `docs/Metrics/samples/*.samples.json` and predicates in `scripts/predicates.js`.
- Adding sensor-config fixtures under `docs/Metrics/samples-config/*.configs.json` and rules in `scripts/config-rules.js`.

Confirmed working on the current tree — 5 DTDL interfaces, 51 telemetries, 219 entities, 91 telemetry fixtures, 10 config fixtures.

---

## 1. Schema-level validation

### 1.1 Using the Microsoft `@azure/dtdl-parser` (Node.js)

The cheapest local check. Same parser engine as the .NET `DTDLParser`, so what passes here passes in .NET too.

**Install:**

```bash
mkdir -p /tmp/dtdl-eval && cd /tmp/dtdl-eval
npm init -y >/dev/null
npm install --silent @azure/dtdl-parser
```

**Validator (`scripts/validate-dtdl.js`):**

```javascript
#!/usr/bin/env node
const { createParser, ModelParsingOption } = require('@azure/dtdl-parser');
const fs = require('fs');
const path = require('path');

const BASE = path.resolve(__dirname, '..');
const FILES = [
  '01_CPU_NETWORK.dtdl.json',
  '02_MEMORY_DISK_SYSTEM_GPU.dtdl.json',
  '03_HARDWARE_INFO.dtdl.json',
  '04_ONBOARD_SENSOR.dtdl.json',
  '05_HARDWARE_FEATURE.dtdl.json',
];

(async () => {
  let failed = 0;

  // Validate each file in isolation -- catches per-file issues.
  for (const f of FILES) {
    const json = fs.readFileSync(path.join(BASE, f), 'utf8');
    try {
      const parser = createParser(ModelParsingOption.PermitAnyTopLevelElement);
      const model = await parser.parse([json]);
      console.log(`OK  ${f}: ${Object.keys(model).length} entities`);
    } catch (e) {
      failed++;
      console.error(`ERR ${f}:`);
      console.error('  ' + (e.message || e).toString().split('\n').slice(0, 20).join('\n  '));
    }
  }

  // Validate all five together -- catches DTMI collisions across Interfaces.
  try {
    const jsons = FILES.map(f => fs.readFileSync(path.join(BASE, f), 'utf8'));
    const parser = createParser(ModelParsingOption.PermitAnyTopLevelElement);
    const model = await parser.parse(jsons);
    console.log(`OK  combined: ${Object.keys(model).length} entities`);
  } catch (e) {
    failed++;
    console.error('ERR combined:');
    console.error('  ' + (e.message || e).toString().split('\n').slice(0, 20).join('\n  '));
  }

  process.exit(failed === 0 ? 0 : 1);
})();
```

**Expected output:**

```
OK  01_CPU_NETWORK.dtdl.json: 48 entities
OK  02_MEMORY_DISK_SYSTEM_GPU.dtdl.json: 81 entities
OK  03_HARDWARE_INFO.dtdl.json: 31 entities
OK  04_ONBOARD_SENSOR.dtdl.json: 32 entities
OK  05_HARDWARE_FEATURE.dtdl.json: 36 entities
OK  combined: 219 entities
```

Any line starting `ERR` is a schema bug that must be fixed before shipping.

### 1.2 Using `DTDLParser` (.NET)

For projects that already ship .NET tooling. Add the NuGet:

```bash
dotnet add package DTDLParser
```

**Validator (xUnit fact):**

```csharp
using DTDLParser;
using DTDLParser.Models;
using Xunit;

public sealed class AllInterfacesParse
{
    private static readonly string[] Files = new[]
    {
        "01_CPU_NETWORK.dtdl.json",
        "02_MEMORY_DISK_SYSTEM_GPU.dtdl.json",
        "03_HARDWARE_INFO.dtdl.json",
        "04_ONBOARD_SENSOR.dtdl.json",
        "05_HARDWARE_FEATURE.dtdl.json",
    };

    [Fact]
    public async Task All_dtdl_files_parse_together()
    {
        var jsons = Files.Select(f => File.ReadAllText($"docs/Metrics/{f}")).ToArray();
        var parser = new ModelParser();
        var model = await parser.ParseAsync(jsons);
        Assert.NotEmpty(model);
        Assert.Equal(5, model.Values.OfType<DTInterfaceInfo>().Count());
    }
}
```

Run as part of `dotnet test`.

### 1.3 What schema-level validation catches

| Catches | Misses |
|---------|--------|
| Malformed JSON. | Wrong `enumValue` (e.g., `"USAGE"` instead of `"usage"`) — that's a **semantic** mismatch with the agent, not a DTDL bug. |
| Missing required fields (`@id`, `@type`, `schema` on Telemetry). | Comment text accuracy — comments are free-form. |
| Invalid DTMI format. | Whether the comment's rule matches the agent's actual emitted range. |
| Unresolvable `schema` references (e.g., DTMI typo in `Telemetry.schema: <enum-DTMI>`). | Whether downstream consumers have implemented the rule. |
| Invalid semantic type or unit (`"unit": "byteS"`). | Cross-Interface invariants (`MemoryUsed + MemoryAvailable == MemoryTotal`). |
| `comment` / `description` exceeding the 512-char DTDL v2 limit. | DTMI version-bump correctness (the parser allows additive changes). |
| DTMI collisions across Interfaces in the combined parse. | |

---

## 2. Instance-level validation (sample data)

The DTDL parser validates the **shape** of the schema; it does not check whether a `Value` like `108.4` is acceptable for `CpuUsage` (it isn't — out of `[0, 100]`). That check is owned by the consumer side.

The recipe:

1. **Maintain a fixture file per metric category** in `docs/Metrics/samples/` with one *valid* and one or more *invalid* samples.
2. **Hand-write predicates per DTMI** (these mirror the `comment` text — see the USAGE docs for the full set).
3. **Run the predicates against the fixtures** as part of CI.

### 2.1 Sample-data fixture shape

Each fixture is a JSON array of `{ResourceId, Value, expect}` objects:

```json
[
  {
    "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",
    "Value": 45.67,
    "expect": "valid"
  },
  {
    "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",
    "Value": 108.4,
    "expect": "invalid",
    "reason": "out of [0, 100] range"
  }
]
```

Five fixture files live under `docs/Metrics/samples/`, one per Interface. See §3 for the canonical contents.

### 2.2 Per-DTMI predicate set

The validator script ships with predicates that mirror every Telemetry's `comment`. Adding a new Telemetry to the DTDL requires adding its predicate here:

```javascript
// Excerpt from dtdl-validate/scripts/predicates.js
module.exports = {
  // === 01 CPU & Network ===
  'dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1':
    v => typeof v === 'number' && v >= 0 && v <= 100 && Number.isFinite(v),
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1':
    v => typeof v === 'number' && v >= 0,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad5;1':
    v => typeof v === 'number' && v >= 0,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad15;1':
    v => typeof v === 'number' && v >= 0,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1':
    v => Number.isInteger(v) && v >= 0,

  'dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1':
    v => Number.isInteger(v) && v >= 0,
  // ... one per Telemetry across all 5 Interfaces (full list in scripts/predicates.js)
};
```

### 2.3 The validator script

```javascript
#!/usr/bin/env node
// dtdl-validate/scripts/validate-samples.js
const fs = require('fs');
const path = require('path');
const predicates = require('./predicates');

const SAMPLES_DIR = path.resolve(__dirname, '..', 'samples');
const fixtures = fs.readdirSync(SAMPLES_DIR)
  .filter(f => f.endsWith('.json'))
  .map(f => ({ file: f, samples: JSON.parse(fs.readFileSync(path.join(SAMPLES_DIR, f), 'utf8')) }));

let failed = 0;
for (const { file, samples } of fixtures) {
  console.log(`\n--- ${file} (${samples.length} samples) ---`);
  for (const s of samples) {
    const fn = predicates[s.ResourceId];
    if (!fn) {
      console.error(`  ERR no predicate registered for ${s.ResourceId}`);
      failed++;
      continue;
    }
    const ok = fn(s.Value);
    const expected = s.expect === 'valid';
    if (ok === expected) {
      console.log(`  OK  ${s.expect.padEnd(7)} ${s.ResourceId} = ${JSON.stringify(s.Value)}`);
    } else {
      console.error(`  ERR ${s.expect.padEnd(7)} ${s.ResourceId} = ${JSON.stringify(s.Value)}` +
                    (s.reason ? `  (${s.reason})` : ''));
      failed++;
    }
  }
}
console.log(`\n${failed === 0 ? 'ALL PASSED' : failed + ' FAILURE(S)'}`);
process.exit(failed === 0 ? 0 : 1);
```

### 2.4 Expected output for the shipped fixtures

```
--- 01_CPU_NETWORK.samples.json (10 samples) ---
  OK  valid   dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1 = 45.67
  OK  invalid dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1 = 108.4
  OK  invalid dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1 = "NaN"
  OK  valid   dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1 = 1.25
  OK  invalid dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1 = -0.1
  ...
ALL PASSED
```

A fixture sample marked `"expect": "invalid"` *should* fail the predicate. The validator passes when **every sample lands on its declared verdict** — so an `invalid` sample that incorrectly passes the predicate is a regression that needs investigation.

---

## 2a. Config-level validation (`Parameters.*` shape rules)

The DTDL parser checks the schema; the predicates check published values; neither checks whether a *sensor entry in `devicecfg.json`* is well-formed. That's what `validate-configs.js` (and its .NET twin `ConfigsValidator.cs`) is for.

> **Now also formally declared in the DTDL.** `01_CPU_NETWORK.dtdl.json` `schemas[]` includes a reusable Object schema **`NetworkSensorParameters`** with named fields `metricType` (Enum), `metricName` (Enum), `interface` (`string`), `interfaces` (`Array<string>`). That makes `Parameters.Interfaces` machine-discoverable through the official DTDL parser — every consumer that walks `schemas[]` can introspect the parameter shape without resorting to text-mining the prose. The Object schema **describes the wire shape**; the runtime rules that DTDL v2 can't express (the CSV-string variant of `interfaces`, mutual exclusivity with `interface`) live in this validator's `config-rules.js` and are documented in the Object's `comment` field.

The first ruleset ships with the `network` MetricType and covers the v1.0 / v1.1 `Parameters.Interface` / `Parameters.Interfaces` contract documented in `01_CPU_NETWORK_FIELDS.md`:

| Rule | Pass | Fail |
|------|------|------|
| `Parameters.Interfaces`, when present, is `string` or `array<string>` | `[]`, `""`, `["eth0","eth1"]`, `"eth0,eth1"` | `42`, `["eth0", 17]`, `{ "eth0": true }` |
| `Parameters.Interface`, when present, is a **non-empty** string | `"eth0"`, omitted | `""`, `0`, `true` |
| `Interface` and `Interfaces` are **mutually exclusive** when both are non-empty | only `Interface` set; only `Interfaces` set; neither set (auto-detect) | both set non-empty |

### 2a.1 Fixture shape

Each fixture is a JSON array of full Sensor entries (same schema as `devicecfg.json` Sensors[]) plus two test-metadata fields:

```json
{
  "Name": "network_bytes_sent_ambiguous",
  "SensorGroup": "SYS",
  "Parameters": { "MetricType": "network", "MetricName": "bytes_sent",
                  "Interface": "eth0", "Interfaces": ["eth1"] },
  "Report":     { "Enabled": true, "Interval": 5000 },
  "SensorInfo": { "Schema": "long", "Description": "...", "DisplayName": "..." },

  "expect": "invalid",
  "reason": "both Interface and Interfaces set -- ambiguous"
}
```

Fixtures live in `docs/Metrics/samples-config/`. The first file shipped is `01_CPU_NETWORK.configs.json` (10 sensors: 6 valid, 4 invalid).

### 2a.2 Running

```bash
# Local Node
cd dtdl-validate/scripts && npm run validate:configs

# Docker -- both Node and .NET containers chain configs after dtdl + samples
cd dtdl-validate
docker compose run --rm validate            # Node
docker compose run --rm validate-dotnet     # .NET
docker compose run --rm validate-dotnet configs   # config-only subcommand
```

Expected tail of any run:

```
--- 01_CPU_NETWORK.configs.json (10 sensors) ---
  OK  valid   network_eth0_bytes_sent
  OK  valid   network_bytes_sent_list
  OK  valid   network_bytes_sent_csv
  OK  valid   network_bytes_sent_auto_arr
  OK  valid   network_bytes_sent_auto_str
  OK  valid   network_bytes_sent_auto_missing
  OK  invalid network_bytes_sent_ambiguous
  OK  invalid network_bytes_sent_iface_empty
  OK  invalid network_bytes_sent_ifaces_number
  OK  invalid network_bytes_sent_ifaces_mixed

ALL 10 CONFIGS PASSED
```

### 2a.3 Adding a rule (e.g. require `MountPoint` for `disk`)

The rules table in `config-rules.js` (and `ConfigRules.cs`) is keyed by `MetricType`. To add `disk`-specific checks:

```javascript
// dtdl-validate/scripts/config-rules.js
const rules = {
  network: [ /* …existing… */ ],

  disk: [
    sensor => {
      const mp = sensor.Parameters?.MountPoint;
      if (typeof mp === 'string' && mp.length > 0) return { ok: true };
      return { ok: false, reason: 'disk sensors require a non-empty Parameters.MountPoint' };
    },
  ],
};
```

Then drop a matching fixture file under `samples-config/02_MEMORY_DISK_SYSTEM_GPU.configs.json` and CI picks it up automatically.

The same pattern in `ConfigRules.cs` is one entry in the `Rules` dictionary plus the rule function — keep the two languages in lock-step (the test fixtures are shared, so they must produce identical verdicts).

---

## 3. Sample-data fixtures

Five fixture files cover the five Interfaces. Each captures the canonical valid case plus the most common failure modes (range violations, NaN/Inf, wrong type, non-monotonic counter, unsupported value).

### 3.1 `samples/01_CPU_NETWORK.samples.json`

```json
[
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": 45.67,        "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": 0.0,          "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": 100.0,        "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": 108.4,        "expect": "invalid", "reason": "out of [0, 100]" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": -0.5,         "expect": "invalid", "reason": "out of [0, 100]" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",            "Value": "NaN",        "expect": "invalid", "reason": "wrong type / NaN" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1",            "Value": 1.25,         "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1",            "Value": -0.1,         "expect": "invalid", "reason": "negative load" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1",  "Value": 9876543210,   "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1",    "Value": 17592186044, "expect": "valid" }
]
```

### 3.2 `samples/02_MEMORY_DISK_SYSTEM_GPU.samples.json`

```json
[
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:MemoryTotal;1",        "Value": 16777216000, "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:MemoryUsed;1",         "Value": 4294967296,  "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:MemoryUsed;1",         "Value": -1,          "expect": "invalid", "reason": "negative" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:DiskUsagePercent;1",   "Value": 73.4,        "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:DiskUsagePercent;1",   "Value": 100.01,      "expect": "invalid", "reason": "> 100" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:SystemTime;1",         "Value": 1747084800,  "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:SystemTime;1",         "Value": 5000000000,  "expect": "invalid", "reason": "year > 2100" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:SystemProcsRunning;1", "Value": 4,           "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1",     "Value": 87,          "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1",     "Value": 150,         "expect": "invalid", "reason": "> 100" }
]
```

### 3.3 `samples/03_HARDWARE_INFO.samples.json`

```json
[
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:HwinfoMotherboardName;1", "Value": "AIMB-588",  "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:HwinfoManufacturer;1",    "Value": "Advantech", "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:HwinfoBiosRevision;1",    "Value": "V1.05",     "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:HwinfoBiosRevision;1",    "Value": "",          "expect": "invalid", "reason": "empty string" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:HwinfoDriverVersion;1",   "Value": null,        "expect": "valid",   "reason": "null is suppressed, considered OK" }
]
```

### 3.4 `samples/04_ONBOARD_SENSOR.samples.json`

```json
[
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:Temperature;1",  "Value": 42.5,   "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:Temperature;1",  "Value": 200.0,  "expect": "invalid", "reason": "out of [-40, 125]" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:Temperature;1",  "Value": -50.0,  "expect": "invalid", "reason": "out of [-40, 125]" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:Voltage;1",      "Value": 5.05,   "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:FanSpeed;1",     "Value": 2400.0, "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:FanSpeed;1",     "Value": -100,   "expect": "invalid", "reason": "negative RPM" }
]
```

### 3.5 `samples/05_HARDWARE_FEATURE.samples.json`

```json
[
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpioIsSupported;1",              "Value": true,  "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpioIsSupported;1",              "Value": "yes", "expect": "invalid", "reason": "not boolean" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpioPinState;1",                 "Value": 0,     "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpioPinState;1",                 "Value": 1,     "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:GpioPinState;1",                 "Value": 2,     "expect": "invalid", "reason": "not in {0, 1}" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:WatchdogIsSupported;1",          "Value": false, "expect": "valid" },
  { "ResourceId": "dtmi:advantech:EdgeSync:SystemInfo:ThermalProtectionIsSupported;1", "Value": true,  "expect": "valid" }
]
```

---

## 4. Cross-sample invariants

A few rules don't fit the single-sample predicate model — they need two or more samples within the same collection cycle:

| Invariant | Telemetries | Check |
|-----------|-------------|-------|
| Memory partition | `MemoryUsed + MemoryAvailable ≈ MemoryTotal` | `abs((used + avail) - total) <= total / 1000` (0.1 % rounding slack) |
| File-descriptor headroom | `SystemFilefdAllocated <= SystemFilefdMaximum` | strict |
| Swap range | `MemorySwapFree <= MemorySwapTotal` | strict |
| Disk capacity | `DiskUsed + DiskAvailable <= DiskTotal` *(plus root-reserve)* | `DiskFree >= DiskAvailable` |
| Monotonic counters | `*BytesSent`, `*BytesReceived`, `*PacketsSent`, `*PacketsReceived`, `*Errors*`, `Disk*Completed`, `Disk{Read,Written}Bytes`, `SystemIntrTotal`, `CpuContextSwitches` | `Δ(v) >= 0` between samples unless `SystemBootTime` also changed |
| Capability gates | If `GpioIsSupported == false`, suppress `GpioPinState` alerts; if `ThermalProtectionIsSupported == false`, escalate `Temperature` alarms |

These are documented in the per-Interface USAGE docs (`*_USAGE.md` §6 "Cross-field invariants"). Add a fixture file `samples/cross_sample.invariants.json` if you want CI to check them across pairs of samples.

---

## 4a. Run inside a Docker container

If you don't want Node.js on the host (CI agents, dev VMs, locked-down developer machines) — bring the validator up in a container instead. The image bakes in the parser; DTDL files and sample fixtures are bind-mounted at run time so the same image evaluates any branch without rebuilds.

### 4a.1 One-command flow

From the `dtdl-validate/` directory at the project root (the validator code and the compose file live here; the DTDLs they read sit in `docs/Metrics/` and are bind-mounted automatically):

```bash
docker compose run --rm validate
```

That builds the image on first use (~30 s, cached afterwards) and runs **both** validators against the working tree. Exit code propagates: 0 means clean, non-zero means at least one DTDL file or sample fixture failed.

To run only one of the validators:

```bash
# Schema-level only
docker compose run --rm validate node /app/validate-dtdl.js

# Instance-level only
docker compose run --rm validate node /app/validate-samples.js
```

### 4a.2 Without compose — raw `docker run`

From `dtdl-validate/scripts/`:

```bash
# Build once
docker build -t weda-dtdl-validate .

# Run from anywhere; mount the docs/Metrics/ folder as /work
docker run --rm \
  -v "$(pwd)/../../docs/Metrics:/work:ro" \
  weda-dtdl-validate
```

The image is ~155 MB on top of `node:20-alpine` (parser binary is the bulk). The container runs as the non-root `node` user (UID 1000) and the bind-mount is read-only — the validator never writes to your tree.

### 4a.3 What's in the image

| Path | Contents |
|------|----------|
| `/app/node_modules/` | `@azure/dtdl-parser` and its transitive deps (pinned in `package.json`). |
| `/app/validate-dtdl.js` | Schema-level validator. |
| `/app/validate-samples.js` | Instance-level validator. |
| `/app/predicates.js` | Per-DTMI value predicates. |
| `/work/` (bind-mount) | The five `*.dtdl.json` files and the `samples/` folder, mounted from your host. |
| Env `DTDL_BASE=/work` | Tells both scripts where to read DTDLs / samples from. |

### 4a.4 Verifying the image rejects bad input

A quick smoke test — temporarily corrupt a Telemetry's `schema` and confirm the container exits non-zero:

```bash
# Corrupt
cp 01_CPU_NETWORK.dtdl.json /tmp/orig.json
jq '.contents[0].schema = "flooble"' /tmp/orig.json > 01_CPU_NETWORK.dtdl.json

# Run -- should print ERR and exit 1
docker compose run --rm validate; echo "exit: $?"

# Restore
mv /tmp/orig.json 01_CPU_NETWORK.dtdl.json
```

Expected output ends with `ERR combined: Parsing exception -- 1errors in model …` and `exit: 1`.

### 4a.5 .NET equivalent

If your team prefers .NET (already-pinned SDK, shared base image with the agent itself), the same two-layer validation is available as a .NET 8 console app under [`scripts-dotnet/`](./scripts-dotnet/). It uses the **official Microsoft `DTDLParser` NuGet package** — same parser engine as the .NET examples in the per-Interface `*_USAGE.md` docs, so what passes here passes the in-agent loader too.

**Source layout:**

| File | Purpose |
|------|---------|
| `scripts-dotnet/ValidateDtdl.csproj` | .NET 8 console project; references `DTDLParser` 1.0.52. |
| `scripts-dotnet/Program.cs` | Argv routing (`dtdl` / `samples` / `both`) and `DTDL_BASE` resolution. |
| `scripts-dotnet/DtdlValidator.cs` | Schema-level — parses each Interface, then the combined model. |
| `scripts-dotnet/SamplesValidator.cs` | Instance-level — runs every fixture against `Predicates.Map`. |
| `scripts-dotnet/Predicates.cs` | Per-DTMI `Func<JsonElement, bool>` — mirror of `scripts/predicates.js`. |
| `scripts-dotnet/Dockerfile` | Multi-stage build: SDK image restores + publishes, runtime image is `mcr.microsoft.com/dotnet/runtime:8.0`. |

**Compose service (already wired in `dtdl-validate/docker-compose.yml`):**

```bash
# From dtdl-validate/
docker compose run --rm validate-dotnet            # both validators
docker compose run --rm validate-dotnet dtdl       # schema-level only
docker compose run --rm validate-dotnet samples    # instance-level only
```

**Raw `docker run`:**

```bash
# From dtdl-validate/scripts-dotnet/
docker build -t weda-dtdl-validate-dotnet .
docker run --rm -v "$(pwd)/../../docs/Metrics:/work:ro" weda-dtdl-validate-dotnet
docker run --rm -v "$(pwd)/../../docs/Metrics:/work:ro" weda-dtdl-validate-dotnet samples
```

**Local development (no Docker):**

```bash
cd dtdl-validate/scripts-dotnet/
dotnet run -- both          # schema + instance
dotnet run -- dtdl
dotnet run -- samples
```

`DTDL_BASE=/work` is set automatically inside the container; for local `dotnet run` the validator walks five levels up from `bin/<config>/net8.0/` to reach the project root, then sideways into `docs/Metrics/`, so no env var is needed.

**Image size:** ~196 MB on top of `mcr.microsoft.com/dotnet/runtime:8.0` — larger than the Node.js image (155 MB) because the runtime image is bigger; in exchange you get strict-typed predicates with compile-time error checking (the project is built with `TreatWarningsAsErrors=true`).

**Verified parity:** the .NET container produces the same `OK / ERR / ALL 91 SAMPLES PASSED` output as the Node.js container; both return exit 1 on a corrupted DTDL or a misclassified sample.

### 4a.6 Pinning the image for CI

The image tag `weda-dtdl-validate:latest` is mutable. For CI, build once and pin by digest:

```bash
docker build -t weda-dtdl-validate:v1 dtdl-validate/scripts/
docker tag weda-dtdl-validate:v1 my-registry.example.com/weda-dtdl-validate:v1
docker push my-registry.example.com/weda-dtdl-validate:v1
# Then in your pipeline (note the mount target is the DTDL docs, not dtdl-validate/):
docker run --rm -v "$PWD/docs/Metrics:/work:ro" \
  my-registry.example.com/weda-dtdl-validate@sha256:<digest>
```

---

## 5. CI integration

### 5.1 GitHub Actions

**Native Node** — fastest on Linux runners that already have Node available:

```yaml
# .github/workflows/dtdl-validate.yml
name: validate-dtdl
on:
  pull_request:
    paths:
      - 'docs/Metrics/**'
      - 'dtdl-validate/**'
jobs:
  schema:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - working-directory: dtdl-validate/scripts
        run: |
          npm install
          npm run validate
```

**Docker** — for runners without Node installed, or to share the exact image with local devs:

```yaml
name: validate-dtdl
on:
  pull_request:
    paths:
      - 'docs/Metrics/**'
      - 'dtdl-validate/**'
jobs:
  schema:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - working-directory: dtdl-validate
        run: docker compose run --rm validate
```

### 5.2 dotnet test (instance + schema in one run)

```csharp
// tests/SystemAgent.Schema.Tests/DtdlAndSamplesTests.cs
public sealed class DtdlAndSamplesTests
{
    [Fact] public async Task All_dtdl_files_parse() { /* as in §1.2 */ }

    [Theory]
    [MemberData(nameof(LoadAllSamples))]
    public void Sample_matches_expectation(string resourceId, JsonElement value, string expect, string? reason)
    {
        var isValid = SamplePredicates.IsValid(resourceId, value);
        if (expect == "valid")
            Assert.True(isValid, $"{resourceId} = {value} should be valid ({reason})");
        else
            Assert.False(isValid, $"{resourceId} = {value} should be invalid ({reason})");
    }

    public static IEnumerable<object?[]> LoadAllSamples() { /* iterate samples/*.json */ }
}
```

Keep `SamplePredicates` in sync with the JS `predicates.js` — they're the same rules in two languages.

---

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `ERR Cannot create instance of class 'Telemetry'` | A required field (`@id`, `name`, `schema`) is missing on a Telemetry. | Re-add it. |
| `ERR DTMI 'dtmi:…' is invalid` | DTMI segment violates `[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?` or version is missing (`;1`). | Fix the DTMI. |
| `ERR multiple definitions for dtmi:…` | The same DTMI exists in two files passed to a combined parse. | Re-namespace one of them. |
| `ERR unrecognized type 'DataSiZe'` | Misspelled DTDL semantic type. Case-sensitive. | Correct to `DataSize`. |
| `ERR unit 'byteS' is invalid for DataSize` | Misspelled unit; the parser ships an allow-list per semantic type. | See the DTDL v2 semantic-type cheat sheet in `_template/README.md`. |
| Schema parses but `validate-samples.js` reports `no predicate registered for dtmi:…` | A new Telemetry was added to a DTDL but the matching predicate wasn't added to `scripts/predicates.js`. | Add the predicate; mirror the rule from the new Telemetry's `comment` field. |
| Sample with `expect: "invalid"` is reported as `OK invalid …` | That's actually correct — the predicate **rejected** the invalid sample, which matches the declared expectation. The script prints `OK` for any sample that lands on its declared verdict. |
| Sample with `expect: "invalid"` is reported as `ERR invalid …` | The predicate **accepted** an invalid sample. Either the predicate is too lax or the sample's `expect` is wrong. Inspect both. |

---

## 7. When to bump the Interface DTMI version

Per DTDL v2 compatibility rules:

| Change | Bump `;1` → `;2`? |
|--------|------------------|
| Add a new Telemetry | No |
| Add a new Enum value to a `schemas[]` Enum | No |
| Add a new reusable Enum in `schemas[]` | No |
| Tighten a `comment` rule (text change only) | No |
| Change a Telemetry's `schema` (`long` → `integer`) | **Yes** |
| Remove a Telemetry | **Yes** |
| Remove an Enum value | **Yes** |
| Rename a Telemetry `name` | **Yes** |

Bumping the Interface DTMI requires updating every consumer's `*Dtmis.Interface` constant and any cloud-side twin registration that pins the version.

---

## Related

- [`01_CPU_NETWORK_FIELDS.md`](./01_CPU_NETWORK_FIELDS.md) – [`05_HARDWARE_FEATURE_FIELDS.md`](./05_HARDWARE_FEATURE_FIELDS.md) — per-MetricType configuration reference.
- [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md) – [`05_HARDWARE_FEATURE_USAGE.md`](./05_HARDWARE_FEATURE_USAGE.md) — .NET consumption guides; their value-validator sections are the .NET twin of `predicates.js`.
- [`_template/README.md`](./_template/README.md) — adding a new metric type (don't forget to extend `predicates.js` and the matching samples file).
