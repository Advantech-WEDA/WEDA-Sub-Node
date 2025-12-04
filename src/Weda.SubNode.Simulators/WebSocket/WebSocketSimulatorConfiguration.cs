namespace Weda.SubNode.Simulators.WebSocket;

/// <summary>
/// Configuration for WebSocket Simulator
/// </summary>
public class WebSocketSimulatorConfiguration
{
    /// <summary>
    /// Section name for configuration binding
    /// </summary>
    public const string SectionName = nameof(WebSocketSimulatorConfiguration);

    /// <summary>
    /// WebSocket server settings
    /// </summary>
    public WebSocketServerSettings Server { get; set; } = new();

    /// <summary>
    /// Global simulation settings
    /// </summary>
    public WebSocketSimulationSettings Simulation { get; set; } = new();

    /// <summary>
    /// Simulated sensors configuration
    /// </summary>
    public List<WebSocketSimulatedSensor> Sensors { get; set; } = [];
}

/// <summary>
/// WebSocket server settings
/// </summary>
public class WebSocketServerSettings
{
    /// <summary>
    /// Host address to bind (default: localhost)
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port to listen on (default: 8080)
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// WebSocket path (default: /stream)
    /// </summary>
    public string Path { get; set; } = "/stream";

    /// <summary>
    /// Maximum concurrent connections (default: 100)
    /// </summary>
    public int MaxConnections { get; set; } = 100;
}

/// <summary>
/// WebSocket simulation settings
/// </summary>
public class WebSocketSimulationSettings
{
    /// <summary>
    /// Global broadcast interval in milliseconds (default: 1000ms)
    /// </summary>
    public int BroadcastIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Enable/disable value changes (default: true)
    /// If false, sensors will maintain their initial values
    /// </summary>
    public bool EnableValueChanges { get; set; } = true;

    /// <summary>
    /// Message format: json or binary (default: json)
    /// </summary>
    public MessageFormat Format { get; set; } = MessageFormat.Json;

    /// <summary>
    /// Include timestamp in messages (default: true)
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;
}

/// <summary>
/// Message format enum
/// </summary>
public enum MessageFormat
{
    Json,
    Binary
}

/// <summary>
/// WebSocket simulated sensor configuration
/// </summary>
public class WebSocketSimulatedSensor
{
    /// <summary>
    /// Sensor name (e.g., "temperature1", "humidity1")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Sensor type for simulation behavior
    /// </summary>
    public WebSocketSensorType Type { get; set; } = WebSocketSensorType.Temperature;

    /// <summary>
    /// Unit of measurement (e.g., "°C", "%RH", "hPa")
    /// If not specified, defaults based on sensor type
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Simulation parameters
    /// </summary>
    public WebSocketSensorSimulationParams SimulationParams { get; set; } = new();
}

/// <summary>
/// WebSocket sensor simulation parameters
/// </summary>
public class WebSocketSensorSimulationParams
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
    /// Add random noise (default: 0.1)
    /// </summary>
    public double NoiseLevel { get; set; } = 0.1;

    /// <summary>
    /// Quality indicator: always "good", random "uncertain", or "bad" on error
    /// </summary>
    public double UncertainProbability { get; set; } = 0.05;
}

/// <summary>
/// WebSocket sensor types with realistic simulation behaviors
/// </summary>
public enum WebSocketSensorType
{
    Temperature,    // 18-35°C, slow changes
    Humidity,       // 30-80%, medium changes
    Pressure,       // 980-1030 hPa, very slow changes
    Voltage,        // 215-245V, minimal changes
    Current,        // 0-10A, fast changes
    Vibration,      // 0-5g, fast changes
    Custom          // Use MinValue/MaxValue from config
}

/// <summary>
/// Default configurations for WebSocket sensor types
/// </summary>
public static class WebSocketSensorDefaults
{
    public static WebSocketSensorSimulationParams Temperature => new()
    {
        MinValue = 18.0,
        MaxValue = 35.0,
        ChangeRate = 0.3,
        NoiseLevel = 0.1,
        UncertainProbability = 0.02
    };

    public static WebSocketSensorSimulationParams Humidity => new()
    {
        MinValue = 30.0,
        MaxValue = 80.0,
        ChangeRate = 0.5,
        NoiseLevel = 0.2,
        UncertainProbability = 0.02
    };

    public static WebSocketSensorSimulationParams Pressure => new()
    {
        MinValue = 980.0,
        MaxValue = 1030.0,
        ChangeRate = 0.2,
        NoiseLevel = 0.1,
        UncertainProbability = 0.01
    };

    public static WebSocketSensorSimulationParams Voltage => new()
    {
        MinValue = 215.0,
        MaxValue = 245.0,
        ChangeRate = 0.8,
        NoiseLevel = 0.5,
        UncertainProbability = 0.02
    };

    public static WebSocketSensorSimulationParams Current => new()
    {
        MinValue = 0.0,
        MaxValue = 10.0,
        ChangeRate = 0.5,
        NoiseLevel = 0.2,
        UncertainProbability = 0.03
    };

    public static WebSocketSensorSimulationParams Vibration => new()
    {
        MinValue = 0.0,
        MaxValue = 5.0,
        ChangeRate = 0.3,
        NoiseLevel = 0.15,
        UncertainProbability = 0.05
    };

    public static WebSocketSensorSimulationParams GetDefaults(WebSocketSensorType type)
    {
        return type switch
        {
            WebSocketSensorType.Temperature => Temperature,
            WebSocketSensorType.Humidity => Humidity,
            WebSocketSensorType.Pressure => Pressure,
            WebSocketSensorType.Voltage => Voltage,
            WebSocketSensorType.Current => Current,
            WebSocketSensorType.Vibration => Vibration,
            _ => new WebSocketSensorSimulationParams()
        };
    }

    public static string GetDefaultUnit(WebSocketSensorType type)
    {
        return type switch
        {
            WebSocketSensorType.Temperature => "°C",
            WebSocketSensorType.Humidity => "%RH",
            WebSocketSensorType.Pressure => "hPa",
            WebSocketSensorType.Voltage => "V",
            WebSocketSensorType.Current => "A",
            WebSocketSensorType.Vibration => "g",
            _ => ""
        };
    }
}
