# 更新日誌

Weda SubNode SDK 的所有重要變更都將記錄在此文件中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，
本專案遵循 [語意化版本](https://semver.org/lang/zh-TW/)。

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

[1.0.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4301/Sub-node
[0.2.0]: https://dev.azure.com/Advantech-EBO/IoT%20Platform/_wiki/wikis/IoT-Platform.wiki/4338/Sub-Node-SDK
