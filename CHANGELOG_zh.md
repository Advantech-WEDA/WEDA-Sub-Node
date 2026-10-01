# 更新日誌

Weda SubNode SDK 的所有重要變更都將記錄在此文件中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，
本專案遵循 [語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

### 新增
- 變更觸發的設定回報 — `ISubNodeManager.TriggerConfigSync()` 可要求立即發佈已回報的裝置設定。當你在本地(非雲端 desired 更新)變更裝置設定後呼叫它,雲端即可反映變更,不必等到下一次週期同步。並行呼叫會合併為單次發佈。此成員以 default no-op 介面方法加入,不會 source-break 既有的外部 `ISubNodeManager` 實作者。
- 重連後重新發佈 — `IWedaCloudService.ConnectionRestored` 會在底層連線中斷後重新建立時觸發(初次連線不觸發)。`SubNodeManager` 訂閱此事件並重新發佈已回報設定,使離線期間發生的變更能立即送達雲端。`MockCloudService.SimulateConnectionRestoredAsync()` 提供離線/獨立測試時觸發該事件的入口。
- 排程式 recording 清理 — `RecordingCleanupScheduler` 會在啟動時掃一次,之後每 `Record.CleanupIntervalMinutes` 分鐘(預設 **60**)再掃一次,同時執行保留期清理與 ring-buffer FIFO。每次掃描都會記錄 cutoff、掃描檔數、刪除檔數、釋放位元組與刪除失敗檔數,讓「掃過但沒東西可刪」與「根本沒掃」能區分開來。有刪除失敗的掃描以 `Warning` 記錄,其餘以 `Information` — 裝置出廠的 Serilog 最低層級是 Warning,而「儲存會持續成長」正是唯一不能被濾掉的結果。
- `Record.CleanupIntervalMinutes`(0-1440,預設 60)— 清理掃描的間隔。設為 `0` 會停用排程清理,屆時儲存只會在寫入資料時才縮減。
- `Record.StorageDirectory` 現在是真正可設定的屬性。它原本是 `const`,因此永遠無法被設定,而 `RecordingOptions.Validate()` 對它的空值檢查是死碼。預設值不變(`./.weda/data/recording`);原常數保留為 `RecordingOptions.DefaultStorageDirectory`。
- `IRecordingService.StorageSettings` / `UpdateStorageSettings(RecordingStorageSettings)` 與 `IRecordStorage.EnforceStorageLimitsAsync()` — 保留期與 ring-buffer 上限現在可於執行期變更,且無須有寫入進行中即可強制執行容量上限。
- **可遠端設定的 recording** — recording 設定改放在 `devicecfg.json` 新的 SubNode 層級 `SubNodeSettings.Record` 區塊(與 `SubNode` / `DeviceConfigs` 同層),並可透過 DeviceConfig desired 管線從雲端設定。下發的 record 會被驗證,並以第一級參與者的身分納入 all-or-nothing 的裝置設定交易(無效的 record 會退回整筆更新,裝置失敗時 record 也隨之回滾),快取後在重啟時重新套用。`IRecordingService` 新增 `CaptureForRollback` / `ApplyRecordConfig` / `RestoreFromRollback` 供交易參與。參見 [`docs/sample-devicecfg.json`](./docs/sample-devicecfg.json)。

### 變更
- 設定同步預設週期由 **60 秒調整為 24 小時**(`BackgroundTaskPeriods.DefaultReportConfigurationPeriod`);允許範圍由 10 秒–7 天收斂為 **60 秒–7 天**(`MinReportConfigurationPeriod` 10000 → 60000)。穩態下的 devicecfg 改以變更/重連觸發為主,而非高頻週期 tick。
- 超出範圍的 `Periods.ReportConfiguration` 現在會**夾到最近的邊界**(先前是替換為預設值),以保留操作者意圖;`0` 仍為明確停用(關閉週期同步),且現在會記錄一則警告而非靜默套用。
- **每個 sensor 的 `Record.Interval` 現在改為選填,僅在啟用錄製時必填。** `Sensors[].Record.Interval` 改為 nullable;僅當 `Record.Enabled` 為 `true` 時才必填,且必須 `>= 1000`。當錄製停用、或未填 interval 時,套用階段會以該 sensor 的 report interval 補值,並夾到 `1000` ms 的錄製下限(report interval 下限降為 `50` ms)。在套用階段就 resolve 出 interval,使上雲 round-trip 的值必定在合法範圍內,停用的 record 不再被雲端的 interval 檢查拒絕。**BREAKING:** `Record.Enabled` 的預設值改為 `false` — 原本沒帶 record 設定、或帶了 record 但沒明確 `enabled` 的 sensor,以前預設會錄製,現在不會。DTDL:report interval `minimum` 由 `1000` 降為 `50`;record interval `minimum` 維持 `1000`。
- `Record.MinFreeDiskSpaceMb` 與 `Record.MaxStorageSizeMb` 的說明文件已更正為兩個不同的限制 — 前者是整顆*磁碟*的可用空間下限,後者是 recording *目錄*的容量上限。預設值維持不變(128 MB 與 1024 MB);兩者同為 128 的巧合,長期讓前者被誤讀為「recording 上限 128 MB」。
- `IRecordStorage.CleanupAsync` 與 `IRecordingService.CleanupAsync` 的回傳型別由 `Task` 改為 `RecordingCleanupResult`(掃描/刪除檔數、釋放位元組、失敗檔數)。對外部實作者為 source-breaking 變更。
- Recording 設定改為透過共用的 `RecordingOptionsProvider` 讀取,不再各自快取 `IOptions<RecordingOptions>.Value`,因此一次更新即可同時生效於 storage、recording service 與清理排程。更新會發佈新的 options 實例,不會就地修改既有實例。

### 移除
- **BREAKING — recording 不再於 `systemcfg.json` 設定。** `systemcfg` 依政策不開放遠端設定;recording 已移至 `devicecfg.json` 的 `SubNodeSettings.Record`。移除項目:`HandleSystemConfigUpdateAsync` 中的 `Record` 套用路徑、`SubNodeSystemCfgDto.Record` wire 欄位,以及 `RecordValidator`。仍留在 `systemcfg.json` 的 `Record` 區塊現在會被忽略,並在啟動時記錄一則警告。
- **BREAKING — `WedaContextOptions.RecordingOptions` 已移除。** 以程式碼提供 recording 設定與「可遠端設定」的模型相衝突 — 它會在下次啟動時覆蓋雲端下發的值。recording 只能透過 `devicecfg.json` 的 `SubNodeSettings.Record` 設定。

#### 安裝與升級說明(移除項目)
- 將每個 `systemcfg.json` 的 `Record` 區塊移到對應的 `devicecfg.json`,放在新的頂層 `SubNodeSettings` 物件底下:`{ "SubNodeSettings": { "Record": { … } } }`,與 `SubNode`、`DeviceConfigs` 同層。參見 [`docs/sample-devicecfg.json`](./docs/sample-devicecfg.json)。把 `Record` 留在 `systemcfg.json` 不會報錯但不會生效(啟動警告會指出該檔)。
- 若你在程式碼中設定 `WedaContextOptions.RecordingOptions`,請移除該指派,改把值放進 `devicecfg.json` 的 `SubNodeSettings.Record`。

### 修正
- **超過 `RetentionDays` 的 recording 檔案沒有被自動清除**(工作項目 53692)。清理排程使用 `dueTime: Zero, period: 1 day`,因此只在行程啟動時跑一次,之後每 24 小時的「運行時間」才跑一次:啟動掃描之後才產生的檔案最多要多留一整天,而重開機頻率高於一天一次的裝置,實際上永遠只跑得到啟動那一次掃描 — 而它只看得到開機前就存在的檔案。現在預設每小時掃描一次,且排程本身已有測試覆蓋(`RecordingCleanupSchedulerTests`),這是舊 timer 所缺的。
- **雲端下發的 `Record` 設定被靜默丟棄。** `SubNodeManager` 只套用了 `Enabled`、`BatchEnabled` 與 `BatchMaxSamples`;同一則訊息中的 `RetentionDays`、`MaxStorageSizeMb` 與 `MinFreeDiskSpaceMb` 通過了 `RecordValidator`、被回報成功 — 然後被丟棄。三者現在都會套用,且無須重啟即可在下一次寫入與下一次掃描生效。
- **Ring-buffer 容量上限只在寫入時才會生效。** `EnsureDiskSpace` 僅在 `WriteAsync` 內執行,因此 sensor 停止記錄(或 recording 被關閉)的裝置會無限期超出上限。排程掃描現在也會強制執行。
- **MIME 型別的 recording 永遠不會過期。** `DynamicRecordStorage.CleanupAsync` 存在但無人呼叫,已記錄的 JSON 與影像因此無上限累積。掃描現在會以相同 cutoff 套用到兩種儲存。
- **單一無法刪除的檔案會中止整趟清理。** `BinaryRecordStorage.CleanupAsync` 直接呼叫 `File.Delete` 而未防護,唯一的例外處理在 timer callback 最外層,因此一個被鎖定或權限不足的檔案會讓其後所有 sensor 都沒被掃到。現在改為逐檔記錄失敗、計入 `RecordingCleanupResult.FilesFailed`,並繼續掃完。
- `DynamicRecordStorage` 以未解析的相對路徑建構,而 `BinaryRecordStorage` 對同一設定會經過 `PathHelper` 解析,導致在容器外兩者可能寫到不同目錄。現在兩者解析方式一致。

### 安裝與升級說明
- 若你的裝置會在執行期變更自身設定(sensor 重新解析、calibration 寫入、`RawDeviceCfgJson` 更新),且原本仰賴舊的 60 秒週期 tick 來傳播,請在這些變更點呼叫 `ISubNodeManager.TriggerConfigSync()` — 在 24 小時預設下,未觸發的本地變更最多可能延遲一天才上雲。
- 任何低於 60000ms 的 `Periods.ReportConfiguration` 現在會被夾到 60000ms(舊的 10000ms 下限不再適用)。若你依賴特定週期,請明確設定其值。
<<<<<<< HEAD
- `Record.Enabled` 的預設值改為 `false`。若有 sensor 原本仰賴舊預設值來錄製(未明確設定),請在 `devicecfg.json` 該 sensor 加上 `"Record": { "Enabled": true, "Interval": <ms> }`。啟用錄製時 interval 必須 `>= 1000` ms。
=======
- `IRecordStorage` 或 `IRecordingService` 的外部實作者需將 `CleanupAsync` 回傳型別改為 `RecordingCleanupResult`,並實作 `EnforceStorageLimitsAsync`、`StorageSettings` 與 `UpdateStorageSettings`。
>>>>>>> develop

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
