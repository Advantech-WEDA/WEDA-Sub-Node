# 更新日誌

Weda SubNode SDK 的所有重要變更都將記錄在此文件中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，
本專案遵循 [語意化版本](https://semver.org/lang/zh-TW/)。

## [未發布]

### 新增
- `IDeviceRegistry` 用於跨裝置發現和通訊
- `IWedaApplicationContext` 上的 `GetDevice`/`FindDevice` 方法
- `IDevice` 上的 `GetSensor`/`FindSensor`/`GetSensorByResourceId`/`FindSensorByResourceId` 方法
- `WedaApplicationBuilder` 上的 `ConfigureConnectionPolicy()` 方法
- 協定解析器介面：`IProtocolParserCore`、`IPublishSubscribeProtocolParser`、`IRequestResponseProtocolParser`
- `ISensingPubSubParser` 用於 ISensing MQTT 發布/訂閱協定
- `ModbusRequestResponseParser` 用於 Modbus 請求/回應協定

### 變更
- 連線重試策略現可透過 `ConnectionOptions` 配置
- `ConnectionOptions` 擴充 `MaxRetryDelayMs` 和 `WithRetries()` 工廠方法
- 重構 `IProtocolParser` 為針對不同通訊模式的專用介面

### 移除
- 過時的整合測試 (`Wise4012SeDeviceIntegrationTests`、`MqttISensingIntegrationTests`)
- `DeviceOptions` 中未使用的 `DefaultPollingIntervalMs`

## [0.0.1] - 2025-11-17

### 新增

#### 核心框架
- `IWedaApplicationContext` - 中央應用程式上下文介面
- `WedaApplicationContext` - 預設實作，包含 NATS 雲端服務
- `WedaApplicationBuilder` - 流暢建構器模式，用於應用程式配置
- `WedaApplication.CreateBuilder()` 和 `CreateDefaultBuilder()` 工廠方法

#### 裝置抽象
- `IDevice` 裝置抽象介面
- `DeviceBase` 基礎類別，包含通用裝置功能
- `DeviceConfiguration` 用於從 appsettings.json 讀取裝置設定
- `DeviceCapabilities` 用於裝置元資料（製造商、型號、版本）
- `Sensor` 類別，支援轉換和 DSP 管線

#### Modbus 支援
- `TcpModbusDevice` 用於 Modbus TCP 通訊
- `ModbusDevice` Modbus 協定處理基礎類別
- 連續暫存器的批次讀取最佳化
- 自動批次演算法，可配置 `MaxGapSize` 和 `MaxBatchSize`

#### ISensing MQTT 支援
- `MqttISensingDevice` 用於 ISensing MQTT 通訊
- `ISensingDevice` ISensing 協定處理基礎類別
- 發布/訂閱模式，用於即時感測器資料

#### 雲端整合
- 基於 NATS 的雲端服務 (`WedaCloudService`)
- 遙測上傳功能
- 向 Weda.Core 進行裝置註冊
- 模擬雲端服務 (`MockCloudService`) 用於開發/測試

#### 配置
- 基於 `appsettings.json` 的配置
- Serilog 日誌整合
- NATS 連線設定
- 裝置配置，包含感測器、轉換和 DSP 濾波器

#### 資料處理
- 轉換管線（校正、單位轉換）
- DSP 濾波器管線（移動平均、卡爾曼、低通、高通）
- 閾值監控（上限嚴重、上限警告、下限警告、下限嚴重）

#### 連線管理
- 基於 Polly 的韌性策略
- 可配置的重試策略（預設為 AlwaysRetry）
- 連線狀態管理
- 自動重連支援

#### 範本
- `SubNodeTemplate` 專案範本，快速開始
- 範例專案（WISE-4012 建構器模式）

#### 文件
- 完整的 Wiki 文件（英文和中文）
- 快速入門指南
- API 參考
- 最佳實踐

### 基礎設施
- DevContainer 開發支援
- 集中式 DTDL 管理
- 解決方案結構重組以改善組織

---

[未發布]: https://dev.azure.com/AIM-IIoT/EdgeSync/_git/edge_subnode/branchCompare?baseVersion=GTv0.0.1&targetVersion=GBfeature/phase-2
[0.0.1]: https://dev.azure.com/AIM-IIoT/EdgeSync/_git/edge_subnode/branchCompare?baseVersion=GTa19787d&targetVersion=GTv0.0.1
