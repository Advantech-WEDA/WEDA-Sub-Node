---
title: "Overview - Choose Your Template"
description: "Compare subnode, wedaapi, and wedaapi-c templates to choose the right one for your project"
author: "Rain Hu"
date: "2025-11-07"
lang: "en"
parent: "README"
next: "01_install_templates"
translations:
  - lang: "zh"
    path: "../../zh/01_quick_start/00_overview.md"
---

# Overview - Choose Your Template

Weda SubNode SDK provides three project templates for different use cases. This guide helps you choose the right one.

**Time**: 5 minutes
**Difficulty**: Beginner

---

## Three Templates at a Glance

| Feature | subnode | wedaapi | wedaapi-c |
|---------|---------|---------|-----------|
| **Command** | `dotnet new subnode` | `dotnet new wedaapi` | `dotnet new wedaapi-c` |
| **Setup Time** | 15 min | 10 min | 15 min |
| **Code Required** | Custom class | Minimal | Manual setup |
| **Configuration** | Mixed | Pure JSON | Mixed |
| **Flexibility** | High | Low | Maximum |
| **Learning Curve** | Moderate | Easy | Advanced |
| **Auto-Config** | Manual | Yes | Manual |
| **Custom Logic** | Full control | Limited | Full control |
| **DI Control** | No | No | Full |
| **Best For** | Custom devices | Quick start | Enterprise |

---

## Template 1: subnode - Custom Device

**When to use**:
- Need custom device lifecycle logic
- Want to override event handlers
- Implementing custom protocols
- Maximum control over device behavior

**What you get**:
- Inherit from `TcpModbusDevice` base class
- Override methods like `OnDataReceived`
- Custom event handlers
- Full protocol control

**Example structure**:
```
MyDevice/
├── MyDevice.cs              # Your custom device class
├── Program.cs               # Application entry
├── appsettings.json         # Configuration
└── MyDevice.csproj
```

**Key code**:
```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Your custom logic here
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("{Name}: {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

**Pros**:
- Clear object-oriented architecture
- Natural extension through inheritance
- Separation of concerns, easy to maintain
- Testable code

**Cons**:
- More code to write
- Need to understand device lifecycle
- Moderate learning curve

---

## Template 2: wedaapi - Simple API

**When to use**:
- Want the fastest setup
- Standard Modbus device with no custom logic
- Configuration-driven approach preferred
- Just need to collect data and send to cloud

**What you get**:
- Everything configured in `appsettings.json`
- Automatic device initialization
- Built-in logging and error handling
- Zero custom code required

**Example structure**:
```
MyApp/
├── Program.cs               # 3 lines of code!
├── appsettings.json         # All configuration here
└── MyApp.csproj
```

**Key code**:
```csharp
// Program.cs - That's it!
var app = WedaApplication.CreateDefaultBuilder(args).Build();
await app.RunAsync();
```

**Pros**:
- Fastest way to get started
- No coding required for basic scenarios
- Perfect for beginners
- Easy to maintain

**Cons**:
- Less intuitive when extending functionality
- Uses framework's built-in device classes by default
- Best suited for standard Modbus use cases

---

## Template 3: wedaapi-c - Advanced API

**When to use**:
- Need to register custom services
- Want full control over dependency injection
- Custom logging or middleware required
- Integrating with existing .NET infrastructure

**What you get**:
- Manual service registration
- Full builder pattern control
- Custom logging configuration
- Manual device registration with `AddDevice<TDevice>()`

**Example structure**:
```
MyApp/
├── MyCustomDevice.cs        # Your device class
├── MyCustomService.cs       # Your services
├── Program.cs               # Manual setup
├── appsettings.json         # Configuration
└── MyApp.csproj
```

**Key code**:
```csharp
// Program.cs
var builder = WedaApplication.CreateBuilder(args);

// Register custom services
builder.Services.AddSingleton<IMyService, MyService>();

// Customize logging
builder.Logging.AddFilter("Weda.SubNode", LogLevel.Debug);

// Manually add devices
builder.AddDevice<MyCustomDevice>(deviceConfig);

var app = builder.Build();
await app.RunAsync();
```

**Pros**:
- Maximum flexibility
- Full control over DI container
- Easy to integrate with existing apps
- Professional architecture

**Cons**:
- More setup required
- Advanced .NET knowledge needed
- Can be overkill for simple scenarios

---

## Decision Tree

```
Start here
    │
    ├─→ First time using SDK?
    │   └─→ YES → Use wedaapi ✓
    │
    ├─→ Need custom device logic?
    │   └─→ YES → Use subnode ✓
    │
    ├─→ Need custom services/DI?
    │   └─→ YES → Use wedaapi-c ✓
    │
    └─→ Just collecting data?
        └─→ YES → Use wedaapi ✓
```

---

## What's Included in All Templates?

All three templates include:

- **Modbus Simulator** (ModbusPal) - Test without hardware
- **MockCloudService** - Local development without cloud
- **Serilog Logging** - Structured logging
- **Configuration Support** - appsettings.json
- **Error Handling** - Retry policies and circuit breakers
- **Health Monitoring** - Device health reporting

**Default Setup**:
- Connects to `127.0.0.1:502` (local Modbus Simulator)
- Uses `MockCloudService` (no cloud connection needed)
- Polls device every 3 seconds
- Logs to console and file

---

## Comparison by Use Case

### Use Case 1: Quick Data Collection
**Scenario**: Read temperature from a Modbus sensor, send to cloud
**Recommended**: `wedaapi`
**Why**: Zero code, pure configuration

### Use Case 2: Need Clear Class Architecture
**Scenario**: Apply rules, trigger alerts, store to database
**Recommended**: `subnode`
**Why**: Object-oriented design, clean extension through inheritance

### Use Case 3: Complex Dependency Injection
**Scenario**: Integrate with existing ASP.NET Core app, multiple custom services
**Recommended**: `wedaapi-c`
**Why**: Full control over DI container and service registration

### Use Case 4: Multi-Device Gateway
**Scenario**: Manage 10+ devices with different configurations
**Recommended**: `wedaapi` for simple, `wedaapi-c` for complex
**Why**: Easy to configure multiple devices in JSON

### Use Case 5: Emphasis on Code Organization
**Scenario**: Large project requiring modularization and clear architecture
**Recommended**: `subnode` or `wedaapi-c`
**Why**: `subnode` provides class inheritance structure, `wedaapi-c` provides DI architecture

---

## Important Note

**All templates can implement custom business logic and custom protocols!**

The difference between templates lies in:
- **Architecture style**: OOP inheritance vs configuration-driven vs DI container control
- **Initial complexity**: How much boilerplate code required
- **Extension approach**: How to add new features

Choose a template based on: **Your preferred development style** and **your project's architectural needs**.

---

## Next Steps

Now that you understand the templates, let's install them:

**[→ Install Templates](01_install_templates.md)**

After installation, jump to your chosen template:
- [subnode - Custom Device →](02_subnode_basic.md)
- [wedaapi - Simple API →](03_wedaapi_basic.md)
- [wedaapi-c - Advanced API →](04_wedaapi_c_basic.md)

---

## Summary

**Choose subnode** if you want to write custom device classes with event handlers.

**Choose wedaapi** if you want the fastest setup with zero code.

**Choose wedaapi-c** if you need full control over services and DI.

**Still unsure?** Start with **wedaapi** - it's the easiest way to learn!

---

**Version**: 1.0.0
**Last Updated**: 2025-11-07
**Maintainer**: Rain Hu
