---
sidebar_position: 4
sidebar_label: 'Connect to WedaCore'
hide_title: true
title: 'Connect to WedaCore | SubNode SDK'
keywords: ['SubNode', 'WedaCore', 'WedaNode', 'NATS', 'Cloud', 'Authentication']
description: 'Configure SubNode to connect to WedaCore cloud platform via WedaNode.'
---

# Connect to WedaCore

> Configure SubNode to connect to WedaCore cloud platform via WedaNode.

## Overview

SubNode applications do not connect to WedaCore directly. Instead, they communicate through a local WedaNode (NATS Proxy). WedaNode is installed by the Device Activator and handles secure connections, credential management, and message routing. This article explains the differences between development and production modes, and how to configure various authentication strategies in `systemcfg.json`.

## What You'll Learn

After reading this article, you will be able to:

- Understand the relationship between SubNode, WedaNode, and WedaCore
- Distinguish between development mode (MockCloud) and production mode
- Configure different authentication strategies in `systemcfg.json`
- Troubleshoot common connection issues

## Prerequisites

- Completed [Prerequisites](./01-prerequisites.md)
- Completed [Start with Example](./02-start-with-example.md) or [Start with Template](./03-start-with-template.md)

---

## Architecture Overview

```text
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│   SubNode    │──NATS──>│   WedaNode   │──NATS──>│   WedaCore   │
│ Application  │         │ (127.0.0.1:  │         │   (Cloud)    │
│              │         │     4224)    │         │              │
└──────────────┘         └──────────────┘         └──────────────┘
```

- **SubNode** - Your edge device application
- **WedaNode** - Local NATS Proxy, installed by Device Activator, listens on `127.0.0.1:4224` by default
- **WedaCore** - Cloud platform providing digital twin, telemetry storage, remote control, etc.

---

## Development Mode vs Production Mode

### Development Mode (MockCloud)

During development, use `UseMockCloud()` -- no WedaNode or network connection required:

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();
```

MockCloud simulates cloud behavior, allowing you to:

- Test device communication and sensor configuration
- Debug Data Pipeline
- Develop Command Handlers
- Work completely offline

### Production Mode

In production, remove `UseMockCloud()`. `CreateDefaultBuilder` automatically reads WedaNode connection settings from `systemcfg.json`:

```csharp
// CreateDefaultBuilder automatically reads systemcfg.json for WedaNode settings
var builder = WedaApplication.CreateDefaultBuilder(args);
```

---

## Configuring WedaNode Connection

### systemcfg.json

WedaNode connection settings are defined in `systemcfg.json`:

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

> `systemcfg.json` is internally loaded into the `SystemConfig` section (i.e., `SystemConfig:WedaNode`).

---

## Authentication Strategies

The SDK supports the following authentication strategies (corresponding to the `NatsAuthStrategy` enum):

### None (Default)

Anonymous connection, no authentication required:

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

### UserPassword

Username and password authentication (default strategy used by Device Activator):

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "UserPassword",
    "Username": "advantech_nats",
    "Password": "your_password_hash"
  }
}
```

### Token

Token-based authentication:

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "Token",
    "Token": "your_auth_token"
  }
}
```

### CredFile

NATS Credential File (contains JWT + NKey Seed):

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "CredFile",
    "CredFile": "/path/to/credentials.creds"
  }
}
```

### TlsCert

TLS client certificate authentication:

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "TlsCert",
    "TlsCertPath": "/path/to/client.crt",
    "TlsKeyPath": "/path/to/client.key",
    "TlsCaPath": "/path/to/ca.crt"
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `TlsCertPath` | Yes | TLS client certificate (PEM or PFX) |
| `TlsKeyPath` | Yes | TLS client private key (PEM) |
| `TlsCaPath` | No | CA certificate for server verification |

### Authentication Strategy Summary

| AuthStrategy | Required Fields | Use Case |
|--------------|-----------------|----------|
| `None` | - | Development, testing |
| `UserPassword` | `Username`, `Password` | Device Activator default |
| `Token` | `Token` | Simple token authentication |
| `CredFile` | `CredFile` | NATS JWT + NKey authentication |
| `TlsCert` | `TlsCertPath`, `TlsKeyPath` | Enterprise TLS mutual authentication |

---

## Overriding Settings

In addition to `systemcfg.json`, WedaNode connection settings can be overridden via environment variables or command-line arguments. The configuration path is `SystemConfig:WedaNode:<Property>`.

### Environment Variables

```bash
# Connection URL
export SystemConfig__WedaNode__Url="127.0.0.1:4224"

# UserPassword authentication
export SystemConfig__WedaNode__AuthStrategy="UserPassword"
export SystemConfig__WedaNode__Username="your_username"
export SystemConfig__WedaNode__Password="your_password_hash"

# Or CredFile authentication
export SystemConfig__WedaNode__AuthStrategy="CredFile"
export SystemConfig__WedaNode__CredFile="/path/to/credentials.creds"
```

> Environment variables use `__` (double underscore) as the hierarchy separator.

### Command-Line Arguments

```bash
# Override URL
dotnet run -- --SystemConfig:WedaNode:Url=172.22.160.197:4224

# Override authentication
dotnet run -- \
  --SystemConfig:WedaNode:AuthStrategy=UserPassword \
  --SystemConfig:WedaNode:Username=admin \
  --SystemConfig:WedaNode:Password=secret
```

> Command-line arguments have the highest priority, overriding both `systemcfg.json` and environment variables. Useful for quickly testing different WedaNode connections.

---

## Cloud Features

Once connected to WedaCore, the following features are available:

| Feature | Direction | Description |
|---------|-----------|-------------|
| Telemetry | Uplink | Send sensor data to cloud |
| Health Reporting | Uplink | Report device health status |
| Commands | Downlink | Receive and execute remote commands |
| Config Updates | Downlink | Receive and apply configuration changes |
| Recording | Local | Local historical data storage |

When using `CreateBuilder`, you can selectively enable features:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddTelemetry()        // Uplink: telemetry reporting
    .AddHealthReporting()  // Uplink: health status
    .AddCommands()         // Downlink: remote commands
    .AddConfigUpdates()    // Downlink: configuration sync
    .AddRecording();       // Local: historical data storage
```

> `CreateDefaultBuilder` enables all features by default.

---

## Verifying the Connection

### Check WedaNode Status

```bash
# Check if WedaNode is listening
netstat -an | grep 4224
# or
ss -tlnp | grep 4224
```

### Enable Connection Logging

Enable verbose logging in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Weda.SubNode.Cloud": "Debug"
      }
    }
  }
}
```

---

## Troubleshooting

### Connection Refused

```text
Error: Connection refused to 127.0.0.1:4224
```

**Solutions:**

1. Verify WedaNode is running
2. Check WedaNode logs for errors
3. Ensure the firewall allows local connections
4. Contact the WedaNode administrator

### Authentication Failed

```text
Error: Authentication failed - invalid credentials
```

**Solutions:**

1. Verify `AuthStrategy` matches the method provided by the WedaNode administrator
2. Check that `Username` / `Password` or credential file paths are correct
3. Ensure credentials have not expired
4. Contact the WedaNode administrator for valid credentials

---

## Summary

- SubNode connects to WedaCore via WedaNode (local NATS Proxy), not directly to the cloud
- Use `UseMockCloud()` during development; remove it in production to auto-read `systemcfg.json`
- The SDK supports 5 authentication strategies: `None`, `UserPassword`, `Token`, `CredFile`, `TlsCert`
- Device Activator defaults to `UserPassword` strategy; `systemcfg.json` is configured automatically

## See Also

- [Project Structure](../03-architecture/01-project-structure.md) - Understand configuration file loading order
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Configure devices and sensors
- [Terminology](../01-introduction/03-terminology.md) - Definitions of WedaNode, WedaCore

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |
