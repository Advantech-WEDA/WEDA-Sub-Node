using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Power aggregator definition that calculates Power = Voltage × Current.
/// Reads voltage and current source mappings from Sensor.Parameters:
/// - VoltageSource: "DeviceName/SensorName" format (e.g., "VoltageSensor/voltage001")
/// - CurrentSource: "DeviceName/SensorName" format (e.g., "CurrentSensor/current001")
/// </summary>
public class PowerAggregatorDefinition : AggregatorDefinitionBase
{
    // Parameter keys for source configuration
    private const string VoltageSourceKey = "VoltageSource";
    private const string CurrentSourceKey = "CurrentSource";

    /// <summary>
    /// Creates a PowerAggregatorDefinition from a Sensor configuration.
    /// Reads VoltageSource and CurrentSource from Sensor.Parameters.
    /// </summary>
    /// <param name="sensor">
    /// The sensor configuration containing:
    /// - Name: Sensor name (available immediately)
    /// - ResourceId: Output power sensor ResourceId (populated after cloud registration)
    /// - Parameters["VoltageSource"]: "DeviceName/SensorName" for voltage
    /// - Parameters["CurrentSource"]: "DeviceName/SensorName" for current
    /// </param>
    /// <param name="logger">Logger instance</param>
    public PowerAggregatorDefinition(
        Sensor sensor,
        ILogger<PowerAggregatorDefinition>? logger = null)
        : base(
            sensor,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<PowerAggregatorDefinition>(),
            VoltageSourceKey,
            CurrentSourceKey)
    {
    }

    /// <summary>
    /// Calculates power from voltage and current: P = V × I
    /// </summary>
    /// <param name="values">Dictionary containing VoltageSource and CurrentSource values</param>
    /// <returns>Calculated power in Watts</returns>
    protected override double Calculate(IReadOnlyDictionary<string, double> values)
    {
        var voltage = values[VoltageSourceKey];
        var current = values[CurrentSourceKey];
        return voltage * current;  // P = V × I
    }

    /// <summary>
    /// Custom log output for power calculation.
    /// </summary>
    protected override void LogCalculation(IReadOnlyDictionary<string, double> values, double result)
    {
        // var voltage = values[VoltageSourceKey];
        // var current = values[CurrentSourceKey];
        // Logger.LogInformation(
        //     "[PowerAggregator] POWER CALCULATED: {Voltage:F2} V x {Current:F2} A = {Power:F2} W",
        //     voltage, current, result);
    }
}
