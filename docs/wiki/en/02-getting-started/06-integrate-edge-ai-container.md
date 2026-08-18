---
sidebar_position: 6
sidebar_label: 'Recipe: Integrate an Edge AI Container'
hide_title: true
title: 'Integrate an Edge AI Container (Vision/Sound, REST + MQTT) | SubNode SDK'
keywords: ['SubNode', 'Edge AI', 'Vision Detection', 'Sound Detection', 'REST API', 'MQTT', 'System Integrator', 'Inference', 'Pub/Sub']
description: 'An SI recipe for bringing an Edge AI container (vision/sound detection) that exposes a RESTful status API and an MQTT status channel into WedaCore using the SubNode SDK.'
---

# Recipe: Integrate an Edge AI Container

> A step-by-step recipe for a System Integrator who already has an **Edge AI container that detects environment changes** -- for example **vision detection** (person/object/intrusion) or **sound detection** (glass-break, alarm, abnormal noise) -- and exposes its results two ways: a **RESTful API** (query on demand) and an **MQTT channel** (publish on detection). This guide shows how to surface those detections as WedaCore telemetry and alerts with the SubNode SDK.

## Overview

Your Edge AI container is, from SubNode's point of view, just another **data source**. SubNode does not care that the data comes from a vision or audio model -- it cares about the *transport* (HTTP or MQTT) and the *payload shape* (JSON, plus optionally a binary frame or audio clip). The SDK already ships two reusable patterns that match your two interfaces exactly:

| Your Edge AI interface | SubNode pattern | Base class | Reference example |
|------------------------|-----------------|-----------|-------------------|
| MQTT topic (container **publishes** status) | Pub/Sub (push) | `PubSubDeviceBase` | [`examples/mqtt-image-chunked`](https://github.com/Advantech-Containers/WEDA-Sub-Node/tree/main/examples/mqtt-image-chunked), `MqttISensingDevice` |
| RESTful API (SubNode **queries** status) | Request/Response (poll) | `RequestResponseDeviceBase` | [`examples/http-air-quality`](https://github.com/Advantech-Containers/WEDA-Sub-Node/tree/main/examples/http-air-quality) |

You can integrate via **either** interface, or both at once (e.g. MQTT for instant change events plus REST for a periodic health snapshot).

## What You'll Learn

After reading this article, you will be able to:

- Decide whether to use the MQTT or REST interface (or both)
- Model your Edge AI status fields as SubNode sensors
- Wire up an MQTT pub/sub device that receives published status
- Wire up an HTTP request/response device that polls the status API
- Turn an "environment change" into a WedaCore alert using thresholds

## Prerequisites

- Completed the [SI Integration Guide (Docker-only)](./05-si-integration-guide.md) or [Start with Example](./02-start-with-example.md)
- Your Edge AI container is reachable on the network: you know its **MQTT broker URL + topic** and/or its **HTTP endpoint URL**
- A sample of the JSON status payload your container emits

---

## Step 0 — Decide: MQTT, REST, or Both

| Question | Choose MQTT (Pub/Sub) | Choose REST (Request/Response) |
|----------|----------------------|--------------------------------|
| When do you need the data? | The instant the AI detects a change (event-driven) | On a fixed schedule (e.g. every 10s) |
| Who initiates? | The container pushes to a broker | SubNode pulls from the API |
| Is there already an MQTT broker? | Yes -- use it | Not required |
| Typical use | Change/alert events, low latency | Periodic snapshots, health checks |

> **Recommendation for vision/sound detection:** detections are *events* -- they happen at unpredictable moments when the model fires. That is a natural fit for **MQTT push**, so a detection reaches WedaCore the instant it occurs. Use **MQTT** as the primary channel, and optionally add a **REST** sensor polled at a slow interval as a heartbeat (e.g. "model alive", last-inference timestamp, rolling detection count). The two can coexist as two devices in the same SubNode.

---

## Step 1 — Model the Detection Result as Sensors

A vision or sound detector typically emits a **detection event**: *what* was detected, *how confident* the model is, *how many* instances, and sometimes a supporting **snapshot frame** or **audio clip**. Assume your container emits JSON like this (on MQTT, or as a REST response):

```json
{
  "zone": "entrance-1",
  "source": "vision",
  "detected": true,
  "object_class": "person",
  "object_count": 3,
  "confidence": 0.92,
  "inference_ms": 41,
  "frame": "iVBORw0KGgoAAAANSUg...",
  "ts": 1717200000000
}
```

A sound detector looks the same shape, just different fields:

```json
{
  "zone": "loading-dock",
  "source": "audio",
  "detected": true,
  "sound_class": "glass_break",
  "confidence": 0.87,
  "peak_db": 78.5,
  "clip": "UklGRiQAAABXQVZF...",
  "ts": 1717200000000
}
```

Each field you want in WedaCore becomes a **sensor**. The key insight for detection workloads: model the **class label** as a string, the **confidence** as a threshold-able number, and treat **`detected`** as the boolean alarm.

| Detection field | SensorGroup | Schema | Why |
|-----------------|-------------|--------|-----|
| `detected` | `DI` | `boolean` | The alarm bit -- fires the event |
| `object_class` / `sound_class` | `SYS` | `string` | What the model saw/heard (label) |
| `confidence` | `AI` | `double` | Model certainty -- **threshold this** to suppress false positives |
| `object_count` | `AI` | `integer` | How many instances |
| `peak_db` | `AI` | `double` | Sound level -- threshold-able |
| `inference_ms` | `SYS` | `integer` | Model health/latency |
| `frame` / `clip` | `SYS` | `image/png` / `application/octet-stream` | Optional evidence -- see [below](#capturing-the-snapshot-frame-or-audio-clip) |

> **Threshold the confidence, not just the flag.** Edge AI models fire on low-confidence guesses too. Reporting `confidence` as its own sensor and setting `UpperWarning`/`UpperCritical` lets WedaCore alert only when the model is *sure* -- see [Step 2](#step-2--turn-a-detection-into-an-alert).

See [Configuration via JSON](../04-configuration/02-configuration-via-json.md) and the [Configuration Reference](../04-configuration/02-configuration-via-json.md#sensor-field-reference) for the full field list.

---

## Option A — MQTT (Container Publishes Status)

This uses the **Pub/Sub** pattern: SubNode subscribes to your container's status topic and the framework caches each pushed value, then reports it on the sensor's interval. This is the same machinery behind the `mqtt-image-chunked` and `MqttISensingDevice` examples.

### A.1 Configure `devicecfg.json`

```json
{
  "SubNode": {
    "Name": "EdgeAI-EnvMonitor",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "YourCompany",
    "Model": "EdgeAI-Env-v1",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "EdgeAiMqtt": {
      "Enabled": true,
      "DeviceCommunication": {
        "BrokerUrl": "mqtt://127.0.0.1:1883",
        "ClientId": "subnode-edgeai"
      },
      "Dtdl": { "AutoGenEnabled": true },
      "Sensors": [
        {
          "Name": "detected",
          "SensorGroup": "DI",
          "Parameters": { "Topic": "edgeai/entrance-1/detections", "JsonPath": "$.detected" },
          "SensorInfo": { "DisplayName": "Detection Triggered", "Schema": "boolean" },
          "Report": { "Enabled": true, "Interval": 1000 }
        },
        {
          "Name": "object_class",
          "SensorGroup": "SYS",
          "Parameters": { "Topic": "edgeai/entrance-1/detections", "JsonPath": "$.object_class" },
          "SensorInfo": { "DisplayName": "Detected Class", "Schema": "string" },
          "Report": { "Enabled": true, "Interval": 1000 }
        },
        {
          "Name": "confidence",
          "SensorGroup": "AI",
          "Parameters": {
            "Topic": "edgeai/entrance-1/detections",
            "JsonPath": "$.confidence",
            "Thresholds": { "UpperWarning": 0.7, "UpperCritical": 0.9 }
          },
          "SensorInfo": { "DisplayName": "Detection Confidence", "Schema": "double" },
          "Report": { "Enabled": true, "Interval": 1000 }
        }
      ]
    }
  }
}
```

> All sensors can share one `Topic` (the container's status topic); each sensor's `JsonPath` selects its field from the published JSON. The broker connection comes from `DeviceCommunication.BrokerUrl` / `ClientId`.

### A.2 Create the Device

The fastest path is to **start from `examples/mqtt-image-chunked`** and replace its parser with a JSON-status parser. The device class is thin -- it inherits the Pub/Sub machinery and just logs/handles received data:

```csharp
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices;          // PubSubDeviceBase
using Weda.SubNode.Core.Communication.Mqtt; // MqttCommunication

public class EdgeAiMqttDevice : PubSubDeviceBase
{
    public EdgeAiMqttDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey]) { }

    public EdgeAiMqttDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += (_, e) =>
        {
            foreach (var m in e.Data)
                _logger.LogInformation("{ResourceId} = {Value}", m.ResourceId, m.Value);
        };
    }

    private static IPubSubProtocolParser CreateParser(
        IWedaApplicationContext context, DeviceConfiguration configuration)
    {
        var uri = new Uri(configuration.DeviceCommunication["BrokerUrl"].ToString()!);
        var clientId = configuration.DeviceCommunication["ClientId"].ToString();
        var mqtt = new MqttCommunication(uri.Host, uri.Port > 0 ? uri.Port : 1883, clientId,
                                         null, context.GetLogger<Weda.SubNode.Core.Communication.Common.CommunicationBase>());
        return new EdgeAiStatusParser(configuration, mqtt, context.GetLogger<EdgeAiStatusParser>());
    }
}
```

### A.3 Create the Parser (JSON → TelemetryMeasure)

The parser implements `IPubSubProtocolParser`: subscribe to the topic in `StartAsync`, and on each message extract every sensor's `JsonPath` field and raise `OnTelemetryReceived`. Use the SDK's `ISensingPubSubParser` (in `src/Weda.SubNode.Core/Protocols/ISensing/`) as your working reference -- it does exactly this for the ISensing JSON format. Your version simply maps your field names instead.

```csharp
using System.Text.Json;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

public class EdgeAiStatusParser : IPubSubProtocolParser
{
    private readonly DeviceConfiguration _config;
    private readonly IPubSub _mqtt;
    public ICommunication Communication => _mqtt;
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public EdgeAiStatusParser(DeviceConfiguration config, IPubSub mqtt, ILogger<EdgeAiStatusParser> logger)
    { _config = config; _mqtt = mqtt; /* store logger */ }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _mqtt.MessageReceived += OnMessage;                 // raw payload arrives here
        var topic = _config.Sensors[0].Parameters["Topic"].ToString()!;
        await _mqtt.SubscribeAsync(topic, ct);
    }

    private void OnMessage(string topic, byte[] payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var measures = new List<TelemetryMeasure>();
        foreach (var s in _config.Sensors)
        {
            // e.g. JsonPath "$.confidence" -> property "confidence"
            var field = s.Parameters["JsonPath"].ToString()!.TrimStart('$', '.');
            if (doc.RootElement.TryGetProperty(field, out var v))
                measures.Add(new TelemetryMeasure { ResourceId = s.ResourceId, Value = ReadValue(v), Timestamp = ts });
        }
        if (measures.Count > 0) OnTelemetryReceived?.Invoke(measures);
    }

    public Task StopAsync(CancellationToken ct = default) { _mqtt.MessageReceived -= OnMessage; return _mqtt.UnsubscribeAsync(_config.Sensors[0].Parameters["Topic"].ToString()!, ct); }
    public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand c, CancellationToken ct = default) => Task.FromResult<ErrorOr<object>>(new object());

    private static object ReadValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True or JsonValueKind.False => e.GetBoolean(),
        _ => e.ToString()
    };
}
```

> The exact `MessageReceived` member name may differ in your SDK version -- check `IPubSub` / `MqttCommunication`. The shape (subscribe → parse JSON → raise `OnTelemetryReceived`) is the contract that matters; the framework's `PubSubDeviceBase` handles caching and interval reporting from there.

### A.4 Register and Run

`Program.cs`:

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();                 // remove for production (see Connect to WedaCore)
builder.AddDevice<EdgeAiMqttDevice>("EdgeAiMqtt");
var app = builder.Build();
await app.RunAsync();
```

---

## Capturing the Snapshot Frame or Audio Clip

Detection events are often more useful with **evidence** -- the image frame the vision model fired on, or the audio snippet the sound model flagged. SubNode already handles binary telemetry; the `examples/mqtt-image-chunked` project does exactly this for images over MQTT.

Model the evidence as its own sensor with a binary `Schema`, and add a `chunking` transform so large payloads are split for low-bandwidth links:

```json
{
  "Name": "snapshot",
  "SensorGroup": "SYS",
  "Parameters": { "Topic": "edgeai/entrance-1/detections", "JsonPath": "$.frame" },
  "SensorInfo": { "DisplayName": "Detection Snapshot", "Schema": "image/png" },
  "Report": {
    "Enabled": true,
    "Interval": 1000,
    "TransformPipeline": [
      { "Type": "chunking", "Enabled": true, "Parameters": { "chunkSize": 128 } }
    ]
  }
}
```

| Evidence type | `Schema` | Notes |
|---------------|----------|-------|
| Image frame (PNG/JPEG, base64) | `image/png` / `image/jpeg` | Same path as `examples/mqtt-image-chunked` |
| Audio clip (WAV/PCM, base64) | `application/octet-stream` | Decode base64 in your parser before emitting |

> Sending a frame or clip on **every** detection can be heavy. Consider reporting evidence only on high-confidence detections, or at a slower interval than the scalar fields. Keep the lightweight fields (`detected`, `confidence`, `object_class`) on a fast interval for responsive alerting.

---

## Option B — REST (SubNode Polls the Status API)

This uses the **Request/Response** pattern: SubNode calls your container's HTTP endpoint on each sensor `Interval` and maps the JSON response to telemetry. This is the `http-air-quality` pattern -- copy that example and change the URL + field mapping.

### B.1 Configure `devicecfg.json`

```json
{
  "SubNode": {
    "Name": "EdgeAI-EnvMonitor",
    "SubNodeType": "CustomDevice"
  },
  "DeviceConfigs": {
    "EdgeAiRest": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 8080
      },
      "Dtdl": { "AutoGenEnabled": true },
      "Sensors": [
        {
          "Name": "detections_last_min",
          "SensorGroup": "AI",
          "Parameters": {
            "Endpoint": "/api/v1/status",
            "Method": "GET",
            "JsonPath": "$.detections_last_min",
            "Thresholds": { "UpperWarning": 5, "UpperCritical": 20 }
          },
          "SensorInfo": { "DisplayName": "Detections (last minute)", "Schema": "integer" },
          "Report": { "Enabled": true, "Interval": 10000 }
        }
      ]
    }
  }
}
```

### B.2 Reuse the air-quality-monitor structure

The `http-air-quality` example already implements the three pieces you need; copy its folder and adapt:

| File in `http-air-quality` | What to change for your Edge AI API |
|-------------------------------|-------------------------------------|
| `Communication/AirQualityClient.cs` | Point `BaseUrl` at your container; build the URL from `Host`/`Port` + `Endpoint`; drop the API key if not needed |
| `Communication/HttpCommunication.cs` | Usually unchanged -- it wraps the client behind `RequestResponseCommunicationBase` |
| `Protocols/AirQualityParser.cs` | Map your JSON fields (`anomaly_score`, etc.) to `TelemetryMeasure` per sensor `JsonPath` |
| `Devices/AirQualityDevice.cs` | Rename to `EdgeAiRestDevice`; it extends `RequestResponseDeviceBase` |
| `Program.cs` | `builder.AddDevice<EdgeAiRestDevice>("EdgeAiRest");` |

The device skeleton (from that example) is:

```csharp
public class EdgeAiRestDevice : RequestResponseDeviceBase
{
    public EdgeAiRestDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey]) { }

    public EdgeAiRestDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += (_, e) => { /* log measures */ };
    }
    // CreateParser builds HttpClient -> EdgeAiClient -> HttpCommunication -> EdgeAiParser
}
```

The parser's `ReadTelemetryAsync` calls `communication.RequestAsync(...)`, then emits one `TelemetryMeasure` per sensor -- exactly as `AirQualityParser` does. SubNode calls it automatically every `Interval`.

---

## Step 2 — Turn a Detection into an Alert

Whichever interface you use, **thresholds** on the `confidence` sensor convert a detection into a WedaCore warning/critical state with no extra code -- so only confident detections raise an alert:

```json
"Parameters": {
  "JsonPath": "$.confidence",
  "Thresholds": { "UpperWarning": 0.7, "UpperCritical": 0.9 }
},
"Report": { "Enabled": true, "Interval": 1000 }
```

Vision/sound models often *flicker* -- firing on one frame, clearing on the next. Smooth the confidence with a DSP filter before the threshold check to avoid alert storms:

```json
"Parameters": {
  "JsonPath": "$.confidence",
  "Thresholds": { "UpperWarning": 0.6, "UpperCritical": 0.85 }
},
"Report": {
  "DspPipeline": [ { "Type": "movingAverage", "Enabled": true, "Parameters": { "WindowSize": 5 } } ]
}
```

See [Data Pipeline](../05-data-pipeline/01-overview.md) for transforms, filters, and thresholds.

---

## Step 3 — Run It (Docker, No .NET)

Follow the same loop as the [SI Integration Guide](./05-si-integration-guide.md): edit `devicecfg.json`, then bring the container up. If your Edge AI container and a broker run via Docker Compose, add this SubNode service to that same compose file so they share a network. To send the telemetry on to the cloud, supply WedaNode credentials as environment variables -- see [Connect to WedaCore](./04-connect-to-wedacore.md#environment-variables). No code change is needed to switch from `UseMockCloud()` to production.

---

## Summary

- Your Edge AI container is a data source; SubNode integrates it by **transport**: MQTT → `PubSubDeviceBase`, REST → `RequestResponseDeviceBase`.
- Vision/sound detections are **events** -- prefer **MQTT** (instant push); optionally add a slow **REST** poll as a heartbeat (rolling count, last-inference time).
- Model the detection: **class label** → `string`, **confidence** → threshold-able `double`, **`detected`** → `boolean` alarm, plus an optional **frame/clip** as a binary sensor with `chunking`.
- Don't write integrations from scratch -- copy `examples/mqtt-image-chunked` (MQTT + binary) or `examples/http-air-quality` (REST) and change the topic/URL and field mapping.
- Threshold the **confidence** (with optional DSP smoothing) so only confident, non-flickering detections raise WedaCore alerts.

## See Also

- [SI Integration Guide (Docker-only)](./05-si-integration-guide.md) -- the no-code Docker workflow
- [Configuration via JSON](../04-configuration/02-configuration-via-json.md) -- sensor fields and `Parameters`
- [Data Pipeline](../05-data-pipeline/01-overview.md) -- transforms, DSP filters, thresholds
- [Connect to WedaCore](./04-connect-to-wedacore.md) -- send telemetry to the cloud
- Reference examples: [`http-air-quality`](https://github.com/Advantech-Containers/WEDA-Sub-Node/tree/main/examples/http-air-quality) (REST), [`mqtt-image-chunked`](https://github.com/Advantech-Containers/WEDA-Sub-Node/tree/main/examples/mqtt-image-chunked) (MQTT)

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-06-01 | Kevin.Chien | Doc created -- Edge AI container (REST + MQTT) integration recipe. |
| 1.1.0 | 2026-06-01 | Kevin.Chien | Tailored to vision/sound detection: detection-event modeling, confidence thresholds, snapshot frame/audio clip evidence. |
