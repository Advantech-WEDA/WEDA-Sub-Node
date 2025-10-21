English / [繁體中文](README_zh.md)

# Weda SubNode SDK

A comprehensive .NET SDK for IoT edge devices, providing device connectivity, data acquisition, processing, and cloud integration.

## Documentation

**[Read Full Documentation](docs/wiki/en/README.md)**

- [Introduction](docs/wiki/en/introduction.md) - SDK overview and features
- [How to Start](docs/wiki/en/how_to_start.md) - Quick start guide
- [Modbus Scanner](docs/wiki/en/modbus/ModbusScanner.md) - Auto-discover device configuration
- [Telemetry Transform](docs/wiki/en/transform/TelemetryTransform.md) - Data processing pipeline

## Quick Start

```bash
# Install template
dotnet new install Weda.SubNode.Templates

# Create project
dotnet new wedaapi -n MyIoTApp
cd MyIoTApp

# Run
dotnet run
```

## Features

- **Multi-Protocol Support** - Modbus TCP/RTU, MQTT
- **Data Processing Pipeline** - Calibration, filtering, transformation
- **Event-Driven Architecture** - Extensible event system
- **Cloud Integration** - Weda EdgeSync Cloud platform
- **Easy to Use** - ASP.NET Core-style framework

## License

Copyright © 2025 Advantech Corporation

---
**Version**: 0.0.1 | **Maintainer**: Rain Hu
