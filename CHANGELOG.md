# Changelog

All notable changes to the Weda SubNode SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `IDeviceRegistry` for cross-device discovery and communication
- `GetDevice`/`FindDevice` methods on `IWedaApplicationContext`
- `GetSensor`/`FindSensor`/`GetSensorByResourceId`/`FindSensorByResourceId` methods on `IDevice`
- `ConfigureConnectionPolicy()` method on `WedaApplicationBuilder`
- Protocol parser interfaces: `IProtocolParserCore`, `IPublishSubscribeProtocolParser`, `IRequestResponseProtocolParser`
- `ISensingPubSubParser` for ISensing MQTT publish/subscribe protocol
- `ModbusRequestResponseParser` for Modbus request/response protocol

### Changed
- Connection retry policy now configurable via `ConnectionOptions`
- `ConnectionOptions` extended with `MaxRetryDelayMs` and `WithRetries()` factory method
- Refactored `IProtocolParser` into specialized interfaces for different communication patterns

### Removed
- Obsolete integration tests (`Wise4012SeDeviceIntegrationTests`, `MqttISensingIntegrationTests`)
- Unused `DefaultPollingIntervalMs` from `DeviceOptions`

## [0.0.1] - 2025-11-17

### Added

#### Core Framework
- `IWedaApplicationContext` - Central application context interface
- `WedaApplicationContext` - Default implementation with NATS cloud service
- `WedaApplicationBuilder` - Fluent builder pattern for application configuration
- `WedaApplication.CreateBuilder()` and `CreateDefaultBuilder()` factory methods

#### Device Abstraction
- `IDevice` interface for device abstraction
- `DeviceBase` base class with common device functionality
- `DeviceConfiguration` for device settings from appsettings.json
- `DeviceCapabilities` for device metadata (manufacturer, model, version)
- `Sensor` class with transform and DSP pipeline support

#### Modbus Support
- `TcpModbusDevice` for Modbus TCP communication
- `ModbusDevice` base class for Modbus protocol handling
- Batch reading optimization for consecutive registers
- Auto-batch algorithm with configurable `MaxGapSize` and `MaxBatchSize`

#### ISensing MQTT Support
- `MqttISensingDevice` for ISensing MQTT communication
- `ISensingDevice` base class for ISensing protocol handling
- Publish/subscribe pattern for real-time sensor data

#### Cloud Integration
- NATS-based cloud service (`WedaCloudService`)
- Telemetry upload functionality
- Device registration with Weda.Core
- Mock cloud service (`MockCloudService`) for development/testing

#### Configuration
- `appsettings.json` based configuration
- Serilog logging integration
- NATS connection settings
- Device configuration with sensors, transforms, and DSP filters

#### Data Processing
- Transform pipeline (Calibration, UnitConversion)
- DSP filter pipeline (MovingAverage, Kalman, LowPass, HighPass)
- Threshold monitoring (UpperCritical, UpperWarning, LowerWarning, LowerCritical)

#### Connection Management
- Polly-based resilience policies
- Configurable retry strategies (AlwaysRetry default)
- Connection state management
- Auto-reconnection support

#### Templates
- `SubNodeTemplate` project template for quick start
- Example projects (WISE-4012 builder pattern)

#### Documentation
- Comprehensive wiki documentation (English and Chinese)
- Quick start guides
- API reference
- Best practices

### Infrastructure
- DevContainer support for development
- Centralized DTDL management
- Solution restructuring for better organization

---

[Unreleased]: https://dev.azure.com/AIM-IIoT/EdgeSync/_git/edge_subnode/branchCompare?baseVersion=GTv0.0.1&targetVersion=GBfeature/phase-2
[0.0.1]: https://dev.azure.com/AIM-IIoT/EdgeSync/_git/edge_subnode/branchCompare?baseVersion=GTa19787d&targetVersion=GTv0.0.1
