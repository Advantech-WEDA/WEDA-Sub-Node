# SubNode 範例可部署性與防呆機制 — 開發計畫

| | |
|---|---|
| **分支** | `docs/e2e-example-report-schema` |
| **狀態** | 已完成 — P1~P8 全數實作並驗測；`feature-transform-pipeline` 的交付包已在 EPC-R7300A1 實跑通過 |
| **前置 PR** | #7361（範例可部署性修正）、!7396（報告 schema + 2026-09-10 重跑） |
| **範圍外** | 排程 build（CI 第三層）、把 pipeline 測試改成會擋（CI 第二層）、真機部署進 CI |
| **後續** | 另外 5 個自帶模擬器的範例（`feature-custom-commands`、`http-air-quality`、`http-stock-quotes`、`feature-aggregation`、`opcua-basic`）的 `delivery.md` 已備妥，待 build 並 push 後產交付包 |

---

## 0. 這份計畫要解決什麼

`!7396` 把 15 份範例測試報告統一了 schema 並重跑一輪，但 review 找到四類問題。這份計畫處理其中六件事：

| # | 問題 | 處理階段 |
|---|---|---|
| 1 | 15 份 compose 有 8 份只有 `build:` 沒有 `image:`，是本機編譯用的形狀，裝置上不能編譯；實際部署用的 compose 沒留在 repo，重跑指令只能寫 `<compose-file>` 佔位符 | P2 |
| 2 | `daq-collector` 報告同時寫著「compose 驗證過不了」與「部署成功」，因為測的不是 repo 那份 | P1 + P2 |
| 3 | 大報告 §12 簽核表仍寫「13 confirmed」，與 §7 的 11/2/1/1 不一致 | P7 |
| 4 | 「有在吐資料」這格只看 health report（框架心跳），範例自己的業務功能沒被檢查 | P6 + P7 |
| 5 | `dotnet new` 模板仍帶著已修掉的四個缺陷，新專案會原地重演 | P5 |
| 6 | 平台預設 NATS 密碼隨 image 上 Harbor | P3 |

**NOTE**：CI 第二層（pipeline 測試改成會擋）與第三層（排程 build）不在這條分支處理；`docs/wiki/.../06-remote-control/04-historical-report-command.md` 與 `examples/system-agent/scripts/build-windows-package.sh` 兩個與分支主題不相關的檔案依舊保留。

---

## 1. 命名慣例（P2 起全程沿用）

image 名稱一律等於範例目錄名，與 `scripts/build-push-image.sh` 的預設值一致（`harbor.arfa.wise-paas.com/edge-coa/${NAME}`）。容器名沿用 2026-09-10 那輪報告已經在用的名字。

| # | 範例目錄 | image | 容器名 | 隨附服務 |
|---|---|---|---|---|
| 01 | `feature-transform-pipeline` | `edge-coa/feature-transform-pipeline` | `weda-transform-pipeline` | — |
| 02 | `feature-custom-commands` | `edge-coa/feature-custom-commands` | `weda-command-handler` | — |
| 03 | `http-air-quality` | `edge-coa/http-air-quality` | `weda-air-quality` | — |
| 04 | `http-stock-quotes` | `edge-coa/http-stock-quotes` | `weda-stock` | — |
| 05 | `feature-aggregation` | `edge-coa/feature-aggregation` | `weda-power-agg` | — |
| 06 | `opcua-basic` | `edge-coa/opcua-basic` | `weda-opcua` | — |
| 07 | `modbus-wise4012` | `edge-coa/modbus-wise4012` | `weda-modbus` | — |
| 08 | `modbus-wise4012-builder` | `edge-coa/modbus-wise4012-builder` | `weda-modbusb` | — |
| 09 | `mqtt-image-chunked` | `edge-coa/mqtt-image-chunked` | `weda-image-sensor` | mosquitto |
| 10 | `mqtt-isensing-wise4012` | `edge-coa/mqtt-isensing-wise4012` | `weda-isensing` | mosquitto |
| 11 | `vision-object-detection` | `edge-coa/vision-object-detection` | `weda-vision` | mosquitto |
| 12 | `daq-proxy` | `edge-coa/daq-proxy` | `weda-daqp` | — |
| 13 | `cfx-endpoint` | `edge-coa/cfx-endpoint` | `weda-cfx` | rabbitmq |
| 14 | `daq-collector` | `edge-coa/daq-collector` | `weda-daqc` | — |
| 15 | `system-agent` | `edge-coa/system-agent` | `weda-system-agent` | — |

`mqtt-image-chunked` 目前的 compose 用的是 `edge-coa/image-sensor`，與建置腳本的預設不符，P2 一併對齊。

---

## 2. 階段

### P1 — 修 `daq-collector` compose 的驗證錯誤

**改什麼**

`examples/daq-collector/docker-compose.yml` 的 `devices:` 鍵存在但所有項目都被註解掉，等於空值，compose 驗證直接失敗：

```
services.daq-collector.devices must be a array
```

把 `devices:` 這個鍵本身也一起註解掉，並在說明裡寫明「取消註解時要連鍵一起打開」。

**如何驗測**

```bash
docker compose -f examples/daq-collector/docker-compose.yml config -q   # 無輸出 = 通過
```

---

### P2 — 每個範例補一份部署用 compose

**改什麼**

每個範例目錄新增 `docker-compose.deploy.yml`，與現有的 `docker-compose.yml`（本機開發用，保留不動）並存：

- `image:` 指向第 1 節表格的 Harbor 位址，**不含 `build:`**
- `container_name:` 用第 1 節的名字
- 不掛任何 `./xxx.json`（設定檔已烘進 image），只保留 `${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service>}:/app/.weda`
- 隨附服務（mosquitto / rabbitmq）一併寫進來，**全部 host networking**，不使用 `ports:`
- `mqtt-image-chunked` 的 mosquitto 目前是 bridge + `ports:` + 掛 `./mosquitto/config`，三項在裝置上都不成立，改成與 `vision-object-detection` 一致的寫法（host networking + entrypoint 內產生最小設定）
- `cfx-endpoint` 的 `cfx-replay` 是驗證用途、掛在 profile 下，不列入部署 compose

連帶修正一處本機開發用的 compose：`examples/vision-object-detection/docker-compose.yml` 的
`WEDA_NODE_URL` 預設值是 `127.0.0.1:4222`（錯的 port）、`WEDA_NODE_USERNAME` 預設空字串。環境變數
會蓋掉烘進 image 的 `systemcfg.json`，所以這是 D-01 從環境變數那一層跑回來。改為 `4224` 與
`advantech_nats`。

同時新增 `examples/<name>/e2e-expect.json`，讓驗證腳本只需要範例名就能自己找到其餘資訊：

```json
{
  "compose": "docker-compose.deploy.yml",
  "container": "weda-modbus",
  "image": "harbor.arfa.wise-paas.com/edge-coa/modbus-wise4012:latest",
  "business": {
    "mode": "external",
    "patterns": [],
    "note": "Needs a reachable WISE-4012 at 172.16.8.122:502 ..."
  }
}
```

`patterns` 是陣列且**全部都要命中**才算通過（例如 `feature-custom-commands` 要三個指令都成功）。

`business.mode` 三種值的定義見 P7。

**如何驗測**

```bash
for f in examples/*/docker-compose.deploy.yml; do docker compose -f "$f" config -q || echo "FAIL $f"; done
```

- 15 份都要通過驗證，且每份都要有 `image:`、`container_name:`、`WEDA_DATA_PATH`
- 每份都不得同時出現 `network_mode: host` 與 `ports:`
- 每份都不得出現 `build:` 或 `./*.json` 的 bind mount
- 抽 `feature-transform-pipeline`（自帶模擬器）與 `mqtt-image-chunked`（有隨附 broker）兩個實機部署一次，確認容器名與腳本抓 log 的名字一致

---

### P3 — image 內密碼留空，部署時注入

> **P4 取代了這裡的一部分決定。** 交付用的 image 改成在 build 時把密碼寫進去，部署端不再需要提供。
> 仍然成立的是：repo 內沒有明文密碼，`git grep` 零命中；值只存在於 build 者的環境變數與 image 內。

**改什麼**

- 14 個範例的 `systemcfg.json` 把 `WedaNode.Password` 改為 `""`（`cfx-endpoint` 已經是空的，維持不變）。帳號 `advantech_nats` 與位址 `127.0.0.1:4224` 保留。
- 本機開發用的 `docker-compose.yml`：新增 `SystemConfig__WedaNode__Password: ${WEDA_NATS_PASSWORD:?...}`，並在各範例目錄補 `.env.example`。
- 部署用的 `docker-compose.deploy.yml`：同樣以環境變數注入，值由部署端提供。`system-agent` 例外，它不烘設定、讀裝置上 `/opt/Advantech` 的那份，所以不加這個變數。
- 一併補 `DeviceConfig__SubNode__Name=${SUBNODE_NAME:-<烘進去的名字>-${EDGESYNC_EDGE_ID:-local}}`。agent 部署每個 stack 時會自動注入 `EDGESYNC_EDGE_ID`（裝置 id），所以同一份 stack config 推到多台裝置不會撞名，不必每台改 `environment`。SubNode 名稱在同一個 org 內唯一，同一個範例部署到第二台裝置時第二次註冊會被拒（`409 Device name already in use`）。2026-09-10 那輪報告登記的名字（`ModbusDemo-0909` 等）與烘進 devicecfg 的名字（`Wise4012` 等）對不上，就是因為當時是用環境變數蓋掉的。
- 順手移除 `mqtt-isensing-wise4012` 的 mosquitto 服務上那個 `DeviceConfig__...BrokerUrl` 變數：mosquitto 不讀它，是複製貼上殘留。
- SOP `docs/wiki/en/02-getting-started/09-build-push-deploy-images.md` 增加一小節，說明部署時必須傳 `SystemConfig__WedaNode__Password`，以及沒傳時的症狀（NATS 認證失敗、容器持續重試）。

這組密碼在 repo 裡是同一組，屬於平台預設值而非每台裝置獨立，所以 repo 內其餘出現位置一併清掉：

| 檔案 | 怎麼清 |
|---|---|
| `tools/nats-web-dashboard/appsettings.json` | 密碼留空；順帶把 `172.22.160.197:4224` 改為 `127.0.0.1:4224`（與 D-01 同一類） |
| `examples/mqtt-image-chunked/Receiver/appsettings.json` | 密碼留空，靠環境變數覆蓋 |
| `examples/mqtt-image-chunked/.env.sample` | `WEDA_NODE_PASSWORD=` 留空，加註解說明值從哪裡取得 |
| `examples/feature-transform-pipeline/scripts/test-device-capabilities-upload.sh` | 改為 `NATS_PASS="${WEDA_NATS_PASSWORD:?set WEDA_NATS_PASSWORD}"` |
| `examples/daq-proxy/Devices/FeatureProxyDevice.cs` | 內建預設值改為空字串，空的時候讀環境變數 `WEDA_NATS_PASSWORD`；讀 `systemcfg.json` 的既有邏輯不動 |

**如何驗測**

```bash
PUSH=false PLATFORMS=linux/amd64 ./scripts/build-push-image.sh feature-transform-pipeline
docker run --rm --entrypoint cat feature-transform-pipeline:latest /app/systemcfg.json | grep '"Password"'
```

- 上面必須看到 `"Password": ""`
- 加上 `${WEDA_NATS_PASSWORD:?...}` 之後，`docker compose config` 要帶假值才會通過，這一點同步反映在 P8 的檢查腳本
- 對其餘 13 個 image 重複同一檢查（或以 P8 的靜態檢查涵蓋 `systemcfg.json` 這一側）
- 實機以 `SystemConfig__WedaNode__Password` 注入部署一次，log 要出現 `NATS connection verified`；不注入時要出現認證失敗而非靜默無 log
- 版控內搜尋該密碼字串（`git grep`）必須零命中；`examples/sit-testnode/` 是未納入版控的臨時專案，不在範圍內
- `daq-proxy` 改動後要能建置並啟動，確認讀 `systemcfg.json` 的路徑沒被破壞

---

### P3b —文件同步

**改什麼**

P2 換了部署方式、P3 換了憑證來源，兩者都是使用者會直接踩到的介面，但原本的 plan 沒有安排文件工作。照舊文件走的人會拿到 `required variable WEDA_NATS_PASSWORD is missing a value` 而不知道發生什麼事。

- SOP `09-build-push-deploy-images.md` 新增 §8「手動透過 SSH 部署」：兩份 compose 的差別、一行寫入 `.env` 的指令（`read -rsp`，不進 shell history）、四個可覆蓋的 WedaNode 變數、四種提供憑證的情境、四項檢查、首次與再次部署在 log 上的差別。§2 加一行指向它。
- `examples/README.md`：兩份 compose 的用途，以及 `.env.example` → `.env`。
- `07-tot-30min-subnode-for-si.md` 那條簡報檢查表已因 P3 過期（寫著「範例的 systemcfg.json 有寫死的 lab 憑證」），改成「`.env` 要填好且不要投影」。
- E2E 主文件 Act 3 開頭指明：compose 內容取自該範例的 `docker-compose.deploy.yml`，容器名與業務判準取自 `e2e-expect.json`。

一併修正 P3 引入的一個設計錯誤：密碼用 `${VAR:?}` 會讓 **每個** compose 子指令（`down`、`ps`、`logs`、`config`）都要求該值，只是要停容器也被擋。改為 `${VAR:-}`，沒填密碼時 compose 照常運作，錯誤改在容器 log 裡以 NATS 認證失敗呈現。

**如何驗測**

- 在 EPC-R7300A1 上照 §8 從頭跑一次 `feature-transform-pipeline`：copy → 建 `.env` → `up` → 四項檢查 → `down`
- 不帶任何環境變數時，`config -q`、`ps`、`down` 三個子指令都要成功
- 文件裡出現的每一條指令都必須是實際跑過的那一條，不得改寫

---

### P4 — Track A：交付「現成 image + compose」（先做 feature-transform-pipeline）

客戶拿到一個 image 位址和一份 compose，把 compose 放上裝置、起起來就會動。設定全部在 image 裡，包含要連
哪一台 WedaNode 與密碼。要用自己的密碼就自己 build（Track B）。

**改什麼**

1. **密碼在 build 時寫進 image**。Dockerfile 加 `ARG WEDA_NATS_PASSWORD`，把值寫進已經烘好的
   `/app/systemcfg.json`；`scripts/build-push-image.sh` 從環境變數傳進去。repo 裡仍然是空字串，所以
   `git grep` 還是零命中——值只存在於 build 者的環境與 image 內。沒帶這個變數 build 出來的 image 就是
   空密碼，compose 仍可覆蓋。
2. **部署 compose 的密碼那行改成「有設才傳」**（`- SystemConfig__WedaNode__Password`，不帶等號）。
   原本的 `${WEDA_NATS_PASSWORD:-}` 在沒給值時會送空字串進容器，**把烘進去的密碼蓋掉**，實機上就是
   `Authorization Violation`。
3. **image 那行改成字面值**。既然作法是叫人改那一行，`${X_IMAGE:-...}` 兩層語法只是障礙。15 份一起改
   （含既有的 `VISION_SUBNODE_IMAGE`、`IMAGE_SENSOR_IMAGE`、`CFX_SUBNODE_IMAGE`）。本機開發用的
   `docker-compose.yml` 不是交付物，維持原狀。
4. **`IMAGE_TAG` 預設改為 `YYYYMMDD-HHMMSS`**，可覆蓋。交付一律固定 tag，`latest` 對不出版本。
5. **新增 `scripts/package-example.sh <example> <tag>`**，產出交付包：

   ```
   examples/<example>/delivery/<example>-<tag>/
     docker-compose.yml    ← 唯一要放上裝置的檔案；檔頭換成客戶版，image 釘到該 tag
     README.md             ← 繁體中文，留在交付方手上
   ```

   交付包放在該範例自己的目錄底下（範例的所有東西都在那裡），已加入 `.gitignore`。腳本產完會自己
   grep 一次，內部字眼（缺陷編號、環境名、測試情境、repo 路徑）只要出現就 `exit 1`。
6. **新增 `examples/<example>/delivery.md`**：範例本身在做什麼、正常會看到什麼。README 其餘部分
   （前提、四步驟、故障排除）由腳本以共用樣板產生，避免 15 份 README 各自漂移。
7. **README 的故障排除三條**：`Authorization Violation`（image 裡的密碼不是這台 WedaNode 的，用 `.env`
   覆蓋或另外建置）、`Code=409, Message=Device name already in use`（說明預設名稱以 `-local` 結尾、
   什麼情況會遇到、用 `SUBNODE_NAME` 避開，**不寫成必填**）、`Address already in use`（一台裝置一次
   只能跑一個 SubNode）。
8. **`.env.example` 移除 repo 內部路徑**（原本指向 `docs/wiki/...`，客戶手上沒有）。開發用的
   `docker-compose.yml` 仍走 `.env` 注入，不受影響。

**如何驗測**

- `docker run --rm --entrypoint sh <image> -c 'grep -q "\"Password\": \"\"" /app/systemcfg.json'`
  對交付用 image 必須**不成立**（密碼確實烘進去了），且值不印出來
- 交付包內 `docker compose config -q` 通過，image 行是字面值且帶該 tag
- 交付包內 `grep -riE "D-[0-9]{2}|SIT|E2E|docs/wiki|stack config"` 零命中（腳本已內建此檢查）
- 在 EPC-R7300A1 上只放 `docker-compose.yml`、不建 `.env`，`docker compose up -d` 後四項檢查全過
- `IMAGE_TAG` 不指定時產生的 tag 形如 `20260911-103046`

---

### P4b — Track B：客戶自己 build

客戶手上有整包 repo，適用於 image 要帶自己的 WedaNode 憑證、或改過範例的情況。

**改什麼**

SOP 新增 §9「自己 build image」：範例不能單獨 build（`.csproj` 以 `../../src/...` 引用 SDK，整包 repo
才是 build context，專案必須放在 repo 根目錄下兩層），四個環境變數各自的用途（`IMAGE_REPO` 指自己的
registry、`WEDA_NATS_PASSWORD` 寫進 image、`PLATFORMS` 對齊裝置架構、`IMAGE_TAG` 釘版本）、改
`docker-compose.deploy.yml` 的 image 那行，然後接 §8（SSH）或 §4（平台）。最後一小節說明用
`dotnet new` 開自己的專案時形狀完全一樣，只是要帶 `PROJECT_DIR`。

**如何驗測**

- 以 `IMAGE_REPO` 指向另一個位址 build 並 push 一次，確認腳本吃得到覆蓋
- 文件裡的每一條指令都要能照抄執行

---

### P5 — 修 `dotnet new` 模板

**改什麼**

`templates/subnode` 與 `templates/wedabuilder` 各四項：

1. `systemcfg.json` 的 `localhost:4222` → `127.0.0.1:4224`
2. `Dockerfile` 補上把 `devicecfg.json` / `systemcfg.json` / `customcfg.json` / `appsettings.json` COPY 進 `/app`
3. `docker-compose.yml` 的 `./weda-data:/app/.weda` → `${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<sourceName>}:/app/.weda`
4. `Dockerfile` 寫死 `WORKDIR /src/apps/<sourceName>`，但 `.csproj` 以 `../../src/...` 引用 SDK，代表專案只能放在 repo 根目錄下的 `apps/`；放到 `examples/` 就會 build 失敗。改為以 `ARG PROJECT_DIR` 帶入路徑，`docker-compose.yml` 一併傳值，並在模板 README（若無則在 `docs/wiki/en/02-getting-started/` 的對應頁）寫明這個限制。

`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 保留，但加註解說明「接 OPC UA 時必須移除，否則 X.509 憑證建立會失敗」。

**如何驗測**

```bash
dotnet new install templates/subnode
dotnet new subnode -n TmpProbe -o examples/tmp-probe
docker build -f examples/tmp-probe/Dockerfile --build-arg PROJECT_DIR=examples/tmp-probe -t tmp-probe:test .
docker run --rm tmp-probe:test        # 需在無 mount 的情況下啟動
```

- 放在 `examples/` 與 `apps/` 兩個位置都要 build 成功
- 無任何 bind mount 啟動時，不得出現缺設定檔的錯誤（連不到 NATS 而重試是預期行為）
- `wedabuilder` 模板重複同一組驗證
- 驗證後刪除暫存專案

---

### P6 — `deploy-verify.sh` 支援新判準

**改什麼**

- 參數從 `<example> <compose-file> <container-name>` 簡化為 `<example>`，其餘由 `e2e-expect.json` 讀取。重跑指令因此不再有 `<compose-file>` 佔位符。
- 4.4 只判心跳；新增 4.5 依 `business.mode` 判定：`auto` 要 `patterns` 全部命中才 PASS，`external`、`manual`、`skip` 一律輸出 `NOT-SCORED` 而非 PASS。
- 新增離線模式 `--from-log <file>`，直接對既有 log 檔跑判定邏輯，讓判準本身可以在沒有裝置的情況下驗證。
- `.result` 輸出增加 `r45` / `e45` / `mode` 欄位。

**如何驗測**

```bash
# 用 2026-09-10 那輪已擷取的 log 驗判準，不需要裝置
./scripts/deploy-verify.sh feature-transform-pipeline --from-log artifacts/e2e/feature-transform-pipeline.log
./scripts/deploy-verify.sh modbus-wise4012 --from-log artifacts/e2e/modbus-wise4012.log
```

- `feature-transform-pipeline` 的 4.5 必須 PASS（log 內有溫度轉換）
- `modbus-wise4012` 的 4.5 必須是 `NOT-SCORED`，且理由寫著缺 WISE-4012；4.1–4.4 仍為 PASS
- 至少一個範例在實機跑完整流程，確認簡化後的參數能自己找到 compose 與容器名

---

### P7 — 報告 schema：把功能檢查拆成心跳與業務

**改什麼**

Act 4 的第四項檢查拆成兩項：

- **E2E-EXD-4.4 心跳**：log 出現 `Reporting health` / `Health report sent successfully` 即通過。這一項只證明 SubNode 這個外殼活著、雲端看得到它，與感測器是否運作無關。腳本自動判定。
- **E2E-EXD-4.5 業務**：這個範例存在的目的有沒有達成。每個範例在 `e2e-expect.json` 定義判準，結果分三態：

| 狀態 | 意義 | 誰判 | 適用範例 |
|---|---|---|---|
| ✅ `auto` | 期望可寫成 log 比對，且已有成功樣本 | 腳本 | 01、02、03、04、05、06（共 6 個） |
| ⚠️ `external` | 需要真實硬體或外部發布端才能驗證 | 記為未驗證並寫明缺什麼 | 07、08、09、10、11、12、13、14（共 8 個） |
| ⏭️ `skip` | 不在這台裝置上驗證 | 記原因 | 15 `system-agent` |
| 👤 `manual` | 只能人工判讀數值是否合理 | 人工，報告記判讀人 | 目前無 |

`external` 佔八個，是 P2 逐一比對報告 §3 的實際 log 後的結果，不是預估：09、10、11、13 的 4.4 綠燈只證明**訂閱建立**（`Subscribed to MQTT topic: ...`），沒有任何訊息進來；12 `daq-proxy` 除了心跳沒有任何業務行，報告自己寫著「Passes without its upstream present」。07、08、14 則是硬體不在。

`external` 的 `patterns` 一律留空，因為還沒有成功樣本可以據以定義判準——等發布端或硬體補上、跑出第一次成功的 log 之後再填。

verdict 規則：4.5 是 `external` 或 `manual` 未判讀時，該範例最高只能是 **PARTIAL**，不得標 PASS。

連帶要改：

- 15 份 `docs/wiki/en/07-testing/examples/*.md`：檢查列 8 → 9，`§2` 加一列，`§5 未驗證` 對齊新狀態
- `docs/wiki/en/07-testing/examples/README.md`：欄位 4.1–4.4 → 4.1–4.5，重算 verdict
- `docs/wiki/en/07-testing/01-e2e-test-scenario-example-images.md`：`§6` 新增 4.5 的定義與三態說明、`§7` 表格加欄
- 同一份大報告 `§12` 簽核表：X-3 由「13 confirmed」改為與 `§7` 一致的數字，整體結論一併更新

依 P6 的新規則，09、10、11、12、13 五個都會從 PASS 變 PARTIAL，通過數由 11 變 6。

**如何驗測**

- 15 份報告都要有 9 列檢查與 7 節（以 grep 逐份確認 `E2E-EXD-4.5` 存在）
- README 的統計數字與 verdict 欄逐列相加相符，且與大報告 `§7`、`§12` 三處一致
- 每個 `business.mode` 為 `external` 或 `manual` 的範例，verdict 都不是 PASS

---

### P8 — CI 第一層：靜態檢查腳本

**改什麼**

新增 `scripts/lint-examples.sh`，不需要 docker daemon 之外的任何資源、不需要裝置或 Harbor，對 `examples/*` 與 `templates/*` 同時套用：

| 檢查 | 守住的缺陷 |
|---|---|
| 所有 compose 通過 `docker compose config -q`（以 exit code 判定，並帶假值環境變數） | D-08 |
| `systemcfg.json` 的 `WedaNode.Url` 只允許 `127.0.0.1:4224` | D-01 |
| 全域不得出現 `REPLACE_WITH_` | D-03 |
| `Program.cs` 不得出現無條件的 `.UseMockCloud()` | D-06 |
| Dockerfile 必須 COPY 四個設定檔到 `/app` | D-02 |
| Dockerfile 不得出現 `apt-get install` 搭配 `套件=確切版本` | D-17 |
| 同一份 compose 不得同時出現 `network_mode: host` 與 `ports:` | D-14 |
| 每份 compose 必須以 `${WEDA_DATA_PATH:-...}` 掛 `/app/.weda` | 註冊持久化 |
| `systemcfg.json` 的 `Password` 必須為空字串 | P3 |
| `docker-compose.deploy.yml` 必須存在、有 `image:` 與 `container_name:`、且無 `build:` | P2 |
| 同一組檢查套用在 `templates/*` | 模板漂移 |

腳本以 `exit 1` 表示失敗並列出每一條違規的檔案與行號。`azure-pipelines.yml` 增加一個獨立的 step 呼叫它。

執行 `docker compose config` 時有兩個實作細節，P1 掃描時實測到的：

- 用了 `${VAR:?...}` 的 compose（`cfx-endpoint`，以及 P3 之後的全部）在沒有值時一定失敗，所以檢查要先餵假值（`CFX_BROKER_PASSWORD=x WEDA_NATS_PASSWORD=x ...`）
- 判定要看 exit code，不能看 stderr 是否為空：`system-agent` 的 compose 還留著已廢棄的 `version: '2.4'`，只會吐警告但驗證是通過的。順帶在 P2 把這個鍵移除

**如何驗測**

- 在修完 P1–P4 的樹上執行，必須全數通過並 `exit 0`
- 逐條反向驗證：每一條檢查都刻意製造一次違規（例如把某個 `systemcfg.json` 的 Url 改成 IP、把某支 Dockerfile 的 COPY 刪掉、把 `devices:` 改回空值），確認腳本抓到並 `exit 1`，然後還原
- pipeline 上實跑一次，確認該 step 的失敗會呈現在 PR 上

---

## 3. 相依順序

編號即執行順序：每個階段只依賴編號比它小的階段。

```
P1 ┐
P2 ┤  四項彼此獨立，順序可換
P3 ┤
P4 ┘
      P2 ───> P6 ───> P7
      P4 ───> P5
      P1~P5 ───────> P8
```

- **P6 只依賴 P2**：腳本要讀 `e2e-expect.json`，那份檔案在 P2 產生
- **P7 依賴 P6**：報告寫的是腳本判出來的結果，先改腳本再改報告，否則報告要改兩次
- **P8 放最後**：它把前面訂下的規則固化成檢查，提前接上 pipeline 只會一直紅
- **P4 先於 P5**：模板要不要附交付形狀，取決於 P4 定下的交付包長什麼樣
- 每個階段各自 commit，訊息語言依 `git log` 現有慣例（英文 title + 中文 body）

---

## 4. 已知會被這份計畫改變的結論

- 09、10、11、12、13 由 PASS 降為 PARTIAL（P6）：它們的 4.4 綠燈建立在「訂閱建立」或心跳上，沒有任何業務資料流過
- 統計由 **11 PASS · 2 PARTIAL · 1 BLOCKED · 1 SKIP** 變成 **6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP**，實際值待 P6 重算確認
- 補齊發布端（`cfx-endpoint` 可用 `tools/cfx-replay`）能把其中幾個轉回 `auto`，屬於後續工作
- 既有的 `docker-compose.yml` 全數保留，本機 `docker compose up --build` 的流程不受影響

---

## 5. 變更紀錄

| 日期 | 修訂 |
|---|---|
| 2026-09-10 | 初版 |
