---
title: "概覽 - 選擇您的模板"
description: "比較 subnode、wedaapi 與 wedaapi-c 模板，選擇最適合您專案的模板"
author: "Rain Hu"
date: "2025-11-07"
lang: "zh"
parent: "README"
next: "01_install_templates"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/00_overview.md"
---

# 概覽 - 選擇您的模板

Weda SubNode SDK 提供三種專案模板，適用於不同的使用情境。本指南協助您選擇最適合的模板。

**所需時間**：5 分鐘
**難度**：初學者

---

## 三種模板快速比較

| 功能 | subnode | wedaapi | wedaapi-c |
|------|---------|---------|-----------|
| **命令** | `dotnet new subnode` | `dotnet new wedaapi` | `dotnet new wedaapi-c` |
| **設定時間** | 15 分鐘 | 10 分鐘 | 15 分鐘 |
| **程式碼需求** | 自訂類別 | 最少 | 手動設定 |
| **設定方式** | 混合 | 純 JSON | 混合 |
| **彈性** | 高 | 低 | 最大 |
| **學習曲線** | 中等 | 簡單 | 進階 |
| **自動設定** | 手動 | 是 | 手動 |
| **自訂邏輯** | 完全控制 | 有限 | 完全控制 |
| **DI 控制** | 無 | 無 | 完全 |
| **最適合** | 自訂裝置 | 快速開始 | 企業應用 |

---

## 模板 1：subnode - 自訂裝置

**何時使用**：
- 需要自訂裝置生命週期邏輯
- 想要覆寫事件處理器
- 實作自訂協定
- 需要對裝置行為的最大控制

**您將獲得**：
- 繼承 `TcpModbusDevice` 基礎類別
- 覆寫方法如 `OnDataReceived`
- 自訂事件處理器
- 完全的協定控制

**範例結構**：
```
MyDevice/
├── MyDevice.cs              # 您的自訂裝置類別
├── Program.cs               # 應用程式入口
├── appsettings.json         # 設定檔
└── MyDevice.csproj
```

**關鍵程式碼**：
```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // 您的自訂邏輯
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("{Name}: {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

**優點**：
- 清楚的物件導向架構
- 透過繼承自然地擴充功能
- 關注點分離，易於維護
- 可測試的程式碼

**缺點**：
- 需要撰寫更多程式碼
- 需要理解裝置生命週期
- 中等學習曲線

---

## 模板 2：wedaapi - 簡易 API

**何時使用**：
- 想要最快速的設定
- 標準 Modbus 裝置，無需自訂邏輯
- 偏好設定檔驅動方式
- 只需要收集數據並傳送到雲端

**您將獲得**：
- 所有設定都在 `appsettings.json`
- 自動裝置初始化
- 內建日誌與錯誤處理
- 零自訂程式碼需求

**範例結構**：
```
MyApp/
├── Program.cs               # 只需要 3 行程式碼！
├── appsettings.json         # 所有設定都在這裡
└── MyApp.csproj
```

**關鍵程式碼**：
```csharp
// Program.cs - 就這樣！
var app = WedaApplication.CreateDefaultBuilder(args).Build();
await app.RunAsync();
```

**優點**：
- 最快的入門方式
- 基本情境不需要寫程式碼
- 非常適合初學者
- 容易維護

**缺點**：
- 需要擴充功能時較不直觀
- 預設使用框架內建的裝置類別
- 適合標準 Modbus 使用案例

---

## 模板 3：wedaapi-c - 進階 API

**何時使用**：
- 需要註冊自訂服務
- 想要完全控制相依性注入
- 需要自訂日誌或中介軟體
- 整合現有的 .NET 基礎設施

**您將獲得**：
- 手動服務註冊
- 完全的 builder 模式控制
- 自訂日誌設定
- 使用 `AddDevice<TDevice>()` 手動註冊裝置

**範例結構**：
```
MyApp/
├── MyCustomDevice.cs        # 您的裝置類別
├── MyCustomService.cs       # 您的服務
├── Program.cs               # 手動設定
├── appsettings.json         # 設定檔
└── MyApp.csproj
```

**關鍵程式碼**：
```csharp
// Program.cs
var builder = WedaApplication.CreateBuilder(args);

// 註冊自訂服務
builder.Services.AddSingleton<IMyService, MyService>();

// 自訂日誌
builder.Logging.AddFilter("Weda.SubNode", LogLevel.Debug);

// 手動加入裝置
builder.AddDevice<MyCustomDevice>(deviceConfig);

var app = builder.Build();
await app.RunAsync();
```

**優點**：
- 最大彈性
- 完全控制 DI 容器
- 容易整合現有應用程式
- 專業架構

**缺點**：
- 需要更多設定
- 需要進階 .NET 知識
- 對簡單情境可能過度設計

---

## 決策樹

```
從這裡開始
    │
    ├─→ 第一次使用 SDK？
    │   └─→ 是 → 使用 wedaapi ✓
    │
    ├─→ 需要自訂裝置邏輯？
    │   └─→ 是 → 使用 subnode ✓
    │
    ├─→ 需要自訂服務/DI？
    │   └─→ 是 → 使用 wedaapi-c ✓
    │
    └─→ 只是收集數據？
        └─→ 是 → 使用 wedaapi ✓
```

---

## 所有模板都包含什麼？

所有三種模板都包含：

- **Modbus Simulator**（ModbusPal）- 無需硬體即可測試
- **MockCloudService** - 本地開發，無需雲端連線
- **Serilog 日誌** - 結構化日誌
- **設定支援** - appsettings.json
- **錯誤處理** - 重試策略與斷路器
- **健康監控** - 裝置健康回報

**預設設定**：
- 連接到 `127.0.0.1:502`（本機 Modbus Simulator）
- 使用 `MockCloudService`（無需雲端連線）
- 每 3 秒輪詢裝置
- 記錄到主控台與檔案

---

## 依使用案例比較

### 使用案例 1：快速數據收集
**情境**：從 Modbus 感測器讀取溫度，傳送到雲端
**推薦**：`wedaapi`
**原因**：零程式碼，純設定檔

### 使用案例 2：需要清楚的類別架構
**情境**：套用規則、觸發警報、儲存到資料庫
**推薦**：`subnode`
**原因**：物件導向設計，透過繼承清楚擴充功能

### 使用案例 3：複雜的相依性注入
**情境**：整合現有 ASP.NET Core 應用程式、多個自訂服務
**推薦**：`wedaapi-c`
**原因**：完全控制 DI 容器與服務註冊

### 使用案例 4：多裝置閘道
**情境**：管理 10+ 個裝置，不同設定
**推薦**：簡單情境用 `wedaapi`，複雜情境用 `wedaapi-c`
**原因**：容易在 JSON 設定多個裝置

### 使用案例 5：強調程式碼組織
**情境**：大型專案，需要模組化與清楚架構
**推薦**：`subnode` 或 `wedaapi-c`
**原因**：`subnode` 提供類別繼承架構，`wedaapi-c` 提供 DI 架構

---

## 重要說明

**所有模板都可以實作自訂業務邏輯與自訂協定！**

三種模板的差異在於：
- **架構風格**：物件導向繼承 vs 設定檔驅動 vs DI 容器控制
- **起始複雜度**：需要多少初始程式碼
- **擴充方式**：如何加入新功能

選擇模板的關鍵是：**您偏好的開發風格**與**專案的架構需求**。

---

## 下一步

現在您已理解模板，讓我們開始安裝：

**[→ 安裝模板](01_install_templates.md)**

安裝後，跳到您選擇的模板：
- [subnode - 自訂裝置 →](02_subnode_basic.md)
- [wedaapi - 簡易 API →](03_wedaapi_basic.md)
- [wedaapi-c - 進階 API →](04_wedaapi_c_basic.md)

---

## 總結

**選擇 subnode**：如果您想要撰寫具有事件處理器的自訂裝置類別。

**選擇 wedaapi**：如果您想要最快速的設定，零程式碼。

**選擇 wedaapi-c**：如果您需要完全控制服務與 DI。

**仍不確定？** 從 **wedaapi** 開始 - 這是最容易學習的方式！

---

**版本**: 1.0.0
**最後更新**: 2025-11-07
**維護者**: Rain Hu
