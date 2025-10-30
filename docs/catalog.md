# Weda SubNode SDK - Documentation Catalog

This catalog provides a structured guide to all documentation in this repository.

## 📖 Getting Started

**New to Weda SubNode?** Start here:

1. **[Introduction](wiki/en/introduction.md)** - What is Weda SubNode SDK?
2. **[How to Start](wiki/en/how_to_start.md)** - Quick start guide (15 min)

## 🏗️ Architecture & Design

Understanding the SDK architecture:

- **[Architecture Overview](wiki/en/architecture_overview.md)** - Layered architecture and design principles
- **[Device Lifecycle](wiki/en/device_lifecycle.md)** - Device state management and lifecycle

## 🔧 Core Features

### Protocol Support
- **[Modbus Scanner](wiki/en/modbus_scanner.md)** - Auto-detect device register configurations

### Data Processing
- **[Telemetry Transform](wiki/en/telemetry_transform.md)** - Data transformation pipeline with context

## 📚 Guides

Best practices and advanced topics:

- **[Strongly-Typed Configuration](wiki/en/guide_strongly_typed_configuration.md)** - Type-safe device configuration

## 🎓 Examples & Tutorials

Step-by-step learning path:

### Overview
- **[00. Overview](wiki/en/example_00_overview.md)** - Start here! Complete overview of examples
- **[Summary](wiki/en/example_summary.md)** - Quick reference of all examples

### Quick Start (Choose Your Path)
- **[01.A. Weda API](wiki/en/example_01_a_wedaapi.md)** - Using Weda Application API
- **[01.B. Weda API (C)](wiki/en/example_01_b_wedaapi_c.md)** - Using Weda API in C
- **[01.C. SubNode](wiki/en/example_01_c_subnode.md)** - Using SubNode SDK directly
- **[01. Quick Start](wiki/en/example_01_quick_start.md)** - Consolidated quick start guide

### Development Workflow
- **[02. Sandbox Testing](wiki/en/example_02_sandbox_testing.md)** - Test without hardware (20 min)
- **[03. Data Transformations](wiki/en/example_03_add_transformation.md)** - Process sensor data (30 min)
- **[04. DSP Filters](wiki/en/example_04_add_dsp_filters.md)** - Signal processing (30 min)
- **[05. Hooks & Handlers](wiki/en/example_05_using_hooks.md)** - Extend functionality (30 min)

### Complete Reference
- **[Getting Started Complete](wiki/en/example_GETTING_STARTED_COMPLETE.md)** - Comprehensive getting started guide

---

## 📂 Documentation Structure

```
docs/
├── wiki/
│   ├── en/              # English documentation
│   └── zh/              # Chinese documentation (繁體中文)
├── .tmp/                # Work-in-progress documentation
├── catalog.md           # This file (English catalog)
├── catalog_zh.md        # Chinese catalog
└── .rule.md             # Documentation standards and rules
```

## 🌐 Other Languages

- [繁體中文 (Traditional Chinese)](catalog_zh.md)

---

**Last Updated**: 2025-10-21
**Maintainer**: Rain Hu
