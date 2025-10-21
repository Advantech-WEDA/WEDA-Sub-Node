# Weda SubNode SDK - Template Management Scripts

This directory contains scripts for managing project templates and developer workflows.

## 📦 Template Management

### Install Templates

Install all Weda SubNode project templates:

```bash
bash scripts/install-templates.sh
```

After installation, you can create new projects with:

```bash
# Custom device (inherits from ModbusDevice)
dotnet new subnode -n MyDevice

# Simple API application
dotnet new wedaapi -n MyApp

# Advanced API application (CreateBuilder pattern)
dotnet new wedaapi-c -n MyAdvancedApp
```

### Uninstall Templates

Remove installed templates:

```bash
bash scripts/uninstall-templates.sh
```

---

## 🔧 Developer Mode

By default, generated projects use **NuGet packages** for the Weda SubNode SDK. This is simple and works out of the box.

For **SDK development** or **debugging SDK source code**, use Developer Mode:

### Enable Developer Mode

Convert a project to use local SDK source code:

```bash
bash scripts/setup-dev.sh <project-path>
```

**Examples:**

```bash
# Setup a project in examples directory
bash scripts/setup-dev.sh examples/MyDevice

# Setup a project with absolute path
bash scripts/setup-dev.sh /Users/you/projects/MyDevice
```

### What Developer Mode Does

1. **Removes NuGet package references** (e.g., `Weda.SubNode.Host`)
2. **Adds project references** to local SDK source code
3. **Creates a solution file** (`.sln`) containing:
   - Your application project
   - All SDK source projects
4. **Enables debugging** into SDK source code

### Solution Structure

After running `setup-dev.sh`, your project will have:

```
MyDevice/
├── MyDevice.sln              # Solution file
├── MyDevice.csproj           # Your project
├── Program.cs
└── appsettings.json

# Solution includes SDK projects:
# - Weda.SubNode.Abstractions
# - Weda.SubNode.Core
# - Weda.SubNode.Devices
# - Weda.SubNode.Cloud
# - Weda.SubNode.Host
```

### Reverting to NuGet Packages

To switch back to NuGet packages:

```bash
cd your-project
dotnet remove reference ../../src/Weda.SubNode.Host/Weda.SubNode.Host.csproj
dotnet add package Weda.SubNode.Host
```

---

## 📋 Template Details

### `subnode` - Custom SubNode

Creates a custom device by inheriting from `ModbusDevice`:

- **Use case:** Create domain-specific devices with custom logic
- **Pattern:** Object-oriented inheritance
- **Dependencies:** `Weda.SubNode.Core`

### `wedaapi` - Simple API

Creates a simple API application using the builder pattern:

- **Use case:** Quick prototypes, simple applications
- **Pattern:** Builder pattern with `WedaApplicationBuilder`
- **Dependencies:** `Weda.SubNode.Host`

### `wedaapi-c` - Advanced API

Creates an advanced application with full control:

- **Use case:** Production applications, complex scenarios
- **Pattern:** CreateBuilder pattern with manual configuration
- **Dependencies:** `Weda.SubNode.Host`
- **Features:**
  - Manual configuration loading
  - Custom logging setup
  - Event subscription
  - Full lifecycle control

---

## 🔍 Troubleshooting

### Templates not appearing after installation

```bash
# Verify installation
dotnet new list | grep -i weda

# If not showing, try reinstalling
bash scripts/uninstall-templates.sh
bash scripts/install-templates.sh
```

### IDE shows reference errors after template generation

This is expected! The templates use **NuGet packages** which need to be published or available locally.

**Options:**

1. **For production:** Publish SDK packages to NuGet/private feed
2. **For development:** Run `bash scripts/setup-dev.sh <project-path>`

### Project can't find SDK packages

Make sure packages are either:
- Published to a NuGet feed (for production)
- Use Developer Mode with `setup-dev.sh` (for development)

---

## 📚 Additional Resources

- [.NET Template Documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates)
- [Weda SubNode SDK Documentation](../README.md)

---

**Maintained by:** Advantech EdgeSync Team
**Last Updated:** 2025-10-15
