using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.Serial;

/// <summary>
/// Factory for managing shared serial communication instances.
/// Implements Multiton pattern with reference counting.
/// </summary>
public class SerialCommunicationFactory : ISerialCommunicationFactory
{
    private readonly record struct CommunicationEntry(
        SerialCommunication instance,
        int RefCount,
        SerialCommunicationSettings Settings);

    private readonly object _lock = new();
    private readonly Dictionary<string, CommunicationEntry> _instances = [];
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SerialCommunicationFactory> _logger;
    private static SerialCommunicationFactory? _instance;
    private static readonly object _instanceLock = new();
    
    /// <summary>
    /// Gets the singleton instance of SerialCommunicationFactory.
    /// </summary>
    public static SerialCommunicationFactory GetInstance(ILoggerFactory loggerFactory)
    {
        if (_instance is not null) return _instance;
        lock (_instanceLock)
        {
            return _instance ??= new SerialCommunicationFactory(loggerFactory);
        }
    }

    public SerialCommunicationFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<SerialCommunicationFactory>();   
    }

    public SerialCommunication GetOrCreate(
        string portName,
        SerialCommunicationSettings settings,
        ConnectionSettings? connectionSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
        {
            if (_instances.TryGetValue(portName, out var entry))
            {
                ValidateSettingsCompatibility(portName, entry.Settings, settings);
                _instances[portName] = entry with { RefCount = entry.RefCount + 1 };

                _logger.LogDebug(
                    "Reusing SerialCommunication for {PortName}, refCount={RefCount}",
                    portName, entry.RefCount + 1);
                
                return entry.instance;
            }

            var logger = _loggerFactory.CreateLogger<CommunicationBase>();
            var instance = new SerialCommunication(settings, connectionSettings, logger);
            _instances[portName] = new CommunicationEntry(instance, 1, settings);

            _logger.LogInformation(
                "Created SerialCommunication for {PortName} (BaudRate={BaudRate}, Parity={Parity})",
                portName, settings.BaudRate, settings.Parity);

            return instance;
        }
    }

    public bool Release(string portName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        lock (_lock)
        {
            if (!_instances.TryGetValue(portName, out var entry))
            {
                _logger.LogWarning("Release called for non-existent port={PortName}", portName);
                return false;
            }

            var newRefCount = entry.RefCount - 1;
            
            if (newRefCount == 0)
            {
                _instances.Remove(portName);
                DisposeInstance(portName, entry.instance);
            }
            else
            {
                _instances[portName] = entry with { RefCount = newRefCount };
                _logger.LogDebug("Released {PortName}, refCount={RefCount}", portName, newRefCount);
            }

            return true;
        }
    }

    public int GetReferenceCount(string portName)
    {
        lock (_lock)
        {
            return _instances.TryGetValue(portName, out var entry) ? entry.RefCount : 0;
        }
    }

    public bool Contains(string portName)
    {
        lock (_lock)
        {
            return _instances.ContainsKey(portName);
        }
    }

    private void ValidateSettingsCompatibility(
        string portName, 
        SerialCommunicationSettings existing, 
        SerialCommunicationSettings requested)
    {
        var errors = new List<string>();

        if (existing.BaudRate != requested.BaudRate)
            errors.Add($"BaudRate: {existing.BaudRate} vs {requested.BaudRate}");
        if (existing.Parity != requested.Parity)
            errors.Add($"Parity: {existing.Parity} vs {requested.Parity}");
        if (existing.DataBits != requested.DataBits)
            errors.Add($"DataBits: {existing.DataBits} vs {requested.DataBits}");
        if (existing.StopBits != requested.StopBits)
            errors.Add($"StopBits: {existing.StopBits} vs {requested.StopBits}");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Port '{portName}' exists with incompatible settings: {string.Join("; ", errors)}");
        }
    }

    private void DisposeInstance(string portName, SerialCommunication instance)
    {
        try
        {
            instance.DisconnectAsync().GetAwaiter().GetResult();
            instance.Dispose();
            _logger.LogInformation("Disposed SerialCommunication for {PortName}", portName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing SerialCommunication for {PortName}", portName);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (portName, entry) in _instances)
            {
                DisposeInstance(portName, entry.instance);
            }

            _instances.Clear();
        }
    }

}