---
sidebar_position: 4
sidebar_label: '連接到 WedaCore'
hide_title: true
title: '連接到 WedaCore - 雲端整合設定'
keywords: ['SubNode', 'WedaCore', 'WedaNode', 'NATS', 'Cloud']
description: '設定 SubNode 透過 WedaNode 連接到 WedaCore 雲端平台'
---

# 連接到 WedaCore

> 設定 SubNode 透過 WedaNode 連接到 WedaCore 雲端平台。

## 架構概述

SubNode 透過 WedaNode（本機 NATS 代理）連接到 WedaCore：

```
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│   SubNode    │──NATS──>│   WedaNode   │──NATS──>│   WedaCore   │
│ Application  │         │ (127.0.0.1:  │         │   (Cloud)    │
│              │         │     4224)    │         │              │
└──────────────┘         └──────────────┘         └──────────────┘
```

WedaNode 由 **Device Activator** 安裝，負責處理：
- 安全連接到 WedaCore
- 憑證管理
- 訊息路由和緩衝

## 開發 vs 正式環境

### 開發模式（Mock Cloud）

開發期間，使用 `UseMockCloud()` 在無 WedaCore 的情況下測試：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();  // 無實際雲端連接
```

這讓您可以：
- 測試裝置通訊
- 驗證感測器設定
- 除錯 Data Pipeline
- 在無網路依賴的情況下開發

### 正式模式（WedaNode）

正式環境中，連接到 WedaNode：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseCloud();  // 連接到 WedaNode
```

## 設定選項

### systemcfg.json

在 `systemcfg.json` 中設定 WedaNode 連接：

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

### 認證策略

WedaNode 支援多種認證方式：

#### 無認證（預設）

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

#### 使用者名稱/密碼

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "UserPassword",
    "Username": "your_username",
    "Password": "your_password_hash"
  }
}
```

#### 憑證檔案

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "CredentialsFile",
    "CredentialsFile": "/path/to/credentials.creds"
  }
}
```

憑證檔案是標準 NATS 憑證檔案，包含：
- User JWT
- Private NKey

#### NKey 認證

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "NKey",
    "NKeyFile": "/path/to/nkey.seed"
  }
}
```

## 透過程式碼設定

您也可以以程式方式設定連接：

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .UseCloud(options =>
    {
        options.Url = "127.0.0.1:4224";
        options.AuthStrategy = AuthStrategy.UserPassword;
        options.Username = "your_username";
        options.Password = "your_password_hash";
    });
```

## 環境變數

使用環境變數覆寫設定：

```bash
# 連接 URL
export WEDANODE__URL="127.0.0.1:4224"

# 認證
export WEDANODE__AUTHSTRATEGY="UserPassword"
export WEDANODE__USERNAME="your_username"
export WEDANODE__PASSWORD="your_password_hash"

# 或使用憑證檔案
export WEDANODE__AUTHSTRATEGY="CredentialsFile"
export WEDANODE__CREDENTIALSFILE="/path/to/credentials.creds"
```

## 驗證連接

### 檢查 WedaNode 狀態

驗證 WedaNode 是否正在執行：

```bash
# 檢查 WedaNode 是否正在監聽
netstat -an | grep 4224
# 或
ss -tlnp | grep 4224
```

### 啟用連接日誌

啟用詳細日誌以排解連接問題：

**appsettings.json：**

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

### 預期日誌輸出

連接成功：

```
[12:00:00 INF] Connecting to WedaNode at 127.0.0.1:4224
[12:00:00 INF] Connected to WedaNode successfully
[12:00:00 INF] Registering SubNode: MySubNode
[12:00:00 INF] SubNode registered with ID: abc-123-def
```

連接失敗：

```
[12:00:00 ERR] Failed to connect to WedaNode at 127.0.0.1:4224
[12:00:00 ERR] Error: Connection refused
[12:00:05 INF] Retrying connection (attempt 2/5)...
```

## 雲端功能

透過 WedaNode 連接到 WedaCore 後，將啟用以下功能：

| 功能 | 方向 | 說明 |
|------|------|------|
| Telemetry | 上行 | 傳送感測器資料到雲端 |
| Health | 上行 | 報告裝置健康狀態 |
| Commands | 下行 | 接收遠端命令 |
| Config Updates | 下行 | 接收設定變更 |
| Alerts | 上行 | 傳送閾值警示 |

### 啟用/停用功能

控制哪些功能處於活動狀態：

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddTelemetry()        // 啟用遙測上傳
    .AddHealthReporting()  // 啟用健康報告
    .AddCommands()         // 啟用命令接收
    .AddConfigUpdates()    // 啟用設定同步
    .UseCloud();
```

## 疑難排解

### 連線被拒絕

```
Error: Connection refused to 127.0.0.1:4224
```

**解決方案：**
1. 驗證 WedaNode 是否正在執行
2. 檢查 WedaNode 日誌是否有錯誤
3. 確保防火牆允許本機連線
4. 聯繫 WedaNode 管理員

### 認證失敗

```
Error: Authentication failed - invalid credentials
```

**解決方案：**
1. 驗證使用者名稱/密碼是否正確
2. 檢查憑證檔案路徑是否存在
3. 確保憑證未過期
4. 聯繫 WedaNode 管理員取得有效憑證

### 逾時

```
Error: Connection timeout after 30s
```

**解決方案：**
1. 檢查網路連線
2. 驗證 WedaNode URL 是否正確
3. 在設定中增加逾時時間：
   ```json
   {
     "WedaNode": {
       "ConnectTimeout": 60000
     }
   }
   ```

## 下一步

- [專案結構](../03-project-structure.md) - 了解程式碼庫
- [感測器設定](../04-sensor-configuration/configuration-reference.md) - 設定感測器
- [遠端控制](../06-remote-control/built-in-commands.md) - 處理雲端命令

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
