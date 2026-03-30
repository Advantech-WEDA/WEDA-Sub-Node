---
sidebar_position: 4
sidebar_label: '連接到 WedaCore'
hide_title: true
title: '連接到 WedaCore | SubNode SDK'
keywords: ['SubNode', 'WedaCore', 'WedaNode', 'NATS', 'Cloud', 'Authentication']
description: '設定 SubNode 透過 WedaNode 連接到 WedaCore 雲端平台。'
---

# 連接到 WedaCore

> 設定 SubNode 透過 WedaNode 連接到 WedaCore 雲端平台。

## Overview

SubNode 應用程式不直接連接 WedaCore，而是透過本機的 WedaNode（NATS Proxy）進行通訊。WedaNode 由 Device Activator 安裝，負責安全連接、憑證管理和訊息路由。本文說明開發與正式環境的差異，以及 `systemcfg.json` 中各種認證策略的設定方式。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解 SubNode、WedaNode、WedaCore 三者的關係
- 區分開發模式（MockCloud）與正式模式的差異
- 在 `systemcfg.json` 中設定不同的認證策略
- 排解常見的連線問題

## Prerequisites

- 完成[環境準備](./01-prerequisites.md)
- 完成[使用範例開始](./02-start-with-example.md)或[使用範本開始](./03-start-with-template.md)

---

## 架構概述

```text
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│   SubNode    │──NATS──>│   WedaNode   │──NATS──>│   WedaCore   │
│ Application  │         │ (127.0.0.1:  │         │   (Cloud)    │
│              │         │     4224)    │         │              │
└──────────────┘         └──────────────┘         └──────────────┘
```

- **SubNode** - 你的邊緣裝置應用程式
- **WedaNode** - 本機 NATS Proxy，由 Device Activator 安裝，預設監聽 `127.0.0.1:4224`
- **WedaCore** - 雲端平台，提供數位分身、遙測儲存、遠端控制等功能

---

## 開發模式 vs 正式模式

### 開發模式（MockCloud）

開發期間使用 `UseMockCloud()`，無需 WedaNode 和網路連線：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();
```

MockCloud 模擬雲端行為，讓你可以：

- 測試裝置通訊和感測器設定
- 除錯 Data Pipeline
- 開發 Command Handler
- 完全離線開發

### 正式模式

正式環境中，移除 `UseMockCloud()`。`CreateDefaultBuilder` 會自動從 `systemcfg.json` 讀取 WedaNode 連線設定：

```csharp
// CreateDefaultBuilder automatically reads systemcfg.json for WedaNode settings
var builder = WedaApplication.CreateDefaultBuilder(args);
```

---

## 設定 WedaNode 連線

### systemcfg.json

WedaNode 連線設定在 `systemcfg.json` 中定義：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

> `systemcfg.json` 在框架內部會被載入到 `SystemConfig` section（即 `SystemConfig:WedaNode`）。

---

## 認證策略

SDK 支援以下認證策略（對應 `NatsAuthStrategy` enum）：

### None（預設）

匿名連線，無需認證：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

### UserPassword

使用者名稱與密碼認證（Device Activator 預設使用此策略）：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "UserPassword",
    "Username": "advantech_nats",
    "Password": "your_password_hash"
  }
}
```

### Token

Token 認證：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "Token",
    "Token": "your_auth_token"
  }
}
```

### CredFile

使用 NATS Credential File（包含 JWT + NKey Seed）：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "CredFile",
    "CredFile": "/path/to/credentials.creds"
  }
}
```

### TlsCert

TLS 用戶端憑證認證：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "TlsCert",
    "TlsCertPath": "/path/to/client.crt",
    "TlsKeyPath": "/path/to/client.key",
    "TlsCaPath": "/path/to/ca.crt"
  }
}
```

| 欄位 | 必要 | 說明 |
|------|------|------|
| `TlsCertPath` | 是 | TLS 用戶端憑證（PEM 或 PFX）|
| `TlsKeyPath` | 是 | TLS 用戶端私鑰（PEM）|
| `TlsCaPath` | 否 | CA 憑證，用於驗證伺服器 |

### 認證策略總覽

| AuthStrategy | 必要欄位 | 適用場景 |
|--------------|----------|----------|
| `None` | - | 開發、測試 |
| `UserPassword` | `Username`, `Password` | Device Activator 預設 |
| `Token` | `Token` | 簡易 Token 驗證 |
| `CredFile` | `CredFile` | NATS JWT + NKey 認證 |
| `TlsCert` | `TlsCertPath`, `TlsKeyPath` | 企業級 TLS 雙向認證 |

---

## 覆寫設定

除了 `systemcfg.json`，也可以透過環境變數或命令列參數覆寫 WedaNode 連線設定。設定路徑為 `SystemConfig:WedaNode:<Property>`。

### 環境變數

```bash
# Connection URL
export SystemConfig__WedaNode__Url="127.0.0.1:4224"

# UserPassword authentication
export SystemConfig__WedaNode__AuthStrategy="UserPassword"
export SystemConfig__WedaNode__Username="your_username"
export SystemConfig__WedaNode__Password="your_password_hash"

# Or CredFile authentication
export SystemConfig__WedaNode__AuthStrategy="CredFile"
export SystemConfig__WedaNode__CredFile="/path/to/credentials.creds"
```

> 環境變數使用 `__`（雙底線）作為階層分隔符。

### 命令列參數

```bash
# Override URL
dotnet run -- --SystemConfig:WedaNode:Url=172.22.160.197:4224

# Override authentication
dotnet run -- \
  --SystemConfig:WedaNode:AuthStrategy=UserPassword \
  --SystemConfig:WedaNode:Username=admin \
  --SystemConfig:WedaNode:Password=secret
```

> 命令列參數優先權最高，會覆蓋 `systemcfg.json` 和環境變數的設定。適合用於快速測試不同的 WedaNode 連線。

---

## 雲端功能

連接到 WedaCore 後，以下功能可用：

| 功能 | 方向 | 說明 |
|------|------|------|
| Telemetry | Uplink | 傳送感測器資料到雲端 |
| Health Reporting | Uplink | 報告裝置健康狀態 |
| Commands | Downlink | 接收並執行遠端命令 |
| Config Updates | Downlink | 接收並套用設定變更 |
| Recording | Local | 本機歷史資料儲存 |

使用 `CreateBuilder` 時，可個別選擇啟用的功能：

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddTelemetry()        // Uplink: telemetry reporting
    .AddHealthReporting()  // Uplink: health status
    .AddCommands()         // Downlink: remote commands
    .AddConfigUpdates()    // Downlink: configuration sync
    .AddRecording();       // Local: historical data storage
```

> `CreateDefaultBuilder` 預設啟用所有功能。

---

## 驗證連線

### 檢查 WedaNode 狀態

```bash
# Check if WedaNode is listening
netstat -an | grep 4224
# or
ss -tlnp | grep 4224
```

### 啟用連線日誌

在 `appsettings.json` 中啟用詳細日誌：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Weda.SubNode.Cloud": "Debug"
      }
    }
  }
}
```

---

## 疑難排解

### 連線被拒絕

```text
Error: Connection refused to 127.0.0.1:4224
```

**解決方案：**

1. 確認 WedaNode 是否正在執行
2. 檢查 WedaNode 日誌是否有錯誤
3. 確保防火牆允許本機連線
4. 聯繫 WedaNode 管理員

### 認證失敗

```text
Error: Authentication failed - invalid credentials
```

**解決方案：**

1. 確認 `AuthStrategy` 與 WedaNode 管理員提供的認證方式一致
2. 驗證 `Username` / `Password` 或憑證檔案路徑是否正確
3. 確保憑證未過期
4. 聯繫 WedaNode 管理員取得有效憑證

---

## Summary

- SubNode 透過 WedaNode（本機 NATS Proxy）連接 WedaCore，不直接連雲端
- 開發時使用 `UseMockCloud()`，正式環境移除即可自動讀取 `systemcfg.json`
- SDK 支援 5 種認證策略：`None`、`UserPassword`、`Token`、`CredFile`、`TlsCert`
- Device Activator 預設使用 `UserPassword` 策略，`systemcfg.json` 會自動設定好

## See Also

- [專案結構](../03-architecture/01-project-structure.md) - 了解設定檔的載入順序
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 設定裝置與感測器
- [術語表](../01-introduction/03-terminology.md) - WedaNode、WedaCore 的定義

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |
