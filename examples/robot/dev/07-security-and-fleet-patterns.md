---
title:    "Chapter 7 — DDS-Security 與多 robot 部署模式"
audience: SubNode SDK 開發者、SI 工程師
status:   Draft
date:     2026-05-20
---

# Chapter 7 — DDS-Security 與多 robot 部署模式

[← Chapter 6](06-tooling-and-customer-view.md) ｜ [回 README](README.md) ｜ Phase 2 → [EXAM](EXAM.md)（即將生）

> **為什麼要讀這章？**
> Chapter 4 講過 `ROS_DOMAIN_ID` **不是安全邊界**（任何人改自己的 DOMAIN_ID 就能聽進來）。
> 真的要安全，靠 DDS-Security（憑證 + 加密 + access control）。
> 但**大多客戶沒開** — 我們的 SubNode 要兩種 mode 都會。
> PRD §5 compliance persona、§11 IEC 62443 SL2 都會走到這。
> 另外把 Chapter 4 的多 robot 故事延伸完整。

---

## 1. ROS 2 安全的兩個層級

```
   ┌───────────────────────────────────────┐
   │  SROS2 — ROS 2 包裝版 (per-node policy) │  ← rcl 層
   ├───────────────────────────────────────┤
   │  DDS-Security — OMG 標準 (per-domain)  │  ← DDS 層 ← 我們在這
   └───────────────────────────────────────┘
```

**我們走 C 路線（DDS 層），所以對接 DDS-Security 即可，SROS2 不必碰。** 兩者使用同一個底層機制，只是 SROS2 多了一層 ROS 2 命名慣例（namespace + node 名綁 access policy）。

---

## 2. DDS-Security 三大支柱

OMG DDS-Security 規範定義五個 plugin，**三個重要**：

| Plugin | 做什麼 | 用什麼技術 |
|---|---|---|
| **Authentication** | 確認對方 Participant 是「合法成員」才允許加入 domain | X.509 憑證 + 握手 |
| **Access Control** | 規定誰可以 publish / subscribe 哪個 topic、呼叫哪個 service | XML governance + permissions file（簽名）|
| **Cryptographic** | 流量加密 + 完整性檢查 | AES-GCM（symmetric key）|
| Logging | 安全事件 audit log | — |
| Data Tagging | 訊息層級分類 | 罕用 |

### 2.1 Authentication 怎麼跑

每個 Participant 啟動時帶三樣東西：

```
   ┌─────────────────────────────────────┐
   │  identity_ca.pem      ← CA 根憑證    │
   │  identity_cert.pem    ← 我的身份證  │
   │  private_key.pem      ← 我的私鑰    │
   └─────────────────────────────────────┘
```

兩個 Participant 在 SPDP 階段認出對方後，**接著做一個 challenge-response 握手**：

```
   A → B：「我是 robot1, 這是我的憑證(被 CA 簽)」
   B → A：「我是 subnode, 這是我的憑證」
   A ↔ B：互相驗證簽章 + 對話金鑰交換
   配對成功 → 後續所有資料加密
   配對失敗 → 互相直接無視（連 SEDP 都不傳）
```

### 2.2 Access Control 怎麼跑

`permissions.p7s`（CA 簽名的 XML）描述：

```xml
<grant name="subnode_permissions">
  <subject_name>CN=subnode</subject_name>
  <allow_rule>
    <domains><id>42</id></domains>
    <subscribe>
      <topics>
        <topic>rt/battery_state</topic>
        <topic>rt/odom</topic>
        <topic>rt/diagnostics</topic>
      </topics>
    </subscribe>
    <publish>
      <topics>
        <!-- 沒有，我們是訂閱者 -->
      </topics>
    </publish>
  </allow_rule>
  <default>DENY</default>
</grant>
```

**這是 REQ-08 service allow-list 的「強硬版」**：在 DDS-Security 開啟的環境，這個 permissions 檔就是 wire-level enforcement，**任何 Participant 想偷訂 / 偷發都被 DDS 直接擋下**，不需要靠應用層 allow-list。

### 2.3 Cryptographic

握手成功後，所有 RTPS payload AES-GCM 加密。**通常開啟 + 加密 = 額外 10–30% CPU、5–10% 延遲**。對 SubNode 影響不大（我們不在高頻 sensor 路徑），但客戶 robot 上的高頻 topic 可能會被 perf 影響到。

---

## 3. SROS2 — ROS 2 包裝版

`sros2` 命令列工具產生這些憑證 + permissions XML：

```bash
ros2 security create_keystore ~/keystore
ros2 security create_enclave ~/keystore /robot1
# 生 identity_ca.pem / identity_cert.pem / governance.p7s / permissions.p7s
```

對我們**走 C 路線**而言：`sros2` 工具我們**不用**，但**產出的檔案（憑證 + permissions）格式必須相容** — 因為客戶會用 sros2 統一管所有 robot + SubNode 的安全配置。

---

## 4. 為什麼大多客戶沒開 DDS-Security

OEM AMR、研究機構、小 fleet 開啟率**遠低於 10%**。原因：

1. **PKI 是大坑**：CA 怎麼開、憑證怎麼 rotate、私鑰怎麼 deploy → SI 沒能力 / 沒時間。
2. **Perf 代價**：高頻 sensor topic 加密可能要硬體升級。
3. **除錯困難**：握手失敗的訊息流幾乎看不到（DDS 直接無視對方），新人花一週才找到「啊原來憑證沒簽到」。
4. **OEM 預設不開**：MiR、Boston Dynamics、UR 出廠的 ROS 2 endpoint 預設都關 security。客戶要動就是動整片。

**所以 SubNode v1.1 兩種 mode 都要會** — secured / unsecured，配置切換不要 break。

---

## 5. SubNode 該怎麼面對 DDS-Security

`devicecfg.json` 安全相關欄位：

```json
{
  "security": {
    "enabled": false,                          // 預設關
    "identity_ca": "/etc/subnode/ca.pem",
    "identity_cert": "/etc/subnode/cert.pem",
    "private_key": "/etc/subnode/key.pem",
    "governance": "/etc/subnode/governance.p7s",
    "permissions": "/etc/subnode/permissions.p7s"
  }
}
```

對 PRD 條目：

| PRD 條目 | SubNode 該做 |
|---|---|
| §5 Compliance Officer persona | `enabled=true` 時必須**強制驗證 CA 鏈 + permissions 有效期**，過期就拒絕啟動（不能 fallback 到 unsecured，那是 silent downgrade attack）|
| §11 IEC 62443 SL2 | 提供「全 fleet 開 security + 跟既有 sros2 keystore 整合」的 reference deployment |
| §8.4 Two-phase config rollback | secured / unsecured 切換是高風險變更 → 必須走 rollback 流程，切失敗自動回上一版 |
| §3.2 P95 latency < 2s | secured mode P95 要重新量（perf overhead），可能要在 secured profile 放寬到 < 3s |

---

## 6. Multi-robot 部署模式進階

延伸 Chapter 4 的兩種隔離法，實務上常見三種拓樸：

### 6.1 Flat fleet（共享 domain + namespace）

```
   ┌────────────────────────────────────────────────────┐
   │  網段 / DOMAIN_ID = 42                              │
   │                                                    │
   │  robot1  robot2  robot3  ...  robot10              │
   │   /robot1/scan   /robot2/scan  ...                 │
   │   /robot1/cmd_vel /robot2/cmd_vel ...              │
   │                                                    │
   │  subnode1 subnode2 ... subnode10                  │
   │   （each 訂自己 robot 的 topic + namespace 前綴）   │
   └────────────────────────────────────────────────────┘
```

**最常見**。Chapter 4 §7 的推薦解。

### 6.2 Per-robot domain（每 robot 獨立 domain）

```
   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
   │ DOMAIN 10    │  │ DOMAIN 11    │  │ DOMAIN 12    │
   │ robot1       │  │ robot2       │  │ robot3       │
   │ subnode1     │  │ subnode2     │  │ subnode3     │
   └──────────────┘  └──────────────┘  └──────────────┘
```

**多 OEM mixed fleet** 用得多 — 兩家 OEM 在同網段但互不信任，每家一個 domain。

### 6.3 Hub-and-spoke（DDS Discovery Server）

```
                  ┌──────────────────┐
                  │ Discovery Server │  ← Chapter 4 §5 提的備案
                  │ (中央 broker-like)│     在 multicast 被擋時用
                  └────────┬─────────┘
              ┌────────────┼────────────┐
              ▼            ▼            ▼
          robot1       robot2       subnode1
                       subnode2     subnode3
```

**hospital / logistics 必用**：IT policy 擋多播，全部走 unicast 經中央 Discovery Server。

### 6.4 對 SubNode 的影響

`devicecfg.json` 三種拓樸都要支援：

```json
{
  "ros_domain_id": 42,
  "namespace": "/robot1",
  "discovery_mode": "multicast" | "unicast_peers" | "discovery_server",
  "unicast_peers": ["10.0.0.5", "10.0.0.6"],
  "discovery_server": "10.0.0.100:11811"
}
```

REQ-21「Fleet broadcast」（v1.2）在這三種拓樸上有不同 cost：

| 拓樸 | Fleet broadcast cost | 備註 |
|---|---|---|
| Flat | 雲端發一條 → 每個 SubNode 收 → 各自發 service 給自家 robot | 最簡單 |
| Per-robot domain | 同上，但每個 SubNode 在自己 domain 裡發 | 同上 cost，多個 domain 不影響 |
| Hub-and-spoke | 同上，但走 discovery server 多一跳 | 多一跳 latency |

---

## 7. 對 PRD 的影響（彙整）

| PRD 條目 | 這章對應的 SDK 工作 |
|---|---|
| §3.2 latency targets | secured mode 要另一份 SLA |
| §5 compliance persona | DDS-Security enable 後不允許 silent fallback；permissions 過期硬擋 |
| §8.4 two-phase config rollback | security 切換是高風險變更，rollback 是必備 |
| §11 IEC 62443 SL2 | sros2 keystore 整合 + reference deployment |
| REQ-08 service allow-list | secured 環境下用 DDS-Security permissions 取代應用層 allow-list（wire-level enforcement）|
| REQ-21 fleet broadcast (v1.2) | 三種拓樸都要適用 |

---

## ✅ 學完這章你應該能回答

1. `ROS_DOMAIN_ID` 跟 DDS-Security 在「擋人進來」的功能上差別是什麼？
2. DDS-Security 三大支柱（Authentication / Access Control / Cryptographic）分別解決什麼問題？
3. 大多客戶不開 DDS-Security 的真實原因是什麼？SubNode 要怎麼面對這個現實？
4. Per-robot domain 跟 Flat fleet（共享 domain + namespace）兩種拓樸，各自最適合哪種客戶？
5. DDS-Security 開啟時，REQ-08 service allow-list 還有存在價值嗎？為什麼？

---

## 課程完成

恭喜，Phase 1 七章全部讀完。把這 7 章 + sidebar 02b 走過一輪後，你應該對 ROS 2 / DDS 有「**能參與設計討論**」的程度 — 看到 PRD 任何一條需求都能反問「這在 wire 上實際是什麼？」。

接下來：
1. 我準備 **Phase 2 EXAM**（[EXAM.md](EXAM.md)）— 20 題左右，混選擇 / 簡答 / 情境題
2. 你作答 → 我批改
3. 通過後 → Phase 3 回頭從 Q5 grilling 接力（鎖剩下的 SDK 設計決策）

要我直接出 EXAM 嗎？還是想先把這章自我檢查試答？
