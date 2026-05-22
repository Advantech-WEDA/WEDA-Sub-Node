---
title:    "Chapter 1 — Topics & QoS 深入"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-19
---

# Chapter 1 — Topics & QoS 深入

[← 回 README](README.md) ｜ [下一章 → Services & Actions 在 DDS wire 上的真相](02-services-and-actions.md)

> **為什麼要讀這章？** 後面我們設計 `Ros2QosProfile`（SDK 怎麼讓使用者描述「我要怎麼訂這個 topic」）時，
> 要決定**暴露多少 QoS 細節**。暴露太少 → 客戶安全相關的 topic 沒法調 Reliable；
> 暴露太多 → DDS 實作換 FastDDS↔Cyclone 會踩 edge case。
> 這章把選項講清楚，後面你才能參與這個決策。

---

## 1. Topic 的真實機制：DataReader / DataWriter

「Topic」其實是個邏輯名稱。真正在做事的是兩種物件：

```
                publisher node                       subscriber node
              ┌──────────────┐                     ┌──────────────┐
              │  DataWriter  │                     │  DataReader  │
              │  ┌────────┐  │                     │  ┌────────┐  │
   user code ─►  │ buffer │  │── network (RTPS) ──►│  │ buffer │  │─► user callback
              │  └────────┘  │                     │  └────────┘  │
              └──────────────┘                     └──────────────┘
                     │                                 │
                     └────── 同一個 topic name ─────────┘
                              "/battery_state"
```

關鍵點：

- **兩邊都有 buffer**。Publisher 寫進 buffer，DataWriter 慢慢送；Subscriber 收進 buffer，user 慢慢讀。
- **buffer 滿了會怎樣？** 由 QoS 決定（後面講）。
- **網路斷了怎樣？** 由 QoS 決定。
- **新訂閱者剛上線，舊資料看不到？** 由 QoS 決定。

**QoS 不是「進階功能」，是「topic 怎麼運作」的基本參數。** 一個 topic 沒設 QoS = 用了預設 QoS，不是「沒有 QoS」。

---

## 2. 為什麼一定要懂 QoS

最常見的 ROS 2 新手坑：**訂閱了 topic，但完全收不到資料。** 90% 是 QoS 對不上。

舉個例子：

```python
# Publisher（lidar）
qos = QoSProfile(reliability=ReliabilityPolicy.BEST_EFFORT, depth=10)
self.pub = self.create_publisher(LaserScan, '/scan', qos)

# Subscriber（slam）
qos = QoSProfile(reliability=ReliabilityPolicy.RELIABLE, depth=10)
self.sub = self.create_subscription(LaserScan, '/scan', cb, qos)
```

結果：**slam 收不到 lidar 的訊息。一個 byte 都沒有。**

原因：subscriber 要求 Reliable（保證每筆都到），publisher 只提供 BestEffort（盡量送、丟了不重送）。
**DDS 規定 — subscriber 要的承諾比 publisher 給的強，就拒絕配對。** 兩個 node 互相看得到彼此（discovery 成功），但訊息流不會建立。

這個現象叫 **QoS incompatibility**，是 PRD 暗示的 PAIN-2 / PAIN-4 的真實表現之一。**SubNode 必須能診斷它**，不然客戶會抱怨「你的 SubNode 抓不到我的 topic」。

---

## 3. 六個關鍵 QoS Policy

DDS 規範有 22 個 QoS policy，ROS 2 實際會用到的大概 6 個。其他都是預設值，不用碰。

### 3.1 Reliability（可靠性）

| 值 | 行為 | 用在哪 |
|---|---|---|
| `BestEffort` | 送出去就算了，丟了不重送 | 高頻率感測器（lidar、camera、joint_states）|
| `Reliable` | 保證送到（內建 ACK + 重送）| 控制指令、`/tf`、安全事件 |

直觀法則：**頻率高 → BestEffort（重送會塞死）；頻率低、丟一筆會出事 → Reliable。**

### 3.2 Durability（持久性）— 新訂閱者要不要看到舊訊息？

| 值 | 行為 | 用在哪 |
|---|---|---|
| `Volatile` | 不保留歷史，新訂閱者只看新訊息 | 預設、大部分 topic |
| `TransientLocal` | publisher 端保留最近 N 筆，新訂閱者一連上會收到 | **"latched" topics** — 設定參數、地圖、靜態 transform |
| `Transient` | 由 service-side durability 保留（DDS 規範有，但 ROS 2 沒用） | — |
| `Persistent` | 永久（DDS 規範有，但 ROS 2 沒用） | — |

實務上 ROS 2 只用 `Volatile` 和 `TransientLocal` 兩個。

**為什麼這對 SubNode 重要？** 例如 `/map` topic 是 `TransientLocal` — 地圖只發佈一次，後來才訂閱的 node 必須拿到。**SubNode 上線時如果用 `Volatile` 訂 `/map`，會永遠收不到。**

### 3.3 History（歷史保留策略）

| 值 | 行為 |
|---|---|
| `KeepLast(N)` | buffer 保留最後 N 筆，超過就丟掉最舊的 |
| `KeepAll` | buffer 全部保留（搭配 Reliable，等 ACK） |

幾乎所有 ROS 2 topic 都用 `KeepLast(N)`，差別在 N。預設 N = 10。

### 3.4 Liveliness（活著沒？）

判定 publisher 還活著的機制：

| 值 | 行為 |
|---|---|
| `Automatic` | DDS 自動發 heartbeat（最常用） |
| `ManualByTopic` | publisher 自己呼叫 `assert_liveliness()` |
| `ManualByNode` | 整個 node 主動 assert |

外加一個參數：**`lease_duration`** — 多久沒 heartbeat 就判定死掉（預設 ∞，常用 1–5 秒）。

**對 SubNode 極關鍵 — PRD REQ-11：「Liveliness loss → `DeviceStatus.Degraded` < 5s」。**
我們訂 robot 的 critical topic 時要設 `lease_duration = 5s`；DDS 會 callback「對方不見了」事件，我們就把 device 標 Degraded 推上雲。

### 3.5 Deadline（每筆樣本最大間隔）

「我要求這個 topic 每 N 毫秒至少有一筆樣本到。」

- 用在「我預期定速 publish 的東西」，例如 IMU 100 Hz。
- 超過 deadline 沒收到 → DDS 觸發 `deadline_missed` 事件。
- **跟 Liveliness 不同**：Liveliness 是「publisher 還活著嗎？」（可能活著但沒發訊息）；Deadline 是「訊息按預期速率到嗎？」。

實務常常一起用：Liveliness 5s + Deadline 200ms。

### 3.6 Lifespan（訊息保鮮期）

「這筆訊息產生超過 N 毫秒，就算過期，subscriber 不要收。」

不常用，但用於低延遲控制環境（例：teleop 命令過期就別執行了）。ROS 2 預設不設。

---

## 4. QoS 對不上會怎樣 — Reliability × Durability 配對表

|  | Publisher Volatile | Publisher TransientLocal |
|---|---|---|
| Sub Volatile | ✅ 都看新訊息 | ✅ 都看新訊息（sub 不要舊的）|
| Sub TransientLocal | ❌ **不配對**（sub 要舊，pub 不存）| ✅ 都看新+舊訊息 |

|  | Publisher BestEffort | Publisher Reliable |
|---|---|---|
| Sub BestEffort | ✅ | ✅（pub 降級到 BestEffort 給這個 sub）|
| Sub Reliable | ❌ **不配對** | ✅ |

**法則**：**Subscriber 要求 ≤ Publisher 承諾，才配對。** Sub 要 Reliable 而 Pub 只給 BestEffort，DDS 拒絕配對。

---

## 5. ROS 2 預設 QoS Profile 速查表

ROS 2 內建幾個常用 preset，客戶會直接引用：

| Preset 名 | Reliability | Durability | History | 用在 |
|---|---|---|---|---|
| `qos_profile_default` | Reliable | Volatile | KeepLast(10) | 通用，多數低頻 topic |
| `qos_profile_sensor_data` | **BestEffort** | Volatile | KeepLast(5) | **高頻感測器（lidar / camera / IMU）** |
| `qos_profile_services_default` | Reliable | Volatile | KeepLast(10) | service request/response |
| `qos_profile_parameters` | Reliable | Volatile | KeepLast(1000) | 參數變更通知 |
| `qos_profile_parameter_events` | Reliable | Volatile | KeepLast(1000) | `/parameter_events` topic |

**80/20 法則：** SubNode 訂客戶 topic 時，第一手猜測：
- 名字含 scan / image / cloud / imu / joint_states → 對方用 `sensor_data` (BestEffort)
- 其他 → 對方用 `default` (Reliable)

但這只是猜測，**SubNode 要能透過 DDS-XTypes discovery 動態問對方的 QoS**（這就是 REQ-17 為什麼會在）。

---

## 6. 對 SubNode 的影響

把上面六個 policy 對到 PRD 需求：

| QoS Policy | PRD 對應 | SDK 該暴露給 user 嗎？ |
|---|---|---|
| Reliability | REQ-10 明列 | **必須**，否則 sensor / service 對不上 |
| Durability | REQ-10 明列 | **必須**，否則 latched topic 收不到 |
| History (KeepLast N) | REQ-10「Depth」 | **必須** |
| Liveliness + lease | REQ-10 明列 + **REQ-11 直接相依** | **必須** |
| Deadline | PRD 沒明列 | **可選**，預設關，少數 critical topic 才開 |
| Lifespan | PRD 沒明列 | **不暴露**（v1.1）|

這就是後面 grilling 會問你的：「`Ros2QosProfile` 暴露這 4 個必須的 + 1 個可選的，5 個欄位，OK 嗎？還是要全部都暴露讓客戶完全控制？」

預習一下答案傾向：**只暴露 5 個**，因為 FastDDS 跟 Cyclone 在 Deadline / Lifespan 的 edge case 詮釋不同，多暴露 = 換 DDS impl 時客戶 config 失效。

---

## ✅ 學完這章你應該能回答

1. publisher 用 BestEffort、subscriber 用 Reliable，會發生什麼事？
2. `TransientLocal` 在 ROS 2 主要用在哪類 topic？舉一個例子。
3. Liveliness 跟 Deadline 的差別是什麼？
4. SubNode REQ-11 要靠哪一個 QoS policy 達成？
5. ROS 2 `sensor_data` preset 跟 `default` preset 最關鍵的差別是什麼？為什麼？

讀完跟我說，我寫第二章（Services & Actions 在 DDS wire 上的真相）。
