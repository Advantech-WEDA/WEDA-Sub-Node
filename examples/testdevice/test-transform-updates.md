# Transform Real-time Update Test Guide

本文件說明如何測試 UnitConversion Transform 的即時參數調整功能。

## ✅ Bug Fix (2025-11-27)

**問題**: 從 Cloud 發送的 Transform pipeline 配置更新無法生效 (例如將 UnitConversion 從 C→F 改成 C→K)

**根本原因**: `DeviceBase.ApplyBaseConfigurationUpdateAsync()` 只調用了 `ApplySensorConfigUpdates()` 來更新 sensor 基本配置(Enabled, Interval, Unit, Thresholds)，但從未調用 `ApplyAllPipelineUpdates()` 來更新 TransformPipeline 和 DspPipeline 的參數。

**修復方式**: 在 [DeviceBase.cs:340-376](../../src/Weda.SubNode.Core/Devices/DeviceBase.cs#L340-L376) 中加入了 `ApplyAllPipelineUpdates()` 調用，現在配置更新流程如下：

1. 驗證配置更新訊息
2. 發送 "updating" 確認訊息到 Cloud
3. 建立配置備份 (用於 rollback)
4. 套用 sensor 基本配置更新
5. **套用 Transform 和 DSP pipeline 參數更新** ✅ NEW
6. 將配置持久化到 cache
7. 發送 "success" 回報到 Cloud

現在以下的測試案例應該都能正常運作了！

## 前置準備

1. 啟動 testdevice:
```bash
cd examples/testdevice
dotnet run
```

2. 確認初始狀態：溫度感測器應顯示華氏溫度 (約 77°F，原始攝氏約 25°C)

---

## 測試案例

### 測試 1: 關閉 Transformation (Enabled=false)

發送以下 Cloud Configuration Update 訊息：

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 1,
  "reqSeqId": "test-001",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": false,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "celsius",
                      "ToUnit": "fahrenheit"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**: 溫度應顯示原始攝氏值 (約 25°C)，因為 Transform 已暫停 (pass-through)

---

### 測試 2: 重新開啟 Transformation

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 2,
  "reqSeqId": "test-002",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "celsius",
                      "ToUnit": "fahrenheit"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**: 溫度應顯示華氏值 (約 77°F)

---

### 測試 3: 修改成華氏轉攝氏

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 3,
  "reqSeqId": "test-003",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "fahrenheit",
                      "ToUnit": "celsius"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**:
- 如果原始值是攝氏 25°C，轉換結果會是 (25-32)/1.8 = -3.89°C
- 這是因為系統把攝氏值當成華氏來轉換

---

### 測試 4: 修改成絕對溫標 (Kelvin)

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 4,
  "reqSeqId": "test-004",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "celsius",
                      "ToUnit": "kelvin"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**: 溫度應顯示絕對溫度 (約 298K = 25°C + 273.15)

---

### 測試 5: 還原成攝氏溫標

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 5,
  "reqSeqId": "test-005",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "celsius",
                      "ToUnit": "celsius"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**: 溫度應顯示原始攝氏值 (約 25°C)，因為 FromUnit 和 ToUnit 相同，等於 pass-through

---

## 驗證失敗測試

### 測試: 空的 FromUnit

```bash
nats pub "eco1j.weda.dm.config.251583620882366464.req" '{
  "deviceId": "Test789",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 6,
  "reqSeqId": "test-006",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyFirstDeviceConfig": {
              "deviceName": "Test789",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "",
                      "ToUnit": "fahrenheit"
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'
```

**預期結果**: 應該回報 `status: "invalid"` 並顯示 "FromUnit cannot be empty" 錯誤訊息

---

## 轉換公式參考

| From | To | Formula |
|------|------|---------|
| Celsius | Fahrenheit | F = C × 1.8 + 32 |
| Fahrenheit | Celsius | C = (F - 32) / 1.8 |
| Celsius | Kelvin | K = C + 273.15 |
| Kelvin | Celsius | C = K - 273.15 |
