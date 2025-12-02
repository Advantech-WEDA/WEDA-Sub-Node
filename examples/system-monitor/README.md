# System Resources Monitoring Example

Cross-platform system resource monitoring using the Weda SubNode SDK. This example demonstrates how to collect detailed system metrics (CPU, Memory, Disk, Network) and publish them to the cloud via NATS messaging.

## Overview

This example showcases:
- **Manual Context Pattern**: Direct control over device lifecycle
- **Cross-Platform Metrics**: Uses .NET APIs for Windows/Linux/macOS compatibility
- **Detailed Monitoring**: Per-core CPU, memory breakdown, per-disk/interface statistics
- **Real-Time Telemetry**: Configurable collection intervals (default 10 seconds)
- **Cloud Integration**: Automatic device registration and telemetry publishing

## Prerequisites

- .NET 9.0 SDK or later
- Linux, Windows, or macOS operating system
- Weda EdgeSync Cloud access (or local NATS server for testing)

## Quick Start

1. **Clone the repository** (if not already done):
   ```bash
   cd examples/system-monitor
   ```

2. **Configure NATS connection** in `appsettings.json`:
   ```json
   "Nats": {
     "Url": "nats://your-nats-server:4224",
     "CredFile": ""
   }
   ```

3. **Run the example**:
   ```bash
   dotnet run
   ```

4. **View metrics** in console logs:
   ```
   [14:23:15.123 INF] System monitor started. Press Ctrl+C to stop...
   [14:23:15.456 INF] CPU Usage: 15.3%
   [14:23:15.457 INF] Memory Used: 8589934592 bytes
   [14:23:15.458 INF] Disk root usage: 45.2%
   ```

5. **Stop the monitor**:
   Press `Ctrl+C` for graceful shutdown

## Configuration

### Adjusting Collection Interval

Edit `appsettings.json` to change telemetry collection frequency:

```json
"Periods": {
  "ReadTelemetry": 10000,    // 10 seconds (10000 ms)
  "SendTelemetry": 10000,    // 10 seconds
  "ReportHealth": 60000      // 60 seconds
}
```

### Adding/Removing Sensors

The configuration includes sensors for common system resources. Add or remove sensors based on your system:

**CPU Cores**: Add sensors for additional cores
```json
{
  "Name": "cpu.core.4",
  "Dtmi": "dtmi:advantech:EdgeSync:CpuCore;1",
  "SensorGroup": "CPU",
  "Config": { "Enabled": true }
}
```

**Disk Drives**: Monitor additional drives
```json
{
  "Name": "disk.C.total",           // Windows: disk.C
  "Dtmi": "dtmi:advantech:EdgeSync:DiskTotal;1",
  "SensorGroup": "Disk",
  "Config": { "Enabled": true }
}
```

**Network Interfaces**: Monitor specific interfaces
```json
{
  "Name": "network.wlan0.bytes_sent",  // Common: eth0, wlan0, enp0s3
  "Dtmi": "dtmi:advantech:EdgeSync:NetworkBytesSent;1",
  "SensorGroup": "Network",
  "Config": { "Enabled": true }
}
```

### Finding Your System Resources

**Linux**:
```bash
# CPU cores
nproc

# Disk drives
df -h

# Network interfaces
ip link show
```

**Windows**:
```powershell
# CPU cores
$env:NUMBER_OF_PROCESSORS

# Disk drives
Get-PSDrive -PSProvider FileSystem

# Network interfaces
Get-NetAdapter
```

## Metrics Collected

### CPU Metrics

| Metric | Description | Platform | Type |
|--------|-------------|----------|------|
| `cpu.usage` | Overall CPU usage percentage | All | double (0-100) |
| `cpu.core.{N}` | Per-core CPU usage | Linux* | double (0-100) |
| `cpu.load.1m` | 1-minute load average | Linux | double |
| `cpu.load.5m` | 5-minute load average | Linux | double |
| `cpu.load.15m` | 15-minute load average | Linux | double |

\* Per-core CPU metrics use `/proc/stat` on Linux. Windows support requires additional permissions.

### Memory Metrics

| Metric | Description | Platform | Type |
|--------|-------------|----------|------|
| `memory.total` | Total system memory | All | long (bytes) |
| `memory.used` | Used memory | All | long (bytes) |
| `memory.available` | Available memory | Linux | long (bytes) |
| `memory.cached` | Cached memory | Linux | long (bytes) |
| `memory.buffers` | Memory buffers | Linux | long (bytes) |

### Disk Metrics

| Metric | Description | Platform | Type |
|--------|-------------|----------|------|
| `disk.{name}.total` | Total disk space | All | long (bytes) |
| `disk.{name}.used` | Used disk space | All | long (bytes) |
| `disk.{name}.available` | Available disk space | All | long (bytes) |
| `disk.{name}.usage_percent` | Disk usage percentage | All | double (0-100) |

Drive names: `root` (Linux `/`), `C` (Windows `C:\`), etc.

### Network Metrics

| Metric | Description | Platform | Type |
|--------|-------------|----------|------|
| `network.{iface}.bytes_sent` | Total bytes sent | All | long |
| `network.{iface}.bytes_received` | Total bytes received | All | long |
| `network.{iface}.packets_sent` | Total packets sent | All | long |
| `network.{iface}.packets_received` | Total packets received | All | long |
| `network.{iface}.errors` | Total packet errors | All | long |

Interface names are sanitized (spaces/dashes replaced with underscores): `eth0`, `wlan0`, `ethernet_adapter`, etc.

## Architecture

### Components

```
SystemMonitorDevice (DeviceBase)
├── SystemResourceCollector
│   ├── CollectCpuMetricsAsync()
│   ├── CollectMemoryMetricsAsync()
│   ├── CollectDiskMetricsAsync()
│   └── CollectNetworkMetricsAsync()
├── ReadTelemetryAsync() - Orchestrates collection
├── StartBackgroundTasksAsync() - Telemetry & health loops
└── NullCommunication - No physical device communication
```

### Manual Context Pattern

```csharp
using var context = new WedaApplicationContext();  // Auto-loads appsettings.json
var device = new SystemMonitorDevice(context);

await device.InitializeAsync();  // Register with cloud
await device.StartAsync();       // Start telemetry collection
// ... wait for Ctrl+C ...
await device.StopAsync();        // Stop gracefully
device.Dispose();                // Cleanup
```

### Data Flow

```
[System Resources]
       ↓
[SystemResourceCollector] → CollectCpuMetrics()
                          → CollectMemoryMetrics()
                          → CollectDiskMetrics()
                          → CollectNetworkMetrics()
       ↓
[TelemetryMeasure List]
       ↓
[DeviceBase.SendTelemetryAsync()]
       ↓
[NATS Messaging]
       ↓
[Weda EdgeSync Cloud]
```

## Platform Support

| Feature | Linux | Windows | macOS |
|---------|-------|---------|-------|
| CPU Usage (overall) | ✓ | ✓ | ✓ |
| CPU Usage (per-core) | ✓ | ○* | ○* |
| Load Average | ✓ | ✗ | ✓ |
| Memory (basic) | ✓ | ✓ | ✓ |
| Memory (detailed) | ✓ | ○ | ○ |
| Disk Metrics | ✓ | ✓ | ✓ |
| Network Metrics | ✓ | ✓ | ✓ |

**Legend**:
- ✓ Fully supported
- ○ Partially supported (may require permissions or have limitations)
- ✗ Not available on platform
- \* Per-core CPU on Windows/macOS may require elevated privileges or alternative APIs

## Example Output

```
[14:23:15.123 INF] System monitor started. Press Ctrl+C to stop...
[14:23:25.456 INF] === System Metrics ===
[14:23:25.457 INF] CPU:
[14:23:25.458 INF]   Overall: 15.3%
[14:23:25.459 INF]   Core 0: 12.1%
[14:23:25.460 INF]   Core 1: 18.5%
[14:23:25.461 INF]   Load Avg (1m): 1.23
[14:23:25.462 INF] Memory:
[14:23:25.463 INF]   Total: 16.0 GB
[14:23:25.464 INF]   Used: 8.5 GB
[14:23:25.465 INF]   Available: 7.5 GB
[14:23:25.466 INF] Disk (root):
[14:23:25.467 INF]   Total: 500.0 GB
[14:23:25.468 INF]   Used: 226.0 GB (45.2%)
[14:23:25.469 INF] Network (eth0):
[14:23:25.470 INF]   Sent: 1.5 GB
[14:23:25.471 INF]   Received: 12.3 GB
```

## Troubleshooting

### Issue: Permission denied reading /proc files (Linux)

**Symptom**: Warnings in logs about failing to read `/proc/stat` or `/proc/meminfo`

**Solution**: Run with appropriate permissions or check that /proc is mounted
```bash
# Check /proc is accessible
ls -la /proc/stat

# Run with sudo if needed (not recommended for production)
sudo dotnet run
```

### Issue: No network interfaces found

**Symptom**: No network metrics in telemetry

**Solution**:
1. Check interface names match your system:
   ```bash
   # Linux
   ip link show

   # Windows
   ipconfig
   ```

2. Update sensor names in `appsettings.json` to match your interfaces

### Issue: Disk metrics missing for some drives

**Symptom**: Some disks not reporting metrics

**Solution**:
1. Check that drives are mounted and ready:
   ```bash
   # Linux
   df -h

   # Windows
   Get-PSDrive -PSProvider FileSystem
   ```

2. Add sensors for each drive in `appsettings.json`

### Issue: High CPU usage from monitoring itself

**Symptom**: System monitor consuming significant CPU

**Solution**: Increase collection interval in `appsettings.json`:
```json
"Periods": {
  "ReadTelemetry": 30000,   // 30 seconds instead of 10
  "SendTelemetry": 30000
}
```

### Issue: Failed to initialize device

**Symptom**: Error message "Failed to initialize system monitor"

**Solution**:
1. Check NATS server is accessible:
   ```bash
   telnet your-nats-server 4224
   ```

2. Verify NATS configuration in `appsettings.json`

3. Check logs in `logs/` directory for detailed error messages

### Issue: Cannot find DTDL file

**Symptom**: Error about missing DTDL schema file

**Solution**: Ensure the DTDL file is copied to output directory:
```bash
# Check file exists
ls -la bin/Debug/net9.0/assets/dtdl/dtmi/advantech/edgesync/system-monitor.json

# If missing, rebuild
dotnet build
```

## Testing Locally

### Option 1: Mock Cloud Service

For testing without a real NATS server, the SDK can use a mock cloud service that logs telemetry locally.

### Option 2: Local NATS Server

Run a local NATS server with Docker:

```bash
# Start NATS server
docker run -d -p 4222:4222 nats:latest

# Update appsettings.json
{
  "Nats": {
    "Url": "nats://localhost:4222"
  }
}
```

## Advanced Usage

### Custom Metric Collection Intervals

Different sensors can be collected at different rates by implementing custom logic in `SystemResourceCollector`.

### Alerting on Thresholds

Add custom logic in `ReadTelemetryAsync()` to check for threshold violations:

```csharp
public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken ct)
{
    var measures = await base.ReadTelemetryAsync(ct);

    // Check CPU threshold
    var cpuUsage = measures.FirstOrDefault(m => m.ResourceId.Contains("cpu.usage"));
    if (cpuUsage?.Value is double cpu && cpu > 80.0)
    {
        _logger.LogWarning("High CPU usage detected: {Usage}%", cpu);
    }

    return measures;
}
```

### Integration with Other Systems

The telemetry data sent to NATS can be consumed by other systems for visualization, alerting, or analysis.

## Related Examples

- **wise-4012**: WISE-4012 Modbus TCP device monitoring
- **wise-4012-builder**: Builder pattern version of WISE-4012
- **wise-4012-isensing**: Advanced iSensing with anomaly detection

## Support

For issues, questions, or contributions, please refer to the main repository documentation.

## License

This example is part of the Weda SubNode SDK project.
