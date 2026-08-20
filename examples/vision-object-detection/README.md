# Vision Object-Detection SubNode

A WEDA SubNode that integrates the **Advantech YOLO object-detection demo container**
([Advantech-YOLO-Vision-Applications](https://github.com/Advantech-Containers/Advantech-YOLO-Vision-Applications),
`cv_ready_to_deploy`). It subscribes to the CV container's MQTT telemetry, maps each
detection frame into scalar sensors, and reports them to WedaNode like any other device.

```
YOLO CV container ──MQTT──> Vision SubNode ──NATS──> WedaNode
  advantech/<id>/vision/*      (this example)         (cloud)
```

## How it works

The CV container publishes three retained/streamed topics under `advantech/<DEVICE_ID>/vision`
(see the [MQTT telemetry contract §6](https://github.com/Advantech-Containers/Advantech-YOLO-Vision-Applications/blob/cv_ready_to_deploy/docs/cv-container-reference.md#6-mqtt-telemetry-contract)):

| Topic | Retained | Payload |
|-------|----------|---------|
| `.../status` | yes | `"online"` / `"offline"` (LWT) — logged |
| `.../meta` | yes | model, source clip, thresholds — logged |
| `.../detections` | no | 1 Hz: `objectCount`, `fps`, `classCounts`, per-object `confidence`/`bbox` |

`VisionDetectionParser` (a `IPubSubProtocolParser`) parses the `detections` JSON and, for
each configured sensor, emits one measure selected by the sensor's `Parameters.Field`:

| `Field` selector | Meaning |
|------------------|---------|
| `objectCount` | objects detected this frame |
| `fps` | inference frame rate |
| `frame` / `lap` | frame counter / loop number |
| `confidence.max` | highest detection confidence (0 when no detections) |
| `confidence.avg` | mean detection confidence (0 when no detections) |
| `classCount.<name>` | per-class count, e.g. `classCount.bottle` (0 when absent) |

`VisionDetectionDevice` extends `PubSubDeviceBase`, so the framework's telemetry pipeline
(SensorCache → interval sampling → batch send) is reused unchanged — no bespoke plumbing.

The default sensor set (`devicecfg.json`) exposes: `object_count`, `inference_fps`,
`confidence_max`, `confidence_avg`, and `bottle_count` (the demo clip is `OD_bottle_2.mp4`).
Add or remove sensors by editing `devicecfg.json` — set a new `Field` selector for each.

## Configuration

| File | Section | What to set |
|------|---------|-------------|
| `devicecfg.json` | `DeviceConfigs.VisionDetectionConfig.DeviceCommunication` | `BrokerUrl` (the CV broker, default `mqtt://localhost:1883`), `DeviceId` (`+` = any CV container, or a 12-char hex id to pin one) |
| `devicecfg.json` | `...Sensors[]` | which detection metrics to report |
| `systemcfg.json` | `WedaNode` | NATS `Url`, `AuthStrategy`, and credentials — **replace the placeholders** |

## Run

### Quick verify (built-in simulator, no CV container, no WedaNode server)

The app can play the CV container itself so you can watch the ingestion path end-to-end.
You need a local MQTT broker on `localhost:1883`:

```bash
# 1. Start a broker (either works)
mosquitto -p 1883 &                       # host binary
# docker run -d --name mqtt -p 1883:1883 eclipse-mosquitto:2   # or docker

# 2. Run the SubNode with the built-in vision simulator
dotnet run -- --simulate
```

You should see `CV model metadata: ...` followed by `Vision telemetry: objectCount = ...`
log lines once per second. (Full telemetry forwarding still requires a reachable WedaNode;
fill in `systemcfg.json` for that.)

### Against the real CV container

1. Deploy the Advantech YOLO stack (`weda-stack-od-demo.yml`) on the edge host — it brings
   up the inference container and its `eclipse-mosquitto` broker on `1883`.
2. Fill `systemcfg.json` with your WedaNode NATS URL + credentials.
3. Point `DeviceId` at the CV container's id (or leave `+`), then:

```bash
dotnet run
# or containerised, sharing the host network with the CV broker:
docker compose up --build
```

## Tests

```bash
dotnet test ../../tests/VisionObjectDetection.Tests
```

The tests drive `VisionDetectionParser` against the verbatim §6 reference payload —
field mapping, confidence aggregation, per-class counts, timestamp conversion, disabled
sensors, and malformed-payload handling.

## Notes & limitations

- **Wildcard `+` assumes one CV container per broker.** Sensors are keyed by `ResourceId`,
  so telemetry from multiple CV devices on the same broker would collide into one device.
  Pin `DeviceId` to a specific hex id when more than one CV container is present.
- **The CV broker is plaintext + anonymous MQTT (port 1883).** Acceptable on an isolated
  edge host; for anything beyond a bench, front it with TLS/auth per IEC 62443 SL2.
- The CV container is telemetry-only, so `ExecuteCommandAsync` returns "not supported".
