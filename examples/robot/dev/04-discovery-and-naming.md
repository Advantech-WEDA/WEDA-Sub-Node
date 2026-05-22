---
title:    "Chapter 4 — Discovery、Naming、`ROS_DOMAIN_ID`"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 4 — Discovery、Naming、`ROS_DOMAIN_ID`

[← Chapter 3](03-msg-idl-and-cdr.md) ｜ [回 README](README.md) ｜ [下一章 → 常見 message 函式庫](05-common-message-libraries.md)

> **為什麼要讀這章？** SubNode 要當一個 DDS Participant 混進客戶 robot 的網段。
> 「混進去」這件事是 SPDP / SEDP 這兩個 discovery 協定在做。
> 如果客戶 IT 把 multicast 擋掉（hospital、logistics 現場常見），我們要有備案。
> 還有：topic 名字 `/scan` 在 wire 上其實叫 `rt/scan` — 名字轉換規則要清楚，
> 不然 fleet 多 robot 部署時兩台同名 topic 撞在一起就慘。

---

## 1. Discovery 是「持續的」，不是「一次的」

NATS / MQTT 思維：client 連 broker，broker 告訴你誰在線上。**一次** lookup。

DDS 思維：**沒有 broker，每個 Participant 自己在網段上廣播自己的存在 + 想要的 topic + 提供的 topic。所有人都聽到，自己決定要不要跟誰配對。**

這事是**持續做的**，每幾秒一次。Participant 上線就開始發、下線停發、聽不到的人就標失聯。

整個流程拆兩步：**SPDP**（誰在這？）+ **SEDP**（誰訂什麼、誰發什麼？）。

---

## 2. SPDP — Simple Participant Discovery Protocol

**「嗨我在這。」** 每個 Participant 啟動時開始定期多播這個訊息：

```
   Participant A 啟動
        │
        ▼
   每 N 秒 (預設 ~3s) 多播一份 SPDP 訊息到
   group  239.255.0.1   port  7400 + 250*DOMAIN_ID
   ┌─────────────────────────────────────────┐
   │  participant_guid = abc-123              │
   │  vendor_id = eProsima (FastDDS)         │
   │  endpoints meta = unicast 10.0.0.5:7411 │
   │  protocol_version = 2.4                 │
   │  lease_duration = 20s                   │
   └─────────────────────────────────────────┘
```

同網段其他 Participant 收到，更新自己的 peer table：

```
   Participant B 的 peer table:
   ┌─────────────────────────────────────┐
   │  abc-123 (A) → 10.0.0.5:7411       │
   │  def-456 (C) → 10.0.0.7:7411       │
   │  我自己 (B)  → 10.0.0.6:7411       │
   └─────────────────────────────────────┘
```

如果 `lease_duration` 過了還沒收到 A 的下一份 SPDP → 從 peer table 移除，所有跟 A 配對的 reader/writer 觸發 `participant_lost` 事件。

**這就是 REQ-11「Liveliness loss → `DeviceStatus.Degraded` < 5s」的底層機制**：我們把 SubNode 訂閱 robot critical topic 時的 Liveliness lease 設 5s，robot 一掛我們最多 5s 內收到 `participant_lost`，轉成 cloud 端的 device status 變更。

---

## 3. SEDP — Simple Endpoint Discovery Protocol

SPDP 只告訴大家「Participant 在這」。**SEDP 才告訴大家「我有什麼 reader / writer」。**

跑法：兩個 Participant 透過 SPDP 知道彼此後，用**單播**互傳一份 endpoint 清單：

```
   B 對 A 單播 (TCP 或 UDP 都行):
   ┌─────────────────────────────────────────────────┐
   │  「我有下列 reader/writer」                       │
   │   ─ DataReader on "rt/scan"                     │
   │        type "sensor_msgs::msg::dds_::LaserScan_"│
   │        QoS { BestEffort, Volatile, KeepLast(5) }│
   │   ─ DataReader on "rt/battery_state"            │
   │        type "sensor_msgs::msg::dds_::Battery..."│
   │        QoS { Reliable, Volatile, KeepLast(10) } │
   │   ─ DataWriter on "rr/set_modeReply"           │
   │        ...                                       │
   └─────────────────────────────────────────────────┘
```

A 收到後比對自己的 reader/writer，**topic 名 + type 名 + QoS** 三者都對得上，就建配對。**任何一項不對 → 不配對，訊息流不會建立**（這就是 Chapter 1 那個 QoS incompatibility 的根）。

**重點**：SEDP 也是持續的。你 runtime 多訂一個 topic、新增一個 service、改 QoS — 都會觸發新一輪 SEDP 廣播。對方就會發現新 endpoint。**這是 SubNode runtime 動態 attach 新 sensor 的基礎**。

---

## 4. `ROS_DOMAIN_ID` 到底是什麼

DDS 規範本來就有 **Domain** 這個概念：**同一個 domain 的 Participant 才會互相發現對方**。ROS 2 把 DDS Domain ID 包裝成 `ROS_DOMAIN_ID` 環境變數。

```bash
export ROS_DOMAIN_ID=42
```

= DDS Domain ID = 42 = 多播 group port 變成 `7400 + 250*42 = 17900`。

兩台機器設不同 `ROS_DOMAIN_ID` → 它們發的 SPDP 用不同 port → 彼此聽不到 → 永遠不發現對方。**這是純 client-side enforcement**，沒有 broker 在那邊判斷誰能進哪個 domain — 純粹是「我聽不同 port，所以我聽不見你」。

**對 fleet 部署的影響**：
- 預設 `ROS_DOMAIN_ID=0`，所有 robot 都互相看得到。**這在 fleet 不能用**（一台機器的 cmd_vel 會看到其他機器發的 cmd_vel）。
- 實務：每台 robot 一個獨立 domain（robot1=10, robot2=11...）或用 namespace（見 §7）。

| 值 | 多播 port | 備註 |
|---|---|---|
| 0 (預設) | 7400 | 大家都用 → fleet 不能用 |
| 1–166 | 7650 – 48900 | 安全範圍 |
| 167+ | > 50000 | 跟 Linux ephemeral port 重疊，**不要用** |

---

## 5. Multicast 被 block 怎麼辦

工業客戶 IT policy（hospital、logistics、製造業）常把 LAN 多播擋掉。Multicast 失效 = SPDP 收不到 = 沒人發現彼此 = 完全不通。

DDS 有三條備案：

| 方案 | 怎麼做 | 何時用 |
|---|---|---|
| **Unicast peers list**（FastDDS / Cyclone 都支援）| 手動寫死「我要單播 SPDP 給這些 IP」 | 已知 robot IP 列表的小 fleet |
| **FastDDS Discovery Server** | 起一個 server 程式，所有 Participant 連上去，server 當 SPDP 中央 broker | 中大型 fleet，多播被擋 |
| **DDS over WAN（routing service）** | RTI Routing Service、Fast-DDS Router 等 | 跨網段 / 跨防火牆 |

PRD REQ-22「rosbridge fallback」是更野的版本 — 連 DDS 都別用，改走 WebSocket。**v1.2 才做**。v1.1 主路徑是多播；多播被擋的客戶用 unicast peers 撐。

**對 SubNode 的影響**：`devicecfg.json` 必須有：

```json
{
  "ros_domain_id": 42,
  "discovery_mode": "multicast" | "unicast" | "discovery_server",
  "unicast_peers": ["10.0.0.5", "10.0.0.6"],
  "discovery_server_address": "10.0.0.100:11811"
}
```

---

## 6. Topic / Service / Action 名字怎麼從 ROS 2 轉到 DDS

死記表：

| ROS 2 端 | DDS topic 名 |
|---|---|
| topic `/scan` | `rt/scan` |
| topic `/robot1/scan` | `rt/robot1/scan` |
| service `/set_mode` request | `rq/set_modeRequest` |
| service `/set_mode` reply | `rr/set_modeReply` |
| action `/navigate_to_pose` goal | `rq/navigate_to_pose/_action/send_goalRequest` |
| action `/navigate_to_pose` result | `rq/navigate_to_pose/_action/get_resultRequest` |
| action `/navigate_to_pose` cancel | `rq/navigate_to_pose/_action/cancel_goalRequest` |
| action `/navigate_to_pose` feedback | `rt/navigate_to_pose/_action/feedback` |
| action `/navigate_to_pose` status | `rt/navigate_to_pose/_action/status` |

三個 prefix 規則：

```
   rt/  ←  reader/writer for Topic
   rq/  ←  reader/writer for service Request (含 action 的所有 service 端 request)
   rr/  ←  reader/writer for service Reply
```

**ROS 2 預設 namespace 是 `/`**，所以 `/scan` 在 DDS wire 上其實是 `rt/scan`（不是 `rt//scan`）。第一條斜線會被吃掉。

---

## 7. ROS 2 Namespace — fleet 用

Namespace 是 ROS 2 高層提供的「prefix 機制」，讓多 robot 不撞名字：

```bash
# robot 1
ros2 run my_pkg lidar_node --ros-args -r __ns:=/robot1

# robot 2
ros2 run my_pkg lidar_node --ros-args -r __ns:=/robot2
```

結果：

| 原 topic | robot 1 wire 上 | robot 2 wire 上 |
|---|---|---|
| `/scan` | `rt/robot1/scan` | `rt/robot2/scan` |
| `/cmd_vel` | `rt/robot1/cmd_vel` | `rt/robot2/cmd_vel` |
| `/set_mode` | `rq/robot1/set_modeRequest` | `rq/robot2/set_modeRequest` |

兩種 fleet 隔離法擇一：

| 方法 | 優點 | 缺點 |
|---|---|---|
| **每 robot 獨立 `ROS_DOMAIN_ID`** | 物理隔離（多播 port 都不同），完全乾淨 | 需要編排（誰是 10、誰是 11）、跨 robot 通訊要走 routing |
| **共享 domain + 不同 namespace** | 一個 fleet 都在同 domain，跨 robot 觀察方便 | 名字長、設定 prefix 要小心、有撞名風險 |

**我們 Q1 已鎖：每 robot 一個 SubNode process**。**SubNode 對應的 robot 該用哪種隔離法是客戶的事**，但我們的 config 兩種都要支援（`ros_domain_id` 必填、`namespace` 選填 — namespace 給 prefix 用）。

---

## 8. 對 SubNode 的影響（彙整）

| 課題 | 我們 SDK 該做的事 |
|---|---|
| `ROS_DOMAIN_ID` | `devicecfg.json` 必填欄位；啟動時設給 DDS Participant |
| Discovery mode | `devicecfg.json` 欄位（multicast / unicast / discovery_server），讓 IT 限制的客戶有備案 |
| Namespace | `devicecfg.json` 選填欄位，prefix 套在 topic name 前面 |
| Topic name mangling | Ros2 domain 層做 — 使用者寫 `/scan`，我們轉 `rt/<namespace>/scan` |
| `participant_lost` / `liveliness_lost` 事件 | 接住 DDS callback → 推到 SDK `IDeviceStatusObserver` → cloud 端變 `DeviceStatus.Degraded`（REQ-11）|
| 動態增刪 topic | 接住 SEDP 事件、runtime 加 reader/writer → 觸發新一輪 SEDP 廣播 |

---

## ✅ 學完這章你應該能回答

1. SPDP 跟 SEDP 各自負責什麼？兩者用多播還是單播？
2. 為什麼 `ROS_DOMAIN_ID` 不同的兩台 robot 永遠看不到對方？是誰擋的？
3. 客戶 IT 把 LAN 多播擋了，DDS 還能跑嗎？怎麼跑？
4. ROS 2 topic `/robot1/cmd_vel` 在 DDS wire 上叫什麼？
5. 「每 robot 獨立 DOMAIN_ID」跟「共享 DOMAIN_ID + namespace」兩種 fleet 隔離法，你會推給客戶哪一種？為什麼？

讀完跟我說，我寫第五章（常見 message 函式庫速查）。
