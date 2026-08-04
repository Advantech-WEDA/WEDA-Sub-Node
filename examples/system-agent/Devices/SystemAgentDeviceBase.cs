using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Protocols;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
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
    private readonly LocalSystemCommunication _systemCommunication;

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
        // Retained so runtime configuration updates can re-discover resources and re-resolve.
        _systemCommunication = communication;

        _logger.LogInformation(
            "SystemAgentDevice initialized ({SensorCount} sensors after resolution)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Re-resolves template sensors after the framework has applied a cloud configuration update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cloud's desired state holds the original template sensors from <c>devicecfg.json</c>
    /// (auto-detect / explicit-list mode). Because updates are applied in REPLACE mode, every
    /// applied update discards the per-resource sensors produced at startup and reinstates the
    /// unresolved templates — which the parser then skips on every poll, stopping their telemetry.
    /// Re-resolving here restores them. See <see cref="SensorReResolver"/>.
    /// </para>
    /// <para>
    /// Background tasks were already restarted by the framework before this hook runs, so they
    /// must be restarted again once the sensor list changes for the new sensors to be polled.
    /// </para>
    /// </remarks>
    /// <param name="e">The configuration update event that was applied.</param>
    /// <param name="ct">Cancellation token.</param>
    protected override async Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        await base.OnAfterConfigUpdateAsync(e, ct);

        try
        {
            var resources = _systemCommunication.DiscoverAvailableResources();

            if (!SensorReResolver.ReResolve(Configuration, resources, _logger))
                return;

            _logger.LogInformation(
                "Re-resolved sensors after configuration update ({SensorCount} sensors)",
                Configuration.Sensors.Count);

            // Parser metadata and polling groups were built from the unresolved list.
            await RestartBackgroundTasksAsync();
        }
        catch (Exception ex)
        {
            // Never fail the configuration update: the framework has already applied and cached it.
            // The device keeps running with the unresolved templates until the next update or restart.
            _logger.LogError(ex,
                "Failed to re-resolve sensors after configuration update for device '{DeviceName}'; " +
                "template sensors will not report until the next successful update",
                Configuration.DeviceName);
        }
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
