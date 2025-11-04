# Modbus Batch Reading Optimization

## 概述

Modbus 批次讀取優化功能可將多個連續暫存器的讀取合併為單一 Modbus 請求，大幅減少網路往返次數並提升效能。

## 問題背景

### 傳統單點採集問題

在傳統的實作中，每個感測器都會獨立發送一個 Modbus 請求：

```
Sensor1 (Address 0) → Modbus Request 1
Sensor2 (Address 1) → Modbus Request 2
Sensor3 (Address 2) → Modbus Request 3
Sensor4 (Address 3) → Modbus Request 4
Sensor5 (Address 4) → Modbus Request 5
```

**問題：**
- 需要 5 次網路往返 (Round-trips)
- 總耗時 = 5 × 網路延遲
- 浪費頻寬和時間

### 批次讀取解決方案

批次讀取可將連續的暫存器合併為單一請求：

```
Sensors 1-5 (Address 0-4) → Modbus Batch Request (讀取 5 個暫存器)
```

**優勢：**
- 只需 1 次網路往返
- 總耗時 = 1 × 網路延遲
- 效能提升 80-98%

## 效能測試結果

基於 10ms 網路延遲的模擬測試：

| 感測器數量 | 傳統請求數 | 批次請求數 | 請求減少率 | 時間改善 |
|------------|-----------|-----------|-----------|----------|
| 5          | 5         | 1         | 80.0%     | 79.6%    |
| 10         | 10        | 1         | 90.0%     | 90.8%    |
| 20         | 20        | 1         | 95.0%     | 89.5%    |
| 50         | 50        | 1         | 98.0%     | 98.2%    |
| 8 (有間隙)  | 8         | 3         | 62.5%     | -        |

## 使用方式

### 1. 預設啟用批次優化

```csharp
var device = new ModbusDevice(context, configuration, communication);
// 批次優化預設為啟用
```

### 2. 停用批次優化（傳統模式）

```csharp
var device = new ModbusDevice(
    context,
    configuration,
    communication,
    useBatchOptimization: false);
```

### 3. 自訂優化選項

```csharp
var device = new ModbusDevice(
    context,
    configuration,
    communication,
    useBatchOptimization: true,
    batchOptions: new ModbusBatchOptimizationOptions
    {
        MaxGapSize = 5,      // 最大間隙大小
        MaxBatchSize = 125   // 最大批次大小
    });
```

## 優化選項

### ModbusBatchOptimizationOptions

#### MaxGapSize（最大間隙大小）

定義感測器之間允許的最大間隙（暫存器數量），以便仍將它們合併到同一批次中。

- **Default**: 2
- **Conservative**: 0（不允許間隙）
- **Aggressive**: 5

**範例：**
```
Sensor1: Address 0
Sensor2: Address 3  (間隙 = 3-1 = 2)

如果 MaxGapSize = 2: 合併到同一批次 ✅
如果 MaxGapSize = 1: 分為兩個批次 ❌
```

#### MaxBatchSize（最大批次大小）

單一批次中可讀取的最大暫存器數量。

- **Default**: 125（Modbus 協定限制）
- **Conservative**: 50
- **Aggressive**: 125

## 批次演算法

### 1. 依暫存器類型分組

```csharp
HoldingRegister → Batch Group 1
InputRegister   → Batch Group 2
```

不同類型的暫存器不能在同一批次中讀取。

### 2. 依地址排序

```csharp
Sensors: [10, 2, 5, 1, 20]
排序後: [1, 2, 5, 10, 20]
```

### 3. 識別連續範圍

```csharp
[1, 2, 5, 10, 20]
 └─┘  │  └──┘  │
  批次1 批次2 批次3 批次4
```

### 4. 決定是否合併

```python
for each sensor:
    gap = current_address - previous_end_address
    potential_size = current_end - batch_start

    if gap <= MaxGapSize AND potential_size <= MaxBatchSize:
        merge into current batch
    else:
        create new batch
```

### 5. 執行批次讀取

```csharp
foreach batch in batches:
    registers = ReadMultipleRegisters(batch.StartAddress, batch.Count)
    distribute results to individual sensors
```

## 實際範例

### 範例 1：連續感測器

**配置：**
```json
{
  "Sensors": [
    { "Name": "Temperature", "Address": 0, "Count": 1 },
    { "Name": "Humidity", "Address": 1, "Count": 1 },
    { "Name": "Pressure", "Address": 2, "Count": 1 }
  ]
}
```

**批次優化：**
```
傳統: 3 個請求 (0, 1, 2)
批次: 1 個請求 (讀取地址 0-2)
改善: 66.7% 減少請求數
```

### 範例 2：有間隙的感測器

**配置：**
```json
{
  "Sensors": [
    { "Name": "Sensor1", "Address": 0, "Count": 1 },
    { "Name": "Sensor2", "Address": 1, "Count": 1 },
    { "Name": "Sensor3", "Address": 10, "Count": 1 },
    { "Name": "Sensor4", "Address": 11, "Count": 1 }
  ]
}
```

**批次優化（MaxGapSize=2）：**
```
傳統: 4 個請求 (0, 1, 10, 11)
批次: 2 個請求 (0-1, 10-11)
改善: 50% 減少請求數
```

### 範例 3：混合暫存器類型

**配置：**
```json
{
  "Sensors": [
    { "Name": "Holding1", "RegisterType": "HoldingRegister", "Address": 0 },
    { "Name": "Input1", "RegisterType": "InputRegister", "Address": 0 },
    { "Name": "Holding2", "RegisterType": "HoldingRegister", "Address": 1 }
  ]
}
```

**批次優化：**
```
批次 1 (HoldingRegister): 讀取地址 0-1
批次 2 (InputRegister): 讀取地址 0
總共: 2 個請求 (從 3 個減少)
```

## 錯誤處理

批次讀取失敗時的處理：

1. **批次層級錯誤**：整個批次失敗，該批次中的所有感測器標記為失敗
2. **感測器層級錯誤**：個別感測器解析失敗，不影響批次中的其他感測器
3. **繼續執行**：一個批次失敗不影響其他批次的執行

```csharp
try
{
    await ExecuteBatchAsync(batch, results, cancellationToken);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error reading batch");

    // 標記批次中所有感測器為失敗
    foreach (var sensor in batch.Sensors)
    {
        results[sensor.Name] = new SensorReadResult
        {
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}
```

## 向後相容性

- ✅ 完全向後相容
- ✅ 可透過建構函式參數停用
- ✅ 不影響現有 API
- ✅ 預設啟用，但可選擇退出

## 建議

### 何時使用批次優化

✅ **建議使用：**
- 多個連續或接近的暫存器
- 相同類型的暫存器（HoldingRegister 或 InputRegister）
- 網路延遲較高的環境
- 需要頻繁讀取的場景

❌ **不建議使用：**
- 感測器地址非常分散（間隙 > 10 個暫存器）
- 只有一個或兩個感測器
- 需要即時讀取個別感測器

### 最佳實踐

1. **配置感測器順序**：盡量讓感測器使用連續的暫存器地址
2. **選擇適當的 MaxGapSize**：
   - 連續設備：使用 Default (2)
   - 分散設備：使用 Conservative (0)
   - 已知佈局：使用 Aggressive (5)
3. **監控效能**：透過日誌查看批次合併情況
4. **測試環境**：先在測試環境驗證批次行為

## 日誌輸出

啟用 Debug 級別日誌可查看詳細的批次資訊：

```
info: Modbus batch optimization ENABLED for device device-123 (5 sensors)
info: Optimized 5 sensors into 1 batch(es) for HoldingRegister
dbug: Finalized batch: StartAddress=0, Count=5, Sensors=5
dbug: Executing batch read: StartAddress=0, Count=5, Sensors=[Temp, Humid, Press, Speed, Torque]
```

## 相關檔案

- **實作**：`src/Weda.SubNode.Core/Protocols/Modbus/ModbusBatchReader.cs`
- **整合**：`src/Weda.SubNode.Core/Devices/ModbusDevice.cs`
- **測試**：`tests/Weda.SubNode.Core.Tests/Protocols/Modbus/ModbusBatchReaderTests.cs`
- **效能測試**：`tests/Weda.SubNode.Core.Tests/Protocols/Modbus/ModbusBatchPerformanceTests.cs`

## 未來改進

可能的未來增強功能：

1. **動態優化**：根據執行時效能自動調整批次策略
2. **統計資訊**：收集批次效能指標
3. **更多暫存器類型**：支援 Coil 和 DiscreteInput 的批次讀取
4. **並行批次**：同時執行多個批次（不同暫存器類型）
5. **快取機制**：快取最近讀取的暫存器值

## 總結

Modbus 批次讀取優化功能透過智慧合併連續暫存器的讀取請求，在不改變現有 API 的情況下，將效能提升了 **80-98%**，大幅減少了網路往返次數和總讀取時間。這對於工業物聯網場景中的 Modbus 設備通訊特別有價值。
