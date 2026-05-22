## Sensor 配置參考

### 欄位總覽

| 欄位 | 可否修改 | 說明 |
|------|---------|------|
| `Name` | ❌ 不可修改 | Sensor 的唯一識別名稱，建立後不可變更 |
| `SensorGroup` | ❌ 不可修改 | Sensor 分組，值為固定列舉（見下表） |
| `Report.Enabled` | ✅ 可修改 | 是否啟用定期上報（`true` / `false`） |
| `Report.Interval` | ✅ 可修改 | 上報間隔，單位為毫秒 |
| `SensorInfo.Schema` | ⚠️ 可修改，但僅限列舉值 | 預期回傳的資料類型，需與 MetricName 回傳類型一致 |
| `SensorInfo.Description` | ✅ 可修改 | Sensor 描述文字，自由填寫 |
| `SensorInfo.DisplayName` | ✅ 可修改 | Sensor 顯示名稱，自由填寫 |
| `Parameters.MetricType` | ⚠️ 可修改，但僅限列舉值 | 指標類型，必須為支援的 MetricType 之一 |
| `Parameters.MetricName` | ⚠️ 可修改，但僅限列舉值 | 指標名稱，必須為對應 MetricType 支援的名稱 |

---

### SensorGroup 支援的值

| 值 | 全稱 | 用途 |
|----|------|------|
| `AI` | Analog Input | 類比輸入感測器讀值 |
| `AO` | Analog Output | 類比輸出控制 |
| `DI` | Digital Input | 數位輸入感測器 |
| `DO` | Digital Output | 數位輸出控制 |
| `TEMP` | Temperature | 溫度感測器 |
| `PWR` | Power | 電力監控感測器 |
| `SYS` | System | 系統資源監控 |

### SensorInfo.Schema 支援的值

| 分類 | Schema 值 | 說明 |
|------|----------|------|
| 基本數值型 | `double` | 雙精度浮點數 |
| 基本數值型 | `integer` | 32 位元整數 |
| 基本數值型 | `long` | 64 位元整數 |
| 基本非數值型 | `boolean` | 布林值 |
| 基本非數值型 | `string` | 字串 |
| MIME 型 | `image/jpeg` | JPEG 圖片 |
| MIME 型 | `image/png` | PNG 圖片 |
| MIME 型 | `application/json` | JSON 資料 |
| MIME 型 | `application/octet-stream` | 二進位資料 |

### MetricType 支援的值

| MetricType | 分類 | 說明 |
|-----------|------|------|
| `cpu` | 系統資源 | CPU 使用率、負載 |
| `memory` | 系統資源 | 記憶體用量 |
| `disk` | 系統資源 | 磁碟容量與 I/O |
| `network` | 系統資源 | 網路流量與封包 |
| `gpu` | 系統資源 | GPU 使用率（需 NVIDIA 驅動） |
| `system` | 系統資源 | 系統時間、處理程序、檔案描述符 |
| `hwinfo` | 硬體資訊 | 主機板、BIOS、驅動版本（需硬體平台驅動） |
| `temperature` | 板載感測器 | 溫度感測器（需硬體平台驅動） |
| `voltage` | 板載感測器 | 電壓感測器（需硬體平台驅動） |
| `fanspeed` | 板載感測器 | 風扇轉速感測器（需硬體平台驅動） |
| `gpio` | 硬體功能 | GPIO 腳位狀態（需硬體平台驅動） |
| `watchdog` | 硬體功能 | 看門狗計時器（需硬體平台驅動） |
| `thermalprotection` | 硬體功能 | 熱保護機制（需硬體平台驅動） |

### 需要額外參數的組合

| MetricType + MetricName | 額外參數 | 說明 |
|------------------------|---------|------|
| `disk` + 任意 MetricName | `MountPoint`（必填，如 `/` 或 `C:\`） | 指定要監控的磁碟掛載點 |
| `network` + 任意 MetricName | `Interface`（必填，如 `eth0`、`en0`） | 指定要監控的網路介面 |
| `gpio` + `pinState` | `PinId`（必填，如 `UIO_GPIO2`） | 指定要查詢的 GPIO 腳位名稱或索引 |

> 其餘 MetricType 組合僅需 `MetricType` + `MetricName`，無額外參數。

#### ⚠️ temperature 的 MetricName 特殊用法

`temperature` 雖然沒有額外參數，但 `MetricName` 的語義與其他 MetricType 不同：

| MetricType | MetricName 語義 | 範例 |
|-----------|----------------|------|
| 其他（cpu、memory 等） | 固定列舉值，指定要採集的指標 | `usage`、`total`、`bytes_sent` |
| `temperature` | **硬體感測器來源名稱**，由硬體決定 | `cpU-therm`、`gpU-therm` |

使用者必須事先知道硬體上存在哪些溫度感測器名稱，才能正確填寫 `MetricName`。詳細說明請參閱 [METRIC-TYPES — temperature](METRIC-TYPES_zh-tw.md#temperature)。

> **v1.1 注意**：v1.1 將此語義拆分為獨立的 `Source` / `Sources` 參數，`MetricName` 改為固定值 `"therm"`。詳見 [v1.1 METRIC-TYPES](../v1.1/METRIC-TYPES_zh-tw.md#三temperatureexplicit-list--auto-detect-mode)。

---

## 範例

### 基本範例 — CPU 使用率

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
    "Description": "CPU usage percentage",
    "DisplayName": "CPU Usage"
  }
}
```

### 需要額外參數 — 磁碟使用率（MountPoint）

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
    "Description": "Disk usage percentage for root",
    "DisplayName": "Disk Usage (/)"
  }
}
```

### 需要額外參數 — 網路流量（Interface）

```json
{
  "Name": "network_eth0_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total bytes sent on eth0",
    "DisplayName": "Network Bytes Sent (eth0)"
  }
}
```

### 需要額外參數 — GPIO 腳位狀態（PinId）

```json
{
  "Name": "gpio_pin_UIO_GPIO2",
  "SensorGroup": "DI",
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

### 硬體資訊 — 主機板名稱

```json
{
  "Name": "hwinfo_motherboard",
  "SensorGroup": "SYS",
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
    "Description": "Motherboard name",
    "DisplayName": "Motherboard"
  }
}
```

### 板載感測器 — 溫度

```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpu-therm"
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

