---
sidebar_position: 1
sidebar_label: 'Custom Device'
hide_title: true
title: 'Custom Device | SubNode SDK'
keywords: ['SubNode', 'Custom Device', 'ICommunication', 'IProtocolParser', 'IDevice']
description: 'Build a custom device for the SubNode SDK by implementing ICommunication and IProtocolParser.'
---

# Custom Device

> Implement your own device by combining a communication channel (`ICommunication`) with a protocol parser (`IProtocolParser`).

:::info
This page is coming soon. In the meantime, see the [Event System](./02-event-system.md)
for injecting custom logic, and [Configuration via Code](../04-sensor-configuration/configuration-via-code.md)
for registering a device programmatically.
:::

## Overview

A custom device lets you support hardware or protocols not covered by the
built-in device classes. The two extension points are:

- **`ICommunication`** — how bytes get on and off the wire (TCP, serial, …).
- **`IProtocolParser`** — how raw frames map to sensor telemetry and commands.

Detailed walkthroughs and code samples are being authored and will appear here.

## See Also

- [Event System](./02-event-system.md)
- [Configuration via Code](../04-sensor-configuration/configuration-via-code.md)
