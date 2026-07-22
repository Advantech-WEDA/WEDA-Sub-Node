# 更新日誌

Weda SubNode SDK 的所有重要變更都將記錄在此文件中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，
本專案遵循 [語意化版本](https://semver.org/lang/zh-TW/)。

## [1.2.0] - 2026-06-02

### 新增
- DTDL v3 capability 發佈管線 — `DeviceCapDto` 改以完整的 DTDL v3 Interface 上傳，並附帶 `devices[]` 與 `sensorTypes[]` 兩層 catalog，取代舊版 JSON Schema payload。雲端與 UI 可直接消費值域層級約束 (minimum / maximum / pattern / required / default) 作為一級擴充欄位，遠端可單次取得並檢視 SubNode 所有能力的 schema。
- 強型別 POCO 能力註冊介面：
  - `IConfigurableDevice<TComm, TProps>`：裝置通訊設定與協定屬性。
  - `IConfigurableSensor<TParameter>`：每個 sensor 的參數 POCO。
  - `IConfigurableTransform<TInput, TParameter>` 與 `IConfigurableDspFilter<TInput, TParameter>` 改為強型別雙泛型；`ValidateParameters` 改為預設介面方法，自動執行 DataAnnotations 與 `IValidatableObject` 驗證。
  - `ICommand<TParameter>` 同時透過 `CommandRegistry.GetDescriptors()` 揭露參數與回應的 schema。
- 強型別 Device / Sensor dispatch — `SensorTypeRegistry` / `DeviceTypeRegistry` 透過 assembly scan 收集 POCO descriptor，並以 try-validate `Parameters` POCO 的方式解析 sensor。`devicecfg.json` 維持原 schema 不變；當 Parameters shape 自然衝突時，以單值 enum 在 Parameters 屬性上做 discriminator。
- Weda.Dtdl 0.0.3 套件 — DTDL emitter、`ConfigConstraint` 擴充與 `WedaDtdlValidator`（基於 DTDLParser）抽離為獨立套件；以 local NuGet feed (`./nuget/`) 隨 repo 一同攜帶。
- `CommandDispatcher` Step 0 DTDL 執行期驗證（由 `DtdlValidationOptions.Enabled` 控制，預設 `false`）；validator 於註冊期就建好，DTDL 寫錯會在 startup 直接 fail fast。
- Modbus 參考 POCO：`TcpCommunicationSettings`、`ModbusProperties`、`ModbusSensorParameters`，附 `[Required]`、`[Range]`、`[Display]`、`[Description]` 註記。
- `Weda.SubNode.CapabilityDump` (capdump) 工具 — 不需啟動 NATS 即可預覽任一 example 的 capability upload payload。
- NATS E2E shadow 測試（Testcontainers）— 驗證所有 emit 出來的 Transform / DspFilter / Command DTDL Interface 都能成功被 `WedaDtdlValidator` 解析。

### 變更
- Auto-gen-only DTMI 政策 — DTMI 完全由 SubNode SDK 指派。`devicecfg.json` sensor 條目不得包含 `Dtmi` 欄位或 `DtdlPath` override；雲端送來的 DTMI 將被忽略。
- Sensor 實例改為參考其 **sensor-type** DTMI（同類型 sensor 共用）；實例身份由 `ResourceId` 攜帶。
- system-agent 遷移至強型別 POCO 設計 — 13 個 sensor family（cpu / memory / system / gpu / hwinfo / voltage / fanspeed / watchdog / thermalprotection / disk / network / gpio / temperature），33/33 sensor 全部解析為強型別 sensor-type DTMI。
- 全部 9 個 example 遷移至強型別 dispatch（60/60 sensor 實例均綁定到註冊過的 sensor-type DTMI）。
- `ConfigUpdateResult` / `ConfigUpdateValidationResult` 的 `HasDtmiDelta` 改名為 `RequiresCapsReupload`；新增 sensor 時仍會觸發 DeviceCaps 重新上傳。
- `DeviceConfigurationMappingExtensions` 從 Abstractions 移到 Core，以便從 capability registry 取得 descriptor；可接受可選的 `CommandRegistry`。

### 安裝與升級說明
- 請移除所有 `devicecfg.json` sensor 條目中的 `Dtmi` 欄位與 `DtdlPath` override，改用標準的 `"Dtdl": { "AutoGenEnabled": true }` 寫法。
- 自訂裝置開發者請改採 `IConfigurableDevice<TComm, TProps>` + `IConfigurableSensor<TParameter>` 介面 — 參考 `docs/customized-device-design-guideline.md` 與 `docs/refactor/typed-poco-dtdl-emit.md`。
- `nuget/Weda.Dtdl.0.0.3.nupkg` 已隨 repo 一併追蹤，`nuget.config` 透過 Package Source Mapping 宣告 local feed。在 `Weda.Dtdl` 正式發佈到 NuGet.org 前，請勿移除 `./nuget/` 或 `nuget.config`。

## [1.1.0] - 2026-05-21

### 新增
- OPC UA 協定支援 — `OpcUaCommunication`、`OpcUaPubSubDevice`、`OpcUaRequestResponseDevice`，包含 TCP 傳輸的模擬器與 example 專案。
- Modbus RTU 支援 — `ModbusRtuCommunication`，含 CRC16 驗證與序列傳輸。
- `SerialCommunicationFactory` — 同一個序列埠由多個 device 共享；半雙工行為已有單元測試覆蓋。
- AI / AO / DI / DO 命令 — `ISensingDevice` 的 getter / setter 實作，附 payload schema。
- 統一命令狀態碼。
- Transform / DSP filter / command capability 上傳 — `SubNodeCapabilitiesDto` 隨裝置配置一併上傳，內含由 `[Required]` / `[Range]` / DataAnnotations 註記的參數 POCO emit 出來的 JSON Schema descriptor。`IConfigurableTransform<TInput, TParameter>` 與 `IConfigurableDspFilter<TInput, TParameter>` 改為強型別雙泛型；`ValidateParameters` 自動執行 DataAnnotations + `IValidatableObject`。`CommandRegistry` 透過 `GetDescriptors()` 同時揭露參數與回應 schema。
- system-agent — `system` 命令（reboot / shutdown），含 `nsenter` 與 libc `reboot()` fallback 路徑。
- system-agent DTDL metrics — per-sensor `devicecfg` DTDL 模型涵蓋 cpu / memory / system / gpu / hwinfo / disk / network / gpio / temperature / voltage / fanspeed / watchdog / thermalprotection；附範例設定，`dtdl-validate` 工具獨立成 project 出貨。
- GPIO `pinState` 限制整數值的驗證規則 + DTDL `GpioSensorParameters` schema。
- 裝置探索能力 — `DiscoverAvailableResources`、auto-sensor 掃描與 list-sensor 流程。
- SensorExpander — 將實體資源（CPU cores、網路介面、GPIO pins）展開為個別 sensor 條目。
- NATS web dashboard 工具，更新 `subnode` skill 樣板。
- ROS 2 bridge wiki 文件與 ROS 2 學習筆記。
- v1.0.0 / v1.1.0 設計文件；image-sensor example README。
- image-sensor example — `docker-compose` 揭露 WedaNode auth 與 Record 環境變數；改用 `build.sh` 預先建好的 multi-arch image。

### 變更
- Advantech.Edge 升級到 1.1.2 → 1.1.3。
- 啟用 ABP 中央套件管理（`<ManagePackageVersionsCentrally>`）。
- 移除獨立的 .NET `ValidateDtdl` 專案（由 `dtdl-validate` 取代）。
- 將 Device 重新整理進獨立的 protocol layer；舊的 OPC UA device 類別在結構重整後移除。
- 停用 `IsPhysicalOrLogicalInterface` 邏輯；SensorExpander 重寫以提升可讀性，不再將 expand 結果寫回 `RawDeviceCfgJson`。
- 文件慣例 — 英文視為預設語言；統一名稱（不再使用 "Sensor Expansion" / "SensorExpander" 的稱呼）。

### 修復
- 多個 interval group 同時觸發 reconnect 的競爭 — 以 `SemaphoreSlim` + 鎖後 double-check 序列化 reconnect；非 `Connected` 狀態統一視為 "not ready"。
- shutdown / config update 期間呼叫端取消不再把 `TcpCommunication` 狀態翻成 `Error`（真實 IO 失敗仍會翻 `Error`）。
- `ModbusTcpCommunication` / `ModbusRtuCommunication` 改為訂閱 inner transport 的 `StateChanged` 事件，遠端關閉或 IO 錯誤造成的內層狀態變化會即時鏡射到 wrapper。
- shutdown / config update 期間裝置讀取路徑的取消噪音 log 已靜音。
- AI / AO / DI / DO 的連線狀態 bug。
- 提前確認 `advantechEdgeDevice` 是否為 `InitializationFailed` 狀態。
- Dockerfile 中 nats-cli 下載連結 404 的問題。

## [1.0.0] - 2026-03-05

### 新增
- Modbus DO 命令支援
- 全域 Serilog 記錄器設定
- air-quality-monitor 範例
- `report.data` 命令
- 動態記錄儲存系統，包含二進位索引和文件
- 範例中的 json payload 檔案
- SubNode MimeType 支援 (application/json & image/png, jpeg)
- ImageSensor 範例
- ImagePubSubParser 和 MqttImageDevice 實作
- 影像協定解析器，包含大型資料處理和動態記錄的設計文件
- 使用 MNIST 資料集的照片模擬器
- 大型影像的分塊功能和自訂分塊的 chunkingTransform
- 每個感測器的影像主題路由
- CRC32 支援
- 感測器遙測資料的 MIME 類型驗證
- MIME 類型 schema 白名單限制
- 驗證指標日誌
- TelemetryMeasureDto 的 json adapter 單元測試

### 變更
- 感測器名稱中的點號替換為底線，以符合 IoT DB 命名規則
- 重新命名 image-sensor 範例
- checksum 重新命名為 crc32Checksum
- 命名修正

### 修復
- 命令介面變更錯誤
- 動態儲存的自動值物件序列化問題
- air-quality 模型從陣列修正為物件
- 測試編譯錯誤
- receiver 使用 transferId 而非 imageId
- 分塊遙測問題
- 批次報告請求 payload
- 在配置更新前停止背景任務，防止競爭條件
- NOT_REGISTERED 問題
- 感測器名稱驗證，符合 IoT-DB 命名規則

## [0.2.0] - 2026-02-02

### 新增
- 遠端 shadow 配置同步支援
- Sub Node Manager 導入
- SubNode Sensor Recording

### 修復
- Sub Node 註冊問題

### 安裝與升級說明
- appsettings 配置介面已變更
- 啟動前請移除 sub node metadata

---

[1.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[1.1.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[1.0.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[0.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4338/Sub-Node-SDK
