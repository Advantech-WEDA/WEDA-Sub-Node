# MetricType 配置說明

使用者透過設定 `MetricType` 指定 System Agent 的採集指標。即使硬體不支援該指標，System Agent 也不會產生異常，只是無法採集對應指標資料。

## Sensor 配置結構

每個 Sensor 在 `devicecfg.json` 中的配置結構如下，Parameters 會因為不同的 Sensor 有不同的欄位：

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
| `SensorGroup` | Sensor 分組 |
| `Parameters.MetricType` | 指標類型（必填） |
| `Parameters.MetricName` | 指標名稱（所有 MetricType 均需要） |
| `Report.Enabled` | 是否啟用上報 |
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

設定 `MetricName` 為感測器名稱（如 `cpU-therm`、`gpU-therm`），返回該感測器的溫度值（`double`，單位 °C）。支援不區分大小寫的比對。

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

---

## 在不支援硬體上的行為

在不支援的硬體上（例如沒有對應硬體平台驅動）會：

1. **程式不會崩潰**：捕獲驅動載入失敗的異常並繼續運行
2. **顯示警告日誌**：
   ```
   [WRN] Advantech driver DLL not found. Hardware metrics unavailable.
   ```
3. **通用指標不受影響**：系統資源類指標正常採集
4. **硬體指標無資料**：相關 sensors 無法採集資料
