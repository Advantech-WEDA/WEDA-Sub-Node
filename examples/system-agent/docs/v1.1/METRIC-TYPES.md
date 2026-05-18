# MetricType 配置說明 (v1.1)

> 本文件描述 v1.1 新增的 **Explicit list / Auto-detect** 兩種 Sensor 解析模式。
> v1.0 已有的 MetricType 分類、Sensor 配置結構、安全性檢查等基礎說明，請參閱 [METRIC-TYPES v1.0](../v1.0/METRIC-TYPES.md)。
> Sensor 配置欄位快速參考與 v1.1 參數變更對照，請參閱 [Sensor 配置快速參考 v1.1](Sensor-Configuration-and-Usage-Guide.md)。

---

## v1.1 新增功能：Explicit list / Auto-detect mode

v1.0 中，每個 Sensor 必須明確綁定到特定資源（例如指定 `Interface`、`PinId`、`MetricName`），即 **Bound mode**。v1.1 新增 **Explicit list mode** 與 **Auto-detect mode**，允許定義「泛用 Sensor」，系統啟動時根據模式自動偵測硬體資源並產生對應的具體 Sensor。

### 核心概念

| 概念 | 說明 |
|------|------|
| **泛用 Sensor** | 不指定具體資源識別參數的 Sensor 定義 |
| **自動偵測模式** | 完全不帶列表（或設為空陣列 `[]` / 空字串 `""`），系統自動探索所有可用資源並展開 |
| **明確列表模式** | 透過 JSON 陣列或逗號分隔字串指定要展開的資源子集 |
| **指定 Sensor** | 已指定單一資源參數（如 `Interface`）的 Sensor，不會被展開（與 v1.0 行為一致） |

### 優先順序

```
已綁定單一資源參數（v1.0 模式） > 明確列表參數（如 Interfaces） > 自動偵測
```

---

## 支援 Explicit list / Auto-detect mode 的 MetricType

僅以下三種 MetricType 支援 Explicit list / Auto-detect mode，其餘 MetricType（`cpu`、`memory`、`disk`、`system`、`gpu`、`hwinfo`、`voltage`、`fanspeed`、`watchdog`、`thermalprotection`、`health`）行為與 v1.0 完全一致。

| MetricType | 單一資源參數（v1.0） | 單一資源參數（v1.1） | 列表參數（v1.1） | 自動偵測觸發條件（v1.1） |
|------------|---------------------|---------------------|------------------|----------------|
| `network` | `Interface` | `Interface` | `Interfaces`（陣列或逗號分隔） | 未設定 `Interface` 且未設定 `Interfaces` |
| `gpio`（`pinState`） | `PinId` | `PinId` | `PinIds`（陣列或逗號分隔） | 未設定 `PinId` 且未設定 `PinIds` |
| `temperature` | `MetricName`（兼任來源名稱） | `Source` | `Sources`（陣列或逗號分隔） | 未設定 `Source` 且未設定 `Sources` 且未設定 `MetricName` |

> **temperature 的 v1.0 → v1.1 變更**：v1.0 中 `MetricName` 同時作為「指標名稱」和「感測器來源識別符」。v1.1 將來源識別拆分到獨立的 `Source` / `Sources` 參數，`MetricName` 回歸為固定的指標名稱（建議使用 `"therm"`）。v1.0 配置（僅設定 `MetricName`）仍然相容。

---

## 一、network（Explicit list / Auto-detect mode）

### 自動偵測模式（不帶列表）

省略 `Interface` 參數，系統啟動時自動偵測所有網路介面並展開。

**配置範例**：
```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network bytes sent"
  }
}
```

若系統偵測到 `eth0`、`wlan0` 兩個介面，自動展開為：

| 展開後 Name | Interface | DisplayName |
|-------------|-----------|-------------|
| `network_bytes_sent_eth0` | `eth0` | `eth0 Network bytes sent` |
| `network_bytes_sent_wlan0` | `wlan0` | `wlan0 Network bytes sent` |

### 明確列表模式（帶列表）

使用 `Interfaces` 陣列或逗號分隔字串指定要監控的介面子集：

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": ["eth0", "eth1"]
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network bytes sent"
  }
}
```

即使系統存在 `wlan0`、`lo` 等其他介面，也只會展開為 `eth0` 和 `eth1` 兩個 Sensor。

### 已綁定模式（v1.0 相容）

若已指定 `Interface`，行為與 v1.0 完全一致，不進行展開：

```json
{
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  }
}
```

---

## 二、gpio（Explicit list / Auto-detect mode）

僅當 `MetricName` 為 `pinState` 時觸發展開。`isSupported` 等其他 MetricName 不受影響。

### 自動偵測模式（不帶列表）

省略 `PinId` 參數，系統自動偵測所有 GPIO 腳位並展開。

**配置範例**：
```json
{
  "Name": "gpio_pin",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin state"
  }
}
```

若系統偵測到 `DI_0`、`DI_1`、`DO_0` 三個腳位，自動展開為：

| 展開後 Name | PinId | DisplayName |
|-------------|-------|-------------|
| `gpio_pin_DI_0` | `DI_0` | `DI_0 Pin state` |
| `gpio_pin_DI_1` | `DI_1` | `DI_1 Pin state` |
| `gpio_pin_DO_0` | `DO_0` | `DO_0 Pin state` |

### 明確列表模式（帶列表）

使用 `PinIds` 陣列或逗號分隔字串指定要監控的腳位子集：

```json
{
  "Name": "gpio_pin",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": ["DI_0", "DI_1"]
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin state"
  }
}
```

即使系統存在 `DO_0` 等其他腳位，也只會展開為 `DI_0` 和 `DI_1` 兩個 Sensor。

### 已綁定模式（v1.0 相容）

若已指定 `PinId`，行為與 v1.0 完全一致：

```json
{
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinId": "UIO_GPIO2"
  }
}
```

---

## 三、temperature（Explicit list / Auto-detect mode）

> **v1.1 變更**：v1.0 中 `MetricName` 同時作為指標名稱和感測器來源識別符。v1.1 將來源識別拆分到獨立的 `Source`（單一）/ `Sources`（列表）參數，`MetricName` 固定為 `"therm"`。v1.0 配置（僅設定 `MetricName` 為來源名稱）仍然相容。

### 自動偵測模式（不帶列表）

省略 `Source` 參數，使用 `Sources: []` 或完全不帶，系統啟動時自動偵測所有溫度感測器來源並展開。

**配置範例**：
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

若系統偵測到 `cpU-therm`、`gpU-therm` 兩個溫度來源，自動展開為：

| 展開後 Name | Source | DisplayName |
|-------------|--------|-------------|
| `temperature_cpU-therm` | `cpU-therm` | `cpU-therm Temperature` |
| `temperature_gpU-therm` | `gpU-therm` | `gpU-therm Temperature` |

### 明確列表模式（帶列表）

使用 `Sources` 陣列或逗號分隔字串指定要監控的溫度來源子集：

```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": ["cpU-therm", "gpU-therm"]
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

即使系統存在其他溫度感測器，也只會展開為指定的來源。

### 已綁定模式（指定單一來源）

若已指定 `Source`，不進行展開：

```json
{
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Source": "cpU-therm"
  }
}
```

### v1.0 相容模式

若僅設定 `MetricName`（不帶 `Source` 或 `Sources`），行為與 v1.0 完全一致——`MetricName` 作為感測器來源名稱使用：

```json
{
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpU-therm"
  }
}
```

### v1.0 與 v1.1 對照

| | v1.0 | v1.1 |
|------|------|------|
| **MetricName** | 感測器來源名稱（如 `cpU-therm`） | 固定為 `"therm"` |
| **來源識別** | 由 `MetricName` 兼任 | 獨立的 `Source` 參數 |
| **列表展開** | ❌ 不支援 | `Sources` 陣列或逗號分隔字串 |
| **自動偵測** | ❌ 不支援 | 省略 `Source` + `Sources: []` |

---

## 展開後的命名規則

當泛用 Sensor 被展開時，產生的 Sensor 屬性按以下規則生成：

| 屬性 | 規則 | 範例 |
|------|------|------|
| `Name` | `{泛用Name}_{resourceName}` | `network_bytes_sent_eth0` |
| `DisplayName` | `{resourceName} {泛用DisplayName}` | `eth0 Network bytes sent` |
| `Description` | `{泛用Description} ({resourceName})` | `Network bytes sent (eth0)` |

展開後的 Sensor 會移除列表參數（如 `Interfaces`），並新增單一資源參數（如 `Interface`）。其餘設定（`Report`、`SensorGroup`、`Schema` 等）從泛用 Sensor 繼承。

---

## 邊界行為

| 情境 | 行為 |
|------|------|
| 自動偵測模式但無發現任何資源 | 保持泛用 Sensor 原樣不展開，輸出警告日誌 |
| 明確列表為空陣列 `[]` 或空字串 `""` | 退回自動偵測模式 |
| 在不支援硬體上使用 GPIO/Temperature 自動偵測 | 無發現資源，保持原樣（與 v1.0 行為一致） |
| 非展開類型（cpu、memory 等）設定列表參數 | 列表參數被忽略，行為與 v1.0 一致 |
