# 教學範例

本目錄包含用於學習 Wed SubNode SDK 的**教學範例**。所有教學都使用 **Mock Cloud** 和 **Simulators**，因此您可以在沒有任何真實硬體或雲端連線的情況下執行。

## 教學結構

教學設計與[文檔 wiki](../docs/wiki/) 相對應：

| 教學 | Wiki 章節 | 說明 |
|------|-----------|------|
| `01-transform-dsp-config` | Transform & DSP (Config) | 使用 appsettings.json 展示 Transform 和 DSP Filter 的用法 |
| `02-transform-dsp-programmatic` | Transform & DSP (Programmatic) | 使用程式碼配置展示 Transform 和 DSP Filter 的用法 |

## Tutorial vs Example

### Tutorials (本目錄)
- **目的**: 循序漸進學習，對應 wiki 文檔
- **環境**: Mock Cloud + Simulators (無需硬體)
- **目標用戶**: 學習 SDK 的開發者
- **程式碼風格**: 註解豐富，教學導向

### Examples (../examples/)
- **目的**: 真實世界整合場景
- **環境**: 真實連線 (TCP/RTU) 或真實硬體
- **目標用戶**: 實作生產方案的開發者
- **程式碼風格**: 生產就緒，實用導向

## 執行教學

每個教學都是獨立的，可以單獨執行：

```bash
cd tutorials/01-transform-dsp-config
dotnet run
```

所有教學都會：
1. 自動啟動 Modbus TCP 模擬器
2. 連接到模擬器 (127.0.0.1:5020)
3. 使用 Mock Cloud (無需網路連線)
4. 顯示帶有說明的輸出

## 學習路徑

我們建議按以下順序學習教學：

1. **從 Examples 開始** - 執行 [examples/wise-4012-builder](../examples/wise-4012-builder) 了解基本裝置連接
2. **Transform Pipeline** - 執行 `01-transform-dsp-config` 學習基於配置的轉換
3. **Programmatic API** - 執行 `02-transform-dsp-programmatic` 學習基於程式碼的配置

## 教學模板結構

每個教學包含：
- `README.md` / `README_zh.md` - 教學說明和學習目標
- `Program.cs` - 帶有詳細註解的主應用程式碼
- `MyFirstDevice.cs` - 自訂裝置實作
- `appsettings.json` - 配置檔案
- `appsettings.template.json` - 自訂模板

## 取得協助

- **文檔**: [Wiki 文檔](../docs/wiki/)
- **問題**: [GitHub Issues](https://github.com/advantech/edge_subnode/issues)
- **範例**: 查看 [examples/](../examples/) 獲取生產場景

## 相關資源

- [Examples 目錄](../examples/) - 真實硬體範例
- [Templates](../templates/) - 專案模板 (subnode, wedabuilder)
- [文檔 Wiki](../docs/wiki/) - 完整 SDK 文檔
