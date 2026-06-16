---
title:    "Phase 2 — ROS 2 / DDS 期末測驗"
audience: 完成 Phase 1 七章的學員
status:   Draft
date:     2026-05-20
---

# Phase 2 — ROS 2 / DDS 期末測驗

[回 README](README.md)

> **規則：**
> 1. **不要看課程資料作答。** 卡住可以略過，所有題目寫完後一次提交。
> 2. 每題標難度：★（簡單）／★★（中等）／★★★（情境應用）
> 3. 每題標來源 chapter — 改錯時可以回頭翻
> 4. 共 20 題，分 8 個 section
> 5. 通過標準：**正確率 ≥ 80%**，且情境題（★★★）不可有錯
> 6. 作答方式：直接打在 chat 裡，可以一次全交或分 section 交

---

## Section A — Topics & QoS（4 題）

### A1（★，Ch1）
一個 publisher 設 `BestEffort + Volatile + KeepLast(5)`，一個 subscriber 要求 `Reliable + Volatile + KeepLast(10)`。會發生什麼？

- (a) 訊息流建立，subscriber 收到所有訊息
- (b) 訊息流建立，但 subscriber 可能丟訊息
- (c) 訊息流不建立，subscriber 收不到任何資料
- (d) DDS 自動降級 subscriber 為 BestEffort

### A2（★★，Ch1）
解釋 `TransientLocal` 跟 `Volatile` 的差別。舉一個 ROS 2 中典型用 `TransientLocal` 的 topic 例子，並解釋為什麼。

### A3（★★，Ch1）
SubNode 要把客戶 robot 的 Liveliness loss 在 5 秒內傳到雲端標 `DeviceStatus.Degraded`（REQ-11）。我們應該怎麼設定 SubNode 訂閱該 topic 的 QoS？

- (a) Liveliness=Automatic, lease_duration=5s
- (b) Liveliness=ManualByNode, lease_duration=5s
- (c) Deadline=5s
- (d) History=KeepLast(5)

### A4（★★★，Ch1）
SI 跟你回報「我訂了 `/scan` 但完全收不到資料，`/odom` 沒問題」。請列出排查順序（至少 3 步）。

---

## Section B — Services & Actions（3 題）

### B1（★，Ch2）
`/set_mode` service 在 DDS wire 上由幾條 topic 組成？

- (a) 一條
- (b) 兩條
- (c) 三條
- (d) 跟一般 topic 一樣，一條

### B2（★★，Ch2）
Service request/reply 的配對機制是什麼？為什麼需要這個機制？（提示：跟 DDS 沒 broker 有關）

### B3（★★★，Ch2）
客戶要求我們支援 `/navigate_to_pose` action 從雲端發起。PRD 把 actions 排在 v1.2。為什麼不是 v1.1？列出兩個原因。

---

## Section C — `.msg` IDL & CDR（3 題）

### C1（★，Ch3）
CDR 編碼開頭 4 byte 是什麼？

- (a) Magic number 識別 ROS 2 訊息
- (b) Encapsulation header — representation identifier + options
- (c) 訊息總長度
- (d) Topic name hash

### C2（★★，Ch3）
`.msg` 內的 `uint8 STATUS_OK=0` 這種常數，會出現在 wire 上嗎？為什麼？對 codegen 有什麼影響？

### C3（★★，Ch3）
SubNode v1.1 要支援 REQ-15「客戶私有 `.msg` package」+ REQ-16「runtime IDL discovery」。為什麼兩條都要做、不能只做一條？

- (a) REQ-16 比較快，REQ-15 比較準
- (b) REQ-15 解「我自己擁有 .msg」、REQ-16 解「我事先不知道 .msg」
- (c) REQ-16 是 REQ-15 的 fallback
- (d) PRD 只是冗餘列出，做 REQ-16 就涵蓋

---

## Section D — Discovery & Naming（3 題）

### D1（★★，Ch4）
ROS 2 topic `/robot2/cmd_vel` 在 DDS wire 上的 topic name 叫什麼？

- (a) `cmd_vel`
- (b) `robot2/cmd_vel`
- (c) `rt/cmd_vel`
- (d) `rt/robot2/cmd_vel`

### D2（★★，Ch4）
SPDP 跟 SEDP 各自做什麼？兩者各用 multicast 還是 unicast？

### D3（★★★，Ch4）
客戶 IT 把 LAN 多播全部擋了。SubNode 還能跑嗎？SDK config 要怎麼設？寫出至少一條解法 + 對應的 config 欄位。

---

## Section E — Common message libraries（2 題）

### E1（★，Ch5）
以下哪個 sensor_msgs 訊息，SubNode 通常**不會**整筆原樣上雲？

- (a) BatteryState
- (b) NavSatFix (GPS)
- (c) PointCloud2
- (d) Temperature

### E2（★★，Ch5）
`Header.msg` 在哪一包？兩個欄位是什麼？SubNode 拿到 `frame_id` 該怎麼處理？

---

## Section F — Tooling & customer view（2 題）

### F1（★★，Ch6）
C 路線（直接 DDS）下，客戶用 `ros2 topic info /battery_state -v` 看我們訂的 topic，會看到什麼？

- (a) 我們不會出現
- (b) 看到 Subscriber 條目，含 GUID 跟 QoS，但 Node name 是 `_NODE_NAME_UNKNOWN_`
- (c) 看到完整 node 資訊跟 SubNode 名字
- (d) 只看到 publisher count + subscription count

### F2（★★★，Ch6）
SI 跟你抱怨「裝完 SubNode 但 robot 訊息收不到」。請按優先級給出排查步驟（至少 3 步），並對應到我們要提供的 REST endpoint。

---

## Section G — Security & fleet patterns（2 題）

### G1（★★，Ch7）
`ROS_DOMAIN_ID` 跟 DDS-Security 在「擋人進來」的功能上差別是什麼？

- (a) 完全一樣
- (b) DOMAIN_ID 是 client-side port 區分，沒在「擋」；DDS-Security 才是真擋（憑證 + 加密）
- (c) DOMAIN_ID 比較安全
- (d) DDS-Security 取代 DOMAIN_ID

### G2（★★★，Ch7）
PRD §11 要求 IEC 62443 SL2。客戶 compliance officer 問你：「DDS-Security 開啟後，REQ-08 service allow-list 還需要做嗎？」你怎麼回答？（至少 2 個理由）

---

## Section H — DDS vs NATS（1 題）

### H1（★★★，Sidebar 02b）
同事說「DDS 應該跟 NATS 差不多吧，可以直接用 NATS Stream 來做 ROS 2 message broker」。請列三點駁回。

---

## 作答範例格式

```
A1: c
A2: TransientLocal 保留歷史給新訂閱者，Volatile 不保留。
    例：/map — 地圖只發一次，後加入的 node 必須拿到才能定位。
A3: a
A4: 1. ...
    2. ...
    3. ...
B1: b
...
```

寫完跟我說，我批改。
