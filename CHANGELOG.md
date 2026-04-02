# Changelog

All notable changes to the Weda SubNode SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-05

### Added
- Modbus DO command support
- Global Serilog logger configuration
- air-quality-monitor example
- `report.data` command
- Dynamic record storage system with binary index and documentation
- JSON payload files in examples
- SubNode MimeType support (application/json & image/png, jpeg)
- ImageSensor example
- ImagePubSubParser and MqttImageDevice implementation
- Image protocol parser with design docs for large data handling and dynamic recording
- Photo simulator using MNIST dataset
- Chunking for large size image and chunkingTransform for custom chunking
- Route image topics per sensor
- CRC32 support
- MIME type validation for sensor telemetry data
- MIME type schema whitelist restriction
- Validation metrics log
- Unit test for json adapter for TelemetryMeasureDto

### Changed
- Replace dots with underscores in sensor names to comply with IoT DB naming rule
- Rename image-sensor example
- Rename checksum to crc32Checksum
- Naming corrections

### Fixed
- Command interface change error
- Auto value object serialization for dynamic storage
- Correct air-quality model from array to object
- Testing compile errors
- Receiver using transferId instead of imageId
- Chunked telemetry issue
- Batch report request payload
- Stop background tasks before config update to prevent race conditions
- NOT_REGISTERED issue
- Sensor name validation with IoT-DB naming rule

## [0.2.0] - 2026-02-02

### Added
- Support remote shadow configuration sync
- Introduce Sub Node Manager
- SubNode Sensor Recording

### Fixed
- Resolved Sub Node Registration Issue

### Installation and Upgrade Instructions
- appsettings configuration interface changed
- Please remove the sub node metadata before launching

---

[1.0.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[0.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4338/Sub-Node-SDK
