# How to Use Hooks to Write Handlers

Hooks allow you to intercept and extend device functionality at key points in the data processing pipeline. This guide shows you how to use hooks to add custom behavior like logging, persistence, and data validation.

## Overview

Hooks in Weda SubNode SDK provide extension points for:
- **Lifecycle hooks**: Device initialization, startup, shutdown
- **Data pipeline hooks**: Before/after data processing
- **Telemetry hooks**: Before sending to cloud
- **Event hooks**: Custom event handling

## Lifecycle Hooks

### ILifecycleHooks Interface

```csharp
using Weda.SubNode.Core.Devices.Lifecycle;

public interface ILifecycleHooks
{
    Task OnInitializingAsync(DeviceConfiguration config);
    Task OnInitializedAsync();
    Task OnStartingAsync();
    Task OnStartedAsync();
    Task OnStoppingAsync();
    Task OnStoppedAsync();
    Task OnErrorAsync(Exception exception);
}
```

### Example: Logging Hook

```csharp
public class LoggingLifecycleHook : ILifecycleHooks
{
    private readonly ILogger _logger;

    public LoggingLifecycleHook(ILogger logger)
    {
        _logger = logger;
    }

    public async Task OnInitializingAsync(DeviceConfiguration config)
    {
        _logger.LogInformation("[TOOL] Initializing device: {DeviceId}", config.Id);
        await Task.CompletedTask;
    }

    public async Task OnStartedAsync()
    {
        _logger.LogInformation("*> Device started successfully");
        await Task.CompletedTask;
    }

    public async Task OnErrorAsync(Exception exception)
    {
        _logger.LogError(exception, "X Device error occurred");
        // Could send alert, trigger recovery, etc.
        await Task.CompletedTask;
    }

    // Implement other methods...
}
```

### Registering Lifecycle Hooks

```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Register lifecycle hooks
        _lifecycleManager.RegisterHook(new LoggingLifecycleHook(_logger));
        _lifecycleManager.RegisterHook(new DatabaseLifecycleHook(_dbContext));
    }
}
```

## Data Pipeline Hooks

### Example: Database Persistence Hook

Save telemetry data to database before transformation:

```csharp
using Weda.SubNode.Abstractions.Events;
using System.Data;

public class DatabasePersistenceHook
{
    private readonly IDbConnection _dbConnection;
    private readonly ILogger _logger;

    public DatabasePersistenceHook(IDbConnection dbConnection, ILogger logger)
    {
        _dbConnection = dbConnection;
        _logger = logger;
    }

    public async Task OnDataReceivedAsync(DataReceivedEvent e)
    {
        try
        {
            foreach (var measure in e.Data)
            {
                await SaveToDatabase(measure);
            }

            _logger.LogDebug("Saved {Count} measurements to database", e.Data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save data to database");
        }
    }

    private async Task SaveToDatabase(TelemetryData data)
    {
        var sql = @"
            INSERT INTO telemetry_raw (
                device_id, resource_id, timestamp, value, unit
            ) VALUES (
                @DeviceId, @ResourceId, @Timestamp, @Value, @Unit
            )";

        using var command = _dbConnection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@ResourceId", data.ResourceId));
        command.Parameters.Add(new SqlParameter("@Timestamp", data.Timestamp));
        command.Parameters.Add(new SqlParameter("@Value", data.ValueObject));
        command.Parameters.Add(new SqlParameter("@Unit", data.Unit ?? ""));

        await command.ExecuteNonQueryAsync();
    }
}
```

### Registering Data Hooks

```csharp
public class MyDevice : TcpModbusDevice
{
    private readonly DatabasePersistenceHook _dbHook;

    public MyDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IDbConnection dbConnection)
        : base(context, configuration)
    {
        _dbHook = new DatabasePersistenceHook(dbConnection, _logger);

        // Subscribe to DataReceived event
        this.DataReceived += async (sender, e) =>
        {
            await _dbHook.OnDataReceivedAsync(e);
        };
    }
}
```

## Telemetry Pipeline Hooks

### Example: Pre-Transformation Hook

Validate and enrich data before transformation:

```csharp
public class DataValidationHook
{
    private readonly ILogger _logger;

    public async Task OnBeforeTransformAsync(TelemetryData data)
    {
        // Validate data range
        if (data.ResourceId == "temperature")
        {
            var value = (double)data.ValueObject;

            if (value < -50 || value > 150)
            {
                _logger.LogWarning(
                    "Temperature out of range: {Value}°C for {ResourceId}",
                    value, data.ResourceId);

                // Could clamp to valid range
                data.ValueObject = Math.Clamp(value, -50, 150);
            }
        }

        // Enrich with metadata
        data.Metadata = new Dictionary<string, object>
        {
            ["ValidationTime"] = DateTime.UtcNow,
            ["Source"] = "ValidationHook"
        };

        await Task.CompletedTask;
    }
}
```

### Example: Post-Transformation Hook

Log or alert after transformation:

```csharp
public class AlertingHook
{
    private readonly IAlertService _alertService;
    private readonly ILogger _logger;

    public async Task OnAfterTransformAsync(TelemetryData before, TelemetryData after)
    {
        // Check if transformation significantly changed value
        var beforeValue = (double)before.ValueObject;
        var afterValue = (double)after.ValueObject;
        var percentChange = Math.Abs((afterValue - beforeValue) / beforeValue) * 100;

        if (percentChange > 20)
        {
            _logger.LogWarning(
                "Large transformation change detected: {Before} -> {After} ({Percent}%)",
                beforeValue, afterValue, percentChange);

            await _alertService.SendAlertAsync(
                $"Unusual transformation: {before.ResourceId}",
                $"Value changed by {percentChange:F1}%");
        }
    }
}
```

### Registering Pipeline Hooks

```csharp
_telemetryPipeline.BeforeTransform += async (sender, e) =>
{
    await _validationHook.OnBeforeTransformAsync(e.Data);
};

_telemetryPipeline.AfterTransform += async (sender, e) =>
{
    await _alertingHook.OnAfterTransformAsync(e.BeforeData, e.AfterData);
};
```

## Advanced Hook Patterns

### Pattern 1: Audit Trail Hook

Track all data changes for compliance:

```csharp
public class AuditTrailHook
{
    private readonly IAuditRepository _auditRepo;

    public async Task OnDataChangedAsync(TelemetryData original, TelemetryData modified)
    {
        var auditEntry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            ResourceId = original.ResourceId,
            OriginalValue = original.ValueObject,
            ModifiedValue = modified.ValueObject,
            ChangeType = "Transformation",
            Actor = "System"
        };

        await _auditRepo.SaveAsync(auditEntry);
    }
}
```

### Pattern 2: Circuit Breaker Hook

Prevent cascading failures:

```csharp
public class CircuitBreakerHook
{
    private int _failureCount = 0;
    private const int FailureThreshold = 5;
    private bool _circuitOpen = false;

    public async Task<bool> OnBeforeOperationAsync()
    {
        if (_circuitOpen)
        {
            _logger.LogWarning("Circuit breaker is open, skipping operation");
            return false;
        }

        return true;
    }

    public async Task OnOperationSuccessAsync()
    {
        _failureCount = 0;
        _circuitOpen = false;
        await Task.CompletedTask;
    }

    public async Task OnOperationFailureAsync(Exception ex)
    {
        _failureCount++;

        if (_failureCount >= FailureThreshold)
        {
            _logger.LogError("Circuit breaker triggered after {Count} failures", _failureCount);
            _circuitOpen = true;

            // Schedule circuit reset
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                _logger.LogInformation("Circuit breaker reset attempt");
                _circuitOpen = false;
                _failureCount = 0;
            });
        }

        await Task.CompletedTask;
    }
}
```

### Pattern 3: Caching Hook

Cache processed data for performance:

```csharp
public class CachingHook
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<TelemetryData?> OnBeforeProcessAsync(string resourceId)
    {
        // Check cache first
        if (_cache.TryGetValue(resourceId, out TelemetryData? cached))
        {
            _logger.LogDebug("Cache hit for {ResourceId}", resourceId);
            return cached;
        }

        return null;
    }

    public async Task OnAfterProcessAsync(TelemetryData data)
    {
        // Store in cache
        _cache.Set(data.ResourceId, data, _cacheDuration);
        await Task.CompletedTask;
    }
}
```

### Pattern 4: Retry Hook

Implement retry logic with exponential backoff:

```csharp
public class RetryHook
{
    private const int MaxRetries = 3;

    public async Task<T> WithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxRetries} failed for {Operation}. Retrying in {Delay}s",
                    attempt, MaxRetries, operationName, delay.TotalSeconds);

                await Task.Delay(delay);
            }
        }

        // Final attempt without catching
        return await operation();
    }
}

// Usage
var result = await _retryHook.WithRetryAsync(
    async () => await _cloudService.SendTelemetryAsync(data),
    "SendTelemetry"
);
```

## Complete Example: Multi-Stage Pipeline with Hooks

```csharp
public class EnhancedDevice : TcpModbusDevice
{
    private readonly DatabasePersistenceHook _dbHook;
    private readonly DataValidationHook _validationHook;
    private readonly AlertingHook _alertingHook;
    private readonly AuditTrailHook _auditHook;
    private readonly CircuitBreakerHook _circuitBreaker;

    public EnhancedDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IServiceProvider services)
        : base(context, configuration)
    {
        // Initialize hooks
        _dbHook = services.GetRequiredService<DatabasePersistenceHook>();
        _validationHook = services.GetRequiredService<DataValidationHook>();
        _alertingHook = services.GetRequiredService<AlertingHook>();
        _auditHook = services.GetRequiredService<AuditTrailHook>();
        _circuitBreaker = services.GetRequiredService<CircuitBreakerHook>();

        ConfigureHooks();
    }

    private void ConfigureHooks()
    {
        // 1. Data received -> Save raw data to DB
        this.DataReceived += async (sender, e) =>
        {
            if (await _circuitBreaker.OnBeforeOperationAsync())
            {
                try
                {
                    await _dbHook.OnDataReceivedAsync(e);
                    await _circuitBreaker.OnOperationSuccessAsync();
                }
                catch (Exception ex)
                {
                    await _circuitBreaker.OnOperationFailureAsync(ex);
                }
            }
        };

        // 2. Before transformation -> Validate and enrich
        _telemetryPipeline.BeforeTransform += async (sender, e) =>
        {
            var original = e.Data.Clone();
            await _validationHook.OnBeforeTransformAsync(e.Data);

            if (!original.Equals(e.Data))
            {
                await _auditHook.OnDataChangedAsync(original, e.Data);
            }
        };

        // 3. After transformation -> Check and alert
        _telemetryPipeline.AfterTransform += async (sender, e) =>
        {
            await _alertingHook.OnAfterTransformAsync(e.BeforeData, e.AfterData);
            await _auditHook.OnDataChangedAsync(e.BeforeData, e.AfterData);
        };

        // 4. Before sending to cloud -> Final validation
        this.TelemetrySending += async (sender, e) =>
        {
            _logger.LogInformation("Sending {Count} measurements to cloud", e.Data.Count);
        };

        // 5. After sending -> Log result
        this.TelemetrySent += async (sender, e) =>
        {
            if (e.Success)
            {
                _logger.LogInformation("*> Telemetry sent successfully");
            }
            else
            {
                _logger.LogError("X Failed to send telemetry: {Error}", e.Error);
            }
        };
    }
}
```

## Best Practices

### 1. Keep Hooks Lightweight

```csharp
// *> Good: Async and fast
public async Task OnDataReceivedAsync(DataReceivedEvent e)
{
    _ = Task.Run(async () => await SaveToDatabase(e)); // Fire and forget
    await Task.CompletedTask;
}

// X Bad: Blocks the pipeline
public async Task OnDataReceivedAsync(DataReceivedEvent e)
{
    await ExpensiveDatabaseOperation(e); // Blocks
}
```

### 2. Handle Errors Gracefully

```csharp
public async Task OnDataReceivedAsync(DataReceivedEvent e)
{
    try
    {
        await ProcessData(e);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Hook failed, but continuing pipeline");
        // Don't rethrow - let pipeline continue
    }
}
```

### 3. Use Dependency Injection

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DatabasePersistenceHook>();
        services.AddSingleton<DataValidationHook>();
        services.AddSingleton<AlertingHook>();
        // ...
    }
}
```

### 4. Test Hooks Independently

```csharp
[Fact]
public async Task DatabaseHook_Should_Save_Data()
{
    // Arrange
    var mockDb = new Mock<IDbConnection>();
    var hook = new DatabasePersistenceHook(mockDb.Object, _logger);
    var testEvent = new DataReceivedEvent { /* ... */ };

    // Act
    await hook.OnDataReceivedAsync(testEvent);

    // Assert
    mockDb.Verify(db => db.ExecuteAsync(It.IsAny<string>()), Times.Once);
}
```

## Performance Considerations

- Use async/await for I/O operations
- Consider using `Task.Run` for CPU-bound work
- Implement timeouts for external service calls
- Use circuit breakers to prevent cascading failures
- Cache frequently accessed data

## Next Steps

- [Device Lifecycle](../device/device_lifecycle.md) - Understand device states
- [Architecture Overview](../architecture/overview.md) - See how hooks fit in
- [Testing Guide](02_sandbox_testing.md) - Test your hooks

## Summary

Hooks in Weda SubNode SDK provide powerful extension points:
- **Lifecycle hooks** for device state transitions
- **Pipeline hooks** for data processing stages
- **Event-based** architecture for loose coupling
- **Composable** - chain multiple hooks together
- **Testable** - isolate and test independently

Key patterns demonstrated:
- Database persistence before transformation
- Data validation and enrichment
- Alerting on anomalies
- Audit trail for compliance
- Circuit breaker for resilience
- Caching for performance

Remember to keep hooks lightweight and handle errors gracefully to maintain pipeline performance.
