---
sidebar_position: 1
sidebar_label: '環境準備'
hide_title: true
title: '環境準備 - SubNode 開發環境設定'
keywords: ['SubNode', 'Prerequisites', '.NET', 'Development', 'Setup']
description: 'SubNode SDK 開發所需的工具和環境設定'
---

# 環境準備

> 設定 SubNode SDK 的開發環境。

## 必要軟體

### .NET SDK

SubNode 需要 .NET 9.0 或更新版本。

**安裝方式：**

- **Windows/macOS/Linux**: 從 [dotnet.microsoft.com](https://dotnet.microsoft.com/download) 下載
- **macOS (Homebrew)**: `brew install dotnet`
- **Ubuntu/Debian**: 依照 [Microsoft 的 Linux 指示](https://learn.microsoft.com/dotnet/core/install/linux)

**驗證安裝：**

```bash
dotnet --version
# 預期輸出: 9.0.x 或更高版本
```

### IDE（建議）

選擇以下其中之一：

| IDE | 平台 | 說明 |
|-----|------|------|
| **Visual Studio 2022** | Windows | 完整功能的 IDE，包含除錯工具 |
| **Visual Studio Code** | 跨平台 | 輕量級，需要 C# 擴充套件 |
| **JetBrains Rider** | 跨平台 | 商業軟體，優秀的 .NET 支援 |

使用 VS Code 時，請安裝以下擴充套件：
- C# Dev Kit (Microsoft)
- C# (Microsoft)

## 選用工具

### Git

用於複製範例和版本控制。

```bash
# 驗證安裝
git --version
```

### Docker

用於執行模擬器和容器化部署。

```bash
# 驗證安裝
docker --version
```

## NuGet 套件來源

SubNode 套件透過 Azure DevOps 私有 feed 發布。設定 NuGet 以存取此 feed：

**選項 1：全域 NuGet 設定**

建立或編輯 `~/.nuget/NuGet/NuGet.Config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="EdgeSync" value="https://pkgs.dev.azure.com/YourOrg/_packaging/EdgeSync/nuget/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <EdgeSync>
      <add key="Username" value="YOUR_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PAT" />
    </EdgeSync>
  </packageSourceCredentials>
</configuration>
```

**選項 2：專案層級設定**

在專案根目錄放入 `NuGet.Config`（範本已提供）。

## 硬體（選用）

用於實體裝置測試：

| 裝置類型 | 範例 | 協定 |
|----------|------|------|
| Modbus TCP 裝置 | WISE-4012、ADAM-6017 | Modbus TCP |
| Modbus RTU 裝置 | ADAM-4017、ADAM-4055 | RS-485 |
| MQTT 裝置 | 任何支援 MQTT 的感測器 | MQTT |

在沒有硬體的情況下開發，可使用內建的 **Modbus Simulator**（請參閱[使用範本開始](./start-with-template.md)）。

## 驗證環境

執行此命令以驗證您的環境：

```bash
# 建立新的 console 專案以驗證 .NET 正常運作
dotnet new console -o TestProject
cd TestProject
dotnet run
# 應該印出 "Hello, World!"

# 清理
cd ..
rm -rf TestProject
```

## 下一步

環境準備好後：

1. [使用範例開始](./start-with-example.md) - 執行預建範例
2. [使用範本開始](./start-with-template.md) - 從範本建立新專案
3. [連接到 WedaCore](./connect-to-wedacore.md) - 設定雲端連線

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
