# Docker Deployment and Multi-platform Support

---

## Deployment Methods

### Method A: Automatic Build with Azure Pipelines (Recommended for Production)

Configuration file: `.azure-pipelines.yml`

```bash
# Trigger build
git tag v1.0.0
git push origin v1.0.0
```

Images are automatically pushed to Harbor Registry:
```
harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:v1.0.0
harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
```

### Method B: Local Build and Push to Harbor Registry

**Prerequisites**:
- Docker Buildx (for multi-platform builds)
- Harbor Registry access permissions

**Multi-platform Build Method**:

```bash
# Must execute from repository root! Dockerfile needs access to dependencies in src/
cd /home/advantech/vincent/edge_subnode

# (First time only) Setup Docker Buildx environment
docker run --privileged --rm tonistiigi/binfmt --install arm64,amd64
docker buildx create --use --name multiarch-builder || docker buildx use multiarch-builder

# Build and push multi-platform images (Dockerfile internally runs dotnet publish)
# Note: -f parameter specifies Dockerfile path, build context is repository root (.)
docker buildx build \
  --platform linux/arm64,linux/amd64 \
  -f examples/daq-data-collector/Dockerfile \
  -t harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest \
  -t harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:$(git describe --tags --always) \
  --push .
```

**Key Steps**:
- **Must execute build from repository root**, as Dockerfile needs access to dependencies in `src/`
- Use `-f examples/daq-data-collector/Dockerfile` to specify Dockerfile path, build context is root directory (`.`)
- Use **multi-stage Dockerfile** with `TARGETARCH` to determine `dotnet publish` (no external publish needed)
- `docker buildx` simultaneously builds for ARM64 and AMD64 architectures and pushes to Harbor
- Image tags include `latest` and version based on Git tags

### Method C: Local Build and Save as Tar Archive (For Offline Testing)

**Applicable Scenarios**: Device cannot access Harbor Registry

```bash
# Linux development environment
# Must execute from repository root
cd /home/advantech/vincent/edge_subnode

# 1. Build single platform image (e.g., ARM64 version)
#    Note: Docker internally runs dotnet publish
docker buildx build \
  --platform linux/arm64 \
  -f examples/daq-data-collector/Dockerfile \
  -t daq-data-collector:latest \
  --load .

# 2. Save as Tar archive
docker save -o daq-data-collector-latest.tar daq-data-collector:latest
```

**Upload to Device**:

```bash
# Using scp
cd /home/advantech/vincent/edge_subnode/examples/daq-data-collector
scp -r daq-data-collector-latest.tar docker-compose.yml appsettings.json devicecfg.json ubuntu@192.168.1.100:~/
```

**Deploy on Device**:

```bash
# Load image
docker load -i ~/daq-data-collector-latest.tar

# Prepare deployment directory
mkdir -p /opt/daq-collector
mv ~/docker-compose.yml ~/appsettings.json ~/devicecfg.json /opt/daq-collector/

# Start container
cd /opt/daq-collector
docker compose up -d
```

**Note**: To build multi-platform images, follow the steps in Method B for each architecture or use `docker buildx` to build

---

## Docker Compose Configuration Reference

### docker-compose.yml Structure

```yaml
version: '3.8'

services:
  daq-collector:
    image: daq-data-collector:latest
    container_name: daq-collector
    
    # Network configuration
    network_mode: "host"              # Use host network for NATS communication
    
    # Volume mounts (device state persistence)
    volumes:
      - ./weda-data:/app/weda-data    # Save registration info
      - ./logs:/app/logs              # Logs directory
    
    # Environment variables
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - LOG_LEVEL=Information
    
    # Restart policy
    restart: unless-stopped
    
    # Resource limits
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 512M
        reservations:
          cpus: '1'
          memory: 256M
```

### Key Configuration Items

| Item | Description | Recommended |
|------|-------------|-------------|
| `network_mode` | Network mode | `host` (for NATS communication) |
| `volumes` | Mount volumes | Mount `weda-data` to save state |
| `restart` | Restart policy | `unless-stopped` (auto-restart) |
| `cpus` | CPU limit | `2` (adjust per hardware) |
| `memory` | Memory limit | `512M` (adjust per sampling rate) |

### Device Mappings for DAQ Devices

DAQ device mappings allow containers to access hardware DAQ devices on the host system.

```yaml
# Device mappings for DAQ devices (e.g., /dev/daq0, /dev/daq1, etc.)
# Uncomment and add specific DAQ devices as needed
# To discover available DAQ devices, run: find /dev -maxdepth 1 -name 'daq*' | sort
devices:
  # - /dev/daq0:/dev/daq0
  # - /dev/daq1:/dev/daq1
  # - /dev/daq2:/dev/daq2
```

**Key Points**:
- Use `devices:` section in docker-compose.yml to map DAQ hardware devices
- Format: `<host-device>:<container-device>`
- To find available DAQ devices on your system, run:
  ```bash
  find /dev -maxdepth 1 -name 'daq*' | sort
  ```
- Map only the DAQ devices that your application needs
- Container must run with appropriate permissions (see `privileged: true` in docker-compose.yml)

### Memory Limit Adjustment

Adjust memory based on sampling rate and collection interval:

| Sampling Rate | Collection Interval | Recommended Memory |
|---------------|---------------------|-------------------|
| 1000 Hz | 1 second | 256M |
| 2500 Hz | 1 second | 512M |
| 5000 Hz | 1 second | 1GB |
| 10000 Hz | 1 second | 2GB |

---

## Deployment Steps Summary

### 1. Prepare Image

Choose one of the following methods:

**Method A** (Recommended for production):
```bash
docker pull harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
```

**Method B** (Local build):
```bash
cd /home/advantech/vincent/edge_subnode
docker buildx build --platform linux/arm64 -f examples/daq-data-collector/Dockerfile -t daq-data-collector:latest --load .
```

### 2. Prepare Configuration Files

Create deployment directory and prepare configuration on device:

```bash
mkdir -p /opt/daq-collector
cd /opt/daq-collector

# Edit appsettings.json
nano appsettings.json
# Modify Nats.Url, for example:
# "Nats": { "Url": "nats://192.168.1.100:4222" }

# Edit devicecfg.json (optional)
nano devicecfg.json
# Adjust sampling parameters and feature enablement
```

### 3. Start Container

```bash
docker compose up -d
```

### 4. Verify Running

```bash
# Check container status
docker compose ps

# View logs
docker compose logs -f

# Success when seeing "Device initialized successfully"
```

### 5. Common Operations

```bash
# View real-time logs
docker compose logs -f daq-collector

# Stop container
docker compose stop

# Start container
docker compose start

# Restart container
docker compose restart

# Remove container
docker compose down

# Remove container and volumes
docker compose down -v
```

---

## Troubleshooting

### Image Load Failed

**Error Messages**:
```
Docker daemon not running
Unable to locate image
```

**Solutions**:
1. Ensure Docker is started
2. Ensure image file is correct or network can access Harbor
3. Check image format is correct

```bash
# Verify image
docker images
docker inspect daq-data-collector:latest
```

### Container Startup Failed

**Error Message**:
```
docker compose up -d
ERROR: Service 'daq-collector' failed to start
```

**Solutions**:
1. Check detailed logs
   ```bash
   docker compose logs
   ```
2. Verify configuration file format
3. Test NATS connection

### Performance Issues (High CPU or Memory)

**Troubleshooting Steps**:
1. Check sampling rate setting (lower it)
2. Adjust memory limit
3. Check logs to confirm feature extraction is normal

```bash
# Check resource usage
docker stats daq-collector
```

### Data Collection Stops

**Possible Causes**:
- DAQ hardware disconnected
- Driver software issue
- NATS connection lost

**Solutions**:
1. Check hardware connection
2. Restart container
3. Check logs for errors

```bash
docker compose restart
docker compose logs -f
```

---

## Advanced Configuration

### Multi-container Deployment

If deploying multiple DAQ collectors on same host:

```yaml
version: '3.8'

services:
  daq-collector-1:
    image: daq-data-collector:latest
    container_name: daq-collector-1
    volumes:
      - ./data-1:/app/weda-data
    environment:
      - DEVICE_CONFIG=devicecfg-device1.json
    restart: unless-stopped

  daq-collector-2:
    image: daq-data-collector:latest
    container_name: daq-collector-2
    volumes:
      - ./data-2:/app/weda-data
    environment:
      - DEVICE_CONFIG=devicecfg-device2.json
    restart: unless-stopped
```

### Co-deployment with NATS Container

```yaml
version: '3.8'

services:
  nats:
    image: nats:latest
    ports:
      - "4222:4222"
    restart: unless-stopped

  daq-collector:
    image: daq-data-collector:latest
    container_name: daq-collector
    depends_on:
      - nats
    environment:
      - NATS_URL=nats://nats:4222
    volumes:
      - ./weda-data:/app/weda-data
    restart: unless-stopped
```

```yaml
version: '3.8'

services:
  daq-collector:
    image: daq-data-collector:latest
    container_name: daq-collector
    
    # 网络配置
    network_mode: "host"              # 使用主机网络，便于访问 NATS
    
    # 卷挂载（设备状态持久化）
    volumes:
      - ./weda-data:/app/weda-data    # 保存注册信息
      - ./logs:/app/logs              # 日志目录
    
    # 环境变量
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - LOG_LEVEL=Information
    
    # 重启策略
    restart: unless-stopped
    
    # 资源限制
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 512M
        reservations:
          cpus: '1'
          memory: 256M
```

### 关键配置项

| 项目 | 说明 | 建议值 |
|------|------|---------|
| `network_mode` | 网络模式 | `host`（便于 NATS 通信）|
| `volumes` | 挂载卷 | 挂载 `weda-data` 保存状态 |
| `restart` | 重启策略 | `unless-stopped`（自动重启）|
| `cpus` | CPU 限制 | `2`（根据硬件调整）|
| `memory` | 内存限制 | `512M`（根据采样率调整）|

### 调整内存限制

根据采样率和采集间隔调整内存：

| 采样率 | 采集间隔 | 推荐内存 |
|--------|---------|---------|
| 1000 Hz | 1 秒 | 256M |
| 2500 Hz | 1 秒 | 512M |
| 5000 Hz | 1 秒 | 1GB |
| 10000 Hz | 1 秒 | 2GB |

---

## 部署步骤总结

### 1. 准备镜像

选择以下任一方式获取镜像：

**方式 A**（推荐生产环境）：
```bash
docker pull harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
```

**方式 B**（本地构建）：
```bash
cd /home/advantech/vincent/edge_subnode
docker buildx build --platform linux/arm64 -f examples/daq-data-collector/Dockerfile -t daq-data-collector:latest --load .
```

### 2. 准备配置文件

在设备上创建部署目录并准备配置：

```bash
mkdir -p /opt/daq-collector
cd /opt/daq-collector

# 编辑 appsettings.json
nano appsettings.json
# 修改 Nats.Url，例如：
# "Nats": { "Url": "nats://192.168.1.100:4222" }

# 编辑 devicecfg.json（可选）
nano devicecfg.json
# 调整采样参数和特征启用
```

### 3. 启动容器

```bash
docker compose up -d
```

### 4. 验证运行

```bash
# 查看容器状态
docker compose ps

# 查看日志
docker compose logs -f

# 看到 "Device initialized successfully" 即表示成功
```

### 5. 常见操作

```bash
# 查看实时日志
docker compose logs -f daq-collector

# 停止容器
docker compose stop

# 启动容器
docker compose start

# 重启容器
docker compose restart

# 删除容器
docker compose down

# 删除容器和卷
docker compose down -v
```

---

## 故障排查

### 镜像加载失败

**错误信息**：
```
Docker daemon not running
Unable to locate image
```

**解决方案**：
1. 确保 Docker 已启动
2. 确保镜像文件正确或网络可连接 Harbor
3. 检查镜像格式是否正确

```bash
# 验证镜像
docker images
docker inspect daq-data-collector:latest
```

### 容器启动失败

**错误信息**：
```
docker compose up -d
ERROR: Service 'daq-collector' failed to start
```

**解决方案**：
1. 查看详细日志
   ```bash
   docker compose logs
   ```
2. 检查配置文件格式
3. 验证 NATS 连接

### 性能问题（CPU 或内存过高）

**排查步骤**：
1. 检查采样率设置（降低采样率）
2. 调整内存限制
3. 查看日志确认特征提取是否正常

```bash
# 查看资源使用
docker stats daq-collector
```

### 数据采集停止

**可能原因**：
- DAQ 硬件断开连接
- 驱动程序问题
- NATS 连接丢失

**解决方案**：
1. 检查硬件连接
2. 重启容器
3. 查看日志确认错误

```bash
docker compose restart
docker compose logs -f
```

---

## 高级配置

### 多容器部署

如果需要在同一主机上部署多个 DAQ 收集器：

```yaml
version: '3.8'

services:
  daq-collector-1:
    image: daq-data-collector:latest
    container_name: daq-collector-1
    volumes:
      - ./data-1:/app/weda-data
    environment:
      - DEVICE_CONFIG=devicecfg-device1.json
    restart: unless-stopped

  daq-collector-2:
    image: daq-data-collector:latest
    container_name: daq-collector-2
    volumes:
      - ./data-2:/app/weda-data
    environment:
      - DEVICE_CONFIG=devicecfg-device2.json
    restart: unless-stopped
```

### 与 NATS 容器共同部署

```yaml
version: '3.8'

services:
  nats:
    image: nats:latest
    ports:
      - "4222:4222"
    restart: unless-stopped

  daq-collector:
    image: daq-data-collector:latest
    container_name: daq-collector
    depends_on:
      - nats
    environment:
      - NATS_URL=nats://nats:4222
    volumes:
      - ./weda-data:/app/weda-data
    restart: unless-stopped
```

