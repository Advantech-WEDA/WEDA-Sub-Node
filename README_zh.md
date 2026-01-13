[English](README.md) / 繁體中文

# Weda SubNode SDK

專為 IoT 邊緣設備開發的完整 .NET SDK，提供設備連接、資料採集、處理與雲端整合。

## 事前準備

選擇以下其中一種環境設定方式：

### 方式 1: Dev Container（推薦）

預先配置好的開發環境，一切都已安裝：

**需求:**
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Dev Containers 擴充套件](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

**設定步驟:**
1. 用 VS Code 開啟此專案
2. 按 `Ctrl + Shift + P` → 選擇 `Dev Containers: Reopen in Container`
3. 等待容器建置完成（首次使用）

所有工具都已預先安裝：.NET SDK、專案範本以及所有開發工具！

**[了解更多 Dev Container 資訊](.devcontainer/README.md)**

### 方式 2: 本地安裝

在本機手動安裝相依套件：

**需求:**
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 程式碼編輯器（VS Code、Visual Studio 或 Rider）
- Git（用於複製儲存庫）

**設定步驟:**
請參閱[安裝指南](docs/wiki/zh/01_quick_start/01_install_templates.md)取得詳細說明。

---

## 快速開始

根據您的背景選擇學習路徑：

### 路徑 1: 從零開始（適合 .NET 開發者）

**適合對象:** 熟悉 .NET 且想從頭建立 SubNode 應用程式的開發者。

**步驟:**
1. 安裝範本:
   ```bash
   dotnet new install ./templates
   ```

2. 建立新專案:
   ```bash
   # 簡單應用程式（Builder pattern）
   dotnet new wedabuilder -n MyProject

   # 自訂裝置實作（Manual Context）
   dotnet new subnode -n MyProject
   ```

3. 遵循文件:
   - [快速開始指南](docs/wiki/zh/01_quick_start/00_overview.md)
   - [SubNode 基礎](docs/wiki/zh/01_quick_start/03_subnode_basic.md)
   - [WedaBuilder 基礎](docs/wiki/zh/01_quick_start/02_wedabuilder_basic.md)

### 路徑 2: 從範例學習（適合系統整合商）

**適合對象:** 想快速調整現有範例以適應自己硬體的系統整合商。

**步驟:**
1. 瀏覽可用範例:
   - [examples/](examples/) - 生產就緒範例（真實硬體）
   - [tutorials/](tutorials/) - 學習範例（模擬器）

2. 選擇符合您使用案例的範例:
   - **WISE-4012 工業 I/O**: [examples/wise-4012-builder/](examples/wise-4012-builder/)
   - **Transform & DSP Filters**: [tutorials/01-transform-dsp-config/](tutorials/01-transform-dsp-config/)

3. 複製並自訂:
   ```bash
   # 複製範例
   cp -r examples/wise-4012-builder my-project
   cd my-project

   # 編輯配置
   nano appsettings.json
   # 更新: IP 位址、sensor 對應、裝置設定

   # 執行
   dotnet run
   ```

4. 調整至您的硬體:
   - 修改 `appsettings.json` 中的裝置 IP 和 sensor 配置
   - 如需要，更新 DTDL 中繼資料
   - 在裝置類別中新增自訂處理邏輯

**[瀏覽所有範例](examples/README_zh.md)** | **[瀏覽所有教學](tutorials/README_zh.md)**

---

## 文件

**[閱讀完整文件](docs/wiki/zh/README.md)**

### 主要文件章節

- **快速開始**: 快速上手
  - [概覽](docs/wiki/zh/01_quick_start/00_overview.md)
  - [安裝範本](docs/wiki/zh/01_quick_start/01_install_templates.md)
  - [WedaBuilder 基礎](docs/wiki/zh/01_quick_start/02_wedabuilder_basic.md)
  - [SubNode 基礎](docs/wiki/zh/01_quick_start/03_subnode_basic.md)

- **範例與教學**:
  - [生產範例](examples/README_zh.md) - 真實硬體整合
  - [學習教學](tutorials/README_zh.md) - 循序漸進指南

- **進階主題**:
  - [配置參考](docs/wiki/zh/03_advanced/appsettings_configuration.md)
  - [Transform & DSP Filters](docs/wiki/zh/03_advanced/transform_dsp.md)
  - [DTDL 整合](docs/wiki/zh/03_advanced/dtdl_integration.md)

---

## 功能特色

- **多協定支援** - Modbus TCP/RTU、MQTT
- **資料處理管道** - 校準、濾波、轉換
- **事件驅動架構** - 可擴展的事件系統
- **雲端整合** - Weda EdgeSync Cloud 平台
- **簡單易用** - ASP.NET Core 風格的 Builder pattern

---

## 專案結構

```
edge_subnode/
├── examples/          # 生產就緒範例（真實硬體）
├── tutorials/         # 學習教學（Mock Cloud + 模擬器）
├── templates/         # dotnet new 範本
│   ├── subnode/       # Manual Context pattern
│   └── wedabuilder/   # Builder pattern
├── src/               # SDK 原始碼
├── tests/             # 單元測試與整合測試
└── docs/              # 文件 wiki
```

---

## 取得協助

- **文件**: [docs/wiki/zh/README.md](docs/wiki/zh/README.md)
- **範例**: [examples/README_zh.md](examples/README_zh.md)
- **教學**: [tutorials/README_zh.md](tutorials/README_zh.md)

---

## 授權

Copyright © 2025 Advantech Corporation

---

**版本**: 0.0.1 | **維護者**: Rain Hu
