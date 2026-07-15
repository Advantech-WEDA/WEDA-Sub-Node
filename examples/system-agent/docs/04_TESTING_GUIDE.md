# Testing Guide

Guide for using test and monitoring scripts.

## Test Scripts

All scripts located in `scripts/` directory.

### test-config-validation.ps1

Validates startup configuration without running the full application.

**Purpose**: Verify configuration is correct before deployment.

**Usage**:
```powershell
cd examples/system-agent
.\scripts\test-config-validation.ps1
```

**What it tests**:
- Configuration file exists
- JSON syntax is valid
- Required fields present (DeviceName, NATS URL)
- NATS URL format correct (nats://host:port or tls://host:port)
- Sensor intervals are positive integers

**When to use**: After editing appsettings.json, before running application.

---

### test-hot-reload.ps1

Tests configuration updates at runtime via NATS.

**Purpose**: Verify configuration can be changed without restarting application.

**Prerequisites**:
- NATS server running
- system-agent application running (`dotnet run`)
- NATS CLI available (for publishing messages)

**Usage**:
```powershell
cd examples/system-agent
.\scripts\test-hot-reload.ps1
```

**What it tests**:
- Configuration update message format
- NATS topic subscription
- Configuration validation on update
- Sensor interval changes applied
- Configuration cached to disk

**Manual steps**:
1. Start application: `dotnet run`
2. Note current metric frequency in logs
3. Create update message with new interval
4. Publish via NATS: `nats pub "edgesync.device.<DeviceId>.config.update" @message.json`
5. Verify frequency changed in logs

**When to use**: Before deploying config changes to production, testing hot-reload feature.

---

### check-docker-logs.ps1

Diagnostic tool for logging and syslog issues on device.

**Purpose**: Investigate excessive log growth or Docker logging problems.

**Requirements**: Run on device with sudo privileges (Linux).

**Usage**:
```powershell
# On device
sudo pwsh ./scripts/check-docker-logs.ps1
```

**What it checks**:
- Docker daemon log driver (json-file, journald, etc.)
- Container log driver configuration
- Log file sizes and locations
- /var/log/syslog size and growth rate
- journald ForwardToSyslog setting
- logrotate configuration

**Sample output**:
```
=== Docker Daemon Log Driver ===
Default: json-file

=== Container: system-agent ===
Log Driver: json-file
Log Path: /var/lib/docker/containers/<id>/<id>-json.log
Log Size: 895 MB

=== /var/log/syslog ===
Size: 14 GB
Growth Rate: 9 MB/hour
```

**When to use**: When investigating:
- Syslog growing too large
- Container logs consuming disk space
- Need to verify Docker log limits applied
- Troubleshooting log-related performance issues

---

### monitor-advantech.sh

24-hour stress test monitoring memory and CPU usage.

**Purpose**: Detect memory leaks or resource issues during extended operation.

**Usage - Start monitoring**:
```bash
# On device, run in background
nohup ./scripts/monitor-advantech.sh > /tmp/monitor-output.log 2>&1 &
```

**Usage - View live output**:
```bash
tail -f /tmp/monitor-output.log
```

**Usage - View hourly summary**:
```bash
cat /tmp/memory-test-advantech.log
```

**Usage - Stop monitoring**:
```bash
pkill -f monitor-advantech.sh
```

**Test parameters**:
- Duration: 24 hours
- Sample interval: 5 minutes
- Total samples: 288
- Monitored metrics: Memory (MEM %), CPU %, Container uptime (UP)

**Expected behavior**:
- Memory usage stabilizes at 50-60 MB
- No continuous growth trend
- CPU usage remains consistent
- Container stays healthy for full 24 hours

**When to use**:
- Before production deployment
- After significant code changes
- When troubleshooting memory issues
- Validating Advantech sensor stability

---

## Local Testing Workflow

### 1. Validate Configuration

```powershell
.\scripts\test-config-validation.ps1
```

If validation fails, fix errors in appsettings.json per error message.

### 2. Run Application

```powershell
dotnet run
```

Verify application starts and begins sending telemetry.

### 3. Test Hot-Reload (Optional)

While application running in another terminal:
```powershell
.\scripts\test-hot-reload.ps1
```

Verify configuration updates work.

### 4. Check Docker Deployment

```powershell
docker-compose up --build
```

Verify application runs in container and sends telemetry.

### 5. Stress Test (Optional)

On device, run for 24 hours:
```bash
nohup ./scripts/monitor-advantech.sh > /tmp/monitor-output.log 2>&1 &
```

Monitor memory and CPU usage over time.

---

## Troubleshooting Tests

### Configuration Validation Fails

Check error message:
- **"Configuration file not found"** → Create appsettings.json
- **"Invalid JSON format"** → Fix JSON syntax (missing commas, quotes)
- **"Missing required field"** → Add missing field (DeviceName, NATS URL)
- **"Invalid NATS URL"** → Use format `nats://host:port` or `tls://host:port`
- **"Invalid interval"** → Ensure intervals are positive integers (milliseconds)

### Hot-Reload Not Working

1. Verify NATS server is running
2. Verify device is registered (check .weda/ directory exists)
3. Check NATS topic format: `edgesync.device.<DeviceId>.config.update`
4. Watch application logs for validation errors
5. Verify message JSON format is correct

### Docker Logs Too Large

1. Run check-docker-logs.ps1 to diagnose
2. Verify docker-compose.yml has logging limits:
   ```yaml
   logging:
     driver: "json-file"
     options:
       max-size: "50m"
       max-file: "3"
   ```
3. Check /var/log/syslog size with: `ls -lh /var/log/syslog`
4. Clear syslog if needed: `sudo truncate -s 0 /var/log/syslog`

### Memory Grows During Stress Test

If monitor-advantech.sh shows continuous memory growth:
1. Check application logs for errors
2. Verify no debug logging enabled
3. Check for leak in sensor collection loop
4. Consider reducing sample interval if necessary

---

## Related Documentation

- [README.md](../README.md) - Architecture and configuration
- [DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) - Docker deployment
