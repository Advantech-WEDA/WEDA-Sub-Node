# Docker Deployment and Multi-platform Support

---

## Deployment Methods

### Method A: Automatic Build with Azure Pipelines (Recommended for Production)

Configuration file: `azure-pipelines.yml`

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
scp -r daq-data-collector-latest.tar docker-compose.yml appsettings.json systemcfg.json devicecfg.json customcfg.json ubuntu@192.168.1.100:~/
```

**Deploy on Device**:

```bash
# Load image
docker load -i ~/daq-data-collector-latest.tar

# Prepare deployment directory
mkdir -p /opt/daq-collector
mv ~/docker-compose.yml ~/appsettings.json ~/systemcfg.json ~/devicecfg.json ~/customcfg.json /opt/daq-collector/

# Start container
cd /opt/daq-collector
docker compose up -d
```

**Note**: To build multi-platform images, follow the steps in Method B for each architecture or use `docker buildx` to build

---

## Docker Compose Configuration Reference

### docker-compose.yml Structure

```yaml
services:
  daq-data-collector:
    image: harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
    container_name: daq-data-collector
    privileged: true          # Required for SUSI Driver hardware access
    pid: host
    network_mode: host        # Use host network for WEDA Node communication
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "50m"
        max-file: "3"

    # DAQ device character devices from host — required for container access to DAQNavi hardware nodes.
    # 1. Run this on the HOST first to see which daqN nodes actually exist:
    #      find /dev -maxdepth 1 -name 'daq*' | sort
    # 2. Uncomment the lines below that match the output (remove ones that don't exist,
    #    add more if your system exposes additional module indices).
    devices:
      # - /dev/daq0:/dev/daq0
      # - /dev/daq1:/dev/daq1
      # - /dev/daq2:/dev/daq2
      # - /dev/daq3:/dev/daq3
      # - /dev/daq255:/dev/daq255

    volumes:
      # Configuration files are baked into the image at build time.
      # Uncomment the lines below to override them with host files instead
      # (useful for editing without rebuilding the image, or per-device configs).
      # .json must be in same directory as docker-compose.yml
      # - ./devicecfg.json:/app/devicecfg.json
      # - ./systemcfg.json:/app/systemcfg.json
      # - ./customcfg.json:/app/customcfg.json
      # - ./appsettings.json:/app/appsettings.json

      # Persist device registration state
      - ./weda-data:/app/.weda

      # Advantech ARM64 SUSI Driver (host-mounted, read-only)
      - /etc/board:/etc/board:ro
      - /usr/lib/Advantech/:/usr/lib/Advantech/:ro
      - /lib/libSUSI-4.00.so:/lib/libSUSI-4.00.so:ro
      - /lib/libSUSI-4.00.so.1:/lib/libSUSI-4.00.so.1:ro
      - /lib/libSUSI-4.00.so.1.0.0:/lib/libSUSI-4.00.so.1.0.0:ro
      - /lib/libjansson.a:/lib/libjansson.a:ro
      - /lib/libjansson.so:/lib/libjansson.so:ro
      - /lib/libjansson.so.4:/lib/libjansson.so.4:ro
      - /lib/libjansson.so.4.11.0:/lib/libjansson.so.4.11.0:ro
      - /lib/libSusiIoT.so:/lib/libSusiIoT.so:ro
      - /lib/libSusiIoT.so.1.0.0:/lib/libSusiIoT.so.1.0.0:ro

      # Advantech DAQNavi resources
      - /home/advantech:/app/advantech
      - /var/lib/daq/:/var/lib/daq/
      - /opt/advantech/:/opt/advantech/
```

### Key Configuration Items

| Item | Description | Notes |
|------|-------------|-------|
| `network_mode: host` | Use host network stack | Required for WEDA Node (NATS) communication |
| `privileged: true` | Full device access | Required for SUSI Driver and DAQ character devices |
| `pid: host` | Share host PID namespace | Required for DAQNavi driver interaction |
| `devices` | DAQ character device mappings | Add or remove `/dev/daqN` entries to match connected hardware |
| `volumes` (config) | Bind-mount configuration files | Edit files on the host; restart container to apply |
| `volumes` (weda-data) | Persist device registration | Preserve across container restarts and upgrades |
| `restart: unless-stopped` | Auto-restart policy | Container restarts after crash or host reboot |

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
  # - /dev/daq3:/dev/daq3
  # - /dev/daq255:/dev/daq255
```

**Key Points**:
- Use `devices:` section in docker-compose.yml to map DAQ hardware devices
- Format: `<host-device>:<container-device>`
- To find available DAQ devices on your system, run:
  ```bash
  find /dev -maxdepth 1 -name 'daq*' | sort
  ```
- Uncomment and add only the DAQ devices that your application needs
- Container must run with appropriate permissions (see `privileged: true` in docker-compose.yml)

### Memory Limit Adjustment

Adjust memory based on sampling rate and collection interval:

| Sampling Rate | Collection Interval | Recommended Memory |
|---------------|---------------------|-------------------|
| 1000 Hz | 1 second | 256M |
| 2500 Hz | 1 second | 512M |
| 5000 Hz | 1 second | 1GB |
| 10000 Hz | 1 second | 2GB |

Add `mem_limit` under the service in `docker-compose.yml` to enforce the limit:

```yaml
services:
  daq-data-collector:
    image: harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
    mem_limit: 512m      # Adjust based on sampling rate — see table above
    ...
```

---

## Deployment Steps Summary

### 1. Prepare Image

Choose one of the following methods:

**Method A** (Recommended for production):
```bash
docker login harbor.arfa.wise-paas.com
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

# Edit systemcfg.json — set the WedaNode connection URL
nano systemcfg.json
# "WedaNode": { "Url": "192.168.1.100:4224" }

# Edit devicecfg.json — SubNode.Name, DaqModuleDeviceNumber, sampling parameters, feature enablement
nano devicecfg.json

# appsettings.json only controls logging (Serilog) and normally needs no changes
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

# Method A (pulled from Harbor): inspect the full registry-qualified tag
docker inspect harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest

# Method B/C (local build): inspect the local tag instead
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
3. Test connectivity to the port configured in `systemcfg.json`'s `WedaNode.Url`:
   ```bash
   # Example below uses 4224 — replace with your actual configured port
   telnet 192.168.1.100 4224
   ```

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

> **Note**: The app always reads a file literally named `devicecfg.json` inside the container — there is no `DEVICE_CONFIG` environment variable to select a different filename. To run multiple devices, give each container its own `devicecfg-deviceN.json` on the host and bind-mount it to the same `/app/devicecfg.json` path, as shown below. Each container also needs the other host mounts from the main `docker-compose.yml` (SUSI/DAQNavi library mounts, `devices:`, etc.), omitted here for brevity.

```yaml
version: '3.8'

services:
  daq-collector-1:
    image: harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
    container_name: daq-collector-1
    volumes:
      - ./devicecfg-device1.json:/app/devicecfg.json
      - ./systemcfg.json:/app/systemcfg.json
      - ./customcfg.json:/app/customcfg.json
      - ./appsettings.json:/app/appsettings.json
      - ./data-1:/app/.weda
    restart: unless-stopped

  daq-collector-2:
    image: harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
    container_name: daq-collector-2
    volumes:
      - ./devicecfg-device2.json:/app/devicecfg.json
      - ./systemcfg.json:/app/systemcfg.json
      - ./customcfg.json:/app/customcfg.json
      - ./appsettings.json:/app/appsettings.json
      - ./data-2:/app/.weda
    restart: unless-stopped
```

### Co-deployment with NATS Container

> **Note**: There is no `NATS_URL` environment variable the app reads — the connection target is `WedaNode.Url` in `systemcfg.json` (bind-mounted like the other config files below).
>
> **Caveat**: The main `docker-compose.yml` runs `daq-collector` with `network_mode: host` for hardware access, and a host-networked container cannot resolve other Compose services by name (`nats`) — it would need the host's actual IP:port instead. The example below only works if `daq-collector` uses the default bridge network (i.e., drops `network_mode: host`), which also means it loses direct hardware device access. This pattern is useful for testing WedaNode/NATS communication without hardware attached — not for a real DAQ hardware deployment.

```yaml
version: '3.8'

services:
  nats:
    image: nats:latest
    ports:
      - "4222:4222"
    restart: unless-stopped

  daq-collector:
    image: harbor.arfa.wise-paas.com/edge-coa/daq-data-collector:latest
    container_name: daq-collector
    depends_on:
      - nats
    volumes:
      - ./devicecfg.json:/app/devicecfg.json
      - ./systemcfg.json:/app/systemcfg.json   # set WedaNode.Url to "nats:4222" to reach the nats service by name
      - ./customcfg.json:/app/customcfg.json
      - ./appsettings.json:/app/appsettings.json
      - ./weda-data:/app/.weda
    restart: unless-stopped
```


