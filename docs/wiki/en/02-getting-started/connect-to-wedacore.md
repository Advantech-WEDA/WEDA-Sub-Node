---
sidebar_position: 4
sidebar_label: 'Connect to WedaCore'
hide_title: true
title: 'Connect to WedaCore - Cloud Integration Setup'
keywords: ['SubNode', 'WedaCore', 'WedaNode', 'NATS', 'Cloud']
description: 'Configure SubNode to connect to WedaCore cloud platform via WedaNode'
---

# Connect to WedaCore

> Configure SubNode to connect to WedaCore cloud platform via WedaNode.

## Architecture Overview

SubNode connects to WedaCore through WedaNode, a local NATS proxy:

```
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│   SubNode    │──NATS──>│   WedaNode   │──NATS──>│   WedaCore   │
│ Application  │         │ (127.0.0.1:  │         │   (Cloud)    │
│              │         │     4224)    │         │              │
└──────────────┘         └──────────────┘         └──────────────┘
```

WedaNode is installed by the **Device Activator** and handles:
- Secure connection to WedaCore
- Credential management
- Message routing and buffering

## Development vs Production

### Development Mode (Mock Cloud)

During development, use `UseMockCloud()` to test without WedaCore:

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();  // No real cloud connection
```

This allows you to:
- Test device communication
- Verify sensor configuration
- Debug data pipelines
- Develop without network dependency

### Production Mode (WedaNode)

For production, connect to WedaNode:

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseCloud();  // Connects to WedaNode
```

## Configuration Options

### systemcfg.json

Configure WedaNode connection in `systemcfg.json`:

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

### Authentication Strategies

WedaNode supports multiple authentication methods:

#### No Authentication (Default)

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "None"
  }
}
```

#### Username/Password

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "UserPassword",
    "Username": "your_username",
    "Password": "your_password_hash"
  }
}
```

#### Credentials File

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "CredentialsFile",
    "CredentialsFile": "/path/to/credentials.creds"
  }
}
```

The credentials file is a standard NATS credentials file containing:
- User JWT
- Private NKey

#### NKey Authentication

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "NKey",
    "NKeyFile": "/path/to/nkey.seed"
  }
}
```

## Configuration via Code

You can also configure connection programmatically:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .UseCloud(options =>
    {
        options.Url = "127.0.0.1:4224";
        options.AuthStrategy = AuthStrategy.UserPassword;
        options.Username = "your_username";
        options.Password = "your_password_hash";
    });
```

## Environment Variables

Override configuration with environment variables:

```bash
# Connection URL
export WEDANODE__URL="127.0.0.1:4224"

# Authentication
export WEDANODE__AUTHSTRATEGY="UserPassword"
export WEDANODE__USERNAME="your_username"
export WEDANODE__PASSWORD="your_password_hash"

# Or using credentials file
export WEDANODE__AUTHSTRATEGY="CredentialsFile"
export WEDANODE__CREDENTIALSFILE="/path/to/credentials.creds"
```

## Verifying Connection

### Check WedaNode Status

Verify WedaNode is running:

```bash
# Check if WedaNode is listening
netstat -an | grep 4224
# or
ss -tlnp | grep 4224
```

### Enable Connection Logging

Enable verbose logging to troubleshoot connection issues:

**appsettings.json:**

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

### Expected Log Output

Successful connection:

```
[12:00:00 INF] Connecting to WedaNode at 127.0.0.1:4224
[12:00:00 INF] Connected to WedaNode successfully
[12:00:00 INF] Registering SubNode: MySubNode
[12:00:00 INF] SubNode registered with ID: abc-123-def
```

Connection failure:

```
[12:00:00 ERR] Failed to connect to WedaNode at 127.0.0.1:4224
[12:00:00 ERR] Error: Connection refused
[12:00:05 INF] Retrying connection (attempt 2/5)...
```

## Cloud Features

Once connected to WedaCore via WedaNode, these features are enabled:

| Feature | Direction | Description |
|---------|-----------|-------------|
| Telemetry | Uplink | Send sensor data to cloud |
| Health | Uplink | Report device health status |
| Commands | Downlink | Receive remote commands |
| Config Updates | Downlink | Receive configuration changes |
| Alerts | Uplink | Send threshold alerts |

### Enable/Disable Features

Control which features are active:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddTelemetry()        // Enable telemetry upload
    .AddHealthReporting()  // Enable health reporting
    .AddCommands()         // Enable command reception
    .AddConfigUpdates()    // Enable config sync
    .UseCloud();
```

## Troubleshooting

### Connection Refused

```
Error: Connection refused to 127.0.0.1:4224
```

**Solutions:**
1. Verify WedaNode is running
2. Check WedaNode logs for errors
3. Ensure firewall allows local connection
4. Contact WedaNode administrator

### Authentication Failed

```
Error: Authentication failed - invalid credentials
```

**Solutions:**
1. Verify username/password are correct
2. Check credentials file path exists
3. Ensure credentials are not expired
4. Contact WedaNode administrator for valid credentials

### Timeout

```
Error: Connection timeout after 30s
```

**Solutions:**
1. Check network connectivity
2. Verify WedaNode URL is correct
3. Increase timeout in configuration:
   ```json
   {
     "WedaNode": {
       "ConnectTimeout": 60000
     }
   }
   ```

## Next Steps

- [Project Structure](../03-project-structure.md) - Understand the codebase
- [Sensor Configuration](../04-sensor-configuration/configuration-reference.md) - Configure sensors
- [Remote Control](../06-remote-control/built-in-commands.md) - Handle cloud commands

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
