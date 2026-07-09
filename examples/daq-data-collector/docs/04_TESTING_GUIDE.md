# DAQ Data Collector Testing Guide

This guide covers manual end-to-end verification for DAQ Data Collector (no automated test project exists for it yet — see Testing Overview below).

---

## Testing Overview

> **Status**: There is currently no automated unit/integration test project for `daq-data-collector` itself. `tests/Weda.SubNode.Core.Tests/`, `tests/Weda.SubNode.Integration.Tests/`, and `tests/SystemAgentDevice.Tests/` are framework-level and other-example test projects — they do not cover DAQ-specific logic (feature extraction, DAQ communication, etc.). Until a dedicated test project exists, verification for this example is manual, via the End-to-End steps below.

---

## Prerequisites

### 1. Environment Requirements

```bash
# .NET SDK Version
dotnet --version
# Should be 10.0.0 or above

# Build project
cd /home/advantech/vincent/edge_subnode
dotnet build
```

### 2. Real DAQ Hardware Required

Unlike other Weda SubNode examples, `daq-data-collector` accesses hardware directly through the DAQNavi/SUSI native SDK (see `DaqCollector.cs`), not through a network protocol. The generic simulator at `tools/simulator-host/` (which simulates temperature/humidity sensors over TCP/MQTT/WebSocket) **cannot substitute for real iDAQ hardware** in this example — there is currently no way to exercise the DAQ acquisition and feature-extraction pipeline without a real, connected DAQ device.

---

## End-to-End Tests

### 1. Local Execution Test

**Step 1**: Prepare test environment

```bash
# Start a local NATS instance for the WedaNode connection
docker run -d --name nats-test -p 4222:4222 nats:latest
```

> Real DAQ hardware must be connected for this step — see "Real DAQ Hardware Required" above. There is no working simulator substitute for this example.

**Step 2**: Start application

```bash
cd examples/daq-data-collector
dotnet run
```

**Step 3**: Verify output

```bash
# Should see following logs
# [00:00:00] Starting Weda SubNode Application
# [00:00:01] Auto-generated DTDL for device...
# [00:00:02] Starting DAQ streaming: DaqDeviceNumber=..., SamplingRate=... Hz, FrameSize=..., FrameInterval=... s
# [00:00:02] Discovering DAQ modules...
# [00:00:02] Discovered 1 DAQ module(s).
# [00:00:02] Found target DAQ module: DeviceNumber=...
# [00:00:03] Configured DAQ module for streaming: SampleClockSource=BackplaneClock, SampleInterval=1 ms
# [00:00:03] Received report with 1000 samples for channel 0.
```

### 2. Docker Container Test

**Step 1**: Build test image

```bash
cd /home/advantech/vincent/edge_subnode

docker buildx build \
  --platform linux/amd64 \
  -f examples/daq-data-collector/Dockerfile \
  -t daq-data-collector:test \
  --load .
```

**Step 2**: Start container

```bash
cd examples/daq-data-collector

# Edit systemcfg.json to point WedaNode.Url at the local NATS instance
# "WedaNode": { "Url": "host.docker.internal:4222" }

docker compose up
```

**Step 3**: Verify running

```bash
docker compose logs -f

# Wait to see "Device initialized successfully"
```

### 3. Performance Considerations

There is no automated performance benchmark for this example. Use these targets as manual observation guidance:

| Metric | Target | How to check |
|--------|--------|--------------|
| Feature extraction latency | < 100 ms | Compare consecutive `[PHM Results]` log timestamps |
| Memory usage | < 512 MB | `docker stats daq-data-collector` (see 03_DOCKER_DEPLOY.md memory table for sizing by sampling rate) |
| CPU usage | < 50% | `docker stats daq-data-collector` |
| NATS message latency | < 50 ms | Compare the `device_time` sensor value to wall-clock receipt time on the subscriber |

---

## Test Execution

> These commands run the repo-wide test suite (`tests/Weda.SubNode.Core.Tests/`, etc.) — not daq-data-collector-specific tests, since none exist yet (see Testing Overview above).

### Run All Tests

```bash
cd /home/advantech/vincent/edge_subnode

# Run all tests
dotnet test

# Display detailed output
dotnet test --logger "console;verbosity=detailed"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### Run Specific Tests

```bash
# Run a single test class (replace with an actual class name from tests/)
dotnet test --filter "FullyQualifiedName~YourTestClassName"

# Run a specific test method
dotnet test --filter "Name=YourTestMethodName"

# Run a specific category
dotnet test --filter "Category=Integration"
```

### Generate Test Report

```bash
# Generate HTML coverage report
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover

# View report
open coverage/index.html  # macOS
xdg-open coverage/index.html  # Linux
start coverage/index.html  # Windows
```

---

## CI/CD Integration

This project's CI/CD runs on **Azure Pipelines**, not GitHub Actions — see `azure-pipelines.yml` and [03_DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) (Method A) for the actual build/push pipeline that produces the `daq-data-collector` image. There is no dedicated test stage in that pipeline yet, since there is no daq-data-collector test project to run (see Testing Overview above).

---

## Troubleshooting

### Common Test Failure Causes

| Cause | Symptom | Solution |
|-------|---------|----------|
| NATS/WedaNode not reachable | Connection errors at startup | Start a NATS container and verify `systemcfg.json`'s `WedaNode.Url` |
| DAQ hardware missing/not detected | `HardwareNotFoundException` or no modules found by `dndev` | Connect real DAQ hardware — there is no working simulator for this example (see Prerequisites above) |
| Configuration error | `InvalidOperationException` at startup | Check `systemcfg.json` and `devicecfg.json` (not `appsettings.json`, which only controls logging) |
| Timeout | `TimeoutException` | Increase test timeout |

### Debugging Tips

```bash
# Enable log debugging
dotnet test --logger "console;verbosity=diagnostic"

# Debug specific test
dotnet test --filter "Name=TestName" -- RunConfiguration.DebuggerEnabled=true

# View stack trace
dotnet test --logger "console;verbosity=detailed" -- RunConfiguration.LogConsoleOutput=true
```

