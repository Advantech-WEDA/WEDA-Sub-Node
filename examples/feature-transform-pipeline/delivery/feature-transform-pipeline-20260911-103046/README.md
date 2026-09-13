# Transform Pipeline SubNode

讀取一個溫度感測器，在回報給 WedaNode 之前先經過一段轉換流程：原始的攝氏讀值先換算成華氏，再做平滑。
它示範的是單位換算、校正、濾波這類處理該放在 SubNode 的哪個位置——夾在「讀裝置」與「回報」之間，兩邊都
不需要知道它的存在。

感測器是容器內建的模擬器，不必接任何硬體就看得到結果。

## 正常運作時會看到什麼

每五秒出現一次感測值，後面跟著轉換後的值：

```
[INF] temperature_sensor: 25
[INF]   [0] 25 -> 77
[INF] temperature_sensor: 25.030037
[INF]   [0] 25.030037 -> 77.05406646728515
[INF]   [0] 77.05406646728515 -> 25.03003692626953
```

`25 -> 77` 是攝氏轉華氏那一段，第三行是平滑那一段對前一個結果再處理。數值之間會有微幅飄動，因為模擬器
本身就會飄。

---

## 你需要什麼

- 一台裝得到 Docker、你連得進去的 WEDA 裝置，而且它的 WedaNode 正在跑
- 拉得到 `harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:20260911-103046` 的權限

設定檔（包含要連哪一台 WedaNode、用什麼帳密）都已經包在 image 裡，不需要另外準備。

## 怎麼跑

**1. 把 compose 檔放到裝置上**

```bash
ssh <device> 'mkdir -p /opt/feature-transform-pipeline'
scp docker-compose.yml <device>:/opt/feature-transform-pipeline/
```

裝置上只需要這一個檔案，這份 README 留在你手上就好。

**2. 確認 image 那一行**

`docker-compose.yml` 第一個服務寫的是：

```yaml
    image: harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:20260911-103046
```

如果你是從別的位址拉這個 image，改這一行。

**3. 啟動**

```bash
cd /opt/feature-transform-pipeline
docker compose up -d
```

**4. 確認它有在做事**

```bash
docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' weda-transform-pipeline
docker logs weda-transform-pipeline
```

第一行要是 `status=running restarts=0`，log 裡要看到：

| 這一行 | 代表 |
|---|---|
| `NATS connection verified` | 連上 WedaNode 了 |
| `NATS service discovered: … WedaNodeId=<裝置 id>` | 而且連到的是**這台**裝置的 WedaNode |
| `Registering SubNode … with Cloud` 或 `Found existing SubNode registration` | 註冊成功，或沿用先前註冊過的身分 |
| `Reporting health: … IsHealthy=True` | 心跳正常 |

再往下就會看到最上面那段的感測值。

**5. 停止**

```bash
cd /opt/feature-transform-pipeline
docker compose down
```

註冊資料存在裝置的 `/opt/Advantech/weda/node/` 底下，停掉不會消失，所以下次啟動會沿用同一個身分。
刪掉那個目錄就會重新註冊。

## 遇到問題

**log 一直出現 `Failed to connect to WedaNode` 與 `Authorization Violation`**

容器有起來，但 image 裡帶的帳密不是這台 WedaNode 要的。兩種解法：

- 在 compose 檔旁邊建一個 `.env`，指定這台 WedaNode 的密碼：

  ```
  SystemConfig__WedaNode__Password=<這台 WedaNode 的密碼>
  ```

  然後 `docker compose up -d` 重起。

**log 出現 `Code=409, Message=Device name already in use`**

SubNode 的名稱在同一個 WedaNode 底下必須唯一，而這個範例沒有指定時名稱會以 `-local` 結尾。代表已經有另一台
裝置用同一個名稱註冊過了。在 `.env` 裡指定一個自己的名稱再重起：

```
SUBNODE_NAME=TransformPipeline-Line3
```

**log 出現 `SocketException (98): Address already in use`**

SubNode 的 API 固定使用 2395 埠，所以一台裝置一次只能跑一個 SubNode。先把另一個停掉。
