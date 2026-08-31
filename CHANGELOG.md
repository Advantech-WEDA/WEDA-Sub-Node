# Changelog

All notable changes to the Weda SubNode SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Change-triggered configuration reporting — `ISubNodeManager.TriggerConfigSync()` requests an immediate publish of the reported device configuration. Call it after changing a device configuration locally (outside a cloud-desired update) so the cloud reflects the change without waiting for the next periodic sync. Concurrent calls coalesce into a single publish. Added as a default no-op interface member so it does not source-break external `ISubNodeManager` implementers.
- Reconnect republish — `IWedaCloudService.ConnectionRestored` fires when the underlying connection is re-established after a drop (never on the initial connect). `SubNodeManager` subscribes and republishes the reported configuration so state changed while offline reaches the cloud immediately. `MockCloudService.SimulateConnectionRestoredAsync()` raises the event for offline/standalone testing.
- SubNode liveness heartbeat — a SubNode can report that it is alive so the platform can derive `Connected` / `Abnormal` / `Disconnected` from when the last beat arrived. Enabled by declaring one reserved sensor named `hb` in `devicecfg.json`; no `Program.cs` or `systemcfg.json` change. **Disabled by default** — a SubNode that declares no such sensor emits nothing, so upgrading the SDK never starts a heartbeat on its own. See [Liveness Heartbeat](./docs/wiki/en/04-sensor-configuration/heartbeat.md).
  - The beat is synthesised by the SDK and never polled, so it works identically on Modbus / OPC UA / MQTT / custom devices; it needs no `Parameters`, and `GroupSensorsByInterval` excludes it from every device's read path.
  - Sent on the SubNode DeviceId, so one SubNode emits one liveness signal regardless of device count.
  - Declared as a real sensor, so it receives a `ResourceId` and appears in the generated DTDL, the capability upload and the device-management sensor registry — enriched telemetry resolves it instead of falling back to `"unknown"`.
  - The DTMI (`dtmi:com:advantech:weda:Heartbeat;1`) and the `boolean` schema are stamped by the SDK, not configured. `devicecfg.json` declares a name and an interval; the DTDL is auto-generated like any other sensor's.
  - Because typed dispatch skips the heartbeat, it never enters the sensor-type registry — so it contributes its own Interface to `refModelsMap.configs`, extending `Sensor:base` and carrying no `Parameters`. Without it the sensor's DTMI would resolve to nothing in the uploaded catalog.
  - Identified by that DTMI, which the enrichment stage already resolves for every measure. Note this is a post-enrichment match; a marker cannot travel on the raw message (see Fixed, below).
  - `ARMED` and `CONFIRMED` are logged once per process at `Debug` — the pair separates a working heartbeat from one running but silently failing to publish. `DISABLED` is a **warning**: a SubNode publishing no liveness signal reads as `Disconnected` with nothing else indicating why, so that direction stays visible under the default `Warning` minimum.
  - Default cadence `T` = 60 s, clamped to a 1 s floor. Transport faults are logged and retried on the next beat rather than tearing down the loop.

### Changed
- Configuration sync default period raised from **60s to 24h** (`BackgroundTaskPeriods.DefaultReportConfigurationPeriod`); allowed range narrowed from 10s–7d to **60s–7d** (`MinReportConfigurationPeriod` 10000 → 60000). Steady-state devicecfg is now driven primarily by change/reconnect triggers rather than a fast periodic tick.
- Out-of-range `Periods.ReportConfiguration` is now **clamped to the nearest bound** (previously replaced with the default), preserving operator intent; `0` remains an explicit opt-out (periodic sync off) and now logs a warning instead of being applied silently.
- **Auto-gen-only DTMI policy is now absolute.** No `devicecfg.json` sensor entry carries a `Dtmi`, the heartbeat included — an earlier iteration made it an exception, which meant deleting one line silently disabled liveness. Identity is the reserved sensor name `hb`, and the SDK stamps the DTMI itself. A sensor named `hb` is reserved platform-wide and must not be used for application data.

### Fixed
- Heartbeat measures no longer set `TelemetryMeasure.Metadata`. That field is the framework's chunked-transfer descriptor, and the WedaNode telemetry proxy rejects the **entire message** when metadata is present without a `transferId` — so every beat was discarded before it reached the cloud, leaving a healthy SubNode reading as `Disconnected`. The same defect previously affected the CFX and vision paths.
- Sensor configuration reference documented `Record.Enabled` as defaulting to `false` and listed a non-existent `Path` property; the default is `true` and the second property is `Interval`.

### Installation and Upgrade Instructions
- If your device mutates its own configuration at runtime (sensor re-resolution, calibration writes, `RawDeviceCfgJson` refresh) and relied on the old 60s periodic tick to propagate it, call `ISubNodeManager.TriggerConfigSync()` at those change points — with the 24h default, an untriggered local change can otherwise lag up to a day.
- Any `Periods.ReportConfiguration` below 60000ms is now clamped to 60000ms (the old 10000ms floor no longer applies). Set the value explicitly if you depend on a specific cadence.

## [1.2.0] - 2026-06-02

### Added
- DTDL v3 capability publishing — `DeviceCapDto` now ships full DTDL v3 Interfaces alongside `devices[]` + `sensorTypes[]` catalogs, replacing the legacy JSON Schema payload. Cloud and UI can consume value-level constraints (minimum / maximum / pattern / required / default) as first-class extension fields, so every SubNode capability can be inspected remotely from a single upload.
- Strong-typed POCO capability registration:
  - `IConfigurableDevice<TComm, TProps>` for device communication + protocol-specific settings.
  - `IConfigurableSensor<TParameter>` for per-sensor parameter POCOs.
  - `IConfigurableTransform<TInput, TParameter>` and `IConfigurableDspFilter<TInput, TParameter>` migrated to strongly-typed two-arg generics; `ValidateParameters` is now a default interface method running DataAnnotations + `IValidatableObject`.
  - `ICommand<TParameter>` surfaces both parameter and response schemas through `CommandRegistry.GetDescriptors()`.
- Typed device/sensor dispatch — `SensorTypeRegistry` / `DeviceTypeRegistry` assembly scan POCO descriptors and resolve sensors by try-validating the `Parameters` POCO. `devicecfg.json` gains no new fields; discriminator encoded by single-value enum on a Parameters property when shapes naturally collide.
- Weda.Dtdl 0.0.3 package — DTDL emitter, `ConfigConstraint` extension and `WedaDtdlValidator` (DTDLParser-based) extracted to a standalone package; bundled via local NuGet feed under `./nuget/`.
- Step 0 DTDL runtime validation in `CommandDispatcher` (gated by `DtdlValidationOptions.Enabled`, default `false`); validators built eagerly at registration so DTDL bugs fail fast at startup.
- Modbus reference POCOs: `TcpCommunicationSettings`, `ModbusProperties`, `ModbusSensorParameters` annotated with `[Required]` / `[Range]` / `[Display]` / `[Description]`.
- `Weda.SubNode.CapabilityDump` (capdump) tool — preview the capability upload payload for any example without NATS.
- E2E NATS shadow test (Testcontainers) verifying every emitted Transform / DspFilter / Command DTDL Interface constructs a `WedaDtdlValidator` without throwing.

### Changed
- Auto-gen-only DTMI policy — DTMI assignment is now solely the SubNode SDK's responsibility. `devicecfg.json` sensor entries must not contain a `Dtmi` field or `DtdlPath` override; cloud-supplied DTMI values are ignored.
- Sensor instances now reference their **sensor-type** DTMI (shared across instances); identity moves to `ResourceId`.
- system-agent migrated to typed POCO design — 13 typed sensor families (cpu / memory / system / gpu / hwinfo / voltage / fanspeed / watchdog / thermalprotection / disk / network / gpio / temperature); 33/33 sensors resolve to typed sensor-type DTMIs.
- All 9 examples migrated to typed dispatch (60/60 sensor instances bound to a registered sensor-type DTMI).
- `HasDtmiDelta` → `RequiresCapsReupload` on `ConfigUpdateResult` / `ConfigUpdateValidationResult`; DeviceCaps re-upload still triggers when new sensors appear.
- `DeviceConfigurationMappingExtensions` moved from Abstractions to Core to pull descriptors from the capability registries; accepts an optional `CommandRegistry`.

### Installation and Upgrade Instructions
- Remove `Dtmi` fields and `DtdlPath` overrides from all `devicecfg.json` sensor entries; replace with the canonical `"Dtdl": { "AutoGenEnabled": true }` shape.
- Custom device authors should adopt `IConfigurableDevice<TComm, TProps>` + `IConfigurableSensor<TParameter>` — see `docs/customized-device-design-guideline.md` and `docs/refactor/typed-poco-dtdl-emit.md`.
- The bundled `nuget/Weda.Dtdl.0.0.3.nupkg` is now tracked in the repo; `nuget.config` declares the local feed via Package Source Mapping. Until `Weda.Dtdl` ships to NuGet.org, do not delete `./nuget/` or `nuget.config`.

## [1.1.0] - 2026-05-21

### Added
- OPC UA protocol support — `OpcUaCommunication`, `OpcUaPubSubDevice`, `OpcUaRequestResponseDevice`, simulator and example project with TCP transport.
- Modbus RTU support — `ModbusRtuCommunication` with CRC16 validation and serial transport.
- `SerialCommunicationFactory` — shared serial port instance across multiple devices on the same port; half-duplex behavior covered by unit tests.
- AI / AO / DI / DO commands — getter and setter implementations for `ISensingDevice` with payload schema.
- Standardized command status codes across all command handlers.
- Transform / DSP filter / command capability upload — `SubNodeCapabilitiesDto` published alongside device configuration carrying JSON Schema descriptors emitted from `[Required]` / `[Range]` / DataAnnotations-annotated parameter POCOs. `IConfigurableTransform<TInput, TParameter>` and `IConfigurableDspFilter<TInput, TParameter>` migrated to strongly-typed two-arg generics; `ValidateParameters` runs DataAnnotations + `IValidatableObject` automatically. `CommandRegistry` emits both parameter and response schemas through `GetDescriptors()`.
- system-agent — `system` command (reboot / shutdown) with `nsenter` + libc `reboot()` fallback paths.
- system-agent DTDL metrics — per-sensor `devicecfg` DTDL models for cpu / memory / system / gpu / hwinfo / disk / network / gpio / temperature / voltage / fanspeed / watchdog / thermalprotection; sample configs and `dtdl-validate` tooling shipped as an independent project.
- GPIO `pinState` integer-only validation rules + DTDL `GpioSensorParameters` schema.
- Device discovery — `DiscoverAvailableResources`, auto-sensor scan and list-sensor flow.
- SensorExpander — per-sensor expansion of physical resources (CPU cores, network interfaces, GPIO pins) into individual sensor entries.
- NATS web dashboard tool and updated `subnode` skill scaffolding.
- ROS 2 bridge wiki documentation and ROS 2 learning notes for robot integration.
- v1.0.0 / v1.1.0 design documentation; image-sensor example README.
- image-sensor example — exposed WedaNode auth and Record env vars in `docker-compose`; switched to prebuilt multi-arch image with `build.sh`.

### Changed
- Advantech.Edge upgraded to 1.1.2 → 1.1.3.
- ABP central package management enabled (`<ManagePackageVersionsCentrally>`).
- Removed the standalone .NET `ValidateDtdl` project (superseded by `dtdl-validate`).
- Devices rearranged into a dedicated protocol layer; legacy OPC UA device classes removed after restructuring.
- Disabled the `IsPhysicalOrLogicalInterface` heuristic; SensorExpander reworked for readability and no longer writes the expand result back into `RawDeviceCfgJson`.
- Documentation language convention — English treated as the default; unified terminology (no more "Sensor Expansion" / "SensorExpander").

### Fixed
- Concurrent reconnect race across interval groups — serialized reconnect with `SemaphoreSlim` + double-check after lock acquisition; non-`Connected` states uniformly treated as "not ready".
- Caller cancellation no longer flips `TcpCommunication` state to `Error` during shutdown / config update (genuine IO failures still flip to `Error`).
- `ModbusTcpCommunication` / `ModbusRtuCommunication` now subscribe to inner transport `StateChanged` events and mirror state changes (remote close, IO error) onto the wrapper.
- Cancellation noise silenced in device read paths during shutdown / config update.
- AI / AO / DI / DO connection-state bugs.
- Check `advantechEdgeDevice` `InitializationFailed` state early.
- nats-cli download link 404 in Dockerfile.

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

[1.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[1.1.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[1.0.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[0.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4338/Sub-Node-SDK
