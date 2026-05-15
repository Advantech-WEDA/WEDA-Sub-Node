# Sensor 配置快速參考 — v1.1 差異

> 本文件僅說明 v1.1 與 v1.0 的差異。完整欄位說明請參閱 [v1.0 Sensor-Configuration-and-Usage-Guide](../v1.0/Sensor-Configuration-and-Usage-Guide.md)。

---

## 需要額外參數的組合（v1.1 更新）

v1.1 新增列表參數支援 Sensor Expansion，原有的單一參數仍然有效（已綁定模式）。

| MetricType + MetricName | v1.0 參數 | v1.1 新增 | 說明 |
|------------------------|-----------|----------|------|
| `disk` + 任意 | `MountPoint`（必填） | — | 無變更 |
| `network` + 任意 | `Interface`（單一介面） | `Interfaces`（陣列、逗號分隔或 `[]` 自動偵測） | 展開為多個網路介面感測器 |
| `gpio` + `pinState` | `PinId`（單一腳位） | `PinIds`（陣列、逗號分隔或 `[]` 自動偵測） | 展開為多個 GPIO 腳位感測器 |
| `temperature` + `therm` | `MetricName`（來源名稱） | `Source`（單一）/ `Sources`（陣列、逗號分隔或 `[]` 自動偵測） | `MetricName` 固定為 `"therm"`，來源識別改用 `Source`/`Sources` |

> **注意**：`[]`（空陣列）、`""`（空字串）與省略該參數效果相同，都會觸發自動偵測。
>
> **格式說明**：列表參數支援 JSON 陣列（如 `["eth0", "eth1"]`）或逗號分隔字串（如 `"eth0,eth1"`）兩種格式。

> 展開後的命名規則、邊界行為等細節請參閱 [METRIC-TYPES v1.1](METRIC-TYPES.md#展開後的命名規則)。

---

## 變更的範例

### 板載感測器 — 溫度（v1.1）

**v1.0 寫法**（仍然相容）：
```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpu-therm"
  }
}
```

**v1.1 寫法**（已綁定單一來源）：
```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Source": "cpu-therm"
  }
}
```

**v1.1 寫法**（自動偵測所有來源）：
```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Temperature sensor",
    "DisplayName": "Temperature"
  }
}
```

### 網路流量 — 自動偵測所有介面（v1.1 新增）

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total bytes sent",
    "DisplayName": "Network Bytes Sent"
  }
}
```

### GPIO 腳位 — 自動偵測所有腳位（v1.1 新增）

```json
{
  "Name": "gpio_pinState",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "GPIO Pin State"
  }
}
```

---

## 未變更的部分

以下內容與 v1.0 完全一致，請參閱 [v1.0 文件](../v1.0/Sensor-Configuration-and-Usage-Guide.md)：

- 欄位總覽表（各欄位可否修改）
- SensorGroup 列舉值
- Schema 支援的值
- 支援的 MetricType 列表
- `cpu`、`memory`、`disk`、`gpu`、`system`、`hwinfo`、`voltage`、`fanspeed`、`watchdog`、`thermalprotection` 的配置方式
