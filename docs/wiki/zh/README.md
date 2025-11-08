---
title: "Weda SubNode SDK - 文檔中心"
description: "使用 Weda SubNode SDK 建立工業物聯網邊緣裝置的完整文檔"
author: "Rain Hu"
date: "2025-11-07"
lang: "zh"
translations:
  - lang: "en"
    path: "../en/README.md"
---

# Weda SubNode SDK - 文檔中心

歡迎使用 Weda SubNode SDK 文檔！本指南將協助您使用 .NET 9.0 建立工業物聯網邊緣應用程式。

**[English Documentation →](../en/README.md)**

---

## 什麼是 Weda SubNode SDK？

Weda SubNode SDK 是一個用於建立工業物聯網邊緣應用程式的 .NET 框架，提供以下功能：
- 透過 Modbus TCP/RTU 及其他協定連接裝置
- 從感測器與設備收集遙測數據
- 使用 Transformation 與 DSP 濾波器處理數據
- 透過 NATS 訊息傳遞至雲端服務
- 監控裝置健康狀態並妥善處理錯誤

---

## 快速導航

### 初學者入門
如果您是第一次使用 Weda SubNode SDK，請從這裡開始：
1. [概覽 - 選擇模板](01_quick_start/00_overview.md)（5 分鐘）
2. [安裝模板](01_quick_start/01_install_templates.md)（3 分鐘）
3. [您的第一個裝置 - wedaapi](01_quick_start/03_wedaapi_basic.md)（10 分鐘）

### 進階開發者
直接跳到進階主題：
- [自訂裝置邏輯 - subnode](01_quick_start/02_subnode_basic.md)
- [完全控制 - wedaapi-c](01_quick_start/04_wedaapi_c_basic.md)
- [使用案例](02_use_cases/)

---

## 學習路徑

### 步驟 1：開始使用（30 分鐘）

根據您的需求選擇三種模板之一：

| 文檔 | 說明 | 模板 | 時間 | 難度 |
|------|------|------|------|------|
| [00. 概覽](01_quick_start/00_overview.md) | 比較模板並選擇適合的 | - | 5 分鐘 | 初學者 |
| [01. 安裝模板](01_quick_start/01_install_templates.md) | 安裝專案模板 | - | 3 分鐘 | 初學者 |
| [02. subnode - 自訂裝置](01_quick_start/02_subnode_basic.md) | 繼承基礎類別，自訂邏輯 | `subnode` | 15 分鐘 | 中級 |
| [03. wedaapi - 簡易 API](01_quick_start/03_wedaapi_basic.md) | 設定檔驅動，最少程式碼 | `wedaapi` | 10 分鐘 | 初學者 |
| [04. wedaapi-c - 進階 API](01_quick_start/04_wedaapi_c_basic.md) | 完全控制，手動服務註冊 | `wedaapi-c` | 15 分鐘 | 進階 |

**預設設定**：所有模板預設使用 **Modbus Simulator** + **MockCloudService**。

### 步驟 2：連接真實環境（30 分鐘）

| 文檔 | 說明 | 時間 | 難度 |
|------|------|------|------|
| [05. 連接真實裝置](01_quick_start/05_connect_real_device.md) | 從 Simulator 切換到真實 Modbus 裝置 | 15 分鐘 | 初學者 |
| [06. 連接 Weda.Core](01_quick_start/06_connect_weda_core.md) | 從 MockCloudService 切換到 Weda.Core 雲端 | 15 分鐘 | 中級 |
| [07. iSensing 裝置](01_quick_start/07_real_device_isensing.md) | 連接 iSensing 協定裝置 | 20 分鐘 | 進階 |

### 步驟 3：進階使用案例（60+ 分鐘）

| 文檔 | 說明 | 時間 | 難度 |
|------|------|------|------|
| [自訂 Capability](02_use_cases/01_custom_capability.md) | 定義自訂 DTDL capability 欄位 | 20 分鐘 | 中級 |
| [Transformation Pipeline](02_use_cases/02_transformation.md) | 校正、單位轉換、自訂轉換 | 30 分鐘 | 中級 |
| [DSP 濾波器](02_use_cases/03_dsp_filters.md) | 降噪、Kalman 濾波 | 30 分鐘 | 進階 |
| [Hooks 與事件](02_use_cases/04_hooks.md) | 生命週期 hooks、資料庫持久化 | 30 分鐘 | 進階 |

### 步驟 4：進階主題（依需求）

| 文檔 | 說明 |
|------|------|
| [裝置生命週期](03_advanced/device_lifecycle.md) | 裝置狀態機與生命週期管理 |
| [Modbus Scanner](03_advanced/modbus_scanner.md) | 自動設定與裝置探索 |
| [Telemetry Transform](03_advanced/telemetry_transform.md) | 數據轉換 pipeline 架構 |

---

## 三種模板比較

| 功能 | subnode | wedaapi | wedaapi-c |
|------|---------|---------|-----------|
| **設定時間** | 15 分鐘 | 10 分鐘 | 15 分鐘 |
| **程式碼需求** | 自訂類別 | 最少 | 手動設定 |
| **彈性** | 高 | 低 | 最大 |
| **學習曲線** | 中等 | 簡單 | 進階 |
| **自動設定** | 手動 | 是 | 手動 |
| **自訂邏輯** | 完全 | 有限 | 完全 |
| **最適合** | 自訂裝置 | 快速開始 | 企業應用 |

**建議**：
- 第一次使用 SDK？從 **wedaapi** 開始
- 需要自訂邏輯？使用 **subnode**
- 需要完全控制？使用 **wedaapi-c**

---

## 先決條件

開始之前，請確認：

- **.NET 9.0 SDK** 或更新版本
  ```bash
  dotnet --version  # 應該是 9.0.x 或更高
  ```

- **程式碼編輯器**（VS Code、Visual Studio 或 Rider）

- **選用**：Modbus 裝置或模擬器
  - 實體裝置（例如：Advantech WISE-4012、PLC）
  - 軟體模擬器（ModbusPal、pyModSlave）
  - 模板內建的模擬器

- **選用**：NATS Server（用於雲端整合）
  ```bash
  # 安裝 NATS server
  # macOS: brew install nats-server
  # Linux: 參見 https://docs.nats.io/

  # 啟動 NATS 與 JetStream
  nats-server -js
  ```

---

## 範例專案

探索儲存庫中的可運作範例：

### 開源範例
- [examples/basic-modbus](../../examples/basic-modbus) - 使用 wedaapi 的基礎 Modbus 裝置
- [examples/simulator-demo](../../examples/simulator-demo) - 完整的模擬器設定

### 內部範例（僅限內部分支）
- `internal/wise-4012` - Advantech WISE-4012 整合
- `internal/wise-4012-isensing` - 使用 iSensing 協定的 WISE-4012

---

## 文檔結構

```
docs/wiki/
├── en/                                # 英文文檔
│   ├── README.md                      # 英文版導覽中心
│   ├── 01_quick_start/                # 快速開始指南
│   ├── 02_use_cases/                  # 使用案例
│   └── 03_advanced/                   # 進階主題
│
└── zh/                                # 中文文檔（平行結構）
    ├── README.md                      # 本文件 - 中文導覽中心
    ├── 01_quick_start/                # 快速開始指南
    │   ├── 00_overview.md             # 模板比較
    │   ├── 01_install_templates.md    # 安裝
    │   ├── 02_subnode_basic.md        # subnode 模板
    │   ├── 03_wedaapi_basic.md        # wedaapi 模板
    │   ├── 04_wedaapi_c_basic.md      # wedaapi-c 模板
    │   ├── 05_connect_real_device.md  # 連接真實裝置
    │   ├── 06_connect_weda_core.md    # 連接 Weda.Core
    │   └── 07_real_device_isensing.md # iSensing 裝置
    ├── 02_use_cases/                  # 使用案例
    │   ├── 01_custom_capability.md    # DTDL 自訂
    │   ├── 02_transformation.md       # 數據轉換
    │   ├── 03_dsp_filters.md          # 訊號處理
    │   └── 04_hooks.md                # Hooks 與事件
    └── 03_advanced/                   # 進階主題
        ├── device_lifecycle.md        # 裝置狀態機
        ├── modbus_scanner.md          # 自動設定
        └── telemetry_transform.md     # 轉換 pipeline
```

---

## 取得協助

- **文檔**：您正在閱讀！
- **範例**：查看 [examples/](../../examples/) 資料夾
- **問題回報**：請至 GitHub repository
- **貢獻指南**：參見 [docs/.rule.md](../../.rule.md) 的文檔撰寫規範

---

## 貢獻文檔

新增文檔時：

1. 遵循 [docs/.rule.md](../../.rule.md) 中的規範
2. 為所有 `.md` 檔案加上 YAML front matter
3. **同時更新 en/README.md 與 zh/README.md** 加入新文章
4. 測試所有程式碼範例
5. 確保英文與中文版本同步

---

## 下一步

**準備好開始了嗎？**

1. [概覽 - 選擇模板 →](01_quick_start/00_overview.md)
2. [安裝模板 →](01_quick_start/01_install_templates.md)
3. [建立您的第一個裝置 →](01_quick_start/03_wedaapi_basic.md)

---

**版本**: 1.0.0
**最後更新**: 2025-11-07
**維護者**: Rain Hu
