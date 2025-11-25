# 範例

本目錄包含展示 Weda SubNode SDK 真實世界整合場景的**生產就緒範例**。所有範例都使用**真實連線**或**真實硬體**。

## 可用範例

### WISE-4012 Builder 範例

#### [`wise-4012-builder/`](wise-4012-builder/)
使用 Builder pattern 整合 Advantech WISE-4012 工業 I/O 模組。
- **Pattern**: Builder pattern (`WedaApplication.CreateBuilder()`)
- **硬體**: WISE-4012 (4AI + 2AO)
- **協定**: Modbus TCP
- **功能**: 多通道類比 I/O、DTDL 中繼資料、生產就緒
- **使用案例**: 工業監控與控制
- **對應模板**: `wedabuilder`

### 真實硬體範例

#### [`wise-4012/`](wise-4012/)
使用預設 Context 單例整合 Advantech WISE-4012 工業 I/O 模組。
- **Pattern**: 預設單例 (`WedaApplicationContext.Default`)
- **硬體**: WISE-4012 (4AI + 2AO)
- **協定**: Modbus TCP
- **功能**: 多通道類比 I/O、DTDL 中繼資料
- **使用案例**: 工業監控與控制
- **對應模板**: `subnode`

#### [`wise-4012-isensing/`](wise-4012-isensing/)
具有 iSensing 智慧診斷功能的 WISE-4012。
- **硬體**: WISE-4012 with iSensing
- **協定**: Modbus TCP
- **功能**: 進階診斷、異常檢測
- **使用案例**: 預測性維護

## Examples vs Tutorials

### Examples (本目錄)
- **目的**: 真實世界整合場景
- **環境**: 真實連線或真實硬體
- **目標用戶**: 實作生產方案的開發者
- **程式碼風格**: 生產就緒，實用導向

### Tutorials (../tutorials/)
- **目的**: 循序漸進學習，對應 wiki 文檔
- **環境**: Mock Cloud + Simulators (無需硬體)
- **目標用戶**: 學習 SDK 的開發者
- **程式碼風格**: 註解豐富，教學導向

## 選擇正確的範例

### 用於學習
1. 從 **[tutorials/](../tutorials/)** 開始了解基本概念
2. 查看 **[wise-4012-builder](wise-4012-builder/)** 了解生產模式
3. 研究真實硬體範例了解整合細節

### 用於生產
1. 使用 **[wise-4012-builder](wise-4012-builder/)** 作為模板
2. 參考 DTDL 和多感測器模式
3. 為您的特定硬體調整配置

## 執行範例

### 先決條件

1. 已安裝 **.NET 9.0 SDK**
2. **真實硬體**或 **Modbus TCP 伺服器**正在執行
3. **雲端配置** (或使用 Mock Cloud 進行測試)

### 基本步驟

```bash
# 導航到範例目錄
cd examples/wise-4012-builder

# 編輯 appsettings.json 配置連線
nano appsettings.json

# 執行範例
dotnet run
```

### 通用配置

所有範例都使用 `appsettings.json` 進行配置：

```json
{
  "Nats": {
    "Url": "nats://your-cloud-server:4224"
  },
  "DeviceConfigs": {
    "YourDevice": {
      "Communication": {
        "Host": "192.168.1.100",  // 您的裝置 IP
        "Port": 502
      }
    }
  }
}
```

## 範例結構

每個範例通常包含：

- **README.md** / **README_zh.md** - 範例概述和說明
- **Program.cs** - 主應用程式進入點
- **MyDevice.cs** (或類似) - 自訂裝置實作
- **appsettings.json** - 配置檔案
- **[DeviceName].csproj** - 專案檔案

## Pattern 對比

| 功能 | wise-4012 | wise-4012-builder |
|------|-----------|-------------------|
| **Pattern** | Manual Context | Builder Pattern |
| **DI 容器** | 否 | 是 (Microsoft.Extensions.DI) |
| **Hosted Services** | 手動生命週期 | 自動生命週期 |
| **Configuration** | 手動載入 | 自動載入 |
| **Multi-Device** | 手動管理 | 自動管理 |
| **最適合** | 學習、調試 | 生產、擴展 |
| **程式碼行數** | 更明確 | 更簡潔 |

## 取得協助

- **教學**: 查看 [tutorials/](../tutorials/) 獲取學習資源
- **文檔**: [Wiki 文檔](../docs/wiki/)
- **模板**: 使用 `dotnet new subnode` 或 `dotnet new wedabuilder`
- **問題**: [GitHub Issues](https://github.com/advantech/edge_subnode/issues)

## 貢獻

新增範例時：

1. **範例**應使用真實連線或真實硬體
2. 包含完整的 README (英文和中文)
3. 遵循現有專案結構
4. 提交前使用真實硬體測試

對於教學內容 (Mock Cloud + Simulators)，請新增到 [tutorials/](../tutorials/)。

## 相關資源

- [Tutorials 目錄](../tutorials/) - 使用 Mock Cloud 的學習範例
- [Templates](../templates/) - 專案模板 (subnode, wedabuilder)
- [文檔 Wiki](../docs/wiki/) - 完整 SDK 文檔
