# SubNode SDK 範例

> 每個範例都是可執行的 SubNode 應用程式。挑**傳輸協定**與你要整合的設備相符的那一個，或挑**功能**
> 與你想學的東西相符的那一個。

名稱以你會拿來搜尋的軸線開頭，所以目錄列表本身就是索引：`modbus-*` 都講 Modbus、`mqtt-*` 都講 MQTT、
`feature-*` 教的是 SDK 的某個能力而不是某個協定。

## 依傳輸協定

有設備要接、而且知道它怎麼講話時，從這裡開始。

| 範例 | 傳輸協定 | 資料型別 | 什麼情況選它 |
|---|---|---|---|
| [`modbus-wise4012`](./modbus-wise4012/) | Modbus TCP | `integer`、`boolean` | 你剛接觸 SDK。最單純的完整裝置，全部手動接起來，沒有東西被藏起來。 |
| [`modbus-wise4012-builder`](./modbus-wise4012-builder/) | Modbus TCP | `integer`、`boolean` | 同一台裝置，改用 builder 託管，有依賴注入與自動生命週期。要照抄到正式環境就抄這個形狀。 |
| [`mqtt-isensing-wise4012`](./mqtt-isensing-wise4012/) | MQTT（iSensing） | `double`、`boolean` | 你的裝置用 MQTT 發送研華的 iSensing JSON。 |
| [`mqtt-image-chunked`](./mqtt-image-chunked/) | MQTT | `image/png` | 你要搬二進位酬載，並想了解大的值怎麼被切塊傳輸。 |
| [`opcua-basic`](./opcua-basic/) | OPC UA | `double` | 你的設備提供 OPC UA 伺服器。內含模擬器，沒有硬體也跑得起來。 |
| [`http-stock-quotes`](./http-stock-quotes/) | HTTP（輪詢） | `double`、`long` | 你要定時輪詢一個 REST API，並把欄位對應成感測器。 |
| [`http-air-quality`](./http-air-quality/) | HTTP（輪詢） | `application/json` | 你要輪詢 REST API，並回報整份文件而不是個別純量欄位。 |
| [`daq-collector`](./daq-collector/) | DAQ 串流 | `double` | 你有高頻串流來源，需要做視窗特徵萃取。 |
| [`daq-proxy`](./daq-proxy/) | SubNode 對 SubNode | `double`、`string` | 你需要一個 SubNode 去消費另一個的 telemetry。與 `daq-collector` 成對。 |

## 依 SDK 功能

傳輸協定已經解決、想學某個能力時，從這裡開始。這幾個剛好都建在 Modbus 上，但傳輸協定只是載體。

| 範例 | 教什麼 |
|---|---|
| [`feature-transform-pipeline`](./feature-transform-pipeline/) | 轉換與 DSP 濾波——單位換算、校正、平滑——夾在讀取與回報之間。 |
| [`feature-custom-commands`](./feature-custom-commands/) | 可由雲端呼叫的自訂指令：處理器、驗證器、回傳結果，以及指令如何出現在能力清單裡。 |
| [`feature-aggregation`](./feature-aggregation/) | 從多個來源衍生出一個感測器——由電壓與電流算出功率。 |

## 不是範例的目錄

| 目錄 | 它其實是什麼 |
|---|---|
| [`system-agent`](./system-agent/) | 出貨的產品元件——跑在每一台 WEDA Node 上的主機指標 SubNode。暫時放在這裡，但它不是拿來抄的樣本。 |
| [`robot`](./robot/) | 只有文件——ROS2／DDS 整合的設計筆記（topic 與 QoS、DDS 與 NATS 的比較、訊息函式庫、機群模式）。沒有程式碼，沒有東西可跑。 |

---

## 怎麼跑範例

每個範例都是同樣的四檔設定形狀：

| 檔案 | 存什麼 |
|---|---|
| `devicecfg.json` | 裝置、它的感測器，以及回報設定 |
| `systemcfg.json` | telemetry 送到哪裡（WedaNode 位址與憑證） |
| `customcfg.json` | 範例專屬的開關，包含要不要用 mock cloud（假的雲端，把資料記在本機、不接真正的
  WedaNode，本機測試方便但看不到真正的雲端連線） |
| `appsettings.json` | 日誌 |

### 一個範例裡的三份 compose

它們是照**角色**分的，不是照階段：

| 檔案 | 誰用 | 在哪裡用 | 做什麼 |
|---|---|---|---|
| `docker-compose.yml` | SI（系統整合商） | 自己的機器 | 從這份 checkout 現場編譯，並把四個 JSON 掛進容器。改 `devicecfg.json` → `docker compose up` → 看 log，本機不需要安裝 .NET |
| `docker-compose.deploy.yml` | SubNode Application 開發者（透過 container-management 或手動 SSH） | 裝置 | 拉現成 image、設定已經烘在裡面、除了狀態目錄什麼都不掛。裝置不編譯，也沒有那四個 JSON 可掛 |
| `delivery/<範例>-<tag>/docker-compose.yml` | 客戶 | 客戶自己的裝置 | 上一份的快照，image 釘死在某個 tag。由 `scripts/package-example.sh` 產生 |

本機有 .NET 的話，前兩份都不必用——直接跑最快：

```bash
cd examples/<範例名稱>
dotnet run
```

要改部署行為，改 `docker-compose.deploy.yml` 再重跑打包腳本；不要直接改 `delivery/` 底下的檔案，
下次重產會被蓋掉。細節見
[建置、推送並部署範例 image](../docs/wiki/en/02-getting-started/09-build-push-deploy-images_zh.md)。

### WedaNode 的密碼

版控裡存的是**空密碼**——那個值屬於你要連的那一台 WedaNode，不屬於範例。

- **本機開發**：把 `.env.example` 複製成 `.env` 並填值；docker compose 會自動讀 `.env`，而 `.env` 已被
  gitignore。
- **交付用的 image**：密碼在 build 時就寫進去，所以收到的人只需要 image 與
  `docker-compose.deploy.yml`。`scripts/package-example.sh` 就是把這兩樣組裝起來。

其他認證方式（Token、CredFile、TlsCert）見
[連線到 WedaCore](../docs/wiki/en/02-getting-started/04-connect-to-wedacore.md)。

### 註冊資料要留著

每個範例的 compose 都透過 `WEDA_DATA_PATH` 持久化 `/app/.weda`——那裡存的是已被接受的 SubNode 註冊與
錄製資料——預設值是裝置上的慣例 `/opt/Advantech/weda/node/{containerServiceName}`。想把狀態放在範例
旁邊就改它：

```bash
WEDA_DATA_PATH=./weda-data docker compose up -d
```

弄丟那個目錄會讓 SubNode 重新註冊並撞上自己先前的註冊（`409 Device name already in use`），所以請
把它當成狀態，不是快取。

## 命名慣例

新範例遵循同一條規則，讓目錄列表繼續是索引：

- **資料夾**用 kebab-case，以傳輸協定為前綴（`modbus-`、`mqtt-`、`http-`、`opcua-`、`daq-`）；當要教
  的東西與傳輸協定無關時，前綴用 `feature-`。
- **專案檔**是資料夾名稱的 PascalCase——`modbus-wise4012/ModbusWise4012.csproj`。不要加 `Example`
  後綴，目錄名已經說了。

## 接下來看哪裡

- [前置需求](../docs/wiki/en/02-getting-started/01-prerequisites.md) — 開發環境設定
- [從範例開始](../docs/wiki/en/02-getting-started/02-start-with-example.md) — 逐步導覽
- [用 JSON 設定](../docs/wiki/en/04-configuration/02-configuration-via-json.md) — 完整的感測器參考
