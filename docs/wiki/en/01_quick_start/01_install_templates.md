---
title: "Install Templates"
description: "Install Weda SubNode SDK project templates using dotnet CLI"
author: "Rain Hu"
date: "2025-11-07"
lang: "en"
parent: "README"
prev: "00_overview"
next: "02_subnode_basic"
translations:
  - lang: "zh"
    path: "../../zh/01_quick_start/01_install_templates.md"
---

# Install Templates

Learn how to install the three Weda SubNode SDK project templates.

**Time**: 3 minutes
**Difficulty**: Beginner

---

## Prerequisites

Before installing templates, ensure you have:

- **.NET 9.0 SDK** or later installed
  ```bash
  dotnet --version  # Should output 9.0.x or higher
  ```

If you don't have .NET 9.0 SDK:
- **macOS**: `brew install dotnet@9`
- **Windows**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Linux**: Follow [Microsoft's instructions](https://learn.microsoft.com/dotnet/core/install/linux)

---

## Installation Methods

There are two ways to install templates:

### Method 1: Using Install Script (Recommended)

Navigate to the SDK repository root and run:

```bash
bash scripts/install-templates.sh
```

**Expected output**:
```
════════════════════════════════════════════════════════════════
 Weda SubNode SDK - Template Installation
════════════════════════════════════════════════════════════════

Installing templates from: /path/to/edge_subnode/templates

→ Installing template: subnode
Success: Weda.SubNode.CustomDevice installed successfully

→ Installing template: wedaapi
Success: Weda.SubNode.API.Simple installed successfully

→ Installing template: wedaapi-c
Success: Weda.SubNode.API.Advanced installed successfully

════════════════════════════════════════════════════════════════
✓ Successfully installed 3 templates
════════════════════════════════════════════════════════════════

Available templates:
- subnode    : Custom Device (inherits from ModbusDevice)
- wedaapi    : Simple API (auto-configuration)
- wedaapi-c  : Advanced API (full control)

Verify installation: dotnet new list | grep -i subnode
```

### Method 2: Manual Installation

If the script doesn't work, install each template manually:

```bash
cd /path/to/edge_subnode/templates

# Install subnode template
dotnet new install ./subnode

# Install wedaapi template
dotnet new install ./wedaapi

# Install wedaapi-c template
dotnet new install ./wedaapi-c
```

---

## Verify Installation

Check that all templates are installed:

```bash
dotnet new list | grep -i subnode
```

**Expected output**:
```
Template Name                         Short Name   Language  Tags
------------------------------------  -----------  --------  -----------------------
Weda SubNode API (Advanced)           wedaapi-c    [C#]      Weda/SubNode/IoT/Advanced
Weda SubNode API (Simple)             wedaapi      [C#]      Weda/SubNode/IoT
Weda SubNode Custom Device            subnode      [C#]      Console/IoT/Weda/SubNode/Custom
```

If you see all three templates listed, installation was successful! ✅

---

## Template Details

After installation, you have access to:

### 1. subnode - Custom Device Template

**Short name**: `subnode`
**Usage**: `dotnet new subnode -n MyDevice`

Creates a project with:
- Custom device class inheriting from `TcpModbusDevice`
- Event-driven architecture (`OnDataReceived`)
- Full control over device behavior

### 2. wedaapi - Simple API Template

**Short name**: `wedaapi`
**Usage**: `dotnet new wedaapi -n MyApp`

Creates a project with:
- Minimal code (3 lines in `Program.cs`)
- Configuration-driven (`appsettings.json`)
- Auto-initialization and device management

### 3. wedaapi-c - Advanced API Template

**Short name**: `wedaapi-c`
**Usage**: `dotnet new wedaapi-c -n MyApp`

Creates a project with:
- Full builder pattern control
- Manual service registration
- Manual device registration with `AddDevice<T>()`

---

## Quick Test

Test the installation by creating and running a sample project:

### Step 1: Create Test Project

```bash
# Create a temporary directory
cd /tmp

# Create project using subnode template
dotnet new subnode -n QuickTest

# Navigate to project
cd QuickTest
```

### Step 2: Run the Project

```bash
# Run the project (automatically builds)
dotnet run
```

### Step 3: Verify Success

**Expected output** - You should see:

```
[12:34:54 INF] Initialized sensor TemperatureSensor (Temperature): Address=0, InitialValue=25.00 °C
[12:34:54 INF] Starting Modbus TCP Simulator on 127.0.0.1:5020 (Slave ID: 1)
[12:34:54 INF] Modbus TCP Simulator started successfully
[12:34:54 INF] ────────────────────────────────────────────────────────
[12:34:54 INF] Modbus batch optimization ENABLED for device  (1 sensors)
[12:34:54 INF] Initializing device 
[12:34:54 INF] Connecting to TCP at 127.0.0.1:5020
[12:34:54 INF] Communication state changed from Disconnected to Connecting
[12:34:54 INF] Communication state changed from Connecting to Connected
[12:34:54 INF] Connected to TCP at 127.0.0.1:5020
[12:34:54 INF] Physical device connected successfully
[12:34:54 INF] Client connected from 127.0.0.1:63410
[12:34:54 INF] Cloud service connected successfully
[12:34:54 INF] All connections established successfully
...
[12:34:54 INF] Starting telemetry task with period 5000ms
[12:34:54 INF] Optimized 1 sensors into 1 batch(es) for HoldingRegister
[12:34:54 INF] temperature.sensor: 25
[12:34:59 INF] Optimized 1 sensors into 1 batch(es) for HoldingRegister
[12:34:59 INF] temperature.sensor: 25.178045
```

**Success indicators** ✅:
1. Modbus simulator starts successfully
2. Device connects successfully
3. **Telemetry data is sent periodically** 
4. Temperature values change between 18-32°C

### Step 4: Stop and Clean Up

Press `Ctrl+C` to stop the program, then delete the test project:

```bash
# After stopping
cd /tmp
rm -rf QuickTest
```

**🎉 If you see periodic telemetry data, congratulations! You've successfully created your first SubNode!**

---

### Quick Test Option 2: Using wedaapi Template

If you want to test the simpler API template:

```bash
# create temporary directory
mkdir tmp
cd tmp

# use subnode template to create project
dotnet new subnode -n QuickTest

# enter project directory
cd QuickTest
```

**Expected output**:
```
[12:34:56 INF] Now listening on: http://localhost:5000
[12:34:56 INF] Application started. Press Ctrl+C to shut down.
```

Open your browser and visit `http://localhost:5000` to see the API response.

---

## Troubleshooting

### Issue 1: Template Not Found

**Symptom**:
```
No templates or subcommands found matching: 'subnode'
```

**Solution**:
1. Verify you're in the correct directory
2. Re-run the install script
3. Try manual installation method

### Issue 2: Permission Denied

**Symptom**:
```
bash: permission denied: install-templates.sh
```

**Solution**:
Make the script executable:
```bash
chmod +x scripts/install-templates.sh
bash scripts/install-templates.sh
```

### Issue 3: .NET 9.0 SDK Not Found

**Symptom**:
```
dotnet: command not found
```

**Solution**:
Install .NET 9.0 SDK:
- macOS: `brew install dotnet@9`
- Windows/Linux: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

### Issue 4: Old Template Version

**Symptom**: Template exists but creates outdated code

**Solution**:
Uninstall old templates first:
```bash
# List installed templates
dotnet new uninstall

# Uninstall specific template
dotnet new uninstall Weda.SubNode.CustomDevice
dotnet new uninstall Weda.SubNode.API.Simple
dotnet new uninstall Weda.SubNode.API.Advanced

# Reinstall
bash scripts/install-templates.sh
```

---

## Updating Templates

When templates are updated in the repository:

```bash
# Navigate to SDK repository
cd /path/to/edge_subnode

# Pull latest changes
git pull

# Reinstall templates
bash scripts/install-templates.sh
```

The install script automatically handles template updates.

---

## Uninstalling Templates

To remove all Weda SubNode templates:

```bash
dotnet new uninstall Weda.SubNode.CustomDevice
dotnet new uninstall Weda.SubNode.API.Simple
dotnet new uninstall Weda.SubNode.API.Advanced
```

Verify removal:
```bash
dotnet new list | grep -i subnode
# Should return no results
```

---

## Next Steps

Now that templates are installed, choose your path:

### Recommended for Beginners
**[→ wedaapi - Simple API](03_wedaapi_basic.md)**
- Fastest way to get started
- Zero code required
- Configuration-driven

### For Custom Logic
**[→ subnode - Custom Device](02_subnode_basic.md)**
- Object-oriented architecture
- Event-driven
- Full control over device behavior

### For Advanced Users
**[→ wedaapi-c - Advanced API](04_wedaapi_c_basic.md)**
- Complete control over DI
- Manual service registration
- Enterprise-ready

---

## Summary

You've successfully installed three Weda SubNode SDK templates:

- ✅ `subnode` - Custom Device template
- ✅ `wedaapi` - Simple API template
- ✅ `wedaapi-c` - Advanced API template

**Ready to build?** Pick a template guide above and start building your first IoT edge device!

---

**Version**: 1.0.0
**Last Updated**: 2025-11-07
**Maintainer**: Rain Hu
