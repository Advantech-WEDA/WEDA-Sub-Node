# How-To: Evaluate the DTDL Schemas and Sensor Configs

This guide shows how to (1) validate that the five DTDL Interfaces in `docs/Metrics/` are well-formed and (2) validate that a `devicecfg.json` Sensor entry conforms to the per-`MetricType` `Parameters.*` shape rules.

The same workflow runs locally (developer laptop), in CI, or as a one-off check before shipping a DTDL or `devicecfg.json` change.

> **Two layers of validation — keep them separate.**
> - **Schema-level** — does the DTDL file parse? This is what the official Microsoft DTDL parser checks. Pure JSON-structure + identifier-resolution validation; no value-level checks.
> - **Config-level** — does a `devicecfg.json` Sensor entry satisfy the per-`MetricType` config-shape rules (`Parameters.Interfaces` is `string` or `string[]`; `Interface` is non-empty; the two are mutually exclusive; `Parameters.MountPoint` is a non-empty string for `disk`; etc.)? The DTDL parser does **not** enforce these; `dtdl-validate/scripts/config-rules.js` (and its .NET mirror `ConfigRules.cs`) does.

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

# 3. Config-level validation — Parameters.* shape rules for sensor configs.
npm run validate:configs
#   Expected:  every sensor reported 'OK', final 'ALL N CONFIGS PASSED'.

# Both in one go:
npm run validate
```

Green output means **the schemas parse cleanly AND every sensor-config fixture passes its shape rules** (`valid` samples accepted, `invalid` samples rejected). Extend by adding sensor-config fixtures under `docs/Metrics/samples-config/*.configs.json` and rules in `scripts/config-rules.js`.

Confirmed working on the current tree — 13 DTDL Interfaces (config-only, no Telemetries; one Interface per MetricType, flattened Properties with native `writable`), **264 entities** parse cleanly in the combined model, **59 sensor-config fixtures** pass.

> **Context version.** All five files are `dtmi:dtdl:context;3`. DTDL v2 forbids `Array` as a Property's `schema`, so `NetworkSensorConfig.Interfaces`, `TemperatureSensorConfig.Sources`, and `GpioSensorConfig.PinIds` would fail under v2. v3 allows the Array schema to be declared in `schemas[]` and referenced by DTMI from the Property.

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
  '01_SYSTEM_RESOURCE.dtdl.json',
  '02_GPU_RESOURCE.dtdl.json',
  '03_HARDWARE_INFO.dtdl.json',
  '04_ONBOARD_SENSOR.dtdl.json',
  '05_HARDWARE_FEATURE.dtdl.json',
];

(async () => {
  let failed = 0;

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
OK  01_SYSTEM_RESOURCE.dtdl.json: 121 entities
OK  02_GPU_RESOURCE.dtdl.json: 19 entities
OK  03_HARDWARE_INFO.dtdl.json: 24 entities
OK  04_ONBOARD_SENSOR.dtdl.json: 52 entities
OK  05_HARDWARE_FEATURE.dtdl.json: 60 entities
OK  combined: 264 entities
```

Each file is now a JSON array of one Interface per MetricType (13 Interfaces total):

| File | Interfaces |
|------|------------|
| `01_SYSTEM_RESOURCE.dtdl.json` | `CpuSensorConfig`, `MemorySensorConfig`, `DiskSensorConfig`, `NetworkSensorConfig`, `SystemSensorConfig` |
| `02_GPU_RESOURCE.dtdl.json` | `GpuSensorConfig` |
| `03_HARDWARE_INFO.dtdl.json` | `HwinfoSensorConfig` |
| `04_ONBOARD_SENSOR.dtdl.json` | `TemperatureSensorConfig`, `VoltageSensorConfig`, `FanspeedSensorConfig` |
| `05_HARDWARE_FEATURE.dtdl.json` | `GpioSensorConfig`, `WatchdogSensorConfig`, `ThermalProtectionSensorConfig` |

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
        "01_SYSTEM_RESOURCE.dtdl.json",
        "02_GPU_RESOURCE.dtdl.json",
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
| Malformed JSON. | Wrong `enumValue` (e.g. `"USAGE"` instead of `"usage"`) — that's a **semantic** mismatch with the agent, not a DTDL bug. |
| Missing required fields (`@id`, `@type`). | Whether a `devicecfg.json` Sensor entry actually conforms — that's what config-level validation in §2 covers. |
| Invalid DTMI format. | Cross-Interface invariants outside the schema (e.g. `Interface` / `Interfaces` mutual exclusivity). |
| Unresolvable `schema` references (e.g. DTMI typo on an Object `field.schema`). | DTMI version-bump correctness (the parser allows additive changes). |
| Invalid `comment` / `description` exceeding the 512-char DTDL v2 limit. | |
| DTMI collisions across Interfaces in the combined parse. | |

---

## 2. Config-level validation (`Parameters.*` shape rules)

The DTDL parser checks the schema; it does **not** check whether a *sensor entry in `devicecfg.json`* is well-formed. That's what `validate-configs.js` (and its .NET twin `ConfigsValidator.cs`) is for.

> **Formally declared in the DTDL too.** Every `*SensorConfig` Interface declares its editable surface as Properties in `contents[]` — `Enabled`, `Interval`, `MetricName`, type-specific extras like `MountPoint` / `Interfaces` / `Sources` / `PinIds`, etc. — with native `writable=true/false`. That makes the form spec machine-discoverable through the official DTDL parser: backends use it for type+enum validation, frontends use it to render config forms (Property `displayName` → label, `description` → help text, `schema` → input control, `writable` → editable vs read-only). The runtime rules DTDL still can't express (`Interval` range, uniqueness, mutex, conditional-required, `Schema`↔`MetricName` cross-consistency) live in this validator's `config-rules.js` and are echoed in each Property's `description`.

### 2.1 Ruleset shipped today

| MetricType | Rules |
|------------|-------|
| `network`     | `Parameters.Interfaces` is `string` or `array<string>`; `Parameters.Interface` is a non-empty string when present; `Interface` and `Interfaces` are mutually exclusive when both non-empty. |
| `disk`        | `Parameters.MountPoint` is required and must be a non-empty string (no auto-detect; defaults to `"/"` only inside the agent's fallback path, never silently from validation). |
| `gpio`        | `Parameters.PinId` is a non-negative integer when present (`metricName=pinState`); `Parameters.PinIds` is `Array<integer>` of non-negative integers; `PinId` and `PinIds` are mutually exclusive when both non-empty; `metricName=isSupported` sensors must not carry `PinId` / `PinIds`. |
| `temperature` | `Parameters.Source` is a non-empty string when present; `Parameters.Sources` is an array of non-empty strings; `Source` and `Sources` are mutually exclusive when both non-empty; v1.0-compat sensors (`metricName != "therm"`) must not carry `Source` / `Sources` (the source name lives in `metricName` itself). |

MetricTypes without a declared rule set (`cpu`, `memory`, `system`, `gpu`, `hwinfo`, `voltage`, `fanspeed`, `watchdog`, `thermalprotection`) carry no `Parameters.*` shape constraints beyond the enum allow-lists the DTDL already enforces — sensors of those types pass through `validate-configs.js` unchecked.

### 2.2 Fixture shape

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

Fixtures live in `docs/Metrics/samples-config/` — one file per DTDL Interface. Shipped today:

- `01_SYSTEM_RESOURCE.configs.json` — 27 sensors (18 valid + 9 invalid; covers `cpu`, `memory`, `disk`, `network`, `system`).
- `02_GPU_RESOURCE.configs.json` — 2 sensors (2 valid; covers `gpu`).
- `03_HARDWARE_INFO.configs.json` — 6 sensors (6 valid; one fixture per hwinfo `MetricName`).
- `04_ONBOARD_SENSOR.configs.json` — 10 sensors (6 valid + 4 invalid; covers `temperature` v1.0/v1.1 modes, `voltage`, `fanspeed`).
- `05_HARDWARE_FEATURE.configs.json` — 14 sensors (7 valid + 7 invalid; covers `gpio` integer-only contract, `watchdog`, `thermalprotection`).

### 2.3 Running

```bash
# Local Node
cd dtdl-validate/scripts && npm run validate:configs

# Docker -- both Node and .NET containers run dtdl + configs in sequence
cd dtdl-validate
docker compose run --rm validate            # Node
docker compose run --rm validate-dotnet     # .NET
docker compose run --rm validate-dotnet configs   # config-only subcommand
```

Expected tail of any run:

```
--- 01_SYSTEM_RESOURCE.configs.json (27 sensors) ---
  OK  valid   cpu_usage
  OK  valid   memory_total
  ...
  OK  invalid network_bytes_sent_ifaces_mixed

--- 04_ONBOARD_SENSOR.configs.json (10 sensors) ---
  OK  valid   temperature_therm_source
  ...
  OK  invalid temperature_v10_with_source

--- 05_HARDWARE_FEATURE.configs.json (14 sensors) ---
  OK  valid   gpio_pin_4
  ...
  OK  valid   thermalprotection_supported

ALL 59 CONFIGS PASSED
```

A fixture marked `"expect": "invalid"` *should* fail the rule. The validator passes when **every sensor lands on its declared verdict** — an `invalid` sample that incorrectly passes is a regression.

### 2.4 Adding a rule (e.g. require `MountPoint` for `disk`)

The rules table in `config-rules.js` (and `ConfigRules.cs`) is keyed by `MetricType`. To add `disk`-specific checks:

```javascript
// dtdl-validate/scripts/config-rules.js
const rules = {
  network: [ /* …existing… */ ],
  gpio:    [ /* …existing… */ ],

  disk: [
    sensor => {
      const mp = sensor.Parameters?.MountPoint;
      if (typeof mp === 'string' && mp.length > 0) return { ok: true };
      return { ok: false, reason: 'disk sensors require a non-empty Parameters.MountPoint' };
    },
  ],
};
```

Then drop fixtures into the matching `samples-config/*.configs.json` file (the one named after the DTDL Interface that owns the MetricType) and CI picks them up automatically.

The same pattern in `ConfigRules.cs` is one entry in the `Rules` dictionary plus the rule function — keep the two languages in lock-step (the test fixtures are shared, so they must produce identical verdicts).

---

## 3. Run inside a Docker container

If you don't want Node.js on the host (CI agents, dev VMs, locked-down developer machines) — bring the validator up in a container instead. The image bakes in the parser; DTDL files and sample-config fixtures are bind-mounted at run time so the same image evaluates any branch without rebuilds.

### 3.1 One-command flow

From the `dtdl-validate/` directory at the project root (the validator code and the compose file live here; the DTDLs they read sit in `docs/Metrics/` and are bind-mounted automatically):

```bash
docker compose run --rm validate
```

That builds the image on first use (~30 s, cached afterwards) and runs **both** validators against the working tree. Exit code propagates: 0 means clean, non-zero means at least one DTDL file or sensor-config fixture failed.

To run only one of the validators:

```bash
# Schema-level only
docker compose run --rm validate node /app/validate-dtdl.js

# Config-level only
docker compose run --rm validate node /app/validate-configs.js
```

### 3.2 Without compose — raw `docker run`

From `dtdl-validate/scripts/`:

```bash
docker build -t weda-dtdl-validate .
docker run --rm -v "$(pwd)/../../docs/Metrics:/work:ro" weda-dtdl-validate
```

The image is ~155 MB on top of `node:20-alpine` (parser binary is the bulk). The container runs as the non-root `node` user (UID 1000) and the bind-mount is read-only — the validator never writes to your tree.

### 3.3 What's in the image

| Path | Contents |
|------|----------|
| `/app/node_modules/` | `@azure/dtdl-parser` and its transitive deps (pinned in `package.json`). |
| `/app/validate-dtdl.js` | Schema-level validator. |
| `/app/validate-configs.js` | Config-level validator. |
| `/app/config-rules.js` | Per-MetricType `Parameters.*` rules. |
| `/work/` (bind-mount) | The five `*.dtdl.json` files and the `samples-config/` folder, mounted from your host. |
| Env `DTDL_BASE=/work` | Tells both scripts where to read DTDLs / samples-config from. |

### 3.4 Pinning the image for CI

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

## 4. CI integration

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

---

## 5. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `ERR Cannot create instance of class 'Telemetry'` | A required field (`@id`, `name`, `schema`) is missing on a Telemetry. | Re-add it. |
| `ERR DTMI 'dtmi:…' is invalid` | DTMI segment violates `[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?` or version is missing (`;1`). | Fix the DTMI. |
| `ERR multiple definitions for dtmi:…` | The same DTMI exists in two files passed to a combined parse. | Re-namespace one of them. |
| `ERR unrecognized type 'DataSiZe'` | Misspelled DTDL semantic type. Case-sensitive. | Correct to `DataSize`. |
| `ERR unit 'byteS' is invalid for DataSize` | Misspelled unit; the parser ships an allow-list per semantic type. | See the DTDL v2 semantic-type cheat sheet in `_template/README.md`. |
| `validate-configs.js` reports `OK skipped` (or no rules ran) for a MetricType you expected to check | The MetricType has no ruleset registered in `config-rules.js`. | Add a rule-set keyed by that MetricType (see §2.4). |
| Sample with `expect: "invalid"` is reported as `OK invalid …` | Correct — the rule **rejected** the invalid sample, which matches the declared expectation. The script prints `OK` for any sample that lands on its declared verdict. |
| Sample with `expect: "invalid"` is reported as `ERR invalid …` | The rule **accepted** an invalid sample. Either the rule is too lax or the sample's `expect` is wrong. Inspect both. |

---

## 6. When to bump the Interface DTMI version

Per DTDL v2 compatibility rules:

| Change | Bump `;1` → `;2`? |
|--------|------------------|
| Add a new Telemetry | No |
| Add a new Enum value to a `schemas[]` Enum | No |
| Add a new reusable Enum or Object in `schemas[]` | No |
| Tighten a `comment` rule (text change only) | No |
| Change a Telemetry's `schema` (`long` → `integer`) | **Yes** |
| Remove a Telemetry | **Yes** |
| Remove an Enum value | **Yes** |
| Rename a Telemetry `name` | **Yes** |

Bumping the Interface DTMI requires updating every consumer's `*Dtmis.Interface` constant and any cloud-side twin registration that pins the version.

---

## Related

- [`01_SYS_RES_CPU_NETWORK_FIELDS.md`](./01_SYS_RES_CPU_NETWORK_FIELDS.md), [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](./01_SYS_RES_MEMORY_DISK_FIELDS.md), [`02_GPU_FIELDS.md`](./02_GPU_FIELDS.md), [`03_HARDWARE_INFO_FIELDS.md`](./03_HARDWARE_INFO_FIELDS.md), [`04_ONBOARD_SENSOR_FIELDS.md`](./04_ONBOARD_SENSOR_FIELDS.md), [`05_HARDWARE_FEATURE_FIELDS.md`](./05_HARDWARE_FEATURE_FIELDS.md) — per-MetricType configuration reference.
- The matching `*_USAGE.md` companions — .NET consumption guides showing parser setup, Enum allow-list extraction, and Sensor-config validation against the DTDL.
- [`_template/README.md`](./_template/README.md) — adding a new MetricType (don't forget to register a `config-rules.js` rule-set if it has type-specific `Parameters.*`).
