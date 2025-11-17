---
title: "Overview - Weda SubNode SDK Introduction"
description: "Learn about Weda SubNode SDK's core capabilities and three project templates"
author: "Rain Hu"
date: "2025-11-07"
lang: "en"
parent: "README"
next: "01_install_templates"
translations:
  - lang: "zh"
    path: "../../zh/01_quick_start/00_overview.md"
---

# Overview - Weda SubNode SDK Introduction

Weda SubNode SDK is a .NET framework for connecting edge devices to cloud platforms, enabling you to easily build IoT edge nodes for seamless cloud-to-edge integration.

**Time**: 5 minutes
**Difficulty**: Beginner

---

## What is Weda SubNode?

**Weda SubNode** is the bridge between edge devices and cloud management platforms (Weda.Core). It handles:

- **⬆️ Uplink (Data Collection)**: Collect data from edge devices, process it, and send to the cloud
- **⬇️ Downlink (Device Command)**: Receive commands from the cloud and control edge devices

### Architecture Diagrams

#### (1) Data Collection (Telemetry) - Uplink
```
                                                          OSI Layer
┌────────────────────┐          ┌────────────────────────┐
│    Edge devices    │          │      Weda.SubNode      │
│  ┌──────────────┐  │          │  ┌──────────────────┐  │
│  │   Device 1   │  │          │  │  ICommunication  │  │ ← Layer 4-7 (Transport/Application)
│  └──────────────┘  │          │  └──────────────────┘  │          ┌─────────────────────────────┐
│  ┌──────────────┐  │  IComm.  │  ┌──────────────────┐  │   NATS   │          Weda.Core          │
│  │   Device 2   │  │ ───────▶ │  │ IProtocolParser  │  │ ───────▶ │   Device Management Agent   │
│  └──────────────┘  │  (L4-7)  │  └──────────────────┘  │   (L7)   │                             │
│        ...         │          │  ┌──────────────────┐  │          └─────────────────────────────┘
│  ┌──────────────┐  │          │  │ ITransformation  │  │ ← Layer 7 (Application)
│  │   Device N   │  │          │  └──────────────────┘  │
│  └──────────────┘  │          │  ┌──────────────────┐  │
│                    │          │  │   IDspFilter     │  │ ← Layer 7 (Application)
│                    │          │  └──────────────────┘  │
└────────────────────┘          └────────────────────────┘
 Layer 1-4 (Physical-Transport)      Layer 4-7 (Transport-Application)
```

#### (2) Device Control (Command) - Downlink
```
                                                          OSI Layer
┌────────────────────┐          ┌────────────────────────┐
│    Edge devices    │          │      Weda.SubNode      │
│  ┌──────────────┐  │          │  ┌──────────────────┐  │
│  │   Device 1   │  │          │  │  ICommunication  │  │ ← Layer 4-7 (Transport/Application)
│  └──────────────┘  │          │  └──────────────────┘  │          ┌─────────────────────────────┐
│  ┌──────────────┐  │  IComm.  │  ┌──────────────────┐  │   NATS   │          Weda.Core          │
│  │   Device 2   │  │ ◀─────── │  │ IProtocolParser  │  │ ◀─────── │   Device Management Agent   │
│  └──────────────┘  │  (L4-7)  │  └──────────────────┘  │   (L7)   │                             │
│        ...         │          │                        │          └─────────────────────────────┘
│  ┌──────────────┐  │          │  ┌──────────────────┐  │
│  │   Device N   │  │          │  │  CommandHandler  │  │ ← Layer 7 (Application)
│  └──────────────┘  │          │  └──────────────────┘  │
└────────────────────┘          └────────────────────────┘
 Layer 1-4 (Physical-Transport)      Layer 4-7 (Transport-Application)

```
---

## Core Capabilities

### Layered Architecture

Weda SubNode follows the **OSI 7-layer model**, with clear separation of concerns:

```
┌─────────────────────────────────────────────────────┐
│ Weda.SubNode Internal - Layer 7 (Application)       │
│ ┌─────────────────────────────────────────────────┐ │
│ │ ITransformation - Data transformation & logic   │ │
│ │ IDspFilter - Digital signal processing          │ │
│ │ CommandHandler - Command processing             │ │
│ │ IProtocolParser - Protocol data parsing         │ │
│ └─────────────────────────────────────────────────┘ │
│                                                     │
│ ┌─────────────────────────────────────────────────┐ │
│ │ ICommunication - Unified Interface (Layer 4-7)  │ │
│ │ • Encapsulates full communication stack         │ │
│ │ • Supports Modbus / MQTT / HTTP protocols       │ │
│ │ • Handles TCP/UDP connections                   │ │
│ │ • Manages sessions and reconnection             │ │
│ └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────┐
│ OS Network Stack (Layer 1-4)                        │
│ • TCP/IP Stack                                      │
│ • Ethernet / WiFi / Serial                          │
└─────────────────────────────────────────────────────┘
```

**Key Concepts**:
- **SubNode internal** primarily operates at **Layer 7 (Application)**
- **ICommunication** encapsulates the complete **Layer 4-7** communication stack
- **IProtocolParser** parses Layer 7 protocol data into application-usable format
- **ITransformation/IDspFilter** handles application-layer business logic

---

### 📡 Communication Layer (ICommunication) - Layer 4-7
**Unified communication interface** that encapsulates the full stack (Layer 4-7):

**Supported Protocols**:
- **Modbus TCP/RTU** - Industrial standard protocol
- **MQTT** - Lightweight messaging protocol
- **HTTP/REST** - Web API integration
- **Custom Protocols** - Extensible architecture

**Encapsulated Layers**:
- **Layer 7**: Application protocol implementation (Modbus, MQTT, HTTP)
- **Layer 6**: Data encoding and decoding
- **Layer 5**: Connection management, session maintenance, auto-reconnect
- **Layer 4**: TCP/UDP transport, flow control

**Key Features**:
- Provides unified `ICommunication` interface
- Hides low-level communication complexity
- Automatically handles connection lifecycle
- Supports synchronous and asynchronous operations

### 🔄 Protocol Parser (IProtocolParser) - Layer 7
**Application-layer data processing**, converts protocol data to application format:

**Handled Protocol Data**:
- **Modbus Data** - Register values, Function Code responses
- **MQTT Messages** - Topics, Payloads
- **Custom Formats** - Extensible parsers

**Key Features**:
- Parses data passed up from protocol layer
- Converts binary to structured data (Float, Int, Bool, String...)
- Automatic Big/Little Endian handling
- Type validation and error detection
- Provides unified data model for upper layers

### 🔧 Data Transformation (ITransformation) - Layer 7
Handles **Application layer** business logic, flexible data processing pipeline:
- **Formula Transformation** - `(x * 10) + 5`
- **Unit Conversion** - Celsius to Fahrenheit
- **Data Mapping** - Rename fields
- **Conditional Logic** - if-then-else rules

**Key Features**:
- Complex mathematical operations
- Dynamic expression evaluation
- Pipeline processing (chain multiple transformations)

### 📊 Digital Filtering (IDspFilter) - Layer 7
Handles **Application layer** signal processing, built-in DSP:
- **Moving Average** - Smooth noisy data
- **Low-pass Filter** - Remove high-frequency noise
- **Anomaly Detection** - Identify outliers
- **Data Buffering** - Batch transmission

**Key Features**:
- Real-time signal processing algorithms
- Configurable filter parameters
- Reduce cloud transmission volume

### ☁️ Cloud Connection (NATS) - Layer 7
Handles **Application layer** cloud communication, high-performance bidirectional:
- **Lightweight** - Low latency, low resource usage
- **Reliable** - Auto-reconnect and message acknowledgment
- **Secure** - TLS encryption and authentication
- **Bidirectional** - Telemetry upload & Command delivery

**Key Features**:
- Pub/Sub messaging pattern
- Request/Reply synchronous communication
- JetStream persistent storage

### ⚙️ Device Management
Complete device lifecycle management:
- **Auto-initialization** - Connection and configuration checks
- **Health Monitoring** - Periodic status reporting
- **Error Retry** - Automatic reconnection on failure
- **Circuit Breaker** - Prevent cascading failures

---

## Use Cases

### 🏭 Industrial IoT (IIoT)
- Collect factory equipment data
- Real-time production line monitoring
- Remote equipment control

### 🏢 Smart Buildings
- BMS system integration
- Energy monitoring and management
- HVAC system control

### 🌾 Smart Agriculture
- Environmental sensor data collection
- Automated irrigation control
- Greenhouse environment monitoring

### 🔌 Energy Management
- Power monitoring and analysis
- Smart meter integration
- Renewable energy monitoring

---

## Why Choose Weda SubNode SDK?

### ✅ Rapid Development
- **3 minutes** to create your first project
- **Built-in templates** for quick start
- **Automated configuration** reduces boilerplate

### ✅ Industrial-Grade Stability
- **Auto-reconnect** on disconnection
- **Error handling** with comprehensive retry mechanisms
- **Memory management** prevents leaks

### ✅ Highly Flexible
- **Extensible architecture** for custom protocols and logic
- **Multiple templates** for different development styles
- **Configuration-driven** adjust without code changes

### ✅ Modern Technology
- **.NET 9.0** latest framework
- **Async programming** high-performance processing
- **Dependency injection** easy to test and maintain

---

## Next Steps

Ready to get started? Let's begin with installation:

**[→ Install Templates and Start Building](01_install_templates.md)**

**Recommended Learning Path**:
- **Beginners**: Start with wedabuilder (Web API style)
- **Single device dev/debug**: Use subnode (Console App style)

---

**Version**: 1.0.0
**Last Updated**: 2025-11-07
**Maintainer**: Rain Hu
