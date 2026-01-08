# Quick Start Guide

## Before You Start

### 前置要求
- 裝置已安裝 Docker Compose
- 網路可連接到 NATS 伺服器
- Advantech SUSI 驅動（欲使用相關功能則需要）

## Quick Start

### 1. 準備檔案

複製以下檔案範本並上傳到裝置上目錄：
- `docker-compose.yml`
- `appsettings.json`

以目錄 `/opt/system-agent` 為例：
```bash
mkdir -p /opt/system-agent
cd /opt/system-agent
# 使用 scp 或 rsync 或其他方式上傳檔案
```

### 2. 編輯配置

```bash
nano appsettings.json
```

**必須修改**：

1. **NATS 伺服器地址** - `Nats.Url`
   ```json
   "Nats": {
     "Url": "nats://192.168.1.100:4222"
   }
   ```

2. **裝置名稱** - `DeviceName`
   - **新裝置**：設定唯一名稱，例如 `system-agent-Device-01`
   - **已註冊裝置**：不要隨意變更 `weda-data/.weda` 目錄以保存現有註冊資訊

**可選調整 - Sensors（指標採集頻率）**：

位置：`DeviceConfigs.SystemAgentDeviceConfig.Sensors`

每個 Sensor 的 `Interval`（採集間隔時間，單位毫秒）：

```json
{
  "Sensors": [
    {
      "Name": "CPU",
      "Config": { "Interval": 1000 }  // 1秒採集一次
    },
    {
      "Name": "Memory", 
      "Config": { "Interval": 2000 }  // 2秒採集一次
    }
  ]
}
```

**常見調整建議**：

| 指標 | 預設值 | 建議調整 | 說明 |
|------|---------|---------|------|
| CPU | 500ms | 保持或改為 1000ms | 變化頻繁，不需要太頻繁採集 |
| Memory | 1000ms | 保持 | 較為穩定 |
| Disk | 5000ms | 保持或改為 10000ms | 變化較慢，低頻率即可 |
| Network | 1000ms | 保持 | 中等變化 |

**調整後重啟容器**（無需重新構建 image）：
```bash
docker compose down
docker compose up -d
```

### 3. 啟動容器

```bash
docker compose up -d
```

### 4. 驗證部署狀態

請參考下節「Check Installation Status」來檢查

### 5. 停止容器

```bash
docker compose down
```

---

## Check Installation Status

### 1. 檢查容器狀態

```bash
docker compose ps
```

**應該看到**：
```
NAME             IMAGE                 COMMAND                STATUS
system-agent   system-agent:latest "./SystemAgentDevi?? Up 10 seconds
```

- `STATUS` 應該顯示 `Up`
- 如果顯示 `Exited` 或 `Exit X`，表示容器啟動失敗

### 2. 檢查日誌

```bash
docker compose logs -f
```

**正常日誌（應該看到）**：

```
[OK] Configuration file is valid
[OK] Device configuration is valid
[INF] Using device 'system-agent-Device-01' configuration from appsettings.json
[INF] Starting Weda SubNode Application
[INF] Creating NATS connection to nats://192.168.1.100:4222 with auth strategy: UserPassword
[INF] NATS connection verified - RTT: 1.5274ms
[INF] Connected to Weda Cloud Service
[INF] Registering device system-agent-Device-01 with Cloud
[INF] Device registered: DeviceId=261404103115014144
[INF] Configuration validation passed: 30 sensors enabled
[INF] All connections established successfully
```

**持續運行後會看到**：
```
[INF] Found 2 network interfaces
[INF] Collected 2 network metrics
[INF] Sending telemetry: DeviceId=261404103115014144, MeasureCount=26
[INF] Telemetry sent successfully
```

### 3. 常見問題

**容器立即退出 (Exit 1, Exit 2, 等)**

```bash
docker compose logs
```

檢查日誌中的錯誤訊息，常見原因：
- `appsettings.json` 檔案不存在
- `Nats.Url` 設定錯誤
- NATS 伺服器無法連接

**連接 NATS 失敗**

```
[ERR] Failed to create NATS connection to nats://...
```

檢查：
- NATS 伺服器是否正在運行
- 網路連接是否正常
- 防火牆是否阻擋 NATS 端口（預設 4222）

**DeviceName 衝突 (Code 409)**

```
[ERR] Failed to register device: Code=409, Message=Device name already in use
```

**原因**：
`DeviceName` 已被其他裝置或雲端註冊使用

**解決方法**：
1. **修改 DeviceName**：在 `appsettings.json` 中設定新的唯一名稱
2. **保留註冊資料**：如果是同一裝置重新部署，請保留 `weda-data/.weda` 目錄以使用現有 deviceId

**本地快取與雲端不一致**

本地存在 `.weda/registration.json` 但雲端已經沒有該裝置註冊資料（例如雲端已刪除該裝置）

**錯誤訊息**：
```
[ERR] Device registration validation failed
```

**系統行為**：
系統會偵測到註冊失效，自動刪除本地快取，然後使用 `appsettings.json` 中的 `DeviceName` 重新註冊

**無需手動介入**：
框架會自動處理此問題，確保程式正常執行

**沒有看到 telemetry 資料**

表示配置或連接有問題，請檢查日誌：

```bash
docker compose logs --tail 100
```

**容器偶爾崩潰 (Exit 139)**

容器運行一段時間後突然退出，日誌顯示 `Exit 139`（即 `Segmentation Fault`）：

```
container exited with code 139
```

**原因**：
這是 Advantech.Edge 庫的已知 Finalizer bug：當系統缺少 Advantech SUSI 硬體驅動時，雖然應用程式的初始化正常並繼續執行，但庫內部的 `SusiLib.Finalize()` 會在垃圾回收 (GC) 時拋出 `NullReferenceException`，由於 Finalizer 異常在 .NET GC 執行緒執行，無法被應用程式的 try-catch 捕獲，導致整個程式崩潰

**解決方法**：
- **這是已知現象**，不影響系統功能
- Docker Compose 設置了 `restart: unless-stopped`，容器會自動重啟並繼續運行
- 異常發生後容器會自動重啟，之後的運行次數會逐漸趨於穩定
- 若要完全避免此問題，可在目標裝置上安裝 Advantech SUSI 驅動（通常不需要）

**如何判斷是否為此問題**：
- 日誌中有 `Failed to initialize Advantech Device collector` 訊息
- 容器啟動後在短時間內崩潰一次，之後穩定運行
- 其他 collectors（CPU、Memory）正常採集並發送監測資料

**架構不支援的裝置上運行**

如果使用的 CPU 架構不匹配，會出現以下錯誤：

**ARM 鏡像在 x86 機器**：
```
standard_init_linux.go:228: exec user process caused: exec format error
```
或
```
exec /app/SystemAgentDevice: exec format error
```

**x86 鏡像在 ARM 機器上（使用 QEMU 模擬）**：
```
qemu-x86_64: Could not open '/lib64/ld-linux-x86-64.so.2': No such file or directory
```

**解決方法**：
使用 Harbor Registry 的 multi-platform 功能時，Docker 會自動選擇正確的架構，不會出現此錯誤。如果出現，請檢查：
1. 鏡像是否正確建置為 multi-platform（`docker buildx build --platform linux/arm64,linux/amd64`）
2. Harbor Registry 是否正確保存了 manifest list
3. 是否手動指定了錯誤的 `--platform` 參數

**缺少 SUSI 驅動的裝置上執行**

如果使用的非 Advantech 裝置（或缺少 SUSI 驅動），並在 `appsettings.json` 中配置了硬體感測器相關的 sensors（例如 `MetricType: "temperature"`、`"voltage"`、`"fanspeed"` 等）

**警告訊息**：
```
[WRN] Failed to initialize Advantech Device. Advantech-specific metrics will not be available.
[ERR] System.DllNotFoundException: Unable to load shared library 'libSUSI-4.00.so'
```

**行為**：
- 不會影響其他功能：程式會捕獲異常並繼續運行
- 通用指標正常運行：CPU、Memory、Disk、Network 等指標正常採集
- 硬體感測器指標無資料：`temperature`、`voltage`、`fanspeed`、`hwinfo`、`gpio`、`watchdog`、`thermalprotection` 等 MetricType 將無法採集資料

**建議**：
- 在非 Advantech 裝置上，建議移除 `appsettings.json` 中的硬體感測器相關的 sensors（設置 `Enabled: false`）
- 在非 Advantech 裝置上，這些 sensors 不會採集資料

**注意**：硬體感測器使用通用名稱（temperature、voltage 等），但當前實作依賴 Advantech SUSI 驅動，未來可能支援其他供應商的硬體。詳見 [METRIC_TYPES.md](METRIC_TYPES.md)

---

## 配置說明

### appsettings.json 完整配置

`appsettings.json` 必須與 `docker-compose.yml` 放在同一目錄

docker-compose.yml 中會將此檔案掛載到容器內：

```yaml
volumes:
  - ./appsettings.json:/app/appsettings.json
  - ./weda-data:/app/.weda  # 儲存裝置註冊資料
  - /etc/board:/etc/board:ro
  - /usr/lib/Advantech/:/usr/lib/Advantech/:ro
  - /lib/libSUSI-4.00.so:/lib/libSUSI-4.00.so:ro
  - /dev:/dev
```

### .weda 目錄 - 儲存裝置註冊

- **用途**：儲存裝置註冊資料 (deviceId 和憑證等)
- **首次啟動**：自動建立 `.weda` 目錄並註冊
- **已註冊裝置**：保留此目錄即可，程式會使用現有 deviceId
- **重新部署**：保留此目錄可避免 DeviceName 衝突（409 錯誤）


---

## 需要幫助

- 檢查 `docker compose logs` - 診斷問題
- 確認 `Nats.Url` 和網路連接正常  
- 檢查 [README.md](README.md) - 完整設定和配置說明

## 下一步

部署成功後可以：
- 在雲端平台查看裝置監測資料
- 參考 [METRIC_TYPES.md](METRIC_TYPES.md) 了解所有支援的 MetricType 和配置方法
- 參考 [README.md](README.md#configuration-validation) 了解完整配置和架構設計
- 參考 [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md) 了解多平台支援和如何新增平台

---