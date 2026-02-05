# Introduction

IoT applications can send a **Command Request** to a device agent to execute a **Remote Procedure Call (RPC)** in real time on an online device system. These commands are **volatile** and will not persist after a service restart or device reboot.

# Design

## Command Request

### Topics

```{protocolVer}.{groupId}.{deviceUId}.dm.cmd.[action]```

|Action| Topic | Description |
|--|--|--|
|Request Command Request-Reply|{protoVer}.{groupId}.{deviceUId}.dm.cmd.req|App sends a **Command Request** to run real-time RPC at an online device agent and response in this channel **synchronously**.   Only available when message bus supports blocking communication, like NATS with request-reply message interface, [https://docs.nats.io/nats-concepts/core-nats/reqreply](https://docs.nats.io/nats-concepts/core-nats/reqreply) )|
|Async Command Response|{protoVer}.{groupId}.{deviceUId}.dm.cmd.rsp| Agent sends execution result/state of **Command Request** at device system **asynchronously**. |

```json
    {
      "cmd": "deviceCmd",
      "seqId": <uint64>,
      "reqSeqId": "<uuid>",
      "timestamp": <unixTimestamp>,
      "data": {
          "deviceCmd": "<cmdType>.<subCmd>"
          "timeout": 30,
          "respTopic": "<protoVer>.<groupID>.<deviceUID>.dm.dt.cmd.rsp",
          "parameters": {
              "<paramKey>": "<paramValue>"
          },
      }
    }
```

### Command Request Payload Fields

| Field | Limitation | Description |
| --- | --- | --- |
| cmd | string | Specific message type of payload message.  <br>ex: "deviceCmd" |
| seqId | uint64 | Message Increment sequence Id at sender side.  <br>ex: 10 |
| reqSeqId | string | Command request unique Id in UUID format.  <br>ex: 5550e8400-e29b-41d4-a716-446655440000 |
| timestamp | int64 | Unix timestamp in milliseconds.  <br>ex: 1733711275311 |
| data | object | Specific data payload message of each type of request. |


### Command Request Data Payload Fields

| Field | Limitation | Description |
| --- | --- | --- |
| deviceCmd | string | device request command. "<cmdType>.<subCmd>". Example: "stack.stop"、"stack.start"、"stack.pause"、"stack.pause"、"tunnel.establish"、 "tunnel.terminate"、 "report.historical"、"report.data"|
| timeout | uint32 | command execution timeout. |
| respTopic | string | topic to response COMMAND execution result. |
| parameters | dict | key value maps of the device command parameters |

### Command Request Example

- report.historical
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "timestamp": 1770194027000,
  "data": {
    "deviceCmd": "report.historical",
    "timeout": 300,
    "respTopic": "eco1j.weda.74fe488d5d54.subnode.cmd.rsp",
    "parameters": {
      "transmissionRateLimit": "1111111",
      "maxBatchesPerMessage": "10000",
      "timeRange": {
        "endTime": 1770134400000,
        "startTime": 1770177600000
      },
      "sensorFilter": {
        "include": [
          "f782c"
        ],
        "exclude": []
      }
    }
  }
}
```

- report.data 
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "timestamp": 1770194027000,
  "data": {
    "deviceCmd": "report.data ",
    "timeout": 300,
    "respTopic": "eco1j.weda.74fe488d5d54.subnode.cmd.rsp",
    "parameters": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "dataId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    }
  }
}
```

### Command Response Schema

```json
{
  "cmd": "deviceCmd",
  "seqId": <uint64>,
  "reqSeqId": "<uuid>",
  "rspSeqId": "<uuid>",
  "timestamp": <unixTimestamp>,
  "deviceId": "<deviceUId>",
  "data": {
      "deviceCmd": "<cmdType>.<subCmd>"
      "msgType": "<ack|progress|result>"
      "status": <int>,
      "message": "<string>",
      "resultData": {
          "<key>": "<value>"
      }
  }
}
```

### Command Response

#### Root Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cmd` | string | Yes | Command type: `"deviceCmd"` |
| `seqId` | uint64 | Yes | Message sequence number at sender |
| `reqSeqId` | string (UUID) | Yes | Request identifier from original command request |
| `rspSeqId` | string (UUID) | Yes | Unique response identifier |
| `timestamp` | int64 | Yes | Unix timestamp in milliseconds |
| `deviceId` | string | Yes | Device unique identifier |
| `data` | object | Yes | Response-specific data payload |

#### Data Object Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `deviceCmd` | string | Yes | Command name from original request (e.g., `"report.historical"`, `"report.data"`) |
| `msgType` | string | Yes | Response message type: `"ack"` (acknowledgment), `"progress"` (progress update), or `"result"` (final result) |
| `status` | int | Yes | Status code (0 = success, non-zero = error/partial success) |
| `message` | string | Yes | Human-readable status message |
| `resultData` | object | Yes | Command-specific result data (empty object `{}` if no data) |
| `executedAt` | int64 | No | Unix timestamp when command execution started (only in `result` message) |
| `completedAt` | int64 | No | Unix timestamp when command execution completed (only in `result` message) |

### Command Response Examples

#### **Action Command Execution ACK payload**

- report.data ACK example
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "rspSeqId": "9e2b3b1a-d67c-43f7-b61a-154703aa3061",
  "timestamp": 1770194028146,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.data ",
    "msgType": "ack",
    "status": 0,
    "message": "",
    "resultData": {}
  }
}

```

- report.historical ACK example
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "rspSeqId": "9e2b3b1a-d67c-43f7-b61a-154703aa3061",
  "timestamp": 1770194028146,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "ack",
    "status": 0,
    "message": "Historical data query started",
    "resultData": {
      "estimatedBatches": 12,
      "estimatedSamples": 632,
      "estimatedDurationSeconds": 1,
      "storageAvailable": true
    }
  }
}
```

- report.historical Resulte example
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "rspSeqId": "71364519-7eb9-48ec-b267-eff7ed060dbf",
  "timestamp": 1770194028149,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "result",
    "status": 0,
    "message": "Historical data retrieval complete",
    "resultData": {
      "batchesSent": 12,
      "totalSamples": 632,
      "timeRange": {
        "startTime": "2026-02-04T08:23:48.146Z",
        "endTime": "2026-02-04T08:33:48.146Z"
      },
      "sensors": [
        "089c9",
        "0f564",
        "1df77",
        "1ee0d",
        "2e2ae",
        "392da",
        "3d73e",
        "5b4d7",
        "7c063",
        "99538",
        "b92a7",
        "c6d25"
      ]
    },
    "executedAt": 1770194028146,
    "completedAt": 1770194028148
  }
}
```

- report.historical Progress example
```json
{
  "cmd": "deviceCmd",
  "seqId": 1770194026,
  "reqSeqId": "req-1770194026",
  "rspSeqId": "5c40d247-5c47-47c0-a0f2-a94424ae3b3a",
  "timestamp": 1770194028147,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "progress",
    "status": 0,
    "message": "Progress update",
    "resultData": {
      "batchesSent": 8,
      "totalBatches": 12,
      "samplesSent": 508,
      "totalSamples": 632,
      "percentComplete": 66.7
    }
  }
}
```


________________________________________________________

```json
{
  "group_id": "000a",
  "deviceId": "74fe488d5d54",
  "seqId": 28,
  "reqSeqId": "df00a8d8-01e0-44c0-a169-8b0630a1a860",
  "rspSeqid": "D8309388-7D77-40AE-A371-1EC6ED7B650A",
  "cmd": "deviceCmd",
  "status": 0,
  "message": "",
}
```


#### **Action Command Execution Result Response payload**

```json
{
  "deviceId": "74fe488d5d54",
  "seqId": 28,
  "reqSeqId": "df00a8d8-01e0-44c0-a169-8b0630a1a860",
  "rspSeqid": "D8309388-7D77-40AE-A371-1EC6ED7B650A",
  "cmd": "deviceCmd",
  "data": {
      "deviceCmd": "stack.stop"
      "status": 0,
      "message": "stack dmagent stopped",
      "resultData": {},
      "executedAt": <unixtimestamp>,
      "completedAt": <unixtimestamp>
  }
}
```
