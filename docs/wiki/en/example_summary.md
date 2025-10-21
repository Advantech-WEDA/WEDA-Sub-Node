1. Overview
  - introduction to this SDK
  - Introduction to layer structure

2. Quick start
  - Template 介紹(`dotnet new subnode`, `dotnet new wedaapi`, `dotnet new wedaapi-c`)
  - Template 安裝(via `install-templates.sh`)
  a. `SubNode` (with TcpModbus sample)
  b. `WedaApi` (with TcpModbus sample)
  c. `Custom WedaApi` (logger setting, NATS configure, retry setting)

3. 沙盒模式
  - Run with Simulator
  - Sensor Scanner(modbus only)
  - Configure with appsettings.json
  - Configure with programmatic injection (TcpModbusDeviceConfiguration -> DeviceConfiguration): optional

4. Transformation
  - Calibration Transformation (offset + scaling)
  - Unit Conversion (攝氏轉華氏)
  - How to implement custom transformation
  
5. Dsp Filters
  - Kalman Filter
  - Moving Average Filter
  - How to implement custom filters

6. Telemetry
  - Send telemetries to Weda Node (Configure NATS)
  - Define Health Report

7. Event-Driven (Observer, no processing)
  - using hooks // record raw data before transformation/filters, periodic cleansing 
                // device status changed, send email
