# 各範例的端對端部署測試報告

每個 SubNode 範例一份報告，記錄它在「建置 → 發佈到 Harbor → 透過 container-management API 部署到真實
裝置」之後實際做了什麼。

共用的方法——關卡、步驟格式、前置條件、負向案例、缺陷登記表與驗收條件——在
**[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images_zh.md)**。這些頁面只記錄各範例自己的
結果，請先讀母報告。

| | |
|---|---|
| **受測裝置** | EPC-R7300A1 `48b02dea8160` — ARM64, Ubuntu 20.04 |
| **環境** | SIT — `weda-sit-k3s.weda.dev`，租戶 `central` |
| **本輪** | **2026-09-10** — 全部重新建置、重新推送、重新部署，並逐一擷取裝置上的 log |
| **上一輪** | 2026-09-09 |
| **報告 schema** | v1.3 — 見 [報告 schema](#報告-schema) |

## 結果

關卡：**B**uild · **P**ush · **S**tandalone start（E2E-EXD-2.3）· **D**eploy，之後是五項功能檢查。
4.4 是框架心跳；4.5 是這個範例存在的目的。`—` = 不適用，`⏭️` = 未執行。

| # | 範例 | 結論 | B | P | S | D | 4.1 | 4.2 | 4.3 | 4.4 | 4.5 | 外部相依 |
|---|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| 01 | [`feature-transform-pipeline`](01-feature-transform-pipeline_zh.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 無——內建模擬器 |
| 02 | [`feature-custom-commands`](02-feature-custom-commands.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 無——內建模擬器 |
| 03 | [`http-air-quality`](03-http-air-quality.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 對外網路（環境部 API） |
| 04 | [`http-stock-quotes`](04-http-stock-quotes.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 對外網路（證交所） |
| 05 | [`feature-aggregation`](05-feature-aggregation.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 無——內建兩個模擬器 |
| 06 | [`opcua-basic`](06-opcua-basic.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 無——內建模擬器 |
| 07 | [`modbus-wise4012`](07-modbus-wise4012.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | 真實 WISE-4012 — **拒絕連線** |
| 08 | [`modbus-wise4012-builder`](08-modbus-wise4012-builder.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | 真實 WISE-4012 — **拒絕連線** |
| 09 | [`mqtt-image-chunked`](09-mqtt-image-chunked.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker（隨附） |
| 10 | [`mqtt-isensing-wise4012`](10-mqtt-isensing-wise4012.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker（隨附） |
| 11 | [`vision-object-detection`](11-vision-object-detection.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker（隨附） |
| 12 | [`daq-proxy`](12-daq-proxy.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 與 `daq-collector` 成對 |
| 13 | [`cfx-endpoint`](13-cfx-endpoint.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | CFX bridge（隨附 RabbitMQ） |
| 14 | [`daq-collector`](14-daq-collector.md) | ❌ BLOCKED | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | **真實 DAQ 模組 — 不存在** |
| 15 | [`system-agent`](15-system-agent.md) | ⏭️ SKIP | ✅ | ✅ | — | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | **需要獨立的一台裝置** |

**6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP = 15** —— 而 SKIP 不算通過。

> **數字更正**：本頁前一版寫著 *13 PASS · 1 PARTIAL · 1 BLOCKED · 1 SKIP*，對一套 15 個範例的測試
> 加起來是 16；而它下面的表格顯示 12 個 PASS。以結論欄為準，上面的統計是從結論欄數出來的。

## 4.4／4.5 拆開之後改變了什麼

把框架心跳與範例自己的輸出分開計分之後，五個範例離開了 PASS。它們都到達平台並完成註冊，但沒有一個
被看到真的在做事。

| 範例 | 原本 | 現在 | 4.4 原本替代掉的是什麼 |
|---|---|---|---|
| `mqtt-image-chunked` | ✅ PASS | ⚠️ PARTIAL | 訂閱了影像 topic，但從來沒有影格進來 |
| `mqtt-isensing-wise4012` | ✅ PASS | ⚠️ PARTIAL | 訂閱了 iSensing topic，但沒有人往上面發資料 |
| `vision-object-detection` | ✅ PASS | ⚠️ PARTIAL | 訂閱了視覺 topic，但沒有 CV 容器在跑 |
| `daq-proxy` | ✅ PASS | ⚠️ PARTIAL | 只有心跳；它的上游是被 blocked 的狀態 |
| `cfx-endpoint` | ✅ PASS | ⚠️ PARTIAL | 訂閱了 17 個感測器；17 是訂閱數，不是收到數 |

`modbus-wise4012`、`modbus-wise4012-builder` 與 `daq-collector` 的 4.4 標記也一併拿掉，但理由不同：
那幾份報告保留的摘錄裡沒有心跳行，而摘錄缺席不等於心跳缺席。它們的 §5 說明了這一點，並寫出要怎麼
才能定論。

## 相對於 2026-09-09 改變了什麼

| 範例 | 原本 | 現在 | 為什麼 |
|---|---|---|---|
| `modbus-wise4012` | ✅ PASS | ⚠️ PARTIAL | **是環境不是程式。** WISE-4012 現在在 `:502` 回 `Connection refused`。SubNode 這一側仍然證明了 4.1–4.3 |
| `daq-collector` | ❌ BLOCKED，4.2/4.3「未到達」 | ❌ BLOCKED，4.2/4.3 ✅ | **舊報告寫錯了。** 它有連上 NATS 並完成註冊，**之後**才在硬體探測失敗 |
| `daq-collector`、`system-agent` | Build ✅ | Build ✅ *（修過之後）* | **D-17** 讓兩者的 build 直接失敗——見下 |
| 所有範例 | 4.2 靠工具的斷言 | 4.2 靠逐字引用的 log | v1.1 schema 暴露出來的證據缺口補上了 |

### D-17 — 對安全性套件做精準版本釘選會爛掉

`daq-collector` 與 `system-agent` 都為了一個 CVE 修正釘住 `gpgv=2.4.4-2ubuntu17.4`。Ubuntu 後來出了
`2.4.4-2ubuntu17.6` 並**把 17.4 從套件庫移除**，於是兩者的 build 都失敗：

```
E: Version '2.4.4-2ubuntu17.4' for 'gpgv' was not found
```

repo 一個字都沒改——變的是套件庫。對會持續安全更新的套件用 `=` 精準釘選是會自我毀滅的寫法。唯一的
慈悲是它**失敗在明處**：build 直接死掉，而不是默默裝上更舊的版本。修法是取套件庫的候選版本，再斷言
下限，因為 `apt-get install` 沒有 `>=` 這種運算子：

```dockerfile
apt-get install -y --no-install-recommends gpgv && \
dpkg --compare-versions "$(dpkg-query -W -f='${Version}' gpgv)" ge 2.4.4-2ubuntu17.4
```

這比原本的釘選嚴格：永遠裝上當下的安全性版本，而且萬一那個版本低於 CVE 下限，build 會失敗。

### 觀察 — 心跳警告把後果講得太重

14 個已部署的範例中有 11 個會印出：

> `SubNode heartbeat is DISABLED: no enabled sensor named 'hb' was found. This SubNode publishes no
> liveness signal and the platform will read it as Disconnected.`

組織的裝置清單**並不支持**這個說法：`opcua-basic` 屬於「被停用」那一組，執行中卻顯示 `Connected`；
而**有** `hb` 感測器的 `modbus-wise4012`，在被拆掉之後顯示 `Disconnected`。連線狀態追蹤的是 SubNode
當下有沒有在跑，不是 `hb` 存不存在。這一條列為警告字句的後續修正，**不**當成缺陷開單。

## 結論怎麼讀

| 結論 | 意義 | 對應到檢查 |
|---|---|---|
| **PASS** | 容器執行中且零重啟、NATS 連上、註冊被接受，而且看得到範例在做自己的工作 | B·P·S·D 與 4.1–4.5 全部 ✅ |
| **PARTIAL** | SubNode 有連上雲端，但它的現場裝置、發布端或上游**在測試當下**不可用 | 4.1–4.4 ✅，4.5 ❌ |
| **BLOCKED** | 部署、執行、註冊都沒問題；但缺少這台裝置沒有的硬體就無法運作 | 4.5 ❌ — 形狀與 PARTIAL 相同，差別在缺口不是暫時的 |
| **SKIP** | 未執行，並記錄原因。**永遠不算通過** | 功能檢查 ⏭️ |

PARTIAL 與 BLOCKED 的檢查形狀一樣，差別在預後：PARTIAL 只要有人把裝置電源打開就會解除，BLOCKED 需要
不同的硬體。

容器回報 `running` 只證明行程沒有結束。這套測試找到的缺陷裡有三個產生了「`running` 但什麼都沒做」的
容器——這就是為什麼每一份報告裡 **4.1 是與 4.2–4.5 分開的檢查**，而不是合併成一列「Function」。

## 報告 schema

15 份頁面用相同順序的相同章節，任兩份可以直接 diff，讀的人不必重讀整頁就知道該看哪裡：

| § | 章節 | 內容 |
|---|---|---|
| — | 結論橫幅 | 結論 · 測試日期 · 裝置 · 環境 |
| 1 | **受測對象** | 範例、compose 服務名、image、傳輸協定、外部相依、使用的主機埠、**部署時必須覆蓋的值**、SubNode 名稱、註冊到的 deviceId |
| 2 | **關卡結果** | 九項檢查各一列：關卡 · 步驟編號 · 檢查 · 結果 · **該項檢查自己的證據**。4.4 是框架心跳，4.5 是範例自己的工作 |
| 3 | **證據 log** | 逐字引用的裝置 log，每一行標註它證明哪一項檢查 |
| 4 | **缺陷** | 編號 · 嚴重度 · 狀態 · 這個缺陷在*這個範例*上造成什麼，之後接一段**分析** |
| 5 | **本輪未驗證的項目** | 每一項未被證明的檢查、原因，以及要怎麼才能解除 |
| 6 | **如何重跑這個測試** | 建置 → deploy-verify → 到裝置上確認 |
| 7 | **變更紀錄** | 本輪、上一輪、相關 PR、驗證工具、schema 版本 |

這個 schema 強制的規則：

1. **不准把功能關卡壓成一格。** 4.1 到 4.5 各自一列、各自有證據。單一「Function ✅」會藏住這套測試
   存在的意義所要抓的失效模式；而心跳又藏了它第二次：五個範例在從未收到任何資料的情況下通過了 4.4。
2. **證據必須對應到某一項檢查。** 一行什麼都不證明的 log 不該出現在 §3。
3. **§5 永遠存在。**「無——九項檢查全部執行過」是一個陳述；章節缺席不是。
4. **嚴重度與狀態跟著缺陷走**，讀的人不必打開母報告就知道 D-14 的 agent 那一半還開著。
5. **統計從結論欄數出來**，不另外宣稱一個數字。

## CI 狀態

上一輪期間 `Weda.SubNode.Integration.Tests` 一直有 2/10 失敗。那是 **D-16**——兩支測試寫死
`nats://localhost:<port>`，而 Testcontainers 是把 port 發佈在 Docker host 上，加上 CI agent 自己就是
共用 socket 的容器，所以 `localhost` 什麼都連不到。已在
[PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363)
另外修掉。**它與這些頁面上的任何範例結果無關。**

範例的修正在
[PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361)；
報告 schema、本輪證據與 D-17 的修正在
[PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396)。
