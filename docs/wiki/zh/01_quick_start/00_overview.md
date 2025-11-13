---
title: "概覽 - Weda SubNode SDK 簡介"
description: "了解 Weda SubNode SDK 的核心功能與三種專案模板"
author: "Rain Hu"
date: "2025-11-07"
lang: "zh"
parent: "README"
next: "01_install_templates"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/00_overview.md"
---

# 概覽 - Weda SubNode SDK 簡介

Weda SubNode SDK 是一個用於串連邊緣裝置與雲端平台的 .NET 框架，讓您輕鬆建立 IoT 邊緣節點，實現雲地整合。

**所需時間**：5 分鐘
**難度**：初學者

---

## 什麼是 Weda SubNode？

**Weda SubNode** 是連接邊緣裝置與雲端管理平台（Weda.Core）之間的橋樑。它負責：

- **⬆️ 上行（Data Collection）**：從邊緣裝置收集資料，處理後傳送到雲端
- **⬇️ 下行（Device Command）**：接收雲端指令，控制邊緣裝置

### 架構圖解

#### (1) 資料收集（Telemetry）- 上行
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

#### (2) 裝置控制（Command）- 下行
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

## 核心功能

### 分層架構說明

Weda SubNode 遵循 **OSI 七層模型**，清楚地分離各層職責：

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

**關鍵概念**：
- **SubNode 內部**主要運作在 **Layer 7（應用層）**
- **ICommunication** 封裝了 **Layer 4-7** 的完整通訊堆疊
- **IProtocolParser** 解析 Layer 7 協定資料，轉換為應用程式可用的格式
- **ITransformation/IDspFilter** 處理應用層的業務邏輯

---

### 📡 通訊層（ICommunication）- Layer 4-7
**統一的通訊介面**，封裝完整的通訊堆疊（Layer 4-7）：

**支援的協定**：
- **Modbus TCP/RTU** - 工業標準協定
- **MQTT** - 輕量級訊息協定
- **自訂協定** - 可擴充支援任何協定

**封裝的功能層級**：
- **Layer 7**: 應用層協定實作（Modbus, MQTT, HTTP）
- **Layer 6**: 資料編碼與解碼
- **Layer 5**: 連線管理、會話維護、自動重連
- **Layer 4**: TCP/UDP 傳輸、流量控制

**關鍵功能**：
- 提供統一的 `ICommunication` 介面
- 隱藏底層通訊複雜度
- 自動處理連線生命週期
- 支援同步與非同步操作

### 🔄 協定解析（IProtocolParser）- Layer 7
**應用層資料處理**，將協定資料轉換為應用程式可用格式：

**處理的協定資料**：
- **Modbus 資料** - Register 值、Function Code 回應
- **MQTT 訊息** - Topic、Payload
- **自訂格式** - 可擴充的解析器

**關鍵功能**：
- 解析協定層已傳遞上來的資料
- 二進位轉換為結構化資料（Float, Int, Bool, String...）
- 自動處理 Big/Little Endian
- 型別驗證與錯誤檢測
- 提供統一的資料模型給上層使用

### 🔧 資料轉換（ITransformation）- Layer 7
負責**應用層**業務邏輯，彈性的資料處理管線：
- **公式轉換** - `(x * 10) + 5`
- **單位換算** - 攝氏轉華氏
- **資料映射** - 重新命名欄位
- **條件邏輯** - if-then-else 規則

**關鍵功能**：
- 支援複雜數學運算
- 動態表達式評估
- 管線化處理（多個轉換串接）

### 📊 數位濾波（IDspFilter）- Layer 7
負責**應用層**訊號處理，內建數位訊號處理：
- **移動平均** - 平滑雜訊資料
- **低通濾波** - 去除高頻雜訊
- **異常偵測** - 識別異常值
- **資料緩衝** - 批次傳送

**關鍵功能**：
- 即時訊號處理演算法
- 可配置濾波參數
- 減少雲端傳輸量

### ☁️ 雲端連接（NATS）- Layer 7
負責**應用層**雲端通訊，高效能雙向通訊：
- **輕量級** - 低延遲、低資源佔用
- **可靠傳輸** - 自動重連與訊息確認
- **安全連線** - TLS 加密與認證
- **雙向通訊** - Telemetry 上傳 & Command 下發

**關鍵功能**：
- Pub/Sub 訊息模式
- Request/Reply 同步通訊
- JetStream 持久化儲存

### ⚙️ 裝置管理
完整的裝置生命週期管理：
- **自動初始化** - 連線與設定檢查
- **健康監控** - 定時回報裝置狀態
- **錯誤重試** - 斷線自動重連
- **斷路器模式** - 避免連續失敗

---

## 使用場景

### 🏭 工業物聯網（IIoT）
- 收集工廠機台資料
- 即時監控生產線
- 遠端控制設備

### 🏢 智慧建築
- 整合 BMS 系統
- 能源監控與管理
- HVAC 系統控制

### 🌾 智慧農業
- 環境感測器資料收集
- 自動灌溉控制
- 溫室環境監控

### 🔌 能源管理
- 電力監測與分析
- 智慧電表整合
- 再生能源監控

---

## 為什麼選擇 Weda SubNode SDK？

### ✅ 快速開發
- **3 分鐘** 建立第一個專案
- **內建模板** 快速啟動
- **自動化設定** 減少樣板程式碼

### ✅ 工業級穩定
- **斷線重連** 自動恢復
- **錯誤處理** 完整的重試機制
- **記憶體管理** 防止洩漏

### ✅ 高度彈性
- **可擴充架構** 自訂協定、處理邏輯
- **多種模板** 適應不同開發風格
- **配置驅動** 無需修改程式碼即可調整

### ✅ 現代化技術
- **.NET 9.0** 最新框架
- **非同步程式設計** 高效能處理
- **依賴注入** 易於測試與維護

---

## 下一步

準備好開始使用了嗎？讓我們從安裝開始：

**[→ 安裝模板並開始建置](01_install_templates.md)**

**建議學習路徑**：
- **初學者**：從 wedaapi 開始（Web API 風格）
- **單一裝置開發/調試**：使用 subnode（Console App 風格）

---

**版本**: 1.0.0
**最後更新**: 2025-11-07
**維護者**: Rain Hu
