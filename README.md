English / [繁體中文](README_zh.md)

# Weda SubNode SDK

A comprehensive .NET SDK for IoT edge devices, providing device connectivity, data acquisition, processing, and cloud integration.

## Prerequisites

Choose one of the following environment setups:

### Option 1: Dev Container (Recommended)

Pre-configured development environment with everything installed:

**Requirements:**
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

**Setup:**
1. Open this project in VS Code
2. Press `Ctrl + Shift + P` → Select `Dev Containers: Reopen in Container`
3. Wait for container to build (first time only)

Everything is pre-installed: .NET SDK, project templates, and all development tools!

**[Learn more about Dev Container](.devcontainer/README.md)**

### Option 2: Local Installation

Install dependencies manually on your local machine:

**Requirements:**
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Code editor (VS Code, Visual Studio, or Rider)
- Git (for cloning repository)

**Setup:**
Follow the [Installation Guide](docs/wiki/en/01_quick_start/01_install_templates.md) for detailed instructions.

---

## Quick Start

Choose your learning path based on your background:

### Path 1: Start from Scratch (For .NET Developers)

**Best for:** Developers familiar with .NET who want to build SubNode applications from the ground up.

**Steps:**
1. Install templates:
   ```bash
   dotnet new install ./templates
   ```

2. Create a new project:
   ```bash
   # For simple applications (Builder pattern)
   dotnet new wedabuilder -n MyProject

   # For custom device implementation (Manual Context)
   dotnet new subnode -n MyProject
   ```

3. Follow the documentation:
   - [Quick Start Guide](docs/wiki/en/01_quick_start/00_overview.md)
   - [SubNode Basic](docs/wiki/en/01_quick_start/03_subnode_basic.md)
   - [WedaBuilder Basic](docs/wiki/en/01_quick_start/02_wedabuilder_basic.md)

### Path 2: Study from Examples (For System Integrators)

**Best for:** System integrators who want to quickly adapt existing examples for their hardware.

**Steps:**
1. Browse available examples:
   - [examples/](examples/) - Production-ready examples with real hardware
   - [tutorials/](tutorials/) - Learning examples with simulators

2. Choose an example that matches your use case:
   - **WISE-4012 Industrial I/O**: [examples/wise-4012-builder/](examples/wise-4012-builder/)
   - **Transform & DSP Filters**: [tutorials/01-transform-dsp-config/](tutorials/01-transform-dsp-config/)

3. Copy and customize:
   ```bash
   # Copy the example
   cp -r examples/wise-4012-builder my-project
   cd my-project

   # Edit configuration
   nano appsettings.json
   # Update: IP address, sensor mappings, device settings

   # Run
   dotnet run
   ```

4. Adapt to your hardware:
   - Modify `appsettings.json` for your device IP and sensor configuration
   - Update DTDL metadata if needed
   - Add custom processing logic in device class

**[Browse All Examples](examples/README.md)** | **[Browse All Tutorials](tutorials/README.md)**

---

## Documentation

**[Read Full Documentation](docs/wiki/en/README.md)**

### Key Documentation Sections

- **Quick Start**: Get up and running quickly
  - [Overview](docs/wiki/en/01_quick_start/00_overview.md)
  - [Install Templates](docs/wiki/en/01_quick_start/01_install_templates.md)
  - [WedaBuilder Basic](docs/wiki/en/01_quick_start/02_wedabuilder_basic.md)
  - [SubNode Basic](docs/wiki/en/01_quick_start/03_subnode_basic.md)

- **Examples & Tutorials**:
  - [Production Examples](examples/README.md) - Real hardware integration
  - [Learning Tutorials](tutorials/README.md) - Step-by-step guides

- **Advanced Topics**:
  - [Configuration Reference](docs/wiki/en/03_advanced/appsettings_configuration.md)
  - [Transform & DSP Filters](docs/wiki/en/03_advanced/transform_dsp.md)
  - [DTDL Integration](docs/wiki/en/03_advanced/dtdl_integration.md)

---

## Features

- **Multi-Protocol Support** - Modbus TCP/RTU, MQTT
- **Data Processing Pipeline** - Calibration, filtering, transformation
- **Event-Driven Architecture** - Extensible event system
- **Cloud Integration** - Weda EdgeSync Cloud platform
- **Easy to Use** - ASP.NET Core-style Builder pattern

---

## Project Structure

```
edge_subnode/
├── examples/          # Production-ready examples (real hardware)
├── tutorials/         # Learning tutorials (Mock Cloud + Simulators)
├── templates/         # dotnet new templates
│   ├── subnode/       # Manual Context pattern
│   └── wedabuilder/   # Builder pattern
├── src/               # SDK source code
├── tests/             # Unit and integration tests
└── docs/              # Documentation wiki
```

---

## Getting Help

- **Documentation**: [docs/wiki/en/README.md](docs/wiki/en/README.md)
- **Examples**: [examples/README.md](examples/README.md)
- **Tutorials**: [tutorials/README.md](tutorials/README.md)

---

## License

Copyright © 2025 Advantech Corporation

---

**Version**: 0.0.1 | **Maintainer**: Rain Hu
