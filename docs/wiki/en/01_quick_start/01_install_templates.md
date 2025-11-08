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

Test the installation by creating a sample project:

```bash
# Create a temporary directory
cd /tmp

# Create project using wedaapi template
dotnet new wedaapi -n QuickTest

# Navigate to project
cd QuickTest

# Build project
dotnet build
```

**Expected output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If the build succeeds, templates are working correctly! ✅

You can delete the test project:
```bash
cd /tmp
rm -rf QuickTest
```

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
