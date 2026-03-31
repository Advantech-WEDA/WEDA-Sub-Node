---
sidebar_position: 1
sidebar_label: '遠端設定更新'
hide_title: true
title: '遠端設定更新 | SubNode SDK'
keywords: ['SubNode', 'Configuration', 'Remote', 'Cloud', 'Config Update']
description: '了解 SubNode 如何接收並套用來自 WedaCore 的遠端設定更新。'
---

# 遠端設定更新

> 了解 SubNode 如何接收並套用來自 WedaCore 的遠端設定更新。

## Overview

WedaCore 可以遠端下發設定變更到 SubNode，例如新增/移除感測器、調整報告間隔、更新 Transform 參數等。SubNode 框架內建了完整的設定更新機制，包含驗證、套用、快取和狀態回報。開發者只需在自訂裝置中處理裝置特有的設定邏輯。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解設定更新的流程和框架自動處理的部分
- 在自訂裝置中覆寫 `OnAfterConfigUpdateAsync` 處理裝置特有設定
- 理解 `.device-config-cache.json` 的離線快取機制

## Prerequisites

- 完成[透過 JSON 設定](../04-configuration/02-configuration-via-json.md)
- 了解[連接到 WedaCore](../02-getting-started/04-connect-to-wedacore.md)

---

## 設定更新流程

```text
┌──────────┐   Config      ┌──────────────┐    Validate   ┌──────────┐
│ WedaCore │───Update─────▶│   SubNode    │──────────────▶│  Device  │
│          │   Message     │   Framework  │               │          │
└──────────┘               └──────┬───────┘               └────┬─────┘
                                  │                            │
                                  │  1. Validate               │
                                  │  2. Apply base config      │
                                  │  3. Persist to cache       │
                                  │  4. Report status          │
                                  │                            │
                                  │  5. Call hook ────────────▶│
                                  │     OnAfterConfigUpdate    │
                                  │                            │
```

### 框架自動處理的部分

| 步驟 | 動作 | 說明 |
|------|------|------|
| 1 | 驗證 | 呼叫 `ValidateConfigurationUpdate` 檢查設定合法性 |
| 2 | 套用基礎設定 | 更新 Sensors、Periods、Transform/DSP 參數 |
| 3 | 持久化 | 寫入 `.device-config-cache.json`（離線重啟時使用）|
| 4 | 狀態回報 | 向 WedaCore 回報 updating / success / failed |

### 開發者需要處理的部分

| Hook | 時機 | 用途 |
|------|------|------|
| `OnBeforeConfigUpdateAsync` | 套用之前 | 預處理或攔截 |
| `ValidateConfigurationUpdate` | 驗證階段 | 自訂驗證規則 |
| `OnAfterConfigUpdateAsync` | 套用之後 | 處理裝置特有設定 |

---

## 在自訂裝置中處理設定更新

### 基本用法

大多數情況下，框架自動處理即可。如果你的裝置有特殊設定，覆寫 `OnAfterConfigUpdateAsync`：

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey) { }

    protected override Task OnAfterConfigUpdateAsync(
        UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation(
            "Configuration update applied for device: {SubNodeId}", SubNodeId);

        // Handle device-specific custom configuration here
        // Base sensors and periods are already applied by the framework

        return Task.CompletedTask;
    }
}
```

> 參考範例：`examples/wise-4012/MyFirstDevice.cs`

### 自訂驗證

如果需要對設定變更加入額外驗證：

```csharp
protected override ConfigurationValidationResult ValidateConfigurationUpdate(
    UpdateConfigurationMessage message)
{
    // Run base validation first
    var baseResult = base.ValidateConfigurationUpdate(message);
    if (!baseResult.IsValid)
        return baseResult;

    // Add custom validation
    var desired = message.Data?.Cfg?.Desired;
    // ... validate custom properties ...

    return ConfigurationValidationResult.Valid();
}
```

---

## 設定快取

### .device-config-cache.json

雲端下發的設定更新會被持久化到 `.weda/.device-config-cache.json`。

| 場景 | 行為 |
|------|------|
| 正常啟動（有網路） | 從 WedaCore 取得最新設定 |
| 離線啟動（無網路） | 使用 cache 中的設定 |
| `--no-cache` 啟動 | 忽略 cache，使用 `devicecfg.json` 原始設定 |

```bash
# Force ignore cache
dotnet run -- --no-cache
```

---

## 可被遠端更新的設定

| 設定項目 | 支援 | 說明 |
|----------|------|------|
| Sensor 新增/移除 | 是 | 動態增減感測器 |
| Sensor Report Interval | 是 | 調整報告間隔 |
| Sensor Enabled/Disabled | 是 | 啟用/停用個別感測器 |
| Transform Parameters | 是 | 如 Scale、Offset |
| DSP Filter Parameters | 是 | 如 Window、ProcessNoise |
| DeviceCommunication | 否 | 需要重啟 |

---

## Summary

- 框架自動處理設定驗證、套用、快取和狀態回報
- 開發者只需覆寫 `OnAfterConfigUpdateAsync` 處理裝置特有設定
- `.device-config-cache.json` 確保離線重啟時使用最後同步的設定
- 使用 `--no-cache` 可強制忽略快取

## See Also

- [遠端命令](./02-command.md) - 從雲端執行裝置命令
- [連接到 WedaCore](../02-getting-started/04-connect-to-wedacore.md) - 雲端連線設定
- [透過 JSON 設定](../04-configuration/02-configuration-via-json.md) - 設定檔結構

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |
