using Weda.SubNode.Simulators.Modbus;

namespace Weda.SubNode.Simulators.OpcUa;

/// <summary>
/// Configuration for OPC-UA Server Simulator
/// </summary>
public class OpcUaSimulatorConfiguration
{
    /// <summary>
    /// Section name for configuration binding
    /// </summary>
    public const string SectionName = nameof(OpcUaSimulatorConfiguration);

    /// <summary>
    /// OPC-UA server settings
    /// </summary>
    public required OpcUaServerSettings Server { get; set; }

    /// <summary>
    /// Global simulation settings (reuses Modbus SimulationSettings)
    /// </summary>
    public SimulationSettings Simulation { get; set; } = new();

    /// <summary>
    /// Simulated OPC-UA nodes configuration
    /// </summary>
    public List<SimulatedOpcUaNode> Nodes { get; set; } = [];
}

/// <summary>
/// OPC-UA server settings
/// </summary>
public class OpcUaServerSettings
{
    /// <summary>
    /// Port to listen on (default: 4840, standard OPC-UA port)
    /// </summary>
    public int Port { get; set; } = 4840;

    /// <summary>
    /// Server name used in the endpoint URL (default: "WedaOpcUaSimulator")
    /// Endpoint URL will be: opc.tcp://localhost:{Port}/{ServerName}
    /// </summary>
    public string ServerName { get; set; } = "WedaOpcUaSimulator";
}

/// <summary>
/// Simulated OPC-UA node configuration
/// </summary>
public class SimulatedOpcUaNode
{
    /// <summary>
    /// Node display name (e.g., "TemperatureSensor")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// OPC-UA string node identifier (e.g., "Temperature")
    /// Will be placed in namespace index 2
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Sensor type for simulation behavior (reuses Modbus SensorType)
    /// </summary>
    public SensorType Type { get; set; } = SensorType.Temperature;

    /// <summary>
    /// Data type for the node value
    /// </summary>
    public SimulatedDataType DataType { get; set; } = SimulatedDataType.Float32;

    /// <summary>
    /// Unit of measurement (e.g., "celsius", "%", "hPa")
    /// If not specified, defaults based on sensor type
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Whether the node is writable (default: false)
    /// </summary>
    public bool Writable { get; set; }

    /// <summary>
    /// Simulation parameters (reuses Modbus SensorSimulationParams)
    /// </summary>
    public SensorSimulationParams SimulationParams { get; set; } = new();
}
