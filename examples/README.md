# Examples

This directory contains **production-ready examples** demonstrating real-world integration scenarios with the Weda SubNode SDK. All examples use **real connections** (TCP/RTU) or **real hardware**.

## 📚 Examples vs Tutorials

### Examples (this directory)
- **Purpose**: Real-world integration scenarios
- **Environment**: Real connections (TCP/RTU) or real hardware
- **Target Audience**: Developers implementing production solutions
- **Code Style**: Production-ready, practical focus

### Tutorials (../tutorials/)
- **Purpose**: Step-by-step learning, corresponds to wiki documentation
- **Environment**: Mock Cloud + Simulators (no hardware needed)
- **Target Audience**: Developers learning the SDK
- **Code Style**: Heavily commented, educational focus

---

## 📂 Available Examples

### 🚀 Basic Examples

#### [`basic-modbus/`](basic-modbus/)
Basic Modbus TCP device integration using **manual Context pattern**.
- **Pattern**: Manual Context creation (`new WedaApplicationContext()`)
- **Protocol**: Modbus TCP
- **Connection**: Real TCP connection (requires Modbus server)
- **Best For**: Learning SDK fundamentals, single device development
- **Template Equivalent**: `subnode`

#### [`basic-modbus-builder/`](basic-modbus-builder/)
Basic Modbus TCP device integration using **Builder pattern**.
- **Pattern**: Builder pattern (`WedaApplication.CreateBuilder()`)
- **Protocol**: Modbus TCP
- **Connection**: Real TCP connection (requires Modbus server)
- **Best For**: Production environments, multiple devices
- **Template Equivalent**: `wedabuilder`

### 🏭 Real Hardware Examples

#### [`wise-4012/`](wise-4012/)
Advantech WISE-4012 industrial I/O module integration.
- **Hardware**: WISE-4012 (4AI + 2AO)
- **Protocol**: Modbus TCP
- **Features**: Multi-channel analog I/O, DTDL metadata
- **Use Case**: Industrial monitoring and control

#### [`wise-4012-isensing/`](wise-4012-isensing/)
WISE-4012 with iSensing intelligent diagnostic features.
- **Hardware**: WISE-4012 with iSensing
- **Protocol**: Modbus TCP
- **Features**: Advanced diagnostics, anomaly detection
- **Use Case**: Predictive maintenance

---

## 🎯 Choosing the Right Example

### For Learning
1. Start with **[basic-modbus](basic-modbus/)** to understand SDK fundamentals
2. Progress to **[tutorials/](../tutorials/)** for specific features (Transform, DSP)
3. Review real hardware examples for integration patterns

### For Production
1. Use **[basic-modbus-builder](basic-modbus-builder/)** as a template
2. Reference **[wise-4012](wise-4012/)** for DTDL and multi-sensor patterns
3. Adapt configuration for your specific hardware

---

## 🚀 Running Examples

### Prerequisites

1. **.NET 9.0 SDK** installed
2. **Modbus TCP server** or **real hardware** running
3. **Cloud configuration** (or use Mock Cloud for testing)

### Basic Steps

```bash
# Navigate to example directory
cd examples/basic-modbus-builder

# Edit appsettings.json to configure connection
nano appsettings.json

# Run the example
dotnet run
```

### Common Configuration

All examples use `appsettings.json` for configuration:

```json
{
  "Nats": {
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

---

## 📖 Example Structure

Each example typically contains:

- **README.md** - Example overview and instructions
- **Program.cs** - Main application entry point
- **MyDevice.cs** (or similar) - Custom device implementation
- **appsettings.json** - Configuration file
- **[DeviceName].csproj** - Project file

---

## 🔄 Pattern Comparison

| Feature | basic-modbus | basic-modbus-builder |
|---------|--------------|----------------------|
| **Pattern** | Manual Context | Builder Pattern |
| **DI Container** | ❌ No | ✅ Microsoft.Extensions.DI |
| **Hosted Services** | ❌ Manual lifecycle | ✅ Automatic lifecycle |
| **Configuration** | Manual loading | Automatic loading |
| **Multi-Device** | Manual management | Automatic management |
| **Best For** | Learning, debugging | Production, scaling |
| **Code Lines** | More explicit | More concise |

---

## 🆘 Getting Help

- **Tutorials**: See [tutorials/](../tutorials/) for learning resources
- **Documentation**: [Wiki Documentation](../docs/wiki/)
- **Templates**: Use `dotnet new subnode` or `dotnet new wedabuilder`
- **Issues**: [GitHub Issues](https://github.com/advantech/edge_subnode/issues)

---

## 📝 Contributing

When adding new examples:

1. **Examples** should use real connections or real hardware
2. Include comprehensive README with:
   - Hardware/connection requirements
   - Configuration instructions
   - Expected output
3. Follow existing project structure
4. Test with real hardware before submitting

For tutorial content (Mock Cloud + Simulators), add to [tutorials/](../tutorials/) instead.

---

## 🔗 Related Resources

- [Tutorials Directory](../tutorials/) - Learning examples with Mock Cloud
- [Templates](../templates/) - Project templates (subnode, wedabuilder)
- [Documentation Wiki](../docs/wiki/) - Complete SDK documentation
- [Internal Examples](../internal/) - Advantech internal hardware examples (internal branches only)
