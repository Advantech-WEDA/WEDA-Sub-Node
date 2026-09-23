# SubNode 範例可部署性、交付與防呆機制 — 開發計畫

| | |
|---|---|
| **分支** | `docs/e2e-example-report-schema` |
| **狀態** | 已完成 — 八項工作全數實作並驗測；`feature-transform-pipeline` 的交付包已在 EPC-R7300A1 實跑通過 |
| **前置 PR** | #7361（範例可部署性修正）、!7396（報告 schema + 2026-09-10 重跑） |
| **範圍外** | 排程 build、把 pipeline 既有測試改成會擋、真機部署進 CI |
| **後續** | 另外 5 個自帶模擬器的範例（`feature-custom-commands`、`http-air-quality`、`http-stock-quotes`、`feature-aggregation`、`opcua-basic`）的 `delivery.md` 已備妥，待 build 並 push 後產交付包 |

---

## 0. 這份計畫解決什麼

`!7396` 把 15 份範例測試報告統一了 schema 並重跑一輪，review 找到六件事：

| # | 問題 | 說明 | 對應段落 |
|---|---|---|---|
| 1 | 15 份 compose 有 8 份只能本機編譯，裝置上不能編譯 | 裝置沒有原始碼、不能現場編譯，只能拉現成的 image 來跑。這 8 份範例的 compose 檔還停在「本機編譯」那個形狀，沒有一份「直接拉 image 來跑」的版本可以部署。也因為沒有這份檔案，各範例報告「怎麼重跑這個測試」那節的指令只能留一個 `<compose-file>` 佔位字讓人自己填——例如 `01-feature-transform-pipeline.md` 原本寫的是 `./scripts/deploy-verify.sh feature-transform-pipeline <compose-file> weda-transform-pipeline` | C |
| 2 | `14-daq-collector.md` 這份報告自相矛盾 | 同一份文件裡，「Deploy」那格打勾寫部署成功，底下「Defects」列表卻又寫著 D-08「`docker-compose.yml` 驗證失敗：`devices must be a list`」還沒修好。兩句話兜不起來，是因為測試當時實際部署用的檔案，跟現在 repo 裡的 `examples/daq-collector/docker-compose.yml` 不是同一份 | A + C |
| 3 | `01-e2e-test-scenario-example-images.md` 這份總覽報告裡兩個地方數字對不上 | 開頭摘要寫「13 個確認可用」，後面 §7 逐範例列表卻列著「11 個」 | G |
| 4 | 「範例真的有在運作」這一格，其實只檢查了心跳，沒檢查真正的功能 | 見 E 段的完整說明 | E + G |
| 5 | 用 `dotnet new` 產生新專案時，會重新出現舊範例已經修好的問題 | 因為修的是既有的 15 個範例，產生新專案用的模板沒有跟著修 | D |
| 6 | 連線密碼明文寫在 20 個檔案裡，而且會跟著 image 一起上傳到公開的 image 倉庫 | 任何人只要能拉到這個 image，就能從裡面翻出密碼 | C |

除了修這六個問題，這次還多一個目標：**讓範例能直接交給客戶用**——客戶只要拿到一個 image 位址和一份
啟動設定檔，放到裝置上就能跑起來，不需要再自己準備任何東西。

---

## 1. 命名與版本慣例

**這張表在講什麼**：15 個範例裡，只有 6 個「可交付」（見上面新增的目標）——原因很簡單，另外 9 個
都需要接真實硬體（例如某台特定型號的儀表）或某個外部系統來源才看得到效果，把這種範例包成一個獨立
的 image 交給客戶，客戶啟動後只會看到「連不到硬體」的錯誤，沒有意義。所以現階段先讓那 6 個能單獨
運作、示範用的範例可以交付。

image 名稱一律等於範例目錄名，與 `scripts/build-push-image.sh` 的預設一致
（`harbor.arfa.wise-paas.com/edge-coa/${NAME}`）。容器名沿用 2026-09-10 那輪報告已經在用的名字。

| # | 範例目錄 | 容器名 | 隨附服務 | 可交付 |
|---|---|---|---|---|
| 01 | `feature-transform-pipeline` | `weda-transform-pipeline` | — | ✅ 已交付 |
| 02 | `feature-custom-commands` | `weda-command-handler` | — | ✅ 待 build |
| 03 | `http-air-quality` | `weda-air-quality` | — | ✅ 待 build（需外網） |
| 04 | `http-stock-quotes` | `weda-stock` | — | ✅ 待 build（需外網） |
| 05 | `feature-aggregation` | `weda-power-agg` | — | ✅ 待 build |
| 06 | `opcua-basic` | `weda-opcua` | — | ✅ 待 build |
| 07 | `modbus-wise4012` | `weda-modbus` | — | ❌ 需 WISE-4012 |
| 08 | `modbus-wise4012-builder` | `weda-modbusb` | — | ❌ 需 WISE-4012 |
| 09 | `mqtt-image-chunked` | `weda-image-sensor` | mosquitto | ❌ 需影像發布端 |
| 10 | `mqtt-isensing-wise4012` | `weda-isensing` | mosquitto | ❌ 需 iSensing 發布端 |
| 11 | `vision-object-detection` | `weda-vision` | mosquitto | ❌ 需 CV 發布端 |
| 12 | `daq-proxy` | `weda-daqp` | — | ❌ 需上游 SubNode |
| 13 | `cfx-endpoint` | `weda-cfx` | rabbitmq | ❌ 需 CFX 來源 |
| 14 | `daq-collector` | `weda-daqc` | — | ❌ 需 DAQ 模組 |
| 15 | `system-agent` | `weda-system-agent` | — | ❌ 產品元件 |

「可交付」的判準是**不需要外部硬體或發布端就能看到成果**。交給客戶一個起來就報錯說連不到硬體的包沒有意義。

- **image tag**：`YYYYMMDD-HHMMSS`，`IMAGE_TAG=` 可覆蓋。交付一律固定 tag，`latest` 對不出版本。
- **交付包**：`examples/<範例>/delivery/<範例>-<tag>/`，內含 `docker-compose.yml` 與繁體中文 `README.md`，納入版控。

---

## 2. 工作項

### A — 修 `daq-collector` compose 的驗證錯誤

`devices:` 鍵存在但所有項目都被註解掉，等於空值，compose 驗證直接失敗
（`services.daq-collector.devices must be a array`）。把鍵本身也一起註解，並寫明取消註解時要連鍵一起打開。

**如何驗測**：`docker compose -f examples/daq-collector/docker-compose.yml config -q` 無輸出。

---

### B — `vision-object-detection` 開發用 compose 不再蓋掉 image 的設定

`WEDA_NODE_URL` 預設 `127.0.0.1:4222`（錯的 port）、`WEDA_NODE_USERNAME` 預設空字串。環境變數在 JSON
之後套用，所以這兩個會蓋掉烘進 image 的 `systemcfg.json`。

這其實是同一個問題的重演：`E2E-EXAMPLE-IMAGES-001` 那份測試報告把「範例的連線設定寫錯，指向了開發者
自己的機器或錯誤的 port」這個缺陷編號為 D-01，原本已經在 `systemcfg.json` 裡修好。但這裡的
`docker-compose.yml` 是用環境變數去覆蓋 `systemcfg.json` 的值，而環境變數裡又是同一個錯——等於修好的
東西又從另一層被蓋回去。改為 `4224` 與 `advantech_nats`。

**如何驗測**：`docker compose config` 展開後兩個值正確。

---

### C — 可部署與可交付

**目標**：裝置上跑得起來；客戶拿一個 image 位址加一份 compose 就能跑。

**做法**

1. **每個範例新增 `docker-compose.deploy.yml`**，讓裝置拉現成 image 就能跑：
   - `image:` 寫字面值——客戶要換位址或版本，改的就是這一行
   - `container_name:` 固定——查 log 的指令不必猜容器叫什麼
   - 唯一的掛載是 `/app/.weda`，那裡放已被接受的註冊；沒有它，每次重建容器都會重新註冊並撞上自己
     先前的身分（`409 Device name already in use`）
   - 隨附服務一律 host networking——與 bridge 混用時 agent 會部署不起來且不吐 log
   - `system-agent` 例外：它的設定由安裝程式放在裝置的 `/opt/Advantech/system-monitor/`

2. **設定與密碼都放進 image**，讓客戶不必準備任何檔案：
   - Dockerfile 把四個 JSON 檔 COPY 進 `/app`
   - 密碼：**建置的人**在自己的 shell 設 `WEDA_NATS_PASSWORD=<值>` 之後再跑
     `scripts/build-push-image.sh`；腳本以 `--build-arg WEDA_NATS_PASSWORD=...` 傳給 `docker build`，
     Dockerfile 的 `ARG` 再把它寫進 image 裡的 `systemcfg.json`
   - repo 裡那份 `systemcfg.json` 永遠是空字串，密碼不進版控

3. **`scripts/package-example.sh <範例> <tag>` 產出實際交給客戶的東西**：
   `examples/<範例>/delivery/<範例>-<tag>/` 底下的 `docker-compose.yml`（image 釘到該 tag）與中文
   `README.md`。納入版控，因為它是「交出去的是哪一版」的紀錄。

4. **compose 裡的憑證項目寫成不帶等號的 bare key**（`- SystemConfig__WedaNode__Password`），意思是
   「環境有設就傳、沒設就不傳」。這樣客戶不設定時 image 裡的密碼留著，要換 WedaNode 時在 compose 旁
   放 `.env` 就能覆蓋。

5. **SubNode 名稱預設 `${SUBNODE_NAME:-<烘進去的名字>-${EDGESYNC_EDGE_ID:-local}}`**，讓同一份 compose
   推到多台裝置不會撞名——`EDGESYNC_EDGE_ID` 是 agent 部署時自動注入的裝置 id。

6. **`examples/<範例>/e2e-expect.json` 記錄容器名、image 與「該被看到做什麼」**，讓驗證腳本（E）與
   報告（G）用同一份來源，不會各自漂移。

7. **repo 內其餘五處同一組密碼一併清空**，讓「版控裡沒有明文密碼」這件事成立。

**如何驗測**

```bash
for f in examples/*/docker-compose.deploy.yml; do docker compose -f "$f" config -q || echo "FAIL $f"; done
docker run --rm --entrypoint sh <交付用 image> -c 'grep -q "\"Password\": \"\"" /app/systemcfg.json'
```

- 15 份 compose 都通過驗證，都有 `image:`、`container_name:`、`WEDA_DATA_PATH`，都沒有 `build:` 或
  `./*.json` 掛載
- 第二行對交付用 image 必須**不成立**，且值不印出來
- `git grep` 該密碼字串零命中
- 不帶環境變數時 `config`、`ps`、`down` 都成功；帶 `SystemConfig__WedaNode__Password` 時展開成傳入的值
- 交付包內 `grep -riE "D-[0-9]{2}|SIT|E2E|docs/wiki|stack config"` 零命中
- 在 EPC-R7300A1 上只放 `docker-compose.yml`、不建 `.env`，`up -d` 後四項檢查全過

---

### D — 修 `dotnet new` 模板

**這裡的「模板」指什麼**：`templates/subnode/` 與 `templates/wedabuilder/` 這兩個資料夾。使用者打
`dotnet new subnode` 或 `dotnet new wedabuilder` 時，工具就是照這兩份骨架長出一個新專案。C 段修的是
現有 15 個範例；這裡修的是「以後生出新範例／新專案」用的模子——模子本身沒修，新專案會把 C 段修掉的
問題重新生一次。

**模子裡帶著五個問題，跟 C 段修過的是同一批**：

1. 連線位址寫死 `localhost:4222`（對照 B 段修過的 `172.22.160.197:4222`，同一類錯誤：指向開發者
   自己的機器，不是裝置上的 WedaNode）
2. 設定檔沒有烘進 image（對照 C 段第 2 點，裝置上沒有檔案可掛，容器會直接啟動失敗）
3. 註冊資料存在 `./weda-data`（相對路徑，容器一刪就不見，對照 C 段的 `/app/.weda` 掛載）
4. Dockerfile 把專案路徑寫死成 `apps/<專案名>`，但範例照慣例會建在 `examples/<專案名>`，路徑對不上
   導致 build 失敗
5. `Program.cs` 不管設定寫死選 mock cloud（假的雲端，本機模擬用），新專案不改一行程式碼就永遠連不到
   真正的 WedaNode

**怎麼修**：

- 新增一個建置參數 `PROJECT_DIR`，讓使用者建置時告訴 Dockerfile「我的專案放在哪裡」，不用再寫死
  `apps/<name>`
- 讀取 `CustomConfig:UseMockCloud` 這個設定值來決定要不要用 mock cloud，預設為 false（連真正的
  WedaNode），不再寫死
- 密碼的處理方式跟 C 段第 2 點一樣：建置時用 `ARG` 把密碼寫進 image

**如何驗測**：把兩個模板都建成新專案、放到 `examples/` 底下，確認能編譯成功；用
`PROJECT_DIR=examples/tmp-probe` 建置 image 成功；跑起來的容器不掛任何檔案也能啟動、讀得到烘進去的
設定，而且**不再印出 mock cloud 的提示字樣**（代表真的連的是真雲端，不是模擬用的假雲端）。

---

### E — `deploy-verify.sh` 把「活著」與「有在做事」分開判

**背景**：範例部署到裝置後，怎麼知道它「真的在運作」？之前的驗證腳本 `deploy-verify.sh` 只看一件事
——裝置有沒有定時傳心跳訊號回雲端。但心跳只代表「這支程式沒當機」，不代表它真正的工作（讀感測器、
轉發資料）有在跑。以前只看心跳，等於一個範例卡死在「連上了但什麼都沒做」也會被判定通過——這正是這次
review 抓到的問題（見上面第 4 點問題）。

E 段把「活著」與「有在做事」拆成兩個獨立的檢查項目，並各自編號，之後 G 段的報告表格會用到這兩個編號：

- **4.4（心跳檢查）**：裝置有沒有定時回報「我還活著」
- **4.5（功能檢查）**：這個範例存在的目的有沒有真的達成（例如「有沒有讀到感測器的值」「有沒有把
  資料轉發出去」）

這兩個編號的完整脈絡在母文件 `E2E-EXAMPLE-IMAGES-001`（也就是 `01-e2e-test-scenario-example-images.md`）
的「第 4 幕」：那份文件把「部署完之後怎麼驗證它真的在動」拆成四到五個檢查點依序編號 4.1、4.2……，
4.4 與 4.5 是其中最後兩個。這裡不重講整份文件，只講 4.4／4.5 這兩點怎麼改。

**功能檢查怎麼判定**：每個範例的目錄底下有一個 `e2e-expect.json` 檔案，裡面寫著「這個範例正常運作時，
log 裡應該出現哪一行文字」。腳本就拿這個當標準答案去比對裝置上實際印出的 log。如果比對不到，就標記
為 `NOT-SCORED`（無法判定是否成功），並把整體結論降為 `PARTIAL`（部分通過，不算完全過關）。

**其他改動**：

- 指令只需要打範例名稱，其餘（compose 檔路徑、容器名稱）都自動從 `e2e-expect.json` 讀出來，不會跟
  報告寫的對不上
- 新增 `--from-log` 選項，可以直接餵一份已經存在的 log 檔去測，不必真的連裝置——用來驗證判定標準
  本身寫得對不對
- 透過平台部署時，連線密碼等變數要放進「部署設定」（stack config，平台那邊記錄「要在裝置上跑
  什麼」的設定物件）的 `environment` 欄位一起送出——裝置端沒有 `.env` 檔可以讀，這是唯一能把值
  傳過去的管道

**如何驗測**：拿 `feature-transform-pipeline`（自帶模擬器、能自動判定的範例）已存在的 log 測，功能
檢查要判定為通過；拿 `modbus-wise4012`（需要外部硬體、目前沒接的範例）的 log 測，功能檢查要判定為
`NOT-SCORED` 並印出缺什麼（沒接 WISE-4012），整體結論為 `PARTIAL`。

---

### F — 新增一支 CI 自動檢查腳本

**這裡的「CI」指什麼**：程式碼推上去之後、正式合併之前，系統自動跑的一批檢查。跑起來只要幾秒鐘、
不需要真的裝置，所以放在最前面——檢查沒過就不用浪費時間往下編譯。

**這支腳本 `scripts/lint-examples.sh` 檢查什麼**：把 A~D 段修過的每一種錯誤，都寫成一條規則，
以後有人不小心改回錯的寫法，這裡就會擋下來。十條規則對應的錯誤類型：

1. compose 檔本身格式錯到連 `docker compose` 都解析不了（對照 A 段）
2. WedaNode 連線位址指向開發者自己的機器，不是 `127.0.0.1`（對照 B 段）
3. repo 裡出現真的密碼，或是像 `REPLACE_WITH_...` 這種忘記填的佔位字
4. 程式碼寫死用假的雲端（mock cloud），沒有讀設定值來決定要不要用（對照 D 段第 5 點）
5. 設定檔沒有烘進 image（對照 C 段第 2 點）
6. Dockerfile 用 `apt-get install pkg=1.2.3` 這種釘死版本號的寫法——套件倉庫之後可能把那個版本
   下架，build 會突然壞掉
7. 同一個 compose 檔裡把 `network_mode: host` 跟對外開 port 的寫法混在一起——這個組合部署到裝置上
   會啟動失敗
8. SubNode 的註冊資料沒有掛到裝置上的固定路徑（對照 C 段第 1 點的 `/app/.weda`）
9. 部署用的 compose 還在用 `build:` 建置，或把密碼寫成「一律傳、沒設就傳空字串」而不是「有設才傳」
   （對照 C 段第 1、4 點）
10. 範例目錄底下沒有 `e2e-expect.json`（對照 E 段）

檔名結尾是 `-sim` 的 compose 檔是刻意留給開發機用的模擬環境，本來就不打算部署到裝置，所以規則 7、8
會跳過它，並在檔案開頭寫明「這份不是拿去部署的」。

**如何驗測**：在已經修好的樹上跑一次，十條全部通過；接著針對每一條規則，故意把某個檔案改回錯誤的
寫法，確認腳本會抓到並回報失敗，改完再改回來（也就是「先證明抓得到，再證明抓不到就是真的沒問題」）。

---

### G — 把 15 份範例測試報告改成新的計分方式

**這裡改的是什麼**：`docs/wiki/en/07-testing/examples/` 底下那 15 份文件，每個範例一份，記錄它在
真實裝置上測試的結果。這些報告原本就有一張表格，列出每個範例通過了哪些檢查；E 段把「心跳檢查」
（表格裡的 4.4 欄）與「功能檢查」（4.5 欄）拆開之後，這些報告的表格也要跟著改，數字才會對得起來。

**改法**：

- 每份報告的表格從「4.4 一欄」變成「4.4（心跳）／4.5（功能）」兩欄
- 功能檢查（4.5）依 E 段的判定方式分四種情況。每個範例目錄下的 `e2e-expect.json` 裡都有一個
  `business.mode` 欄位，就是用來標記屬於哪一種：

| `business.mode` 的值 | 意思 | 對這個範例的結論 |
|---|---|---|
| `auto` | 有現成的成功紀錄可以比對，而且比對到了，功能真的有驗證過 | 通過 |
| `external` | 需要外部硬體或外部資料來源，但目前沒接，沒辦法驗證（不是失敗，是環境不齊全） | 最高只能是「部分通過」 |
| `manual` | 需要人工看數字判斷合不合理，還沒有人判讀過 | 最高只能是「部分通過」 |
| `skip` | 這次測試根本沒有跑這個範例，沒驗證 | 沒辦法算通過 |

- 拿同一批 2026-09-10 測試留下的 log 重新對照計分，**沒有重新跑一次測試**：
  15 個範例裡，6 個完全通過、7 個部分通過、1 個卡在缺硬體、1 個沒跑。舊報告寫的是「11 個完全通過」，
  差的那 5 個原本就只有心跳合格、功能從來沒真的驗證過（例如：範例訂閱了某個訊息通道，但從頭到尾
  沒有任何資料真的傳進來）
- 有 3 個範例的「心跳」那格改標成「沒有紀錄可查」而不是「失敗」：那 3 份報告當初只保留了整份 log
  裡的一小段摘錄，摘錄裡剛好沒有心跳那行，但這不代表心跳真的沒發生——原始 log 有 80 幾行，並非
  只有摘錄那幾行

**如何驗測**：15 份報告都要有完整的九項檢查列表；範例索引頁、總覽報告、還有底下的驗收簽核表，這
三個地方的統計數字要一致（都是前面講的 6／7／1／1）；被判定「部分通過」的範例，結論欄位不能寫
「完全通過」。

---

### H — 更新使用文件

前面 A~G 段動了程式碼與 compose 檔，這一段是把文件跟上，不然照舊文件操作的人會卡在「怎麼跟改過的
東西對不起來」。

- **建置與部署的操作手冊**（SOP，即 `09-build-push-deploy-images.md`）：
  - 補上「透過 SSH 手動部署」這一節：範例目錄下現在有兩份 compose（開發用、部署用），差在哪裡；
    連線的帳號密碼位址等四個值可以怎麼覆蓋；部署完之後怎麼確認範例真的在動；以及「第一次部署」跟
    「重新部署」在 log 上長得不一樣（`SUBNODE_NAME` 這個設定值只有在第一次部署時才有意義，因為
    程式碼裡的 `GetOrRegisterDeviceIdAsync` 會直接沿用上次記住的裝置 ID，不會拿新設的名字去比對）
  - 補上「自己建置 image」這一節：範例的專案結構限制它不能單獨建置，一定要在整個 repo 的根目錄下
    建置；並列出四個建置時可以設定的環境變數，各自預防了什麼失敗情況
  - 說明部署設定（stack config）裡的 `environment` 欄位是拿來解析 compose 檔案裡 `${...}` 這種
    變數用的，而且大多數情況不需要特別去設定它
- **範例總覽頁**（`examples/README.md`）：把「本機開發時該用哪份 compose」跟「交付給客戶時該用
  哪份」分開講清楚；`.env.example` 檔案上也註明它是本機開發用的
- **業務簡報用的檢查清單**（`07-tot-30min-subnode-for-si.md`）：原本提醒簡報者「`systemcfg.json`
  裡有密碼，畫面上不要投影出來」——這句話已經過期，因為 C 段之後這個檔案裡已經沒有密碼了，所以拿掉

**如何驗測**：文件裡出現的每一條指令，都要是真的在裝置上跑過、確認可行的那一條，不能是憑印象寫的。

---

## 3. 相依順序

```
A ┐
B ┤  三項彼此獨立
C ┘
    C ──> D（模板比照範例的形狀）
    C ──> E ──> G（腳本先改，報告才有結果可寫）
    A~D ─────> F（規則固化成檢查，前面沒修完只會一直紅）
    全部 ──> H
```

每個工作項各自 commit，訊息語言依 `git log` 現有慣例（英文 title + 英文 body）。

---

## 4. 已知被這份計畫改變的結論

- 統計由 **11 PASS · 2 PARTIAL · 1 BLOCKED · 1 SKIP** 變成 **6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP**。
  `mqtt-image-chunked`、`mqtt-isensing-wise4012`、`vision-object-detection`、`daq-proxy`、`cfx-endpoint`
  五個從 PASS 降為 PARTIAL：它們只證明了訂閱建立或只有心跳，沒有任何業務資料流過
- 上面那 5 個「部分通過」的範例，現在都卡在同一個原因：只證明訂閱建立，沒有真的資料流進來，
  因為沒有東西在對面發資料。之後找人把發送端接上、跑出一次成功的資料，就能把 `e2e-expect.json`
  的 `business.mode` 從 `external`（目前的「還沒有成功樣本可比對」）改成 `auto`（有樣本可以自動
  判定），下次重測就能真的判定通過或失敗，不用再靠人工。`cfx-endpoint` 這一個已經有現成工具可以
  用：`tools/cfx-replay` 會把錄好的 CFX 資料重播到同一個 broker 上，模擬真的有資料進來
  （`docker compose --profile replay up`）
- 既有的 `docker-compose.yml` 全數保留，本機 `docker compose up --build` 的流程不受影響

---

## 5. 變更紀錄

| 日期 | 修訂 |
|---|---|
| 2026-09-10 | 初版 |
| 2026-09-14 | 密碼的處理由「image 留空、部署時注入」改為「build 時寫進 image」，交付包與打包腳本納入範圍；全文改寫為與實作一致 |
