---
title:    "ROS 2 入門 — 為 SubNode SDK 整合而寫"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-19
---

# ROS 2 入門 — 為 SubNode SDK 整合而寫

本文挑「跟 SubNode 整合決策會用到的部分」講，不塞細節。配合 [docs/.dev/ros2.md](../../docs/.dev/ros2.md)（PRD-002）一起讀。

---

## 一句話

**ROS 2 是給機器人寫的「程式內部通訊框架」。** 一台 robot 內部會有十幾、幾十個小程式（叫 node），它們透過 ROS 2 互相講話。

不是作業系統。是 Linux/Windows 上跑的一套 library + middleware。

---

## 比喻：一台 robot 內部是「公司」

一台 AMR（自走車）內部大概長這樣：

```
              robot 主機板 (Linux)
   ┌────────────────────────────────────────┐
   │  ┌────────┐  ┌────────┐  ┌──────────┐  │
   │  │ lidar  │  │ motor  │  │ camera   │  │  ← 一堆「node」
   │  │ node   │  │ ctrl   │  │ node     │  │     每個是獨立的小程式
   │  └────────┘  └────────┘  └──────────┘  │
   │  ┌────────┐  ┌────────┐  ┌──────────┐  │
   │  │ slam   │  │ nav    │  │ battery  │  │
   │  │ node   │  │ planner│  │ monitor  │  │
   │  └────────┘  └────────┘  └──────────┘  │
   │       ↕  ROS 2 內部訊息匯流排  ↕         │
   └────────────────────────────────────────┘
```

每個 node 是公司一個員工，做自己的事。它們互相不直接呼叫對方的函式 — **透過「ROS 2 訊息匯流排」喊話**。

---

## 三個核心概念

只要記這三個：

### 1. Topic — 廣播訊息（pub/sub）

「我有東西要說，誰想聽就聽。」

```
lidar node ──發佈──► /scan ──訂閱──► slam node
                            ──訂閱──► obstacle avoid node
```

- `/scan` 是 topic 名字（像群組聊天室名字）
- lidar 一秒 publish 10 次「我看到什麼」到 `/scan`
- 其他 node 訂閱 `/scan`，就會收到

robot 內常見 topic 例子：

- `/battery_state` — 電池狀況（每秒 1 次）
- `/odom` — 車子目前位置（每秒 50 次）
- `/scan` — 雷射雷達（每秒 10–40 次）
- `/cmd_vel` — 移動指令（每秒 10–50 次）

### 2. Service — 同步呼叫（request/response）

「我要拜託你做件事，等你回我。」

```
mission manager ──呼叫──► /set_mode  ──回應──► mission manager
                          (auto/manual)
```

- 比 topic 重 — 是雙向、會等回應
- robot 內常見 service 例子：
  - `/set_mode` — 把 robot 設成自動 / 手動模式
  - `/dispatch` — 派任務（去 A 點搬東西到 B 點）
  - `/calibrate` — 校正感測器
  - `/emergency_stop` — 緊急停車確認

### 3. Action — 長時間任務（有進度回報）

「我要拜託你做件需要時間的事，過程中回報進度，做完告訴我，我中途可以取消。」

```
mission manager ──goal──► /navigate_to_pose ──feedback──► mission manager
                                              "走了 30%"
                                              "走了 60%"
                                          ──result──► "到了" / "撞到牆"
```

- 像 service，但執行時間以秒、分鐘計
- 例如「導航到 (5.2, 3.1) 座標」可能要 30 秒，過程中每秒回報進度
- **這就是 PRD-002 REQ-20 講的「ROS 2 actions」，v1.2 才支援**

---

## 程式碼大概長什麼樣

robot 上寫程式的人（客戶的 robotics engineer）會寫類似這樣（Python）：

```python
# 發佈 topic
class LidarNode(Node):
    def __init__(self):
        self.pub = self.create_publisher(LaserScan, '/scan', qos)
        self.timer = self.create_timer(0.1, self.publish_scan)  # 10 Hz

    def publish_scan(self):
        msg = LaserScan()
        msg.ranges = read_from_hardware()
        self.pub.publish(msg)
```

```python
# 訂閱 topic + 呼叫 service
class MissionNode(Node):
    def __init__(self):
        self.sub = self.create_subscription(BatteryState, '/battery_state', self.on_battery, qos)
        self.client = self.create_client(SetMode, '/set_mode')

    def on_battery(self, msg):
        if msg.percentage < 0.2:
            req = SetMode.Request(mode='return_to_dock')
            self.client.call_async(req)
```

**我們要做的事**：就是寫一個「假裝是 robot 上一個 node」的程式，但它跑在 SubNode 上，**訂閱客戶 robot 的 topic、呼叫客戶 robot 的 service**，把資料聚合後送上雲。客戶 robot 程式碼一個字都不用改。

---

## ROS 2 底下：DDS

robot 內部 node 之間怎麼通訊？答案不是 ROS 2 自己寫的網路層 — 它**用 DDS**。

```
   node ─→ 你寫 ROS 2 API
            │
            ▼
        rclpy / rclcpp  ← ROS 2 library
            │
            ▼
          rmw  ← middleware 抽象層
            │
            ▼
        DDS impl  ← 真正講網路的人 (FastDDS / CycloneDDS)
            │
            ▼  RTPS 協定 (over UDP)
        網路
```

DDS 不是 ROS 2 發明的 — 是 OMG 國際標準（軍工、汽車、航太用很久了）。ROS 2 把它選成 middleware。

**我們選的路線是直接在 DDS 層講話，跳過 ROS 2 的 rcl 層。** 對 robot 來說「有人在跟我講 RTPS」就夠了 — 它不需要知道我們不是 ROS 2 node。

---

## rcl 跟 DDS 的分工（深入版）

一句話：**rcl 是 DDS 之上的便利層。沒打開任何 DDS 沒有的網路能力 — 它只是把 DDS 原有的東西包成 ROS 2 風味。**

具體展開：

```
   ┌──────────────────────────────────────────────────────────────────┐
   │  你寫的 ROS 2 程式碼                                                │
   │                                                                   │
   │    create_publisher(LaserScan, '/scan', qos_profile_sensor_data)  │
   │    create_client(SetMode, '/set_mode')                            │
   │    client.call_async(req)                                         │
   └──────────────────────────────────────┬───────────────────────────┘
                                          │
   ╔══════════════════════════════════════▼═══════════════════════════╗
   ║   rcl  /  rclcpp  /  rclpy           ─  ROS 2 慣例 + 簿記         ║
   ║                                                                  ║
   ║    '/scan'                  ──name mangle──►   "rt/scan"        ║
   ║                                                                  ║
   ║    qos_profile_sensor_data  ──expand──►   { BestEffort,         ║
   ║                                              Volatile,          ║
   ║                                              KeepLast(5), ... } ║
   ║                                                                  ║
   ║    LaserScan (C++ 類別)     ──typesupport──►  CDR codec +        ║
   ║                                                DDS type name     ║
   ║                                                                  ║
   ║    call_async(req)          ──correlation──►  publish to         ║
   ║                                                "rq/set_modeReq"  ║
   ║                                                + (guid, seq=42)  ║
   ║                                                + subscribe       ║
   ║                                                "rr/set_modeReply"║
   ║                                                + filter by guid  ║
   ║                                                + match seq=42    ║
   ║                                                + resolve future  ║
   ║                                                                  ║
   ║    （順便）註冊 node graph  ──►  ros2 node list 看得到我          ║
   ╠══════════════════════════════════════╤═══════════════════════════╣
   ║   rmw（抽象層）                       │                          ║
   ╠══════════════════════════════════════▼═══════════════════════════╣
   ║   DDS (FastDDS / CycloneDDS)        ─  網路 + 真實傳輸           ║
   ║                                                                  ║
   ║    建 DataWriter on topic "rt/scan"                              ║
   ║         with type "sensor_msgs::msg::dds_::LaserScan_"           ║
   ║         with QoS { ... }                                         ║
   ║                                                                  ║
   ║    SPDP 多播「嗨我是 participant X，在 domain 42」                ║
   ║    SEDP 單播「我有以下 reader/writer」                            ║
   ║    QoS requested-vs-offered 配對                                  ║
   ║    Reliable → ACK / NACK / 重送                                  ║
   ║    Liveliness → 定時 heartbeat / lease 過期觸發 lost event        ║
   ║    CDR encode/decode 真正的 byte                                  ║
   ║    RTPS 封包打包 / 解包                                            ║
   ╚══════════════════════════════════════╤═══════════════════════════╝
                                          │
                                          ▼
                            網路（RTPS over UDP，多播 + 單播）
```

哪一層做什麼：

```
   工作                                     rcl    DDS
   ─────────────────────────────────────   ────   ────
   '/scan' 字串轉 "rt/scan"                  ✅
   QoS preset 展開成 6 policy                ✅
   類別 ↔ CDR bytes 的 glue                  ✅
   service correlation (guid, seq)          ✅
   timeout / future 處理                     ✅
   node graph 註冊                           ✅
   parameter server / lifecycle / tf2       ✅
   ─────────────────────────────────────         ────
   Discovery (SPDP / SEDP multicast)              ✅
   QoS 配對協商                                    ✅
   ACK + 重送                                     ✅
   Liveliness heartbeat                          ✅
   CDR encode/decode 的位元級實作                  ✅
   RTPS 封包 / 多播 / UDP 路由                     ✅
```

我們的兩個路線選擇：

```
   A 路線 — 走 rcl                          C 路線 — 走 DDS
   ─────────────────                       ────────────────
   ┌──────────────────────┐                ┌──────────────────────┐
   │  SubNode 邏輯         │                │  SubNode 邏輯        │
   ├──────────────────────┤                │                      │
   │  rcl + C shim +      │ ◄ 用它便利     │  ↑ 我們自己重寫      │
   │  P/Invoke            │                │     rcl 那些便利     │
   ├──────────────────────┤                │     （只做我們要的） │
   │  DDS (FastDDS)       │                ├──────────────────────┤
   └──────────┬───────────┘                │  DDS (FastDDS)       │
              │                            └──────────┬───────────┘
              │                                       │
              └──────── RTPS over UDP ────────────────┘
                  (兩條路最終在這裡相遇,
                   wire format 完全一樣)

   交付:                                   交付:
   ─ SubNode binary  30 MB                 ─ SubNode binary  30 MB
   ─ ROS 2 runtime  500 MB                 ─ (only)
   ─ 每個 distro 一個 build                ─ distro-agnostic

   能力:                                   能力:
   ─ ros2 node list  ✅ 看得到我們          ─ ros2 node list  ❌ 看不到
   ─ parameter / lifecycle  ✅              ─ (不做)
   ─ service / topic  ✅ 全自動             ─ service correlation 自己寫
```

C 路線可行的根本理由：**rcl 沒打開任何 DDS 沒有的網路能力**。我們省下 rcl 的便利，自己重寫**我們需要的子集**（subscribe / call service / QoS preset 對照），但**得到的能力跟 A 完全一樣 — 因為 wire 上跑的就是同一條 RTPS**。

---

## 訊息格式：`.msg` 檔

每個 topic / service 有固定的訊息格式。客戶寫個 `.msg` 文字檔定義：

```
# BatteryState.msg
float32 voltage
float32 percentage
uint8   power_supply_status
string  serial_number
```

ROS 2 工具會把 `.msg` 編譯成 C++/Python 類別。**我們也得會吃 `.msg`**（這就是 PRD-002 REQ-15）。

ROS 2 內建一堆標準 `.msg` 包（`std_msgs`、`sensor_msgs`、`geometry_msgs`、`nav_msgs`、`diagnostic_msgs`），客戶機器人 80% 用這幾包。客戶自己也可以定義 `.msg`（REQ-15 那塊）。

---

## ROS 2 vs ROS 1

|  | ROS 1 | ROS 2 |
|---|---|---|
| 中央 broker | 有（`roscore`，掛了就全死） | **沒有**（DDS peer-to-peer 自己發現） |
| 加密 / 認證 | 沒 | 有（DDS Security） |
| 即時性 | 普通 | 較好 |
| 平台 | Linux only | Linux / Win / Mac |
| 狀態 | **2025 EOL，棄用中** | 在用 |

PRD-002 NG-8 明說不支援 ROS 1，因為它已經死了。

---

## Distro（版本）

ROS 2 像 Ubuntu，**每年出新版**，名字是英文字母遞增的動物 / 概念：

| Distro | 發佈年 | 類型 | 狀態（2026 年）|
|---|---|---|---|
| Foxy | 2020 | LTS | EOL |
| **Humble** | 2022 | **LTS** | **🟢 主力**（PRD 目標 floor）|
| Iron | 2023 | 非 LTS | EOL |
| **Jazzy** | 2024 | **LTS** | 🟢 升級中 |
| Kilted | 2025 | 非 LTS | 🟡 少數早期採用 |

客戶現場 80%+ 還在 Humble（2027 EOL 才會大規模升級）。所以 PRD-002 OI-10 還在 open，但**我們直接走 DDS 層，這個 distro 問題對我們是輕傷**（DDS wire 跨 distro 穩定）。

---

## `ROS_DOMAIN_ID`

DDS 有「domain」概念 — 同一個 domain 的 participant 才會互相發現對方。

```bash
export ROS_DOMAIN_ID=42
```

robot A 跟 robot B 設成 domain 42，它們就會自動發現對方；設成不同 domain 就互相看不到。

**對我們**：SubNode 啟動時要設成跟客戶 robot 一樣的 `ROS_DOMAIN_ID`，否則我們連不上。這是 `devicecfg.json` 一個必填欄位。

---

## 我們 SubNode 在這個世界扮演什麼角色

```
            客戶 robot
   ┌────────────────────────────┐                     雲端
   │ lidar / motor / ... node   │                  ┌────────┐
   │                            │  RTPS over UDP   │ Weda   │
   │  ROS 2 message broker   ◄──┼────────────────► │ Core   │
   │                            │                  │        │
   │ (客戶程式碼,不改)            │  我們 SubNode     │        │
   └────────────────────────────┘  「混進去當另一    └────────┘
              ▲                     個 node 訂閱」        ▲
              │                     的角色                │
              │                                          │
              └──── SubNode (Linux box, 我們的 binary) ───┘
                        │
                  訂閱 /battery_state、/odom...
                  呼叫 /set_mode、/dispatch...
                  彙整後 1 Hz 上傳雲端
```

**SubNode 對 robot 而言**：是另一個「會講 DDS 的 node」（從 robot 看就是普通鄰居）。
**SubNode 對雲而言**：是一個 device，跟 PLC、感測器一樣會回報 telemetry、收 command。
