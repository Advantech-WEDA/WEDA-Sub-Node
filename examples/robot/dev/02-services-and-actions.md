---
title:    "Chapter 2 — Services & Actions 在 DDS wire 上的真相"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 2 — Services & Actions 在 DDS wire 上的真相

[← Chapter 1](01-topics-and-qos.md) ｜ [回 README](README.md) ｜ [下一章 → `.msg` IDL 與 CDR](03-msg-idl-and-cdr.md)

> **為什麼要讀這章？** 我們選了 C 路線 — 直接在 DDS 層講話。
> Service / Action 在 ROS 2 高層看起來像一個「函式呼叫」，但**在 DDS wire 上其實是兩條 topic + request_id 配對**。
> 我們要自己重現這個機制。沒搞清楚就寫，會出現「客戶 service call 過得去但收不到 response」這種詭異 bug。

---

## 1. Service 在高層看起來像什麼

寫 robot 程式的人看到的：

```python
# Server 端（robot 上）
def set_mode_callback(request, response):
    robot.mode = request.mode
    response.success = True
    return response

self.srv = self.create_service(SetMode, '/set_mode', set_mode_callback)

# Client 端
client = self.create_client(SetMode, '/set_mode')
req = SetMode.Request(mode='auto')
future = client.call_async(req)
# ...等 future 完成...
response = future.result()
```

看起來就是「呼叫一個遠端函式，等回應」。乾淨。但這是 **rcl 層幫你包好的假象**。

---

## 2. Service 在 DDS wire 上是什麼

剝開 rcl 那層，DDS 看到的 `/set_mode` service 是**兩條獨立 topic**：

```
       Client                                    Server
   ┌─────────────┐                               ┌─────────────┐
   │ writes ──► rq/set_modeRequest  ───►   read  │
   │            (client_guid +                   │
   │             sequence_number)                │
   │                                             │ 處理 ...
   │            rr/set_modeReply    ◄───  writes │
   │ read ◄──   (include the same                │
   │             client_guid +                   │
   │             sequence_number)                │
   └─────────────┘                               └─────────────┘
```

關鍵事實：

| Wire-level 機制 | 內容 |
|---|---|
| **兩條獨立 topic** | `rq/set_modeRequest`（請求）、`rr/set_modeReply`（回應）|
| **三條路 prefix 規則** | topic = `rt/<name>`、service-request = `rq/<name>Request`、service-reply = `rr/<name>Reply` |
| **request_id** | 兩個 uint64：`client_guid_hash` + `sequence_number`，附在訊息 metadata 裡 |
| **配對責任** | 由 client 自己負責 — 發 request 時記住 sequence_number，收到 reply 時用它對回去 |
| **沒有 RPC framework** | DDS 不認識「service」概念，只看到兩條 topic 在流訊息 |

**所以「呼叫 service」實際上的工作量：**

1. Client 對 `rq/set_modeRequest` publish 訊息，附 `(client_guid, sequence_number)`。
2. Server 訂閱 `rq/set_modeRequest`，收到請求，處理。
3. Server 對 `rr/set_modeReply` publish 回應，附**同一組** `(client_guid, sequence_number)`。
4. Client 訂閱 `rr/set_modeReply`，收到所有人的回應，**自己過濾**只屬於自己 `client_guid` 且 sequence_number 匹配的那一筆。

**有趣的事實**：客戶 robot 上的 `/set_mode` server 會收到**所有** client（包含其他 robot、其他工具）的 request，它在 server 端用 client_guid 區分要回給誰。我們的 SubNode 當 client 時，每送一個 request 也都會看到所有人的 reply，要自己挑屬於自己的那筆。

---

## 3. ROS 2 怎麼幫你藏這些細節

ROS 2 rcl 層做了這些事：

| Client 側 | Server 側 |
|---|---|
| 自動產生 client_guid | 自動訂 `rq/...Request` |
| 自動遞增 sequence_number | 從 metadata 抽 client_guid 跟 sequence_number |
| 自動訂 `rr/...Reply` 並過濾 | 帶上同樣的 ID 對 `rr/...Reply` publish |
| 自動把 Future 跟 sequence_number 配對 | — |
| **timeout** 邏輯（client side） | — |
| 配對成功就 resolve Future | — |

**我們走 C 路線（直接 DDS）= 上面這些事每一件都要自己做。** 工程量不大，但不能漏。

---

## 4. Service 的 QoS

預設 service QoS：

| Policy | 值 | 為什麼 |
|---|---|---|
| Reliability | **Reliable** | 指令不能掉 |
| Durability | Volatile | request/reply 都是當下事件，不存舊的 |
| History | KeepLast(10) | 短期暫存配對 |
| Deadline | ∞ | service 沒固定速率 |
| Liveliness | Automatic, ∞ lease | 用 discovery 判斷對方在不在 |

**我們的 SubNode 呼叫客戶 robot service 時，必須跟對方一樣 Reliable + Volatile + KeepLast(10)，否則 wire-level 配對失敗。** 這條規矩 hardcode 在 ROS 2 client lib 裡 — 客戶不會也不該改。

---

## 5. Service 跟 Topic 的差別（一張表收齊）

|  | Topic | Service |
|---|---|---|
| Wire 上 | 一條 topic | 兩條 topic（request + reply）|
| 通訊模式 | pub/sub，1→N | 1→1（client/server，雖然 wire 上是廣播）|
| 同步 / 非同步 | 非同步 | 同步（呼叫者等回應）|
| 訊息形態 | 一個 `.msg` | 一個 `.srv`，內含 request + response 兩個 `.msg` 段 |
| 配對 ID | 沒有 | `(client_guid, sequence_number)` |
| 預設 QoS | depends（sensor_data / default）| Reliable + Volatile + KeepLast(10) |
| Timeout | 無 | 有（client 自己管）|

---

## 6. Action — service 的進階版

Action 是「長時間任務 + 進度回報 + 可取消」。在 wire 上是 **5 條 topic** 的組合：

```
Action: /navigate_to_pose
                                     wire 上實際:
   ┌─────────────────────────────┐   rq/navigate_to_pose/_action/send_goal     (service Request)
   │     send_goal               │   rr/navigate_to_pose/_action/send_goal     (service Reply)
   ├─────────────────────────────┤
   │     get_result              │   rq/navigate_to_pose/_action/get_result    (service)
   │                             │   rr/navigate_to_pose/_action/get_result    (service)
   ├─────────────────────────────┤
   │     cancel_goal             │   rq/navigate_to_pose/_action/cancel_goal   (service)
   │                             │   rr/navigate_to_pose/_action/cancel_goal   (service)
   ├─────────────────────────────┤
   │     feedback                │   rt/navigate_to_pose/_action/feedback      (topic)
   │     status                  │   rt/navigate_to_pose/_action/status        (topic)
   └─────────────────────────────┘
```

= **3 個 service（send_goal / get_result / cancel_goal）+ 2 個 topic（feedback / status）= 共 5 個邏輯端點 = wire 上 8 條 topic**。

PRD REQ-20「ROS 2 actions」是 v1.2 才支援。**v1.1 不做 action**，所以這章你只需要知道 action 是「service+topic 的組合」即可，不必背 8 條 wire。

但要知道為什麼 v1.2 才做 action：
1. **8 條 topic 的生命週期管理複雜**：goal_id 狀態機（accepted → executing → succeeded/aborted/canceled）要自己維護。
2. **跟 SubNode 的 command 抽象不太對齊** — 既有 `ExecuteCommandAsync` 是「一發一收」，action 是「發+持續收 feedback+最終 result」，要先決定怎麼擴 SDK 的 command surface（這就是 grilling 後面 Q10 的事）。

---

## 7. 對 SubNode 的影響

把 service 機制對到 PRD：

| PRD 需求 | DDS wire 層要做的事 |
|---|---|
| REQ-07「Call high-level ROS 2 services with typed request/response, timeout, error mapping」 | 自己寫 client_guid + sequence_number 配對；自己管 timeout；錯誤要分清楚是 timeout、service 沒人接、server 回應 error |
| REQ-08「per-deployment service allow-list」 | 在 Ros2 domain 層攔截 `CallServiceAsync(serviceName, ...)`，比對 config 的 allow-list，不在裡面就拒絕（不發 wire request）|
| REQ-09「Publish to allow-listed ROS topics from cloud」 | 同樣是 allow-list 攔截，但走的是 topic publish 不是 service call |

**所以 grilling 後面會問：「Service 配對的細節（client_guid、sequence_number、timeout）應該抽在 `Ros2 domain` 的哪一層？要不要讓 FastDDS 跟 CycloneDDS 兩個 impl 各自實作，還是 domain 層統一處理？」**

預習答案傾向：**domain 層統一處理**。client_guid 跟 sequence_number 配對是 ROS 2 wire convention，跟 DDS 實作無關 — 換 impl 不影響。只有「怎麼 publish / subscribe」這層需要 swap，配對邏輯在上面。

---

## ✅ 學完這章你應該能回答

1. ROS 2 `/set_mode` service 在 DDS wire 上實際上是幾條 topic？名字長什麼樣？
2. Service 的 request 跟對應的 reply 是用什麼配對的？
3. 為什麼 service 預設 Reliability 是 Reliable、不是 BestEffort？
4. Action 在 wire 上由幾個邏輯端點組成？它跟 service 最大的差別是什麼？
5. 如果客戶 robot 上有 `/cmd_vel` 這個 topic（不是 service），我們的 REQ-08 service allow-list 擋得到它嗎？要靠什麼擋？

讀完跟我說，我寫第三章（`.msg` IDL 與 CDR 序列化）。
