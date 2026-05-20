---
title:    "Sidebar — DDS vs NATS：給 NATS 使用者的對照表"
audience: 熟悉 NATS、要學 DDS 的工程師
status:   Draft
date:     2026-05-20
---

# Sidebar — DDS vs NATS：給 NATS 使用者的對照表

[回 README](README.md)

> **這篇是參考用，不是課程主線。** 如果你熟悉 NATS，
> 用這份對照表把 NATS 直覺校準到 DDS 上，
> 後面 Chapter 3–7 看起來會更快。

---

## TL;DR

**直覺對的部分：** DDS 跟 NATS 都是 pub/sub。兩邊的 request/reply 都是「pub/sub 上層的慣例」 — NATS 用 `inbox subject + reply correlation`，DDS（ROS 2 service）用 `rq/rr 兩條 topic + (client_guid, sequence_number)`。模式一樣。

**直覺錯的部分：** DDS 沒有中央 broker、topic 是強型別、QoS 是配對的必要條件、沒有外部持久化。

---

## 結構性差異一覽

| 維度 | NATS | DDS（ROS 2 用的） |
|---|---|---|
| **架構** | 有 `nats-server` 中央 broker，client 連它 | **真 peer-to-peer，沒 broker**。每個 Participant 用 multicast discovery 找對方，然後直連 |
| **Discovery** | server 維護 subject 路由表 | **SPDP**（多播「嗨我在這」）+ **SEDP**（單播「我有這些 reader/writer」），每個 peer 自己維護 peer table |
| **Subject / Topic 型別** | 不分型別，payload 是 opaque bytes，你自己挑 protobuf / JSON / msgpack | **強型別**。每個 topic 綁定一個 IDL type，wire format 是 CDR。型別資訊本身可以 discovery（DDS-XTypes）|
| **QoS** | Core 是 at-most-once；JetStream 加 stream 持久化 | **22 個 QoS policy**，per-DataReader/DataWriter；requested-vs-offered 配對機制 |
| **傳輸** | TCP 到 server | **UDP 為主**（discovery 必走 multicast；data 可 UDP/TCP）|
| **「持久化」** | JetStream 後端 stream | **沒有外部持久化**。`TransientLocal` 是 writer 端保留 N 筆。writer 一掛、late joiner 就讀不到 |
| **HA / Clustering** | NATS cluster | **沒有對應概念**。Participant 就是 process 本身 |
| **多帳號 / 隔離** | NATS accounts | `DOMAIN_ID` — 不同 domain 互相看不到，純 client-side enforcement |
| **Subject 命名** | `foo.bar.baz` 點分層級，可 wildcard `foo.*` / `foo.>` | Topic 是平面字串。ROS 2 在上面加 `/` 慣例 |

---

## 你可以保留的 NATS 直覺

- ✅ **pub/sub 心智模型** — DataReader/DataWriter ≈ subscriber/publisher
- ✅ **request/reply 是 pub/sub 上層慣例** — DDS service 就是這樣
- ✅ **subject/topic name 就是路由** — DDS 還會用 type name 做第二層過濾
- ✅ **沒有「連線」概念，只有「訊息」概念** — DDS 也沒有 `client.connect()`

## 你要丟掉的 NATS 直覺

- ❌ **「broker 是路由樞紐」** — DDS 沒有 broker。網路上多播 packet 到 robot，robot 自己決定要不要收。
- ❌ **「subject 是 string，payload 是 bytes」** — DDS topic 帶 IDL type，型別不對連 discovery 都不配對。
- ❌ **「QoS 是後加的進階功能」** — DDS QoS 是 first-class，沒設 = 用預設值；不同 QoS 配對失敗 = 訊息流根本不建立。
- ❌ **「用 NATS Stream 持久化歷史」** — DDS 沒這層。要歷史 → writer 端 `TransientLocal`，writer 掛了就沒了。
- ❌ **「掛掉的 client 重連就好」** — DDS 沒有「重連」。Participant 不見 → 對方在 Liveliness lease 過後標你失聯。
- ❌ **「subject hierarchy + wildcard」** — DDS topic 是平面字串。ROS 2 用 `/` 是純命名慣例，不是 wildcard 機制。

---

## 對 SubNode 設計的影響

| 如果照 NATS 直覺設計會出現 | DDS 真實情況下該怎麼做 |
|---|---|
| 「我們有個 message broker，SubNode 連上就好」 | 沒 broker。SubNode 本身就是 Participant，要會講 multicast SPDP/SEDP |
| 「payload 是 JSON，我們解析它」 | payload 是 CDR encoded，要會 codec（Chapter 3 主題）|
| 「QoS 用預設就好，要調再說」 | QoS 是配對的必要條件，預設值就會擋住 70% 的 sensor topic |
| 「reliability 加在 client lib」 | DDS Reliable 是 wire 層的 ACK + 重送，writer/reader 雙方協商 |
| 「用 wildcard 訂 `foo.*`」 | 不行。要訂哪些 topic 就一個一個 explicitly subscribe |
| 「broker 重啟，所有人重連」 | 沒人重連。Participant 之間 Liveliness lease 一過互相標失聯 |

---

## Request/Reply 細節對比

兩邊都「在 pub/sub 上加 correlation 做 RPC」，但細節不同：

| 步驟 | NATS request/reply | ROS 2 service over DDS |
|---|---|---|
| Client 怎麼宣告「我要收 reply」 | 訂閱一個臨時 inbox subject（`_INBOX.xxx`），跟 request 一起塞進 header | 啟動時就訂閱固定 reply topic（`rr/<service>Reply`），不分臨時 |
| Server 怎麼回 | publish 到 request header 帶的 reply subject | publish 到固定的 `rr/<service>Reply` topic，附 metadata |
| Reply 的 routing | NATS server 直接送回那個 inbox subject | 所有 subscribe 該 reply topic 的 client 都會收到，**自己過濾** `client_guid` |
| Correlation token | reply subject 本身就是 token | `(client_guid, sequence_number)` 在訊息 metadata |
| 多 client 對同一個 service | inbox 唯一 → 沒衝突 | 全部 client 共享同一條 reply topic，**filter on read** |

**重點**：DDS 的 reply 是「廣播給所有 client，自己過濾」這件事在 NATS 很奇怪，但它是 DDS 模型的副產品（沒 broker → 沒人能單點轉發）。我們寫 SubNode service client 時要記得做這層 filter。

---

## 速查：把 NATS 詞彙翻成 DDS

| NATS | DDS / ROS 2 |
|---|---|
| `nats-server` | （沒有對應，是 broker-less）|
| Subject | Topic（加 IDL type）|
| Client | Participant |
| Subscription | DataReader |
| Publisher | DataWriter |
| Account | Domain（`DOMAIN_ID`）|
| Queue group | （DDS 沒直接對應，要用 partition + QoS 組合）|
| Inbox / reply subject | `rr/<service>Reply` topic + `client_guid` 過濾 |
| JetStream Stream | （沒有；`TransientLocal` 只在 writer 端保留 N 筆）|
| `nats sub foo.*` wildcard | （沒有；要 explicit subscribe）|

---

## 為什麼 DDS 要這樣設計？

NATS 是為「微服務 / 雲端訊息」設計的：訊息頻率中等、broker 可控、雲環境穩定。

DDS 是為「**即時控制系統**」設計的（軍工、汽車、航太、機器人）：
- **沒 broker** — 因為 broker 是 single point of failure，戰機 / 飛彈 / 自走車不能容忍。
- **強型別** — 因為控制系統的訊息格式是嚴格契約，wire 層攔截錯型別比讓 app 解 JSON 失敗早。
- **22 個 QoS** — 因為「我要 10 ms 內到」「過期樣本不算數」「對方斷線 50 ms 內 fail-fast」這些是控制迴路的硬需求。
- **多播 UDP** — 因為一台車內 100 個 node 跑同一網段，廣播分發是最有效率的方式。

換言之，**DDS 是把「即時控制系統的常見需求」直接寫進 middleware**；NATS 是「我給你管道，需求你自己上層加」。

---

## 一句話收斂

> **NATS 是「broker 上的 pub/sub」，DDS 是「網段上的 pub/sub」。**

我們的 SubNode 走 C 路線，就是要當網段上一個 DDS Participant，跟客戶 robot 直接講 RTPS，沒有中間人。
