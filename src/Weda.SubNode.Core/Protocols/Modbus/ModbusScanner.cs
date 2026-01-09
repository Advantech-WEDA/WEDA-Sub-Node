using System.Text;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Result of scanning a single register
/// </summary>
public record ModbusScanResult
{
    public ushort Address { get; init; }
    public ushort[] RawValues { get; init; } = [];
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Dictionary<ModbusDataType, object?> ParsedValues { get; init; } = new();
}

/// <summary>
/// Configuration for Modbus scanning
/// </summary>
public record ModbusScanConfig
{
    /// <summary>
    /// Starting register address (default: 0)
    /// </summary>
    public ushort StartAddress { get; init; } = 0;

    /// <summary>
    /// Ending register address (default: 99)
    /// </summary>
    public ushort EndAddress { get; init; } = 99;

    /// <summary>
    /// Number of registers to read per scan (default: 4 for Float64 support)
    /// </summary>
    public ushort RegistersPerScan { get; init; } = 4;

    /// <summary>
    /// Delay between scans in milliseconds (default: 100ms)
    /// </summary>
    public int DelayBetweenScans { get; init; } = 100;

    /// <summary>
    /// Data types to attempt parsing (default: all types)
    /// </summary>
    public ModbusDataType[] DataTypesToTest { get; init; } =
    [
        ModbusDataType.UInt16,
        ModbusDataType.Int16,
        ModbusDataType.UInt32,
        ModbusDataType.Int32,
        ModbusDataType.Float32,
        ModbusDataType.Float64
    ];
}

/// <summary>
/// Utility class for scanning Modbus registers to discover device capabilities
/// </summary>
public class ModbusScanner
{
    private readonly IRequestResponseCommunication<byte[], byte[]> _communication;
    private readonly byte _slaveId;
    private readonly ILogger? _logger;
    private ushort _transactionId = 0;

    public ModbusScanner(
        IRequestResponseCommunication<byte[], byte[]> communication,
        byte slaveId,
        ILogger? logger = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _slaveId = slaveId;
        _logger = logger;
    }

    /// <summary>
    /// Scan a range of Modbus holding registers and attempt to parse them as different data types
    /// </summary>
    public async Task<List<ModbusScanResult>> ScanHoldingRegistersAsync(
        ModbusScanConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new ModbusScanConfig();
        var results = new List<ModbusScanResult>();

        _logger?.LogInformation(
            "Starting Modbus scan: Address {Start}-{End}, SlaveId={SlaveId}, RegistersPerScan={Count}",
            config.StartAddress,
            config.EndAddress,
            _slaveId,
            config.RegistersPerScan);

        for (ushort address = config.StartAddress; address <= config.EndAddress; address++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Read registers
                var rawValues = await ReadModbusRegistersAsync(
                    address,
                    config.RegistersPerScan,
                    cancellationToken);

                // Try parsing as different data types
                var parsedValues = new Dictionary<ModbusDataType, object?>();
                foreach (var dataType in config.DataTypesToTest)
                {
                    try
                    {
                        var parser = new ModbusProtocolParser(dataType);
                        var requiredRegisters = GetRequiredRegisters(dataType);

                        // Skip if we don't have enough registers
                        if (rawValues.Length < requiredRegisters)
                            continue;

                        var registersToUse = rawValues.Take(requiredRegisters).ToArray();
                        var parsed = parser.Parse(registersToUse);
                        parsedValues[dataType] = parsed;
                    }
                    catch
                    {
                        // Parsing failed for this data type
                        parsedValues[dataType] = null;
                    }
                }

                var result = new ModbusScanResult
                {
                    Address = address,
                    RawValues = rawValues,
                    Success = true,
                    ParsedValues = parsedValues
                };

                results.Add(result);

                _logger?.LogDebug(
                    "Address {Address:D3}: Raw=[{Raw}] | UInt16={U16} Int16={I16} Float32={F32}",
                    address,
                    string.Join(",", rawValues.Select(r => r.ToString("X4"))),
                    parsedValues.GetValueOrDefault(ModbusDataType.UInt16),
                    parsedValues.GetValueOrDefault(ModbusDataType.Int16),
                    parsedValues.GetValueOrDefault(ModbusDataType.Float32));
            }
            catch (Exception ex)
            {
                var result = new ModbusScanResult
                {
                    Address = address,
                    Success = false,
                    Error = ex.Message
                };

                results.Add(result);
                _logger?.LogTrace("[ERR] Address {Address:D3}: {Error}", address, ex.Message);
            }

            // Delay between scans to avoid overwhelming the device
            if (config.DelayBetweenScans > 0)
                await Task.Delay(config.DelayBetweenScans, cancellationToken);
        }

        _logger?.LogInformation(
            "Scan complete: {SuccessCount}/{TotalCount} addresses responded",
            results.Count(r => r.Success),
            results.Count);

        return results;
    }

    /// <summary>
    /// Generate sensor configuration suggestions based on scan results
    /// </summary>
    public List<SensorSuggestion> GenerateSensorSuggestions(List<ModbusScanResult> scanResults)
    {
        var suggestions = new List<SensorSuggestion>();

        foreach (var result in scanResults.Where(r => r.Success))
        {
            // Skip addresses with all zero values (likely unused)
            if (result.RawValues.All(r => r == 0))
                continue;

            // Try to suggest the most likely data type
            var detectedType = DetectMostLikelyDataType(result);
            var suggestion = new SensorSuggestion
            {
                RegisterAddress = result.Address,
                SuggestedName = $"sensor.{result.Address}",
                SuggestedDataType = detectedType,
                RegisterCount = (ushort)GetRequiredRegisters(detectedType),
                RawValues = result.RawValues,
                ParsedValues = result.ParsedValues
            };

            suggestions.Add(suggestion);
        }

        return suggestions;
    }

    /// <summary>
    /// Print scan results in a human-readable format
    /// </summary>
    public void PrintScanResults(List<ModbusScanResult> results, ILogger? logger = null)
    {
        var log = logger ?? _logger;
        if (log == null) return;

        log.LogInformation("\n" + "=".PadRight(80, '='));
        log.LogInformation("Modbus Register Scan Results");
        log.LogInformation("=".PadRight(80, '='));

        foreach (var result in results.Where(r => r.Success))
        {
            // Skip addresses with all zero values
            if (result.RawValues.All(r => r == 0))
                continue;

            log.LogInformation("\nAddress {Address:D3} (0x{Address:X4}):", result.Address, result.Address);
            log.LogInformation("  Raw Hex:  [{Raw}]", string.Join(", ", result.RawValues.Select(r => $"0x{r:X4}")));
            log.LogInformation("  Raw Dec:  [{Raw}]", string.Join(", ", result.RawValues));

            foreach (var (dataType, value) in result.ParsedValues.Where(kv => kv.Value != null))
            {
                log.LogInformation("  {Type,-10}: {Value}", dataType.ToString(), value);
            }
        }

        log.LogInformation("\n" + "=".PadRight(80, '='));
        log.LogInformation("Summary: {Count} registers with non-zero data",
            results.Count(r => r.Success && r.RawValues.Any(v => v != 0)));
        log.LogInformation("=".PadRight(80, '=') + "\n");
    }

    /// <summary>
    /// Generate a comprehensive Markdown report of scan results
    /// </summary>
    /// <param name="results">Scan results to include in report</param>
    /// <param name="config">Scan configuration used</param>
    /// <param name="deviceInfo">Optional device information (host, port, slaveId)</param>
    /// <returns>Markdown formatted report string</returns>
    public string GenerateMarkdownReport(
        List<ModbusScanResult> results,
        ModbusScanConfig config,
        Dictionary<string, object>? deviceInfo = null)
    {
        var sb = new StringBuilder();
        var scanTime = DateTime.Now;

        // Header
        sb.AppendLine("# Modbus Register Scan Report");
        sb.AppendLine();
        sb.AppendLine($"**Scan Date**: {scanTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Device Information
        sb.AppendLine("## Device Information");
        sb.AppendLine();
        if (deviceInfo != null)
        {
            if (deviceInfo.TryGetValue("Host", out var host))
                sb.AppendLine($"- **Host**: `{host}`");
            if (deviceInfo.TryGetValue("Port", out var port))
                sb.AppendLine($"- **Port**: `{port}`");
        }
        sb.AppendLine($"- **Slave ID**: `{_slaveId}`");
        sb.AppendLine();

        // Scan Configuration
        sb.AppendLine("## Scan Configuration");
        sb.AppendLine();
        sb.AppendLine($"- **Address Range**: {config.StartAddress} - {config.EndAddress}");
        sb.AppendLine($"- **Registers Per Scan**: {config.RegistersPerScan}");
        sb.AppendLine($"- **Delay Between Scans**: {config.DelayBetweenScans}ms");
        sb.AppendLine($"- **Data Types Tested**: {string.Join(", ", config.DataTypesToTest)}");
        sb.AppendLine();

        // Summary Statistics
        var successCount = results.Count(r => r.Success);
        var nonZeroCount = results.Count(r => r.Success && r.RawValues.Any(v => v != 0));
        var validSensors = results.Where(r => r.Success && IsValidSensor(r)).ToList();

        sb.AppendLine("## Scan Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Addresses Scanned**: {results.Count}");
        sb.AppendLine($"- **Successful Reads**: {successCount}");
        sb.AppendLine($"- **Addresses with Non-Zero Data**: {nonZeroCount}");
        sb.AppendLine($"- **Valid Sensors Detected**: {validSensors.Count}");
        sb.AppendLine($"- **Success Rate**: {(successCount * 100.0 / results.Count):F1}%");
        sb.AppendLine();

        // Validation Rules
        sb.AppendLine("## Sensor Validation Rules");
        sb.AppendLine();
        sb.AppendLine("A register is considered a **valid sensor** if it meets ALL of the following criteria:");
        sb.AppendLine();
        sb.AppendLine("1. [OK] **Read Success**: Register responds without errors");
        sb.AppendLine("2. [OK] **Non-Zero Data**: At least one register value is non-zero");
        sb.AppendLine("3. [OK] **Valid Float32**: If parsed as Float32:");
        sb.AppendLine("   - Not `NaN` (Not a Number)");
        sb.AppendLine("   - Not `Infinity` or `-Infinity`");
        sb.AppendLine("   - Absolute value < 10,000 (reasonable sensor range)");
        sb.AppendLine("4. [OK] **Stable Pattern**: Raw values show recognizable data pattern (not random noise)");
        sb.AppendLine();

        // Detected Valid Sensors
        if (validSensors.Count > 0)
        {
            sb.AppendLine("## Detected Valid Sensors");
            sb.AppendLine();
            sb.AppendLine("| Address | Hex | Decimal | UInt16 | Int16 | Float32 | Suggested Type |");
            sb.AppendLine("|---------|-----|---------|--------|-------|---------|----------------|");

            foreach (var sensor in validSensors)
            {
                var hex = string.Join(", ", sensor.RawValues.Select(r => $"0x{r:X4}"));
                var dec = string.Join(", ", sensor.RawValues);
                var uint16 = sensor.ParsedValues.GetValueOrDefault(ModbusDataType.UInt16)?.ToString() ?? "N/A";
                var int16 = sensor.ParsedValues.GetValueOrDefault(ModbusDataType.Int16)?.ToString() ?? "N/A";
                var float32 = sensor.ParsedValues.GetValueOrDefault(ModbusDataType.Float32) is float f
                    ? $"{f:F2}"
                    : "N/A";
                var suggested = DetectMostLikelyDataType(sensor);

                sb.AppendLine($"| {sensor.Address} | {hex} | {dec} | {uint16} | {int16} | {float32} | **{suggested}** |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Detected Valid Sensors");
            sb.AppendLine();
            sb.AppendLine("[WARN] **No valid sensors detected**");
            sb.AppendLine();
        }

        // Detailed Register Data
        sb.AppendLine("## Detailed Register Data");
        sb.AppendLine();

        var nonZeroResults = results.Where(r => r.Success && r.RawValues.Any(v => v != 0)).ToList();
        if (nonZeroResults.Count > 0)
        {
            foreach (var result in nonZeroResults)
            {
                sb.AppendLine($"### Address {result.Address} (0x{result.Address:X4})");
                sb.AppendLine();
                sb.AppendLine($"**Raw Values**:");
                sb.AppendLine($"- Hex: `{string.Join(", ", result.RawValues.Select(r => $"0x{r:X4}"))}`");
                sb.AppendLine($"- Decimal: `{string.Join(", ", result.RawValues)}`");
                sb.AppendLine($"- Binary: `{string.Join(", ", result.RawValues.Select(r => Convert.ToString(r, 2).PadLeft(16, '0')))}`");
                sb.AppendLine();

                sb.AppendLine("**Parsed Values**:");
                foreach (var (dataType, value) in result.ParsedValues.Where(kv => kv.Value != null))
                {
                    var validation = IsValidParsedValue(dataType, value!) ? "[OK]" : "[ERR]";
                    sb.AppendLine($"- {validation} **{dataType}**: `{value}`");
                }
                sb.AppendLine();

                sb.AppendLine($"**Validation**: {(IsValidSensor(result) ? "[OK] Valid Sensor" : "[ERR] Not a Valid Sensor")}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("[WARN] No registers with non-zero data found.");
            sb.AppendLine();
        }

        // Failed Reads
        var failedResults = results.Where(r => !r.Success).ToList();
        if (failedResults.Count > 0)
        {
            sb.AppendLine("## Failed Reads");
            sb.AppendLine();
            sb.AppendLine("| Address | Error |");
            sb.AppendLine("|---------|-------|");
            foreach (var failed in failedResults.Take(20)) // Limit to first 20 to avoid huge reports
            {
                sb.AppendLine($"| {failed.Address} | {failed.Error} |");
            }
            if (failedResults.Count > 20)
            {
                sb.AppendLine($"|  | ... and {failedResults.Count - 20} more |");
            }
            sb.AppendLine();
        }

        // Suggested Configuration
        var suggestions = GenerateSensorSuggestions(results);
        if (suggestions.Count > 0)
        {
            sb.AppendLine("## Suggested Sensor Configuration");
            sb.AppendLine();
            sb.AppendLine("Copy the following to your `appsettings.json`:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("\"Sensors\": [");

            for (int i = 0; i < suggestions.Count; i++)
            {
                var s = suggestions[i];
                var comma = i < suggestions.Count - 1 ? "," : "";

                sb.AppendLine("  {");
                sb.AppendLine($"    \"Name\": \"{s.SuggestedName}\",");
                sb.AppendLine($"    \"Dtmi\": \"dtmi:advantech:EdgeSync:Sensor;1\",");
                sb.AppendLine("    \"Parameters\": {");
                sb.AppendLine("      \"RegisterType\": \"HoldingRegister\",");
                sb.AppendLine($"      \"RegisterAddress\": {s.RegisterAddress},");
                sb.AppendLine($"      \"RegisterCount\": {s.RegisterCount},");
                sb.AppendLine($"      \"DataType\": \"{s.SuggestedDataType}\"");
                sb.AppendLine("    },");
                sb.AppendLine("    \"Config\": {");
                sb.AppendLine("      \"Enabled\": true,");
                sb.AppendLine("      \"Interval\": 1000");
                sb.AppendLine("    }");
                sb.AppendLine($"  }}{comma}");
            }

            sb.AppendLine("]");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"*Report generated by Weda SubNode Modbus Scanner at {scanTime:yyyy-MM-dd HH:mm:ss}*");

        return sb.ToString();
    }

    /// <summary>
    /// Determine if a scan result represents a valid sensor
    /// </summary>
    private static bool IsValidSensor(ModbusScanResult result)
    {
        // Rule 1: Must be successful read
        if (!result.Success)
            return false;

        // Rule 2: Must have non-zero data
        if (result.RawValues.All(r => r == 0))
            return false;

        // Rule 3: Must have at least one valid parsed value
        var hasValidValue = result.ParsedValues
            .Where(kv => kv.Value != null)
            .Any(kv => IsValidParsedValue(kv.Key, kv.Value!));

        return hasValidValue;
    }

    /// <summary>
    /// Validate if a parsed value is reasonable for its data type
    /// </summary>
    private static bool IsValidParsedValue(ModbusDataType dataType, object value)
    {
        return dataType switch
        {
            ModbusDataType.Float32 when value is float f32 =>
                !float.IsNaN(f32) && !float.IsInfinity(f32) && Math.Abs(f32) < 10000,

            ModbusDataType.Float64 when value is double f64 =>
                !double.IsNaN(f64) && !double.IsInfinity(f64) && Math.Abs(f64) < 10000,

            ModbusDataType.UInt16 => true,
            ModbusDataType.Int16 => true,
            ModbusDataType.UInt32 => true,
            ModbusDataType.Int32 => true,
            ModbusDataType.UInt64 => true,
            ModbusDataType.Int64 => true,

            _ => false
        };
    }

    private async Task<ushort[]> ReadModbusRegistersAsync(
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        var request = BuildModbusRequest(0x03, startAddress, count);
        var response = await _communication.RequestAsync(request, cancellationToken);
        return ParseModbusResponse(response, count);
    }

    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort count)
    {
        var transactionId = ++_transactionId;

        return
        [
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            _slaveId,
            functionCode,
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),
            (byte)(count >> 8), (byte)(count & 0xFF)
        ];
    }

    private static ushort[] ParseModbusResponse(byte[] response, ushort expectedCount)
    {
        if (response.Length < 9)
            throw new InvalidOperationException($"Invalid Modbus response length: {response.Length}");

        var byteCount = response[8];
        var expectedByteCount = expectedCount * 2;

        if (byteCount != expectedByteCount)
            throw new InvalidOperationException($"Unexpected byte count: {byteCount}, expected: {expectedByteCount}");

        var registers = new ushort[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            var offset = 9 + (i * 2);
            registers[i] = (ushort)((response[offset] << 8) | response[offset + 1]);
        }

        return registers;
    }

    private static int GetRequiredRegisters(ModbusDataType dataType)
    {
        return dataType switch
        {
            ModbusDataType.UInt16 => 1,
            ModbusDataType.Int16 => 1,
            ModbusDataType.UInt32 => 2,
            ModbusDataType.Int32 => 2,
            ModbusDataType.Float32 => 2,
            ModbusDataType.UInt64 => 4,
            ModbusDataType.Int64 => 4,
            ModbusDataType.Float64 => 4,
            ModbusDataType.String16 => 16,
            _ => 1
        };
    }

    private static ModbusDataType DetectMostLikelyDataType(ModbusScanResult result)
    {
        // Heuristic: Try to detect the most likely data type based on the values

        // If Float32 produces a reasonable value (not NaN, not Infinity, reasonable range)
        if (result.ParsedValues.TryGetValue(ModbusDataType.Float32, out var float32) && float32 is float f32)
        {
            if (!float.IsNaN(f32) && !float.IsInfinity(f32) && Math.Abs(f32) < 10000)
                return ModbusDataType.Float32;
        }

        // If UInt32 is reasonable
        if (result.ParsedValues.TryGetValue(ModbusDataType.UInt32, out var uint32) && uint32 is uint u32)
        {
            if (u32 < 1_000_000)
                return ModbusDataType.UInt32;
        }

        // Default to UInt16
        return ModbusDataType.UInt16;
    }
}

/// <summary>
/// Suggested sensor configuration based on scan results
/// </summary>
public record SensorSuggestion
{
    public ushort RegisterAddress { get; init; }
    public string SuggestedName { get; init; } = string.Empty;
    public ModbusDataType SuggestedDataType { get; init; }
    public ushort RegisterCount { get; init; }
    public ushort[] RawValues { get; init; } = [];
    public Dictionary<ModbusDataType, object?> ParsedValues { get; init; } = new();
}
