---
sidebar_position: 1
sidebar_label: 'Prerequisites'
hide_title: true
title: 'Prerequisites - SubNode Development Environment Setup'
keywords: ['SubNode', 'Prerequisites', '.NET', 'Development', 'Setup']
description: 'Required tools and environment setup for SubNode SDK development'
---

# Prerequisites

> Set up your development environment for SubNode SDK.

## Required Software

### .NET SDK

SubNode requires .NET 9.0 or later.

**Installation:**

- **Windows/macOS/Linux**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **macOS (Homebrew)**: `brew install dotnet`
- **Ubuntu/Debian**: Follow [Microsoft's Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux)

**Verify installation:**

```bash
dotnet --version
# Expected output: 9.0.x or higher
```

### IDE (Recommended)

Choose one of the following:

| IDE | Platform | Notes |
|-----|----------|-------|
| **Visual Studio 2022** | Windows | Full-featured IDE with debugging |
| **Visual Studio Code** | Cross-platform | Lightweight, requires C# extension |
| **JetBrains Rider** | Cross-platform | Commercial, excellent .NET support |

For VS Code, install these extensions:
- C# Dev Kit (Microsoft)
- C# (Microsoft)

## Optional Tools

### Git

For cloning examples and version control.

```bash
# Verify installation
git --version
```

### Docker

For running simulators and containerized deployments.

```bash
# Verify installation
docker --version
```

## NuGet Package Sources

SubNode packages are distributed via Azure DevOps private feed. Configure NuGet to access the feed:

**Option 1: Global NuGet Configuration**

Create or edit `~/.nuget/NuGet/NuGet.Config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="EdgeSync" value="https://pkgs.dev.azure.com/YourOrg/_packaging/EdgeSync/nuget/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <EdgeSync>
      <add key="Username" value="YOUR_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PAT" />
    </EdgeSync>
  </packageSourceCredentials>
</configuration>
```

**Option 2: Project-level Configuration**

Include `NuGet.Config` in your project root (provided in templates).

## Hardware (Optional)

For real device testing:

| Device Type | Example | Protocol |
|-------------|---------|----------|
| Modbus TCP Device | WISE-4012, ADAM-6017 | Modbus TCP |
| Modbus RTU Device | ADAM-4017, ADAM-4055 | RS-485 |
| MQTT Device | Any MQTT-capable sensor | MQTT |

For development without hardware, use the built-in **Modbus Simulator** (see [Start with Template](./start-with-template.md)).

## Verify Environment

Run this command to verify your environment:

```bash
# Create a new console project to verify .NET is working
dotnet new console -o TestProject
cd TestProject
dotnet run
# Should print "Hello, World!"

# Clean up
cd ..
rm -rf TestProject
```

## Next Steps

Once your environment is ready:

1. [Start with Example](./start-with-example.md) - Run a pre-built example
2. [Start with Template](./start-with-template.md) - Create a new project from template
3. [Connect to WedaCore](./connect-to-wedacore.md) - Configure cloud connectivity

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
