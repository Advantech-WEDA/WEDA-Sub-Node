---
title:    "Chapter 6 — `ros2` CLI 工具 — 客戶會用什麼來除錯我們"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 6 — `ros2` CLI 工具 — 客戶會用什麼來除錯我們

[← Chapter 5](05-common-message-libraries.md) ｜ [回 README](README.md) ｜ [下一章 → DDS-Security & 多 robot](07-security-and-fleet-patterns.md)

> **為什麼要讀這章？** 我們走 C 路線 = **不會出現在 `ros2 node list`**。
> 客戶 robotics engineer 第一次部署 SubNode 時，幾乎一定會跑 `ros2 node info` 來「確認你訂了什麼」 — 然後找不到我們，恐慌。
> 我們要事先想好替代診斷路徑（PRD §8.4「Local REST API for on-edge diagnostics」），並且訓練 SI 怎麼接話。

---

## 1. `ros2` CLI 速覽

ROS 2 安裝後內建一支 `ros2` 命令，子命令很多但**客戶最常用就 6 條**：

```bash
ros2 topic       # topic 相關（最常用）
ros2 service     # service 相關
ros2 node        # node 相關 ← 我們在這條看不到
ros2 interface   # 看 .msg / .srv 結構
ros2 doctor      # 健康檢查
ros2 bag         # 錄製 / 回放（不影響我們）
```

---

## 2. `ros2 topic` — 我們**看得到**的部分

### `ros2 topic list`

列出網段上所有 topic。**我們訂閱的 topic 會出現在這裡**，因為 list 是看 DDS-level discovery。

```bash
$ ros2 topic list
/battery_state
/cmd_vel
/diagnostics
/odom
/parameter_events
/rosout
/scan
/set_mode/... (service-related)
```

### `ros2 topic echo /battery_state`

印出 topic 的訊息內容。**完全不受我們影響** — 客戶用這個看 robot 自己發的訊息，跟我們訂不訂無關。

### `ros2 topic info /battery_state -v`

這條客戶會用來「**確認 SubNode 有沒有訂上**」。**重點：`-v` 加上去**才有 GUID 細節：

不加 `-v`：

```
$ ros2 topic info /battery_state
Type: sensor_msgs/msg/BatteryState
Publisher count: 1
Subscription count: 1
```

加 `-v`：

```
$ ros2 topic info /battery_state -v
Type: sensor_msgs/msg/BatteryState

Publishers:
 * Node name: battery_node
   Node namespace: /
   Topic type: sensor_msgs/msg/BatteryState
   Endpoint type: PUBLISHER
   GID: 01.10.f2.5a...
   QoS profile:
     Reliability: RELIABLE
     ...

Subscribers:
 * Node name: _NODE_NAME_UNKNOWN_       ← 我們是這個
   Node namespace: _NODE_NAMESPACE_UNKNOWN_
   Topic type: sensor_msgs/msg/BatteryState
   Endpoint type: SUBSCRIPTION
   GID: 01.0f.ff.aa...                  ← 我們的 GUID
   QoS profile:
     Reliability: RELIABLE
     ...
```

**這就是 C 路線在客戶 terminal 上的真實樣貌**：客戶看得到「有人訂了」、看得到 QoS、看得到 GUID — **但看不到「人是誰」**。

### `ros2 topic hz / bw`

量 publish 頻率、頻寬。**完全不受我們影響**。

### `ros2 topic pub`

從 command line 發 topic 到 robot。客戶測試環境會用。**SubNode 不影響**。

---

## 3. `ros2 service` — 我們**看得到**的部分

### `ros2 service list`

列出網段上所有 service。**我們呼叫的 service 不會出現在客戶的 list**（list 只看 server 端）。但客戶 robot 上的 service 我們**呼叫得到**。

### `ros2 service call /set_mode std_msgs/SetMode "{mode: 'auto'}"`

客戶從 command line 直接呼叫 robot 上的 service。SubNode 不影響。

### `ros2 service type /set_mode`

查 service 的 type — 客戶會用這個確認 SubNode config 裡的 service type 寫對沒。

---

## 4. `ros2 interface show sensor_msgs/msg/BatteryState`

看 `.msg` 內部結構。客戶看到的就是 [Chapter 5](05-common-message-libraries.md) 那種長相。**SubNode 不影響**。

```bash
$ ros2 interface show sensor_msgs/msg/BatteryState
std_msgs/Header header
        builtin_interfaces/Time stamp
                ...
float32 voltage
float32 temperature
...
```

---

## 5. `ros2 node` — 我們**看不到**的部分（C 路線的痛點）

### `ros2 node list`

```bash
$ ros2 node list
/battery_node
/cmd_vel_mux
/lidar_driver
/nav2_planner
/slam_node
```

**我們不會出現在這裡。** 客戶第一次部署完 SubNode、想確認「你連上了沒」，第一條指令通常就是這個 — 然後找不到我們。

### `ros2 node info /<我們>`

直接報 not found（因為我們根本沒註冊到 graph）。

---

## 6. Foxglove Studio / rqt_graph — 視覺化

客戶 robotics engineer 在 development 階段會用：

- **`rqt_graph`**：畫 node 圖譜（誰連誰）。**我們不會在圖上**。
- **`Foxglove Studio`**：3D 視覺化 + topic explorer。Topic / publisher / subscriber 的列表 **看得到我們的 GUID 出現**（同 `ros2 topic info -v`），但**看不到我們是哪個 node**。

---

## 7. C 路線的「客戶第一印象」會是什麼

當客戶 SI 第一次裝完我們的 SubNode 之後，會發生這樣的對話：

> **客戶**：「我裝完了，但 `ros2 node list` 看不到你們？」
> **我們**：「對，我們是 DDS-native peer，不註冊 ROS 2 graph。要看訂閱關係請用 `ros2 topic info /battery_state -v`，或打開 SubNode dashboard。」
> **客戶**：「為什麼不註冊？」
> **我們**：「為了部署輕量化（不用裝 ROS 2 runtime）跟跨 distro 相容。代價是 `ros2 node` 看不到我們，但 topic / service 一切正常。」

**這段對話必須事先寫進文件**。SI / Sales 第一次客戶 call 不能脫稿。

---

## 8. 我們提供的替代診斷面

PRD §8.4 提到「**Local REST API for on-edge diagnostics**」 — 這就是用來補 `ros2 node` 看不到我們的空白。

最少要提供的端點：

| 端點 | 回什麼 | 解什麼問題 |
|---|---|---|
| `GET /api/v1/ros2/subscriptions` | 我訂了哪些 topic、QoS、配對狀態 | 取代 `ros2 node info <我們>` 的訂閱清單 |
| `GET /api/v1/ros2/services` | 我準備呼叫哪些 service | 同上的 service 部分 |
| `GET /api/v1/ros2/discovery` | DDS peer table（我看到哪些 Participant、GUID）| 確認 SubNode discovery 健康 |
| `GET /api/v1/ros2/qos-mismatch` | **配對失敗的 topic 清單**（Chapter 1 那個坑）| **第一線除錯神器** — 「我訂了但收不到」90% 是這個 |
| `GET /api/v1/ros2/stats/{topic}` | 訂閱頻率、丟包率、最後訊息時戳 | 確認「資料有流」 |

**這些是 PRD §8.4 落地的最小集合。** SI 拿到 dashboard 比 `ros2 node info` 還快上手，因為 dashboard 上**直接顯示 QoS mismatch**（ROS 2 自己沒這功能，要客戶 fight 半天）。

---

## 9. 我們文件要寫什麼（PRD §9.4 partner enablement）

SI 上手包必須有的 page：

1. **"Why `ros2 node list` doesn't show SubNode"** — 一頁解釋 + 替代路徑
2. **"Debugging cheat sheet"** — 三條最常見問題：
   - 「我訂了但收不到」→ 看 dashboard QoS mismatch
   - 「我看不到 robot」→ 確認 ROS_DOMAIN_ID 一致 + multicast 沒被擋
   - 「service call 永遠 timeout」→ 確認 service allow-list + service type 跟 wire 上一致
3. **"Mapping ros2 CLI commands to SubNode dashboard"** — 一張對照表

對 SI 而言，「會看 dashboard」比「會用 `ros2` CLI」省事得多。長期 SubNode 會變成 SI 的主要除錯介面 — **但只在文件寫好的前提下**。

---

## 10. 對 PRD 的影響（彙整）

| PRD 條目 | 這章對應的 SDK 工作 |
|---|---|
| §8.4「Local REST API for on-edge diagnostics」 | 上面 §8 那 5 個 endpoint 至少要有 |
| §9.4 partner enablement | 上面 §9 那 3 個 doc page 必出 |
| §5 「Robotics Software Engineer」persona 「clear schema-mismatch diagnostics」 | QoS mismatch + schema mismatch（REQ-06 DTMI bump）兩種診斷面都要在 dashboard 上 |
| C 路線的部署輕量代價 | 用 dashboard + REST 補 `ros2 node` 缺口 |

---

## ✅ 學完這章你應該能回答

1. 客戶用 `ros2 topic list` 會看到我們訂的 topic 嗎？為什麼？
2. 客戶用 `ros2 topic info /battery_state -v` 會看到什麼跟我們相關的東西？
3. 客戶用 `ros2 node list` **不會**看到我們的原因是什麼？要怎麼跟客戶解釋？
4. SubNode 必須提供哪一個 REST endpoint 才能取代 `ros2` CLI 沒辦法看到的 QoS mismatch 診斷？
5. 一個 SI 抱怨「我裝好 SubNode 但 robot 的訊息收不到」— 你會請他先看什麼診斷面？

讀完跟我說，我寫最後一章（DDS-Security & 多 robot patterns）。
