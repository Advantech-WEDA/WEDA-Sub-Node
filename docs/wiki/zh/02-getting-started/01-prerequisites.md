---
sidebar_position: 1
sidebar_label: '環境準備'
hide_title: true
title: '環境準備 | SubNode SDK'
keywords: ['SubNode', 'Prerequisites', '.NET', 'Development', 'Setup', 'Docker', 'Dev Container']
description: 'SubNode SDK 開發所需的工具和環境設定，提供三種安裝方案。'
---

# 環境準備

> 選擇適合你的安裝方案，設定 SubNode SDK 開發環境。

## Overview

SubNode SDK 提供三種安裝方案，適合不同背景的使用者。選擇最符合你需求的方案，依照步驟完成即可開始開發。

## What You'll Learn

閱讀本文後，你將能夠：

- 根據自身背景選擇適合的安裝方案
- 完成開發環境的安裝與設定
- 驗證環境是否正確設定

---

## 選擇安裝方案

| 方案 | 需安裝 | 適合對象 |
|------|--------|----------|
| [A. .NET SDK + Editor](#a-net-sdk--editor) | .NET SDK + 任意編輯器 | .NET 開發者 |
| [B. Docker](#b-docker) | Docker | SI 系統整合商 |
| [C. VS Code + Dev Container](#c-vs-code--dev-container) | Docker + VS Code + Dev Containers Extensions | 無 .NET 經驗的開發者 |

---

## A. .NET SDK + Editor

適合已有 .NET 開發經驗的開發者。本機安裝 SDK，使用熟悉的編輯器或 IDE 開發。

### A.1 安裝 .NET SDK

SubNode 需要 .NET 10.0 或更新版本。

- **Windows/macOS/Linux**: 從 [dotnet.microsoft.com](https://dotnet.microsoft.com/download) 下載
- **macOS (Homebrew)**: `brew install dotnet`
- **Ubuntu/Debian**: 依照 [Microsoft 的 Linux 指示](https://learn.microsoft.com/dotnet/core/install/linux)

### A.2 驗證安裝

```bash
dotnet --version
# 預期輸出: 10.0.x 或更高版本
```

### A.3 選擇編輯器

| IDE | 平台 | 說明 | 備註
|-----|------|------| ----|
| **Visual Studio Code** | 跨平台 | 輕量級，需安裝 C# Dev Kit 擴充套件 | 推薦 |
| **Visual Studio** | Windows | 完整功能的 IDE，包含除錯工具 ||
| **JetBrains Rider** | 跨平台 | 商業軟體，優秀的 .NET 支援 ||

### A.4 驗證環境

```bash
dotnet new console -o TestProject && cd TestProject && dotnet run
# 預期輸出: Hello, World!

# 清理
cd .. && rm -rf TestProject
```

---

## B. Docker

適合系統整合商（SI）。不需要安裝 .NET SDK，透過 `docker compose up` 直接執行 SubNode 應用程式。

### B.1 安裝 Docker

- **Windows/macOS**: 安裝 [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- **Linux**: 依照 [Docker 官方指示](https://docs.docker.com/engine/install/)

### B.2 驗證安裝

```bash
docker --version
# 預期輸出: Docker version 28.x 或更高版本

docker compose version
# 預期輸出: Docker Compose version v2.x
```

### B.3 執行範例

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node/examples/feature-transform-pipeline

# 使用 Docker Compose 啟動範例
docker compose up
```

Docker 會自動下載所需映像檔並啟動 SubNode 應用程式，無需本機安裝 .NET SDK。

---

## C. VS Code + Dev Container

適合沒有 .NET 經驗的開發者。所有開發工具（.NET SDK、NuGet feed、擴充套件）預裝在 container 中，開箱即用。

### C.1 安裝 Docker

同 [B.1](#b1-安裝-docker)。

### C.2 安裝 VS Code

從 [code.visualstudio.com](https://code.visualstudio.com/) 下載並安裝。

### C.3 安裝 Dev Containers 擴充套件

在 VS Code 中安裝 **Dev Containers** 擴充套件（Microsoft 發行）。

### C.4 開啟專案

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
code WEDA-Sub-Node
```

VS Code 偵測到 `.devcontainer/` 設定後會提示 **Reopen in Container**，點擊即可。Container 內已預裝：

- .NET 10.0 SDK
- NuGet 套件來源設定
- C# Dev Kit 擴充套件
- Git

### C.5 驗證環境

在 VS Code 終端機中：

```bash
dotnet --version
# 預期輸出: 10.0.x
```

---

## 硬體（選用）

用於實體裝置測試：

| 裝置類型 | 範例 | 協定 |
|----------|------|------|
| Modbus TCP 裝置 | WISE-4012、ADAM-6017 | Modbus TCP |
| MQTT 裝置 | 任何支援 MQTT 的感測器 | MQTT |

在沒有硬體的情況下開發，可使用內建的 **Modbus Simulator**（請參閱[使用範本開始](./03-start-with-template.md)）。

## Summary

| 方案 | 需安裝 | 適合對象 | 開發體驗 |
|------|--------|----------|----------|
| A. .NET SDK + Editor | .NET SDK | .NET 開發者 | 本機編譯、除錯 |
| B. Docker | Docker | SI 系統整合商 | `docker compose up` 直接執行 |
| C. VS Code + Dev Container | Docker + VS Code + Dev Container Extensions | 無 .NET 經驗者 | Container 內開發，零設定 |

## See Also

- [使用範例開始](./02-start-with-example.md) - 執行預建範例
- [使用範本開始](./03-start-with-template.md) - 從範本建立新專案
- [連接到 WedaCore](./04-connect-to-wedacore.md) - 設定雲端連線

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-25 | Rain Hu | Doc created. |
