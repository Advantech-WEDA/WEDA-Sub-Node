using AirQualityMonitor.Communication;
using AirQualityMonitor.Protocols;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Devices;

namespace AirQualityMonitor.Devices;

public class AirQualityDevice : RequestResponseDeviceBase
{   
    private const string DefaultApiKey = "4c89a32a-a214-461b-bf29-30ff32a61a8a";
    
    public AirQualityDevice(IWedaApplicationContext context, DeviceConfiguration configuration) 
        : base(context, configuration, CreateParser(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    public AirQualityDevice(IWedaApplicationContext context, string configKey) 
        : this(context, context[configKey])
    {
    }

    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context, 
        DeviceConfiguration configuration)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var client = new AirQualityClient(httpClient, context.GetLogger<AirQualityClient>(), DefaultApiKey);
        var communication = new HttpCommunication(client, null, context.GetLogger<Communication.HttpCommunication>());

        return new AirQualityParser(configuration, communication, context.GetLogger<AirQualityParser>());
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            var preview = measure.Value?.ToString()?[..Math.Min(100, measure.Value.ToString()?.Length ?? 0)];
            _logger.LogInformation("Air quality data received: {Preview}...", preview);
        }
    }
}