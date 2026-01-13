using System.Net.NetworkInformation;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Utilities;
using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class NetworkCollector
{
    private readonly ILogger _logger;

    public NetworkCollector(ILogger logger)
    {
        _logger = logger;
    }

    public List<NetworkMetrics> CollectNetworkMetrics()
    {
        var networks = new List<NetworkMetrics>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            _logger.LogInformation("Found {Count} network interfaces", interfaces.Length);

            foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                _logger.LogInformation("Processing interface: {Name}, Status: {Status}", iface.Name, iface.OperationalStatus);

                var stats = iface.GetIPv4Statistics();

                long nonUnicastPacketsSent = 0;
                long nonUnicastPacketsReceived = 0;
                try
                {
                    nonUnicastPacketsSent = stats.NonUnicastPacketsSent;
                    nonUnicastPacketsReceived = stats.NonUnicastPacketsReceived;
                }
                catch (PlatformNotSupportedException)
                {
                    _logger.LogInformation("NonUnicastPackets not supported for {Name}, using 0", iface.Name);
                }

                var networkMetric = new NetworkMetrics
                {
                    InterfaceName = PlatformHelpers.SanitizeInterfaceName(iface.Name),
                    ReceiveBytesTotal = stats.BytesReceived,
                    TransmitBytesTotal = stats.BytesSent,
                    ReceivePacketsTotal = stats.UnicastPacketsReceived + nonUnicastPacketsReceived,
                    TransmitPacketsTotal = stats.UnicastPacketsSent + nonUnicastPacketsSent,
                    ReceiveErrsTotal = stats.IncomingPacketsWithErrors,
                    TransmitErrsTotal = stats.OutgoingPacketsWithErrors
                };

                _logger.LogInformation("Network {Name}: RX={RxBytes}, TX={TxBytes}, RxPkts={RxPkts}, TxPkts={TxPkts}, RxErrs={RxErrs}, TxErrs={TxErrs}",
                    networkMetric.InterfaceName,
                    networkMetric.ReceiveBytesTotal,
                    networkMetric.TransmitBytesTotal,
                    networkMetric.ReceivePacketsTotal,
                    networkMetric.TransmitPacketsTotal,
                    networkMetric.ReceiveErrsTotal,
                    networkMetric.TransmitErrsTotal);

                networks.Add(networkMetric);
            }

            _logger.LogDebug("Collected {Count} network metrics", networks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect network metrics: {Message}", ex.Message);
            _logger.LogError("Exception Type: {Type}, StackTrace: {StackTrace}", ex.GetType().Name, ex.StackTrace);
        }

        return networks;
    }
}
