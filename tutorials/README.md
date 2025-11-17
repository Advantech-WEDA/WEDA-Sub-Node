# Tutorials

This directory contains **tutorial examples** for learning the Weda SubNode SDK. All tutorials use **Mock Cloud** and **Simulators** so you can run them without any real hardware or cloud connection.

## 📚 Tutorial Structure

Tutorials are designed to correspond with the [documentation wiki](../docs/wiki/):

| Tutorial | Wiki Chapter | Description |
|----------|--------------|-------------|
| `01-transform-dsp-config` | Transform & DSP (Config) | Demonstrates Transform and DSP Filter usage with appsettings.json |
| `02-transform-dsp-programmatic` | Transform & DSP (Programmatic) | Demonstrates Transform and DSP Filter usage with code configuration |

## 🎯 Tutorial vs Example

### Tutorials (this directory)
- **Purpose**: Step-by-step learning, corresponds to wiki documentation
- **Environment**: Mock Cloud + Simulators (no hardware needed)
- **Target Audience**: Developers learning the SDK
- **Code Style**: Heavily commented, educational focus

### Examples (../examples/)
- **Purpose**: Real-world integration scenarios
- **Environment**: Real connections (TCP/RTU) or real hardware
- **Target Audience**: Developers implementing production solutions
- **Code Style**: Production-ready, practical focus

## 🚀 Running Tutorials

Each tutorial is self-contained and can be run independently:

```bash
cd tutorials/01-transform-dsp-config
dotnet run
```

All tutorials will:
1. Start a Modbus TCP simulator automatically
2. Connect to the simulator (127.0.0.1:5020)
3. Use Mock Cloud (no internet connection required)
4. Display output with explanations

## 📖 Learning Path

We recommend following tutorials in this order:

1. **Start with Examples** - Run [examples/basic-modbus](../examples/basic-modbus) to understand basic device connectivity
2. **Transform Pipeline** - Run `01-transform-dsp-config` to learn configuration-based transforms
3. **Programmatic API** - Run `02-transform-dsp-programmatic` to learn code-based configuration

## 📝 Tutorial Template Structure

Each tutorial contains:
- `README.md` - Tutorial instructions and learning objectives
- `Program.cs` - Main application code with detailed comments
- `MyFirstDevice.cs` - Custom device implementation
- `appsettings.json` - Configuration file
- `appsettings.template.json` - Template for customization

## 🆘 Getting Help

- **Documentation**: [Wiki Documentation](../docs/wiki/)
- **Issues**: [GitHub Issues](https://github.com/advantech/edge_subnode/issues)
- **Examples**: See [examples/](../examples/) for production scenarios

## 🔗 Related Resources

- [Examples Directory](../examples/) - Real hardware examples
- [Templates](../templates/) - Project templates (subnode, wedabuilder)
- [Documentation Wiki](../docs/wiki/) - Complete SDK documentation
