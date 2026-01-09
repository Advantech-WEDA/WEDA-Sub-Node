# MetricType 配置說明

使用者透過設定 `MetricType` 指定 System Agent 的採集指標。即使硬體不支援該指標，System Agent 也不會產生異常，只是無法採集對應指標資料。

## MetricType 分類與設計原則

---

## 一、系統資源類

支援跨平台（Linux、Windows、macOS），使用標準作業系統 API 獲取

### cpu

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `usage` | 整體 CPU 使用率 | double | % (0-100) |
| `load1` | 1 分鐘負載平均值 | double | - |
| `load5` | 5 分鐘負載平均值 | double | - |
| `load15` | 15 分鐘負載平均值 | double | - |
| `context_switches` | 上下文切換總數 | long | 次數 |

**配置範例**：
```json
{
  "Name": "cpu.usage",
  "Parameters": {
    "MetricType": "cpu",
    "MetricName": "usage"
  },
  "Config": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

### memory

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `total` | 總記憶體 | long | bytes |
| `available` | 可用記憶體 | long | bytes |
| `used` | 已使用記憶體 | long | bytes |
| `cached` | 快取記憶體 | long | bytes |
| `usage_percent` | 記憶體使用率 | double | % (0-100) |

### disk

需要額外參數 `MountPoint`（例如 `/` 或 `C:\`）

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `total` | 磁碟總容量 | long | bytes |
| `available` | 可用容量 | long | bytes |
| `used` | 已使用容量 | long | bytes |
| `usage_percent` | 使用率百分比 | double | % (0-100) |
| `reads_completed` | 讀取操作總數 | long | 次數 |
| `writes_completed` | 寫入操作總數 | long | 次數 |
| `read_bytes` | 讀取位元組總數 | long | bytes |
| `written_bytes` | 寫入位元組總數 | long | bytes |

**配置範例**：
```json
{
  "Name": "disk.root.usage",
  "Parameters": {
    "MetricType": "disk",
    "MetricName": "usage_percent",
    "MountPoint": "/"
  },
  "Config": {
    "Enabled": true,
    "Interval": 3000
  }
}
```

### network

需要額外參數 `Interface`（例如 `eth0`）

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `bytes_sent` | 傳送位元組總數 | long | bytes |
| `bytes_received` | 接收位元組總數 | long | bytes |
| `packets_sent` | 傳送封包總數 | long | 封包數 |
| `packets_received` | 接收封包總數 | long | 封包數 |
| `errors_in` | 接收錯誤數 | long | 次數 |
| `errors_out` | 傳送錯誤數 | long | 次數 |

**配置範例**：
```json
{
  "Name": "network.eth0.tx",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  },
  "Config": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

### system

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `time` | 當前系統時間 | long | Unix timestamp (秒) |
| `boot_time` | 系統啟動時間 | long | Unix timestamp (秒) |
| `procs_running` | 運行中的處理程序數 | long | 數量 |
| `procs_blocked` | 被阻塞的處理程序數 | long | 數量 |

### gpu

| MetricName | 說明 | 資料類型 | 單位 |
|-----------|------|---------|------|
| `utilization` | GPU 使用率百分比 | double | % (0-100) |

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
  "Name": "hwinfo.motherboard",
  "Parameters": {
    "MetricType": "hwinfo",
    "MetricName": "motherboardname"
  },
  "Config": {
    "Enabled": true,
    "Interval": 60000
  }
}
```

---

## 三、板載感測器類

適用平台：工業電腦（需硬體平台驅動，如 Advantech SUSI Driver）

### temperature

**返回類型**：`Dictionary<string, double?>`

Parser 直接返回所有溫度感測器的完整字典，不支援指定單個感測器。

- **Key**：感測器來源名稱（如 "CPU", "System"）
- **Value**：溫度值（°C）

**範例返回值**：
```json
{
  "CPU": 45.0,
  "System": 38.5,
  "PCH": 42.0
}
```

**配置範例**：
```json
{
  "Name": "temperature",
  "Parameters": {
    "MetricType": "temperature"
  },
  "Config": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

### voltage

**返回類型**：`Dictionary<string, double?>`

Parser 直接返回所有電壓感測器的完整字典。

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
  "Name": "voltage",
  "Parameters": {
    "MetricType": "voltage"
  },
  "Config": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

### fanspeed

**返回類型**：`Dictionary<string, double?>`

Parser 直接返回所有風扇轉速的完整字典。

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
  "Name": "fanspeed",
  "Parameters": {
    "MetricType": "fanspeed"
  },
  "Config": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

---

## 四、硬體功能類

適用平台：工業電腦（需硬體平台驅動，如 Advantech SUSI Driver）

### gpio

**返回類型**：`GpioMetrics` 對象

Parser 直接返回完整的 GPIO 資訊，包含支援狀態和所有腳位資料。

**對象結構**：
```json
{
  "IsSupported": true,
  "PinNames": ["GPIO0", "GPIO1", "GPIO2", "GPIO3"],
  "PinStateDetails": null
}
```

**配置範例**：
```json
{
  "Name": "gpio",
  "Parameters": {
    "MetricType": "gpio"
  },
  "Config": {
    "Enabled": true,
    "Interval": 6000
  }
}
```

### watchdog

**返回類型**：`WatchdogMetrics` 對象

Parser 直接返回完整的 Watchdog 資訊，包含支援狀態、計時器列表及詳細設置。

**對象結構**：
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
  "Name": "watchdog",
  "Parameters": {
    "MetricType": "watchdog"
  },
  "Config": {
    "Enabled": true,
    "Interval": 6000
  }
}
```

### thermalprotection

**返回類型**：`ThermalProtectionMetrics` 對象

Parser 直接返回完整的熱保護資訊，包含支援狀態、保護區域列表及詳細設置。

**對象結構**：
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
  "Name": "thermalprotection",
  "Parameters": {
    "MetricType": "thermalprotection"
  },
  "Config": {
    "Enabled": true,
    "Interval": 6000
  }
}
```

---

## 重要說明

### 1. MetricName 設計
- **不需要 MetricName 的 MetricType**：`temperature`、`voltage`、`fanspeed`、`gpio`、`watchdog`、`thermalprotection` 
  - Parser 直接返回完整的數據（Dictionary 或 Metrics 對象）
  - 配置時不需要設置 `MetricName` 參數

### 2. 頻率限制

**這些 MetricType 無法設置不同細節的不同頻率**，這是與其他 MetricType（如 `cpu`、`memory`）最大的差異：

- **其他 MetricType**：可為不同的 MetricName 設置不同的 Interval
  - 例如：`cpu.usage` 每 1 秒上拋，`cpu.load1` 每 5 秒上拋

- **這些 MetricType**：硬體收集器一次性上拋所有數據，**無法為單個感測器或屬性設置不同的上拋頻率**
  - 它們會以相同的 Interval 統一返回

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
