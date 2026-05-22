---
title:    "Chapter 5 — 常見 message 函式庫速查"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 5 — 常見 message 函式庫速查

[← Chapter 4](04-discovery-and-naming.md) ｜ [回 README](README.md) ｜ [下一章 → `ros2` CLI 工具](06-tooling-and-customer-view.md)

> **為什麼要讀這章？** PRD REQ-14 說 SDK **預先 ship 5 個 package 的 codec**：
> `std_msgs`、`sensor_msgs`、`geometry_msgs`、`nav_msgs`、`diagnostic_msgs`。
> 你不用會背每個 .msg 的欄位 — 但要知道**這 5 包大概各自裝什麼**，
> 後面討論 pre-built 覆蓋範圍、DTDL auto-gen 要先做哪些型別時才有 grounded 的判斷。
>
> **這章是速查表**，看過一次有印象就夠了，不用背。

---

## 1. 五包的角色分工

```
   builtin_interfaces   ← 最底層：Time、Duration（其他 5 包都引用它）
        │
        ▼
   std_msgs             ← 基本型別包裝（Header、String、Bool ...）
        │
        ├──► geometry_msgs   ← 幾何（座標、姿態、速度）
        │       │
        │       ▼
        ├──► sensor_msgs     ← 感測器讀數
        │
        ├──► nav_msgs        ← 導航 / 地圖
        │
        └──► diagnostic_msgs ← 自我診斷狀態
```

---

## 2. `builtin_interfaces` — 時間 / 期間

只有兩個型別，但**幾乎每個訊息都引用它**：

```
# Time.msg
int32  sec
uint32 nanosec

# Duration.msg
int32  sec
uint32 nanosec
```

對 SubNode：時戳是 telemetry 上雲時的「時間欄位」，要對應到 WedaCore 的 ISO-8601 字串或 epoch ms。Codegen 時這個轉換要 hardcode。

---

## 3. `std_msgs` — 基本型別 + Header

最核心是 **`Header`**，所有「有時戳 / 在某座標系」的訊息都用它：

```
# Header.msg
builtin_interfaces/Time stamp     # 何時量到的
string                  frame_id  # 哪個座標系（例：'base_link'、'map'）
```

`frame_id` 對 SLAM / 導航很重要（要把感測器讀數轉換到 robot base 上）。SubNode 不做 tf 變換，所以**我們把 frame_id 當 telemetry metadata 帶上雲**就好。

其他常見：

| Type | 用途 |
|---|---|
| `Empty` | 純信號訊息（無 payload，就是「事情發生了」）|
| `Bool` / `String` / `Int32` / `Float32` / `Float64` | 純值包裝（debug 用、自家寫 topic 常用） |
| `ColorRGBA` | LED / 視覺化 |

---

## 4. `geometry_msgs` — 座標、姿態、速度

機器人三大幾何概念：**位置 + 方向 + 速度**。每個有「裸版」跟「Stamped 版」（多帶 `Header`）。

### 4.1 基本元件

```
# Point.msg          位置
float64 x
float64 y
float64 z

# Quaternion.msg     方向（用四元數，避免 Euler 角的死鎖問題）
float64 x
float64 y
float64 z
float64 w

# Vector3.msg        向量（用於速度、力、加速度）
float64 x
float64 y
float64 z
```

### 4.2 組合型別

```
# Pose.msg                位置 + 方向
Point      position
Quaternion orientation

# Twist.msg               線速度 + 角速度
Vector3 linear
Vector3 angular

# Transform.msg           平移 + 旋轉
Vector3    translation
Quaternion rotation
```

### 4.3 Stamped 版（多帶 Header）

```
# PoseStamped.msg
std_msgs/Header header
Pose            pose

# TwistStamped.msg
std_msgs/Header header
Twist           twist
```

**對 SubNode**：機器人姿態、目標位置這類 KPI 用 Stamped 版上雲（time + frame_id 一起帶）。Composite message（REQ-05）的典型例子 — 一筆「PoseStamped」當成一個 JSON 物件上去。

---

## 5. `sensor_msgs` — 感測器讀數（最大宗）

REQ-14 五包裡**最大、最常用**的一包。常見：

| Type | 內容 | 上雲時通常 |
|---|---|---|
| **`BatteryState`** | 電壓 / 電流 / 百分比 / 充放電狀態 | 整筆 JSON 上雲（PRD §8.2 composite message 典型）|
| **`Imu`** | 角速度 + 線加速度 + 姿態（四元數） | 取百分位上雲 |
| **`LaserScan`** | 雷射雷達掃描 — `ranges[]` 變長陣列 | **聚合**（min/max/障礙物個數）上雲，不發原始陣列 |
| **`PointCloud2`** | 3D 點雲 — 數 MB / 筆 | **不上雲**（NG-2 firehose 排除）|
| **`Image`** | RGB / depth 影像 — 數 MB / 筆 | **不上雲**（NG-2）|
| **`CompressedImage`** | JPEG / PNG 壓縮影像 | 視 case — 客戶有需求才 sample 上雲 |
| **`JointState`** | 關節位置 / 速度 / 力矩（多個關節）| 聚合上雲（總工時、異常關節數）|
| **`NavSatFix`** | GPS — 緯度 / 經度 / 高度 | 整筆上雲 |
| **`Temperature`** / **`RelativeHumidity`** / **`FluidPressure`** | 環境感測器 | 整筆上雲 |
| **`Range`** | 超音波 / 紅外線測距 | 整筆上雲 |
| **`Illuminance`** | 光度 | 整筆上雲 |

`BatteryState` 範例（最常用的 robot KPI）：

```
# BatteryState.msg
std_msgs/Header header
float32  voltage             # V
float32  temperature         # °C
float32  current             # A
float32  charge              # Ah
float32  capacity            # Ah (目前)
float32  design_capacity     # Ah (設計)
float32  percentage          # 0.0–1.0
uint8    power_supply_status # 看下方常數
uint8    power_supply_health
uint8    power_supply_technology
bool     present
float32[] cell_voltage       # 變長：每個電芯電壓
float32[] cell_temperature
string   location
string   serial_number

uint8 POWER_SUPPLY_STATUS_UNKNOWN=0
uint8 POWER_SUPPLY_STATUS_CHARGING=1
uint8 POWER_SUPPLY_STATUS_DISCHARGING=2
uint8 POWER_SUPPLY_STATUS_NOT_CHARGING=3
uint8 POWER_SUPPLY_STATUS_FULL=4
```

對 SubNode：**`BatteryState` → DTDL Object schema 一次到位**（REQ-05 composite message）。常數列舉就是 DTDL `EnumValue`。

---

## 6. `nav_msgs` — 導航 / 地圖

| Type | 內容 | SubNode 上雲嗎 |
|---|---|---|
| **`Odometry`** | 當下位置 + 速度（Pose + Twist + 共變異矩陣）| ✅ 取位置 + 速度上雲 |
| **`Path`** | 一連串 `PoseStamped` — 規劃路徑 | 點數可能多，**聚合**（路徑長度、ETA）後上雲 |
| **`OccupancyGrid`** | 2D 地圖（每格 -128~127 機率值）| **不上雲**（NG-2，地圖數 MB）|
| **`MapMetaData`** | 地圖 metadata（解析度、原點、大小）| 整筆上雲 |
| **`GridCells`** | 格點集合 | 視 case |

`Odometry` 範例：

```
# Odometry.msg
std_msgs/Header header
string  child_frame_id
geometry_msgs/PoseWithCovariance  pose
geometry_msgs/TwistWithCovariance twist
```

對 SubNode：機器人「目前位置」KPI 的主來源。

---

## 7. `diagnostic_msgs` — 自我診斷

robot 內部健康狀態回報用。**對 SubNode 是金礦** — robot 自己已經做好了健康評估，我們直接搬上雲。

```
# DiagnosticStatus.msg
byte    level         # 0=OK, 1=WARN, 2=ERROR, 3=STALE
string  name          # 例：'/lidar_driver'
string  message       # 例：'connected, 10 Hz'
string  hardware_id   # 例：'lidar_serial_0123'
KeyValue[] values     # 任意 key/value pair

# KeyValue.msg
string key
string value

# DiagnosticArray.msg
std_msgs/Header header
DiagnosticStatus[] status
```

對 SubNode：訂 `/diagnostics` topic（標準 ROS 2 慣例），整包上雲就是現成的 health report。**這是 DeviceStatus 自然來源之一**。

---

## 8. 額外要知道的（不在 REQ-14 但常見）

| Package | 用途 | SubNode |
|---|---|---|
| **`std_srvs`** | 通用 service：`Empty`、`Trigger`、`SetBool` | service allow-list 範例對象 |
| **`action_msgs`** | action 共用基礎：`GoalStatus`、`GoalID` | v1.2 actions 才碰到 |
| **`tf2_msgs`** / **`geometry_msgs/TransformStamped`** | tf 變換廣播 | 通常**不訂閱**（高頻、聚合困難）|
| **`rcl_interfaces`** | 參數 / lifecycle | NG — 我們不做 rcl 那層 |
| **`visualization_msgs`** | RViz 顯示用 marker | 不需要 |

---

## 9. 對 SubNode pre-built 範圍的影響（REQ-14）

REQ-14「pre-built coverage for 5 packages」展開來，**實際要 codegen 的型別數**：

| Package | 估計型別數 | SubNode 對應 |
|---|---|---|
| `std_msgs` | ~30 | Header 是必生；其他 primitive wrapper 全生但很少訂閱 |
| `sensor_msgs` | ~30 | 全生；DTDL Object schema 全生 |
| `geometry_msgs` | ~30 | Pose / Twist / 各種 Stamped 是核心 |
| `nav_msgs` | ~10 | Odometry / Path 是核心 |
| `diagnostic_msgs` | 4 | 全生 |

**總共大約 100+ 個 `.msg`**。我們的 codegen 工具一跑就全產，是一次性工作。

額外要 ship：
- `builtin_interfaces`（依賴項）— Time、Duration
- `action_msgs` + `std_srvs`（v1.1 service 用得到的部分）

**剛剛沒列在 REQ-14 但建議補上**：
- `std_srvs` — service 範例對象（PRD REQ-07 範例的 `Trigger` / `SetBool` 都在這裡）
- `builtin_interfaces` — 上述 5 包都引用，沒它不能編

建議在 Phase 3 grilling 提出「REQ-14 五包 + `builtin_interfaces` + `std_srvs`」當實際 ship 範圍。

---

## ✅ 學完這章你應該能回答

1. `Header` 在哪一包？裡面兩個欄位是什麼？哪些 message 會引用它？
2. `BatteryState` 跟 `LaserScan` 都是 sensor_msgs，但 SubNode 上雲策略不同 — 差別是什麼？
3. `/diagnostics` 是哪個 message type？為什麼對 SubNode 很重要？
4. PointCloud2 / Image 為什麼 NG（PRD §4 NG-2）？SubNode 訂閱它們會怎樣？
5. PRD REQ-14 列了 5 包，你會建議補哪些 package 一起 pre-build？為什麼？

讀完跟我說，我寫第六章（`ros2` CLI 工具 — 客戶會用什麼來除錯我們）。
