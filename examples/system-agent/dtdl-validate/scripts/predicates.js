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
  'dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1':
    v => isFiniteNumber(v) && v >= 0 && v <= 100,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1': isNonNegativeNumber,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad5;1': isNonNegativeNumber,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuLoad15;1': isNonNegativeNumber,
  'dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1': isNonNegativeInt,

  'dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1':      isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesReceived;1':  isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsSent;1':    isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsReceived;1':isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkErrors;1':         isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsIn;1':       isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsOut;1':      isNonNegativeInt,

  // ============================================================
  // 02 Memory · Disk · System · GPU
  // ============================================================
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryTotal;1':     v => Number.isInteger(v) && v > 0,
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryAvailable;1': isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryUsed;1':      isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryFree;1':      isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryCached;1':    isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemoryBuffers;1':   isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemorySwapTotal;1': isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:MemorySwapFree;1':  isNonNegativeInt,

  'dtmi:advantech:EdgeSync:SystemInfo:DiskTotal;1':           isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskAvailable;1':       isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskFree;1':            isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskUsed;1':            isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskUsagePercent;1':    v => isFiniteNumber(v) && v >= 0 && v <= 100,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskReadsCompleted;1':  isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskWritesCompleted;1': isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskReadBytes;1':       isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:DiskWrittenBytes;1':    isNonNegativeInt,

  'dtmi:advantech:EdgeSync:SystemInfo:SystemTime;1':
    v => Number.isInteger(v) && v >= EPOCH_MIN && v < EPOCH_MAX,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemTimexOffset;1': isFiniteNumber,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemBootTime;1':
    v => Number.isInteger(v) && v >= EPOCH_MIN && v < EPOCH_MAX,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdAllocated;1': isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdMaximum;1':   v => Number.isInteger(v) && v > 0,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemProcsRunning;1':    isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemProcsBlocked;1':    isNonNegativeInt,
  'dtmi:advantech:EdgeSync:SystemInfo:SystemIntrTotal;1':       isNonNegativeInt,

  'dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1':
    v => Number.isInteger(v) && v >= 0 && v <= 100,

  // ============================================================
  // 03 Hardware Info
  // ============================================================
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoMotherboardName;1': isAsciiOrNull,
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoManufacturer;1':    isAsciiOrNull,
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoBiosRevision;1':    isAsciiOrNull,
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoDriverVersion;1':   isAsciiOrNull,
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoLibraryVersion;1':  isAsciiOrNull,
  'dtmi:advantech:EdgeSync:SystemInfo:HwinfoEcRevision;1':      isAsciiOrNull,

  // ============================================================
  // 04 Onboard Sensors
  // ============================================================
  'dtmi:advantech:EdgeSync:SystemInfo:Temperature;1':
    v => isFiniteNumber(v) && v >= -40.0 && v <= 125.0,
  'dtmi:advantech:EdgeSync:SystemInfo:Voltage;1':  isNonNegativeNumber,
  'dtmi:advantech:EdgeSync:SystemInfo:FanSpeed;1': isNonNegativeNumber,

  // ============================================================
  // 05 Hardware Features
  // ============================================================
  'dtmi:advantech:EdgeSync:SystemInfo:GpioIsSupported;1':              isBool,
  'dtmi:advantech:EdgeSync:SystemInfo:GpioPinState;1':                 v => v === 0 || v === 1,
  'dtmi:advantech:EdgeSync:SystemInfo:WatchdogIsSupported;1':          isBool,
  'dtmi:advantech:EdgeSync:SystemInfo:ThermalProtectionIsSupported;1': isBool,
};
