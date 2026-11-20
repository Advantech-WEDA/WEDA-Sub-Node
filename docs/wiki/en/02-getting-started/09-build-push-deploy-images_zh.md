# 建置、推送並部署範例 image

把 SubNode 範例從原始碼變成 WEDA 邊緣裝置上執行中的容器的標準作業程序：建多架構 image、推到 Harbor、
用 container-management API 部署，並確認它真的在運作。

「container-management」是 WEDA 平台負責管理裝置上容器的服務：透過它的 API 下指令，就能把 image
部署到遠端裝置、查看裝置上跑了什麼、把它移除，全程不需要自己 SSH 進裝置操作。本頁 §3~§7 講的就是
怎麼用這條路徑；§8 講的是另一條路——不透過這個服務、自己 SSH 進裝置手動部署。

已在 **EPC-R7300A1**（ARM64, Ubuntu 20.04）對 `weda-sit-k3s.weda.dev` 完整驗證過。

## 0. 每個人都會踩的那一條規則

**build context 是 repo 根目錄，永遠不是範例目錄。** 每個範例的 `.csproj` 都引用
`../../src/Weda.SubNode.*`，而 Dockerfile 會複製整棵樹，所以：

```bash
cd examples/opcua-basic && docker build -t x .     # ✗ 失敗
```

```
MSBUILD : error MSB1003: Specify a project or solution file.
```

這個錯誤既沒提 context 也沒提 Dockerfile，所以看起來像範例壞掉。請用
`scripts/build-push-image.sh`（它會強制正確的 context），或自己從根目錄用 `-f` 指定。

## 1. 讓 image 架構對上裝置

建置前先確認——amd64 的 image 不會在 Jetson 這類裝置上執行：

```bash
curl -s --cacert weda.pem -H "Authorization: Bearer $TOK" \
  "$BASE/api/v1/devices/$DEVICE/capabilities" | grep -i processorArchitecture
#   "processorArchitecture": "ARM64"
```

| 裝置 | 架構 | 要建的目標 |
|---|---|---|
| EPC-R7300（Jetson Orin） | ARM64 | `linux/arm64` |
| x86 邊緣 PC／開發機 | AMD64 | `linux/amd64` |

## 2. 建置與推送

```bash
cd <repo-root>
docker login harbor.arfa.wise-paas.com

# 多架構（預設）——兩種架構、一份 manifest list
# manifest list：同一個 image 名稱＋tag 底下，同時裝著 amd64 與 arm64 兩種架構的版本；
# 裝置拉取時會自動挑對應自己架構的那一份，不用另外指定
./scripts/build-push-image.sh opcua-basic

# 單一架構，對應目標裝置
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh feature-transform-pipeline

# 建的是工具而不是範例
PATH_PREFIX=tools PLATFORMS=linux/arm64 ./scripts/build-push-image.sh simulator-host

# 本機建置、不推送（只能單一平台——--load 吃不了 manifest list）
PUSH=false PLATFORMS=linux/amd64 ./scripts/build-push-image.sh modbus-wise4012
```

image 會落在 `harbor.arfa.wise-paas.com/edge-coa/<名稱>:latest`，可用 `IMAGE_REPO` 與 `IMAGE_TAG`
覆蓋。腳本第一次使用時會建立一個 `docker-container` 類型的 buildx builder（預設的 `docker` driver
產不出 manifest list），並在建置目標含 arm 時安裝 `binfmt`。

確認實際推上去的是什麼：

```bash
docker buildx imagetools inspect harbor.arfa.wise-paas.com/edge-coa/<名稱>:latest
```

release 分支上的這些 image 由 CI 建置：`examples/azure-pipelines.yml` 在每次推送到 `release/vX.Y.Z`、
且動到 SDK 或範例時執行，把每個範例發佈成 `<名稱>:vX.Y.Z_<yyyyMMdd>.<n>`（固定不變，回報問題時引用這個）、
`<名稱>:vX.Y.Z` 與 `<名稱>:latest`。這些 image 一律不含 NATS 密碼；需要內含密碼時，請依下面的方式自己建。
system-agent 有自己的 pipeline。

要建一個準備交出去的 image 時，請在環境裡設好 `WEDA_NATS_PASSWORD`：它會被寫進烘在 image 裡的設定，
於是收到的人只需要 image 和一份 compose 檔。`scripts/package-example.sh <範例> <tag>` 就是把這兩樣
組裝起來。

本頁其餘部分走的是 container-management 部署。想自己透過 SSH、不經平台把 image 放到裝置上，請跳到
[§8](#8-手動透過-ssh-部署)。

### image 依設計是自給自足的

每個範例的 Dockerfile 都把四個設定檔（`devicecfg`、`systemcfg`、`customcfg`、`appsettings`）烘進
`/app`。container-management 部署出去的 stack 沒有本機檔案可以 bind-mount，所以依賴掛載的 image 會
啟動後立刻死掉。本機的 `docker compose` bind mount 仍然會覆蓋烘進去的那份，而任何單一值都可以在執行
時用它的環境變數覆蓋：

```
DeviceConfig__DeviceConfigs__<Device>__DeviceCommunication__Password=...
SystemConfig__WedaNode__Password=...
```

> **烘進去的東西是讀得到的。** 版控裡的 `systemcfg.json` 帶的是空密碼；設了 `WEDA_NATS_PASSWORD` 建
> 出來的 image 會帶著那個值，而這正是「交付出去的 image 能直接跑」的原因。任何能拉這個 image 的人都
> 讀得到它，所以請為每一群可以持有該憑證的對象各建一個 image；如果某台裝置的 WedaNode 不同，用 stack
> 的 `environment` 區塊處理。

## 3. 每個組織註冊一次 registry

裝置是以自己的身分去 Harbor 拉 image，所以組織需要有 registry 憑證，否則每次拉取都會因認證失敗而
出錯，而且只會表現成「部署卡住」：

```bash
curl -s -X POST "$BASE/api/v1/orgs/$ORG/containers/registries" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  --data @registry.json      # 不要把密碼直接寫在指令列
```

建立前先確認有沒有重複的：

```bash
curl -s -H "Authorization: Bearer $TOK" "$BASE/api/v1/orgs/$ORG/containers/registries"
```

## 4. 建立 stack config

「stack config」是 container-management 平台上的一個設定物件，記錄「要在裝置上跑哪一份 compose」；
建立它、之後把它部署到某台裝置，就是 §5 在做的事。

`composeFileContent` 是 **base64 編碼**的 compose YAML，不是純文字。

```bash
COMPOSE_B64=$(base64 -w0 stack-compose.yml)
curl -s -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d "{\"stackName\":\"my-stack\",\"type\":\"compose\",
       \"composeFileContent\":\"$COMPOSE_B64\",\"description\":\"...\"}"
```

回應會帶 `stackConfigId` 與 `stackRevisionId`。每次編輯都會產生一個不可變的新版本；部署時一定是部署
某個特定版本，而回退就是部署較舊的那個版本。

當容器必須連到裝置自己的 dmagent（跑在每台 WEDA 裝置上、負責跟雲端溝通並管理容器的常駐程式；它
開的 NATS 埠在 `127.0.0.1:4224`），或透過 localhost 連到同一個 stack 裡的另一個容器時，請用
`network_mode: host`。

### 怎麼提供變數

stack config 在 compose 旁邊帶著一份 `environment` map，那就是 compose 裡 `${...}` 的解析來源——這條
路徑上的 `.env` 等價物。所以同一份 `docker-compose.deploy.yml` 在這裡和在 SSH 那條路都不用改，差別只
在值從哪裡來。多數部署不需要在裡面放任何東西：image 自己帶著 WedaNode 的連線資訊，而 SubNode 名稱的
預設值本來就每台裝置不同。只有在要把某個 image 指向另一台 WedaNode 時，才需要在這裡放
`SystemConfig__WedaNode__Password`。

```bash
COMPOSE_B64=$(base64 -w0 examples/<範例>/docker-compose.deploy.yml)
curl -s -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d "{\"stackName\":\"my-stack\",\"type\":\"compose\",
       \"composeFileContent\":\"$COMPOSE_B64\",
       \"environment\":{\"SUBNODE_NAME\":\"MySubNodeName\"},
       \"description\":\"...\"}"
```

放在這裡的憑證是由 stack config 持有，不是由某一台裝置上的檔案持有——這與 `.env` 是不同的暴露面，
值得刻意決定而不是預設如此。

鍵名是照字面取用的：map 裡的 `WEDA_NATS_PASSWORD` 就是 compose 裡 `${WEDA_NATS_PASSWORD}` 取到的
那個。這份 map 是插值來源，不是無條件注入——compose 完全沒提到的鍵不會進到容器裡。

agent 會自己加一個：**`EDGESYNC_EDGE_ID`**，也就是裝置 id，除非 map 裡已經定義了這個鍵
（[`stack.go`](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/digitaltwin_device_mgmt_agent)
在部署前會確保它存在）。每份部署用 compose 都用它讓 SubNode 名稱每台裝置不同：

```yaml
- DeviceConfig__SubNode__Name=${SUBNODE_NAME:-TestDeviceCmd-${EDGESYNC_EDGE_ID:-local}}
```

| 部署方式 | 註冊時用的名稱 |
|---|---|
| container-management | `TestDeviceCmd-48b02dea8160`——agent 提供裝置 id，所以一份 stack config 可以服務任意多台裝置 |
| 手動並設了 `SUBNODE_NAME` | 你設的那個 |
| 手動且兩者都沒設 | `TestDeviceCmd-local` |

只有第一次部署會用到這個名稱——見 [§8.4](#84-確認它有在做事)。

## 5. 部署到裝置

```bash
curl -s -X POST \
  "$BASE/api/v1/orgs/$ORG/stack-configs/$STACK_ID/revisions/$REV_ID:deploy" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d '{"deviceIds":["48b02dea8160"]}'
```

這會改變真實硬體上跑的東西——先確認裝置清單。

## 6. 驗證，四個步驟

不要停在「API 回了 200」。底下每一步都證明前一步證明不了的事：

| # | 檢查 | 證明 |
|---|---|---|
| 1 | `GET /api/v1/orgs/$ORG/stack-configs/deployments?deviceIds=$DEVICE` → 狀態到達 `deployed`／`running` | 裝置接受並套用了該版本 |
| 2 | `GET /api/v1/devices/$DEVICE/docker/stacks` 列出該 stack 且容器為 `running` | image 拉下來了，而且容器真的啟動了 |
| 3 | 裝置的 telemetry 出現新的感測值 | 容器裡的 SubNode 在做自己的工作 |
| 4 | `GET /api/v1/devices/$DEVICE/capabilities/sensors` 列出 SubNode 註冊的感測器 | SubNode 連上了 WedaNode 並註冊了自己的能力清單 |

容器 `running` 只證明行程沒有結束——一個連不到自己裝置或 WedaNode 的 SubNode 會一直活著並持續記錯誤。
第 3、4 步才是「已部署」與「在運作」的分界。

## 實際跑過才知道的坑

第一次在 EPC-R7300 上跑時有三件事花了時間；三件看起來都像基礎設施故障，但都不是。

**要持久化 `/app/.weda`，否則 SubNode 無法重新註冊。** 這件事會咬你兩次。
`/app/.weda/subnode.registration.json` 存的是已被接受的註冊。沒有 volume 的話，每次重建容器它都會被
摧毀，於是 SubNode 從頭註冊並撞上自己先前的註冊：

```
Failed to register SubNode <name> with Cloud: Code=409, Message=Device name already in use
```

每個範例的 `docker-compose.yml` 都透過 `WEDA_DATA_PATH` 掛載它，預設值是裝置上的慣例
`/opt/Advantech/weda/node/{containerServiceName}`；透過 container-management 部署的 stack 也必須宣告
同樣的東西，否則它只能成功一次：

```yaml
    volumes:
      - ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service-name>}:/app/.weda
```

要把狀態搬到別的地方（不同磁碟、共用掛載點、或本機開發用的暫存目錄），設定 `WEDA_DATA_PATH`：

```bash
WEDA_DATA_PATH=./weda-data docker compose up -d     # 改成放在範例旁邊
```

如果在一台已經註冊過的裝置上更改狀態的位置，請先把既有目錄搬過去，否則 SubNode 會重新註冊並撞上
下面那個 409：

```bash
docker run --rm -v <old-volume>:/from -v /opt/Advantech/weda/node/<service>:/to busybox cp -a /from/. /to/
```

volume 就位之後，重啟容器會以同一個 `deviceId` 繼續回報，完全不會重新註冊。

**`409 Device name already in use`。** 同樣的錯誤、不同的原因：`devicecfg.json` 裡的名稱已經被組織裡
另一台不相干的裝置佔用。每次部署時覆蓋它：

```yaml
    environment:
      - DeviceConfig__SubNode__Name=MyDemo-<deviceId>
```

要注意 SubNode 子裝置事後不一定清得掉：對 `/api/v1/devices/{subNodeId}` 的 `GET`、`:deactivate` 與
`DELETE` 都可能對一個「組織清單仍然列出」的裝置回 `404`，所以被殘留的子註冊佔掉的名稱可能拿不回來。

**mock cloud 是設定，不是改程式碼。** 曾經有三個範例寫死 `.UseMockCloud()`，因此不管怎麼設定都只會
把 telemetry 記在本機、永遠連不到 WedaNode。它們現在改讀 `CustomConfig:UseMockCloud`，預設走真雲；
要在沒有 WedaNode 的情況下運作，就把 `customcfg.json` 裡那個值設成 `true`，或在環境裡設
`CustomConfig__UseMockCloud=true`。

**有些範例已經內建模擬器。** `feature-transform-pipeline` 會自己在 `127.0.0.1:5020` 起一個 Modbus TCP
模擬器。在同一個 stack 裡再加一個 `simulator-host` 容器會先佔走那個埠，SubNode 就死於
`SocketException (98): Address already in use`——接著 `restart: unless-stopped` 讓它陷入崩潰迴圈，而
API 仍然回報容器為 `running`。把範例與模擬器配在一起之前，先看它的 log 裡有沒有「Modbus Simulator
started」。

**部署狀態會落後裝置，甚至與裝置完全相反。**
`…/stack-configs/deployments` 可能在裝置早就拉好、重建並啟動容器之後，還顯示 `pending` 且容器清單是
空的。更糟的是，在改過 stack 裡的服務名稱之後，雲端顯示 `deployed`／`Stack is managed but not
currently running`，而裝置上根本沒有那個 compose 專案。在那個狀態下：

- `commands:start` 會失敗於 `Command execution failed: stack not found`
- 重新部署同一版本會被回 `"skip"`，因為雲端認為它已經在那裡了

出路是刪掉裝置端的副本再部署一次，那次會回 `"add"`：

```bash
curl -X DELETE "$BASE/api/v1/devices/$DEVICE/docker/stacks?stackConfigId=$STACK_ID" -H "Authorization: Bearer $TOK"
# 然後再 POST …/revisions/$REV:deploy
```

永遠以裝置本身為準（`docker compose ls`、容器 log），不要相信部署紀錄。

**批次建置會把 Docker 磁碟塞爆。** 一次把所有範例建成多架構，每個範例都會帶兩份完整的 self-contained
.NET runtime 通過 BuildKit。在建置主機上這把 Docker 的資料磁碟區（與 `/` 是不同的掛載點）塞滿，build
失敗於 `no space left on device`、`ResourceExhausted`，以及看起來像網路故障的 NuGet 損毀錯誤。批次
之間先 prune：

```bash
docker buildx prune -f --keep-storage 2GB
```

## 7. 清理

```bash
# 從裝置上移除一個 stack
curl -s -X DELETE "$BASE/api/v1/devices/$DEVICE/docker/stacks?stackConfigId=$STACK_ID" \
  -H "Authorization: Bearer $TOK"
```

## 8. 手動透過 SSH 部署

一個範例會出現三份 compose，先確認你要用哪一份：

| 檔案 | 誰用 | 在哪裡用 | image 從哪來 | 設定從哪來 |
|---|---|---|---|---|
| 1. `examples/<範例>/docker-compose.yml` | **SI**（系統整合商） | 自己的開發機 | `build:`，從這份 checkout 現場編譯 | 四個 JSON 檔，從範例目錄 bind-mount 進去 |
| 2. `examples/<範例>/docker-compose.deploy.yml` | **SubNode Application 開發者**（透過 container-management 或手動 SSH） | 裝置 | `image:`，從 registry 拉 | 烘在 image 內的 `/app` |
| 3. `examples/<範例>/delivery/<範例>-<tag>/docker-compose.yml` | **客戶** | 客戶自己的裝置 | 同上，但 tag 釘死 | 同上 |

**第一份的目的**：SI 的機器上沒有 .NET，`dotnet run` 對他不存在。他的工作是改
`devicecfg.json`（感測器位址、register、輪詢間隔）；`build:` 讓編譯發生在容器裡、bind-mount 讓他改完
重啟就生效，兩者都不需要他本機裝任何東西。這份在裝置上沒有用——裝置不編譯，也沒有那四個 JSON 可掛。

**本機開發不用碰前兩份**：有 .NET 的話 `dotnet run` 最快，改完程式立刻看得到。

**SubNode Application 開發者要驗證裝置上的行為時，用的是第二份** `docker-compose.deploy.yml`——不管是
貼進 container-management stack config（§4）還是手動 SSH。它的 image 寫的是 `:latest`，範例只要重新
build、push，它指到的就是最新那個，15 個能部署的範例也都有這一份，所以 `e2e-expect.json` 與
`deploy-verify.sh` 拿它做持續驗測。底下 8.1~8.6 講的都是這一份，只是改用手動驅動。

**第三份是第二份的快照**，由 `scripts/package-example.sh` 產生：內容相同，只是把 image 釘到某個 tag、
檔頭換成客戶拿得到的版本。要改部署行為就改第二份，然後重跑打包腳本；第三份是 delivery 給客戶的固定
版本，把它貼進 stack config 不會壞，只是驗到的會是「某次交出去的舊版本」而不是「現在的樣子」。

### 8.1 把 compose 檔放到裝置上

```bash
ssh <device> 'mkdir -p /opt/<範例>-demo'
scp examples/<範例>/docker-compose.deploy.yml <device>:/opt/<範例>-demo/
```

裝置上只需要這一個檔案。憑證——如果真的需要的話——在 8.2 才寫上去。

### 8.2 憑證，以及什麼時候才真的需要

用 `build-push-image.sh` 並在環境裡設了 `WEDA_NATS_PASSWORD` 建出來的 image 會帶著那個密碼，所以跑它
的裝置不需要其他東西。沒設就建的話，image 帶的是版控裡那個空密碼，值就得改由環境提供。
不論哪一種，值都是該 image 要跑的那台裝置上 dmagent 的 `NATS_PASSWORD`，在那台裝置上用
`grep NATS_PASSWORD /opt/Advantech/dmagent/docker-compose.yml` 讀取。

compose 裡那一項是**不帶等號的鍵**——`- SystemConfig__WedaNode__Password`——所以只有在環境真的有定義
時才會被傳進去。這一點很重要：寫成 `=${WEDA_NATS_PASSWORD:-}` 時，沒設值會展開成空字串，而空的環境
變數會贏過 JSON 檔，把 image 建置時寫進去的密碼抹掉。症狀是 log 一直出現 `Authorization Violation`。

要把某個 image 指向另一台 WedaNode，把值寫在 compose 檔旁邊：

```bash
cd /opt/<範例>-demo
read -rsp 'WedaNode NATS password: ' PW && \
  printf 'SystemConfig__WedaNode__Password=%s\n' "$PW" > .env && \
  unset PW && chmod 600 .env
```

這個檔案必須讓執行 `docker compose` 的那個使用者讀得到，在 `/opt` 底下通常代表不要用 `sudo` 去建它。

可以這樣覆蓋的有四個值：

| 變數 | image 裡帶的是 | 什麼時候要覆蓋 |
|---|---|---|
| `SystemConfig__WedaNode__Password` | 建置時寫進去的那個 | 那台 WedaNode 用的是不同的密碼 |
| `SystemConfig__WedaNode__Url` | `127.0.0.1:4224` | WedaNode 不是這台裝置自己的 |
| `SystemConfig__WedaNode__Username` | `advantech_nats` | 那台節點用不同的帳號 |
| `SystemConfig__WedaNode__AuthStrategy` | `UserPassword` | 改用 `Token`、`CredFile` 或 `TlsCert`——見 [連線到 WedaCore](04-connect-to-wedacore.md) |

| 情境 | 做法 |
|---|---|
| 手動，而且要撐過容器重建 | `.env` 放在 compose 檔旁邊，如上 |
| 一次性，不寫入磁碟 | `SystemConfig__WedaNode__Password=… docker compose -f docker-compose.deploy.yml up -d`——每次 `down` 之後都要重打 |
| 自動化部署 | `--env-file /path/to/secrets.env`，由部署系統放置 |
| 透過 container-management | stack config 的 `environment` map——見 §4 |

密碼錯誤或缺少不會在 compose 這一層被擋下來。容器會啟動並在自己的 log 裡回報認證失敗，錯誤出現在它該
出現的地方——而 `down`、`ps`、`logs` 不論有沒有設值都照常運作。

### 8.3 啟動

```bash
cd /opt/<範例>-demo
docker compose -f docker-compose.deploy.yml up -d
```

`pull_policy: always` 會去抓當前的 `:latest`，所以不需要另外 `docker pull`。

### 8.4 確認它有在做事

```bash
docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' <容器>
docker logs <容器> 2>&1 | grep -E \
  "Registering SubNode|Found existing SubNode|NATS connection verified|NATS service discovered|Configuration uploaded|Reporting health"
```

`<容器>` 就是該範例部署 compose 裡的 `container_name`，同時也記在
`examples/<範例>/e2e-expect.json` 裡，連同「這個範例該被看到做什麼」。

| 這一行 | 代表 |
|---|---|
| `Registering SubNode <name> with Cloud` | **首次部署**——`/app/.weda` 是空的，所以它以 `SUBNODE_NAME` 註冊 |
| `Found existing SubNode registration` | **再次部署**——它沿用了快取的身分 |
| `NATS connection verified` + `NATS service discovered: … WedaNodeId=<deviceId>` | 它連到的是*這台*裝置的 WedaNode，不是某台開發機 |
| `Configuration uploaded successfully` | 它註冊成了自己的子裝置 |
| `Reporting health: … IsHealthy=True` | 心跳在跑 |

**`SUBNODE_NAME` 只在首次部署有意義。** 一旦 `/app/.weda/subnode.registration.json` 存在，快取的
DeviceId 會被沿用，而設定的名稱不會拿來比對，所以用這個方式替一個已註冊的 SubNode 改名不會有任何效果。
在首次部署上，一個在該組織已被用過的名稱會被拒絕：`409 Device name already in use`。

### 8.5 覆蓋的不只是憑證

環境變數可以觸及任何單一欄位——`DeviceConfig__DeviceConfigs__<name>__<field>` 同樣定址到巢狀的值
——所以少數幾處差異不需要動到檔案。當某台裝置真的需要*不同的內容*（不同的感測器、不同的輪詢間隔）
時，改用 bind-mount 把檔案蓋在烘進去的那份上面：

```yaml
    volumes:
      - /opt/<範例>-demo/devicecfg.json:/app/devicecfg.json:ro
      - ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service>}:/app/.weda
```

把要改的檔案從範例目錄複製出來、在裝置上編輯，再加上這個掛載。`system-agent` 是現成的先例：它完全
不烘設定，四個檔案全部從 `/opt/Advantech/system-monitor/` 掛進來，因為那些內容是由安裝程式逐台管理的。

動手之前要接受兩個後果：

- **掛上去之後就永遠贏。** 拉了更新的 image 也不會改變那個檔案，而且是默默地。裝置會繼續跑它手上的
  那份。
- **只有在放得了檔案的地方才成立。** stack config 帶的是一份 compose 文件和一份 environment map
  ——所以透過 container-management 的部署放不了那個檔案；路徑必須事先就存在於裝置上。

這就是為什麼憑證走環境變數、內容留在 image 裡：環境變數兩條部署路徑都到得了，檔案只到得了一條。

### 8.6 停止

```bash
cd /opt/<範例>-demo
docker compose -f docker-compose.deploy.yml down
```

`/app/.weda` 位於主機上的 `WEDA_DATA_PATH`，不會因此消失。刪掉它會讓下次啟動重新註冊。

## 9. 自己建 image

範例交付出去的另一種方式：交的是這個 repo，而不是已發佈的 image。當 image 必須帶你自己的 WedaNode
憑證、或你改過範例時，用這條路。

範例沒辦法單獨建置——它的 `.csproj` 以 `../../src/Weda.SubNode.*` 取得 SDK，所以整個 repo 才是 build
context，而專案必須位在 repo 根目錄底下兩層。這就是為什麼這裡每次建置都從 repo 根目錄執行，也是
`build-push-image.sh` 強制這一點的原因。

### 9.1 建置並發佈

```bash
cd <repo-root>
docker login <your-registry>

IMAGE_REPO=<your-registry>/<namespace>/opcua-basic \
WEDA_NATS_PASSWORD='<這個 image 要連的那台 WedaNode 的密碼>' \
PLATFORMS=linux/arm64 \
  ./scripts/build-push-image.sh opcua-basic
```

| 變數 | 為什麼 |
|---|---|
| `IMAGE_REPO` | 你拉得到的位址；不設的話腳本會推到研華的 registry，而你可能沒有寫入權限 |
| `WEDA_NATS_PASSWORD` | 會寫進烘在 image 裡的設定，於是裝置不需要其他東西。不設的話 image 帶的是空密碼，改由 compose 提供 |
| `PLATFORMS` | 對上裝置：EPC-R7300 用 `linux/arm64`，x86 機器用 `linux/amd64`。預設兩種都建 |
| `IMAGE_TAG` | 預設是時間戳 `YYYYMMDD-HHMMSS`。釘一個你能在問題回報裡引用的版本 |

腳本會印出它推上去的 manifest。在靠近裝置之前先看架構清單：缺少該裝置架構的 image 會在拉取時因
manifest 不符而失敗，而那個錯誤看起來像 registry 有問題。

### 9.2 把 compose 指向它

`examples/<名稱>/docker-compose.deploy.yml` 裡有一行字面值的 image：

```yaml
    image: harbor.arfa.wise-paas.com/edge-coa/opcua-basic:latest
```

改成你推上去的那個。檔案裡其他部分本來就適合裝置。

### 9.3 之後完全一樣

SSH 那條路照 [§8](#8-手動透過-ssh-部署) 部署，透過平台則照 [§4](#4-建立-stack-config)。
[§8.4](#84-確認它有在做事) 的檢查不變。

### 9.4 不是範例，而是你自己的專案

`dotnet new subnode` 與 `dotnet new wedabuilder` 產生的專案形狀完全一樣：設定會烘進去、`PROJECT_DIR`
帶著專案的位置，而且同樣吃 `WEDA_NATS_PASSWORD` 這個 build 參數。把它 scaffold 到 repo 根目錄底下
兩層——`apps/<名稱>` 或 `examples/<名稱>`——並把那個路徑傳進去：

```bash
dotnet new wedabuilder -n MyDevice -o apps/my-device
docker build -f apps/my-device/Dockerfile --build-arg PROJECT_DIR=apps/my-device \
  --build-arg WEDA_NATS_PASSWORD='<密碼>' -t <your-registry>/my-device:1.0.0 .
```

## 延伸閱讀

- [`scripts/build-push-image.sh`](../../../../scripts/build-push-image.sh) — 建置／推送腳本
- [範例索引](../../../../examples/README.md) — 「Building a container image」
- [連線到 WedaCore](04-connect-to-wedacore.md) — 憑證與 WedaNode 連線
