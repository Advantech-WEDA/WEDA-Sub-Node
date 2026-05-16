using System.Text.Json;

namespace Weda.Dtdl.Validator;

/// <summary>
/// Per-DTMI value validators that mirror the `comment` field of each Telemetry
/// in the WEDA System-Agent DTDL Interfaces. Keep in sync with
/// scripts/predicates.js (the Node.js port).
/// </summary>
public static class Predicates
{
    private const long EpochMin = 1262304000;   // 2010-01-01 UTC
    private const long EpochMax = 4102444800;   // 2100-01-01 UTC

    public static readonly IReadOnlyDictionary<string, Func<JsonElement, bool>> Map =
        new Dictionary<string, Func<JsonElement, bool>>
        {
            // ============================================================
            // 01 CPU & Network
            // ============================================================
            ["dtmi:advantech:WEDA:SystemInfo:CpuUsage;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= 0 && d <= 100,
            ["dtmi:advantech:WEDA:SystemInfo:CpuLoad1;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:WEDA:SystemInfo:CpuLoad5;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:WEDA:SystemInfo:CpuLoad15;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:WEDA:SystemInfo:CpuContextSwitches;1"] = IsNonNegativeInt,

            ["dtmi:advantech:WEDA:SystemInfo:NetworkBytesSent;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkBytesReceived;1"]  = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkPacketsSent;1"]    = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkPacketsReceived;1"]= IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkErrors;1"]         = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkErrorsIn;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:NetworkErrorsOut;1"]      = IsNonNegativeInt,

            // ============================================================
            // 02 Memory · Disk · System · GPU
            // ============================================================
            ["dtmi:advantech:WEDA:SystemInfo:MemoryTotal;1"] = v =>
                IsInt(v, out var l) && l > 0,
            ["dtmi:advantech:WEDA:SystemInfo:MemoryAvailable;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemoryUsed;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemoryFree;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemoryCached;1"]    = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemoryBuffers;1"]   = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemorySwapTotal;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:MemorySwapFree;1"]  = IsNonNegativeInt,

            ["dtmi:advantech:WEDA:SystemInfo:DiskTotal;1"]           = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskAvailable;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskFree;1"]            = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskUsed;1"]            = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskUsagePercent;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= 0 && d <= 100,
            ["dtmi:advantech:WEDA:SystemInfo:DiskReadsCompleted;1"]  = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskWritesCompleted;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskReadBytes;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:DiskWrittenBytes;1"]    = IsNonNegativeInt,

            ["dtmi:advantech:WEDA:SystemInfo:SystemTime;1"] = v =>
                IsInt(v, out var l) && l >= EpochMin && l < EpochMax,
            ["dtmi:advantech:WEDA:SystemInfo:SystemTimexOffset;1"] = v =>
                IsFiniteNumber(v, out _),
            ["dtmi:advantech:WEDA:SystemInfo:SystemBootTime;1"] = v =>
                IsInt(v, out var l) && l >= EpochMin && l < EpochMax,
            ["dtmi:advantech:WEDA:SystemInfo:SystemFilefdAllocated;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:SystemFilefdMaximum;1"] = v =>
                IsInt(v, out var l) && l > 0,
            ["dtmi:advantech:WEDA:SystemInfo:SystemProcsRunning;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:SystemProcsBlocked;1"] = IsNonNegativeInt,
            ["dtmi:advantech:WEDA:SystemInfo:SystemIntrTotal;1"]    = IsNonNegativeInt,

            ["dtmi:advantech:WEDA:SystemInfo:GpuUtilization;1"] = v =>
                IsInt(v, out var l) && l >= 0 && l <= 100,

            // ============================================================
            // 03 Hardware Info -- non-empty ASCII or null
            // ============================================================
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoMotherboardName;1"] = IsAsciiOrNull,
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoManufacturer;1"]    = IsAsciiOrNull,
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoBiosRevision;1"]    = IsAsciiOrNull,
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoDriverVersion;1"]   = IsAsciiOrNull,
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoLibraryVersion;1"]  = IsAsciiOrNull,
            ["dtmi:advantech:WEDA:SystemInfo:HwinfoEcRevision;1"]      = IsAsciiOrNull,

            // ============================================================
            // 04 Onboard Sensors
            // ============================================================
            ["dtmi:advantech:WEDA:SystemInfo:Temperature;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= -40.0 && d <= 125.0,
            ["dtmi:advantech:WEDA:SystemInfo:Voltage;1"]  = IsNonNegativeNumber,
            ["dtmi:advantech:WEDA:SystemInfo:FanSpeed;1"] = IsNonNegativeNumber,

            // ============================================================
            // 05 Hardware Features
            // ============================================================
            ["dtmi:advantech:WEDA:SystemInfo:GpioIsSupported;1"] = IsBool,
            ["dtmi:advantech:WEDA:SystemInfo:GpioPinState;1"] = v =>
                IsInt(v, out var l) && (l == 0 || l == 1),
            ["dtmi:advantech:WEDA:SystemInfo:WatchdogIsSupported;1"]          = IsBool,
            ["dtmi:advantech:WEDA:SystemInfo:ThermalProtectionIsSupported;1"] = IsBool,
        };

    // --- helpers ---

    private static bool IsFiniteNumber(JsonElement v, out double d)
    {
        d = 0;
        if (v.ValueKind != JsonValueKind.Number) return false;
        if (!v.TryGetDouble(out d)) return false;
        return !double.IsNaN(d) && !double.IsInfinity(d);
    }

    private static bool IsNonNegativeNumber(JsonElement v) =>
        IsFiniteNumber(v, out var d) && d >= 0;

    /// <summary>
    /// Strict integer: rejects 12.5 etc. We check the raw token has no '.' or 'e'.
    /// </summary>
    private static bool IsInt(JsonElement v, out long l)
    {
        l = 0;
        if (v.ValueKind != JsonValueKind.Number) return false;
        var raw = v.GetRawText();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E')) return false;
        return v.TryGetInt64(out l);
    }

    private static bool IsNonNegativeInt(JsonElement v) =>
        IsInt(v, out var l) && l >= 0;

    private static bool IsBool(JsonElement v) =>
        v.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool IsAsciiOrNull(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Null) return true;
        if (v.ValueKind != JsonValueKind.String) return false;
        var s = v.GetString();
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
            if (c >= 128) return false;
        return true;
    }
}
