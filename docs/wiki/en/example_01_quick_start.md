# Quick Start - Choose Your Path

The Weda SubNode SDK provides three templates for different use cases. Choose the one that best fits your needs.

## >>> Three Ways to Get Started

### 1. Simple API Mode (Recommended for Beginners)
**Template**: `dotnet new wedaapi`  
**Time**: 5 minutes  
**Best for**: Quick data collection, auto-configuration, minimal code

- Everything configured in JSON
- Automatic device initialization
- Built-in logging and error handling
- Perfect for standard Modbus devices

**[→ Start with Simple API](01_a_wedaapi.md)**

---

### 2. Advanced API Mode (Full Control)
**Template**: `dotnet new wedaapi-c`  
**Time**: 10 minutes  
**Best for**: Custom services, dependency injection, complex applications

- Manual service registration
- Custom logging configuration
- Full control over initialization
- Integrate custom business logic

**[→ Start with Advanced API](01_b_wedaapi_c.md)**

---

### 3. Custom Device Mode (Maximum Flexibility)
**Template**: `dotnet new subnode`  
**Time**: 15 minutes  
**Best for**: Custom device logic, event handling, protocol customization

- Inherit from `ModbusDevice` base class
- Override device lifecycle methods
- Custom event handlers
- Full protocol control

**[→ Start with Custom Device](01_c_subnode.md)**

---

## Prerequisites (All Modes)

Before you begin, ensure you have:

- **.NET 9.0 SDK** or later
  ```bash
  dotnet --version  # Should be 9.0.x or higher
  ```

- **Code Editor** (VS Code, Visual Studio, or Rider)

- **Optional**: Modbus device or simulator for testing

- **Optional**: NATS server for cloud integration
  ```bash
  nats-server -js
  ```

## Install Templates (One-Time Setup)

Navigate to the SDK repository and run the installation script:

```bash
cd /path/to/weda_subnode
bash scripts/install-templates.sh
```

You should see output confirming the installation:

```
════════════════════════════════════════════════════════════════
 Weda SubNode SDK - Template Installation
════════════════════════════════════════════════════════════════

Success: Installed 3 templates
  - wedaapi   : Simple API (auto-configuration)
  - wedaapi-c : Advanced API (full control)
  - subnode   : Custom Device (maximum flexibility)
```

Verify installation:
```bash
dotnet new list | grep -i subnode
```

Expected output:
```
Weda SubNode API (Simple)     wedaapi    [C#]  Weda/SubNode/IoT
Weda SubNode API (Advanced)   wedaapi-c  [C#]  Weda/SubNode/IoT/Advanced
Weda SubNode Custom Device    subnode    [C#]  Console/IoT/Weda/SubNode/Custom
```

---

## [TARGET] Which Template Should I Choose?

### Choose **wedaapi** (Simple API) if:
- You want the fastest setup
- Standard Modbus device with no custom logic
- Configuration-driven approach preferred
- Just need to collect data and send to cloud

**Perfect for**: Beginners, quick prototypes, standard data collection

---

### Choose **wedaapi-c** (Advanced API) if:
- Need to register custom services
- Want full control over dependency injection
- Custom logging or middleware required
- Integrating with existing .NET infrastructure

**Perfect for**: Enterprise applications, complex integrations, custom services

---

### Choose **subnode** (Custom Device) if:
- Need custom device lifecycle logic
- Want to override event handlers
- Implementing custom protocols
- Maximum control over device behavior

**Perfect for**: Custom protocols, advanced event handling, specialized devices

---

## [DOCS] Detailed Guides

Pick your template and follow the corresponding guide:

### 1. [Simple API (wedaapi) →](01_a_wedaapi.md)
- Minimal code, maximum productivity
- Auto-configuration from JSON
- Perfect for beginners
- **Recommended starting point**

### 2. [Advanced API (wedaapi-c) →](01_b_wedaapi_c.md)
- Full builder pattern control
- Custom service registration
- Advanced scenarios

### 3. [Custom Device (subnode) →](01_c_subnode.md)
- Inherit from base classes
- Override lifecycle methods
- Maximum flexibility

---

## Template Comparison

| Feature | wedaapi | wedaapi-c | subnode |
|---------|---------|-----------|---------|
| **Setup Time** | 5 min | 10 min | 15 min |
| **Code Required** | Minimal | Moderate | Full |
| **Flexibility** | Low | High | Maximum |
| **Learning Curve** | Easy | Moderate | Advanced |
| **Auto-Config** | Yes | ! Manual | ! Manual |
| **Custom Logic** | X Limited | Full | Full |
| **DI Control** | X No | Full | Full |
| **Best For** | Quick start | Enterprise | Custom devices |

---

## Next Steps

After completing any quick start guide, continue with:

1. **[02. Sandbox Testing](02_sandbox_testing.md)** - Test without hardware
2. **[03. Data Transformations](03_add_transformation.md)** - Process sensor data
3. **[04. DSP Filters](04_add_dsp_filters.md)** - Apply signal processing
4. **[05. Hooks & Events](05_using_hooks.md)** - Extend functionality

---

## Common Questions

**Q: Can I switch between templates later?**  
A: Yes! The templates are just starting points. You can migrate between them as your needs evolve.

**Q: Which template do most users choose?**  
A: Beginners typically start with `wedaapi` (Simple API), then move to `subnode` (Custom Device) when they need more control.

**Q: Do I need NATS server?**  
A: NATS is optional for local testing. You can disable cloud integration in `appsettings.json`.

**Q: Can I use multiple devices?**  
A: Yes! All templates support multiple devices configured in `appsettings.json`.

**Q: What's the difference between wedaapi and wedaapi-c?**  
A: `wedaapi` uses auto-configuration with minimal code. `wedaapi-c` gives you full control over the application builder, services, and initialization.

---

## Summary

The Weda SubNode SDK provides three templates for different needs:

1. **wedaapi** - Fast, simple, configuration-driven
2. **wedaapi-c** - Full control, custom services
3. **subnode** - Maximum flexibility, custom devices

**Ready to build?** Pick a guide above and get started!
