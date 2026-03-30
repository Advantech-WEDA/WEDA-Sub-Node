---
sidebar_position: 1
sidebar_label: 'Prerequisites'
hide_title: true
title: 'Prerequisites | SubNode SDK'
keywords: ['SubNode', 'Prerequisites', '.NET', 'Development', 'Setup', 'Docker', 'Dev Container']
description: 'Tools and environment setup for SubNode SDK development, with three installation options.'
---

# Prerequisites

> Choose an installation option that fits your background and set up your SubNode SDK development environment.

## Overview

SubNode SDK offers three installation options designed for different user profiles. Pick the one that best matches your needs, follow the steps, and you are ready to start developing.

## What You'll Learn

After reading this article, you will be able to:

- Choose the installation option that fits your background
- Complete the development environment setup
- Verify that your environment is configured correctly

---

## Choose an Installation Option

| Option | Requirements | Best For |
|--------|-------------|----------|
| [A. .NET SDK + Editor](#a-net-sdk--editor) | .NET SDK + any editor | .NET developers |
| [B. Docker](#b-docker) | Docker | System integrators (SI) |
| [C. VS Code + Dev Container](#c-vs-code--dev-container) | Docker + VS Code + Dev Containers extension | Developers without .NET experience |

---

## A. .NET SDK + Editor

For developers who already have .NET experience. Install the SDK locally and use your preferred editor or IDE.

### A.1 Install .NET SDK

SubNode requires .NET 10.0 or later.

- **Windows/macOS/Linux**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **macOS (Homebrew)**: `brew install dotnet`
- **Ubuntu/Debian**: Follow [Microsoft's Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux)

### A.2 Verify Installation

```bash
dotnet --version
# Expected output: 10.0.x or higher
```

### A.3 Choose an Editor

| IDE | Platform | Description | Notes |
|-----|----------|-------------|-------|
| **Visual Studio Code** | Cross-platform | Lightweight; install the C# Dev Kit extension | Recommended |
| **Visual Studio** | Windows | Full-featured IDE with debugging tools | |
| **JetBrains Rider** | Cross-platform | Commercial; excellent .NET support | |

### A.4 Verify Environment

```bash
dotnet new console -o TestProject && cd TestProject && dotnet run
# Expected output: Hello, World!

# Clean up
cd .. && rm -rf TestProject
```

---

## B. Docker

For system integrators (SI). No .NET SDK installation required -- run SubNode applications directly with `docker compose up`.

### B.1 Install Docker

- **Windows/macOS**: Install [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- **Linux**: Follow [Docker's official instructions](https://docs.docker.com/engine/install/)

### B.2 Verify Installation

```bash
docker --version
# Expected output: Docker version 28.x or higher

docker compose version
# Expected output: Docker Compose version v2.x
```

### B.3 Run an Example

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node/examples/testdevice

# Start the example with Docker Compose
docker compose up
```

Docker automatically pulls the required images and starts the SubNode application -- no local .NET SDK installation needed.

---

## C. VS Code + Dev Container

For developers without .NET experience. All development tools (.NET SDK, NuGet feeds, extensions) come pre-installed inside the container -- ready to use out of the box.

### C.1 Install Docker

Same as [B.1](#b1-install-docker).

### C.2 Install VS Code

Download and install from [code.visualstudio.com](https://code.visualstudio.com/).

### C.3 Install Dev Containers Extension

Install the **Dev Containers** extension (published by Microsoft) in VS Code.

### C.4 Open the Project

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
code WEDA-Sub-Node
```

When VS Code detects the `.devcontainer/` configuration, it will prompt you to **Reopen in Container**. Click to proceed. The container comes pre-installed with:

- .NET 10.0 SDK
- NuGet package source configuration
- C# Dev Kit extension
- Git

### C.5 Verify Environment

In the VS Code terminal:

```bash
dotnet --version
# Expected output: 10.0.x
```

---

## Hardware (Optional)

For testing with physical devices:

| Device Type | Example | Protocol |
|-------------|---------|----------|
| Modbus TCP device | WISE-4012, ADAM-6017 | Modbus TCP |
| MQTT device | Any MQTT-capable sensor | MQTT |

For development without hardware, use the built-in **Modbus Simulator** (see [Start with Template](./03-start-with-template.md)).

## Summary

| Option | Requirements | Best For | Experience |
|--------|-------------|----------|------------|
| A. .NET SDK + Editor | .NET SDK | .NET developers | Local build and debug |
| B. Docker | Docker | System integrators | `docker compose up` to run |
| C. VS Code + Dev Container | Docker + VS Code + Dev Containers extension | No .NET experience | Develop inside container, zero setup |

## See Also

- [Start with Example](./02-start-with-example.md) - Run a pre-built example
- [Start with Template](./03-start-with-template.md) - Create a new project from a template
- [Connect to WedaCore](./04-connect-to-wedacore.md) - Set up cloud connectivity

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-25 | Rain Hu | Doc created. |
| 1.1.0 | 2026-03-30 | Rain Hu | Rewritten to match zh version with three installation options. |
