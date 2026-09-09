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

## 5. Deploy to the device

```bash
curl -s -X POST \
  "$BASE/api/v1/orgs/$ORG/stack-configs/$STACK_ID/revisions/$REV_ID:deploy" \
  -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d '{"deviceIds":["48b02dea8160"]}'
```

This changes what runs on real hardware — confirm the device list first.

## 6. Verify, in four steps

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

The local `docker-compose.yml` files mount `./weda-data:/app/.weda`; a stack deployed through
container-management must declare the equivalent or it works exactly once:

```yaml
    volumes:
      - weda-data:/app/.weda

volumes:
  weda-data:
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

## See also

- [`scripts/build-push-image.sh`](../../../../scripts/build-push-image.sh) — the build/push script
- [Examples index](../../../../examples/README.md) — "Building a container image"
- [Connect to WedaCore](04-connect-to-wedacore.md) — credentials and the WedaNode connection
