---
sidebar_position: 3
sidebar_label: 'Command Capability Gating'
hide_title: true
title: 'Command Capability Gating | SubNode SDK'
keywords: ['SubNode', 'Command', 'RequiresDeviceCapability', 'Capability', 'Generic Command', 'Device-Specific Command']
description: 'Use RequiresDeviceCapability so a SubNode only exposes device-specific commands its devices can actually serve.'
---

# Command Capability Gating

> Use `[RequiresDeviceCapability]` so a SubNode only exposes device-specific commands its devices can actually serve.

## Overview

The SDK ships two kinds of commands. Generic commands (`report.data`, `report.historical`) are part of every SubNode implementation and are always available. Device-specific commands (`di.get`, `do.set`, ...) only make sense when a device with the matching hardware capability is present — a System Agent has no digital inputs, so it should not offer `di.get` on the WEDA portal. Command Capability Gating keeps generic commands untouched while filtering device-specific commands out of the cloud catalog when no configured device supports them.

## What You'll Learn

After reading this article, you will be able to:

- Distinguish generic commands from device-specific commands
- Understand what command registration and command exposure each mean
- Know when `[RequiresDeviceCapability]` applies (library code) and when it does not (application code)
- Configure the capability filter manually outside the Host builder

## Prerequisites

- Completed [Remote Control Configuration](./01-configuration.md)
- Familiarity with device capability interfaces such as `IDigitalInputReadable`
  (see [Custom Device](../09-customization/01-custom-device.md))

---

## Terminology: Registration vs Exposure

Two different things happen to a command inside the SDK, and this feature only touches the second one:

| Term | Definition |
|------|------------|
| **Registration** | The command's handler is placed in `CommandRegistry`, making the command dispatchable — if a request for it arrives over NATS, the handler runs. Every scanned handler is always registered. |
| **Exposure** | The command is included in the capability catalog the SubNode uploads to the cloud: an entry in `deviceCapabilities.commands[]` plus its DTDL Interface in `refModels[]`. The WEDA portal builds its command list and parameter forms from this catalog, so *exposed* means *visible and invocable from the portal UI*. |

A command that is registered but not exposed does not appear on the portal. If something dispatches it anyway, the handler still executes and its own runtime device lookup returns an explicit "no device supports this capability" error — never a misleading "unknown command".

```
┌──────────────────────────────────────────────────────────────┐
│                      CommandRegistry                         │
│                                                              │
│  ScanAssembly()          GetDescriptors()                    │
│  ┌────────────┐          ┌─────────────────────────┐         │
│  │ register   │          │ filter device-specific  │         │
│  │ ALL        │─────────▶│ commands by device      │──▶ Cloud│
│  │ handlers   │          │ capabilities            │  Upload │
│  └────────────┘          └─────────────────────────┘         │
│        │                                                     │
│        ▼                                                     │
│  Dispatch (always available, handler guards at runtime)      │
└──────────────────────────────────────────────────────────────┘
```

## Generic vs Device-Specific Commands

| Kind | Examples | Exposed |
|------|----------|------------|
| **Generic** — default-provided by the SDK for every SubNode, independent of what devices are attached | `report.data`, `report.historical` | Always |
| **Application-defined** — written in the SubNode application itself, for that SubNode's own devices | `system.reboot`, `system.shutdown` (System Agent) | Always |
| **Device-specific (library-shipped)** — reaches every SubNode via assembly scan, but only meaningful when a device implements the matching capability interface | `di.get`, `do.get`, `do.set`, `ai.get`, `ao.get`, `ao.set` | Only when a configured device class implements the required capability |

Every SubNode therefore always exposes the generic set. A SubNode whose devices have no I/O capability at all — such as the System Agent — still exposes:

```json
["report.data", "report.historical", "system.reboot", "system.shutdown"]
```

The mechanism behind the table is a single attribute: a handler **without** `[RequiresDeviceCapability]` is generic (always exposed); a handler **with** it is device-specific (gated).

### Built-in device-specific commands

| Command | Handler | Required capability |
|---------|---------|---------------------|
| `di.get` | `GetDigitalInputCommandHandler` | `IDigitalInputReadable` |
| `do.get` | `GetDigitalOutputCommandHandler` | `IDigitalOutputReadable` |
| `do.set` | `SetDigitalOutputCommandHandler` | `IDigitalOutputControllable` |
| `ai.get` | `GetAnalogInputCommandHandler` | `IAnalogInputReadable` |
| `ao.get` | `GetAnalogOutputCommandHandler` | `IAnalogOutputReadable` |
| `ao.set` | `SetAnalogOutputCommandHandler` | `IAnalogOutputControllable` |

Gating rule: a device-specific command is exposed when **any** device class registered through `AddDevice<TDevice>()` implements **any** of the capability interfaces the handler declares.

If you later register a Modbus device class that implements `IDigitalInputReadable`, `di.get` joins the catalog automatically — no configuration change needed.

## Who Needs the Attribute? Library Code Only

The convention is simple: **`[RequiresDeviceCapability]` belongs in library code, not in application code.**

| Where the handler lives | Attribute | Why |
|-------------------------|-----------|-----|
| SubNode application (entry assembly) | Not needed | You wrote the command for this specific SubNode — your devices support it by construction. It is always exposed. |
| SDK / reusable device library | Required for hardware-dependent commands | Library handlers reach every SubNode through assembly scanning, including SubNodes whose devices lack the hardware. The attribute is what keeps them out of those catalogs. |

The original problem this feature solves only exists in library code: the SDK ships its I/O handlers to every SubNode via `ScanAssembly`, so without gating a System Agent would expose `di.get` it can never serve. Application handlers are opted in by being written, so no gating question arises.

For library authors, declare the capability the handler depends on:

```csharp
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Devices.Capabilities;

[RequiresDeviceCapability(typeof(IDigitalOutputControllable))]
public class SetDigitalOutputCommandHandler
    : ICommandHandler<SetDigitalOutputCommand, SetDigitalOutputResult>
{
    public async Task<ErrorOr<SetDigitalOutputResult>> HandleAsync(
        SetDigitalOutputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        // Runtime guard: locate devices that actually implement the capability
        var devices = context.GetAllDevices<IDigitalOutputControllable>();
        // ...
    }
}
```

Multiple capabilities use ANY-of semantics — the command is exposed when a device implements at least one of them:

```csharp
[RequiresDeviceCapability(
    typeof(IDigitalOutputControllable),
    typeof(IAnalogOutputControllable))]
public class OutputResetCommandHandler
    : ICommandHandler<OutputResetCommand, OutputResetResult>
```

Two rules for library authors:

- The interfaces declared in the attribute must match what the handler actually looks up via `context.GetAllDevices<T>()`, so exposure never diverges from runtime behavior.
- Leave the attribute off for commands that do not depend on device hardware (`report.data`-style commands) — they stay generic.

## Host Wiring (Automatic)

With `WedaApplication.CreateBuilder()` or `CreateDefaultBuilder()`, the SDK feeds the device classes from `AddDevice<TDevice>()` into `CommandRegistry` automatically. No extra code:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddCommands();

// The device classes registered here drive the capability filter
builder.AddDevice<LocalSystemAgentDevice>("SystemAgentDeviceConfig");

var app = builder.Build();
await app.RunAsync();
```

## Manual Wiring (Non-Host Scenarios)

When you operate `CommandRegistry` directly — integration tests, custom bootstrapping — provide the device classes with `SetDeviceClasses()`:

```csharp
var registry = new CommandRegistry();
registry.ScanAssembly(typeof(CommandRegistry).Assembly);

// Device-specific commands are exposed only if implemented by these classes
registry.SetDeviceClasses([typeof(MyModbusDevice)]);

var descriptors = registry.GetDescriptors();
```

**Backward compatibility**: a registry on which `SetDeviceClasses()` was never called keeps the legacy behavior — every registered command is exposed.

## Behavior Reference

| Scenario | Result |
|----------|--------|
| Handler without attribute (generic) | Always exposed |
| Handler with attribute, some device class implements a declared interface | Exposed |
| Handler with attribute, no device class matches | Not exposed, still dispatchable |
| `SetDeviceClasses()` never called | Everything exposed (legacy) |
| Cloud dispatches a non-exposed command | Handler runs, returns "no capable device" error |

---

## Summary

Key takeaways:

- Generic commands (`report.data`, `report.historical`) and application-defined commands (`system.reboot`) are always exposed on every SubNode.
- Device-specific commands are gated: they reach the cloud catalog only when a registered device class implements the declared capability interface.
- Exposure controls portal visibility only; registration and dispatch are never gated, and handlers guard themselves at runtime.
- `[RequiresDeviceCapability]` is a library-code concern: the SDK's built-in I/O handlers (and any reusable device library shipping handlers) declare it; application code never needs it.
- Host builders wire the filter automatically; bare registries keep the legacy expose-everything behavior until `SetDeviceClasses()` is called.

## See Also

- [Remote Control Configuration](./01-configuration.md) - Enable command receiving and config updates
- [Custom Commands](../09-customization/04-custom-commands.md) - Full guide to custom commands and handlers
- [Custom Device](../09-customization/01-custom-device.md) - Device classes and capability interfaces

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-07-15 | Rain Hu | Initial version. |
