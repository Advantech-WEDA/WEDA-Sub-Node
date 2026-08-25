# DAQ Feature Proxy Testing Guide

This guide provides instructions for unit tests, integration tests, and end-to-end tests for the DAQ Feature Proxy.

---

## Testing Overview

### Test Classification

| Test Type | Scope | Purpose |
|-----------|-------|---------|
| Unit tests | Single class/method | Verify feature mapping, anomaly logic, API call behaviour |
| Integration tests | Multiple components | Verify capability query, NATS subscription, message processing pipeline |
| End-to-end tests | Complete system | Verify proxy initialisation, PHM API calls, telemetry output |

### Test Project Locations

- Unit tests: `tests/Weda.SubNode.Core.Tests/`
- Integration tests: `tests/Weda.SubNode.Integration.Tests/`

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

### 3. External Service Dependencies for Integration / E2E Tests

| Service | Purpose | Minimum Requirement |
|---------|---------|---------------------|
| NATS broker | Message transport | Running instance (use Docker: `docker run -d -p 4224:4224 nats:latest`) |
| `daq-collector` | Publishes PHM feature telemetry | Running and registered with WEDA Node |
| PHM Inference Service | Inference API | Running with at least one model in `ready` status |

---

## Unit Tests

### 1. Feature Convention Table Test

**Test Objective**: Verify `FeatureProxyDevice.BuildShortIdToApiMap` correctly maps sensor names to PHM Service keys and skips unknown sensors.

```bash
dotnet test --filter "Category=FeatureConventionTable"
```

**Sample Test Code**:

```csharp
[Fact]
public void BuildShortIdToApiMap_WithKnownSensors_MapsCorrectly()
{
    // Arrange — simulate shortId→name from a capability response
    var shortIdToName = new Dictionary<string, string>
    {
        ["4dbb8"] = "x_axis_rms_mg",
        ["bcf5e"] = "x_axis_peak_mg",
        ["55b0c"] = "x_axis_peak_to_peak_displacement"   // not in convention table
    };

    // Act
    var map = FeatureProxyDevice.BuildShortIdToApiMap(shortIdToName);

    // Assert
    map.Should().ContainKey("4dbb8").WhoseValue.Should().Be("X-Axis_RMSmg");
    map.Should().ContainKey("bcf5e").WhoseValue.Should().Be("X-Axis_Peakmg");
    map.Should().NotContainKey("55b0c");    // excluded — not in convention table
}
```

### 2. Anomaly Detection Logic Test

**Test Objective**: Verify `anomaly_result` values are derived correctly from `health_score` and `PhmAnomalyThreshold`.

```bash
dotnet test --filter "Category=AnomalyDetection"
```

**Sample Test Code**:

```csharp
[Theory]
[InlineData(-1.0, "unavailable")]       // API failure
[InlineData(0.0,  "anomaly_detected")]  // below threshold
[InlineData(0.29, "anomaly_detected")]  // just below threshold
[InlineData(0.3,  "normal")]            // at threshold (inclusive)
[InlineData(0.87, "normal")]            // healthy
[InlineData(1.0,  "normal")]            // perfect health
public void AnomalyResult_FromHealthScore_MatchesExpected(double healthScore, string expected)
{
    const double threshold = 0.3;

    var result = healthScore < 0
        ? "unavailable"
        : healthScore < threshold ? "anomaly_detected" : "normal";

    result.Should().Be(expected);
}
```

### 3. Capability Response Parsing Test

**Test Objective**: Verify `DeviceCapabilityClient` correctly parses the NATS capability response and extracts `deviceId` and `ShortIdToNameMap`.

```bash
dotnet test --filter "Category=CapabilityParsing"
```

**Sample Test Code**:

```csharp
[Fact]
public void ParseCapability_WithValidResponse_ExtractsDeviceIdAndSensors()
{
    // Arrange
    var json = """
    {
      "code": 0,
      "data": {
        "subNodes": [{
          "deviceId": "318281269764947968",
          "deviceName": "UniaxialVibrationDevice2",
          "capabilities": {
            "sensors": [
              { "name": "x_axis_rms_mg", "sensorGroup": "AI",
                "resourceId": "b73a9e41-9d5a-5f3b-96e9-65ce13c4dbb8" },
              { "name": "timestamp_timestamp", "sensorGroup": "SYS",
                "resourceId": "00000000-0000-0000-0000-000000000001" }
            ]
          }
        }]
      }
    }
    """;

    // Act
    var result = DeviceCapabilityClient.ParseCapability(json, "UniaxialVibrationDevice2");

    // Assert
    result.Should().NotBeNull();
    result!.DeviceId.Should().Be("318281269764947968");
    result.ShortIdToNameMap.Should().ContainKey("4dbb8").WhoseValue.Should().Be("x_axis_rms_mg");
    result.ShortIdToNameMap.Should().NotContainKey("00001");   // SYS sensors excluded
}
```

### 4. PHM API Request Format Test

**Test Objective**: Verify `ProxyAnalysisTransform` builds the correct inference request payload.

```bash
dotnet test --filter "Category=PhmApiRequest"
```

**Sample Test Code**:

```csharp
[Fact]
public async Task AnalyzeAsync_WithMappedFeatures_BuildsCorrectApiPayload()
{
    // Arrange
    string capturedPayload = null!;
    var httpHandler = new MockHttpMessageHandler(request =>
    {
        capturedPayload = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"healthScore":0.87}]}""")
        };
    });

    var shortIdToApi = new Dictionary<string, string>
    {
        ["4dbb8"] = "X-Axis_RMSmg",
        ["bcf5e"] = "X-Axis_Peakmg"
    };

    var transform = new ProxyAnalysisTransform(
        NullLogger<ProxyAnalysisTransform>.Instance,
        "http://localhost:8000/api/v1/models", "test-model-id",
        timeoutMs: 5000, threshold: 0.3,
        sensorResourceIds: new Dictionary<string, string>(),
        shortIdToApiMap: shortIdToApi,
        httpClient: new HttpClient(httpHandler));

    var inputFeatures = new Dictionary<string, double>
    {
        ["4dbb8"] = 42.5,
        ["bcf5e"] = 85.3
    };

    // Act
    await transform.AnalyzeAsync(inputFeatures, timestampMs: 1717504800000L, ct: default);

    // Assert
    using var doc = JsonDocument.Parse(capturedPayload);
    var features = doc.RootElement
        .GetProperty("data")[0]
        .GetProperty("features");

    features.GetProperty("X-Axis_RMSmg").GetDouble().Should().BeApproximately(42.5, 0.001);
    features.GetProperty("X-Axis_Peakmg").GetDouble().Should().BeApproximately(85.3, 0.001);
}
```

---

## Integration Tests

### 1. Capability Query Integration Test

**Test Objective**: Verify end-to-end capability query against a live NATS broker with `daq-collector` registered.

```bash
# Start NATS broker
docker run -d --name nats-test -p 4224:4224 nats:latest

# Start daq-data-collector (must be registered before running the test)

# Run tests
dotnet test --filter "Category=Integration&Category=CapabilityQuery"
```

**Test Coverage**:

- Connection to NATS broker using credentials from `systemcfg.json`
- Capability query returns the expected `deviceId` and sensor list
- Retry logic triggers when the first attempt times out

### 2. Feature Subscription Integration Test

**Test Objective**: Verify `FeatureSubscriptionHandler` receives and parses telemetry messages from the NATS broker.

```bash
dotnet test --filter "Category=Integration&Category=FeatureSubscription"
```

**Test Coverage**:

- Subscribe to `eco1j.weda.{deviceId}.telemetry`
- Parse incoming telemetry JSON into `Dictionary<string, double>` (keyed by `resourceShortId`)
- Fire-and-forget: subscription loop is not blocked by slow message processing
- Malformed messages are skipped without crashing the loop

### 3. PHM API Integration Test

**Test Objective**: Verify `ProxyAnalysisTransform` calls the PHM Inference Service and processes the response.

```bash
# Start PHM mock or real service on port 8000
dotnet test --filter "Category=Integration&Category=PhmApi"
```

**Test Coverage**:

- `POST /api/v1/models/{modelId}/infer` is called with the correct payload
- `health_score` is correctly extracted from `data[0].healthScore`
- Timeout (5000 ms) triggers fallback: `health_score = -1`, `anomaly_result = "unavailable"`
- HTTP 4xx and 5xx responses trigger fallback

---

## End-to-End Tests

### 1. Local Execution Test

**Step 1**: Start supporting services

```bash
# Start NATS broker
docker run -d --name nats-e2e -p 4224:4224 nats:latest

# Start daq-data-collector (full stack)
cd examples/daq-collector
docker compose up -d

# Start PHM Inference Service (or use a mock)
```

**Step 2**: Start the proxy

```bash
cd examples/daq-proxy
dotnet run
```

**Step 3**: Verify output

Expected startup logs:
```
[INF] NATS client initialized: nats://127.0.0.1:4224
[INF] Capability resolved: DeviceId=..., Topic=eco1j.weda.....telemetry, 7 feature sensors mapped
[INF] PHM Feature Proxy initialized: Subject=eco1j.weda.....telemetry, ModelId=..., Endpoint=http://...
```

Expected per-cycle logs (every ~1 second):
```
[INF] Feature message received, processing...
[INF] PHM API response: healthScore=0.87, anomaly_result=normal, processingTime=45ms
```

### 2. Docker Container Test

**Step 1**: Build test image

```bash
cd /home/advantech/vincent/edge_subnode

docker buildx build \
  --platform linux/amd64 \
  -f examples/daq-proxy/Dockerfile \
  -t daq-feature-proxy:test \
  --load .
```

**Step 2**: Edit `systemcfg.json` to point to the local NATS broker, then start:

```bash
cd examples/daq-proxy
docker compose up
```

**Step 3**: Verify

```bash
docker compose logs -f
# Wait to see "PHM Feature Proxy initialized"
```

### 3. Resilience Test

**Test Objective**: Verify the proxy continues operating correctly when the PHM Inference Service is unavailable.

**Procedure**:

1. Start the proxy with a running PHM Service — verify normal operation
2. Stop the PHM Service
3. Observe proxy logs — expect:
   ```
   [WRN] PHM API timed out after 5000ms for model ...
   ```
4. Verify output telemetry: `health_score=-1`, `anomaly_result="unavailable"`
5. Restart the PHM Service
6. Observe proxy logs — expect normal processing resumes automatically

### 4. Performance Test

**Test Objective**: Verify acceptable latency across different PHM API response times.

```bash
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"
```

**Performance Targets**:

| Metric | Target | Notes |
|--------|--------|-------|
| Message processing latency (excl. API) | < 5 ms | JSON parse + feature mapping |
| End-to-end latency (incl. API, normal) | < 500 ms | Typical PHM Service response |
| Timeout fallback latency | < `PhmApiTimeoutMs` + 10 ms | Must not exceed configured timeout |
| NATS subscription loop blocking | 0 ms | Processing is fire-and-forget |

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
dotnet test --filter "FullyQualifiedName~ProxyAnalysisTransformTests"

# Run specific test method
dotnet test --filter "Name=AnomalyResult_FromHealthScore_MatchesExpected"

# Run specific category
dotnet test --filter "Category=Integration"
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
| NATS not running | `NatsConnectionException` | Start NATS: `docker run -d -p 4224:4224 nats:latest` |
| `daq-collector` not registered | Capability query times out after 3 retries | Start and wait for DAQ to register |
| PHM Service not running | `HttpRequestException` in `ProxyAnalysisTransform` | Start PHM Service or use mock HTTP handler in unit tests |
| Wrong `DaqDataCollectorSubNodeName` | `DeviceId` not found in capability response | Check exact `SubNode.Name` in DAQ's `devicecfg.json` |
| Model not in `ready` status | PHM Service returns HTTP 422 | Use `GET /api/v1/models/` to find a trained model |

### Debugging Tips

```bash
# Enable verbose logging
dotnet test --logger "console;verbosity=diagnostic"

# Debug specific test
dotnet test --filter "Name=TestName" -- RunConfiguration.DebuggerEnabled=true

# View stack trace
dotnet test --logger "console;verbosity=detailed" -- RunConfiguration.LogConsoleOutput=true
```
