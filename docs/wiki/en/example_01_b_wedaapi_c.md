# Quick Start: Advanced API Mode (wedaapi-c)

**Template**: `dotnet new wedaapi-c`
**Time to complete**: 10 minutes
**Difficulty**: ** Intermediate

## What You'll Build

An advanced Modbus device application with:
- *> Full control over application builder
- *> Custom service registration
- *> Custom logging configuration
- *> Dependency injection control
- *> Enterprise-ready architecture

**Perfect for**: Enterprise applications, custom services, complex integrations

---

## Step 1: Create Project

Create a new project using the `wedaapi-c` template:

```bash
# Create project
dotnet new wedaapi-c -n MyAdvancedDevice
cd MyAdvancedDevice

# Project structure
MyAdvancedDevice/
├── Program.cs               # Full builder pattern code
├── appsettings.json         # Device configuration
├── appsettings.Development.json  # Dev environment overrides
└── MyAdvancedDevice.csproj
```

---

## Step 2: Review the Code

Open `Program.cs` - notice the full builder pattern:

```csharp
using Weda.SubNode.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create builder with full control
var builder = WedaApplication.CreateBuilder(args);

// Configure services
builder.Services.AddSingleton<IMyCustomService, MyCustomService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add custom logging filters
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Weda.SubNode", LogLevel.Debug);

// Build and run
var app = builder.Build();
await app.RunAsync();
```

**Key difference from `wedaapi`**: You manually configure services and logging instead of using defaults.

---

## Step 3: Configure Your Device

Edit `appsettings.json` (similar to `wedaapi` template):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Weda.SubNode": "Debug"
    }
  },

  "DeviceConfigs": {
    "MyDevice": {
      "Id": "my-device-001",
      "Name": "My Advanced Device",
      "DeviceType": "ModbusTCP",
      "ConnectionSettings": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1,
        "ConnectionTimeout": 5000,
        "RetryAttempts": 3
      },
      "PollingInterval": 1000,
      "Sensors": [
        {
          "ResourceId": "temp",
          "Name": "Temperature",
          "RegisterAddress": 0,
          "RegisterType": "HoldingRegister",
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  },

  "Nats": {
    "Url": "nats://localhost:4222",
    "Enabled": true,
    "MaxReconnectAttempts": 10,
    "ReconnectDelay": 1000
  }
}
```

---

## Step 4: Add Custom Services

One of the key advantages of `wedaapi-c` is the ability to register custom services.

### Create a Custom Service

Create `Services/DataProcessingService.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace MyAdvancedDevice.Services;

public interface IDataProcessingService
{
    Task ProcessDataAsync(double value, CancellationToken ct = default);
}

public class DataProcessingService : IDataProcessingService
{
    private readonly ILogger<DataProcessingService> _logger;
    private readonly HttpClient _httpClient;

    public DataProcessingService(
        ILogger<DataProcessingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task ProcessDataAsync(double value, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing value: {Value}", value);

        // Custom business logic
        if (value > 80.0)
        {
            _logger.LogWarning("High value detected: {Value}", value);

            // Send alert to external API
            await SendAlertAsync(value, ct);
        }
    }

    private async Task SendAlertAsync(double value, CancellationToken ct)
    {
        // Implementation...
        _logger.LogInformation("Alert sent for value: {Value}", value);
    }
}
```

### Register the Service

Update `Program.cs`:

```csharp
using MyAdvancedDevice.Services;

var builder = WedaApplication.CreateBuilder(args);

// Register HttpClient
builder.Services.AddHttpClient();

// Register custom service
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var app = builder.Build();
await app.RunAsync();
```

---

## Step 5: Custom Logging Configuration

The `wedaapi-c` template gives you full control over logging:

### Example: Add Serilog

```bash
dotnet add package Serilog.Extensions.Hosting
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

Update `Program.cs`:

```csharp
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/mydevice-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WedaApplication.CreateBuilder(args);

    // Use Serilog
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    // Register services
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();

    var app = builder.Build();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## Step 6: Environment-Specific Configuration

Use `appsettings.Development.json` for development overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Weda.SubNode": "Trace"
    }
  },

  "DeviceConfigs": {
    "MyDevice": {
      "ConnectionSettings": {
        "Host": "127.0.0.1",  // Local simulator
        "Port": 502
      },
      "Sandbox": {
        "Enabled": true
      }
    }
  },

  "Nats": {
    "Enabled": false  // Disable cloud in development
  }
}
```

Run with development settings:

```bash
dotnet run --environment Development
```

---

## Step 7: Add Database Persistence

### Install Entity Framework Core

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

### Create DbContext

Create `Data/ApplicationDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace MyAdvancedDevice.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
}

public class TelemetryRecord
{
    public int Id { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Register DbContext

Update `Program.cs`:

```csharp
using MyAdvancedDevice.Data;
using Microsoft.EntityFrameworkCore;

var builder = WedaApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

await app.RunAsync();
```

Add to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=telemetry.db"
  }
}
```

---

## Step 8: Add Health Checks

### Install Health Checks

```bash
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
dotnet add package AspNetCore.HealthChecks.NpgSql
```

### Configure Health Checks

Update `Program.cs`:

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck("nats", () =>
    {
        // Custom NATS health check logic
        return HealthCheckResult.Healthy("NATS connection is healthy");
    });

var app = builder.Build();
await app.RunAsync();
```

---

## Step 9: Run Your Application

### Build and Run

```bash
dotnet build
dotnet run
```

### Expected Output

```
[12:34:56 INF] Starting Weda SubNode application...
[12:34:56 INF] DbContext initialized: telemetry.db
[12:34:56 INF] Health checks registered: 2
[12:34:56 INF] Custom service registered: DataProcessingService
[12:34:56 INF] NATS URL configured: nats://localhost:4222
[12:34:56 INF] Initializing device: MyDevice (my-device-001)
[12:34:56 INF] Device started successfully. Press Ctrl+C to stop...
[12:34:57 DBG] Connected to Modbus device at 192.168.1.100:502
[12:34:57 INF] [DATA] Temperature: 25.5°C
[12:34:57 INF] Processing value: 25.5
```

---

## Advanced Scenarios

### Scenario 1: Add Custom Middleware

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Add middleware (if using ASP.NET Core hosting)
builder.Services.AddSingleton<IStartupFilter, CustomStartupFilter>();

var app = builder.Build();
await app.RunAsync();
```

### Scenario 2: Background Services

```csharp
public class DataAggregationService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Aggregate telemetry data every minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<DataAggregationService>();
```

### Scenario 3: Multiple Device Types

```csharp
// Register device-specific services
builder.Services.AddKeyedSingleton<IDeviceHandler, TempSensorHandler>("TempSensor");
builder.Services.AddKeyedSingleton<IDeviceHandler, PressureSensorHandler>("PressureSensor");
```

---

## When to Use wedaapi-c vs wedaapi vs subnode?

### Stick with `wedaapi-c` (Advanced API) if:
- *> You need custom service registration
- *> Full control over logging configuration
- *> Integrating with existing infrastructure
- *> Enterprise requirements (database, health checks, etc.)

### Downgrade to `wedaapi` (Simple API) when:
- ! You don't need custom services
- ! Default logging is sufficient
- ! Prefer configuration over code

### Upgrade to `subnode` (Custom Device) when:
- ! Need to override device lifecycle methods
- ! Custom protocol implementation required
- ! Want to handle device events programmatically

---

## Next Steps

Now that you have an advanced application, continue learning:

1. **[02. Sandbox Testing](02_sandbox_testing.md)** - Test without hardware
2. **[03. Data Transformations](03_add_transformation.md)** - Advanced data processing
3. **[04. DSP Filters](04_add_dsp_filters.md)** - Signal processing techniques
4. **[05. Hooks & Events](05_using_hooks.md)** - Lifecycle event handling

---

## Summary

**Advanced API Mode (`wedaapi-c`)** gives you:
- [CTRL] **Full control** - Configure everything manually
- [ELEC] **DI support** - Register custom services easily
- [DATA] **Enterprise-ready** - Database, health checks, background services
- [TOOL] **Flexible** - Custom logging, middleware, configuration

**Trade-offs**:
- ! More boilerplate code than `wedaapi`
- ! Need to understand .NET builder pattern
- ! More configuration required

**Perfect for**: Enterprise applications, custom integrations, complex business logic.
