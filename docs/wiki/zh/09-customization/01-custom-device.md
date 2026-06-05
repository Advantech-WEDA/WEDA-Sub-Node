---
sidebar_position: 1
sidebar_label: '自訂裝置'
hide_title: true
title: '自訂裝置 | SubNode SDK'
keywords: ['SubNode', 'Custom Device', 'ICommunication', 'IProtocolParser', 'IDevice']
description: '透過實作 ICommunication 與 IProtocolParser，為 SubNode SDK 建立自訂裝置。'
---

# 自訂裝置

> 透過組合通訊通道（`ICommunication`）與協定解析器（`IProtocolParser`）實作您自己的裝置。

:::info
此頁面即將推出。在此之前，請參考[事件系統](./02-event-system.md)以注入自訂邏輯，
以及[透過程式碼配置](../04-configuration/01-configuration-via-code.md)以程式化方式註冊裝置。
:::

## 概觀

自訂裝置讓您支援內建裝置類別未涵蓋的硬體或協定。兩個擴充點為：

- **`ICommunication`** — 位元組如何在線路上收送（TCP、序列…）。
- **`IProtocolParser`** — 原始封包如何對應到感測器遙測與命令。

詳細的逐步說明與程式碼範例正在撰寫中，將於此處呈現。

## 相關連結

- [事件系統](./02-event-system.md)
- [透過程式碼配置](../04-configuration/01-configuration-via-code.md)
