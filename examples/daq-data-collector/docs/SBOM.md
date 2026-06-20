# Software Bill of Materials (SBOM)

**Component:** `daq-data-collector`
**Version:** 1.0.0
**Date:** 2026-06-23
**Format:** Markdown (SPDX-inspired)
**Author:** Advantech — Weda SubNode Team

---

## 1. Primary Component

| Field | Value |
|---|---|
| Name | daq-data-collector |
| Version | 1.0.0 |
| Type | Application (Console / Worker Service) |
| Language | C# |
| Target Framework | net10.0 |
| Runtime | .NET 10 (self-contained) |
| License | Proprietary — Advantech |
| Repository | edge_subnode |

---

## 2. Internal Project References (Weda SubNode SDK)

These are source-built projects within the same repository. No external packages are consumed directly — they are resolved transitively through the sub-projects listed here.

| Component | Path | Version | License |
|---|---|---|---|
| Weda.SubNode.Host | `src/Weda.SubNode.Host` | 1.0.0 | Proprietary |
| Weda.SubNode.Core | `src/Weda.SubNode.Core` | 1.0.0 | Proprietary |
| Weda.SubNode.Cloud | `src/Weda.SubNode.Cloud` | 1.0.0 | Proprietary |
| Weda.SubNode.Devices | `src/Weda.SubNode.Devices` | 1.0.0 | Proprietary |
| Weda.SubNode.Abstractions | `src/Weda.SubNode.Abstractions` | 1.0.0 | Proprietary |

---

## 3. Direct NuGet Dependencies

Packages declared directly in `daq-data-collector.csproj`.

| Package | Version | Source | Purpose | License |
|---|---|---|---|---|
| Advantech.Edge | 2.0.2-rc7 | nuget.org | Advantech DAQ hardware abstraction | Proprietary |
| MathNet.Numerics | 5.0.0 | nuget.org | Signal processing / PHM feature computation | MIT |
| Serilog | 4.1.0 | nuget.org | Structured logging core | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | nuget.org | Microsoft.Extensions.Logging bridge | Apache-2.0 |
| Serilog.Settings.Configuration | 8.0.4 | nuget.org | appsettings.json configuration for Serilog | Apache-2.0 |
| Serilog.Sinks.Console | 6.0.0 | nuget.org | Console log sink | Apache-2.0 |

---

## 4. Transitive NuGet Dependencies

Packages resolved at restore time via `project.assets.json`. All come from nuget.org unless noted.

### 4.1 Advantech / Internal

| Package | Version | License |
|---|---|---|
| Advantech.Edge | 2.0.2-rc7 | Proprietary |

### 4.2 NATS Messaging

| Package | Version | License |
|---|---|---|
| NATS.Net | 2.6.10 | Apache-2.0 |
| NATS.Client.Core | 2.6.10 | Apache-2.0 |
| NATS.Client.Hosting | 2.6.10 | Apache-2.0 |
| NATS.Client.JetStream | 2.6.10 | Apache-2.0 |
| NATS.Client.KeyValueStore | 2.6.10 | Apache-2.0 |
| NATS.Client.ObjectStore | 2.6.10 | Apache-2.0 |
| NATS.Client.Serializers.Json | 2.6.10 | Apache-2.0 |
| NATS.Client.Services | 2.6.10 | Apache-2.0 |
| NATS.Client.Simplified | 2.6.10 | Apache-2.0 |

### 4.3 OPC UA (OPC Foundation)

| Package | Version | License |
|---|---|---|
| OPCFoundation.NetStandard.Opc.Ua.Client | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Configuration | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Core | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Security.Certificates | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Types | 1.5.378.134 | MIT |

### 4.4 Resilience / Caching

| Package | Version | License |
|---|---|---|
| Polly.Core | 8.5.0 | BSD-3-Clause |
| Polly.Extensions | 8.5.0 | BSD-3-Clause |
| BitFaster.Caching | 2.5.4 | MIT |

### 4.5 MQTT

| Package | Version | License |
|---|---|---|
| MQTTnet | 4.3.7.1207 | MIT |

### 4.6 Error Handling

| Package | Version | License |
|---|---|---|
| ErrorOr | 2.0.1 | MIT |

### 4.7 Serialization

| Package | Version | License |
|---|---|---|
| Newtonsoft.Json | 13.0.4 | MIT |

### 4.8 Serilog (full set including transitive)

| Package | Version | License |
|---|---|---|
| Serilog | 4.1.0 | Apache-2.0 |
| Serilog.Extensions.Hosting | 8.0.0 | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | Apache-2.0 |
| Serilog.Settings.Configuration | 8.0.4 | Apache-2.0 |
| Serilog.Sinks.Console | 6.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |

### 4.9 Microsoft.Extensions (all resolved versions)

| Package | Version | License |
|---|---|---|
| Microsoft.Extensions.Configuration | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.Binder | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.Json | 10.0.2 | MIT |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.2 | MIT |
| Microsoft.Extensions.DependencyInjection | 10.0.2 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.DependencyModel | 8.0.2 | MIT |
| Microsoft.Extensions.Diagnostics | 10.0.2 | MIT |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.FileProviders.Physical | 10.0.2 | MIT |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.2 | MIT |
| Microsoft.Extensions.Hosting | 10.0.2 | MIT |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.Logging | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.Configuration | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.Console | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.Debug | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.EventLog | 10.0.2 | MIT |
| Microsoft.Extensions.Logging.EventSource | 10.0.2 | MIT |
| Microsoft.Extensions.Options | 10.0.2 | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.2 | MIT |
| Microsoft.Extensions.Primitives | 10.0.2 | MIT |

### 4.10 System / BCL Supplements

| Package | Version | License |
|---|---|---|
| System.Diagnostics.EventLog | 10.0.2 | MIT |
| System.IO.Hashing | 9.0.3 | MIT |
| System.IO.Ports | 10.0.2 | MIT |

---

## 5. Container / Docker Components

### 5.1 Build-Stage Base Image

| Field | Value |
|---|---|
| Image | `mcr.microsoft.com/dotnet/sdk:10.0` |
| Platform | `$BUILDPLATFORM` (AMD64 forced for cross-compilation) |
| Purpose | Builds and publishes the self-contained binary |
| Publisher | Microsoft |
| License | MIT (.NET SDK) |

### 5.2 Runtime Base Image

| Field | Value |
|---|---|
| Image | `mcr.microsoft.com/dotnet/runtime:10.0-noble` |
| OS | Ubuntu 24.04 (Noble Numbat) |
| Purpose | Minimal .NET runtime for containerised deployment |
| Publisher | Microsoft |
| License | MIT (.NET Runtime) + Ubuntu open-source stack |

### 5.3 Runtime APT Package (patched in Dockerfile)

| Package | Version | Purpose | License |
|---|---|---|---|
| gpgv | 2.4.4-2ubuntu17.4 | CVE fix applied at image build time | GPL-2.0 |

---

## 6. Runtime System Libraries (host-mounted via Docker volumes)

These libraries are **not bundled** in the container image; they are bind-mounted from the host at runtime and must be present on the target Advantech hardware.

### 6.1 Advantech SUSI Driver

| Library | Purpose |
|---|---|
| `libSUSI-4.00.so` | Advantech SUSI hardware abstraction layer |
| `libSUSI-4.00.so.1` | SUSI shared library (major version symlink) |
| `libSUSI-4.00.so.1.0.0` | SUSI shared library (full version) |
| `libSusiIoT.so` | Advantech SusiIoT extension |
| `libSusiIoT.so.1.0.0` | SusiIoT shared library (full version) |

### 6.2 Jansson JSON Library

| Library | Purpose |
|---|---|
| `libjansson.a` | Static Jansson JSON library |
| `libjansson.so` | Jansson JSON shared library |
| `libjansson.so.4` | Jansson major version symlink |
| `libjansson.so.4.11.0` | Jansson full version shared library |
| License | MIT |

### 6.3 Advantech DAQNavi

Mounted from `/opt/advantech/` and `/var/lib/daq/` on the host. Provides the DAQNavi kernel driver interface for `/dev/daq*` character devices.

| Component | Source path | Purpose |
|---|---|---|
| DAQNavi runtime | `/opt/advantech/` | DAQ device driver runtime |
| DAQ device state | `/var/lib/daq/` | Persistent DAQ state / calibration data |
| License | Proprietary — Advantech |

---

## 7. Hardware Interface

| Device Node | Description |
|---|---|
| `/dev/daq0` | DAQ module 0 |
| `/dev/daq1` | DAQ module 1 |
| `/dev/daq2` | DAQ module 2 |
| `/dev/daq3` | DAQ module 3 |
| `/dev/daq255` | DAQ broadcast / control node |

---

## 8. Configuration Files

These files are not compiled into the binary but are required at runtime.

| File | Purpose |
|---|---|
| `appsettings.json` | Serilog logging configuration |
| `systemcfg.json` | WEDA Node connection (URL, auth) |
| `devicecfg.json` | DAQ device hardware configuration |
| `customcfg.json` | Application-level custom settings |

---

## 9. License Summary

| License | Packages |
|---|---|
| MIT | MathNet.Numerics, ErrorOr, MQTTnet, BitFaster.Caching, OPC UA stack, Newtonsoft.Json, all Microsoft.Extensions.*, System.*, libjansson, .NET SDK/Runtime |
| Apache-2.0 | Serilog family, NATS.Net family |
| BSD-3-Clause | Polly.Core, Polly.Extensions |
| GPL-2.0 | gpgv (Ubuntu runtime APT package) |
| Proprietary | Advantech.Edge, libSUSI, libSusiIoT, DAQNavi, Weda SubNode SDK |

---

## 10. Notes

- `Advantech.Edge 2.0.2-rc7` is published on **nuget.org** and can be restored directly from there. The `local_repo` source in `nuget.config` may still be used for offline or pre-release scenarios but is not required for this version.
- The application is compiled as a **self-contained** .NET binary (`--self-contained true`). The .NET runtime is bundled inside the Docker image layer rather than installed separately on the host.
- SUSI and DAQNavi libraries are **host-injected** and are not part of the container image itself; the SBOM entry reflects a runtime dependency, not a build artifact.
- The `gpgv` APT package is explicitly pinned to `2.4.4-2ubuntu17.4` in the Dockerfile to address a known CVE present in the base Ubuntu Noble image at the time of build.
