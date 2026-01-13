---
title: "安裝模板"
description: "使用 dotnet CLI 安裝 Weda SubNode SDK 專案模板"
author: "Rain Hu"
date: "2025-11-07"
lang: "zh"
parent: "README"
prev: "00_overview"
next: "02_subnode_basic"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/01_install_templates.md"
---

# 安裝模板

學習如何安裝兩種 Weda SubNode SDK 專案模板。

**所需時間**：3 分鐘
**難度**：初學者

---

## 先決條件

安裝模板前，請確認您已安裝：

- **.NET 10.0 SDK** 或更新版本
  ```bash
  dotnet --version  # 應輸出 9.0.x 或更高版本
  ```

如果尚未安裝 .NET 10.0 SDK：
- **macOS**: `brew install dotnet@9`
- **Windows**: 從 [dotnet.microsoft.com](https://dotnet.microsoft.com/download) 下載
- **Linux**: 參考 [Microsoft 安裝說明](https://learn.microsoft.com/zh-tw/dotnet/core/install/linux)

---

## 安裝方式

有兩種方式可以安裝模板：

### 方式 1：使用安裝腳本（推薦）

導航到 SDK repository 根目錄並執行：

```bash
bash scripts/install-templates.sh
```

**預期輸出**：
```
════════════════════════════════════════════════════════════════
 Weda SubNode SDK - Template Installation
════════════════════════════════════════════════════════════════

Installing templates from: /path/to/edge_subnode/templates

→ Installing template: subnode
Success: Weda.SubNode.CustomDevice installed successfully

→ Installing template: wedabuilder
Success: Weda.SubNode.WebApi installed successfully

════════════════════════════════════════════════════════════════
✓ Successfully installed 2 templates
════════════════════════════════════════════════════════════════

Available templates:
- subnode    : Console App 風格（單一裝置開發/調試）
- wedabuilder    : Web API 風格（production 多裝置）

Verify installation: dotnet new list | grep -i subnode
```

### 方式 2：手動安裝

如果腳本無法運作，可手動安裝每個模板：

```bash
cd /path/to/edge_subnode/templates

# 安裝 subnode 模板
dotnet new install ./subnode

# 安裝 wedabuilder 模板
dotnet new install ./wedabuilder
```

---

## 驗證安裝

檢查所有模板是否已安裝：

```bash
dotnet new list | grep -i subnode
```

**預期輸出**：
```
Template Name                         Short Name   Language  Tags
------------------------------------  -----------  --------  -----------------------
Weda SubNode Console Application      subnode      [C#]      Console/IoT/Weda/SubNode
Weda SubNode Web API Application      wedabuilder      [C#]      Web/WebAPI/IoT/Weda/SubNode
```

如果看到兩個模板都列出來，表示安裝成功！✅

---

## 模板詳細資訊

安裝後，您可以使用：

### 1. subnode - Console 應用程式模板

**簡稱**：`subnode`
**用法**：`dotnet new subnode -n MyDevice`
**風格**：Console App

建立的專案包含：
- 繼承自 `TcpModbusDevice` 的自訂裝置類別
- 事件驅動架構（`OnDataReceived`）
- 適合單一裝置開發與調試

### 2. wedabuilder - Web API 模板

**簡稱**：`wedabuilder`
**用法**：`dotnet new wedabuilder -n MyApp`
**風格**：Web API

建立的專案包含：
- 使用 `CreateDefaultBuilder` 模式的最少程式碼
- 設定檔驅動（`appsettings.json`）
- 自動初始化與裝置管理
- 適合 production 部署多裝置

---

## 快速測試

透過建立範例專案並實際執行來測試安裝：

### 步驟 1: 建立測試專案

```bash
# 建立臨時目錄
mkdir tmp
cd tmp

# 使用 subnode 模板建立專案
dotnet new subnode -n QuickTest

# 進入專案目錄
cd QuickTest
```

### 步驟 2: 執行專案

```bash
# 直接執行專案（會自動 build）
dotnet run
```

### 步驟 3: 驗證成功

**預期輸出** - 您應該會看到：

```
[12:34:54 INF] Initialized sensor TemperatureSensor (Temperature): Address=0, InitialValue=25.00 °C
[12:34:54 INF] Starting Modbus TCP Simulator on 127.0.0.1:5020 (Slave ID: 1)
[12:34:54 INF] Modbus TCP Simulator started successfully
[12:34:54 INF] ────────────────────────────────────────────────────────
[12:34:54 INF] Modbus batch optimization ENABLED for device  (1 sensors)
[12:34:54 INF] Initializing device 
[12:34:54 INF] Connecting to TCP at 127.0.0.1:5020
[12:34:54 INF] Communication state changed from Disconnected to Connecting
[12:34:54 INF] Communication state changed from Connecting to Connected
[12:34:54 INF] Connected to TCP at 127.0.0.1:5020
[12:34:54 INF] Physical device connected successfully
[12:34:54 INF] Client connected from 127.0.0.1:63410
[12:34:54 INF] Cloud service connected successfully
[12:34:54 INF] All connections established successfully
...
[12:34:54 INF] Starting telemetry task with period 5000ms
[12:34:54 INF] Optimized 1 sensors into 1 batch(es) for HoldingRegister
[12:34:54 INF] temperature.sensor: 25
[12:34:59 INF] Optimized 1 sensors into 1 batch(es) for HoldingRegister
[12:34:59 INF] temperature.sensor: 25.178045
```

**成功指標** ✅：
1. 看到 Modbus simulator 啟動訊息
2. 看到裝置連線成功訊息
3. **看到定時報送的 telemetry 資料**
4. 溫度值在 18-32°C 之間變化

### 步驟 4: 停止並清理

按 `Ctrl+C` 停止程式，然後刪除測試專案：

```bash
# 停止程式後
cd /tmp
rm -rf QuickTest
```

**🎉 如果您看到定時報送的 telemetry，恭喜！您已成功建立第一個 SubNode！**

---

### 快速測試選項 2: 嘗試使用 wedabuilder

```bash
mkdir tmp
cd tmp
dotnet new wedabuilder -n QuickTest
cd QuickTest

# 執行 API
dotnet run
```

**預期輸出**：
```
[12:34:56 INF] Now listening on: http://localhost:5000
[12:34:56 INF] Application started. Press Ctrl+C to shut down.
```

打開瀏覽器訪問 `http://localhost:5000` 應該會看到 API 回應。

---

## 疑難排解

### 問題 1：找不到模板

**症狀**：
```
No templates or subcommands found matching: 'subnode'
```

**解決方式**：
1. 確認您在正確的目錄
2. 重新執行安裝腳本
3. 嘗試手動安裝方式

### 問題 2：權限被拒絕

**症狀**：
```
bash: permission denied: install-templates.sh
```

**解決方式**：
讓腳本可執行：
```bash
chmod +x scripts/install-templates.sh
bash scripts/install-templates.sh
```

### 問題 3：找不到 .NET 10.0 SDK

**症狀**：
```
dotnet: command not found
```

**解決方式**：
安裝 .NET 10.0 SDK：
- macOS: `brew install dotnet@9`
- Windows/Linux: 從 [dotnet.microsoft.com](https://dotnet.microsoft.com/download) 下載

### 問題 4：舊版本模板

**症狀**：模板存在但建立的程式碼已過時

**解決方式**：
先解除安裝舊模板：
```bash
# 列出已安裝的模板
dotnet new uninstall

# 解除安裝特定模板
dotnet new uninstall Weda.SubNode.Console
dotnet new uninstall Weda.SubNode.WebApi

# 重新安裝
bash scripts/install-templates.sh
```

---

## 更新模板

當 repository 中的模板更新時：

```bash
# 導航到 SDK repository
cd /path/to/edge_subnode

# 拉取最新變更
git pull

# 重新安裝模板
bash scripts/install-templates.sh
```

安裝腳本會自動處理模板更新。

---

## 解除安裝模板

移除所有 Weda SubNode 模板：

```bash
dotnet new uninstall Weda.SubNode.Console
dotnet new uninstall Weda.SubNode.WebApi
```

驗證移除：
```bash
dotnet new list | grep -i subnode
# 應該沒有結果
```

---

## 下一步

模板已安裝，選擇您的路徑：

### 推薦給初學者
**[→ wedabuilder - 簡易 API](03_wedabuilder_basic.md)**
- 最快速的入門方式
- 零程式碼需求
- 設定檔驅動

### 需要單一裝置開發
**[→ subnode - Console App](02_subnode_basic.md)**
- Console 應用程式風格
- 事件驅動架構
- 適合開發與調試

---

## 總結

您已成功安裝兩種 Weda SubNode SDK 模板：

- ✅ `subnode` - Console App 模板（單一裝置開發/調試）
- ✅ `wedabuilder` - Web API 模板（production 多裝置）

**準備好開始建置了嗎？** 從上方選擇一個模板指南，開始建立您的第一個 IoT 邊緣裝置！

---

**版本**: 1.0.0
**最後更新**: 2025-11-07
**維護者**: Rain Hu
