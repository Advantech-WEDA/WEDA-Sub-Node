// Per-DTMI value validators that mirror the `comment` field of each Telemetry
// in the WEDA System-Agent DTDL Interfaces. Keep in sync with the .NET
// `TelemetryValueValidator` classes in the *_USAGE.md docs.
//
// Each predicate takes the wire `Value` and returns true iff the value
// satisfies the rule documented in the matching Telemetry's `comment`.

const EPOCH_MIN = 1262304000;   // 2010-01-01 UTC
const EPOCH_MAX = 4102444800;   // 2100-01-01 UTC

const isFiniteNumber = v => typeof v === 'number' && Number.isFinite(v);
const isNonNegativeNumber = v => isFiniteNumber(v) && v >= 0;
const isNonNegativeInt = v => Number.isInteger(v) && v >= 0;
const isBool = v => typeof v === 'boolean';
const isNonEmptyString = v => typeof v === 'string' && v.length > 0;
const isAsciiOrNull = v => v === null || (typeof v === 'string' && v.length > 0 && /^[\x20-\x7e]+$/.test(v));

module.exports = {
  // ============================================================
  // 01 CPU & Network
  // ============================================================
  'dtmi:advantech:WEDA:SystemInfo:CpuUsage;1':
    v => isFiniteNumber(v) && v >= 0 && v <= 100,
  'dtmi:advantech:WEDA:SystemInfo:CpuLoad1;1': isNonNegativeNumber,
  'dtmi:advantech:WEDA:SystemInfo:CpuLoad5;1': isNonNegativeNumber,
  'dtmi:advantech:WEDA:SystemInfo:CpuLoad15;1': isNonNegativeNumber,
  'dtmi:advantech:WEDA:SystemInfo:CpuContextSwitches;1': isNonNegativeInt,

  'dtmi:advantech:WEDA:SystemInfo:NetworkBytesSent;1':      isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkBytesReceived;1':  isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkPacketsSent;1':    isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkPacketsReceived;1':isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkErrors;1':         isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkErrorsIn;1':       isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:NetworkErrorsOut;1':      isNonNegativeInt,

  // ============================================================
  // 02 Memory · Disk · System · GPU
  // ============================================================
  'dtmi:advantech:WEDA:SystemInfo:MemoryTotal;1':     v => Number.isInteger(v) && v > 0,
  'dtmi:advantech:WEDA:SystemInfo:MemoryAvailable;1': isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemoryUsed;1':      isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemoryFree;1':      isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemoryCached;1':    isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemoryBuffers;1':   isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemorySwapTotal;1': isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:MemorySwapFree;1':  isNonNegativeInt,

  'dtmi:advantech:WEDA:SystemInfo:DiskTotal;1':           isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskAvailable;1':       isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskFree;1':            isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskUsed;1':            isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskUsagePercent;1':    v => isFiniteNumber(v) && v >= 0 && v <= 100,
  'dtmi:advantech:WEDA:SystemInfo:DiskReadsCompleted;1':  isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskWritesCompleted;1': isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskReadBytes;1':       isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:DiskWrittenBytes;1':    isNonNegativeInt,

  'dtmi:advantech:WEDA:SystemInfo:SystemTime;1':
    v => Number.isInteger(v) && v >= EPOCH_MIN && v < EPOCH_MAX,
  'dtmi:advantech:WEDA:SystemInfo:SystemTimexOffset;1': isFiniteNumber,
  'dtmi:advantech:WEDA:SystemInfo:SystemBootTime;1':
    v => Number.isInteger(v) && v >= EPOCH_MIN && v < EPOCH_MAX,
  'dtmi:advantech:WEDA:SystemInfo:SystemFilefdAllocated;1': isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:SystemFilefdMaximum;1':   v => Number.isInteger(v) && v > 0,
  'dtmi:advantech:WEDA:SystemInfo:SystemProcsRunning;1':    isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:SystemProcsBlocked;1':    isNonNegativeInt,
  'dtmi:advantech:WEDA:SystemInfo:SystemIntrTotal;1':       isNonNegativeInt,

  'dtmi:advantech:WEDA:SystemInfo:GpuUtilization;1':
    v => Number.isInteger(v) && v >= 0 && v <= 100,

  // ============================================================
  // 03 Hardware Info
  // ============================================================
  'dtmi:advantech:WEDA:SystemInfo:HwinfoMotherboardName;1': isAsciiOrNull,
  'dtmi:advantech:WEDA:SystemInfo:HwinfoManufacturer;1':    isAsciiOrNull,
  'dtmi:advantech:WEDA:SystemInfo:HwinfoBiosRevision;1':    isAsciiOrNull,
  'dtmi:advantech:WEDA:SystemInfo:HwinfoDriverVersion;1':   isAsciiOrNull,
  'dtmi:advantech:WEDA:SystemInfo:HwinfoLibraryVersion;1':  isAsciiOrNull,
  'dtmi:advantech:WEDA:SystemInfo:HwinfoEcRevision;1':      isAsciiOrNull,

  // ============================================================
  // 04 Onboard Sensors
  // ============================================================
  'dtmi:advantech:WEDA:SystemInfo:Temperature;1':
    v => isFiniteNumber(v) && v >= -40.0 && v <= 125.0,
  'dtmi:advantech:WEDA:SystemInfo:Voltage;1':  isNonNegativeNumber,
  'dtmi:advantech:WEDA:SystemInfo:FanSpeed;1': isNonNegativeNumber,

  // ============================================================
  // 05 Hardware Features
  // ============================================================
  'dtmi:advantech:WEDA:SystemInfo:GpioIsSupported;1':              isBool,
  'dtmi:advantech:WEDA:SystemInfo:GpioPinState;1':                 v => v === 0 || v === 1,
  'dtmi:advantech:WEDA:SystemInfo:WatchdogIsSupported;1':          isBool,
  'dtmi:advantech:WEDA:SystemInfo:ThermalProtectionIsSupported;1': isBool,
};
