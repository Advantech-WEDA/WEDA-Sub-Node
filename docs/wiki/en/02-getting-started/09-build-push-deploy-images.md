# Build, Push and Deploy an Example Image

The operating procedure for taking a SubNode example from source to a container running on a
WEDA edge device: build a multi-arch image, push it to Harbor, deploy it with the
container-management API, and verify it is actually working.

Verified end to end on an **EPC-R7300A1** (ARM64, Ubuntu 20.04) against `weda-sit-k3s.weda.dev`.

## 0. The one rule that trips everyone up

**The build context is the repository root, never the example directory.** Every example's
`.csproj` references `../../src/Weda.SubNode.*`, and the Dockerfile copies the whole tree, so:

```bash
cd examples/opcua-basic && docker build -t x .     # ✗ fails
```

```
MSBUILD : error MSB1003: Specify a project or solution file.
```

That error names neither the context nor the Dockerfile, so it reads as a broken example. Use
`scripts/build-push-image.sh`, which enforces the correct context, or pass `-f` from the root
yourself.

## 1. Match the image architecture to the device

Check before building — an amd64 image will not run on a Jetson-class device:

```bash
curl -s --cacert weda.pem -H "Authorization: Bearer $TOK" \
  "$BASE/api/v1/devices/$DEVICE/capabilities" | grep -i processorArchitecture
#   "processorArchitecture": "ARM64"
```

| Device | Arch | Build for |
|---|---|---|
| EPC-R7300 (Jetson Orin) | ARM64 | `linux/arm64` |
| x86 edge PC / dev host | AMD64 | `linux/amd64` |

## 2. Build and push

```bash
cd <repo-root>
docker login harbor.arfa.wise-paas.com

# multi-arch (default) — both architectures, one manifest list
./scripts/build-push-image.sh opcua-basic

# single arch, matching the target device
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh feature-transform-pipeline

# a tool rather than an example
PATH_PREFIX=tools PLATFORMS=linux/arm64 ./scripts/build-push-image.sh simulator-host

# local build, no push (single platform only — --load cannot take a manifest list)
PUSH=false PLATFORMS=linux/amd64 ./scripts/build-push-image.sh modbus-wise4012
```

Images land at `harbor.arfa.wise-paas.com/edge-coa/<name>:latest`. Override with `IMAGE_REPO`
and `IMAGE_TAG`. The script creates a `docker-container` buildx builder on first use (the default
`docker` driver cannot emit a manifest list) and installs `binfmt` when a build targets arm.

Confirm what actually landed:

```bash
docker buildx imagetools inspect harbor.arfa.wise-paas.com/edge-coa/<name>:latest
```

Set `WEDA_NATS_PASSWORD` in the environment when building an image to be handed on: it is written
into the configuration baked into the image, so the receiver needs only the image and a compose
file. `scripts/package-example.sh <example> <tag>` assembles exactly that pair.

The rest of this page deploys through container-management. To put the image on a device
yourself, over SSH and without the platform, jump to [§8](#8-deploying-by-hand-over-ssh).

### Images are self-contained by design

Each example Dockerfile bakes its four config files (`devicecfg`, `systemcfg`, `customcfg`,
`appsettings`) into `/app`. A stack deployed by container-management has no local files to
bind-mount, so an image that depends on mounts starts and immediately dies. Local
`docker compose` bind-mounts still override the baked copies, and any single value can be
overridden at run time by its environment variable:

```
DeviceConfig__DeviceConfigs__<Device>__DeviceCommunication__Password=...
SystemConfig__WedaNode__Password=...
```

> **Do not bake real credentials.** The committed `systemcfg.json` files carry placeholder NATS
> credentials. Anything baked into an image is readable by anyone who can pull it — pass real
> secrets through the stack's `environment` block instead.

## 3. Register the registry once per org

The device pulls from Harbor as itself, so the org needs a registry credential or every pull
fails with an auth error that surfaces only as a stuck deployment:

```bash
curl -s -X POST "$BASE/api/v1/orgs/$ORG/containers/registries" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  --data @registry.json      # never inline the password on the command line
```

Check for an existing one before creating a duplicate:

```bash
curl -s -H "Authorization: Bearer $TOK" "$BASE/api/v1/orgs/$ORG/containers/registries"
```

## 4. Create the stack config

`composeFileContent` is **base64-encoded** compose YAML, not plain text.

```bash
COMPOSE_B64=$(base64 -w0 stack-compose.yml)
curl -s -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d "{\"stackName\":\"my-stack\",\"type\":\"compose\",
       \"composeFileContent\":\"$COMPOSE_B64\",\"description\":\"...\"}"
```

The response carries `stackConfigId` and `stackRevisionId`. Every edit creates a new immutable
revision; you always deploy a specific revision, and rolling back is deploying an older one.

Use `network_mode: host` when the container must reach the device's own dmagent (NATS on
`127.0.0.1:4224`) or another container in the same stack over localhost.

### Supplying variables

A stack config carries an `environment` map beside the compose, and it is what `${...}` in the
compose resolves against - the `.env` equivalent for this path. So the same
`docker-compose.deploy.yml` works unmodified here and over SSH; only the source of the values
differs. Most deployments need nothing in it: the image carries its own WedaNode connection, and
the SubNode name defaults to one that is unique per device. Put
`SystemConfig__WedaNode__Password` here only to point an image at a different WedaNode.

```bash
COMPOSE_B64=$(base64 -w0 examples/<example>/docker-compose.deploy.yml)
curl -s -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d "{\"stackName\":\"my-stack\",\"type\":\"compose\",
       \"composeFileContent\":\"$COMPOSE_B64\",
       \"environment\":{\"SUBNODE_NAME\":\"MySubNodeName\"},
       \"description\":\"...\"}"
```

Reading a stack config back shows both, so the values are stored in the organisation rather than
on the device:

```json
{
  "stackName": "envkey-probe",
  "composeFileContent": "c2VydmljZXM6CiAgcHJvYmU6…",
  "environment": { "GREETING": "hello", "TZ": "UTC", "PROBE_TOKEN": "s3cr3t" },
  "version": 4
}
```

A credential put here is held by the stack config, not by a file on one device - a different
exposure from `.env`, and worth deciding deliberately rather than by default.

Keys are taken literally: `WEDA_NATS_PASSWORD` in the map is what `${WEDA_NATS_PASSWORD}` in the
compose picks up. The map is an interpolation source, not a blanket injection - a key the compose
never mentions does not reach the container.

The agent adds one of its own: **`EDGESYNC_EDGE_ID`**, the device id, unless the map already
defines that key ([`stack.go`](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/digitaltwin_device_mgmt_agent)
ensures it before deploying). Each deployment compose uses it to make the SubNode name unique per
device:

```yaml
- DeviceConfig__SubNode__Name=${SUBNODE_NAME:-TestDeviceCmd-${EDGESYNC_EDGE_ID:-local}}
```

| Deployed by | Name it registers under |
|---|---|
| container-management | `TestDeviceCmd-48b02dea8160` - the agent supplies the device id, so one stack config serves any number of devices |
| by hand with `SUBNODE_NAME` set | whatever you set |
| by hand with neither | `TestDeviceCmd-local` |

Only the first deployment uses the name at all - see [§8.4](#84-check-it).

## 5. Deploy to the device

```bash
curl -s -X POST \
  "$BASE/api/v1/orgs/$ORG/stack-configs/$STACK_ID/revisions/$REV_ID:deploy" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d '{"deviceIds":["48b02dea8160"]}'
```

This changes what runs on real hardware — confirm the device list first.

## 6. Verify, in five steps

Do not stop at "the API returned 200". Each step below proves something the previous one does not:

| # | Check | Proves |
|---|---|---|
| 1 | `GET /api/v1/orgs/$ORG/stack-configs/deployments?deviceIds=$DEVICE` → status reaches `deployed`/`running` | The device accepted and applied the revision |
| 2 | `GET /api/v1/devices/$DEVICE/docker/stacks` lists the stack with its containers `running` | The images pulled and the containers actually started |
| 3 | Device telemetry shows the new sensor values arriving | The SubNode inside the container is doing its job |
| 4 | `GET /api/v1/devices/$DEVICE/capabilities/sensors` lists the sensors the SubNode registered | The SubNode reached the WedaNode and registered its capability catalogue |

A container that is `running` proves only that the process has not exited — a SubNode that cannot
reach its device or its WedaNode stays up and logs errors forever. Steps 3 and 4 are what
distinguish "deployed" from "working".

## Gotchas found running this for real

Three things cost time on the first EPC-R7300 run; all three look like infrastructure faults and
are not.

**Persist `/app/.weda`, or the SubNode cannot re-register.** This is the one that bites twice.
`/app/.weda/subnode.registration.json` holds the accepted registration. Without a volume it is
destroyed every time the container is recreated, so the SubNode registers from scratch and
collides with its own earlier registration:

```
Failed to register SubNode <name> with Cloud: Code=409, Message=Device name already in use
```

Every example's `docker-compose.yml` mounts it through `WEDA_DATA_PATH`, defaulting to the
on-device convention `/opt/Advantech/weda/node/{containerServiceName}`, and a stack deployed
through container-management must declare the same or it works exactly once:

```yaml
    volumes:
      - ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service-name>}:/app/.weda
```

Set `WEDA_DATA_PATH` to relocate the state — a different disk, a shared mount, or a scratch
directory for local development:

```bash
WEDA_DATA_PATH=./weda-data docker compose up -d     # keep it beside the example instead
```

If you change where the state lives on a device that has already registered, move the existing
directory across first, or the SubNode re-registers and hits the 409 below:

```bash
docker run --rm -v <old-volume>:/from -v /opt/Advantech/weda/node/<service>:/to busybox cp -a /from/. /to/
```

With the volume in place, restarting the container resumes reporting under the same `deviceId`
with no re-registration at all.

**`409 Device name already in use`.** Same error, different cause: the name in `devicecfg.json` is
already taken on the org by an unrelated device. Override it per deployment:

```yaml
    environment:
      - DeviceConfig__SubNode__Name=MyDemo-<deviceId>
```

Note that a SubNode child device cannot always be cleaned up afterwards: `GET`, `:deactivate` and
`DELETE` on `/api/v1/devices/{subNodeId}` can all answer `404` for a device that the org listing
still returns, so a name consumed by a stale child registration may not be reclaimable.

**Some examples cannot report to the cloud at all.** `opcua-basic` hardcodes `.UseMockCloud()` in
`Program.cs`, so it logs telemetry locally and never reaches the WedaNode no matter how it is
configured. Check `Program.cs` for `UseMockCloud` before choosing an example to verify a cloud
round-trip with.

**Some examples already embed their simulator.** `feature-transform-pipeline` starts its own Modbus
TCP simulator on `127.0.0.1:5020`. Adding a separate `simulator-host` container to the stack takes
the port first, and the SubNode dies with `SocketException (98): Address already in use` — then
`restart: unless-stopped` crash-loops it, so the API keeps reporting the container as `running`.
Check the example's log for "Modbus Simulator started" before pairing it with a simulator.

**Deployment status lags the device, and can disagree with it outright.**
`…/stack-configs/deployments` can read `pending` with an empty `containers` list well after the
device has pulled, recreated and started the container. Worse, after changing the service name in
a stack the cloud reported `deployed` / `Stack is managed but not currently running` while the
device had no such compose project at all. In that state:

- `commands:start` fails with `Command execution failed: stack not found`
- re-deploying the same revision is answered `"skip"`, because the cloud believes it is already there

The way out is to delete the device's copy and deploy again, which comes back as `"add"`:

```bash
curl -X DELETE "$BASE/api/v1/devices/$DEVICE/docker/stacks?stackConfigId=$STACK_ID" -H "Authorization: Bearer $TOK"
# then POST …/revisions/$REV:deploy again
```

Always confirm against the device itself (`docker compose ls`, container logs) rather than
trusting the deployment record.

**Batch builds exhaust the Docker disk.** Building all the examples multi-arch in one pass carries
two full self-contained .NET runtimes per example through BuildKit. On the build host this filled
Docker's data volume (a separate mount from `/`) and the builds failed with
`no space left on device`, `ResourceExhausted`, and corrupt-NuGet errors that look like network
faults. Prune between builds:

```bash
docker buildx prune -f --keep-storage 2GB
```

## 7. Clean up

```bash
# remove one stack from the device
curl -s -X DELETE "$BASE/api/v1/devices/$DEVICE/docker/stacks?stackConfigId=$STACK_ID" \
  -H "Authorization: Bearer $TOK"
```

## 8. Deploying by hand, over SSH

Each example ships **two** compose files, and the difference is which machine builds the image:

| File | Image comes from | Config comes from | Use it |
|---|---|---|---|
| `docker-compose.yml` | `build:` — compiled from this checkout | the four JSON files, bind-mounted from the example directory | on a development machine with the repository |
| `docker-compose.deploy.yml` | `image:` — pulled from Harbor | baked into `/app` inside the image | on a device, which has no checkout and does not build |

`docker-compose.deploy.yml` is also the file to paste into a container-management stack config
(§4). Everything below is the same file driven by hand.

### 8.1 Copy the two files to the device

```bash
ssh <device> 'mkdir -p /opt/<example>-demo'
scp examples/<example>/docker-compose.deploy.yml examples/<example>/.env.example \
    <device>:/opt/<example>-demo/
```

### 8.2 Credentials, when they are needed at all

An image built by `build-push-image.sh` with `WEDA_NATS_PASSWORD` set in the environment carries
that password, so a device running it needs nothing else. Built without it, the image holds the
empty password the repository keeps, and the value has to come from the environment instead.

The compose entry is a bare key - `- SystemConfig__WedaNode__Password` - so it is passed through
only when something actually defines it. That matters: an entry written
`=${WEDA_NATS_PASSWORD:-}` expands to an empty string when nothing is set, and an empty
environment variable beats the JSON file, wiping the password the image was built with. It shows
up as `Authorization Violation` repeating in the log.

To point an image at a different WedaNode, write the values beside the compose file:

```bash
cd /opt/<example>-demo
read -rsp 'WedaNode NATS password: ' PW && \
  printf 'SystemConfig__WedaNode__Password=%s\n' "$PW" > .env && \
  unset PW && chmod 600 .env
```

The file has to be readable by the user that runs `docker compose`, which under `/opt` usually
means not creating it with `sudo`.

Four values can be overridden this way:

| Variable | Comes from the image as | Override when |
|---|---|---|
| `SystemConfig__WedaNode__Password` | whatever it was built with | that WedaNode uses a different one |
| `SystemConfig__WedaNode__Url` | `127.0.0.1:4224` | the WedaNode is not this device's own |
| `SystemConfig__WedaNode__Username` | `advantech_nats` | that node uses a different account |
| `SystemConfig__WedaNode__AuthStrategy` | `UserPassword` | using `Token`, `CredFile` or `TlsCert` — see [Connect to WedaCore](04-connect-to-wedacore.md) |

| Situation | How |
|---|---|
| By hand, persisted across container recreation | `.env` beside the compose file, as above |
| One-off, nothing written to disk | `SystemConfig__WedaNode__Password=… docker compose -f docker-compose.deploy.yml up -d` — repeated after every `down` |
| Automated deployment | `--env-file /path/to/secrets.env`, placed by the deploying system |
| Through container-management | the stack config's `environment` map — see §4 |

A wrong or missing password is not blocked at the compose layer. The container starts and reports
the authentication failure in its own log, which is where that error belongs — and `down`, `ps`
and `logs` keep working regardless.

### 8.3 Start it

```bash
cd /opt/<example>-demo
docker compose -f docker-compose.deploy.yml up -d
```

`pull_policy: always` fetches the current `:latest`, so no separate `docker pull` is needed.

### 8.4 Check it

```bash
docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' <container>
docker logs <container> 2>&1 | grep -E \
  "Registering SubNode|Found existing SubNode|NATS connection verified|NATS service discovered|Configuration uploaded|Reporting health"
```

`<container>` is the `container_name` in that example's deploy compose, and is also recorded in
`examples/<example>/e2e-expect.json` together with what that example should be seen doing.

| Line | Means |
|---|---|
| `Registering SubNode <name> with Cloud` | **first deployment** — `/app/.weda` was empty, so it registered under `SUBNODE_NAME` |
| `Found existing SubNode registration` | **repeat deployment** — it reused the cached identity |
| `NATS connection verified` + `NATS service discovered: … WedaNodeId=<deviceId>` | it reached *this* device's WedaNode, not a developer's machine |
| `Configuration uploaded successfully` | it registered as its own child device |
| `Reporting health: … IsHealthy=True` | the heartbeat is flowing |

**`SUBNODE_NAME` matters only on the first deployment.** Once
`/app/.weda/subnode.registration.json` exists, the cached DeviceId is reused and the configured
name is not compared against it, so renaming an already-registered SubNode this way has no
effect — the log line even prints the new name beside the old id. Set the name anyway: whether
`.weda` survived is not something you want to have to know in advance, and on a genuine first
deployment a name already used in that organisation is refused with
`409 Device name already in use`.

### 8.5 Overriding more than the credentials

Environment variables reach any single field — `DeviceConfig__DeviceConfigs__<name>__<field>`
addresses a nested value the same way — so a handful of differences do not need files. When a
device genuinely needs *different content* (other sensors, another polling interval), bind-mount
the file over the baked one instead:

```yaml
    volumes:
      - /opt/<example>-demo/devicecfg.json:/app/devicecfg.json:ro
      - ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service>}:/app/.weda
```

Copy the file you want to change out of the example directory, edit it on the device, and add the
mount. `system-agent` is the shipped precedent: it does not bake its configuration at all and
mounts all four files from `/opt/Advantech/system-monitor/`, because an installer owns that
content per device.

Two consequences to accept before doing it:

- **The mount wins for good.** Pulling a newer image changes nothing about that file, silently.
  The device keeps running the copy it has, and nothing reports the divergence.
- **It only works where you can place files.** A stack config carries a compose document and an
  environment map - no file payload - so a deployment through container-management cannot put the
  file there; the path has to exist on the device already.

That is why credentials go through the environment and content stays baked: the environment
reaches both deployment paths, files reach only one.

### 8.6 Stop it

```bash
cd /opt/<example>-demo
docker compose -f docker-compose.deploy.yml down
```

`/app/.weda` lives on the host under `WEDA_DATA_PATH` and survives this. Deleting it makes the
next start register afresh.

## 9. Building the image yourself

The other way an example is handed on: with this repository rather than a published image. Use it
when the image has to carry your own WedaNode credentials, or when you have changed the example.

An example cannot be built on its own - its `.csproj` reaches the SDK as `../../src/Weda.SubNode.*`,
so the whole repository is the build context and the project has to sit two levels below its root.
That is why every build here runs from the repository root, and why `build-push-image.sh` enforces
it.

### 9.1 Build and publish

```bash
cd <repo-root>
docker login <your-registry>

IMAGE_REPO=<your-registry>/<namespace>/opcua-basic \
WEDA_NATS_PASSWORD='<the password of the WedaNode this image will talk to>' \
PLATFORMS=linux/arm64 \
  ./scripts/build-push-image.sh opcua-basic
```

| Variable | Why |
|---|---|
| `IMAGE_REPO` | where you can pull from; without it the script pushes to the Advantech registry, which you may not be able to write to |
| `WEDA_NATS_PASSWORD` | written into the configuration baked into the image, so the device needs nothing else. Omit it and the image ships an empty password, which the compose can supply instead |
| `PLATFORMS` | match the device: `linux/arm64` for an EPC-R7300, `linux/amd64` for an x86 box. The default builds both |
| `IMAGE_TAG` | defaults to a timestamp, `YYYYMMDD-HHMMSS`. Pin one you can quote in a problem report |

The script prints the manifest it pushed. Check the architecture list before going near a device:
an image without the device's architecture fails the pull with a manifest mismatch, which reads
like a registry problem.

### 9.2 Point the compose at it

`examples/<name>/docker-compose.deploy.yml` carries one literal image line:

```yaml
    image: harbor.arfa.wise-paas.com/edge-coa/opcua-basic:latest
```

Change it to what you pushed. Everything else in the file already suits a device.

### 9.3 From there it is the same

Deploy it as in [§8](#8-deploying-by-hand-over-ssh) for SSH, or [§4](#4-create-the-stack-config)
through container-management. The checks in [§8.4](#84-check-it) do not change.

### 9.4 Your own project rather than an example

`dotnet new subnode` and `dotnet new wedabuilder` produce a project in the same shape: the
configuration is baked, `PROJECT_DIR` carries where the project sits, and the same
`WEDA_NATS_PASSWORD` build argument applies. Scaffold it two levels below the repository root -
`apps/<name>` or `examples/<name>` - and pass that path:

```bash
dotnet new wedabuilder -n MyDevice -o apps/my-device
docker build -f apps/my-device/Dockerfile --build-arg PROJECT_DIR=apps/my-device \
  --build-arg WEDA_NATS_PASSWORD='<password>' -t <your-registry>/my-device:1.0.0 .
```

## See also

- [`scripts/build-push-image.sh`](../../../../scripts/build-push-image.sh) — the build/push script
- [Examples index](../../../../examples/README.md) — "Building a container image"
- [Connect to WedaCore](04-connect-to-wedacore.md) — credentials and the WedaNode connection
