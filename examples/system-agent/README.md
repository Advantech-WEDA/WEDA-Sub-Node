# System Agent

System Agent collects CPU, memory, disk, network, etc. metrics from the host system and sends them to Weda Node via NATS.

## Quick Start

See [docs/01_QUICK_START.md](docs/01_QUICK_START.md) for deployment instructions.

## Architecture Overview

The framework follows a layered architecture with clear separation of concerns:

```
+--------------------------------------------------------------------------------+
|                         Device Layered Architecture                            |
+--------------------------------------------------------------------------------+
|                                                                                |
|  +---------------------------------------------------------------------------+ |
|  |  LocalSystemAgentDevice                                                   | |
|  |  (Concrete Device - Creates and injects LocalSystemCommunication)         | |
|  +---------------------------------------------------------------------------+ |
|                                    | extends                                   |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  SystemAgentDeviceBase                                                    | |
|  |  (Protocol Device - Creates and injects  SystemMetricsParser)             | |
|  +---------------------------------------------------------------------------+ |
|                                    | extends                                   |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  RequestResponseDeviceBase (SubNode.Core Framework)                       | |
|  |  (Framework Base - Handles telemetry polling and health reporting)        | |
|  +---------------------------------------------------------------------------+ |
|                                    | extends                                   |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  DeviceBase (SubNode.Core Framework)                                      | |
|  |  (Core Base - Lifecycle and configuration management)                     | |
|  +---------------------------------------------------------------------------+ |
+--------------------------------------------------------------------------------+


+--------------------------------------------------------------------------------+
|          Communication & Collection  Layered Architecture                      |
+--------------------------------------------------------------------------------+
|                                                                                |
|  +---------------------------------------------------------------------------+ |
|  |  LocalSystemCommunication                                                 | |
|  |  (Communication - Request-response pattern coordinator)                   | |
|  +---------------------------------------------------------------------------+ |
|                                    | uses                                      |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  LocalSystemResourceCollector                                             | |
|  |  (Resource Coordinator - Orchestrates all metric collectors)              | |
|  +---------------------------------------------------------------------------+ |
|                                    | orchestrates                              |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  Metric Collectors (Core Implementations)                                 | |
|  |  - CpuCollector        - RamCollector        - DiskCollector              | |
|  |  - NetworkCollector    - SystemCollector     - GpuCollector               | |
|  |  - HardwarePlatformCollector (optional hardware-specific collector)       | |
|  +---------------------------------------------------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

### How It Works

1. **Weda Core -> Weda Node -> SystemAgent** (Downlink)
   - Cloud publishes config update via Weda Node
   - OnBeforeConfigUpdateAsync hook validates new config before applying

2. **SystemAgent -> Edge Device** (Within Device/Local)
   - LocalSystemCommunication uses collectors to gather metrics

3. **SystemAgent -> Weda Node -> Weda Core** (Uplink)
   - Publishes metrics to Weda Core via Weda Node


## Configuration Validation

Configuration is validated at **two stages**:

### Stage 1: Startup (Program.cs)

Before device initialization:
- File-level: Check appsettings.json exists and is valid JSON
- Device-level: Validate DeviceName, NATS URL format, sensor intervals
- Failure: Program terminates with Exit Code 1-5

```csharp
// File-level
var fileValidation = StartupConfigurationValidator.ValidateConfigFile("appsettings.json");

// Device-level
var deviceValidation = StartupConfigurationValidator.ValidateDeviceConfig(
    configuration,
    "SystemAgentDeviceConfig");
```

### Stage 2: Runtime (OnBeforeConfigUpdateAsync)

When cloud sends configuration update:
- Same validation rules as Stage 1
- Validates new config before applying
- Rejects invalid updates with error message

```csharp
protected override async Task OnBeforeConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
{
    var validation = StartupConfigurationValidator.ValidateDeviceConfig(
        configuration,
        "SystemAgentDeviceConfig");
    
    if (!validation.IsValid)
        throw new InvalidOperationException(validation.ErrorMessage);
    
    await base.OnBeforeConfigUpdateAsync(e, ct);
}
```

## Exit Codes

| Code | Meaning | Stage | Fix |
|------|---------|-------|-----|
| 0 | Success | - | N/A |
| 1 | ConfigFileNotFound | Startup (File) | Create appsettings.json |
| 2 | InvalidJsonFormat | Startup (File) | Fix JSON syntax |
| 3 | MissingRequiredField | Startup (Device) | Add missing field (DeviceName) |
| 4 | InvalidConfigValue | Startup (Device) | Fix invalid values (NATS URL, intervals) |
| 5 | ConfigValidationError | Startup (Device) | Check logs for details |
| 99 | UnexpectedError | Any | Check application logs |

Check exit code:
```powershell
dotnet run
$LASTEXITCODE
```

## Local Development

```powershell
# 1. Edit appsettings.json
Edit appsettings.json

# 2. Build
dotnet build

# 3. Run
dotnet run
```

## Docker Deployment

```powershell
docker compose up -d
```

See [docs/03_DOCKER_DEPLOY.md](docs/03_DOCKER_DEPLOY.md) for details.

## Troubleshooting

### Application Fails to Start

1. Check exit code: `$LASTEXITCODE`
2. Look at error message in console
3. Fix configuration according to error
4. Retry

**Common issues:**

| Error | Fix |
|-------|-----|
| "Configuration file not found" | Create appsettings.json |
| "Invalid JSON format" | Fix JSON syntax (missing commas, quotes) |
| "Missing required field: DeviceName" | Add DeviceName to appsettings.json |
| "Invalid NATS URL" | Use format: `nats://host:port` or `tls://host:port` |
| "Invalid interval" | Ensure intervals are positive integers |

### Syslog Growing Too Fast

1. Verify Docker log limits in docker-compose.yml:
   ```yaml
   logging:
     driver: "json-file"
     options:
       max-size: "50m"
       max-file: "3"
   ```

2. Check /var/log/syslog size on device:
   ```bash
   ls -lh /var/log/syslog
   df -h
   ```

3. Verify debug logging is disabled

## Documentation

- [docs/01_QUICK_START.md](docs/01_QUICK_START.md) - Quick start and deployment guide
- [docs/03_DOCKER_DEPLOY.md](docs/03_DOCKER_DEPLOY.md) - Docker deployment details
- [docs/04_TESTING_GUIDE.md](docs/04_TESTING_GUIDE.md) - Testing procedures and scripts
- [docs/02_METRIC_TYPES.md](docs/02_METRIC_TYPES.md) - MetricType classification and configuration
- [../../README.md](../../README.md) - Main project overview
- [../../docs/wiki/en/](../../docs/wiki/) - Framework documentation

