---
sidebar_position: 8
sidebar_label: 'TOT: SubNode for Sales/PM/SE (30 min)'
hide_title: true
title: 'TOT Session — SubNode for Sales, PM and SE (30 min) | SubNode SDK'
keywords: ['SubNode', 'TOT', 'Enablement', 'Sales', 'Product Manager', 'Sales Engineer', 'Positioning', 'Qualification', 'Scoping']
description: 'A timeboxed 30-minute enablement session explaining what SubNode is, how a System Integrator delivers with it, and how to qualify and scope an opportunity.'
---

# TOT Session — SubNode for Sales, PM and SE (30 min)

> A run-of-show for briefing a commercial audience: what SubNode is in business terms, how an SI actually delivers with it, and how to qualify and size an opportunity.

## Overview

This is the commercial companion to [TOT: SubNode for SI](./07-tot-30min-subnode-for-si.md). Same product, same thirty minutes, deliberately different content. The SI session teaches someone to *do the work*; this one teaches Sales, Product Managers and Sales Engineers to *talk about the work* -- what SubNode replaces, what an SI engagement looks like, which opportunities are a fast yes, and which need a developer conversation before anyone promises a date.

The hard part with a mixed commercial room is that three roles want three different things from the same half hour. The agenda below solves that by giving each role one clearly-labelled takeaway rather than trying to satisfy all three continuously.

## What You'll Learn

After reading this article, you (as the facilitator) will be able to:

- Deliver the SubNode value proposition without a terminal or a slide deck rebuild
- Explain the SubNode / WedaNode / WedaCore split, which this audience routinely confuses
- Walk through what an SI delivery actually involves, and how long it tends to take
- Classify an opportunity as green, yellow or red before committing to a timeline
- Answer the objections this audience will hear in the field

## Session at a Glance

| | |
|---|---|
| **Duration** | 30 minutes (26 content + 4 Q&A) |
| **Audience** | Sales, Product Managers, Sales Engineers / pre-sales |
| **Assumed knowledge** | The WEDA portfolio at a high level. **No coding, no Docker.** |
| **Format** | Slides or wiki on screen. One short demo, ideally pre-recorded |
| **Outcome** | Each role leaves with one artefact they can use this week |

### What each role should leave with

| Role | Takeaway |
|------|----------|
| **Sales** | The 60-second pitch, five qualifying questions, four objection answers |
| **PM** | Where SubNode sits in the portfolio, and what is genuinely shipped today |
| **SE** | The green/yellow/red scoping heuristic, and the demo they can run themselves |

---

## Before the Meeting (Facilitator Pre-Flight)

Lighter than the SI session, but do not skip it.

- [ ] **Record the demo in advance.** A live `docker compose up` in front of Sales is a risk with no reward. Capture a 90-second screen recording from [the SI session's demo](./07-tot-30min-subnode-for-si.md#segment-3--live-demo-the-si-loop-10-min) and play that instead.
- [ ] **Check the current release.** Confirm the latest version in `CHANGELOG.md` before you present -- do not read it off `common.props` or the wiki home, both of which are stale. Sales will repeat whatever number you say.
- [ ] **Fill the commercial blanks.** Pricing, licensing, support terms and reference customers are not in this repository and are not in this page. Get them from your BU before the session; leaving them vague in a Sales room is worse than omitting them.
- [ ] Open the [Feature Map](../08-examples/00-feature-map.md) in a second tab. It is the honest answer to "does it support X, and since when".

---

## Run of Show

| Time | Segment | Goal |
|------|---------|------|
| 0:00 – 0:02 | Framing | What each role will be able to do afterwards |
| 0:02 – 0:08 | **The problem, and the one-liner** | Why this exists at all |
| 0:08 – 0:14 | **Where SubNode sits** | Untangle SubNode / WedaNode / WedaCore |
| 0:14 – 0:21 | **How an SI delivers with it** | The motion, and how long it takes |
| 0:21 – 0:25 | **Demo** | Ninety seconds of real telemetry |
| 0:25 – 0:30 | **Qualify, scope, objections** | The part Sales came for |

> **Running late?** Compress Segment 2 to the one-line glossary and cut the demo to thirty seconds. Never cut the qualification segment -- it is the only part that changes behaviour on Monday.

---

## Segment 1 — The Problem, and the One-Liner (6 min)

### The customer problem, in their words

A customer has machines on a floor and wants their data in the cloud. That sounds like a small job and never is:

- Every vendor speaks a different protocol, and the same protocol differently.
- Someone has to write the collection loop, the scaling and calibration, the reconnect-and-retry logic, and the cloud upload -- and then write it again on the next project.
- The result is bespoke, undocumented, and owned by whoever wrote it.

This work gets re-done on essentially every industrial IoT project, and it is not where the customer's value is.

### The one-liner

> **SubNode is the edge software that gets machine data into WedaCore -- configured in a JSON file rather than written as custom code.**

### The 60-second version, for Sales

Use this verbatim; it is calibrated to be defensible:

> "When a customer wants machine data in the cloud, someone normally writes custom code for every device -- read the sensor, scale the value, handle dropouts, push it up. SubNode is that layer, already built and already tested. Your integrator describes the device in a configuration file -- what it is, where it is, which values to read, how often -- and runs a container. For the common industrial protocols, that is the entire job: no programming. It means a device integration that used to be a multi-week development task becomes a configuration task, and the next one is faster still because the same software handles both."

Two claims to keep out of that pitch unless you can back them: any specific time saving in weeks or dollars, and any statement about protocol coverage beyond the list in the feature map.

---

## Segment 2 — Where SubNode Sits (6 min)

This audience conflates the four names constantly. Fixing that is worth six minutes on its own.

| Name | What it actually is | Who installs it |
|------|---------------------|-----------------|
| **SubNode** | The edge application that talks to the machines. One per box/site/panel. | The SI, as a container |
| **WedaNode** | A local NATS proxy on the same box. SubNode's only route to the cloud. | The Device Activator |
| **WedaCore** | The cloud platform: digital twin, telemetry storage, remote commands | Advantech |
| **SubNode SDK** | The developer toolkit used to *build* SubNode applications | Only relevant when custom code is needed |

```text
   Machines            The edge box                    The cloud
┌──────────────┐   ┌──────────────────────────┐   ┌──────────────┐
│  PLC / meter │◀─▶│  SubNode ──▶  WedaNode   │◀─▶│  WedaCore    │
│  sensor / AI │   │  (your config)  (proxy)  │   │  twin + data │
└──────────────┘   └──────────────────────────┘   └──────────────┘
```

Three points worth making explicitly:

1. **SubNode never dials the cloud directly.** It hands off to WedaNode locally. So the customer's firewall conversation is about WedaNode, not about SubNode.
2. **"SubNode" is both a product and a hierarchy.** One SubNode contains many Devices, each containing many Sensors. When a customer says "how many SubNodes do I need", they are usually asking about boxes, not machines.
3. **The digital twin comes out of the configuration.** The device model published to WedaCore is generated from the same JSON the SI writes -- the customer does not model their equipment twice.

> **PM note:** the feature map records which release each capability shipped in. OPC UA, for example, arrived in 1.1.0. If a customer is pinned to an older version, the answer to "do you support OPC UA" is version-dependent, not a flat yes.

---

## Segment 3 — How an SI Delivers With It (7 min)

### The delivery motion

```text
  ┌───────────────┐   edit JSON   ┌───────────────┐   docker compose   ┌───────────────┐
  │ devicecfg.json│ ────────────▶ │   Container    │ ─────────────────▶ │   Telemetry    │
  │ (the device)  │               │  (SubNode)     │                    │  to WedaCore   │
  └───────────────┘               └───────────────┘                     └───────────────┘
           ▲                                                                     │
           └───────────────────────  adjust & re-run  ◀─────────────────────────┘
```

The whole job, for a supported protocol, is a loop: describe the device in JSON, start the container, check the values, adjust. Nothing is compiled. The SI needs Docker and a text editor -- not Visual Studio, not the .NET SDK, not a developer.

### What the SI has to supply

The thing to internalise, because it is the most common cause of a stalled project:

> **The SI must have the device's register map or topic map.** Without it, nobody -- not the SI, not Advantech -- can write the configuration. "We'll get the map later" is the single best predictor of a project that slips.

Beyond that: network access from the edge box to the device, somewhere to run a container, and credentials from whoever operates the WedaNode.

### Green / Yellow / Red — the scoping heuristic

This is the SE's main takeaway. Classify every opportunity before quoting a timeline.

| | Situation | What it takes | Who does it |
|---|-----------|---------------|-------------|
| 🟢 **Green** | Modbus TCP/RTU, MQTT, OPC UA, or an HTTP/REST source | Configuration only | The SI, unaided |
| 🟡 **Yellow** | Unusual payload shape, or values derived across several devices | A small amount of C#, starting from an existing example | An SI developer, or us |
| 🔴 **Red** | Proprietary/vendor-SDK protocol, or high-rate streaming with on-edge maths | A genuine development task | A developer, scoped separately |

Anchor each tier to something real rather than to a gut feel:

- 🟢 Copy the closest of the eleven shipped examples and edit its JSON.
- 🟡 There are worked examples for both cases -- a REST data source, and one device computing values from two others.
- 🔴 There is a shipped example doing exactly this for a vibration DAQ with on-edge signal processing. It is a real capability, and it is real engineering work.

Say the quiet part out loud: **green is a configuration engagement; red is a software project.** Quoting red like green is how these deals go wrong.

### It also runs without the cloud

Useful in a POC conversation: SubNode runs offline against a built-in simulator with a mock cloud, records data locally, and exposes a local REST endpoint. A customer can see the whole thing work before any cloud onboarding happens.

---

## Segment 4 — Demo (4 min)

Play the recording. Narrate three beats only:

1. **This is the input.** A JSON file naming a device, an address, a value, and an interval.
2. **This is it running.** Real telemetry in the log within seconds, against a simulator -- no hardware.
3. **This is a change.** Edit the interval, re-run, watch it speed up. Nothing was recompiled.

That third beat is the demo. Everything else is context for it.

If someone asks to see the cloud side, note that connecting to WedaCore is environment variables in the container configuration, not a code change -- then move on. Do not open a terminal in this room.

---

## Segment 5 — Qualify, Scope, Objections (5 min)

### Five qualifying questions

Give Sales these five, in this order. The first two are disqualifiers.

1. **What protocol does the equipment speak?** Modbus, MQTT, OPC UA or HTTP means green. Anything else, bring in an SE.
2. **Do you have the register map or topic list?** No map, no project -- yet.
3. **How many devices per site, and how many sites?** Drives both effort and the commercial shape.
4. **How often does the data need to arrive?** Seconds is routine. Sub-second streaming is a different, larger conversation.
5. **What is the edge box, and can it run containers?** Linux with Docker is the assumed path.

### Objection handling

| They say | You say |
|----------|---------|
| "We already have a script / gateway doing this." | It works until the second protocol, the first network dropout, or the first person who wrote it leaves. SubNode is that same code, maintained and reused across every site. |
| "Our equipment uses a proprietary protocol." | Supported -- it plugs into the same framework -- but it is development work rather than configuration. Let us scope it properly rather than guess. |
| "We don't want to be locked in." | The configuration is plain JSON you own, and the device model published to the cloud is a standard DTDL model. |
| "Can we prove it works before committing?" | Yes -- it runs against a built-in simulator with no hardware and no cloud connection. That is a same-day POC. |
| "How fast can we see our first data?" | With the register map in hand and a supported protocol, quickly. Without the map, we cannot start. Ask for the map first. |

### The three next steps

1. **Sales:** run the five qualifying questions on your two nearest live opportunities this week.
2. **SE:** run the demo yourself once, following the [SI Integration Guide](./05-si-integration-guide.md), so you can do it live when the room warrants it.
3. **PM:** check any feature you have committed to a customer against the [Feature Map](../08-examples/00-feature-map.md), including the version it shipped in.

---

## Facilitator Notes

- **Do not show source code.** Not one line. This room does not benefit, and it contradicts the "configuration, not programming" message.
- **Do not improvise commercial terms.** Pricing, licensing, support SLAs and reference customers are outside this repository. If asked and you do not know, say you will confirm -- Sales will quote you.
- **Keep the protocol list honest.** Answer coverage questions from the feature map, not from memory. It also documents which capabilities have no worked example yet, which is exactly what a PM needs before committing a roadmap item.
- **Expect the room to split.** Sales will push on timelines and pricing, SEs on protocols, PMs on the roadmap. Park the deep ones and finish the agenda; a stalled Segment 3 costs you the qualification segment, which is the one that matters.
- **The SI session is the follow-up.** Anyone who will actually touch the product should attend [TOT: SubNode for SI](./07-tot-30min-subnode-for-si.md) as well.

## See Also

- [TOT: SubNode for SI (30 min)](./07-tot-30min-subnode-for-si.md) -- the hands-on companion session
- [What is SubNode?](../01-introduction/01-what-is-subnode.md) -- the conceptual background for Segment 1
- [Terminology](../01-introduction/03-terminology.md) -- SubNode, Device, Sensor, WedaNode definitions
- [Feature Map](../08-examples/00-feature-map.md) -- capability, shipped version, and worked example for each feature
- [SI Integration Guide (Docker-only)](./05-si-integration-guide.md) -- what the SI actually does, step by step
- [Integrate an Edge AI Container](./06-integrate-edge-ai-container.md) -- the recipe when the customer already has an AI container

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-07-29 | Kevin.Chien | Doc created -- 30-minute TOT run-of-show for Sales, PM and SE. |
