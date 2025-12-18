# Tutorials

本目錄包含 EdgeSync SubNode Framework 的進階教學和實作範例。

## 📚 教學列表

### 1. [Advanced Pipeline Configuration](./advanced_pipeline_configuration.md)
深入探討 Telemetry 處理管道的配置和動態更新。

**涵蓋內容**:
- Transform Pipeline (Calibration, UnitConversion)
- DSP Filter Pipeline (KalmanFilter, MovingAverage, ReLU)
- 動態配置更新機制
- 參數驗證與錯誤處理
- Rollback 機制
- 最佳實踐

**適合對象**:
- 需要進階數據處理的開發者
- 需要動態調整感測器配置的應用場景
- 需要了解配置更新機制的系統管理員

---

## 🧪 測試腳本

所有測試腳本位於 `examples/testdevice/` 目錄:

### 成功案例測試
```bash
cd examples/testdevice
./test-pipeline-success.sh
```

**測試內容** (10 個測試案例):
- ✅ 更新 Transform 參數
- ✅ 啟用/禁用 Transform
- ✅ 更新 DSP Filter 參數
- ✅ 同時更新多個 Pipeline
- ✅ 同時更新多個 Sensor
- ✅ 配置還原

### 失敗案例測試
```bash
cd examples/testdevice
./test-pipeline-failures.sh
```

**測試內容** (10 個失敗案例):
- ❌ 缺少必要參數
- ❌ 參數值為空字串
- ❌ 參數值為負數
- ❌ 參數值超出範圍
- ❌ 類型不匹配 (會被跳過)
- ❌ 多 Sensor 部分失敗 (Atomic Rollback)
- ❌ Scale 為 0
- ❌ Threshold 為負數
- ❌ 缺少所有參數

每個失敗案例都會驗證:
1. 配置更新被拒絕
2. 返回 `status: "failed"` 和錯誤訊息
3. 配置自動 rollback 到更新前狀態

---

## 🚀 快速開始

### 前置準備

1. **啟動 NATS Server**:
```bash
nats-server -js
```

2. **啟動 testdevice**:
```bash
cd examples/testdevice
dotnet run
```

### 執行測試

在新的終端視窗執行測試腳本:

```bash
# 測試成功案例
./test-pipeline-success.sh

# 測試失敗案例
./test-pipeline-failures.sh
```

### 查看結果

觀察 testdevice 的日誌輸出,應該會看到:

**成功案例**:
```
[INFO] Applying configuration update for device: Test789
[INFO] Updated 1 sensors: temperature.sensor
[INFO] Updated pipelines - DSP: 1 sensors, Transform: 1 sensors
[DEBUG] Sensor 'temperature.sensor' Transform updates: 1 transforms updated
[DEBUG] Sensor 'temperature.sensor' DSP updates: 1 filters updated
[INFO] Configuration cached to: /path/to/cache.json
[INFO] Configuration update completed successfully
```

**失敗案例**:
```
[INFO] Validating configuration update for device: Test789
[ERROR] Pipeline update validation failed: Transform at index 0: FromUnit parameter is required
[INFO] Configuration rolled back to previous state
[INFO] Sending failed status response to cloud
```

---

## 📖 相關文件

- [Telemetry Event Hooks](../wiki/zh/03_advanced/telemetry_event_hooks.md) - 事件監控機制
- [Transformation & DSP Filter](../wiki/zh/02_use_cases/02_transformation_dspfilter.md) - Transform 和 Filter 使用案例
- [Configuration Cache System](../wiki/zh/02_use_cases/09_configuration_cache.md) - 配置持久化機制

---

## 🔧 自定義測試

您可以參考測試腳本的格式來創建自己的配置更新測試:

```bash
# 基本格式
nats pub "eco1j.weda.dm.config.{deviceId}.req" '{
  "deviceId": "{deviceId}",
  "cmd": "updateCmd",
  "seqId": 1,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "{SubNodeTypeName}": {
              "deviceName": "{deviceName}",
              "sensors": [
                {
                  "name": "sensor.name",
                  "config": {
                    "transformPipeline": [...],
                    "dspPipeline": [...]
                  }
                }
              ]
            }
          }
        }
      }
    }
  }
}'
```

---

## 💡 提示

1. **執行順序**: 建議先執行成功案例測試,再執行失敗案例測試
2. **日誌等級**: 設定日誌等級為 `Debug` 以查看詳細的 pipeline 更新資訊
3. **監控事件**: 啟用 `EnableValueChangeTracking` 來監控 Transform/Filter 的值變化
4. **配置備份**: 每次更新前都會自動建立備份,失敗時會自動 rollback

---

## 🐛 故障排除

### 問題: 測試腳本無法執行
**解決方案**: 確認腳本有執行權限
```bash
chmod +x test-pipeline-*.sh
```

### 問題: NATS 連接失敗
**解決方案**: 確認 NATS Server 正在運行
```bash
nats-server -js
```

### 問題: 配置更新無反應
**解決方案**: 檢查 device ID 和 device name 是否正確匹配

### 問題: Rollback 未生效
**解決方案**: 查看日誌確認錯誤原因,確保驗證階段有檢測到錯誤

---

## 📝 回饋

如有問題或建議,請提交 Issue 或 Pull Request。