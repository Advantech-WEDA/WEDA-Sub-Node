using Microsoft.Extensions.Logging;
using Weda.SubNode.Simulators.Modbus;

namespace PowerAggregationExample.Simulators;

/// <summary>
/// Factory for creating Modbus simulators with predefined configurations.
/// </summary>
public static class SimulatorFactory
{
    /// <summary>
    /// Creates a current sensor simulator on port 5020.
    /// Simulates electrical current readings (0.5A - 10.0A).
    /// </summary>
    public static TcpModbusSimulator CreateCurrentSimulator(ILogger<TcpModbusSimulator>? logger = null)
    {
        var config = new TcpModbusSimulatorConfiguration
        {
            TcpConnection = new TcpConnectionSettings
            {
                IpAddress = "127.0.0.1",
                Port = 5020
            },
            ModbusProtocol = new ModbusProtocolSettings
            {
                SlaveId = 1
            },
            Simulation = new SimulationSettings
            {
                EnableValueChanges = true,
                GlobalUpdateIntervalSeconds = 2
            },
            Sensors =
            [
                new SimulatedSensor
                {
                    Name = "current",
                    Type = SensorType.Current,
                    StartAddress = 40001,
                    RegisterCount = 2,
                    DataType = SimulatedDataType.Float32,
                    Unit = "A",
                    SimulationParams = new SensorSimulationParams
                    {
                        MinValue = 0.5,
                        MaxValue = 10.0,
                        InitialValue = 5.0,
                        ChangeRate = 0.5,
                        NoiseLevel = 0.1
                    }
                }
            ]
        };

        return new TcpModbusSimulator(config, logger);
    }

    /// <summary>
    /// Creates a voltage sensor simulator on port 5021.
    /// Simulates electrical voltage readings (220V - 240V).
    /// </summary>
    public static TcpModbusSimulator CreateVoltageSimulator(ILogger<TcpModbusSimulator>? logger = null)
    {
        var config = new TcpModbusSimulatorConfiguration
        {
            TcpConnection = new TcpConnectionSettings
            {
                IpAddress = "127.0.0.1",
                Port = 5021
            },
            ModbusProtocol = new ModbusProtocolSettings
            {
                SlaveId = 1
            },
            Simulation = new SimulationSettings
            {
                EnableValueChanges = true,
                GlobalUpdateIntervalSeconds = 2
            },
            Sensors =
            [
                new SimulatedSensor
                {
                    Name = "voltage",
                    Type = SensorType.Voltage,
                    StartAddress = 40001,
                    RegisterCount = 2,
                    DataType = SimulatedDataType.Float32,
                    Unit = "V",
                    SimulationParams = new SensorSimulationParams
                    {
                        MinValue = 220.0,
                        MaxValue = 240.0,
                        InitialValue = 230.0,
                        ChangeRate = 0.5,
                        NoiseLevel = 0.3
                    }
                }
            ]
        };

        return new TcpModbusSimulator(config, logger);
    }
}
