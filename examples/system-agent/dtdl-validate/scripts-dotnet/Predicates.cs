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
            ["dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= 0 && d <= 100,
            ["dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:EdgeSync:SystemInfo:CpuLoad5;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:EdgeSync:SystemInfo:CpuLoad15;1"] = IsNonNegativeNumber,
            ["dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1"] = IsNonNegativeInt,

            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesReceived;1"]  = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsSent;1"]    = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsReceived;1"]= IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkErrors;1"]         = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsIn;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsOut;1"]      = IsNonNegativeInt,

            // ============================================================
            // 02 Memory · Disk · System · GPU
            // ============================================================
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryTotal;1"] = v =>
                IsInt(v, out var l) && l > 0,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryAvailable;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryUsed;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryFree;1"]      = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryCached;1"]    = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemoryBuffers;1"]   = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemorySwapTotal;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:MemorySwapFree;1"]  = IsNonNegativeInt,

            ["dtmi:advantech:EdgeSync:SystemInfo:DiskTotal;1"]           = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskAvailable;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskFree;1"]            = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskUsed;1"]            = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskUsagePercent;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= 0 && d <= 100,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskReadsCompleted;1"]  = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskWritesCompleted;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskReadBytes;1"]       = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:DiskWrittenBytes;1"]    = IsNonNegativeInt,

            ["dtmi:advantech:EdgeSync:SystemInfo:SystemTime;1"] = v =>
                IsInt(v, out var l) && l >= EpochMin && l < EpochMax,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemTimexOffset;1"] = v =>
                IsFiniteNumber(v, out _),
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemBootTime;1"] = v =>
                IsInt(v, out var l) && l >= EpochMin && l < EpochMax,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdAllocated;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdMaximum;1"] = v =>
                IsInt(v, out var l) && l > 0,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemProcsRunning;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemProcsBlocked;1"] = IsNonNegativeInt,
            ["dtmi:advantech:EdgeSync:SystemInfo:SystemIntrTotal;1"]    = IsNonNegativeInt,

            ["dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1"] = v =>
                IsInt(v, out var l) && l >= 0 && l <= 100,

            // ============================================================
            // 03 Hardware Info -- non-empty ASCII or null
            // ============================================================
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoMotherboardName;1"] = IsAsciiOrNull,
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoManufacturer;1"]    = IsAsciiOrNull,
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoBiosRevision;1"]    = IsAsciiOrNull,
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoDriverVersion;1"]   = IsAsciiOrNull,
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoLibraryVersion;1"]  = IsAsciiOrNull,
            ["dtmi:advantech:EdgeSync:SystemInfo:HwinfoEcRevision;1"]      = IsAsciiOrNull,

            // ============================================================
            // 04 Onboard Sensors
            // ============================================================
            ["dtmi:advantech:EdgeSync:SystemInfo:Temperature;1"] = v =>
                IsFiniteNumber(v, out var d) && d >= -40.0 && d <= 125.0,
            ["dtmi:advantech:EdgeSync:SystemInfo:Voltage;1"]  = IsNonNegativeNumber,
            ["dtmi:advantech:EdgeSync:SystemInfo:FanSpeed;1"] = IsNonNegativeNumber,

            // ============================================================
            // 05 Hardware Features
            // ============================================================
            ["dtmi:advantech:EdgeSync:SystemInfo:GpioIsSupported;1"] = IsBool,
            ["dtmi:advantech:EdgeSync:SystemInfo:GpioPinState;1"] = v =>
                IsInt(v, out var l) && (l == 0 || l == 1),
            ["dtmi:advantech:EdgeSync:SystemInfo:WatchdogIsSupported;1"]          = IsBool,
            ["dtmi:advantech:EdgeSync:SystemInfo:ThermalProtectionIsSupported;1"] = IsBool,
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
