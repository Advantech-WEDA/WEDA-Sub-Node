---
title: "Weda SubNode SDK - Documentation"
description: "Complete documentation for building IoT edge devices with Weda SubNode SDK"
author: "Rain Hu"
date: "2025-11-07"
lang: "en"
translations:
  - lang: "zh"
    path: "../zh/README.md"
---

# Weda SubNode SDK - Documentation

Welcome to the Weda SubNode SDK documentation! This guide will help you build industrial IoT edge applications with .NET 9.0.

**[繁體中文文檔 →](../zh/README.md)**

---

## What is Weda SubNode SDK?

Weda SubNode SDK is a .NET framework for building industrial IoT edge applications that:
- Connect to devices via Modbus TCP/RTU and other protocols
- Collect telemetry data from sensors and equipment
- Process data with transformations and DSP filters
- Transmit to cloud services via NATS messaging
- Control edge devices from cloud for remote management
- Monitor device health and handle errors gracefully

---

## Quick Navigation

### For Beginners
Start here if you're new to Weda SubNode SDK:
1. [Overview - Choose Your Template](01_quick_start/00_overview.md) (5 min)
2. [Install Templates](01_quick_start/01_install_templates.md) (3 min)
3. [Your First Device - wedabuilder](01_quick_start/03_wedabuilder_basic.md) (10 min)

### For Experienced Developers
Jump to advanced topics:
- [Console App Style - subnode](01_quick_start/02_subnode_basic.md)
- [Use Cases](02_use_cases/)

---

## Learning Path

### Step 1: Getting Started (30 min)

Choose one of two templates based on your needs:

| Document | Description | Template | Time | Difficulty |
|----------|-------------|----------|------|------------|
| [00. Overview](01_quick_start/00_overview.md) | Compare templates and choose the right one | - | 5 min | Beginner |
| [01. Install Templates](01_quick_start/01_install_templates.md) | Install project templates | - | 3 min | Beginner |
| [02. subnode - Console App](01_quick_start/02_subnode_basic.md) | Console-style for single device development | `subnode` | 15 min | Intermediate |
| [03. wedabuilder - Web API](01_quick_start/03_wedabuilder_basic.md) | Web API-style for production with multiple devices | `wedabuilder` | 10 min | Beginner |

**Default Setup**: All templates use **Modbus Simulator** + **MockCloudService** by default.

### Step 2: Connect to Real Environment (30 min)

| Document | Description | Time | Difficulty |
|----------|-------------|------|------------|
| [05. Connect Real Device](01_quick_start/05_connect_real_device.md) | Switch from Simulator to real Modbus device | 15 min | Beginner |
| [06. Connect Weda.Core](01_quick_start/06_connect_weda_core.md) | Switch from MockCloudService to Weda.Core cloud | 15 min | Intermediate |
| [07. iSensing Device](01_quick_start/07_real_device_isensing.md) | Connect to iSensing protocol devices | 20 min | Advanced |

### Step 3: Advanced Use Cases (60+ min)

| Document | Description | Time | Difficulty |
|----------|-------------|------|------------|
| [Custom Capability](02_use_cases/01_custom_capability.md) | Define custom DTDL capability fields | 20 min | Intermediate |
| [Transformation Pipeline](02_use_cases/02_transformation.md) | Calibration, unit conversion, custom transforms | 30 min | Intermediate |
| [DSP Filters](02_use_cases/03_dsp_filters.md) | Noise reduction, Kalman filtering | 30 min | Advanced |
| [Hooks & Events](02_use_cases/04_hooks.md) | Lifecycle hooks, database persistence | 30 min | Advanced |

### Step 4: Advanced Topics (As needed)

| Document | Description |
|----------|-------------|
| [Device Lifecycle](03_advanced/device_lifecycle.md) | Device state machine and lifecycle management |
| [Modbus Scanner](03_advanced/modbus_scanner.md) | Auto-configuration and device discovery |
| [Telemetry Transform](03_advanced/telemetry_transform.md) | Data transformation pipeline architecture |

---

## Two Templates Comparison

| Feature | subnode | wedabuilder |
|---------|---------|---------|
| **Style** | Console App | Web API |
| **Setup Time** | 15 min | 10 min |
| **Code Required** | Custom class | Minimal |
| **Flexibility** | High | Moderate |
| **Learning Curve** | Moderate | Easy |
| **Use Case** | Single device dev/debug | Production multi-device |
| **Best For** | Development & debugging | Production deployment |

**Recommendation**:
- New to SDK? Start with **wedabuilder**
- Need single device dev/debug? Use **subnode**
- Production deployment? Use **wedabuilder**

---

## Prerequisites

Before you begin:

- **.NET 9.0 SDK** or later
  ```bash
  dotnet --version  # Should be 9.0.x or higher
  ```

- **Code Editor** (VS Code, Visual Studio, or Rider)

- **Optional**: Modbus Device or Simulator
  - Physical device (e.g., Advantech WISE-4012, PLCs)
  - Software simulator (ModbusPal, pyModSlave)
  - Built-in simulator in templates

- **Optional**: NATS Server (for cloud integration)
  ```bash
  # Install NATS server
  # macOS: brew install nats-server
  # Linux: See https://docs.nats.io/

  # Start NATS with JetStream
  nats-server -js
  ```

---

## Example Projects

Explore working examples in the repository:

### Open Source Examples
- [examples/basic-modbus](../../examples/basic-modbus) - Basic Modbus device with wedabuilder
- [examples/simulator-demo](../../examples/simulator-demo) - Complete simulator setup

### Internal Examples (Internal branch only)
- `internal/wise-4012` - Advantech WISE-4012 integration
- `internal/wise-4012-isensing` - WISE-4012 with iSensing protocol

---

## Documentation Structure

```
docs/wiki/
├── en/                                # English documentation
│   ├── README.md                      # This file - Navigation hub
│   ├── 01_quick_start/                # Getting started guides
│   │   ├── 00_overview.md             # Template comparison
│   │   ├── 01_install_templates.md    # Installation
│   │   ├── 02_subnode_basic.md        # subnode template (Console App)
│   │   ├── 03_wedabuilder_basic.md        # wedabuilder template (Web API)
│   │   ├── 05_connect_real_device.md  # Connect real device
│   │   ├── 06_connect_weda_core.md    # Connect Weda.Core
│   │   └── 07_real_device_isensing.md # iSensing devices
│   ├── 02_use_cases/                  # Advanced use cases
│   │   ├── 01_custom_capability.md    # DTDL customization
│   │   ├── 02_transformation.md       # Data transformation
│   │   ├── 03_dsp_filters.md          # Signal processing
│   │   └── 04_hooks.md                # Hooks and events
│   └── 03_advanced/                   # Architecture reference
│       ├── device_lifecycle.md        # Device state machine
│       ├── modbus_scanner.md          # Auto-configuration
│       └── telemetry_transform.md     # Transform pipeline
│
└── zh/                                # Chinese documentation (parallel structure)
    ├── README.md
    ├── 01_quick_start/
    ├── 02_use_cases/
    └── 03_advanced/
```

---

## Getting Help

- **Documentation**: You're reading it!
- **Examples**: Check [examples/](../../examples/) folder
- **Issues**: Report at GitHub repository
- **Contributing**: See [docs/.rule.md](../../.rule.md) for documentation guidelines

---

## Contributing to Documentation

When adding new documentation:

1. Follow the guidelines in [docs/.rule.md](../../.rule.md)
2. Add YAML front matter to all `.md` files
3. **Update en/README.md and zh/README.md** with the new article
4. Test all code examples
5. Ensure English and Chinese versions are in sync

---

## Next Steps

**Ready to start?**

1. [Overview - Choose Your Template →](01_quick_start/00_overview.md)
2. [Install Templates →](01_quick_start/01_install_templates.md)
3. [Build Your First Device →](01_quick_start/03_wedabuilder_basic.md)

---

**Version**: 1.0.0
**Last Updated**: 2025-11-07
**Maintainer**: Rain Hu
