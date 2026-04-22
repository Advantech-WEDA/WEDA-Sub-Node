# MetricType 配置說明

使用者透過設定 `MetricType` 指定 System Agent 的採集指標。即使硬體不支援該指標，System Agent 也不會產生異常，只是無法採集對應指標資料。

## Sensor 配置結構

每個 Sensor 在 `devicecfg.json` 中的配置結構如下：

```json
{
  "Name": "cpu_usage",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "cpu",
    "MetricName": "usage"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "SensorInfo Description for cpu.usage",
    "DisplayName": "SensorInfo DisplayName for cpu.usage"
  }
}
```

| 欄位 | 說明 |
|------|------|
| `Name` | Sensor 唯一識別名稱 |
| `SensorGroup` | Sensor 分組（選填） |
| `Parameters.MetricType` | 指標類型（必填） |
| `Parameters.MetricName` | 指標名稱（所有 MetricType 均需要） |
| `Report.Enabled` | 是否啟用 |
| `Report.Interval` | 上報間隔（毫秒） |
| `SensorInfo.Schema` | 預期回傳資料類型（`double`、`long`、`string`、`boolean`、`integer`） |
| `SensorInfo.Description` | Sensor 描述 |
| `SensorInfo.DisplayName` | Sensor 顯示名稱 |

---

## MetricType 分類與設計原則

## 一、系統資源類

支援跨平台（Linux、Windows、macOS），使用標準作業系統 API 獲取

### cpu

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `usage` | 整體 CPU 使用率（由各核心時間計算） | double | % (0-100) |
| `load1` | 1 分鐘負載平均值 | double | - |
| `load5` | 5 分鐘負載平均值 | double | - |
| `load15` | 15 分鐘負載平均值 | double | - |
| `context_switches` | 上下文切換總數 | long | 次數 |

**配置範例**：
```json
{
  "Name": "cpu_usage",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "cpu",
    "MetricName": "usage"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "SensorInfo Description for cpu.usage",
    "DisplayName": "SensorInfo DisplayName for cpu.usage"
  }
}
```

### memory

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `total` | 總記憶體 | long | bytes |
| `available` | 可用記憶體（含可回收快取） | long | bytes |
| `used` | 已使用記憶體（total - available） | long | bytes |
| `free` | 空閒記憶體 | long | bytes |
| `cached` | 快取記憶體 | long | bytes |
| `buffers` | 緩衝區記憶體 | long | bytes |
| `swap_total` | Swap 總容量 | long | bytes |
| `swap_free` | Swap 可用容量 | long | bytes |

### disk

需要額外參數 `MountPoint`（例如 `/` 或 `C:\`）

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `total` | 磁碟總容量 | long | bytes |
| `available` | 可用容量（非特權使用者） | long | bytes |
| `free` | 空閒容量 | long | bytes |
| `used` | 已使用容量 | long | bytes |
| `usage_percent` | 使用率百分比 | double | % (0-100) |
| `reads_completed` | 讀取操作總數 | long | 次數 |
| `writes_completed` | 寫入操作總數 | long | 次數 |
| `read_bytes` | 讀取位元組總數 | long | bytes |
| `written_bytes` | 寫入位元組總數 | long | bytes |

**配置範例**：
```json
{
  "Name": "disk_root_usage_percent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "disk",
    "MetricName": "usage_percent",
    "MountPoint": "/"
  },
  "Report": {
    "Enabled": true,
    "Interval": 30000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "SensorInfo Description for disk.root.usage_percent",
    "DisplayName": "SensorInfo DisplayName for disk.root.usage_percent"
  }
}
```

### network

需要額外參數 `Interface`（例如 `eth0`、`en0`）

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `bytes_sent` | 傳送位元組總數 | long | bytes |
| `bytes_received` | 接收位元組總數 | long | bytes |
| `packets_sent` | 傳送封包總數 | long | 封包數 |
| `packets_received` | 接收封包總數 | long | 封包數 |
| `errors` | 收發錯誤總數（errors_in + errors_out） | long | 次數 |
| `errors_in` | 接收錯誤數 | long | 次數 |
| `errors_out` | 傳送錯誤數 | long | 次數 |

**配置範例**：
```json
{
  "Name": "network_en0_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "en0"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "SensorInfo Description for network.en0.bytes_sent",
    "DisplayName": "SensorInfo DisplayName for network.en0.bytes_sent"
  }
}
```

### system

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `time` | 當前系統時間 | long | Unix timestamp (秒) |
| `timex_offset` | NTP 時間偏移 | double | 秒 |
| `boot_time` | 系統啟動時間 | long | Unix timestamp (秒) |
| `filefd_allocated` | 已分配的檔案描述符數量 | long | 數量 |
| `filefd_maximum` | 檔案描述符最大數量 | long | 數量 |
| `procs_running` | 運行中的處理程序數 | int | 數量 |
| `procs_blocked` | 被阻塞的處理程序數 | int | 數量 |
| `intr_total` | 中斷處理總數 | long | 次數 |

### gpu

使用 NVIDIA NVML 函式庫，需安裝 NVIDIA 驅動。

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `utilization` | GPU 使用率百分比 | int | % (0-100) |

---

## 二、硬體資訊類

適用平台：工業電腦（需硬體平台驅動，如 Advantech SUSI Driver）

### hwinfo

| MetricName | 說明 | 資料類型 |
|-----------|------|---------|
| `motherboardname` | 主機板名稱 | string |
| `manufacturer` | 製造商 | string |
| `biosrevision` | BIOS 版本 | string |
| `driverversion` | 驅動程式版本 | string |
| `libraryversion` | SDK 庫版本 | string |
| `ecrevision` | 嵌入式控制器版本 | string |

**配置範例**：
```json
{
  "Name": "hwinfo_motherboard",
  "Parameters": {
    "MetricType": "hwinfo",
    "MetricName": "motherboardname"
  },
  "Report": {
    "Enabled": true,
    "Interval": 60000
  },
  "SensorInfo": {
    "Schema": "string",
    "Description": "SensorInfo Description for hwinfo.motherboard",
    "DisplayName": "SensorInfo DisplayName for hwinfo.motherboard"
  }
}
```

---

## 三、板載感測器類

適用平台：工業電腦（需硬體平台驅動，如 Advantech SUSI Driver）

### temperature

支援兩種模式：

**模式 A：指定感測器名稱**（建議使用）

設定 `MetricName` 為感測器名稱（如 `cpU-therm`、`gpU-therm`），返回該感測器的溫度值（`double`，單位 °C）。支援不區分大小寫的比對。

**模式 B：返回所有感測器**

若 `MetricName` 設為任意值但不匹配任何感測器名稱，將返回 `null`。

**配置範例**（指定感測器）：
```json
{
  "Name": "temperature_cpU_therm",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpU-therm"
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "CPU thermal sensor temperature",
    "DisplayName": "CPU Thermal"
  }
}
```

### voltage

**返回類型**：`Dictionary<string, double?>`

Parser 返回所有電壓感測器的完整字典（忽略 `MetricName` 的值）。

- **Key**：電壓軌名稱（如 "VCore", "+3.3V", "+5V"）
- **Value**：電壓值（V）

**範例返回值**：
```json
{
  "VCore": 1.2,
  "+3.3V": 3.28,
  "+5V": 4.98
}
```

**配置範例**：
```json
{
  "Name": "voltage_all",
  "Parameters": {
    "MetricType": "voltage",
    "MetricName": "all"
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "object",
    "Description": "All voltage sensor readings",
    "DisplayName": "Voltage Sensors"
  }
}
```

### fanspeed

**返回類型**：`Dictionary<string, double?>`

Parser 返回所有風扇轉速的完整字典（忽略 `MetricName` 的值）。

- **Key**：風扇名稱（如 "CPU_Fan", "Sys_Fan"）
- **Value**：轉速值（RPM）

**範例返回值**：
```json
{
  "CPU_Fan": 2500,
  "Sys_Fan1": 1800,
  "Sys_Fan2": 1750
}
```

**配置範例**：
```json
{
  "Name": "fanspeed_all",
  "Parameters": {
    "MetricType": "fanspeed",
    "MetricName": "all"
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "object",
    "Description": "All fan speed readings",
    "DisplayName": "Fan Speed Sensors"
  }
}
```

---

## 四、硬體功能類

適用平台：工業電腦（需硬體平台驅動，如 Advantech SUSI Driver）

### gpio

支援以下 MetricName：

| MetricName | 說明 | 額外參數 | 資料類型 |
|-----------|------|---------|---------|
| `isSupported` | GPIO 功能是否受支援 | 無 | boolean |
| `pinState` | 指定腳位的電位狀態（0=Low, 1=High） | `PinId`（必填） | integer |

**配置範例**（查詢支援狀態）：
```json
{
  "Name": "gpio_isSupported",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "GPIO support status",
    "DisplayName": "GPIO Supported"
  }
}
```

**配置範例**（查詢腳位狀態）：
```json
{
  "Name": "gpio_pin_UIO_GPIO2",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinId": "UIO_GPIO2"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state for UIO_GPIO2",
    "DisplayName": "UIO_GPIO2 State"
  }
}
```

### watchdog

支援以下 MetricName：

| MetricName | 說明 | 資料類型 |
|-----------|------|---------|
| `isSupported` | Watchdog 功能是否受支援 | boolean |
| 其他值 | 返回完整的 `WatchdogMetrics` 對象 | object |

**`WatchdogMetrics` 對象結構**：
```json
{
  "IsSupported": true,
  "TimerIds": ["Timer0", "Timer1"],
  "TimerDetails": {
    "Timer0": {
      "Cap": {
        "IsStoppable": true,
        "DelayMinimum": 1,
        "DelayMaximum": 255
      },
      "Config": {
        "Delay": 60,
        "EventType": "NMI"
      }
    }
  }
}
```

**配置範例**：
```json
{
  "Name": "watchdog_isSupported",
  "Parameters": {
    "MetricType": "watchdog",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "Watchdog support status",
    "DisplayName": "Watchdog Supported"
  }
}
```

### thermalprotection

支援以下 MetricName：

| MetricName | 說明 | 資料類型 |
|-----------|------|---------|
| `isSupported` | 熱保護功能是否受支援 | boolean |
| 其他值 | 返回完整的 `ThermalProtectionMetrics` 對象 | object |

**`ThermalProtectionMetrics` 對象結構**：
```json
{
  "IsSupported": true,
  "ZoneIds": ["Zone0", "Zone1"],
  "ZoneDetails": {
    "Zone0": {
      "Cap": {
        "SupportSources": ["CPU", "System"],
        "SendEventTemperatureMinimum": 0,
        "SendEventTemperatureMaximum": 100
      },
      "Config": {
        "Source": "CPU",
        "EventType": "Shutdown",
        "SendEventTemperature": 85
      }
    }
  }
}
```

**配置範例**：
```json
{
  "Name": "thermalprotection_isSupported",
  "Parameters": {
    "MetricType": "thermalprotection",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "Thermal protection support status",
    "DisplayName": "Thermal Protection Supported"
  }
}
```

---

## 五、健康狀態（內部虛擬指標）

### health

`health` 為內部虛擬指標，用於追蹤各 Collector 的採集狀態。不由 Collector 採集，而是從 `HealthStatusMetrics` 中取得。

| MetricName | 說明 | 資料類型 |
|-----------|------|---------|
| `is_healthy` | 系統代理是否健康（1=健康, 0=異常） | int |
| `error_count` | 當前活動錯誤數量 | int |
| `errors` | 所有錯誤訊息（以 `;` 分隔） | string |

---

## 重要說明

### 1. MetricName 設計

所有 MetricType 均需要設定 `MetricName` 參數。若 `MetricName` 為 `null`，Parser 將返回 `null`。

各類型的 MetricName 行為差異：

| MetricType | MetricName 行為 |
|-----------|----------------|
| `cpu`、`memory`、`disk`、`network`、`system`、`gpu`、`hwinfo` | 必須設定為支援的指標名稱，返回單一純量值 |
| `temperature` | 設定為感測器名稱，返回該感測器的溫度值（`double`） |
| `voltage`、`fanspeed` | 需設定但值會被忽略，始終返回完整字典 |
| `gpio` | 設定為 `isSupported` 或 `pinState`（需搭配 `PinId`） |
| `watchdog`、`thermalprotection` | 設定為 `isSupported` 返回布林值，其他值返回完整對象 |
| `health` | 設定為 `is_healthy`、`error_count` 或 `errors` |

### 2. SIL2 安全性檢查

Parser 在轉換指標時會檢查 `Health.ActiveErrors`。若某 MetricType 的 Collector 發生採集失敗，該類型所有 Sensor 的遙測資料會被跳過，避免回報預設值/零值。

### 3. Request-Response 最佳化

系統僅採集已啟用 Sensor 所需的 MetricType。例如只配置了 `cpu` 和 `memory` 的 Sensor，則不會呼叫 `DiskCollector`、`NetworkCollector` 等。

---

## 在不支援硬體上的行為

### 跨平台指標

`cpu`、`memory`、`disk`、`network`、`system`、`gpu` 支援所有平台運行，不依賴硬體驅動。

### 硬體相關指標

在不支援的硬體上（例如沒有對應硬體平台驅動），以下 MetricType 會：

1. **程式不會崩潰**：捕獲驅動載入失敗的異常並繼續運行
2. **顯示警告日誌**：
   ```
   [WRN] Advantech driver DLL not found. Hardware metrics unavailable.
   ```
3. **通用指標不受影響**：系統資源類指標正常採集
4. **硬體指標無資料**：相關 sensors 無法採集資料

### 建議

- **開發環境**：可保留所有 sensor 配置，忽略警告日誌
- **生產環境（非工業電腦）**：建議禁用硬體相關 sensors（設置 `Enabled: false`）
- **生產環境（工業電腦）**：確保硬體平台驅動正確安裝（如 Advantech 工業電腦需要 SUSI 驅動）

---

## 參考資料

- [QUICK_START.md](01_QUICK_START.md) - 快速開始指南
- [DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) - Docker 部署說明
- [README.md](README.md) - 完整說明文件
