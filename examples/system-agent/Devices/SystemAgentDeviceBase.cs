using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Protocols;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Devices;

namespace SystemAgentExample.Devices;

/// <summary>
/// Base class for system agent devices that are coupled to local metrics collection.
/// Responsible for assembling the configuration, LocalSystemCommunication, and the 
/// SystemMetricsParser into a functional Request-Response device structure 
/// that collects CPU, memory, disk, and network metrics.
/// </summary>
public class SystemAgentDeviceBase : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of SystemAgentDeviceBase.
    /// Sensors without specific resource identifiers (Interface, PinId, MetricName)
    /// are auto-expanded into per-resource sensors before device initialization.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance for system metrics collection.</param>
    public SystemAgentDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
        : base(context, ExpandSensors(context, configuration, communication), CreateParser(context, configuration, communication))
    {
        _logger.LogInformation(
            "SystemAgentDevice initialized ({SensorCount} sensors after expansion)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the SystemMetricsParser for this device.
    /// Parser uses the provided communication instance.
    /// </summary>
    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        var loggerFactory = context.LoggerFactory;
        return new SystemMetricsParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<SystemMetricsParser>());
    }

    /// <summary>
    /// Discovers available system resources and expands template sensors
    /// into per-resource sensors before passing to the base constructor.
    /// </summary>
    private static DeviceConfiguration ExpandSensors(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        var logger = context.LoggerFactory.CreateLogger<SystemAgentDeviceBase>();
        var resources = communication.DiscoverAvailableResources();

        logger.LogInformation(
            "Discovered resources — Network: [{Networks}], GPIO: [{Gpio}], Temperature: [{Temp}]",
            string.Join(", ", resources.NetworkInterfaces),
            string.Join(", ", resources.GpioPins),
            string.Join(", ", resources.TemperatureSources));

        configuration.Sensors = SensorExpander.Expand(configuration.Sensors, resources, logger);
        return configuration;
    }
}
