[English](README.md) / 繁體中文

# Weda SubNode SDK 開發容器

此開發容器提供了一個完整配置的 Weda SubNode SDK 開發環境，包括：

- **.NET 10.0 SDK** - 最新的 .NET 開發工具
- **Weda SubNode Templates** - 預先安裝的專案範本
- **VS Code 擴充套件** - 預先安裝的 C#、Docker、Git 擴充套件
- **CLI 工具** - Entity Framework CLI、dotnet-format 等

## 快速開始

### 前置需求

- 已安裝 [Docker Desktop](https://www.docker.com/products/docker-desktop)
- 已安裝 [Visual Studio Code](https://code.visualstudio.com/)
- 已安裝 [Dev Containers 擴充套件](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

### 使用開發容器

1. **在 VS Code 中開啟專案**
   ```bash
   code /path/to/edge_subnode
   ```

2. **在容器中重新開啟**
   - 按 `F1` 或 `Cmd/Ctrl+Shift+P`
   - 選擇：`Dev Containers: Reopen in Container`
   - 等待容器建置並啟動（首次可能需要幾分鐘）

3. **開始開發**
   - 容器會自動安裝範本、執行 `dotnet restore` 和 `dotnet build`
   - 您可以立即開始編寫程式碼！

## 包含內容

### 已安裝工具

- **dotnet-format** - 程式碼格式化工具
- **dotnet-outdated-tool** - 檢查過期套件
- **Git** - 版本控制
- **GitHub CLI** - GitHub 命令列工具
- **Weda SubNode Templates** - 預先安裝的專案範本（wedabuilder、subnode）

> **注意**：由於套件問題，`dotnet-ef` 未預先安裝，但如需要可手動安裝：`dotnet tool install --global dotnet-ef`

### VS Code 擴充套件

- **C# Dev Kit** - 完整的 C# 開發體驗
- **GitLens** - Git 增強工具
- **Docker** - Docker 容器管理
- **EditorConfig** - 一致的編碼風格
- **Markdown All in One** - Markdown 支援

## 常用任務

### 建置 Solution

```bash
dotnet build
```

### 執行測試

```bash
dotnet test
```

### 建立新的裝置專案

範本已在開發容器中預先安裝，您可以立即建立專案：

```bash
# 建立 Web API 風格專案（生產環境推薦）
mkdir devices
cd devices
dotnet new wedabuilder -n MyNewDevice
cd ..
dotnet sln add devices/MyNewDevice/MyNewDevice.csproj

# 或建立 Console 風格專案（開發/除錯用）
mkdir devices
cd devices
dotnet new subnode -n MyDebugDevice
cd ..
dotnet sln add devices/MyDebugDevice/MyDebugDevice.csproj

# 列出可用範本
dotnet new list | grep -i weda
```

## 疑難排解

### 容器無法啟動

1. 確保 Docker Desktop 正在執行
2. 嘗試重建容器：
   - `F1` → `Dev Containers: Rebuild Container`
3. 檢查 Docker 日誌以查看錯誤

### 範本安裝失敗

如果範本安裝失敗，您可以手動安裝：
```bash
dotnet new install ./templates
```

### 建置錯誤

如果容器啟動後遇到建置錯誤：
```bash
dotnet clean
dotnet restore
dotnet build
```

## 自訂配置

### 新增更多 VS Code 擴充套件

編輯 `.devcontainer/devcontainer.json`，在 `extensions` 陣列中新增擴充套件：

```json
"extensions": [
  "ms-dotnettools.csharp",
  "your-extension-id-here"
]
```

### 修改 Dockerfile

您可以自訂 Dockerfile 來新增更多工具或變更配置：

```dockerfile
# 新增您的自訂工具
RUN apt-get update && apt-get install -y your-package
```

## 效能提示

### 在 Windows 上使用 WSL2

為了在 Windows 上獲得更好的效能，請使用 WSL2 作為 Docker 後端。

### 排除 bin/obj 資料夾

開發容器已配置為從 VS Code 檔案監視器中排除 `bin/` 和 `obj/` 資料夾，以獲得更好的效能。

### 使用卷掛載

工作區使用 `:cached` 標誌掛載，以在 macOS 上獲得更好的效能。

## 其他資源

- [VS Code Dev Containers 文件](https://code.visualstudio.com/docs/devcontainers/containers)
- [NATS 文件](https://docs.nats.io/)（雖然此容器不包含 NATS，但在生產環境中會用到）
- [.NET 文件](https://docs.microsoft.com/dotnet/)
- [Weda SubNode SDK 文件](../docs/wiki/zh/README.md)

## 與本地開發的差異

使用 Dev Container 與本地開發的主要差異：

| 項目 | 本地開發 | Dev Container |
|------|---------|--------------|
| 環境設定 | 需手動安裝 .NET SDK、Git 等 | 自動配置，無需安裝 |
| 範本 | 需手動安裝 `dotnet new install` | 自動安裝 |
| 路徑 | 本地檔案系統路徑 | `/workspace` 掛載點 |
| DTDL 路徑解析 | 自動從專案目錄解析 | 自動檢測 `/workspace` |
| 隔離性 | 與系統共用環境 | 完全隔離的容器環境 |

## 最佳實踐

### 定期重建容器

為確保使用最新的範本和依賴項，建議定期重建容器：
```
F1 → Dev Containers: Rebuild Container
```

### 使用 Git

容器內的 Git 配置會保留，但建議在容器啟動後設定您的 Git 使用者資訊（如果尚未設定）：
```bash
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

### 善用 VS Code 終端機

在容器中，所有終端機命令都在容器環境內執行，無需擔心本地環境設定。

## 常見問題

**Q: 為什麼不包含 NATS 和 PostgreSQL？**
A: 對於 SubNode SDK 開發，Mock 服務已足夠。實際的 NATS 和資料庫服務應在生產環境或整合測試環境中執行。

**Q: 如何在容器內訪問本地檔案？**
A: 專案資料夾已自動掛載到 `/workspace`，您可以直接訪問所有專案檔案。

**Q: 容器會佔用多少空間？**
A: 初始映像檔約 1-2 GB，但會在多個專案間共用基礎映像檔。

**Q: 可以同時執行多個容器嗎？**
A: 可以！每個專案可以有自己的容器執行個體。

**Q: 如何更新容器內的 SDK 版本？**
A: 修改 `Dockerfile` 中的基礎映像版本（例如 `FROM mcr.microsoft.com/dotnet/sdk:10.0`），然後重建容器。
