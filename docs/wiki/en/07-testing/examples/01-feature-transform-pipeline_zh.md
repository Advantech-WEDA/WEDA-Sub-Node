---
doc-id:    E2E-EXD-FEATURE_TRANSFORM_PIPELINE
title:     "feature-transform-pipeline — 端對端部署測試報告"
product:   "SubNode SDK（範例）"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images_zh.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-transform-pipeline — 端對端部署測試報告

| 結論 | 測試日期 | 受測裝置 | 環境 |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160`（ARM64, Ubuntu 20.04） | SIT — `weda-sit-k3s.weda.dev`，租戶 `central` |

> 方法、關卡定義、前置條件、負向案例與共用的缺陷登記表都在
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images_zh.md)**。這一頁只記錄**這個**範例
> 做了什麼。15 份報告的章節編號完全一致。

## 1. 受測對象

| 欄位 | 值 |
|---|---|
| **範例** | [`examples/feature-transform-pipeline`](../../../../../examples/feature-transform-pipeline/) |
| **compose 服務名** | `testdevice` |
| **image** | `harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:latest` |
| **傳輸協定** | Modbus TCP（內建模擬器在 `127.0.0.1:5020`） |
| **外部相依** | 無——範例自己啟動模擬器 |
| **使用的主機埠** | 2395（SubNode Web API）、5020（內建 Modbus 模擬器） |
| **部署時必須覆蓋的值** | 無 |
| **SubNode 名稱** | `TransformDemo-09090124` |
| **註冊到的 deviceId** | `355886333169762304` |

## 2. 關卡結果

四道關卡、九項檢查。**容器回報 `running` 只滿足 4.1，不代表其他任何一項。**
4.4 是框架心跳，SubNode 不管讀得到讀不到自己的裝置都會發；4.5 才是這個範例存在的目的。每一列都站在
自己的觀察上，引用自 §3 裝置本身的 log。

| 關卡 | 步驟 | 檢查 | 結果 | 證據 |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | 從 repo 根目錄建置 `linux/arm64` | ✅ | 重新建置 `linux/amd64,linux/arm64` |
| **Push** | E2E-EXD-2.2 | Harbor 上有多架構 manifest | ✅ | 多架構 manifest 已重新發佈至 Harbor |
| **Push** | E2E-EXD-2.3 | image 不掛任何檔案也能跑 | ✅ | 設定烘進 `/app`，不需要任何 bind mount |
| **Deploy** | E2E-EXD-3.2 | 裝置接受並套用該版本 | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | 容器啟動且沒有重啟迴圈 | ✅ | `status=running restarts=0` |
| **Function** | E2E-EXD-4.2 | SubNode 連上**這台裝置的** WedaNode | ✅ | `NATS connection verified - RTT: 0.3242ms` |
| **Function** | E2E-EXD-4.3 | 註冊成自己的子裝置 | ✅ | `Found existing SubNode registration: DeviceName=TransformDemo-09090124, DeviceId=355886333169762304` |
| **Function** | E2E-EXD-4.4 | 心跳送達雲端 | ✅ | [00:58:10 INF] Reporting health: DeviceId=355886333169762304, IsHealthy=True, CpuUsage=45.… |
| **Function** | E2E-EXD-4.5 | 做到這個範例存在的目的 | ✅ | [00:58:10 INF]   [0] 25 -> 77 |

> **圖例**：✅ 通過 · ⚠️ 部分 · ❌ 失敗或未到達 · ⏭️ 未執行 · — 不適用。

## 3. 證據 log

逐字取自裝置上的 `docker logs weda-transform-pipeline`，每一行標註它證明哪一項檢查。
擷取於 2026-09-10，全文共 91 行。

```text
[4.1] [00:58:09 INF] Modbus TCP Simulator started successfully
[4.2] [00:58:09 INF] NATS connection verified - RTT: 0.3242ms
[4.2] [00:58:09 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [00:58:10 INF] Found existing SubNode registration: DeviceName=TransformDemo-09090124, DeviceId=355886333169762304
[4.3] [00:58:10 INF] Configuration uploaded successfully: DeviceId=355886333169762304, Status=applied
[4.4] [00:58:10 INF] Reporting health: DeviceId=355886333169762304, IsHealthy=True, CpuUsage=45.05%, MemoryUsage=1.93%, Topic=eco1j.weda.355886333169762304.health, ReqSeqId=1
[4.5] [00:58:10 INF] temperature_sensor: 25
[4.5] [00:58:10 INF]   [0] 25 -> 77
[4.5] [00:58:15 INF] temperature_sensor: 25.030037
[4.5] [00:58:15 INF]   [0] 25.030037 -> 77.05406646728515
[4.5] [00:58:15 INF]   [0] 77.05406646728515 -> 25.03003692626953
[4.5] [00:58:20 INF]   [0] 24.66589736938477 -> 24.883808578372133
```

## 4. 缺陷

| # | 嚴重度 | 狀態 | 在**這裡**造成什麼 |
|---|---|---|---|
| **D-02** | High | 已修 | 設定沒有烘進 image，部署出去的 stack 啟動時沒有設定檔 |
| **D-11** | Low | 已修 | self-contained 發佈疊在 `dotnet/runtime` 上，多帶了一份沒用到的 runtime |
| **D-12** | Low | 已修 | build context 252.9 MB，其中 177 MB 是被 gitignore 的 log |

> 缺陷編號由整個測試套件共用，完整登記表在[母報告 §10](../01-e2e-test-scenario-example-images_zh.md)。

### 分析

標準案例，也仍然是整套測試裡最乾淨的一次。註冊 id **與 2026-09-09 那輪相同**，代表 `/app/.weda` 的
持久化撐過了一週、一次完整重建 image 與一次重新部署——D-02 的教訓有守住。

## 5. 本輪未驗證的項目

無。九項檢查全部執行並觀察到。

## 6. 如何重跑這個測試

```bash
# 1. 建置並發佈——build context 是 repo 根目錄，腳本會強制這一點
./scripts/build-push-image.sh feature-transform-pipeline

# 2. 透過 container-management 部署該版本並判定 4.1-4.5
./scripts/deploy-verify.sh feature-transform-pipeline

# 3. 到裝置上確認
ssh <device> 'docker logs --tail 40 weda-transform-pipeline'
```

## 7. 變更紀錄

| | |
|---|---|
| **本輪** | 2026-09-10 — EPC-R7300A1 `48b02dea8160`（ARM64, Ubuntu 20.04），SIT — `weda-sit-k3s.weda.dev`，租戶 `central` |
| **上一輪** | 2026-09-09 — 同一台裝置；變動見 §4／§5 |
| **範例修正** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **報告 schema 與本輪證據** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **驗證工具** | `deploy-verify.sh` — 部署該版本、等容器起來、分別判定 4.1-4.5 並擷取完整容器 log |
| **報告 schema** | v1.3 — 功能檢查拆成心跳（4.4）與範例自己的工作（4.5），判準來自 `examples/feature-transform-pipeline/e2e-expect.json` |
