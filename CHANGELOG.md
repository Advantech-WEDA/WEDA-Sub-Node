# Changelog

- **system-agent `cpu_usage` reports current usage, not the average since boot.** It divided cumulative `/proc/stat` counters from a single read, so after a day of uptime the value barely moved under full load. It is now the share of non-idle time over the window between two samples of the aggregate `cpu` line (which also survives CPU hot-plug, unlike the per-core sum it replaced), with iowait counted as idle. A failed read now reaches the retry policy and marks the cpu metrics as failed instead of publishing `0`; platforms without a usage source (Windows, macOS) publish nothing instead of a constant `0`. Load averages are parsed culture-invariantly.
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
  - `ARMED` and `CONFIRMED` are logged at `Debug` each time the beat starts — at startup and after a resume — the pair separates a working heartbeat from one running but silently failing to publish. `DISABLED` is a **warning**: a SubNode publishing no liveness signal reads as `Disconnected` with nothing else indicating why, so that direction stays visible under the default `Warning` minimum.
  - Default cadence `T` = 60 s, clamped to a 1 s floor. Transport faults are logged and retried on the next beat rather than tearing down the loop.
- Scheduled recording cleanup — `RecordingCleanupScheduler` runs retention and the ring-buffer FIFO once at startup and then every `Record.CleanupIntervalMinutes` (default **60**). It reports every sweep with the cutoff, files scanned, files deleted, bytes freed and files that could not be deleted, so "swept and found nothing" is distinguishable from "never swept". A sweep that could not delete something is reported at `Warning`, the rest at `Information` — devices ship with a `Warning` minimum level, so the one outcome that means storage keeps growing must not be filtered out.
- `Record.CleanupIntervalMinutes` (0-1440, default 60) — how often cleanup sweeps. `0` disables scheduled cleanup, leaving storage to shrink only while data is being written.
- `Record.StorageDirectory` is now a real configurable property. It was a `const`, so the value could never be set and `RecordingOptions.Validate()`'s emptiness check was dead code. Default is unchanged (`./.weda/data/recording`); the constant survives as `RecordingOptions.DefaultStorageDirectory`.
- `IRecordingService.StorageSettings` / `UpdateStorageSettings(RecordingStorageSettings)` and `IRecordStorage.EnforceStorageLimitsAsync()` — retention and the ring-buffer limits can now be changed at runtime, and the size cap can be enforced without a write in flight.
- **Remotely configurable recording** — recording settings now live in `devicecfg.json` under a new SubNode-level `SubNodeSettings.Record` section (parallel to `SubNode` / `DeviceConfigs`), and are configurable from the cloud through the DeviceConfig desired pipeline. A pushed record is validated, applied as a first-class participant in the all-or-nothing device-config transaction (so an invalid record rejects the whole update, and a device failure rolls the record back with the devices), cached, and re-applied on restart. `IRecordingService` gains `CaptureForRollback` / `ApplyRecordConfig` / `RestoreFromRollback` for that transaction participation. See [`docs/sample-devicecfg.json`](./docs/sample-devicecfg.json).

### Changed
- Configuration sync default period raised from **60s to 24h** (`BackgroundTaskPeriods.DefaultReportConfigurationPeriod`); allowed range narrowed from 10s–7d to **60s–7d** (`MinReportConfigurationPeriod` 10000 → 60000). Steady-state devicecfg is now driven primarily by change/reconnect triggers rather than a fast periodic tick.
- Out-of-range `Periods.ReportConfiguration` is now **clamped to the nearest bound** (previously replaced with the default), preserving operator intent; `0` remains an explicit opt-out (periodic sync off) and now logs a warning instead of being applied silently.
- **Auto-gen-only DTMI policy is now absolute.** No `devicecfg.json` sensor entry carries a `Dtmi`, the heartbeat included — an earlier iteration made it an exception, which meant deleting one line silently disabled liveness. Identity is the reserved sensor name `hb`, and the SDK stamps the DTMI itself. A sensor named `hb` is reserved platform-wide and must not be used for application data.
- **Per-sensor `Record.Interval` handling reworked; its treatment now depends on `Record.Enabled`.** `Sensors[].Record.Interval` becomes nullable. **When recording is enabled it is required and must already be at least `1000` ms** — a null, `0`, or sub-floor value is rejected at validation; the SDK does not guess an interval for an enabled record. **When recording is disabled it is optional** — if omitted, `0`, or sub-floor, the apply path derives it from the sensor's report interval, clamped up to the `1000` ms record floor (the report interval floor is lowered to `50` ms). The resolved, in-range value **is reported back to the cloud**: the edge patches it into the reported devicecfg (and the fallback DTO report path maps it too), because the cloud validator range-checks the record interval regardless of the enabled flag — echoing the raw desired value would otherwise be rejected on the way back up. **BREAKING:** `Record.Enabled` now defaults to `false` — a sensor that declared no record config, or a record without an explicit `enabled` flag, previously recorded by default and now does not. DTDL: report interval `minimum` lowered `1000` → `50`; record interval `minimum` stays `1000`.
- **BREAKING: an enabled record with a missing, `0`, or sub-floor `Interval` is now rejected.** `{ "Record": { "Enabled": true, "Interval": 0 } }` previously meant "record at the report interval" (as documented in the configuration reference) and was applied silently; `{ "Enabled": true }` with no interval was also accepted. Both are now rejected: an enabled record must declare an interval of at least `1000` ms, e.g. `{ "Enabled": true, "Interval": 1000 }`. The report-interval fallback is no longer available for an *enabled* record — it survives only for a *disabled* record, which may still omit the interval or send `0` and have the SDK derive it from the report interval.
- **The heartbeat sensor now carries `"Parameters": {}`.** `Heartbeat.ApplyReservedContract` stamps an empty object (discarding, with a warning, any configured parameters), the example `devicecfg.json` files declare it, and the heartbeat DTDL Interface now declares `Parameters` bound to a field-less Object (`dtmi:com:advantech:weda:Heartbeat:Parameters;1`). Without that declaration the cloud's strict (v2) validator rejects `parameters` as an unknown field; with it, `{}` (or an omitted key) passes and any actual parameter is still rejected.

### Fixed
- Heartbeat measures no longer set `TelemetryMeasure.Metadata`. That field is the framework's chunked-transfer descriptor, and the WedaNode telemetry proxy rejects the **entire message** when metadata is present without a `transferId` — so every beat was discarded before it reached the cloud, leaving a healthy SubNode reading as `Disconnected`. The same defect previously affected the CFX and vision paths.
- Sensor configuration reference listed a non-existent `Record.Path` property; the second property is `Interval`. (`Record.Enabled` now defaults to `false` — see Changed.)
- `Record.MinFreeDiskSpaceMb` and `Record.MaxStorageSizeMb` are now documented as the distinct limits they are — the first a floor on free space for the whole *drive*, the second a cap on the recording *directory*. Defaults are unchanged (128 MB and 1024 MB); the shared 128 figure made the first routinely read as a 128 MB cap on recordings.
- `IRecordStorage.CleanupAsync` and `IRecordingService.CleanupAsync` return `RecordingCleanupResult` (files scanned / deleted, bytes freed, files failed) instead of `Task`. Source-breaking for external implementers of those interfaces.
- Recording options are read through a shared `RecordingOptionsProvider` rather than a captured `IOptions<RecordingOptions>.Value`, so one update reaches storage, the recording service and the cleanup scheduler together. Updates publish a new options instance; the existing one is never mutated.
- **The heartbeat resumes when its sensor or device is enabled again.** `HeartbeatHostedService` resolved the `hb` sensor once at startup and stopped for good the first time it found the sensor disabled, so turning the owning device off and on again — for example through telemetry-configs — left a healthy SubNode with no heartbeat, reading as `Disconnected`, until the process restarted. A SubNode that started with the sensor disabled, or without one, never began beating. The new `HeartbeatLoop` re-reads configuration on every pass — once a second while not beating, and before every beat — so the beat starts again within about a second of the sensor becoming enabled, follows a sensor rebuilt by a cloud update, and picks up a changed interval. Resuming logs `liveness reporting resumed` at `Information`, and a sensor that is declared but disabled is now reported as such instead of as missing. A configuration read that fails while devices are being updated is logged and retried instead of ending the heartbeat.

### Removed
- **BREAKING — recording is no longer configured in `systemcfg.json`.** `systemcfg` is policy-locked as non-remote-configurable; recording moved to `devicecfg.json` `SubNodeSettings.Record`. Removed: the `Record` apply path in `HandleSystemConfigUpdateAsync`, the `SubNodeSystemCfgDto.Record` wire field, and the `RecordValidator`. A `Record` section left in `systemcfg.json` is now ignored and logs a startup warning.
- **BREAKING — `WedaContextOptions.RecordingOptions` is removed.** Providing recording options in code fought the remote-configurable model — it would override a cloud-pushed value on the next start. Recording is configured solely through `devicecfg.json` `SubNodeSettings.Record`.

#### Installation and Upgrade Instructions
- Move the `Record` block out of each `systemcfg.json` into the matching `devicecfg.json` under a new top-level `SubNodeSettings` object: `{ "SubNodeSettings": { "Record": { … } } }`, placed alongside `SubNode` and `DeviceConfigs`. See [`docs/sample-devicecfg.json`](./docs/sample-devicecfg.json). Leaving `Record` in `systemcfg.json` is not an error but has no effect (a startup warning names the file).
- If you set `WedaContextOptions.RecordingOptions` in code, remove that assignment and put the values in `devicecfg.json` `SubNodeSettings.Record` instead.

### Fixed
- Heartbeat measures no longer set `TelemetryMeasure.Metadata`. That field is the framework's chunked-transfer descriptor, and the WedaNode telemetry proxy rejects the **entire message** when metadata is present without a `transferId` — so every beat was discarded before it reached the cloud, leaving a healthy SubNode reading as `Disconnected`. The same defect previously affected the CFX and vision paths.
- Sensor configuration reference documented `Record.Enabled` as defaulting to `false` and listed a non-existent `Path` property; the default is `true` and the second property is `Interval`.
- **Recordings past `RetentionDays` were not deleted** (work item 53692). Cleanup was scheduled with `dueTime: Zero, period: 1 day`, so it ran once at process start and then only every 24 hours of *uptime*: files arriving after the startup sweep waited up to a full extra day, and a device restarting more often than once a day never ran anything but the startup sweep — which sees only what was on disk before boot. Cleanup now sweeps hourly by default, and the schedule is covered by tests (`RecordingCleanupSchedulerTests`), which the previous timer was not.
- **Cloud-pushed `Record` settings were silently dropped.** `SubNodeManager` applied only `Enabled`, `BatchEnabled` and `BatchMaxSamples`; `RetentionDays`, `MaxStorageSizeMb` and `MinFreeDiskSpaceMb` travelled in the same message, passed `RecordValidator`, were acknowledged — and then discarded. All three are now applied, and take effect on the next write and the next sweep without a restart.
- **The ring-buffer size cap was only enforced while writing.** `EnsureDiskSpace` ran inside `WriteAsync` alone, so a device whose sensors stopped recording (or whose recording was switched off) stayed over the limit indefinitely. The scheduled sweep now enforces it too.
- **MIME-typed recordings never aged out.** `DynamicRecordStorage.CleanupAsync` existed but nothing called it, so recorded JSON and images accumulated without limit. The sweep now applies the same cutoff to both storages.
- **One undeletable recording aborted the whole cleanup pass.** `BinaryRecordStorage.CleanupAsync` called `File.Delete` unguarded and the only handler was at the top of the timer callback, so a single locked or permission-denied file left every remaining sensor unswept. Failures are now logged per file, counted in `RecordingCleanupResult.FilesFailed`, and the pass continues.
- `DynamicRecordStorage` was constructed with an unresolved relative path while `BinaryRecordStorage` resolved the same setting through `PathHelper`, so the two could write to different directories outside a container. Both now resolve identically.

### Installation and Upgrade Instructions
- If your device mutates its own configuration at runtime (sensor re-resolution, calibration writes, `RawDeviceCfgJson` refresh) and relied on the old 60s periodic tick to propagate it, call `ISubNodeManager.TriggerConfigSync()` at those change points — with the 24h default, an untriggered local change can otherwise lag up to a day.
- Any `Periods.ReportConfiguration` below 60000ms is now clamped to 60000ms (the old 10000ms floor no longer applies). Set the value explicitly if you depend on a specific cadence.
- `Record.Enabled` now defaults to `false`. If any sensor relied on the previous default to record without an explicit flag, add `"Record": { "Enabled": true, "Interval": <ms> }` to that sensor in `devicecfg.json`. For an enabled record the interval is now **required and must be at least `1000` ms**.
- If any sensor used `"Record": { "Enabled": true, "Interval": 0 }` (or an enabled record with no interval, or a sub-floor value) to mean "record at the report interval", replace it with an explicit in-range value, e.g. `"Interval": 1000`. An enabled record is now validated and will be rejected otherwise. The report-interval fallback remains only for *disabled* records.
- External implementers of `IRecordStorage` or `IRecordingService` must update `CleanupAsync` to return `RecordingCleanupResult` and implement `EnforceStorageLimitsAsync`, `StorageSettings` and `UpdateStorageSettings`.

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
