---
title:    "SubNode for ROS 2 — 每章衍生的關鍵設計決策"
audience: SubNode SDK 開發者、Tech Lead、Manager
status:   Draft
date:     2026-05-20
---

# SubNode for ROS 2 — 每章衍生的關鍵設計決策

[回 README](README.md)

> 這份文件把 Phase 1 七章 + Sidebar 02b 所「**牽動的 SDK 設計決策**」全部抽出來，標清楚每條決策的狀態（已鎖 / 待 grilling / PRD 已定 / v1.2 後推）。
>
> 用途：
> - Phase 3 grilling 接力的對齊清單
> - 跟 manager / tech lead 討論時的目錄
> - 未來新人加入時的決策歷史 reference
>
> 狀態定義：
>
> | 標籤 | 意義 |
> |---|---|
> | LOCKED | grilling 已鎖定或 PRD 直接寫明，不再變動 |
> | PENDING-MGR | 鎖定方向但等 manager / stakeholder 確認 |
> | PENDING-GRILL | 待 Phase 3 grilling 接力決定 |
> | PRD-DETERMINED | PRD-002 已給答案 |
> | v1.2 | PRD 明定 v1.2 才做，v1.1 不討論 |

---

## Phase 3 grilling 已決定的核心架構 (recap)

| ID | 決策 | 狀態 | 結論 |
|---|---|---|---|
| Q1 | 部署拓樸 | LOCKED | 1 SubNode process = 1 robot = 1 DDS Participant = 1 Ros2Device。Fleet 靠 K8s / docker-compose 橫向擴。 |
| Q2 | DDS / ROS 2 綁定策略 | PENDING-MGR | 傾向 C 路線（直接 DDS、跳過 rcl）。等 manager 拍板。 |
| Q3 | DDS 實作選型 | PENDING-GRILL | 傾向 FastDDS。Cyclone 換手要無痛（Q4 已保證）。 |
| Q4 | 介面分層 | LOCKED | 兩層 inversion：(a) Abstractions ↔ Ros2 domain；(b) Ros2 domain ↔ DDS impl。Ros2 domain 坐 `Core/Protocols/Ros2/`，DDS impl 坐 `Core/Protocols/Ros2/Dds/<impl>/`。 |
| Q5 | Ros2 domain 接 Abstractions 哪個 parser | PENDING-GRILL | 傾向 `IPubSubProtocolParser`，service typed-helper 由 `Ros2Device.CallServiceAsync<TReq,TRes>` 提供。 |

---

## Chapter 1 — Topics & QoS 衍生的設計決策

### D1.1 — `Ros2QosProfile` 暴露多少 QoS 欄位

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL（Q6） |
| 選項 | (a) 4 個必須 + 1 可選 = 5 欄位；(b) 全 6 個；(c) 只 3 個必須 |
| 傾向 | (a) — `Reliability`、`Durability`、`HistoryDepth`、`LivelinessLeaseDuration`、可選 `DeadlineMs`。**不暴露 Lifespan**（FastDDS / Cyclone edge case 詮釋差） |
| 關聯需求 | REQ-10、REQ-11 |

### D1.2 — 預設 QoS 策略

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL |
| 選項 | (a) 永遠用 ROS 2 `default` preset；(b) 看 topic name 自動猜（含 scan/image/imu 用 `sensor_data`）；(c) config 必填，不自動猜 |
| 傾向 | (a) 預設用 `default`，user 沒設就跟它走；自動猜會踩 §1 QoS mismatch 神坑 |
| 關聯需求 | REQ-10 |

### D1.3 — QoS preset 內建表

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | SDK 內 hardcode ROS 2 5 個 preset 對照表：`default`、`sensor_data`、`services_default`、`parameters`、`parameter_events`。使用者用 enum 引用即可。 |
| 關聯章節 | Ch1 §5 |

### D1.4 — REQ-11 Liveliness 機制

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 訂閱 critical topic 時設 `Liveliness=Automatic, lease=5s`；DDS callback `participant_lost` / `liveliness_lost` → 觸發 SDK `IDeviceStatusObserver` → cloud `DeviceStatus.Degraded`。**Automatic 是 ROS 2 publisher 主流選擇**，request 同 kind 才配對得上。 |
| 關聯需求 | REQ-11 |

### D1.5 — QoS mismatch 監測

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | DDS callback `RequestedIncompatibleQos` / `OfferedIncompatibleQos` → 推到 `/api/v1/ros2/qos-mismatch` REST endpoint + dashboard。**90% 的「訂了但收不到」破案線索**。 |
| 關聯需求 | §8.4 操作面 |

### D1.6 — 取樣 cadence pipeline

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL |
| 選項 | (a) DataReader callback 內 throttle（drop sample）；(b) 獨立 timer 拉 DataReader buffer 的 latest |
| 傾向 | (b) — 跟 publish rate 完全解耦，符合 PRD §8.2「per-sensor sample cadence independent of DDS publish rate」 |
| 關聯需求 | REQ-02、REQ-04 (sample-and-hold) |

---

## Chapter 2 — Services & Actions 衍生的設計決策

### D2.1 — Service correlation 邏輯擺哪一層

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | Ros2 domain 層做，**不**讓 FastDDS / Cyclone adapter 各做一份。`(client_guid, sequence_number)` 配對是 ROS 2 wire convention，跟 DDS impl 無關。 |
| 關聯需求 | REQ-07 |

### D2.2 — Service typed-helper API 形狀

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL |
| 選項 | (a) `Ros2Device.CallServiceAsync<TReq,TRes>(name, req, timeout)`；(b) 走 generic `ExecuteCommandAsync` 配 `object`；(c) 兩者並存 |
| 傾向 | (c) — `ExecuteCommandAsync` 走 Abstractions 統一介面（給 SDK pipeline / audit）；`CallServiceAsync<>` 在 Ros2Device 上補 type-safety（給應用層 IDE intellisense） |
| 關聯需求 | REQ-07、Q5 |

### D2.3 — Service allow-list 機制

| 項 | 內容 |
|---|---|
| 狀態 | PRD-DETERMINED |
| 結論 | 每 deployment 一份 service allow-list，攔截 `CallServiceAsync` 入口，不在 list 拒呼叫。允許列表跟 `devicecfg.json` 一起 cloud-push（含 rollback）。 |
| 關聯需求 | REQ-08 |

### D2.4 — Topic publish allow-list（與 service 分開）

| 項 | 內容 |
|---|---|
| 狀態 | PRD-DETERMINED |
| 結論 | REQ-09 是 REQ-08 的姊妹：低風險 topic 允許列表（顯示字串、LED 等）。`/cmd_vel` 之類默認不在 list（NG-3 排除）。 |
| 關聯需求 | REQ-09 |

### D2.5 — Service timeout 處理

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | Client-side timer。Timeout 後 future 標 `Errors.ServiceTimeout`，含「等待 sec」、「沒收到 reply」「server 無回應」三種具體錯誤。 |
| 關聯需求 | REQ-07 |

### D2.6 — Action 支援

| 項 | 內容 |
|---|---|
| 狀態 | v1.2 |
| 結論 | v1.1 不做。v1.2 才碰 — 8 條 wire topic + goal_id 狀態機 + 跟既有 `ExecuteCommandAsync` 抽象不對齊，要先決定怎麼擴 SDK command surface（Phase 3 後續 Q10）。 |
| 關聯需求 | REQ-20 |

---

## Chapter 3 — `.msg` IDL 與 CDR 衍生的設計決策

### D3.1 — Codegen 策略

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL（Q8） |
| 選項 | (a) build-time CLI tool；(b) Roslyn source generator；(c) runtime JIT；(d) 三者並存 |
| 傾向 | (d) 拆兩條 — **build-time CLI** 覆蓋 REQ-14（pre-built）+ REQ-15（客戶 .msg）；**runtime dynamic** 覆蓋 REQ-16（IDL discovery）+ REQ-17（XTypes）。Roslyn SG 延後 — 巢狀 `.msg` 在 SG 上難寫穩。 |
| 關聯需求 | REQ-06、REQ-14、REQ-15、REQ-16、REQ-17 |

### D3.2 — `.msg` parser

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 自寫。`.msg` 文法簡單（< 100 行 grammar），不依賴 ROS 2 toolchain，跨平台 build 簡單。輸出 AST，給 D3.3 codegen 跟 D3.4 DTDL emitter 共用。 |
| 關聯需求 | 全部 codegen 路徑 |

### D3.3 — CDR codec

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 自寫。OMG CDR spec 是固定的（封裝頭 4 byte + 自然對齊 + 變長前綴），不會變。先 ship LE-only（CDR_LE，主流），BE 之後加。 |
| 關聯需求 | 全部 codec 路徑 |

### D3.4 — DTDL auto-gen 工具鏈

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 跟 CSharp codegen 共用同一個 `.msg` parser AST。輸出 DTDL Object schema JSON。每筆 `.msg` 編輯（任何欄位變動）→ DTMI version bump，雲端立刻看見 schema drift。 |
| 關聯需求 | REQ-06 |

### D3.5 — 預先 ship 的 package 範圍

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL（Q11） |
| 傾向 | REQ-14 5 包（`std_msgs`、`sensor_msgs`、`geometry_msgs`、`nav_msgs`、`diagnostic_msgs`）+ **建議補上**：`builtin_interfaces`（依賴項，沒它不能編）、`std_srvs`（service 範例：`Trigger`、`SetBool`、`Empty`）。共 ~110 個 `.msg` / `.srv`。 |
| 關聯需求 | REQ-14 |

### D3.6 — Bounded vs unbounded array codegen

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | wire 編碼相同（4-byte length + 元素），bound 只是 codegen 端靜態約束。生出的 C# class：`int32[<=10]` 仍用 `int[]` 但 setter 檢查 length > 10 throw。 |
| 關聯需求 | — |

---

## Chapter 4 — Discovery、Naming、ROS_DOMAIN_ID 衍生的設計決策

### D4.1 — `devicecfg.json` ROS 2 區欄位

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 必含欄位：`ros_domain_id`（必填）、`discovery_mode`（multicast / unicast_peers / discovery_server）、`unicast_peers`（list）、`discovery_server`（addr）、`namespace`（選填）。 |
| 關聯需求 | REQ-01、REQ-10 |

### D4.2 — Discovery mode 三選一支援

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | v1.1 必須三種 mode 都支援（multicast 預設 / unicast_peers / discovery_server）。Hospital、logistics 必須有 unicast 備案。 |
| 關聯需求 | §11 風險（multicast 被擋） |

### D4.3 — Topic name mangling 在哪一層做

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | Ros2 domain 層做。使用者寫 `/scan`，內部轉 `rt/<namespace>/<name>`；service 轉 `rq/...Request` + `rr/...Reply`。**單元測試一定要覆蓋 namespace 三段式跟 service 兩條 topic 對應。** |
| 關聯需求 | 全部 wire 路徑 |

### D4.4 — Liveliness lease 預設值

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 5 秒（直接對應 REQ-11 SLA）。Critical topic 可在 config 個別調短。 |
| 關聯需求 | REQ-11 |

### D4.5 — Fleet 拓樸 — config 支援哪幾種

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 三種都支援（flat / per-robot-domain / hub-and-spoke），靠 D4.1 三個 config 欄位（`ros_domain_id` + `namespace` + `discovery_mode`）組合而成。Q1 已鎖 1 SubNode = 1 robot，所以 SubNode 自己只看「我這台 robot 在哪個 domain / namespace」。 |
| 關聯需求 | §9.1、REQ-21 (v1.2) |

### D4.6 — rosbridge fallback

| 項 | 內容 |
|---|---|
| 狀態 | v1.2 |
| 結論 | v1.2 才做。v1.1 用 unicast peers / Discovery Server 撐 multicast 被擋場景。 |
| 關聯需求 | REQ-22 |

---

## Chapter 5 — 常見 message 函式庫衍生的設計決策

### D5.1 — Pre-built codegen 是否包含「不該上雲」型別

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | **codec 全做，文件警示哪幾個別發**。例如 `PointCloud2`、`Image`、`CompressedImage` 我們仍 ship codec（客戶可能在本地處理用得到），但 Sensor 上雲 config 預設拒收這些 type。NG-2 防護在 telemetry pipeline 那層做，不在 codec 那層做。 |
| 關聯需求 | REQ-14、NG-2 |

### D5.2 — Composite message（REQ-05）的型別涵蓋

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 凡是 `geometry_msgs/*Stamped`、`sensor_msgs/BatteryState` / `Imu` / `NavSatFix` / `JointState`、`nav_msgs/Odometry` 等「結構性 state」訊息 → 默認以 `application/json` Composite + 自動生 DTDL Object schema 上雲。客戶可在 sensor config 覆寫。 |
| 關聯需求 | REQ-05、REQ-06 |

### D5.3 — `/diagnostics` 自動接管

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL |
| 選項 | (a) SubNode 預設自動訂 `/diagnostics`（如果客戶 robot 有發）；(b) 客戶要 explicit 在 config 開 |
| 傾向 | (a) — robot 已自己評估好健康狀態，現成 health report 不接太可惜。但要支援 (b) opt-out。 |
| 關聯需求 | REQ-11、§8.4 |

### D5.4 — `frame_id` / TF 處理

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | **不做 TF 變換**。`frame_id` 當 telemetry metadata 帶上雲就好。NG-6 已聲明我們不做 nav stack。 |
| 關聯需求 | NG-6 |

---

## Chapter 6 — Tooling 衍生的設計決策

### D6.1 — REST API 最低端點集合

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | v1.1 必須提供 5 個 endpoint：`/api/v1/ros2/subscriptions`、`/api/v1/ros2/services`、`/api/v1/ros2/discovery`、`/api/v1/ros2/qos-mismatch`、`/api/v1/ros2/stats/{topic}`。 |
| 關聯需求 | §8.4 |

### D6.2 — Dashboard 在 SubNode 角色定位

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 主要除錯介面（替代客戶習慣的 `ros2 node info`）。長期目標：SI / robotics engineer 學會用 dashboard 比學 `ros2` CLI 更省事。 |
| 關聯需求 | §8.4、§9.4 |

### D6.3 — 文件 onboarding 必出 page

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 至少 3 篇必出：(1)「Why `ros2 node list` doesn't show SubNode」、(2)「Debugging cheat sheet」（3 大常見問題）、(3)「`ros2` CLI ↔ SubNode dashboard 對照表」。 |
| 關聯需求 | §9.4 |

### D6.4 — Schema mismatch surface

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | `.msg` 端變動 → DTMI version bump → 推到 dashboard + 雲端 schema explorer。客戶看到「battery_state schema 從 v3 升 v4，多了 cell_voltage 欄位」這種訊息。 |
| 關聯需求 | REQ-06 |

---

## Chapter 7 — Security & fleet patterns 衍生的設計決策

### D7.1 — DDS-Security 啟用模式

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 預設 `enabled=false`（unsecured）。`enabled=true` 時必須提供完整 keystore (CA、cert、key、governance、permissions)，**任何一個缺 / 過期 / 簽不過 → 拒絕啟動**，不允許 silent fallback。 |
| 關聯需求 | §5 compliance、§11 IEC 62443 SL2 |

### D7.2 — sros2 keystore 相容

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 接受標準 sros2 產的 keystore 格式（X.509 + signed XML）。客戶用 `ros2 security` 工具統一管 robot + SubNode 的 cert / permissions。我們不發明新格式。 |
| 關聯需求 | §11、§5 |

### D7.3 — REQ-08 service allow-list 在 secured mode 下仍保留

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | 保留。理由：(1) 大多客戶不開 DDS-Security；(2) defence-in-depth — permissions XML 配錯時 SDK 有第二層；(3) 允許列表雲端也能看到（permissions 是 wire-only） |
| 關聯需求 | REQ-08、§11 |

### D7.4 — Latency SLA 分 mode

| 項 | 內容 |
|---|---|
| 狀態 | PENDING-GRILL |
| 選項 | (a) secured / unsecured 一份 SLA；(b) 分兩份 |
| 傾向 | (b) — secured P95 < 3s（unsecured 是 < 2s）。AES-GCM 加密有 5-30% overhead。 |
| 關聯需求 | §3.2 |

### D7.5 — Fleet broadcast (REQ-21)

| 項 | 內容 |
|---|---|
| 狀態 | v1.2 |
| 結論 | v1.2 才做。三種拓樸都要適用，hub-and-spoke 多一跳 latency 可接受。 |
| 關聯需求 | REQ-21 |

---

## Sidebar 02b — DDS vs NATS 衍生的設計決策

### DS.1 — 不引入 NATS 思維到 SDK ROS 2 區

| 項 | 內容 |
|---|---|
| 狀態 | LOCKED |
| 結論 | SDK ROS 2 layer 不可有「broker / persistent stream / opaque payload」這些 NATS 假設。文件 / API 命名都要避免讓使用者誤以為「ROS 2 = NATS over multicast」。Phase 1 sidebar 文件當作 onboarding 必讀。 |
| 關聯需求 | NG-4 |

---

## Phase 3 grilling 接下來要排的議題（暫定順序）

| Q | 議題 | 來自決策 |
|---|---|---|
| Q6 | `Ros2QosProfile` 暴露範圍 | D1.1 |
| Q7 | 預設 QoS 策略 | D1.2 |
| Q8 | Codegen 策略 | D3.1 |
| Q9 | 取樣 cadence pipeline 實作位置 | D1.6 |
| Q10 | v1.2 action surface 抽象 | D2.6 |
| Q11 | Pre-built 包範圍最終確認 | D3.5 |
| Q12 | `/diagnostics` 自動接管 | D5.3 |
| Q13 | Service typed-helper API 形狀 | D2.2 |
| Q14 | Latency SLA 是否分 secured / unsecured | D7.4 |
| (manager) | Q2 — C 路線 / A 路線最終確認 | Q2 |
| (manager) | Q3 — FastDDS 選型 | Q3 |
| (manager) | Q5 — Abstractions 介面選 IPubSubProtocolParser | Q5 |

---

## 統計

| 狀態 | 數量 |
|---|---|
| LOCKED | 21 |
| PRD-DETERMINED | 2 |
| PENDING-MGR | 3 |
| PENDING-GRILL | 9 |
| v1.2 | 3 |

**38 條決策已盤點完。** Phase 3 grilling 預估再走 ~9 題就能把 v1.1 設計面鎖完。
