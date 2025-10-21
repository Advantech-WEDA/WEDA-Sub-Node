namespace Weda.SubNode.Simulators.Modbus;

/// <summary>
/// Configuration for Modbus TCP Simulator
/// </summary>
public class TcpModbusSimulatorConfiguration
{
    /// <summary>
    /// Section name
    /// </summary>
    public const string SectionName = nameof(TcpModbusSimulatorConfiguration);

    /// <summary>
    /// TCP connection settings
    /// </summary>
    public required TcpConnectionSettings TcpConnection { get; set; }

    /// <summary>
    /// Modbus protocol settings
    /// </summary>
    public required ModbusProtocolSettings ModbusProtocol { get; set; }

    /// <summary>
    /// Global simulation settings
    /// </summary>
    public SimulationSettings Simulation { get; set; } = new();

    /// <summary>
    /// Simulated sensors configuration
    /// </summary>
    public List<SimulatedSensor> Sensors { get; set; } = [];
}

/// <summary>
/// TCP connection settings
/// </summary>
public class TcpConnectionSettings
{
    /// <summary>
    /// IP Address to bind (default: 127.0.0.1)
    /// </summary>
    public string IpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port to listen on (default: 5020, use 502 for standard but requires admin)
    /// </summary>
    public int Port { get; set; } = 5020;
}

/// <summary>
/// Modbus protocol settings
/// </summary>
public class ModbusProtocolSettings
{
    /// <summary>
    /// Slave/Unit ID (default: 1)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Register address offset (Modbus convention: 40001-49999 for holding registers)
    /// If UseModbusAddressing=true, address 40001 maps to register 0
    /// </summary>
    public bool UseModbusAddressing { get; set; } = true;

    /// <summary>
    /// Holding register base address (default: 40001)
    /// </summary>
    public int HoldingRegisterBase { get; set; } = 40001;
}

/// <summary>
/// Global simulation settings
/// </summary>
public class SimulationSettings
{
    /// <summary>
    /// Global update interval in seconds (1-65535, overrides individual sensor UpdateIntervalSeconds if set)
    /// If null, each sensor uses its own UpdateIntervalSeconds setting
    /// </summary>
    public ushort? GlobalUpdateIntervalSeconds { get; set; }

    /// <summary>
    /// Enable/disable value changes (default: true)
    /// If false, sensors will maintain their initial values
    /// </summary>
    public bool EnableValueChanges { get; set; } = true;
}

/// <summary>
/// Simulated sensor configuration
/// </summary>
public class SimulatedSensor
{
    /// <summary>
    /// Sensor name (e.g., "Temperature", "Humidity")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Sensor type for simulation behavior
    /// </summary>
    public SensorType Type { get; set; } = SensorType.Temperature;

    /// <summary>
    /// Starting Modbus address (e.g., 40001 or 0 depending on UseModbusAddressing)
    /// </summary>
    public int StartAddress { get; set; }

    /// <summary>
    /// Number of registers (1 for UInt16, 2 for Float32/UInt32, etc.)
    /// </summary>
    public int RegisterCount { get; set; } = 2;

    /// <summary>
    /// Data type
    /// </summary>
    public SimulatedDataType DataType { get; set; } = SimulatedDataType.Float32;

    /// <summary>
    /// Unit of measurement (e.g., "°C", "%", "hPa", "V", "A")
    /// If not specified, defaults based on sensor type
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Simulation parameters
    /// </summary>
    public SensorSimulationParams SimulationParams { get; set; } = new();
}

/// <summary>
/// Sensor simulation parameters
/// </summary>
public class SensorSimulationParams
{
    /// <summary>
    /// Minimum value for simulation
    /// </summary>
    public double MinValue { get; set; }

    /// <summary>
    /// Maximum value for simulation
    /// </summary>
    public double MaxValue { get; set; }

    /// <summary>
    /// Initial value (if not set, random between min/max)
    /// </summary>
    public double? InitialValue { get; set; }

    /// <summary>
    /// How much the value can change per update (default: 0.5)
    /// </summary>
    public double ChangeRate { get; set; } = 0.5;

    /// <summary>
    /// Update interval in seconds (1-65535, default: 1 second)
    /// Can be overridden by GlobalUpdateIntervalSeconds
    /// </summary>
    public ushort UpdateIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Add random noise (default: 0.1)
    /// </summary>
    public double NoiseLevel { get; set; } = 0.1;
}

/// <summary>
/// Sensor types with realistic simulation behaviors
/// </summary>
public enum SensorType
{
    Temperature,    // 18-32°C, slow changes
    Humidity,       // 30-80%, medium changes
    Pressure,       // 980-1030 hPa, very slow changes
    Voltage,        // 220-240V, minimal changes
    Current,        // 0-10A, fast changes
    Custom          // Use MinValue/MaxValue from config
}

/// <summary>
/// Simulated data types
/// </summary>
public enum SimulatedDataType
{
    UInt16,
    Int16,
    UInt32,
    Int32,
    Float32,
    UInt64,
    Int64,
    Float64
}

/// <summary>
/// Default configurations for common sensor types
/// </summary>
public static class SensorDefaults
{
    public static SensorSimulationParams Temperature => new()
    {
        MinValue = 18.0,
        MaxValue = 32.0,
        ChangeRate = 0.2,
        UpdateIntervalSeconds = 10,
        NoiseLevel = 0.1
    };

    public static SensorSimulationParams Humidity => new()
    {
        MinValue = 30.0,
        MaxValue = 80.0,
        ChangeRate = 0.5,
        UpdateIntervalSeconds = 10,
        NoiseLevel = 0.2
    };

    public static SensorSimulationParams Pressure => new()
    {
        MinValue = 980.0,
        MaxValue = 1030.0,
        ChangeRate = 0.1,
        UpdateIntervalSeconds = 10,
        NoiseLevel = 0.05
    };

    public static SensorSimulationParams Voltage => new()
    {
        MinValue = 220.0,
        MaxValue = 240.0,
        ChangeRate = 0.5,
        UpdateIntervalSeconds = 10,
        NoiseLevel = 0.3
    };

    public static SensorSimulationParams Current => new()
    {
        MinValue = 0.0,
        MaxValue = 10.0,
        ChangeRate = 1.0,
        UpdateIntervalSeconds = 10,
        NoiseLevel = 0.2
    };

    public static SensorSimulationParams GetDefaults(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => Temperature,
            SensorType.Humidity => Humidity,
            SensorType.Pressure => Pressure,
            SensorType.Voltage => Voltage,
            SensorType.Current => Current,
            _ => new SensorSimulationParams()
        };
    }

    public static string GetDefaultUnit(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => "°C",
            SensorType.Humidity => "%",
            SensorType.Pressure => "hPa",
            SensorType.Voltage => "V",
            SensorType.Current => "A",
            _ => ""
        };
    }
}
