# 更新日誌

Weda SubNode SDK 的所有重要變更都將記錄在此文件中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，
本專案遵循 [語意化版本](https://semver.org/lang/zh-TW/)。

## [1.1.0] - 2026-05-21

### 新增
- OPC UA 協定支援：通訊層、PubSub 與 RequestResponse 裝置類別、TCP 傳輸、request/response 解析器，以及型別化裝置配置
- OPC UA 模擬器（含 hosted service）與 `opcua-device` 範例專案
- 透過序列埠的 Modbus RTU 支援，含 CRC16 驗證
- Modbus TCP 與 RTU 通訊層（`ModbusTcpCommunication`、`ModbusRtuCommunication`）
- 序列通訊與 `SerialCommunicationFactory`，支援序列埠共享
- 半雙工序列埠行為，並含單元測試
- `ISensingDevice` 的 AI/AO/DI/DO getter 與 setter 命令，含 payload
- 標準化命令狀態碼
- System Agent 重新開機與關機命令（`nsenter` + libc `reboot()`）
- DTDL metrics 模型、範例配置，以及 `devicecfg.json` 驗證工具（`dtdl-validate`）
- GPIO `pinState` 僅限整數的驗證規則與 DTDL `GpioSensorParameters` schema
- 感測器自動探索（`DiscoverAvailableResources`）與 list-sensor 功能
- NATS web dashboard 工具
- 集中式 NuGet 套件版本管理（`ManagePackageVersionsCentrally`）

### 變更
- 將裝置專屬類別移至獨立的 `Protocols` 層
- 將 DTDL 模組重構為獨立的 System Resource 與 GPU Resource 模組
- 將 wiki 重構為以英文為預設，並統一名稱（不再使用「Sensor Expansion」/「SensorExpander」）
- 將 `dtdl-validate` 解耦為獨立專案，不再與 system-agent 一同建置
- 升級 `Advantech.Edge` 至 1.1.3

### 修復
- 序列化裝置重新連線，並在呼叫端取消時維持狀態穩定
- 在 Modbus wrapper 通訊中同步內層傳輸狀態
- 在關機／配置更新時，抑制裝置讀取路徑的取消雜訊
- 提前確認 `advantechEdgeDevice` 是否為 `InitializationFailed` 狀態
- 修正 NATS CLI Dockerfile 下載連結找不到的問題

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

[1.1.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[1.0.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[0.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4338/Sub-Node-SDK
