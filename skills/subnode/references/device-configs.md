# Device Configuration Reference

Complete configuration examples for each device type.

> **`ResourceId` is not a configurable field.** The SDK derives every sensor's `ResourceId` at device
> initialization as `UUIDv5(ns("weda"), "{SubNodeDeviceId}.{DeviceName}.{SensorName}")`, overwriting
> anything supplied in configuration. Do not set it in these files. See
> `docs/wiki/en/04-sensor-configuration/configuration-reference.md#resourceid-generation`.

## Modbus TCP Device Configuration

```json
{
  "ConfigKey": "modbus-device-1",
  "Enabled": true,
  "DeviceCommunication": {
    "Host": "192.168.1.100",
    "Port": 502
  },
  "Properties": {
    "SlaveId": 1,
    "PollingIntervalMs": 1000,
    "BatchOptimization": true
  },
  "ConnectionSettings": {
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "TimeoutMs": 5000,
    "KeepAlive": true
  },
  "Periods": {
    "TelemetryReportIntervalMs": 5000,
    "HealthReportIntervalMs": 60000,
    "CommandTimeoutMs": 10000
  },
  "Sensors": [
    {
      "Name": "Temperature",
      "Dtmi": "dtmi:com:advantech:Temperature;1",
      "SensorGroup": "TEMP",
      "Parameters": {
        "RegisterAddress": 0,
        "RegisterType": "HoldingRegister",
        "DataType": "Float32",
        "ByteOrder": "BigEndian",
        "RegisterCount": 2
      },
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "Transforms": [
          {
            "Type": "calibration",
            "Parameters": {
              "Scale": 0.1,
              "Offset": -40
            }
          }
        ],
        "DspFilters": [
          {
            "Type": "movingaverage",
            "Parameters": {
              "Window": 5
            }
          }
        ]
      }
    },
    {
      "Name": "Humidity",
      "Dtmi": "dtmi:com:advantech:Humidity;1",
      "SensorGroup": "AI",
      "Parameters": {
        "RegisterAddress": 2,
        "RegisterType": "HoldingRegister",
        "DataType": "UInt16",
        "ByteOrder": "BigEndian"
      },
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "Transforms": [
          {
            "Type": "calibration",
            "Parameters": {
              "Scale": 0.01,
              "Offset": 0
            }
          }
        ]
      }
    },
    {
      "Name": "DigitalInput1",
      "Dtmi": "dtmi:com:advantech:DigitalInput;1",
      "SensorGroup": "DI",
      "Parameters": {
        "RegisterAddress": 0,
        "RegisterType": "Coil",
        "DataType": "Boolean"
      },
      "Report": {
        "Enabled": true,
        "Interval": 1000
      }
    }
  ]
}
```

## MQTT/iSensing Device Configuration

```json
{
  "ConfigKey": "mqtt-sensor-1",
  "Enabled": true,
  "DeviceCommunication": {
    "BrokerUrl": "mqtt://localhost:1883",
    "ClientId": "subnode-mqtt-client-001",
    "TopicPrefix": "sensors/building-a",
    "QoS": 1,
    "CleanSession": true
  },
  "Properties": {
    "Username": "",
    "Password": "",
    "UseTls": false,
    "KeepAliveSeconds": 60
  },
  "ConnectionSettings": {
    "RetryCount": 5,
    "RetryDelayMs": 2000,
    "TimeoutMs": 10000,
    "AutoReconnect": true
  },
  "Periods": {
    "TelemetryReportIntervalMs": 5000,
    "HealthReportIntervalMs": 60000
  },
  "Sensors": [
    {
      "Name": "RoomTemperature",
      "Dtmi": "dtmi:com:isensing:Temperature;1",
      "SensorGroup": "TEMP",
      "Parameters": {
        "SubscribeTopic": "sensors/building-a/room-101/temperature",
        "JsonPath": "$.value",
        "Unit": "C"
      },
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "DspFilters": [
          {
            "Type": "kalman",
            "Parameters": {
              "ProcessNoise": 0.01,
              "MeasurementNoise": 0.1
            }
          }
        ]
      }
    },
    {
      "Name": "CO2Level",
      "Dtmi": "dtmi:com:isensing:CO2;1",
      "SensorGroup": "AI",
      "Parameters": {
        "SubscribeTopic": "sensors/building-a/room-101/co2",
        "JsonPath": "$.ppm",
        "Unit": "ppm"
      },
      "Report": {
        "Enabled": true,
        "Interval": 10000,
        "DspFilters": [
          {
            "Type": "movingaverage",
            "Parameters": {
              "Window": 5
            }
          }
        ]
      }
    }
  ]
}
```

## WebSocket Streaming Device Configuration

```json
{
  "ConfigKey": "websocket-stream-1",
  "Enabled": true,
  "DeviceCommunication": {
    "WebSocketUrl": "ws://192.168.1.200:8080/stream",
    "Protocol": "json"
  },
  "Properties": {
    "HeartbeatIntervalMs": 30000,
    "BufferSize": 4096,
    "EnableCompression": false
  },
  "ConnectionSettings": {
    "RetryCount": 10,
    "RetryDelayMs": 3000,
    "TimeoutMs": 15000,
    "ReconnectOnClose": true
  },
  "Periods": {
    "TelemetryReportIntervalMs": 1000,
    "HealthReportIntervalMs": 30000
  },
  "Sensors": [
    {
      "Name": "RealtimePower",
      "Dtmi": "dtmi:com:example:RealtimePower;1",
      "SensorGroup": "PWR",
      "Parameters": {
        "StreamField": "power",
        "Unit": "kW"
      },
      "Report": {
        "Enabled": true,
        "Interval": 1000,
        "Transforms": [
          {
            "Type": "calibration",
            "Parameters": {
              "Scale": 0.001,
              "Offset": 0
            }
          }
        ],
        "DspFilters": [
          {
            "Type": "kalman",
            "Parameters": {
              "ProcessNoise": 0.01,
              "MeasurementNoise": 0.1
            }
          }
        ]
      }
    }
  ]
}
```

## Custom Protocol Device Configuration

```json
{
  "ConfigKey": "custom-device-1",
  "Enabled": true,
  "DeviceCommunication": {
    "Host": "192.168.1.150",
    "Port": 9000,
    "Protocol": "tcp"
  },
  "Properties": {
    "CustomProperty1": "value1",
    "CustomProperty2": 123,
    "FrameDelimiter": "0x0D0A"
  },
  "ConnectionSettings": {
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "TimeoutMs": 5000
  },
  "Periods": {
    "TelemetryReportIntervalMs": 5000,
    "HealthReportIntervalMs": 60000
  },
  "Sensors": [
    {
      "Name": "CustomSensor1",
      "Dtmi": "dtmi:com:custom:Sensor;1",
      "SensorGroup": "AI",
      "Parameters": {
        "ParseOffset": 0,
        "ParseLength": 4,
        "ParseFormat": "float32-le",
        "Unit": "units"
      },
      "Report": {
        "Enabled": true,
        "Interval": 5000
      }
    }
  ]
}
```

## Modbus Register Types Reference

| RegisterType | Address Range | Description | Read Method |
|--------------|---------------|-------------|-------------|
| `Coil` | 00001-09999 | Discrete Output (R/W) | ReadCoils |
| `DiscreteInput` | 10001-19999 | Discrete Input (R) | ReadDiscreteInputs |
| `InputRegister` | 30001-39999 | Analog Input (R) | ReadInputRegisters |
| `HoldingRegister` | 40001-49999 | Analog Output (R/W) | ReadHoldingRegisters |

## Modbus Data Types Reference

| DataType | Size | Description | Example Values |
|----------|------|-------------|----------------|
| `Boolean` | 1 bit | Single coil/discrete | true, false |
| `UInt16` | 2 bytes | Unsigned 16-bit integer | 0 to 65535 |
| `Int16` | 2 bytes | Signed 16-bit integer | -32768 to 32767 |
| `UInt32` | 4 bytes | Unsigned 32-bit integer | 0 to 4294967295 |
| `Int32` | 4 bytes | Signed 32-bit integer | -2147483648 to 2147483647 |
| `Float32` | 4 bytes | 32-bit floating point | IEEE 754 |
| `Float64` | 8 bytes | 64-bit floating point | IEEE 754 |
| `String` | variable | ASCII string | "ABC123" |

## Byte Order Options

| ByteOrder | Description | Use Case |
|-----------|-------------|----------|
| `BigEndian` | MSB first (default) | Most Modbus devices |
| `LittleEndian` | LSB first | Some legacy devices |
| `BigEndianByteSwap` | Big-endian with swapped bytes | Some Asian manufacturers |
| `LittleEndianByteSwap` | Little-endian with swapped bytes | Rare |
