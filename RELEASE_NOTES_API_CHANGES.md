# API 變更發布說明 - Transform & DSP Filter

## 版本

- **變更版本**: v1.1.0
- **發布日期**: 2025-11-17

## 變更摘要

本次更新改進了 Transform 和 DSP Filter 的 API 設計，解決了兩個關鍵問題：

1. **移除冗餘的 Order 參數**：執行順序現在由添加順序（programmatic）或 array index（config）自動決定
2. **引入分離的 Builder API**：避免混用 `AddTransform()` 和 `AddDspFilter()` 造成的混淆

##Breaking Changes

### 1. Order 參數已移除

**影響範圍**: appsettings.json 配置文件

**舊版配置**:
```json
{
  "TransformPipeline": [
    {
      "Type": "Calibration",
      "Enabled": true,
      "Order": 0,  ← 已移除
      "Parameters": { "Scale": 1.0, "Offset": 0.0 }
    }
  ]
}
```

**新版配置**:
```json
{
  "TransformPipeline": [
    {
      "Type": "Calibration",
      "Enabled": true,
      "Parameters": { "Scale": 1.0, "Offset": 0.0 }
    }
  ]
}
```

**遷移動作**: 從所有 `appsettings.json` 中移除 `"Order"` 欄位

### 2. 類別變更

**TransformConfig** (src/Weda.SubNode.Abstractions/Telemetry/SensorConfig.cs):
- ❌ 移除: `public int Order { get; set; }`
- ✅ 執行順序由 JSON array index 決定

**DspFilterConfig** (src/Weda.SubNode.Abstractions/Telemetry/SensorConfig.cs):
- ❌ 移除: `public int Order { get; set; }`
- ✅ 執行順序由 JSON array index 決定

### 3. Factory 行為變更

**TransformFactory.CreateFromConfigs()**:
- ❌ 移除: `.OrderBy(c => c.Order)` 排序
- ✅ 直接使用 array 順序處理

**DspFilterFactory.CreateFromConfigs()**:
- ❌ 移除: `.OrderBy(c => c.Order)` 排序
- ✅ 直接使用 array 順序處理

## 新功能

### 新增分離的 Builder API

#### 新增類別

**TransformPipelineBuilder**:
```csharp
public class TransformPipelineBuilder
{
    public TransformPipelineBuilder Add(ITelemetryTransform transform);
    public TransformPipelineBuilder Add(params ITelemetryTransform[] transforms);
    public TransformPipelineBuilder Clear();
}
```

**DspFilterPipelineBuilder**:
```csharp
public class DspFilterPipelineBuilder
{
    public DspFilterPipelineBuilder Add(IDspFilter filter);
    public DspFilterPipelineBuilder Add(params IDspFilter[] filters);
    public DspFilterPipelineBuilder Clear();
}
```

#### 新增方法 (SensorConfig)

```csharp
// ✅ 推薦：使用分離的 Builder API
public SensorConfig ConfigureTransforms(Action<TransformPipelineBuilder> configure);
public SensorConfig ConfigureDspFilters(Action<DspFilterPipelineBuilder> configure);
```

#### 標記為 Obsolete (但保留向後兼容)

```csharp
// ⚠️ Obsolete：仍可使用，但會有編譯警告
[Obsolete("Consider using ConfigureTransforms() for clearer API")]
public SensorConfig AddTransform(ITelemetryTransform transform);

[Obsolete("Consider using ConfigureDspFilters() for clearer API")]
public SensorConfig AddDspFilter(IDspFilter filter);
```

## 遷移指南

### Programmatic API 遷移

#### 選項 1: 使用新 API（推薦）

**舊版**:
```csharp
sensor.Config
    .AddTransform(new CalibrationTransform(1.0, 0.0))
    .AddTransform(new UnitConversionTransform("*", "celsius", "fahrenheit"))
    .AddDspFilter(new MovingAverageFilter(5));
```

**新版**:
```csharp
sensor.Config
    .ConfigureTransforms(t =>
    {
        t.Add(new CalibrationTransform(1.0, 0.0));
        t.Add(new UnitConversionTransform("*", "celsius", "fahrenheit"));
    })
    .ConfigureDspFilters(f =>
    {
        f.Add(new MovingAverageFilter(5));
    });
```

#### 選項 2: 繼續使用舊 API（不推薦）

舊 API 仍可使用，但會產生 Obsolete 警告：

```csharp
// ⚠️ 警告: CS0618: 'SensorConfig.AddTransform(ITelemetryTransform)' is obsolete
sensor.Config
    .AddTransform(new CalibrationTransform(1.0, 0.0))
    .AddDspFilter(new MovingAverageFilter(5));
```

**重要**: 使用舊 API 時，**不要混用** `AddTransform()` 和 `AddDspFilter()`：

```csharp
// ❌ 錯誤：混用會造成混淆
sensor.Config
    .AddTransform(new CalibrationTransform(...))
    .AddDspFilter(new MovingAverageFilter(...))
    .AddTransform(new UnitConversionTransform(...));  // 會比 DSP Filter 先執行！

// ✅ 正確：所有 Transform 先添加，然後才添加 DSP Filter
sensor.Config
    .AddTransform(new CalibrationTransform(...))
    .AddTransform(new UnitConversionTransform(...))
    .AddDspFilter(new MovingAverageFilter(...));
```

### Config 文件遷移

#### 自動遷移腳本

```bash
# 使用 sed 移除所有 Order 欄位（macOS/Linux）
find . -name "appsettings*.json" -exec sed -i.bak 's/"Order": [0-9]*,\?//g' {} \;

# 清理備份檔案
find . -name "appsettings*.json.bak" -delete
```

#### 手動遷移

從所有 JSON 配置檔案中移除 `"Order"` 欄位：

**Before**:
```json
{
  "TransformPipeline": [
    { "Type": "Calibration", "Enabled": true, "Order": 0, "Parameters": {...} },
    { "Type": "UnitConversion", "Enabled": true, "Order": 1, "Parameters": {...} }
  ]
}
```

**After**:
```json
{
  "TransformPipeline": [
    { "Type": "Calibration", "Enabled": true, "Parameters": {...} },
    { "Type": "UnitConversion", "Enabled": true, "Parameters": {...} }
  ]
}
```

## 影響範圍

### 需要更新的檔案

1. **所有 appsettings.json 檔案**
   - `appsettings.json`
   - `appsettings.template.json`
   - `appsettings.Development.json`
   - `appsettings.Production.json`

2. **程式碼檔案（可選）**
   - 使用 `AddTransform()` 和 `AddDspFilter()` 的程式碼
   - 建議遷移到新 API，但舊 API 仍可使用

### 不受影響的部分

- ✅ Config-based pipeline 載入邏輯（自動處理）
- ✅ Transform 和 DSP Filter 執行邏輯
- ✅ 所有 Transform 和 DSP Filter 實作
- ✅ DTDL 定義
- ✅ 測試程式碼（除非直接測試 Order 參數）

## 測試建議

### 1. 單元測試

確認執行順序正確：

```csharp
[Fact]
public void Transform_ExecutionOrder_ShouldMatchArrayIndex()
{
    var sensor = new ModbusSensorConfiguration();
    
    sensor.Config
        .ConfigureTransforms(t =>
        {
            t.Add(new CalibrationTransform(2.0, 0));   // 應先執行
            t.Add(new CalibrationTransform(1.0, 10));  // 應後執行
        });
    
    // Verify execution order matches add order
}
```

### 2. Integration 測試

驗證 config-based pipeline：

```json
{
  "TransformPipeline": [
    { "Type": "Calibration", "Enabled": true, "Parameters": { "Scale": 2.0 } },
    { "Type": "Calibration", "Enabled": true, "Parameters": { "Offset": 10.0 } }
  ]
}
```

### 3. 回歸測試

確認移除 Order 後行為一致：
- Transform pipeline 執行順序
- DSP Filter pipeline 執行順序
- Mixed pipeline（Transform → DSP） 執行順序

## 文檔更新

已更新的文檔：

1. ✅ `docs/wiki/zh/03_advanced/appsettings_configuration.md` - 移除所有 Order 參數範例
2. ✅ `docs/API_CHANGES.md` - 詳細的 API 變更說明
3. 🔄 `docs/wiki/zh/02_use_cases/02_transformation_dspfilter.md` - 使用新 API 的範例（進行中）
4. 🔄 `examples/transform-dsp-programmatic/` - 更新為新 API（待更新）
5. 🔄 `examples/transform-dsp-config/` - 移除 Order 參數（待更新）

## 常見問題

### Q: 為什麼要移除 Order 參數？

**A**: Order 參數是冗餘的：
- Programmatic API：執行順序就是 Add 的順序
- Config API：執行順序就是 JSON array 的 index
- 保留 Order 參數反而容易造成混淆

### Q: 我的舊程式碼還能用嗎？

**A**: 可以！舊的 `AddTransform()` 和 `AddDspFilter()` 仍然有效，只會產生 Obsolete 警告。但建議遷移到新 API 以獲得更清晰的程式碼。

### Q: 新 API 有什麼好處？

**A**:
1. **清楚分離**：Transform 和 DSP Filter 明確分開配置
2. **避免混淆**：不會誤解執行順序
3. **批次添加**：支援 `Add(params T[] items)` 一次添加多個
4. **編譯時提醒**：舊 API 會有 Obsolete 警告

### Q: 如何清除已添加的 Transform/DSP？

**A**:
```csharp
// 新 API
sensor.Config
    .ConfigureTransforms(t => t.Clear())
    .ConfigureDspFilters(f => f.Clear());

// 舊 API（仍可用）
sensor.Config.ClearTransforms();
sensor.Config.ClearDspFilters();
```

### Q: 未來會移除舊 API 嗎？

**A**: 目前計劃：
- **v1.x**: Order 參數移除，舊 API 標記 Obsolete
- **v2.x**: 可能移除 `AddTransform()` 和 `AddDspFilter()`
- 會提前至少一個大版本通知

## 取得協助

- **API 變更詳細說明**: [docs/API_CHANGES.md](docs/API_CHANGES.md)
- **設定指南**: [docs/wiki/zh/03_advanced/appsettings_configuration.md](docs/wiki/zh/03_advanced/appsettings_configuration.md)
- **使用範例**: [docs/wiki/zh/02_use_cases/02_transformation_dspfilter.md](docs/wiki/zh/02_use_cases/02_transformation_dspfilter.md)
- **GitHub Issues**: 回報問題或提供回饋

---

**變更作者**: Weda SubNode SDK Team  
**審核者**: Rain Hu  
**發布日期**: 2025-11-17
