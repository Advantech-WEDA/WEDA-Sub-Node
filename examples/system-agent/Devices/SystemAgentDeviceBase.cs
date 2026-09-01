using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Models;
using SystemAgentExample.Protocols;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace SystemAgentExample.Devices;

/// <summary>
/// Base class for system agent devices that are coupled to local metrics collection.
/// Responsible for assembling the configuration, LocalSystemCommunication, and the
/// SystemMetricsParser into a functional Request-Response device structure
/// that collects CPU, memory, disk, and network metrics.
/// </summary>

public class SystemAgentDeviceBase : RequestResponseDeviceBase,
    IDigitalOutputControllable, IDigitalInputReadable, IDigitalOutputReadable, IGpioPinListable
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
        // Retained so runtime configuration updates can re-discover resources and re-resolve.
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
            var resources = _localCommunication.DiscoverAvailableResources();

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

    private GpioDigitalIo? _gpio;

    private GpioDigitalIo Gpio => _gpio ??= new GpioDigitalIo(
        pin => _localCommunication.GetGpioPinLevel(ResolveHardwarePinName(pin)),
        (pin, state) => _localCommunication.SetGpioPinLevel(ResolveHardwarePinName(pin), state),
        pin => _localCommunication.GetGpioPinDirection(ResolveHardwarePinName(pin)),
        _logger);

    /// <summary>
    /// Resolves a configured sensor name (e.g. gpio_pinState_UIO_GPIO2) to its
    /// hardware pin via the sensor's PinId parameter. The built-in DI/DO command
    /// handlers address pins by sensor name; the driver expects the raw pin name.
    /// A bare pin name resolves through <see cref="FindSensor"/> and yields itself;
    /// names without a bound sensor pass through unchanged.
    /// </summary>
    private string ResolveHardwarePinName(string name) =>
        GpioPinLookup.ResolveHardwarePin(Configuration.Sensors, name);

    /// <summary>
    /// Resolves a sensor by its configured name, then by the hardware pin it is bound to.
    /// </summary>
    /// <remarks>
    /// This is what lets di.get / do.get / do.set address a pin as "UIO_GPIO2" — the name
    /// <c>gpio.list</c> reports — rather than the resolved sensor name
    /// "gpio_pinState_UIO_GPIO2". The command handlers call this before delegating, so
    /// without the fallback a bare pin name is rejected as "not found on any device".
    /// Both spellings stay valid; the configured name is matched first so an alias can
    /// never shadow a real sensor.
    /// </remarks>
    /// <param name="sensorName">Configured sensor name, or a bare hardware pin name.</param>
    /// <returns>The matching sensor, or null when unknown or bound by more than one sensor.</returns>
    public override Sensor? FindSensor(string sensorName)
    {
        var configured = base.FindSensor(sensorName);
        if (configured is not null)
            return configured;

        var resolved = GpioPinLookup.FindByPinName(
            Configuration.Sensors, sensorName, out var ambiguousWith);

        if (ambiguousWith.Count > 0)
        {
            _logger.LogError(
                "GPIO pin '{PinName}' is bound by {Count} sensors ({Sensors}); "
                + "address the pin by its sensor name instead",
                sensorName, ambiguousWith.Count, string.Join(", ", ambiguousWith));
            return null;
        }

        if (resolved is not null)
            _logger.LogDebug("Resolved GPIO pin '{PinName}' to sensor '{SensorName}'", sensorName, resolved.Name);

        return resolved;
    }

    public List<GpioPinDescriptor> ListGpioPins()
    {
        var sensorByPin = Configuration.Sensors
            .Select(s => (s.Name, PinId: s.Parameters?.GetValueOrDefault("PinId")?.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.PinId))
            .GroupBy(x => x.PinId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        return _localCommunication.ListGpioPins()
            .Select(p => p with { SensorName = sensorByPin.GetValueOrDefault(p.Name) })
            .ToList();
    }

    public Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
        => Gpio.SetOutputAsync(outputName, state, cancellationToken);

    public Task<bool?> GetDigitalInputAsync(string inputName, CancellationToken cancellationToken = default)
        => Task.FromResult(Gpio.GetLevel(inputName));

    public Task<bool?> GetDigitalOutputAsync(string outputName, CancellationToken cancellationToken = default)
        => Task.FromResult(Gpio.GetLevel(outputName));
}
