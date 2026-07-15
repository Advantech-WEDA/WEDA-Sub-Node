---
title:    "Chapter 3 — `.msg` IDL 與 CDR 序列化"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 3 — `.msg` IDL 與 CDR 序列化

[← Chapter 2](02-services-and-actions.md) ｜ [回 README](README.md) ｜ [下一章 → Discovery & Naming](04-discovery-and-naming.md)

> **為什麼要讀這章？** 我們走 C 路線（直接 DDS），訊息上 wire 是 **CDR** 二進位編碼。
> 客戶 robot 那邊用 `.msg` 描述訊息格式 → ROS 2 工具產 CDR codec。
> 我們要在自家 SDK 做**同樣的事**（吃 `.msg`、產 C# class + CDR codec）。
> 這章把這條工具鏈攤開，後面才有辦法討論「codegen 用什麼策略」。

---

## 1. `.msg` 長什麼樣

`.msg` 是純文字檔，描述訊息結構。實例：

```
# std_msgs/Header.msg
builtin_interfaces/Time stamp     # 時戳
string frame_id                   # 在哪個座標系
```

```
# sensor_msgs/BatteryState.msg
std_msgs/Header header
float32 voltage                   # 電壓 (V)
float32 current                   # 電流 (A)
float32 charge                    # 目前電量 (Ah)
float32 capacity                  # 完整電容量 (Ah)
float32 percentage                # 0.0–1.0
uint8 power_supply_status

# 常數（編譯期值，不佔 wire）
uint8 POWER_SUPPLY_STATUS_UNKNOWN=0
uint8 POWER_SUPPLY_STATUS_CHARGING=1
uint8 POWER_SUPPLY_STATUS_DISCHARGING=2
uint8 POWER_SUPPLY_STATUS_NOT_CHARGING=3
uint8 POWER_SUPPLY_STATUS_FULL=4

string location                   # 「Slot 3」之類
string serial_number
```

```
# sensor_msgs/LaserScan.msg
std_msgs/Header header
float32 angle_min
float32 angle_max
float32 angle_increment
float32 time_increment
float32 scan_time
float32 range_min
float32 range_max
float32[] ranges                  # 變長陣列（unbounded）
float32[] intensities
```

關鍵觀察：

- **註解**：`#` 開頭。
- **欄位**：`<型別> <欄位名>`。
- **常數**：`<型別> <名稱>=<值>`。**不上 wire**，是程式碼端的常數。
- **巢狀**：可以引用另一包的 `.msg`（`std_msgs/Header`）。
- **陣列**：`[]` = unbounded 變長、`[N]` = 固定長 N、`[<=N]` = bounded 最多 N。

---

## 2. 基本型別

| `.msg` 型別 | C# 對應 | wire 大小 |
|---|---|---|
| `bool` | `bool` | 1 byte |
| `byte` | `byte` | 1 byte |
| `char` | `byte` | 1 byte |
| `int8` / `uint8` | `sbyte` / `byte` | 1 byte |
| `int16` / `uint16` | `short` / `ushort` | 2 byte |
| `int32` / `uint32` | `int` / `uint` | 4 byte |
| `int64` / `uint64` | `long` / `ulong` | 8 byte |
| `float32` / `float64` | `float` / `double` | 4 / 8 byte |
| `string` | `string` | 4-byte length + bytes + null |
| `wstring` | `string` (UTF-16) | 4-byte length + bytes |
| `builtin_interfaces/Time` | `(int sec, uint nanosec)` | 8 byte |
| `builtin_interfaces/Duration` | `(int sec, uint nanosec)` | 8 byte |

陣列三種：

| 寫法 | 意義 | wire 編碼 |
|---|---|---|
| `int32[]` | 變長，無上限 | 4-byte 長度 + N × 元素 |
| `int32[10]` | 固定長 10 | 純 10 × 4 = 40 byte（無長度前綴）|
| `int32[<=10]` | 變長，但編譯期保證 ≤ 10 | wire 跟 unbounded 一樣（4-byte 長度 + 元素）— bound 只是 codegen 時的靜態檢查 |

**對 SubNode 的影響**：bounded 跟 unbounded 在 wire 一樣，所以 codec 不用分兩條路。但生出的 C# class 要不要把 `int32[<=10]` 變成 `int[10]` fixed-size buffer，是 codegen 設計題（後面 grilling Q8 會碰）。

---

## 3. `.srv` 與 `.action`

`.srv`（service）和 `.action`（action）都是 `.msg` 的組合，用 `---` 分段：

```
# my_pkg/srv/SetMode.srv
string mode                       # request 段
---
bool success                      # response 段
string message
```

```
# my_pkg/action/NavigateToPose.action
geometry_msgs/PoseStamped pose    # goal 段
---
bool reached                       # result 段
---
float32 progress                  # feedback 段（持續回傳）
string current_state
```

CDR 編碼上，**`.srv` 跟 `.action` 都會被 ROS 2 工具拆成多個獨立 `.msg`**（`SetMode_Request.msg` + `SetMode_Response.msg`），分別產生 codec。**所以我們的工具鏈只要做 `.msg` codegen 就夠了**，`.srv` 和 `.action` 自動掉進來。

---

## 4. 型別命名規則

ROS 2 type 永遠是三段式：

```
   <package_name> / <category> / <TypeName>

   例：
   sensor_msgs    / msg        / BatteryState
   sensor_msgs    / msg        / LaserScan
   nav2_msgs      / srv        / SetMode
   nav2_msgs      / action     / NavigateToPose
```

`<category>` 只有三種：`msg`、`srv`、`action`。

在 DDS wire 上，這個名字會被轉成：

```
   sensor_msgs/msg/BatteryState  ──►   sensor_msgs::msg::dds_::BatteryState_
```

（注意尾巴的底線跟 `dds_::` 命名空間 — 這是 ROS 2 工具鏈固定的 mangling。我們的 codec 也要照做。）

---

## 5. CDR — wire 上實際是什麼

**CDR**（Common Data Representation）是 OMG 標準的二進位序列化格式。所有 DDS 流量都是 CDR。

### 5.1 編碼長什麼樣

拿 `BatteryState`（簡化版）來看：

```
   message:
     header.stamp = {sec=1700000000, nanosec=500000000}
     header.frame_id = "battery_0"
     voltage = 12.4
     percentage = 0.83
```

CDR 編碼成（little-endian）：

```
   位移   bytes                          意義
   ────  ─────────────────────────────  ─────────────────────────────
   0     00 01 00 00                    封裝頭（4 byte）
                                        ├─ 00 01 = CDR_LE (little-endian)
                                        └─ 00 00 = options (通常 0)
   4     00 80 5E 65                    stamp.sec  = 1700000000 (int32)
   8     00 65 CD 1D                    stamp.nanosec = 500000000 (uint32)
   12    0A 00 00 00                    frame_id 長度 = 10 (含 null)
   16    62 61 74 74                    "batt"
   20    65 72 79 5F                    "ery_"
   24    30 00                          "0\0"
   26    00 00                          padding 到 4-byte 對齊
   28    CD CC 46 41                    voltage = 12.4 (float32)
   32    52 B8 54 3F                    percentage = 0.83 (float32)
```

### 5.2 CDR 三條規則

CDR 編碼有幾條死硬規則：

1. **封裝頭 4 byte**：開頭兩 byte 是 `{magic, endian}`：
   - `00 00` = CDR_BE (big-endian, 罕見)
   - `00 01` = CDR_LE (little-endian, 主流)
   - 後兩 byte 是 options（通常 `00 00`）
2. **自然對齊**：每個欄位前要 padding 到自己大小的整數倍位移。`int32` 必須對齊到 4 的倍數、`float64` 對齊到 8 的倍數。
3. **變長型別前綴**：
   - `string` = 4-byte 長度（含 null terminator）+ bytes + null
   - `sequence` (`int32[]`) = 4-byte 長度 + 元素們

### 5.3 怎麼解 CDR

**必須事先知道型別結構**（欄位順序、每個欄位的型別）。CDR 是純位元流，沒有自描述。

這就是為什麼 DDS-XTypes（REQ-17）才存在：它讓 wire 上多傳一份 IDL 描述，讓對方可以**動態**解碼未知型別。預設 CDR 不做這件事。

---

## 6. 為什麼不能用 JSON / protobuf

兩個原因：

1. **DDS 規範就是 CDR**。客戶 robot 的 DataWriter 一定發 CDR，我們的 DataReader 一定要會解 CDR。沒有 negotiation 的空間。
2. **效能**：CDR 接近 zero-copy（自然對齊 + 連續記憶體）。JSON 文字解析、protobuf varint 解碼都比 CDR 慢一個量級，且結果還要 padding 才能放進 C struct。

換言之：**CDR 是 DDS 的「母語」，我們別無選擇**。

---

## 7. 對 SubNode 的影響

PRD 把 `.msg` 相關需求列了四條：

| 需求 | 講人話 | 工具鏈責任 |
|---|---|---|
| REQ-14 | `std_msgs`、`sensor_msgs`、`geometry_msgs`、`nav_msgs`、`diagnostic_msgs` 預先 ship | 我們**先**跑 codegen，把這幾包 `.msg` 變成 .NET assembly 包進 SDK |
| REQ-15 | 客戶私有 `.msg` 包可以在自家 project 宣告 | 客戶 project 加 `<Ros2Msg Include="..." />`，build 時 codegen 產 C# class |
| REQ-16 | runtime IDL discovery（沒見過的型別）| 連上去後從 DDS-XTypes 拿 IDL，**runtime 動態**建 codec |
| REQ-17 | DDS-XTypes wire discovery（更野的版本）| 同上但 gated，off by default |
| REQ-06 | DTDL schema auto-gen | `.msg` IDL → DTDL Object — codegen 順便產一份 DTDL JSON |

我們要寫的工具（**這就是 Phase 3 grilling Q8 要決的範圍**）：

```
┌─────────────────────────────────────────────────────────────┐
│  Tool 1: .msg parser                                         │
│  輸入: .msg 文字檔                                            │
│  輸出: 抽象語法樹 (AST) — 欄位清單 + 型別 + 常數              │
└──────────────┬──────────────────────────────────────────────┘
               │
       ┌───────┴───────────────┬───────────────────────┐
       ▼                       ▼                       ▼
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│ Tool 2:     │         │ Tool 3:     │         │ Tool 4:     │
│ C# class    │         │ CDR codec   │         │ DTDL emitter │
│ generator   │         │ generator   │         │             │
│             │         │             │         │             │
│ class       │         │ Serialize() │         │ {"@type":   │
│ BatteryState│         │ Deserialize │         │  "Object",  │
│ {           │         │             │         │   "fields": │
│   Time      │         │             │         │   [...]}    │
│   Stamp;    │         │             │         │             │
│   ...       │         │             │         │             │
│ }           │         │             │         │             │
└─────────────┘         └─────────────┘         └─────────────┘
   REQ-14/15              REQ-14/15                REQ-06
```

加上 runtime 路徑：

```
┌─────────────────────────────────────────────────────────────┐
│  Tool 5: Runtime dynamic codec                               │
│  輸入: 從 DDS-XTypes 拿到的 IDL 或 TypeObject                 │
│  輸出: 一個 runtime ICdrCodec 實例，能 encode/decode dynamic │
└─────────────────────────────────────────────────────────────┘
                                                  REQ-16 / REQ-17
```

**Codegen 策略大決定（Q8 預習）：**

- **a) Build-time CLI tool**：`dotnet ros2-msg-gen *.msg --output gen/`，輸出 .cs 檔，客戶 commit 進 repo。簡單，但 IDE 沒有 live regen。
- **b) Roslyn source generator**：把 `.msg` 加進 `AdditionalFiles`，IDE 即時 regen C# class、intellisense 直接亮。對 .NET 開發者體驗最好，但 source gen 上線難度比 CLI 高。
- **c) Runtime JIT**：啟動時讀 `.msg` 動態建 codec。不要前置編譯，但每次啟動有 cold cost、IDE 看不到強型別。
- **d) 三者並存**：build-time 主路徑（覆蓋 REQ-14/15）、runtime JIT 主路徑覆蓋 REQ-16/17。Roslyn SG 是錦上添花。

預習答案傾向：**(d) — build-time CLI（給 REQ-14/15）+ runtime dynamic（給 REQ-16/17），先不做 source generator**。

理由：source generator 對 `.msg` 這種**會引用其他 `.msg`**（巢狀）的場景比較難寫穩，而工具長期維護成本高。先用 CLI + runtime 兩條路，Roslyn SG 之後客戶有人 request 再做。

---

## ✅ 學完這章你應該能回答

1. `int32[]`、`int32[10]`、`int32[<=10]` 在 wire 上的編碼差異是什麼？哪些是 codegen 時檢查、哪些是 runtime 檢查？
2. ROS 2 `.msg` 裡的 `常數`（例如 `uint8 STATUS_OK=0`）會上 wire 嗎？為什麼？
3. CDR 編碼開頭 4 byte 是什麼？為什麼要這個？
4. 為什麼我們不能用 JSON 取代 CDR？
5. REQ-15「客戶私有 `.msg`」跟 REQ-16「runtime IDL discovery」分別解決什麼問題？兩者重複嗎？

讀完跟我說，我寫第四章（Discovery、Naming、`ROS_DOMAIN_ID`）。
