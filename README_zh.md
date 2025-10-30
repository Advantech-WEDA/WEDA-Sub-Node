[English](README.md) / 繁體中文

# Weda SubNode SDK

專為 IoT 邊緣設備開發的完整 .NET SDK,提供設備連接、資料採集、處理與雲端整合。

## 文件

**[閱讀完整文件](docs/wiki/zh/README.md)**

- [簡介](docs/wiki/zh/introduction.md) - SDK 概述與功能
- [如何開始](docs/wiki/zh/how_to_start.md) - 快速入門指南
- [Modbus 掃描器](docs/wiki/zh/modbus/modbus_scanner.md) - 自動探測設備配置
- [Telemetry 轉換](docs/wiki/zh/transform/telemetry_transform.md) - 資料處理管道

## 快速開始

```bash
# 安裝範本
dotnet new install Weda.SubNode.Templates

# 建立專案
dotnet new wedaapi -n MyIoTApp
cd MyIoTApp

# 執行
dotnet run
```

## 功能特色

- **多協定支援** - Modbus TCP/RTU, MQTT
- **資料處理管道** - 校準、濾波、轉換
- **事件驅動架構** - 可擴展的事件系統
- **雲端整合** - Weda EdgeSync Cloud 平台
- **簡單易用** - ASP.NET Core 風格框架

## 授權

Copyright © 2025 Advantech Corporation

---
**版本**: 0.0.1 | **維護者**: Rain Hu
