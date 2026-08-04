using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Models;
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
    private readonly LocalSystemCommunication _localCommunication;

    /// <summary>
    /// Initializes a new instance of SystemAgentDeviceBase.
    /// Sensors are resolved according to their mode (Bound / Explicit list / Auto-detect)
    /// into per-resource sensors before device initialization.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance for system metrics collection.</param>
    public SystemAgentDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
        : base(context, ResolveSensors(context, configuration, communication), CreateParser(context, configuration, communication))
    {
        _localCommunication = communication;

        _logger.LogInformation(
            "SystemAgentDevice initialized ({SensorCount} sensors after resolution)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Publishes the Advantech HAL load stats into the SubNode capability report.
    /// Runs during device initialization (before configurations are aggregated and uploaded),
    /// writing into <c>SubNodeInfo.Metadata</c> which is mapped to <c>DeviceCapDto.deviceInfo</c>.
    /// </summary>
    protected override async Task OnAfterInitializeAsync(CancellationToken ct)
    {
        var halStatus = _localCommunication.HalStatus;
        _context.SubNodeInfo.Metadata[AdvantechHalStatus.MetadataKey] = halStatus.ToMetadata();

        _logger.LogInformation(
            "Advantech HAL status published to device capability (loaded={Loaded}, driver={Driver}, library={Library})",
            halStatus.Loaded, halStatus.DriverVersion, halStatus.LibraryVersion);

        await base.OnAfterInitializeAsync(ct);
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
    /// Discovers available system resources and resolves template sensors
    /// into per-resource sensors before passing to the base constructor.
    /// </summary>
    private static DeviceConfiguration ResolveSensors(
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

        // Recover array values lost during IConfiguration Dictionary<string, object> binding
        // by reading the raw IConfigurationSection children (e.g., "Sensors:0:Parameters:Interfaces:0")
        var sensorsSection = context.Configuration?
            .GetSection($"DeviceConfig:DeviceConfigs:{configuration.DeviceName}:Sensors");

        ParameterNormalizer.Normalize(configuration.Sensors, sensorsSection);

        configuration.Sensors = SensorResolver.Resolve(configuration.Sensors, resources, logger);

        return configuration;
    }
}
