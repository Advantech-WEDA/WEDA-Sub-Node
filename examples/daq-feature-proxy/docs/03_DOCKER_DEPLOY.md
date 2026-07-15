# Docker Deployment and Multi-platform Support

---

## Deployment Methods

### Method A: Local Build and Push to Harbor Registry

**Prerequisites**:
- Docker Buildx (for multi-platform builds)
- Harbor Registry access permissions

**Multi-platform Build Method**:

```bash
# Must execute from repository root — Dockerfile needs access to dependencies in src/
cd /home/advantech/vincent/edge_subnode

# (First time only) Setup Docker Buildx environment
docker run --privileged --rm tonistiigi/binfmt --install arm64,amd64
docker buildx create --use --name multiarch-builder || docker buildx use multiarch-builder

# Build and push multi-platform images
docker buildx build \
  --platform linux/arm64,linux/amd64 \
  -f examples/daq-feature-proxy/Dockerfile \
  -t harbor.arfa.wise-paas.com/edge-coa/daq-feature-proxy:latest \
  -t harbor.arfa.wise-paas.com/edge-coa/daq-feature-proxy:$(git describe --tags --always) \
  --push .
```

**Key Notes**:
- Must execute build from the **repository root** — `Dockerfile` copies `src/` for the SDK project references
- Use `-f examples/daq-feature-proxy/Dockerfile` to specify the Dockerfile path; build context is root (`.`)
- `docker buildx` simultaneously builds for ARM64 and AMD64 and pushes to Harbor

### Method B: Local Build and Save as Tar Archive (Offline Testing)

**Applicable Scenarios**: Device cannot access Harbor Registry

```bash
# Must execute from repository root
cd /home/advantech/vincent/edge_subnode

# 1. Build single-platform image (e.g., ARM64)
docker buildx build \
  --platform linux/arm64 \
  -f examples/daq-feature-proxy/Dockerfile \
  -t daq-feature-proxy:latest \
  --load .

# 2. Save as Tar archive
docker save -o daq-feature-proxy-latest.tar daq-feature-proxy:latest
```

**Upload to Device**:

```bash
cd /home/advantech/vincent/edge_subnode/examples/daq-feature-proxy
scp daq-feature-proxy-latest.tar docker-compose.yml appsettings.json \
    systemcfg.json devicecfg.json customcfg.json ubuntu@192.168.1.100:~/
```

**Deploy on Device**:

```bash
# Load image
docker load -i ~/daq-feature-proxy-latest.tar

# Prepare deployment directory
mkdir -p /opt/daq-feature-proxy
mv ~/docker-compose.yml ~/appsettings.json ~/systemcfg.json \
   ~/devicecfg.json ~/customcfg.json /opt/daq-feature-proxy/

# Start container
cd /opt/daq-feature-proxy
docker compose up -d
```

---

## Docker Compose Configuration Reference

### docker-compose.yml Structure

```yaml
services:
  daq-feature-proxy:
    image: daq-feature-proxy:latest
    container_name: daq-feature-proxy
    network_mode: host        # Required for WEDA Node (NATS) communication
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "50m"
        max-file: "3"

    volumes:
      # Configuration files (read-only)
      - ./devicecfg.json:/app/devicecfg.json:ro
      - ./systemcfg.json:/app/systemcfg.json:ro
      - ./customcfg.json:/app/customcfg.json:ro
      - ./appsettings.json:/app/appsettings.json:ro

      # Persist device registration state
      - ./weda-data:/app/.weda
```

### Key Configuration Items

| Item | Description | Notes |
|------|-------------|-------|
| `network_mode: host` | Use host network stack | Required for WEDA Node (NATS) and PHM Service communication |
| `volumes` (config) | Bind-mount configuration files | Edit files on the host; restart container to apply changes |
| `volumes` (weda-data) | Persist device registration | Preserve across container restarts and upgrades |
| `restart: unless-stopped` | Auto-restart policy | Container restarts after crash or host reboot |

> **Note**: Unlike `daq-data-collector`, this service requires **no** `privileged` mode, no `pid: host`, no device mappings, and no system library mounts. It is a pure-software container.

---

## Deployment Steps Summary

### 1. Prepare Image

Choose one of the following methods:

**Method A** (from Harbor Registry):
```bash
docker pull harbor.arfa.wise-paas.com/edge-coa/daq-feature-proxy:latest
```

**Method B** (local build):
```bash
cd /home/advantech/vincent/edge_subnode
docker buildx build --platform linux/arm64 -f examples/daq-feature-proxy/Dockerfile -t daq-feature-proxy:latest --load .
```

### 2. Prepare Configuration Files

Create deployment directory and prepare configuration on device:

```bash
mkdir -p /opt/daq-feature-proxy
cd /opt/daq-feature-proxy

# Edit systemcfg.json — set NATS URL (same broker as daq-data-collector)
nano systemcfg.json

# Edit devicecfg.json — set DaqDataCollectorSubNodeName and PhmApiModelId
nano devicecfg.json
```

### 3. Start Container

```bash
docker compose up -d
```

### 4. Verify Running

```bash
# Check container status
docker compose ps

# View logs — success when seeing "PHM Feature Proxy initialized"
docker compose logs -f
```

### 5. Common Operations

```bash
# View real-time logs
docker compose logs -f daq-feature-proxy

# Stop container
docker compose stop

# Start container
docker compose start

# Restart container
docker compose restart

# Remove container
docker compose down

# Remove container and persisted registration data
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
2. Ensure image file is correct or network can reach Harbor Registry
3. Verify image: `docker images && docker inspect daq-feature-proxy:latest`

### Container Startup Failed

**Error Message**:
```
docker compose up -d
ERROR: Service 'daq-feature-proxy' failed to start
```

**Solutions**:
1. Check detailed logs: `docker compose logs`
2. Verify that `PhmApiModelId` and `DaqDataCollectorSubNodeName` are set in `devicecfg.json`
3. Test NATS connectivity: `telnet <nats-host> 4224`

### Container Starts But Proxy Does Not Process Messages

**Symptom**: Container is `Up` but no PHM inference logs appear.

**Check**:
1. Verify capability query succeeded in startup logs: look for `Capability resolved: ...N feature sensors mapped`
2. If `0 feature sensors mapped`, the DAQ sensor names don't match the convention table — see `02_OUTPUT_SENSORS.md`
3. Verify `daq-data-collector` is running and publishing telemetry

### Performance Issues (High CPU or Memory)

**Troubleshooting Steps**:
1. Check PHM Service response time — slow API responses increase per-message processing time
2. Check `PhmApiTimeoutMs` setting — lowering it reduces wait time on a slow/unavailable service
3. Monitor resource usage: `docker stats daq-feature-proxy`

---

## Advanced Configuration

### Running Multiple Proxy Instances

Multiple proxy instances can run against the same DAQ device. Each instance operates independently and calls the PHM Service separately. Ensure each instance has a distinct `SubNode.Name` in its `devicecfg.json`.

```yaml
services:
  proxy-model-a:
    image: daq-feature-proxy:latest
    container_name: daq-feature-proxy-model-a
    network_mode: host
    restart: unless-stopped
    volumes:
      - ./devicecfg-model-a.json:/app/devicecfg.json:ro
      - ./systemcfg.json:/app/systemcfg.json:ro
      - ./customcfg.json:/app/customcfg.json:ro
      - ./appsettings.json:/app/appsettings.json:ro
      - ./weda-data-a:/app/.weda

  proxy-model-b:
    image: daq-feature-proxy:latest
    container_name: daq-feature-proxy-model-b
    network_mode: host
    restart: unless-stopped
    volumes:
      - ./devicecfg-model-b.json:/app/devicecfg.json:ro
      - ./systemcfg.json:/app/systemcfg.json:ro
      - ./customcfg.json:/app/customcfg.json:ro
      - ./appsettings.json:/app/appsettings.json:ro
      - ./weda-data-b:/app/.weda
```

Each `devicecfg-model-*.json` should use a different `PhmApiModelId` and a unique `SubNode.Name`.
