# Examples

This directory contains open source examples demonstrating various features and use cases of the EdgeSync SubNode framework.

## Available Examples

### 🚀 Quick Start Examples

#### `basic-modbus/`
Basic Modbus TCP device integration example using simulator.
- Demonstrates fundamental device setup
- Shows basic telemetry collection
- Good starting point for new users

#### `simulator-demo/`
Modbus simulator demonstration for testing without hardware.
- Includes TCP Modbus simulator
- Simulates temperature sensor readings
- Perfect for development and testing

## Running Examples

Each example directory contains:
- `Program.cs` - Main application entry point
- `appsettings.json` - Configuration file
- `README.md` - Specific instructions for that example

To run an example:
```bash
cd examples/basic-modbus
dotnet run
```

## Creating Your Own Example

You can use our dotnet templates to quickly create new projects:

```bash
# Install templates (first time only)
dotnet new install ./templates

# Create a new project from template
dotnet new subnode -n MyProject
dotnet new wedaapi -n MyProject
dotnet new wedaapi-c -n MyProject
```

## Documentation

For detailed documentation, see:
- [Quick Start Guide](../docs/wiki/en/quick_start.md)
- [API Reference](../docs/wiki/en/api_reference.md)
- [Use Cases](../docs/wiki/en/use_cases.md)

## Contributing

Examples should:
- Be self-contained and runnable
- Include clear documentation
- Use simulator or mock data (no real hardware requirements)
- Follow the coding conventions of the framework

## Internal Examples

For Advantech internal examples with real hardware, see the `internal/` directory (available only in internal branches).
