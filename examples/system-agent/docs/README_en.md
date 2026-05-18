# System Agent Documentation Guide

This directory contains all technical documentation for the System Agent. Below is a map of each document's purpose and recommended reading order.

---

## Human-Readable Docs (Getting Started)

| Order | File | Description |
|-------|------|-------------|
| 1 | [01_QUICK_START.md](01_QUICK_START.md) | Deployment & startup |
| 2 | [Metrics/00_DEVICECFG.md](Metrics/00_DEVICECFG.md) | `devicecfg.json` structure overview (one-page) |
| 3 | [v1.0/Sensor-Configuration-and-Usage-Guide_en.md](v1.0/Sensor-Configuration-and-Usage-Guide_en.md) | Full Sensor configuration reference (v1.0) |
| 4 | [v1.1/Sensor-Configuration-and-Usage-Guide_en.md](v1.1/Sensor-Configuration-and-Usage-Guide_en.md) | v1.1 differences: Auto-detect / Explicit list / Bound mode |
| 5 | [03_DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) | Docker deployment guide |
| 6 | [04_TESTING_GUIDE.md](04_TESTING_GUIDE.md) | Testing guide |

> **Chinese versions**: Each English doc has a corresponding Chinese version in the same directory (without the `_en` suffix).

---

## AI-Consumable Docs (Integration / Development)

Feed the following files to an AI assistant when adding sensors, modifying configs, or developing integrations:

```
# Field definitions (includes Auto-detect / Explicit list / Bound mode specs per MetricType)
docs/Metrics/01_CPU_NETWORK_FIELDS.md
docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU_FIELDS.md
docs/Metrics/03_HARDWARE_INFO_FIELDS.md
docs/Metrics/04_ONBOARD_SENSOR_FIELDS.md
docs/Metrics/05_HARDWARE_FEATURE_FIELDS.md

# Actual devicecfg.json configs
examples/system-agent/devicecfg.json
examples/system-agent/devicecfg-explicit-list.json
```

> **Why are `v1.0/` and `v1.1/` docs not listed?**
> Each `Metrics/*_FIELDS.md` section already covers v1.0/v1.1 differences, three resolution modes (Bound / Explicit list / Auto-detect), priority rules, and examples inline.

---

## Directory Structure

```
docs/
├── README.md                          — Navigation (Chinese)
├── README_en.md                       ← You are here
├── 01_QUICK_START.md                  — Quick deployment
├── 03_DOCKER_DEPLOY.md                — Docker deployment details
├── 04_TESTING_GUIDE.md                — Testing methodology
│
├── v1.0/                              — v1.0 baseline docs
│   ├── Sensor-Configuration-and-Usage-Guide.md     — Full Sensor config reference
│   ├── Sensor-Configuration-and-Usage-Guide_en.md
│   ├── METRIC-TYPES.md                             — MetricType field details
│   └── METRIC-TYPES_en.md
│
├── v1.1/                              — v1.1 delta docs
│   ├── Sensor-Configuration-and-Usage-Guide.md     — Field modification rules + resolution modes
│   ├── Sensor-Configuration-and-Usage-Guide_en.md
│   ├── METRIC-TYPES.md                             — Resolution mode details
│   └── METRIC-TYPES_en.md
│
└── Metrics/                           — Per-MetricType technical docs
    ├── 00_DEVICECFG.md                — devicecfg.json structure overview
    ├── 01_CPU_NETWORK_FIELDS.md       — cpu + network field definitions
    ├── 01_CPU_NETWORK.dtdl.json       — DTDL Interface
    ├── 01_CPU_NETWORK_USAGE.md        — Usage scenarios & examples
    ├── 02_MEMORY_DISK_SYSTEM_GPU_*    — memory + disk + system + gpu
    ├── 03_HARDWARE_INFO_*             — hwinfo
    ├── 04_ONBOARD_SENSOR_*            — temperature + voltage + fanspeed
    ├── 05_HARDWARE_FEATURE_*          — gpio + watchdog + thermalprotection
    ├── HOWTO_EVALUATE_DTDL.md         — DTDL validation methodology
    ├── _template/                     — Template for adding new MetricTypes
    └── samples/                       — Sample data for DTDL validation
```

---

## DTDL Validation Tool

Located at `examples/system-agent/dtdl-validate/`. Validates DTDL Interface definitions and sample data correctness.

| Path | Description |
|------|-------------|
| `dtdl-validate/docker-compose.yml` | One-command validation (includes both .NET and Node.js validators) |
| `dtdl-validate/scripts-dotnet/` | .NET validator (uses Microsoft DTDLParser) |
| `dtdl-validate/scripts/` | Node.js validator (lightweight alternative) |

Usage:

```bash
cd examples/system-agent/dtdl-validate
docker compose up --build
```

What it validates:
- DTDL JSON syntax and structural correctness
- Telemetry schema consistency with `devicecfg.json` `SensorInfo.Schema`
- Sample data type conformance against DTDL definitions

For details, see [HOWTO_EVALUATE_DTDL.md](Metrics/HOWTO_EVALUATE_DTDL.md).

---

## Configuration Files

| File | Purpose |
|------|---------|
| `devicecfg.json` | Default recommended config (uses `[]` auto-detect mode) |
| `devicecfg-explicit-list.json` | Advanced example: explicit list mode + single-bound sensor syntax |

---

## Other References

| File | Description |
|------|-------------|
| [CODE_REVIEW_REPORT.md](CODE_REVIEW_REPORT.md) | Code review report |
| [FAIL_SAFE_IMPLEMENTATION_REPORT.md](FAIL_SAFE_IMPLEMENTATION_REPORT.md) | Fail-safe implementation report |
| [SIGSEGV.md](SIGSEGV.md) | SIGSEGV troubleshooting notes |
