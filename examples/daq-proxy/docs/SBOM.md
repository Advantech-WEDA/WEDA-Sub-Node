# Software Bill of Materials (SBOM)

**Component:** `daq-feature-proxy`
**Version:** 1.0.0
**Date:** 2026-06-23
**Format:** Markdown (SPDX-inspired)
**Author:** Advantech — Weda SubNode Team

---

## 1. Primary Component

| Field | Value |
|---|---|
| Name | daq-feature-proxy |
| Version | 1.0.0 |
| Type | Application (Console / Worker Service) |
| Language | C# |
| Target Framework | net10.0 |
| Runtime | .NET 10 (self-contained) |
| License | Proprietary — Advantech |
| Repository | edge_subnode |

**Role:** Pure-software proxy that subscribes to PHM feature streams published by `daq-data-collector` over NATS, applies analysis transforms, and re-publishes results. It has **no direct hardware dependencies**.

---

## 2. Internal Project References (Weda SubNode SDK)

Source-built projects within the same repository.

| Component | Path | Version | License |
|---|---|---|---|
| Weda.SubNode.Host | `src/Weda.SubNode.Host` | 1.0.0 | Proprietary |
| Weda.SubNode.Core | `src/Weda.SubNode.Core` | 1.0.0 | Proprietary |
| Weda.SubNode.Cloud | `src/Weda.SubNode.Cloud` | 1.0.0 | Proprietary |
| Weda.SubNode.Devices | `src/Weda.SubNode.Devices` | 1.0.0 | Proprietary |
| Weda.SubNode.Abstractions | `src/Weda.SubNode.Abstractions` | 1.0.0 | Proprietary |

---

## 3. Direct NuGet Dependencies

Packages declared directly in `daq-feature-proxy.csproj`. Versions are resolved transitively via `Weda.SubNode.Host`.

| Package | Resolved Version | Source | Purpose | License |
|---|---|---|---|---|
| Serilog | 4.1.0 | nuget.org | Structured logging core | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | nuget.org | Microsoft.Extensions.Logging bridge | Apache-2.0 |
| Serilog.Settings.Configuration | 8.0.4 | nuget.org | appsettings.json configuration for Serilog | Apache-2.0 |
| Serilog.Sinks.Console | 6.0.0 | nuget.org | Console log sink | Apache-2.0 |

> Note: Unlike `daq-data-collector`, this project does **not** depend on `Advantech.Edge` or `MathNet.Numerics` — it consumes pre-computed feature data via NATS.

---

## 4. Transitive NuGet Dependencies

Packages resolved at restore time via `project.assets.json`. All come from nuget.org.

### 4.1 NATS Messaging

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

### 4.2 OPC UA (OPC Foundation)

| Package | Version | License |
|---|---|---|
| OPCFoundation.NetStandard.Opc.Ua.Client | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Configuration | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Core | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Security.Certificates | 1.5.378.134 | MIT |
| OPCFoundation.NetStandard.Opc.Ua.Types | 1.5.378.134 | MIT |

### 4.3 Resilience / Caching

| Package | Version | License |
|---|---|---|
| Polly.Core | 8.5.0 | BSD-3-Clause |
| Polly.Extensions | 8.5.0 | BSD-3-Clause |
| BitFaster.Caching | 2.5.4 | MIT |

### 4.4 MQTT

| Package | Version | License |
|---|---|---|
| MQTTnet | 4.3.7.1207 | MIT |

### 4.5 Error Handling

| Package | Version | License |
|---|---|---|
| ErrorOr | 2.0.1 | MIT |

### 4.6 Serialization

| Package | Version | License |
|---|---|---|
| Newtonsoft.Json | 13.0.4 | MIT |

### 4.7 Serilog (full set including transitive)

| Package | Version | License |
|---|---|---|
| Serilog | 4.1.0 | Apache-2.0 |
| Serilog.Extensions.Hosting | 8.0.0 | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | Apache-2.0 |
| Serilog.Settings.Configuration | 8.0.4 | Apache-2.0 |
| Serilog.Sinks.Console | 6.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |

### 4.8 Microsoft.Extensions (all resolved versions)

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
| Microsoft.Extensions.Logging | 10.0.2 | MIT |
| Microsoft.Extensions.Options | 10.0.2 | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.2 | MIT |
| Microsoft.Extensions.Primitives | 10.0.2 | MIT |

### 4.9 System / BCL Supplements

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

> Note: Unlike `daq-data-collector`, this Dockerfile does **not** run any `apt-get` operations and does not bind-mount host system libraries. The container is fully self-contained.

---

## 6. Configuration Files

These files are not compiled into the binary but are required at runtime (bind-mounted via `docker-compose.yml`).

| File | Purpose |
|---|---|
| `appsettings.json` | Serilog logging configuration |
| `systemcfg.json` | WEDA Node connection (URL, auth) |
| `devicecfg.json` | Proxy device configuration |
| `customcfg.json` | Application-level custom settings |

---

## 7. Runtime Service Dependency

`daq-feature-proxy` does not access hardware directly. It depends on the following external service being reachable at runtime.

| Service | Protocol | Default Address | Description |
|---|---|---|---|
| WEDA Node (NATS) | NATS | `127.0.0.1:4224` | Message broker — transport layer for all inter-service communication |
| daq-data-collector | NATS (via WEDA Node) | — | Upstream data producer; must be running and publishing PHM feature streams for this proxy to receive any data |

---

## 8. License Summary

| License | Packages |
|---|---|
| MIT | ErrorOr, MQTTnet, BitFaster.Caching, OPC UA stack, Newtonsoft.Json, all Microsoft.Extensions.*, System.*, .NET SDK/Runtime |
| Apache-2.0 | Serilog family, NATS.Net family |
| BSD-3-Clause | Polly.Core, Polly.Extensions |
| Proprietary | Weda SubNode SDK |

---

## 9. Notes

- This component contains **no direct hardware dependencies**. It receives pre-computed PHM feature data from `daq-data-collector` via the NATS message broker and applies further analysis transforms (`ProxyAnalysisTransform`).
- Serilog package versions are not pinned in `daq-feature-proxy.csproj`; they are resolved transitively through `Weda.SubNode.Host`. The versions listed in this SBOM reflect the values in `project.assets.json` at the time of last restore.
- The application is compiled as a **self-contained** .NET binary. No separate .NET runtime installation is required on the target host.
- The `weda-data/` directory is bind-mounted to `/app/.weda` to persist device registration state across container restarts.
