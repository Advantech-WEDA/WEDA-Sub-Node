# Weda SubNode Testing Infrastructure

This directory contains the testing infrastructure for the Weda SubNode project.

## Project Structure

```
tests/
├── Weda.SubNode.TestBase/            # Shared test utilities and builders
│   ├── Builders/                       # Test Data Builders
│   │   └── DeviceConfigurationBuilder.cs
│   └── Fixtures/                       # Test fixtures (future)
├── Weda.SubNode.Core.Tests/          # Unit tests for Core layer
│   └── InfrastructureTests.cs          # Smoke tests for test infrastructure
├── Weda.SubNode.Abstractions.Tests/  # Unit tests for Abstractions (future)
├── Weda.SubNode.Devices.Tests/       # Unit tests for Devices (future)
├── Directory.Build.props               # Shared test project configuration
├── coverlet.runsettings                # Code coverage configuration
└── README.md                           # This file
```

## Testing Stack

- **xUnit 2.9.2** - Test framework
- **NSubstitute 5.3.0** - Mocking framework
- **Shouldly 4.2.1** - Assertion library
- **Coverlet** - Code coverage tool
- **Testcontainers 3.10.0** - Integration testing with Docker (future)

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run tests in a specific project
```bash
dotnet test tests/Weda.SubNode.Core.Tests
```

### Run tests with code coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults --settings tests/coverlet.runsettings
```

### Run tests with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Code Coverage

### Coverage Configuration

Coverage settings are defined in `coverlet.runsettings`:
- **Format**: Cobertura XML + JSON
- **Excluded**: Test projects, Program.cs, Startup.cs
- **Excluded Attributes**: Obsolete, GeneratedCode, CompilerGenerated
- **Skip Auto-properties**: Yes

### Current Coverage Targets

- **Line Coverage**: 60% (initial target)
- **Branch Coverage**: 50% (initial target)

These targets will be increased as the test suite matures:
- Phase 2: 70% line, 60% branch
- Phase 3: 80% line, 70% branch

### Viewing Coverage Reports

After running tests with coverage, view the report:
```bash
# Find the coverage file
find TestResults -name "coverage.cobertura.xml"

# Install reportgenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html

# Open the report
open TestResults/CoverageReport/index.html
```

## Test Data Builders

The project uses the **Test Data Builder** pattern to create test objects:

```csharp
// Example: Create a test DeviceConfiguration
var config = DeviceConfigurationBuilder.Default()
    .WithDeviceId("test-device-001")
    .WithDeviceName("Test Device")
    .AddSensor(new Sensor { ResourceId = "sensor-001", Name = "temperature" })
    .Build();
```

## Writing Tests

### Test Class Structure
```csharp
using Xunit;
using Shouldly;
using NSubstitute;
using Weda.SubNode.TestBase.Builders;

namespace Weda.SubNode.Core.Tests;

public class MyFeatureTests
{
    [Fact]
    public void MyMethod_WhenCondition_ShouldBehavior()
    {
        // Arrange
        var config = DeviceConfigurationBuilder.Default().Build();
        var mockService = Substitute.For<IMyService>();
        mockService.SomeMethod().Returns(expectedValue);

        // Act
        var result = MyMethod(config, mockService);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    public void MyCalculation_WithInputs_ShouldReturnExpected(int a, int b, int expected)
    {
        // Arrange & Act
        var result = Calculator.Add(a, b);

        // Assert
        result.ShouldBe(expected);
    }
}
```

### Test Naming Convention

Follow the pattern: `MethodName_Scenario_ExpectedBehavior`

**Good Examples:**
- `Initialize_WhenValidConfiguration_ShouldReturnTrue`
- `Connect_WhenNetworkError_ShouldRetryThreeTimes`
- `ProcessData_WithEmptyList_ShouldThrowArgumentException`

**Bad Examples:**
- `TestInitialize` (too vague)
- `Test1`, `Test2` (meaningless)
- `Initialize_Test` (doesn't describe scenario)

## CI/CD Integration

Tests are automatically run in Azure DevOps Pipeline:

- **Trigger**: Every PR and commit to main/develop
- **Environment**: Ubuntu Latest, .NET 9.0
- **Coverage Check**: Pipeline fails if coverage < 60%
- **Test Results**: Published as build artifacts

See `azure-pipelines.yml` for full configuration.

## Future Enhancements

- [ ] Integration tests with Testcontainers (NATS, PostgreSQL)
- [ ] Performance tests for high-throughput scenarios
- [ ] Contract tests for Modbus protocol
- [ ] Mutation testing with Stryker.NET
- [ ] Snapshot testing for configuration validation

## Testing Library Usage

### NSubstitute - Mocking Framework

**Creating Mocks:**
```csharp
var mockService = Substitute.For<IMyService>();
```

**Setup Return Values:**
```csharp
mockService.GetValue().Returns(42);
mockService.GetAsync(Arg.Any<string>()).Returns(Task.FromResult("result"));
```

**Verify Method Calls:**
```csharp
await mockService.Received(1).ProcessAsync(Arg.Any<string>());
mockService.DidNotReceive().DeleteAll();
```

**Argument Matching:**
```csharp
mockService.Process(Arg.Any<int>());              // Any value
mockService.Process(Arg.Is<int>(x => x > 5));     // Conditional
mockService.Process(42);                           // Exact match
```

### Shouldly - Assertion Library

**Basic Assertions:**
```csharp
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);
result.ShouldBeNull();
result.ShouldNotBeNull();
```

**Numeric Assertions:**
```csharp
value.ShouldBeGreaterThan(10);
value.ShouldBeLessThan(100);
value.ShouldBeInRange(10, 100);
```

**Collection Assertions:**
```csharp
list.ShouldNotBeEmpty();
list.ShouldContain(item);
list.ShouldAllBe(x => x > 0);
```

**String Assertions:**
```csharp
text.ShouldStartWith("prefix");
text.ShouldEndWith("suffix");
text.ShouldContain("substring");
text.ShouldMatch(@"\d{3}-\d{4}");
```

**Exception Assertions:**
```csharp
Should.Throw<ArgumentException>(() => method());
await Should.ThrowAsync<InvalidOperationException>(() => methodAsync());
```

## Troubleshooting

### Tests fail with "Type not found"
Ensure you have added `using Xunit;` at the top of your test file.

### NSubstitute returns null instead of mocked value
Check that you're using `Returns()` correctly and the method signature matches exactly.

### Coverage not collected
Verify `coverlet.runsettings` is in the correct location and the path is correct in the test command.

### Tests pass locally but fail in CI
Check for hardcoded paths, environment variables, or timezone-dependent code.

### Shouldly assertion message unclear
Shouldly provides detailed error messages by default. Ensure you're using the correct assertion method for your scenario.

## References

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [Shouldly Documentation](https://docs.shouldly.org/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Test Data Builder Pattern](https://www.industriallogic.com/xp/refactoring/testDataBuilder.html)
