# NATS Web Dashboard

A real-time web dashboard for monitoring SubNode telemetry via NATS pub-sub.

Subscribes to NATS subjects on the message broker (between WedaNode and SubNode), and streams data to the browser via SignalR.

```
  NATS Server                Dashboard Server                   Browser
  ----------                 ----------------                   -------
  [telemetry] --subscribe-->  NatsSubscriptionService
  [health]    --subscribe-->       |
  [config]    --subscribe-->       v
                              TelemetryHub (SignalR) --push-->  dashboard.js
```

## Quick Start

```bash
cd tools/nats-web-dashboard

# Edit appsettings.json with your NATS connection, then:
dotnet run
```

Open http://localhost:5050 in a browser.

## Configuration

All settings are in `appsettings.json` under the `Dashboard` section.
NATS connection uses the SDK's `NatsConnectionSettings` model, so all auth strategies are supported.

```json
{
  "Dashboard": {
    "Nats": {
      "Url": "172.22.160.197:4224",
      "AuthStrategy": "UserPassword",
      "Username": "advantech_nats",
      "Password": "your-password"
    },
    "Subject": "eco1j.weda.{deviceId}.>",
    "Port": 5050
  }
}
```

### NATS Connection (`Dashboard:Nats`)

Uses `NatsConnectionSettings` from `Weda.SubNode.Abstractions`. The `nats://` prefix is auto-added if omitted.

| Field | Description | Default |
|---|---|---|
| `Url` | NATS server URL | `localhost:4222` |
| `AuthStrategy` | `None`, `UserPassword`, `Token`, `CredFile`, `TlsCert` | `None` |
| `Username` | Username (UserPassword) | - |
| `Password` | Password (UserPassword) | - |
| `Token` | Token (Token) | - |
| `CredFile` | Path to .creds file (CredFile) | - |
| `TlsCertPath` | TLS client cert path (TlsCert) | - |
| `TlsKeyPath` | TLS client key path (TlsCert) | - |
| `TlsCaPath` | CA cert for server verification (TlsCert) | - |

### Dashboard Settings

| Field | Description | Default |
|---|---|---|
| `Subject` | NATS subject pattern to subscribe | `>` (all) |
| `Port` | HTTP port for the dashboard | `5050` |

### Override via Environment Variables

```bash
export Dashboard__Nats__Url=192.168.1.100:4222
export Dashboard__Subject="eco1j.weda.12345.>"
export Dashboard__Port=8080
dotnet run
```

### Override via CLI Arguments

```bash
dotnet run -- --Dashboard:Nats:Url=192.168.1.100:4222 \
              --Dashboard:Subject="eco1j.weda.12345.>" \
              --Dashboard:Port=8080
```

## Finding Your NATS Subject

After a SubNode registers with WedaNode, its topic assignments are cached in `.weda/subnode.registration.json`:

```bash
cat apps/your-project/.weda/subnode.registration.json | jq .natsTopicAssignments.telemetryTopic
# "eco1j.weda.297948673633943552.telemetry"
```

Use the device ID with a wildcard to subscribe to all messages:

```
eco1j.weda.{deviceId}.>
```

## Dashboard Features

- **Sensor Telemetry** -- Live sensor values with min/max/avg statistics
- **Device Health** -- CPU, memory, health status with progress bars
- **Sensor Trend Chart** -- Canvas line chart (last 120 samples), tabs to switch between sensors or overlay all
- **Message Log** -- Raw NATS message stream classified by type (telemetry, health, config, command)
- **Stats Bar** -- Message count, rate (msg/s), last update time
- **Auto-reconnect** -- SignalR built-in reconnect with exponential backoff

## Project Structure

```
nats-web-dashboard/
  Program.cs                    -- App startup, DI wiring, middleware pipeline
  appsettings.json              -- Configuration file
  Configuration/
    DashboardOptions.cs         -- Dashboard-specific config (subject, port)
                                   NATS config reuses SDK's NatsConnectionSettings
  Hubs/
    TelemetryHub.cs             -- SignalR hub for real-time client connections
  Services/
    NatsSubscriptionService.cs  -- BackgroundService: NATS subscribe -> hub broadcast
  wwwroot/
    index.html                  -- Dashboard page structure
    css/dashboard.css           -- Styles
    js/dashboard.js             -- SignalR client, state management, rendering, chart
```

### Responsibilities

| Component | Responsibility |
|---|---|
| `NatsConnectionSettings` (SDK) | NATS connection config + auth strategy resolution |
| `DashboardOptions` | Dashboard-specific config (subject, port) |
| `NatsSubscriptionService` | Connect to NATS, subscribe, push to SignalR hub |
| `TelemetryHub` | SignalR hub, manages client connections and lifecycle |
| `dashboard.js` | SignalR client, state management, DOM rendering, chart |
| `dashboard.css` | Visual styling |
