---
doc-id:    E2E-EXAMPLE-IMAGES-001
title:     "SubNode 範例 — 建置、發佈、部署與驗證 — 端對端測試情境"
product:   "SubNode SDK（範例）"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: docs/wiki/en/02-getting-started/09-build-push-deploy-images_zh.md（SOP）, examples/README.md, templates/subnode/Dockerfile
tags:      [doc:test, feature:sub-node, service:sub-node, service:container-management, layer:edge, layer:cloud, tech:docker, tech:rest, tech:nats, tech:mqtt, ver:1.2, status:review]
---

# SubNode 範例 — 建置、發佈、部署與驗證 — 端對端測試情境

> **給誰看**：SDK 維護者與 QA，用來確認每一個出貨的範例不只編譯得過，而且**能部署到真實邊緣硬體上並
> 且真的在運作**；以及需要示範「把任一個範例從原始碼變成裝置上跑著的 SubNode，全程不碰裝置」的 AE。
> **涵蓋什麼**：每個範例一條連續的路——*建 image → 推到 Harbor → 透過 container-management API 部署到
> 裝置 → 確認裡面的 SubNode 真的完成註冊並回報*。
> **不涵蓋什麼**：單元測試。範例的單元測試套件是獨立的（§11），而且全部通過也證明不了可部署性——
> §10 的每一個缺陷都是部署出來的，不是編譯出來的。

[[_TOC_]]

> **變更紀錄**：範例的修正：[PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361)。
> CI 整合測試修正（D-16）：[PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363)。
> 報告 schema、2026-09-10 重跑的證據與 D-17 的修正：[PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396)。

<!-- 步驟編號 E2E-EXD-<幕>.<步驟> 一旦發佈就固定不動——缺陷標題會引用它。
     這裡的「可部署」指的是 container-management API 能把 image 放到一台沒有 repo checkout 的裝置上。
     這個門檻比「在我筆電上 docker compose up 得起來」嚴格，而且正是這個門檻翻出了 §10 的每一個缺陷。 -->

---

## 0. 一覽

| | |
|---|---|
| **受測對象** | `examples/` 底下的 15 個範例，加上 `tools/simulator-host` |
| **功能** | 範例 image：以 repo 根目錄為 build context、設定烘進 image 以便獨立啟動、多架構發佈、container-management 部署 |
| **受測角色** | **Sam**，一位從沒建過這個 repo 的 SI 工程師，要從 Harbor 把範例部署到客戶的邊緣設備 |
| **次要角色** | **Maya**，SDK 維護者——負責簽核「沒有任何範例帶著只在某一台開發機上成立的預設值」 |
| **範圍** | 每個範例四道關卡：**Build → Push → Deploy → Function**。四道全過才算通過 |
| **裝置** | EPC-R7300A1 — ARM64、6 核心、Ubuntu 20.04、`deviceId 48b02dea8160` |
| **耗時** | 每個範例約 4 分鐘（多架構建置 + 推送 + 部署 + 穩定）；2026-09-10 那次全套約 1 小時 50 分，主要花在第一次拉 image |
| **結論** | §12 的**每一條**驗收條件都打勾才算 PASS |
| **最近一輪** | **2026-09-10** — 6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP（§7） |

## 示範路徑

要對客戶示範時，照順序跑這幾步。每一步都有一句話可以講，矩陣可以略過。

| # | 步驟 | 要講的那句話 |
|---|---|---|
| 1 | `./scripts/build-push-image.sh feature-transform-pipeline` | 「一道指令就把任何範例變成 Harbor 上的多架構 image。」 |
| 2 | `POST …/stack-configs` 然後 `…:deploy` | 「完全不碰裝置——是平台把 stack 推給它的。」 |
| 3 | `GET …/devices/{id}/docker/stacks` | 「容器正在裝置上跑。」 |
| 4 | 裝置 log：`temperature_sensor: 25 → 77` | 「而且裡面的 SubNode 正在讀感測器並轉換數值。」 |
| 5 | `GET …/orgs/{org}/devices` 顯示子 SubNode 為 `Connected` | 「它已經在雲端把自己註冊成一台獨立的裝置。」 |

---

## 1. 測試環境與前置條件

### 1.1 環境

| 項目 | 值 | 備註 |
|---|---|---|
| 環境 | **SIT** | `weda-sit-k3s.weda.dev`，租戶 `central` |
| Base URL | `https://weda-sit-k3s.weda.dev/central/weda` | 自簽憑證——請用 `--cacert` **釘住**，不要用 `-k` |
| API 前綴 | `/api/v1` | |
| 認證 | `Authorization: Bearer <token>` | 用 `weda-login` skill 取得；token 有效 2 小時 |
| Registry | `harbor.arfa.wise-paas.com/edge-coa/<範例>` | 組織需要有 registry 憑證（§1.3） |
| 裝置架構 | **ARM64** | 要去讀，不要假設——見 E2E-EXD-1.1 |
| 建置主機 | amd64，裝有 buildx 與 binfmt | 交叉編譯；見 §1.5 |

### 1.2 需要的帳號

| # | 帳號 | 組織 | 角色 | 用來證明 |
|---|---|---|---|---|
| A1 | `…@advantech.com.tw` | Org-X（擁有該裝置） | **Admin** | 正常路徑——建立 stack config、部署、讀裝置狀態 |

> 只需要一個身分：這份情境測的是可部署性，不是權限控管。權限矩陣屬於 container-management 的 E2E
> 文件，不在這裡。

### 1.3 需要的前置條件

| # | 前置條件 | 需要的狀態 | 用在 |
|---|---|---|---|
| **D1** | 已在 Org-X 註冊的 WEDA Node，`dmagent` 執行中且 NATS 在 `127.0.0.1:4224` | **上線** | 所有幕 |
| **R1** | Org-X 上的 Harbor registry 憑證（`harbor-arfa-edge-coa`） | 存在 | 第 2 幕起——沒有它每次拉 image 都會失敗，表現成部署卡住 |
| **S1** | D1 上可用的主機埠：**2395**（SubNode Web API），以及各範例自己的埠 | — | 第 3 幕 |
| **N1** | D1 有對外網路 | — | 只有 `http-air-quality`、`http-stock-quotes` 需要 |

> **前提：一次只能有一個 SubNode。** 每個 SubNode 都會在 **2395** 起一個 Web API，而 stack 用的是
> `network_mode: host`，所以一台裝置上兩個範例不能同時跑。請依序部署——平行跑時出現「port in use」
> 是測試設計錯誤，不是缺陷。

### 1.5 Shell 前置設定（可直接複製）

```bash
SP=/path/to/scratch
TOK=$(cat "$SP/weda.jwt")
BASE="https://weda-sit-k3s.weda.dev/central/weda"
ORG=b60fee4c-3e27-43eb-9a6e-877a52bb54f5
DEV=48b02dea8160
CA="--cacert $SP/weda-sit-k3s.pem"

api()  { curl -sS $CA -H "Authorization: Bearer $TOK" "$@"; }
japi() { api -H 'Content-Type: application/json' "$@"; }
```

---

## 2. 測試步驟怎麼讀

| 欄位 | 意義 |
|---|---|
| **Step** | 固定編號——開缺陷時請引用它（例如 `E2E-EXD-3.2 failed`） |
| **Sam does** | 這位工程師想達成什麼，用白話寫（這就是 AE 示範時要講的那句） |
| **Call** | 確切的指令或請求 |
| **Expect** | 代表通過的結果 |
| **Proves** | 這一步證明了什麼 |

觀察到的狀態與 **Expect** 不同就是 FAIL。容器回報 `running` **不足以**成為第 4 幕任何一步的證據——
見 §6 的警告。

> **自動化**：`deploy-verify.sh`（這份情境的驗證工具）統一執行第 3 幕與第 4 幕。沒有執行到的步驟記為
> `SKIP`——**`SKIP` 不是通過。**

---

## 3. 第 1 幕 — 這個 image 在那台裝置上跑得起來嗎？*（探查）*

> **情境**：Sam 面前有一台裝置，和一個他從沒建過的範例。在做任何事之前，他得先知道要建哪個架構。

### E2E-EXD-1.1 — 讀出裝置架構

| | |
|---|---|
| **Sam does** | 去問平台這台裝置是什麼，而不是假設它是 x86 |
| **Call** | `api "$BASE/api/v1/devices/$DEV/capabilities" \| grep processorArchitecture` |
| **Expect** | `"processorArchitecture": "ARM64"` |
| **Proves** | 建置目標是從證據選出來的。把 amd64 image 放到 Jetson 這類裝置上，會在拉取時失敗並吐出一個看起來像 registry 故障的 manifest 錯誤 |

### E2E-EXD-1.2 — 確認組織拉得到 Harbor

| | |
|---|---|
| **Sam does** | 在部署任何東西之前先檢查 registry 憑證存不存在 |
| **Call** | `api "$BASE/api/v1/orgs/$ORG/containers/registries"` |
| **Expect** | 有一筆 `registryUrl` 為 `https://harbor.arfa.wise-paas.com` |
| **Proves** | 前置條件 R1。沒有它，部署會永遠停在 `deploying`，而且原因不會被揭露出來 |

---

## 4. 第 2 幕 — 建置與發佈 *（供給側）*

### E2E-EXD-2.1 — 從 repo 根目錄建置

| | |
|---|---|
| **Sam does** | 在不知道 context 規則的情況下建置範例 |
| **Call** | `cd examples/<範例> && docker build -t x .` |
| **Expect** | **失敗**，錯誤為 `MSBUILD : error MSB1003: Specify a project or solution file.` |
| **Proves** | 負向案例有被記錄下來。每個範例的 `.csproj` 都引用 `../../src/Weda.SubNode.*`，所以 context 必須是 repo 根目錄。而這個錯誤訊息既沒提 context 也沒提 Dockerfile |

### E2E-EXD-2.2 — 用腳本建置並推送

| | |
|---|---|
| **Sam does** | 使用官方支援的進入點 |
| **Call** | `PLATFORMS=linux/arm64 ./scripts/build-push-image.sh <範例>` |
| **Expect** | 推送成功；`docker buildx imagetools inspect` 列出 `linux/arm64` |
| **Proves** | Harbor 上存在符合該裝置架構的 image |

### E2E-EXD-2.3 — image 不掛任何檔案也能跑

| | |
|---|---|
| **Sam does** | 完全不掛任何 bind mount 就執行 image，模擬部署時的情況 |
| **Call** | `docker run --rm <image>` |
| **Expect** | 啟動並讀取**烘進去的** `devicecfg.json`——而不是「找不到設定檔」 |
| **Proves** | 這個 image 是可部署的。container-management 部署出去的 stack 沒有 repo checkout，所以依賴掛載的 image 會啟動後立刻死掉 |

---

## 5. 第 3 幕 — 透過平台部署 *（不碰裝置）*

> 底下每一步用的 compose 內容都是該範例的 `docker-compose.deploy.yml`，要找的容器就是裡面的
> `container_name`。這兩者，加上「這個範例該被看到做什麼」，都記錄在
> `examples/<範例>/e2e-expect.json`。

### E2E-EXD-3.1 — 建立 stack config

| | |
|---|---|
| **Sam does** | 把 compose stack 註冊到組織底下 |
| **Call** | `japi -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" -d '{"stackName":…,"type":"compose","composeFileContent":"<base64>"}'` |
| **Expect** | `201`，回傳 `stackConfigId` 與 `stackRevisionId` |
| **Proves** | `composeFileContent` 是 **base64**，不是純 YAML——送純文字會被接受，然後在裝置上以看不懂的方式失敗 |

### E2E-EXD-3.2 — 部署該版本

| | |
|---|---|
| **Sam does** | 把版本推到裝置 |
| **Call** | `japi -X POST "$BASE/api/v1/orgs/$ORG/stack-configs/$STACK/revisions/$REV:deploy" -d '{"deviceIds":["'$DEV'"]}'` |
| **Expect** | `200`，`deviceActions: {<dev>: "add"}` |
| **Proves** | 裝置接受了該版本。**這裡回 `"skip"` 就是失敗**——代表雲端認為該版本已經存在，那是雲端與裝置失步之後的現象（§10 D-09） |

### E2E-EXD-3.3 — 讓 SubNode 的註冊資料持久化

| | |
|---|---|
| **Sam does** | 在 stack 裡為 `/app/.weda` 宣告一個 volume |
| **Call** | `- ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service>}:/app/.weda` |
| **Expect** | `docker restart` 之後 log 出現 `Found existing SubNode registration`——**不是**重新註冊 |
| **Proves** | 沒有它，每次重建容器都會摧毀已被接受的註冊，SubNode 接著會撞上自己先前的身分：`409 Device name already in use`。**這個 stack 只能成功一次** |

---

## 6. 第 4 幕 — SubNode 真的在運作嗎？*（結果）*

> **容器回報 `running` 只證明行程沒有結束。** 一個連不到自己裝置或連不到 WedaNode 的 SubNode 會一直
> 活著並無限重試，而 `restart: unless-stopped` 會把崩潰變成崩潰迴圈，API 照樣回報 `running`。底下
> 每一步存在的理由，都是因為「running」不是證據。

### E2E-EXD-4.1 — 容器啟動且沒有迴圈

| | |
|---|---|
| **Expect** | `status=running` **且** `restarts=0` |
| **Proves** | 不是崩潰迴圈 |

### E2E-EXD-4.2 — SubNode 連上了 WedaNode

| | |
|---|---|
| **Expect** | `NATS connection verified` 以及 `NATS service discovered: … WedaNodeId=<deviceId>` |
| **Proves** | 憑證與 WedaNode 位址對**這台裝置**是正確的，而不是對某台開發機正確 |

### E2E-EXD-4.3 — 它註冊成自己的裝置

| | |
|---|---|
| **Expect** | `Configuration uploaded successfully` 或 `Found existing SubNode registration`，且組織的裝置清單出現一台子裝置，其 `parentDeviceId = <deviceId>`、`connectivity.state = Connected` |
| **Proves** | SubNode 會以 `SubNode.Name` 註冊成**自己的** WEDA 裝置，而不是變成主裝置底下的感測器 |

### E2E-EXD-4.4 — 心跳送達雲端

| | |
|---|---|
| **Expect** | `Reporting health: … IsHealthy=True` |
| **Proves** | 這個 SubNode 對雲端而言是活著的。SubNode 不管讀不讀得到自己的裝置都會發這個，所以它證明不了範例自己的工作 |

### E2E-EXD-4.5 — 它在做自己存在的目的

| | |
|---|---|
| **Expect** | `examples/<範例>/e2e-expect.json` 裡記錄的那一行——轉換後的數值、指令執行結果、一筆報價、一個衍生感測值 |
| **Proves** | 範例真正的工作有在進行：這就是「已部署」與「在運作」的差別 |

該檔案裡的 `business.mode` 說明這一項要怎麼判定：

| 模式 | 意義 | 對結論的影響 |
|---|---|---|
| `auto` | 判準取自實際成功過的 log；全部命中才算 | 由驗證工具判定 |
| `external` | 還沒有成功樣本——缺硬體或缺發布端 | `NOT-SCORED`，上限為 PARTIAL |
| `manual` | 需要人判讀數值 | 判讀前 `NOT-SCORED`，上限為 PARTIAL |
| `skip` | 不在這台裝置上驗證 | `NOT-SCORED` |

**`NOT-SCORED` 不是通過。** 一個到達平台卻從未被看到在運作的範例是 PARTIAL，而它的報告要寫出要怎麼
才能補上這個缺口。

---

## 7. 第 5 幕 — 各範例結果

**2026-09-10 那輪** — 每個 image 都重新建置並重新推送、每個 stack 都重新部署，並逐一擷取裝置本身的
容器 log。上一輪：2026-09-09。

關卡：**B**uild · **P**ush · **S**tandalone start（E2E-EXD-2.3）· **D**eploy，之後是第 4 幕的五項
功能檢查各自計分。`—` = 不適用 · `⏭️` = 未執行。

> **為什麼 4.1–4.5 是五欄而不是一欄。** D-01、D-03、D-06 每一個都產生了「平台回報 `running`、但
> SubNode 什麼都沒連上」的容器。這一輪又兩次證明了拆開是對的：`modbus-wise4012` 與 `daq-collector`
> 都是 ✅✅✅❌——可證明地健康到現場裝置為止，也可證明地卡在那裡。單一個「Function」格子只能寫
> 「失敗」。

每一列都連到 [`examples/`](examples/README_zh.md) 底下自己的報告，那裡引用了每一格背後的 log 行、它暴露
的缺陷、**沒有**被驗證的項目，以及怎麼重跑。

| # | 範例 | B | P | S | D | 4.1 | 4.2 | 4.3 | 4.4 | 4.5 | 主要證據／阻礙 |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| 1 | [`feature-transform-pipeline`](examples/01-feature-transform-pipeline_zh.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `temperature_sensor: 25` → `[0] 25 -> 77`（°C→°F）→ 平滑；deviceId 與 09-09 相同 |
| 2 | [`feature-custom-commands`](examples/02-feature-custom-commands.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `sensor.read`、`tag.set`、`props.get` 在裝置上全部成功 |
| 3 | [`http-air-quality`](examples/03-http-air-quality.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `Air quality data received`；子裝置 `355900119696015360` |
| 4 | [`http-stock-quotes`](examples/04-http-stock-quotes.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 即時證交所行情：台積電 2445／最高 2450／成交量 2203——與 09-09 不同，即時資料本來就該變 |
| 5 | [`feature-aggregation`](examples/05-feature-aggregation.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 電流 5.00 A + 電壓 230.00 V，衍生的功率跟著兩者變動 |
| 6 | [`opcua-basic`](examples/06-opcua-basic.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | **批次中失敗、單獨重跑通過**——第一次拉 image 超過 7.5 分鐘的等待窗，不是 D-09 |
| 7 | [`modbus-wise4012`](examples/07-modbus-wise4012.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | **PASS → PARTIAL。** SubNode 健康；`172.16.8.122:502` 回 `SocketException (111): Connection refused` |
| 8 | [`modbus-wise4012-builder`](examples/08-modbus-wise4012-builder.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | 再次 PARTIAL，同一台連不上的 WISE-4012；18 次連線失敗 |
| 9 | [`mqtt-image-chunked`](examples/09-mqtt-image-chunked.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 訂閱了 `sensor/image/mnist`；隨附的 broker 沒有發布端，所以沒有任何影格進來 |
| 10 | [`mqtt-isensing-wise4012`](examples/10-mqtt-isensing-wise4012.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 訂閱了 `Advantech/00D0C9FAC80E/{data,status}`，子裝置 `355941923954884608`；沒有人往那些 topic 發資料 |
| 11 | [`vision-object-detection`](examples/11-vision-object-detection.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 訂閱了視覺 topic，子裝置 `355914456695308288`；沒有 CV 容器在發布 |
| 12 | [`daq-proxy`](examples/12-daq-proxy.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 只有心跳——`daq-collector` 是 blocked，所以沒有上游可以代理 |
| 13 | [`cfx-endpoint`](examples/13-cfx-endpoint.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | **第一次就乾淨通過**（09-09 那次試了四次）；訂閱 `+/+/+/CFX/#` 涵蓋 17 個感測器／17 種訊息類型——那是訂閱，不是收到 |
| 14 | [`daq-collector`](examples/14-daq-collector.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | **4.2/4.3 更正為 ✅**——它有完成註冊，**之後**才在 `No DAQ modules available` 失敗 |
| 15 | [`system-agent`](examples/15-system-agent.md) | ✅ | ✅ | — | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | **SKIP**——它已經以產品元件的身分跑在這台裝置上；需要一台專屬裝置才能驗 |

**6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP = 15**。

> **數字更正**：先前版本寫著 *13 PASS · 1 PARTIAL · 1 BLOCKED · 1 SKIP*，對 15 個範例加起來是 16。
> 現在的統計是從結論欄數出來的。

> **`system-agent` 是刻意 SKIP，而 SKIP 不是通過。** 把它部署下去被判斷為「對一台正常運作的裝置造成
> 風險」而不是一次測試。要驗它需要一台專屬裝置。

> **4.2 的證據缺口補上了。** 每個已部署的範例現在都有自己的 `NATS connection verified` 行**以及**
> `NATS service discovered: … WedaNodeId=48b02dea8160`。真正重要的是第二行：它指名了 WedaNode，所以
> 證明 SubNode 連到的是**這台**裝置，而不是某台開發機——那正是 D-01 當初破壞掉的東西。

---

## 8. 第 6 幕 — 必須持續失敗的負向案例

| 步驟 | 案例 | 預期 |
|---|---|---|
| E2E-EXD-6.1 | 在範例目錄裡 `docker build .` | 失敗，`MSB1003` |
| E2E-EXD-6.2 | 部署一個沒有 `/app/.weda` volume 的 stack，然後重建容器 | 第二次啟動失敗於 `409 Device name already in use` |
| E2E-EXD-6.3 | 同時部署兩個 SubNode stack | 第二個無法綁定 2395 埠 |
| E2E-EXD-6.4 | 把只有 amd64 的 image 部署到 ARM64 裝置 | 拉取時 manifest 不符而失敗 |

---

## 9. 錯誤速查表

| 症狀 | 真正原因 | 出處 |
|---|---|---|
| `MSBUILD : error MSB1003` | build context 是範例目錄，不是 repo 根目錄 | E2E-EXD-2.1 |
| `409 Device name already in use` | `/app/.weda` 沒有持久化，或該名稱在組織裡已被佔用 | E2E-EXD-3.3 |
| `SocketException (98): Address already in use` | 兩個東西搶同一個埠——常常是範例本身已內建的模擬器 | §8 |
| `BadInternalError 'Unexpected error starting application'` | invariant globalization；OPC UA 的 X.509 路徑需要 ICU | D-05 |
| `MissingMethodException: Constructor … not found` | 裝置類別缺少 `(context, DeviceConfiguration)` 這個多載 | D-07 |
| NATS 重試迴圈、容器 `running` | WedaNode 位址或憑證對**這台裝置**是錯的 | D-01、D-03、D-04 |
| 部署狀態 `deployed`，裝置上卻沒有容器 | 雲端與裝置失步 | D-09 |
| 容器卡在 `Created`，**完全沒有 log** | stack 裡有 host 模式的服務，同時又有 bridge 網路的服務（D-14）；或同名 stack 留下的殘存 bridge 路由（D-13） | D-13、D-14 |
| `no space left on device`、`ResourceExhausted`、NuGet 損毀 | 批次多架構建置期間 Docker 資料磁碟區被塞滿 | D-10 |
| CI 出現 `can not connect uris: nats://localhost:<port>`，本機卻正常 | 測試寫死 `localhost`；Testcontainers 是把 port 發佈在 Docker host 上，而在容器化的 agent 上那是不同的命名空間 | D-16 |

---

## 10. 找到的缺陷

| # | 缺陷 | 嚴重度 | 狀態 |
|---|---|---|---|
| D-01 | 四個範例烘進了**開發機的 WedaNode 位址**（`172.22.160.192/197:4224`），其中一個還用了 `4222` 埠。本機看不出來；設定一旦烘進 image 就致命 | High | **已修** — 全部預設為 `127.0.0.1:4224` |
| D-02 | image 以 bind mount 取得設定，所以部署出去的 stack 沒有設定檔，啟動即死 | High | **已修** — 設定烘進 `/app` |
| D-03 | `vision-object-detection` 出貨時帶著字面值 `REPLACE_WITH_NATS_USER` | High | **已修** |
| D-04 | `cfx-endpoint` 依設計出貨**空的** NATS 密碼（由 `.env` 提供）；沒提供就永遠無法認證 | Medium | **依設計** — 已記錄；部署時必須傳 `SystemConfig__WedaNode__Password` |
| D-05 | `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 會弄壞 OPC UA 的 X.509 應用程式憑證路徑 | High | **已修** — 以排除法證實，不是靠看程式碼推測 |
| D-06 | 三個範例寫死 `.UseMockCloud()`，所以永遠連不到 WedaNode | High | **已修** — 改為設定驅動，預設走真雲 |
| D-07 | `MyFirstISensingDevice` 缺少 host 實際會呼叫的 `(context, DeviceConfiguration)` 建構子 → 在任何主機上啟動都會 `MissingMethodException` | High | **已修** |
| D-08 | `examples/daq-collector/docker-compose.yml` 驗證失敗：`services.daq-collector.devices must be a array`。`devices:` 鍵存在但底下每一項都被註解掉，等於空值 | Medium | **已修** — 鍵與項目一起註解掉，並在說明裡寫明兩者要一起取消註解 |
| D-09 | **在既有 stack config 裡改服務名稱會摧毀該部署。** 新容器會起來，接著 agent 把它移除並停在 `status: deployed`／`reason: "Stack is managed but not currently running"`，容器清單是空的，而裝置上根本沒有這個 compose 專案。`commands:start` 接著回 `stack not found`，重新部署同一版本則被回 `skip`。**可穩定重現**——見 §10.1 | High | **未解** — 平台缺陷。繞法：`DELETE …/docker/stacks` 之後重新部署（會回 `add`） |
| D-10 | 批次多架構建置會耗盡 Docker 資料磁碟區；錯誤訊息看起來像網路故障 | Low | **已記錄** — 批次之間先 prune |
| D-11 | self-contained 發佈疊在 `dotnet/runtime` 上，多帶了**第二份**沒用到的 runtime（每個 image 約 82 MB） | Low | **已修** — 改用 `runtime-deps` |
| D-12 | build context 252.9 MB，其中 177 MB 是 `.dockerignore` 沒涵蓋到的、被 gitignore 的 log | Low | **已修** — context 現在是 22.4 MB |
| D-16 | 兩支整合測試寫死 `nats://localhost:<port>`，而 Testcontainers 是發佈在 **Docker host** 上。CI agent 自己就是共用 socket 的容器，所以 `localhost` 什麼都連不到——在每條分支上紅了一個禮拜，但在開發機上都過 | High | **已修** — 改以 `IContainer.Hostname` 解析主機（[PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363)） |
| D-14 | stack 裡同時有 `network_mode: host` 與 **bridge 網路的兄弟服務**時無法部署：agent 會把專案網路的 veth 寫進 host 命名空間，host 模式的容器被建立但永遠不啟動（`cannot program address …/28 … conflicts with existing route`），而且**完全不吐 log**，看起來像拉 image 卡住 | High | **範例側已修** — 兩個服務都改用 host networking。底層的 agent 行為**未解** |
| D-15 | 從 `command:` 追加內容到 `/etc/rabbitmq/rabbitmq.conf` 會讓 RabbitMQ 以 `failed_to_prepare_configuration` 中止——那個檔案是 entrypoint 依 `RABBITMQ_DEFAULT_*` 產生的 | Low | **已修** — 只啟用外掛；MQTT 留在 1883，SubNode 指向它 |
| D-13 | 同名 stack 反覆部署／刪除會留下該專案網路網段的**殘存 bridge 路由**。下一次部署的容器會被建立但永遠不啟動：`cannot program address 10.226.2.3/28 … conflicts with existing route`。`docker network prune` 清不掉（該網路仍在使用中） | Medium | **未解** — 平台／agent 缺陷。繞法：`DELETE …/docker/stacks` 把專案網路一併移除，再重新部署 |
| D-17 | **對安全性套件做精準 `apt` 版本釘選會爛掉。** `daq-collector` 與 `system-agent` 釘住 `gpgv=2.4.4-2ubuntu17.4`；Ubuntu 出了 `17.6` 並把 `17.4` 從套件庫移除，於是**兩者的 build 直接失敗**——`E: Version '2.4.4-2ubuntu17.4' for 'gpgv' was not found`。repo 什麼都沒改，變的是套件庫。它失敗在明處，這是唯一的慈悲 | High | **已修** — 取套件庫的候選版本並用 `dpkg --compare-versions` 斷言下限（`apt-get install` 沒有 `>=`）。比原本的釘選更嚴格：永遠是最新，而且一旦低於 CVE 下限 build 就會失敗 |

---

### 10.1 D-09 的重現步驟

2026-09-09 以最小化的 stack 確認過——純 `nginx:alpine`、沒有 host networking、沒有 bridge 兄弟服務，
所以與 D-13、D-14 無關。

1. 部署版本 A，其中一個服務叫 `repro-alpha` → `deviceActions: add`，容器執行中。
2. 對同一個 stack config 下 `PATCH`，**只改服務名稱**為 `repro-beta`。在**不**先刪除裝置端副本的
   情況下部署新版本 → `deviceActions: update`。
3. 觀察裝置上的 stack 約 90 秒：

```
[1] status=running  op=idle/deployed                 containers=[('repro-alpha','running')]
[2] status=running  op=idle/awaitingDeployedStatus   containers=[('repro-beta','running')]
...
[6] status=deployed op=idle/deployed                 containers=[]
    reason: "Stack is managed but not currently running"
```

改名後的容器**確實**有啟動（步驟 2–5），然後被移除且再也沒有回來。

4. 到裝置上看：沒有任何 `repro-*` 容器，`docker compose ls -a` 只列出 `dmagent` 與 `sv-ai-model`
   ——專案不見了。
5. `POST …/docker/stacks/commands:start` → `HTTP 200`，但指令結果是
   `status: 1`、`error: {"code":"internalServerError","message":"Command execution failed: stack not found"}`。

也就是雲端認為 stack 被管理著、裝置上什麼都沒有，而復原指令無法把兩邊接起來。唯一的出路是刪掉裝置端
副本再重新部署。

**為什麼重要**：API 在每一步都回報成功。一個相信 `deployed` 的呼叫端——那正是操作介面會顯示的狀態
——看到的是一個健康的部署，而裝置上什麼都沒在跑。

---

## 11. 單元測試涵蓋（背景資訊，不屬於這份情境）

| 測試專案 | 涵蓋 | 結果 |
|---|---|---|
| `SystemAgentDevice.Tests` | `system-agent` | 140 通過 |
| `CommandHandlerExample.Tests` | `feature-custom-commands` | 37 通過 |
| `StockMonitor.Tests` | `http-stock-quotes` | 15 通過 |
| `VisionObjectDetection.Tests` | `vision-object-detection` | 10 通過 |

**202 個測試、0 失敗——而它們一個 §10 的缺陷都沒抓到。** 15 個範例裡只有 4 個有測試，而上面每一個
缺陷都是部署出來的。這就是這份情境存在的理由。

`Weda.SubNode.Core.Tests`（1112 個）也通過。`Weda.SubNode.Integration.Tests` 在整個工作期間都是
**10 個裡有 2 個失敗**——見 D-16；那個失敗比這條分支早一週，與範例無關。

> 有兩個測試專案的名字對應到已經不存在的目錄（`command-handler`、`stock-monitor`）。它們的
> `ProjectReference` 是對的，只有名字沒跟上改名。

---

## 12. 驗收條件 — 簽核表

| # | 條件 | 狀態 |
|---|---|---|
| X-1 | 15 個範例全部能建置並多架構推送到 Harbor | ✅ 15/15 |
| X-2 | 每個範例 image 不掛任何檔案都能啟動（E2E-EXD-2.3） | ✅ |
| X-3 | 每個可部署的範例都在 D1 上連上 NATS 並完成註冊 | ✅ 14/14 已部署 |
| X-3b | 每個可部署的範例都被看到在做自己的工作（E2E-EXD-4.5） | ❌ 14 個裡只有 6 個；7 個需要硬體或發布端，1 個需要 DAQ 模組 |
| X-4 | 註冊資料撐得過容器重建（E2E-EXD-3.3） | ✅ 以重啟驗證過 |
| X-5 | 沒有任何範例預設指向自己所在裝置以外的主機 | ✅ D-01 已修 |
| X-6 | 沒有任何範例寫死 mock cloud | ✅ D-06 已修 |
| X-7 | §10 裡每一個未解缺陷都有負責人 | ❌ D-09、D-13、D-14（agent 行為）無人認領 |

**整體結論：有條件通過（CONDITIONAL PASS）** — X-3b 與 X-7 未達成。**15 個範例中有 6 個被確認端對端
運作**，14 個被確認可部署並完成註冊。這兩個數字之間的落差，正是「把心跳與範例自己的輸出分開計分」
所揭露的：7 個需要這台裝置沒有的硬體或發布端，1 個需要 DAQ 模組，還有 1 個根本不在這裡跑。三個平台
缺陷未解且無人認領（**D-09** stack 失步、**D-13** 殘存 bridge 路由、**D-14** host 與 bridge 混用）
——這三個的表現都是「裝置什麼都沒做，但 API 回報成功」。
