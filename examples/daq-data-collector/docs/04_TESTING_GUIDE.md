# DAQ Data Collector Testing Guide

This guide provides instructions for unit tests, integration tests, and end-to-end tests for DAQ Data Collector.

---

## Testing Overview

### Test Classification

| Test Type | Scope | Purpose |
|-----------|-------|---------|
| Unit tests | Single class/method | Verify basic functionality |
| Integration tests | Multiple components | Verify component interactions |
| End-to-end tests | Complete system | Verify overall workflow |

### Test Project Locations

- Unit tests: `tests/Weda.SubNode.Core.Tests/`
- Integration tests: `tests/Weda.SubNode.Integration.Tests/`
- Device tests: `tests/SystemAgentDevice.Tests/` (for reference)

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

### 2. Test Frameworks

- **xUnit**: Unit testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Assertion library

### 3. DAQ Hardware Simulation

For test environments without actual hardware, use simulator:

```bash
cd examples/simulator-host
dotnet run
```

---

## Unit Tests

### 1. DAQ Communication Layer Test

**Test Objective**: Verify DAQ communication correctness

```bash
cd tests
dotnet test --filter "Category=DaqCommunication"
```

**Test Examples**:

- Verify sampling rate settings
- Verify frame interval calculation
- Verify decimation factor application

### 2. Feature Extraction Test

**Test Objective**: Verify feature calculation correctness

```bash
dotnet test --filter "Category=FeatureExtraction"
```

**Test Coverage**:

- RMS calculation
- Peak detection
- Frequency analysis (FFT)
- Energy calculation

**Sample Test Code**:

```csharp
[Fact]
public void CalculateRMS_WithValidSignal_ReturnsCorrectValue()
{
    // Arrange
    var signal = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
    var extractor = new FeatureExtractor();
    
    // Act
    var rms = extractor.CalculateRMS(signal);
    
    // Assert
    rms.Should().BeApproximately(3.3166, 0.0001);
}
```

### 3. Configuration Validation Test

**Test Objective**: Verify configuration parsing and validation

```bash
dotnet test --filter "Category=Configuration"
```

**Test Coverage**:

- Valid configuration loading
- Invalid configuration rejection
- Boundary value checking
- Default value application

---

## Integration Tests

### 1. Complete Data Collection Pipeline Test

**Test Objective**: Verify end-to-end process from hardware to feature extraction

```bash
dotnet test --filter "Category=Integration&Category=DaqPipeline"
```

**Test Process**:

1. Build `DeviceConfiguration` with DAQ properties
2. Verify configuration values are parsed correctly
3. Construct extractor classes with derived parameters
4. Feed synthetic frame, verify feature output

**Sample Test**:

```csharp
[Fact]
public void DaqConfiguration_WithValidProperties_ParsesParametersCorrectly()
{
    // Arrange
    var config = new DeviceConfigurationBuilder()
        .WithProperties(new Dictionary<string, object>
        {
            ["AcquisitionRateHz"] = 1000,
            ["ObservationWindowSeconds"] = 1.0
        })
        .Build();

    // Act — derive the same values UniaxialVibrationDevice reads at startup
    var acquisitionRate    = Convert.ToInt32(config.Properties["AcquisitionRateHz"]);
    var observationWindow  = Convert.ToDouble(config.Properties["ObservationWindowSeconds"]);
    var frameSize          = (int)(acquisitionRate * observationWindow);

    // Assert
    acquisitionRate.Should().Be(1000);
    observationWindow.Should().Be(1.0);
    frameSize.Should().Be(1000);
}
```

### 2. NATS Communication Integration Test

**Test Objective**: Verify communication with NATS

```bash
# Start test NATS server
docker run -d -p 4222:4222 nats:latest

# Run tests
dotnet test --filter "Category=Integration&Category=Nats"
```

**Test Coverage**:

- Connection and disconnection
- Telemetry transmission
- Configuration reception
- Error handling

---

## End-to-End Tests

### 1. Local Execution Test

**Step 1**: Prepare test environment

```bash
# Start NATS
docker run -d --name nats-test -p 4222:4222 nats:latest

# Start DAQ simulator
cd examples/simulator-host
dotnet run &
```

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

# Edit appsettings.json to point to local NATS
# "Nats": { "Url": "nats://host.docker.internal:4222" }

docker compose up
```

**Step 3**: Verify running

```bash
docker compose logs -f

# Wait to see "Device initialized successfully"
```

### 3. Performance Test

**Test Objective**: Verify performance with different parameters

```bash
# Run performance benchmark tests
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"
```

**Performance Metrics**:

| Metric | Target | Unit |
|--------|--------|------|
| Feature extraction latency | < 100 | ms |
| Memory usage | < 512 | MB |
| CPU usage | < 50 | % |
| NATS message latency | < 50 | ms |

**Sample Performance Test**:

```csharp
[Fact]
public void FrequencyDomainExtraction_PerformanceWithHighAcquisitionRate()
{
    // Arrange — AcquisitionRateHz=10000, ObservationWindowSeconds=1.0 → FrameSize=10000
    int fftSize = 10000;
    var extractor = new FrequencyDomainExtractor(fftSize, NullLogger<FrequencyDomainExtractor>.Instance);
    var frame = new DaqRawFrame
    {
        Samples     = new float[fftSize],
        SamplingRate = 10000,
        AxisName    = "X"
    };
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Act
    var result = extractor.Extract(frame);
    sw.Stop();

    // Assert
    sw.ElapsedMilliseconds.Should().BeLessThan(100);
    result.RMSmg.Should().BeGreaterThanOrEqualTo(0);
}
```

---

## Test Execution

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
# Run single test class
dotnet test --filter "FullyQualifiedName~DaqCommunicationTests"

# Run specific test method
dotnet test --filter "Name=CalculateRMS_WithValidSignal_ReturnsCorrectValue"

# Run specific category
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

## Common Test Scenarios

### Scenario 1: Verify Acquisition Rate Settings

```csharp
[Theory]
[InlineData(1000)]
[InlineData(2500)]
[InlineData(5000)]
[InlineData(10000)]
public void DeviceConfig_WithVariousAcquisitionRates_PropertyParsedCorrectly(int rate)
{
    // Arrange
    var config = new DeviceConfigurationBuilder()
        .WithProperties(new Dictionary<string, object> { ["AcquisitionRateHz"] = rate })
        .Build();

    // Act
    var parsed = Convert.ToInt32(config.Properties["AcquisitionRateHz"]);

    // Assert
    parsed.Should().Be(rate);
}
```

### Scenario 2: Verify Feature Calculation Accuracy

```csharp
[Fact]
public void RMSCalculation_WithKnownSignal_MatchesExpectedValue()
{
    // Sine signal: RMS = Amplitude / √2
    var amplitude = 10.0;
    var signal = Enumerable.Range(0, 100)
        .Select(i => amplitude * Math.Sin(2 * Math.PI * i / 100))
        .ToArray();
    
    var rms = FeatureExtractor.CalculateRMS(signal);
    var expected = amplitude / Math.Sqrt(2);
    
    rms.Should().BeApproximately(expected, 0.1);
}
```

### Scenario 3: Verify Error Handling

```csharp
[Fact]
public void TimeDomainExtractor_WithEmptyFrame_ThrowsArgumentException()
{
    // Arrange
    var extractor = new TimeDomainExtractor(1000, NullLogger<TimeDomainExtractor>.Instance);
    var emptyFrame = new DaqRawFrame { Samples = [], SamplingRate = 1000 };

    // Act
    var act = () => extractor.Extract(emptyFrame);

    // Assert
    act.Should().Throw<ArgumentException>()
       .WithMessage("*at least one sample*");
}
```

---

## CI/CD Integration

### GitHub Actions Test Workflow

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run tests
      run: dotnet test --no-build --verbosity normal
    
    - name: Generate coverage report
      run: dotnet test /p:CollectCoverage=true
    
    - name: Upload coverage
      uses: codecov/codecov-action@v4
```

---

## Troubleshooting

### Common Test Failure Causes

| Cause | Symptom | Solution |
|-------|---------|----------|
| NATS not running | `NatsConnectionException` | Start NATS container |
| DAQ hardware missing | `HardwareNotFoundException` | Start simulator or use Mock |
| Configuration error | `ConfigurationException` | Check appsettings.json |
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

