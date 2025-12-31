# Examples

This directory contains **production-ready examples** demonstrating real-world integration scenarios with the Weda SubNode SDK. All examples use **real connections** or **real hardware**.

## Available Examples

### WISE-4012 Builder Example

#### [`wise-4012-builder/`](wise-4012-builder/)
Advantech WISE-4012 industrial I/O module integration using Builder pattern.
- **Pattern**: Builder pattern (`WedaApplication.CreateBuilder()`)
- **Hardware**: WISE-4012 (4AI + 2AO)
- **Protocol**: Modbus TCP
- **Features**: Multi-channel analog I/O, DTDL metadata, production-ready
- **Use Case**: Industrial monitoring and control
- **Template Equivalent**: `wedabuilder`

### Real Hardware Examples

#### [`wise-4012/`](wise-4012/)
Advantech WISE-4012 industrial I/O module integration using default Context singleton.
- **Pattern**: Default singleton (`WedaApplicationContext.Default`)
- **Hardware**: WISE-4012 (4AI + 2AO)
- **Protocol**: Modbus TCP
- **Features**: Multi-channel analog I/O, DTDL metadata
- **Use Case**: Industrial monitoring and control
- **Template Equivalent**: `subnode`

#### [`wise-4012-isensing/`](wise-4012-isensing/)
WISE-4012 with iSensing intelligent diagnostic features.
- **Hardware**: WISE-4012 with iSensing
- **Protocol**: Modbus TCP
- **Features**: Advanced diagnostics, anomaly detection
- **Use Case**: Predictive maintenance

## Examples vs Tutorials

### Examples (this directory)
- **Purpose**: Real-world integration scenarios
- **Environment**: Real connections or real hardware
- **Target Audience**: Developers implementing production solutions
- **Code Style**: Production-ready, practical focus

### Tutorials (../tutorials/)
- **Purpose**: Step-by-step learning, corresponds to wiki documentation
- **Environment**: Mock Cloud + Simulators (no hardware needed)
- **Target Audience**: Developers learning the SDK
- **Code Style**: Heavily commented, educational focus

## Choosing the Right Example

### For Learning
1. Start with **[tutorials/](../tutorials/)** for basic concepts
2. Review **[wise-4012-builder](wise-4012-builder/)** for production patterns
3. Study real hardware examples for integration details

### For Production
1. Use **[wise-4012-builder](wise-4012-builder/)** as a template
2. Reference DTDL and multi-sensor patterns
3. Adapt configuration for your specific hardware

## Running Examples

### Prerequisites

1. **.NET 9.0 SDK** installed
2. **Real hardware** or **Modbus TCP server** running
3. **Cloud configuration** (or use Mock Cloud for testing)

### Basic Steps

```bash
# Navigate to example directory
cd examples/wise-4012-builder

# Edit appsettings.json to configure connection
nano appsettings.json

# Run the example
dotnet run
```

### Common Configuration

All examples use `appsettings.json` for configuration:

```json
{
  "WedaNode": {
    "Url": "nats://your-cloud-server:4224"
  },
  "DeviceConfigs": {
    "YourDevice": {
      "Communication": {
        "Host": "192.168.1.100",  // Your device IP
        "Port": 502
      }
    }
  }
}
```

## Example Structure

Each example typically contains:

- **README.md** / **README_zh.md** - Example overview and instructions
- **Program.cs** - Main application entry point
- **MyDevice.cs** (or similar) - Custom device implementation
- **appsettings.json** - Configuration file
- **[DeviceName].csproj** - Project file

## Pattern Comparison

| Feature | wise-4012 | wise-4012-builder |
|---------|-----------|-------------------|
| **Pattern** | Manual Context | Builder Pattern |
| **DI Container** | No | Yes (Microsoft.Extensions.DI) |
| **Hosted Services** | Manual lifecycle | Automatic lifecycle |
| **Configuration** | Manual loading | Automatic loading |
| **Multi-Device** | Manual management | Automatic management |
| **Best For** | Learning, debugging | Production, scaling |
| **Code Lines** | More explicit | More concise |

## Getting Help

- **Tutorials**: See [tutorials/](../tutorials/) for learning resources
- **Documentation**: [Wiki Documentation](../docs/wiki/)
- **Templates**: Use `dotnet new subnode` or `dotnet new wedabuilder`
- **Issues**: [GitHub Issues](https://github.com/advantech/edge_subnode/issues)

## Contributing

When adding new examples:

1. **Examples** should use real connections or real hardware
2. Include comprehensive README (both English and Chinese)
3. Follow existing project structure
4. Test with real hardware before submitting

For tutorial content (Mock Cloud + Simulators), add to [tutorials/](../tutorials/) instead.

## Related Resources

- [Tutorials Directory](../tutorials/) - Learning examples with Mock Cloud
- [Templates](../templates/) - Project templates (subnode, wedabuilder)
- [Documentation Wiki](../docs/wiki/) - Complete SDK documentation
