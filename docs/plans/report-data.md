[[_TOC_]]

# report.data Command Specification

## Document Control

| Version | Date       | Author        | Description of Changes                     |
|---------|------------|---------------|--------------------------------------------|
| v1.0    | 2026-02-05 | [Author Name] | Initial specification for report.data command |

---

## 1. Overview

### 1.1 Purpose

The `report.data` command enables **on-demand retrieval of both primitive and MIME type telemetry data** from Sub Nodes. Unlike `report.historical` which returns data in batch telemetry format, `report.data` returns data in **realtime telemetry format**, allowing the same downstream consumers to process both live and queried data without format adaptation.

### 1.2 Scope

This specification applies to:
- Cloud-to-edge on-demand data queries (Core → WEDA Node → Sub Node)
- Primitive telemetry data (double, integer, string, boolean)
- MIME type telemetry data (image/png, image/jpeg, application/octet-stream, application/json)
- Large MIME data with automatic chunking in responses

### 1.3 Comparison with report.historical

| Aspect | `report.historical` | `report.data` |
|--------|---------------------|---------------|
| **Purpose** | Retrieve stored historical batch data | On-demand query of primitive + MIME data |
| **Data types** | Primarily primitive time-series | Primitive **and** MIME (images, blobs, JSON) |
| **Response format** | Batch telemetry format | Realtime telemetry format |
| **NATS subject segment** | `*.hist.enr` | `*.rl.enr` |
| **MIME chunking** | Not applicable | Yes, with chunk metadata |
| **Use cases** | Gap-filling, compliance audits | Interactive queries, image retrieval, forensics with binary data |

### 1.4 Related Specifications

| Specification | Relationship |
|---------------|-------------|
| [B.2.3.2 Device REPORT Command](../../../Historical-Telemetry-Data-Upload/Protocol-Specification/B.2.3.2-Device-REPORT-Command.md) | `report.historical` command — shares ACK/progress/result lifecycle pattern |
| [Large-Size-MIME.md](./Large-Size-MIME.md) | Chunking format and Transceiver pipeline processing |
| [SubNode-MIME-Sensor-Interface.md](./SubNode-MIME-Sensor-Interface.md) | Sub Node SDK sensor interface for MIME data publishing |
| [Report-Data-Retransmission.md](./Report-Data-Retransmission.md) | Error handling for chunk loss and CRC errors |
| [Functional-Requirements.md](../Functional-Requirements.md) | Parent spec — FR-006 |

---

## 2. Command Architecture

### 2.1 Command Flow

:::mermaid
sequenceDiagram
    box Cloud Platform
        participant Portal as MGMT API
        participant TX as Transceiver Service
        participant CNATS as NATS Broker
    end

    box Edge Device
        participant WN as WEDA Node
        participant SN as Sub Node
        participant Store as Local Storage
    end

    autonumber
    Note over Portal, Store: report.data Request Flow

    Portal->>TX: report.data request<br/>(timeRange, sensorIds)
    activate Portal
    TX->>TX: Validate request parameters
    TX->>CNATS: Publish report.data command
    TX->>Portal: Reply Command Accepted
    deactivate Portal

    CNATS->>WN: Sync report.data command
    WN->>SN: Forward report.data command
    activate SN
    SN->>SN: Validate time range and sensors
    SN->>Store: Query local storage
    activate Store
    Store-->>SN: Return matching records
    deactivate Store

    SN->>CNATS: Publish ACK (query started)

    loop For each matching record
        alt Primitive data
            SN->>CNATS: Publish realtime telemetry format
            CNATS->>TX: Receive telemetry
        else MIME data ≤ ChunkSize
            SN->>CNATS: Publish single MIME telemetry
            CNATS->>TX: Receive telemetry
        else MIME data > ChunkSize
            SN->>SN: Generate transferid, split into chunks
            loop For each chunk
                SN->>CNATS: Publish chunk with metadata
                CNATS->>TX: Receive chunk
            end
        end
    end

    SN->>CNATS: Publish Result (complete)
    deactivate SN
    CNATS->>TX: Final status
    TX->>Portal: Data retrieval complete
:::

---

## 3. Command Request Message Format

### 3.1 NATS Topic Pattern

**Mgmt Service → Transceiver:**
```
eco1j.{TENANT_ALIAS}.{deviceId}.subnode.cmd.req
```

**Transceiver → WEDA Node:**
```
eco1p.{TENANT_ALIAS}.{deviceId}.subnode.cmd.req
```

**WEDA Node → Sub Node:**
```
eco1j.weda.{deviceId}.subnode.cmd.req
```

### 3.2 Request Payload

```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "timestamp": 1737004691020,
  "data": {
    "deviceCmd": "report.data",
    "respTopic": "eco1p.advantech.74fe488d5d54.subnode.cmd.rsp",
    "timeout": 300,
    "parameters": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    }
  }
}
```

### 3.3 Field Definitions

#### Root Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cmd` | string | Yes | Command type: `"deviceCmd"` |
| `seqId` | uint64 | Yes | Message sequence number at sender |
| `reqSeqId` | string (UUID) | Yes | Unique request identifier for correlation |
| `timestamp` | int64 | Yes | Unix timestamp in milliseconds |
| `respTopic` | string | Yes | Topic for ACK, progress, and result messages |
| `timeout` | uint32 | Yes | Command execution timeout in seconds |
| `data` | object | Yes | Command-specific parameters |

#### Data Object Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `deviceCmd` | string | Yes | Command name: `"report.data"` |
| `parameters` | object | Yes | Report data parameters |

#### Parameters Object Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sensorShortResourceId` | string | Yes | Short resource ID of the sensor to query |
| `resourceTimestamp` | int64 | Yes | Unix timestamp in milliseconds for the data point to retrieve |
| `transferId` | string (UUID) | No | Unique identifier for the object of this transfer operation |


---

## 4. Command Response Messages

### 4.1 ACK Response

**NATS Topic**: Specified in `respTopic`

```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "rspSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737004691500,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.data",
    "msgType": "ack",
    "status": 0,
    "message": "Data query started",
    "resultData": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    }
  }
}
```

### 4.2 Progress Update

**NATS Topic**: Specified in `respTopic`

```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "rspSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737004695000,
  "deviceId": "74fe488d5d54",
  "data": {
    "status": 0,
    "message": "Data query in progress",
    "deviceCmd": "report.data",
    "msgType": "progress",
    "resultData": {
      "recordsSent": 75,
      "totalRecords": 150,
      "mimeItemsSent": 5,
      "chunksSent": 15,
      "percentComplete": 50.0,
      "currentTimeRange": {
        "startTime": 1737000000000,
        "endTime": 1737001800000
      }
    }
  }
}
```

### 4.3 Completion Result

**NATS Topic**: Specified in `respTopic`

**Success:**
```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "rspSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737004750000,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.data",
    "msgType": "result",
    "status": 0,
    "message": "Data retrieval complete",
    "resultData": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "dataTransferred": true
    }
  }
}
```

**Error:**
```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "rspSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737004750000,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.data",
    "msgType": "result",
    "status": 3,
    "message": "No data available",
    "resultData": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "dataTransferred": false
    }
  }
}
```

---

## 5. Data Response Format (Realtime Telemetry)

Data responses are published to the `dataRespTopic` in **realtime telemetry format**. The NATS subject uses `rl` to distinguish from live realtime streams.

### 5.1 Subject Routing

| Data Type | NATS Subject Pattern |
|-----------|---------------------|
| Primitive (`double`, `integer`, `string`, `bool`) | `{protoVer}.{groupId}.{deviceId}.dm.dt.rl.enr` |
| `application/json` | `eco1j.{groupId}.{deviceId}.app.json.data.enr` |
| `image/png` | `eco1j.{groupId}.{deviceId}.img.png.data.enr` |
| `image/jpeg` | `eco1j.{groupId}.{deviceId}.img.jpeg.data.enr` |
| `application/octet-stream` | `eco1j.{groupId}.{deviceId}.app.blob.data.enr` |

### 5.2 Primitive Data Measuesures are published in the same format as live realtime telemetry:

```json
{
  "resourceId": "res-temp-001",
  "timestamp": 1737000060000,
  "value": 25.5
}
```

### 5.3 JSON Data Response 

```json
{
  "resourceId": "res-event-meta-001",
  "timestamp": 1737000060000,
  "value": {
    "eventType": "objectDetected",
    "objectClass": "person",
    "confidence": 0.95
  }
}
```

### 5.4 MIME Data Response (Single, No Chunking)

When MIME data size ≤ ChunkSize:

```json
{
  "resourceId": "res-snapshot-001",
  "timestamp": 1737000060000,
  "value": "<base64_encoded_image>",
  "size": 500000
}
```

### 5.5 MIME Data Response (Chunked)

When MIME data size > ChunkSize, the data is split into multiple messages sharing the same `transferid`:

**Chunk 0:**
```json
{
  "resourceId": "res-snapshot-001",
  "timestamp": 1737000060000,
  "value": "<base64_chunk_0>",
  "metadata": {
    "transferid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "chunkIndex": 0,
    "totalChunks": 3,
    "totalSize": 2097152,
    "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }
}
```

**Chunk 1:**
```json
{
  "resourceId": "res-snapshot-001",
  "timestamp": 1737000060000,
  "value": "<base64_chunk_1>",
  "metadata": {
    "transferid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "chunkIndex": 1,
    "totalChunks": 3,
    "totalSize": 2097152,
    "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }
}
```

**Chunk 2:**
```json
{
  "resourceId": "res-snapshot-001",
  "timestamp": 1737000060000,
  "value": "<base64_chunk_2>",
  "metadata": {
    "transferid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "chunkIndex": 2,
    "totalChunks": 3,
    "totalSize": 2097152,
    "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }
}
```

> Chunk metadata format is identical to [Large-Size-MIME.md](./Large-Size-MIME.md) and [SubNode-MIME-Sensor-Interface.md](./SubNode-MIME-Sensor-Interface.md), ensuring consumers need only one reassembly implementation.

---

## 6. Error Handling

### 6.1 Status Codes

| Code | Name | Description |
|------|------|-------------|
| 0 | SUCCESS | Command executed successfully, all data retrieved |
| 1 | PARTIAL_SUCCESS | Command completed with data gaps or quality issues |
| 2 | INVALID_INPUT_ARGUMENT | Invalid input argument (bad time range, unknown sensor) |
| 3 | NO_DATA_AVAILABLE | No data found for specified time range and sensors |
| 4 | STORAGE_ERROR | Local storage unavailable or corrupted |
| 5 | TIMEOUT | Command execution exceeded timeout |
| 6 | PERMISSION_DENIED | Insufficient permissions for data access |
| 7 | RESOURCE_EXHAUSTED | Device resources insufficient for query |
| 8 | CHUNK_ASSEMBLY_FAILED | Failed to read or chunk stored MIME data |
| 9 | DATA_CORRUPTED | Stored data integrity check (CRC32) failed |
| 500 | GENERIC_ERROR | Unexpected error |

### 6.2 Error Response Example

```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "rspSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737004693020,
  "deviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.data",
    "status": 2,
    "message": "Invalid time range: sensor data not found",
    "resultData": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    }
  }
}
```

### 6.3 Retransmission

For chunk loss or data corruption during `report.data` responses, the retransmission mechanism is defined in [Report-Data-Retransmission.md](./Report-Data-Retransmission.md):

1. **Chunk Lost** (e.g., received chunks [0, 2, 3] but missing [1]): Wait until timeout → send `report.datapack` request to retransmit missing chunks
2. **CRC32 Error**: Send `report.datapack` request to retransmit corrupted chunk

---

## 7. Use Cases

### 7.1 Query Primitive Sensor Data

**Scenario**: Retrieve a specific temperature reading.

```json
{
  "cmd": "deviceCmd",
  "seqId": 200,
  "reqSeqId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "timestamp": 1737004691020,
  "respTopic": "eco1p.advantech.74fe488d5d54.subnode.cmd.rsp",
  "timeout": 60,
  "data": {
    "deviceCmd": "report.data",
    "parameters": {
      "sensorShortResourceId": "f782c",
      "resourceTimestamp": 1706500000000,
      "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    }
  }
}
```

**Response**: Individual primitive telemetry record on `{protoVer}.{groupId}.{deviceId}.dm.dt.rl.enr`.

### 7.2 Query Camera Snapshots

**Scenario**: Retrieve a specific JPEG snapshot.

```json
{
  "cmd": "deviceCmd",
  "seqId": 201,
  "reqSeqId": "d4e5f6a7-b8c9-0123-def0-234567890123",
  "timestamp": 1737050400000,
  "respTopic": "eco1p.advantech.74fe488d5d54.subnode.cmd.rsp",
  "timeout": 180,
  "data": {
    "deviceCmd": "report.data",
    "parameters": {
      "sensorShortResourceId": "a3f9b",
      "resourceTimestamp": 1737050400000,
      "transferId": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
    }
  }
}
```

**Response**: Chunked JPEG image on `eco1j.{groupId}.{deviceId}.img.jpeg.data.enr`, identified by unique `transferid` and `transferId`.

### 7.3 Query JSON Metadata

**Scenario**: Retrieve JSON metadata for an event detection.

```json
{
  "cmd": "deviceCmd",
  "seqId": 202,
  "reqSeqId": "e5f6a7b8-c9d0-1234-ef01-345678901234",
  "timestamp": 1737000000000,
  "respTopic": "eco1p.advantech.74fe488d5d54.subnode.cmd.rsp",
  "timeout": 300,
  "data": {
    "deviceCmd": "report.data",
    "parameters": {
      "sensorShortResourceId": "c7e4d",
      "resourceTimestamp": 1737000060000,
      "transferId": "c3d4e5f6-a7b8-9012-cdef-234567890123"
    }
  }
}
```

<!-- **Response**: Data routed to appropriate subject based on sensor type:
- Primitive → `{protoVer}.{groupId}.{deviceId}.dm.dt.rl.enr`
- JSON → `eco1j.{groupId}.{deviceId}.app.json.data.enr`
- JPEG → `eco1j.{groupId}.{deviceId}.img.jpeg.data.enr`
- PNG → `eco1j.{groupId}.{deviceId}.img.png.data.enr`
- Binary → `eco1j.{groupId}.{deviceId}.app.blob.data.enr`
- Binary → `eco1j.{groupId}.{deviceId}.app.blob.data.enr` -->

---