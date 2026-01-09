# Docker 部署與多平台支援說明

---

## 部署方法說明

### 方法 A：使用 Azure Pipelines 自動構建（推薦用於生產環境）

配置文件：`.azure-pipelines-docker.yml`

```bash
# 觸發建置
git tag v1.0.0
git push origin v1.0.0
```

鏡像將自動推送到 Harbor Registry：
```
harbor.arfa.wise-paas.com/edge-coa/system-agent:v1.0.0
harbor.arfa.wise-paas.com/edge-coa/system-agent:latest
```

### 方法 B：本地構建推送到 Harbor Registry

**前置要求**：
- Docker Buildx（支持多平台構建）
- Harbor Registry 訪問權限

**多平台構建方法**：

```bash
# 必須在 repository 目錄下執行！Dockerfile 需要訪問 src/ 下的依賴項目
cd D:\gitrepo\edge_subnode

# (僅第一次) 設置 Docker Buildx 環境
docker run --privileged --rm tonistiigi/binfmt --install arm64,amd64
docker buildx create --use --name multiarch-builder || docker buildx use multiarch-builder

# 構建並推送多平台鏡像（Dockerfile 內部會執行 dotnet publish）
# 注意：不需要使用 -f 參數來指定 Dockerfile，build context 是當前目錄的根目錄
docker buildx build \
  --platform linux/arm64,linux/amd64 \
  -f examples/system-agent/Dockerfile \
  -t harbor.arfa.wise-paas.com/edge-coa/system-agent:latest \
  -t harbor.arfa.wise-paas.com/edge-coa/system-agent:$(git describe --tags --always) \
  --push .
```

**關鍵步驟說明**：
- **必須在 repository 目錄執行 build**，因為 Dockerfile 需要訪問 `src/` 下的依賴項目
- 使用 `-f examples/system-agent/Dockerfile` 來指定 Dockerfile 路徑，build context 是根目錄 (`.`)
- 使用**多階段 Dockerfile** 內容內部使用 `TARGETARCH` 來決定 `dotnet publish`（不需要外部手動 publish）
- `docker buildx` 會同時為 ARM64 和 AMD64 構建並推送到 Harbor
- 鏡像標籤包含 `latest` 和基於 Git 標籤的版本

### 方法 C：本地構建並保存為 Tar 檔案（離線測試用）

**適用場景**：裝置無法訪問 Harbor Registry

```powershell
# Windows 開發環境
# 必須在 repository 目錄
cd D:\gitrepo\edge_subnode

# 1. 構建單一平台鏡像（例如 ARM64 版本）
#    注意：Docker 會在內部執行 dotnet publish
docker buildx build \
  --platform linux/arm64 \
  -f examples/system-agent/Dockerfile \
  -t system-agent:latest \
  --load .

# 2. 保存為 Tar 檔案
docker save -o system-agent-latest.tar system-agent:latest
```

**上傳到裝置**：

```powershell
# 使用 WSL/Git Bash
wsl bash -c "cd /mnt/d/gitrepo/edge_subnode/examples/system-agent && \
  sshpass -p '1234qwer' scp system-agent-latest.tar docker-compose.yml appsettings.json ubuntu@172.22.160.197:~/"
```

**在裝置上部署**：

```bash
# 載入鏡像
docker load -i ~/system-agent-latest.tar

# 準備部署目錄
mkdir -p /opt/system-agent
mv ~/docker-compose.yml ~/appsettings.json /opt/system-agent/

# 啟動容器
cd /opt/system-agent
docker compose up -d
```

**注意**：若要構建多平台鏡像，請參考方法 B 的步驟為每個架構發佈或使用 `docker buildx` 來構建

---

## 運行時配置管理

### 配置文件掛載

`docker-compose.yml` 中的 volume 設置讓 `appsettings.json` 可以在運行時修改而無需重建鏡像

```yaml
services:
  system-agent:
    image: system-agent:latest
    volumes:
      - ./appsettings.json:/app/appsettings.json
      - ./weda-data:/app/.weda
      - /etc/board:/etc/board:ro
      - /usr/lib/Advantech/:/usr/lib/Advantech/:ro
      - /lib/libSUSI-4.00.so:/lib/libSUSI-4.00.so:ro
      - /dev:/dev
```

### 修改配置後重啟

```bash
# 編輯配置
nano /opt/system-agent/appsettings.json

# 重啟容器（無需重建鏡像）
cd /opt/system-agent
docker compose down
docker compose up -d
```

詳細配置說明請參考 [QUICK_START.md#配置說明](01_QUICK_START.md#配置說明)

---

## 裝置設定管理

### .weda 目錄

儲存裝置註冊資料（deviceId 和憑證等）

```yaml
volumes:
  - ./weda-data:/app/.weda
```

**說明**：
- 保留 `.weda` 目錄可避免重複註冊時的 DeviceName 衝突（409 錯誤）
- 預設會在首次啟動時自動建立
- 清空此目錄會導致裝置視為新裝置

---

## 多平台支援說明

### 支援的平台

| 平台 | 架構 | .NET RID | 狀態|
|------|------|----------|------|
| linux/amd64 | x86_64 | linux-x64 | 完整支援 |
| linux/arm64 | aarch64 | linux-arm64 | 完整支援 |
| linux/arm/v7 | armhf | linux-arm | 完整支援 |

### 建置和部署支援流程

#### 建置階段（Build）

**方法**：Multi-stage Dockerfile + Docker Buildx

**流程**：
1. Docker Buildx 提供 `TARGETPLATFORM` 和 `TARGETARCH` 環境變數
2. Dockerfile 使用這些變數來決定對應的 .NET Runtime Identifier (RID)
3. 容器內部執行 `dotnet publish -r <RID>` 編譯對應平台的執行檔
4. 各平台分別建置並推送到 Harbor Registry

**優勢**：
- 不需要本機手動執行 `dotnet publish`
- 一次建置多個平台
- 適合 CI/CD 流程

#### 部署階段（Deployment）

**方法**：Harbor Registry Multi-platform Manifest

**流程**：
1. Harbor 會為同一標籤（例如 `system-agent:latest`）
2. 該標籤會包含多個架構的鏡像清單（manifest list）
3. 裝置執行 `docker pull` 時 Docker 會自動識別正確的平台
4. 不需要手動指定 `--platform`

**優勢**：
- 裝置端無需知道自己的架構
- 可以使用相同的 `docker-compose.yml` 適用各平台
- 簡化部署流程

### 如何新增平台

#### 步驟 1：確認 .NET 支援

參考 [.NET RID Catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)，確認目標平台對應的 RID

常見 RID 範例：
- `linux-x64` - Linux x86_64
- `linux-arm64` - Linux ARM64
- `linux-arm` - Linux ARM32
- `linux-musl-x64` - Alpine Linux x86_64
- `linux-musl-arm64` - Alpine Linux ARM64

#### 步驟 2：修改 Dockerfile

在 [Dockerfile](../Dockerfile) 的第 0-28 行的 RID 映射邏輯中新增新平台：

```dockerfile
RUN set -e; \
    echo "TARGETPLATFORM=${TARGETPLATFORM}, TARGETARCH=${TARGETARCH}"; \
    case "${TARGETPLATFORM}" in \
      linux/amd64|linux/x86_64)              echo "linux-x64"   > /rid.txt ;; \
      linux/arm64|linux/aarch64)             echo "linux-arm64" > /rid.txt ;; \
      linux/arm/v7|linux/armv7|linux/armhf)  echo "linux-arm"   > /rid.txt ;; \
      # 新增平台範例：
      linux/riscv64)                          echo "linux-riscv64" > /rid.txt ;; \
      *) \
        case "${TARGETARCH}" in \
          amd64|x86_64|X86|x86)  echo "linux-x64"   > /rid.txt ;; \
          arm64|aarch64)         echo "linux-arm64" > /rid.txt ;; \
          arm)                    echo "linux-arm"   > /rid.txt ;; \
          # 新增架構範例：
          riscv64)                echo "linux-riscv64" > /rid.txt ;; \
          *) echo "Unsupported platform/arch: TARGETPLATFORM=${TARGETPLATFORM}, TARGETARCH=${TARGETARCH}" >&2; exit 1 ;; \
        esac \
      ;; \
    esac
```

#### 步驟 3：修改 Azure Pipelines

在 [azure-pipelines.yml](../../../azure-pipelines.yml) 的 `docker buildx build` 命令中新增平台：

```yaml
- script: |
    docker buildx build \
      --platform linux/arm64,linux/amd64,linux/riscv64 \  # 新增 linux/riscv64
      -t harbor.arfa.wise-paas.com/edge-coa/system-agent:latest \
      --push .
  displayName: 'Build and push multi-platform image'
```

#### 步驟 4：測試建置

```bash
# 本地測試單一平台建置
docker buildx build --platform linux/riscv64 -t system-agent:test-riscv64 .

# 測試多平台建置
docker buildx build --platform linux/riscv64,linux/arm64,linux/amd64 -t system-agent:test-multi .
```

#### 步驟 5：驗證部署

```bash
# 在目標平台裝置上拉取鏡像
docker pull harbor.arfa.wise-paas.com/edge-coa/system-agent:latest

# 檢查鏡像架構
docker inspect harbor.arfa.wise-paas.com/edge-coa/system-agent:latest | grep Architecture

# 運行容器
docker run --rm system-agent:latest
```

---

## 最優解分析

目前的設計已經是最優解，因為：

1. **Docker Buildx 限制**：必須明確指定 `--platform` 參數，無法自動偵測
2. **.NET RID 映射**：TARGETARCH 和 .NET RID 名稱不一致（例如 `amd64` vs `linux-x64`），需要手動映射
3. **Multi-platform Manifest**：Harbor Registry 需要明確的平台清單才能建立 manifest list

### 架構不匹配的錯誤行為

#### ARM 鏡像在 x86 機器上執行

**錯誤訊息**：
```
standard_init_linux.go:228: exec user process caused: exec format error
```
或
```
exec /app/SystemAgentDevice: exec format error
```

**原因**：x86 CPU 無法執行 ARM 指令集

#### x86 鏡像在 ARM 機器上執行（使用 QEMU）

**錯誤訊息**：
```
qemu-x86_64: Could not open '/lib64/ld-linux-x86-64.so.2': No such file or directory
```

**原因**：雖然 QEMU 可以模擬 x86 指令，但需要對應的系統庫

#### 正確行為

使用 Harbor multi-platform manifest 時：
- Docker 會自動選擇正確的鏡像
- 不會出現架構不匹配錯誤
- 不需要手動指定 `docker pull` 命令的平台

---

## 已知限制

### QEMU + .NET 9.0 ARM64 Build 問題

在 Windows/macOS 開發環境使用 Docker Desktop + QEMU 進行 ARM64 cross-compilation build 時可能會遇到以下問題：

**現象**：
```
qemu: uncaught target signal 11 (Segmentation fault) - core dumped
System.InvalidCastException: Unable to cast object of type 'System.Reflection.Emit.OpCode' to type 'System.Globalization.CultureInfo'
```

**原因**：
- 這是 .NET 9.0 runtime 在 QEMU 模擬環境下的已知問題
- 特別在 `dotnet publish` 階段的 IL Compilation 時觸發
- 即使使用 `PublishReadyToRun` 仍可能發生

**解決方案**：
1. **推薦**：使用真實的 ARM64 機器進行 build（例如 Azure Pipelines 的 ARM64 runner）
2. **替代**：僅 build AMD64 鏡像，在目標 ARM64 裝置上直接 build
3. **CI/CD**：Azure Pipelines 使用真實的 ARM64 runner，不會遇到此問題

**Azure Pipelines 不會受影響**：
- CI 環境使用真實的 ARM64 硬體（非 QEMU 模擬）
- 不依賴跨架構模擬，可以可靠完成多平台 build

---

## 故障排查

| 問題 | 原因 | 解決方法 |
|------|------|---------|
| 鏡像推送失敗 | Harbor Registry 無法訪問 | 檢查網路連接和 Registry 認證 |
| 容器啟動失敗 | 配置文件缺失或格式錯誤| 驗證 `appsettings.json` 和 `docker-compose.yml` |
| DeviceName 衝突 (409) | 裝置名稱已被註冊 | 修改 `DeviceName` 或刪除 `.weda` 目錄 |
| NATS 連接失敗 | NATS 伺服器無法訪問| 檢查 `Nats.Url` 和網路路由 |
| SUSI 驅動缺失 | 缺少 Advantech SUSI 庫| 安裝驅動並檢查 volume 掛載 |
| 容器偶爾崩潰 (Exit 139) | Advantech.Edge 庫的 Finalizer bug | 已知現象，容器會自動重啟並繼續執行，詳見 QUICK_START 常見問題 |
| 多平台構建失敗| Docker Buildx 未設置或 Dockerfile 未正確處理 RID | 檢查 `docker buildx ls` 並重新創建 builder，檢查 Dockerfile 中 TARGETARCH 和 RID 的邏輯 |

**詳細診斷**：

```bash
# 查看容器日誌
docker compose logs -f

# 檢查鏡像大小和層級
docker images --no-trunc system-agent
docker history system-agent:latest

# 驗證 volume 掛載
docker inspect <container-id> | grep -A 20 "Mounts"
```

---

## 效能與可靠性

### 多平台鏡像的優勢

使用 Docker Buildx 和多階段 Dockerfile構建多平台鏡像（內部 publish，維護更簡單）：
- ARM64：Advantech 邊緣裝置
- AMD64：開發和測試環境

Harbor Registry 會自動為不同平台保存多個鏡像（manifest），pull 時自動選擇正確的平台

### 容器資源限制

可在 `docker-compose.yml` 中設置：

```yaml
services:
  system-agent:
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
```

---

## 參考資料

- [QUICK_START.md](01_QUICK_START.md) - 快速開始指南（適合技術使用者）
- [METRIC_TYPES.md](02_METRIC_TYPES.md) - MetricType 配置說明
- [README.md](README.md) - 完整設定和配置說明
- [TESTING_GUIDE.md](05_TESTING_GUIDE.md) - 部署驗證和測試
- [Docker Buildx Documentation](https://docs.docker.com/build/building/multi-platform/)
- [.NET Runtime Identifier Catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [Harbor Multi-platform Images](https://goharbor.io/docs/2.0.0/working-with-projects/working-with-images/managing-multi-platform-images/)
