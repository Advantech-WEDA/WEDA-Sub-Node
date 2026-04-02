using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Utilities;
using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class NetworkCollector
{
    private readonly ILogger _logger;

    /// <summary>
    /// Network interface types that represent real (physical or logical) adapters.
    /// Based on IANA ifType definitions (RFC 2863).
    /// Excludes NDIS filter drivers, virtual switch extensions, PPP, and tunnel interfaces.
    /// </summary>
    private static readonly HashSet<NetworkInterfaceType> WindowsAllowedInterfaceTypes = 
        [
        NetworkInterfaceType.Ethernet,          // ifType 6  - standard wired LAN
        NetworkInterfaceType.Wireless80211,      // ifType 71 - Wi-Fi
        NetworkInterfaceType.GigabitEthernet    // ifType 117 - some drivers report this instead of Ethernet
        ];

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
            _logger.LogDebug("Found {Count} network interfaces", interfaces.Length);

            foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                if (!IsPhysicalOrLogicalInterface(iface))
                {
                    _logger.LogDebug("Skipping filter/virtual interface: {Name} (Type={Type}, Desc={Description})",
                        iface.Name, iface.NetworkInterfaceType, iface.Description);
                    continue;
                }

                _logger.LogDebug("Processing interface: {Name}, Type: {Type}, Status: {Status}",
                    iface.Name, iface.NetworkInterfaceType, iface.OperationalStatus);
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
                    _logger.LogWarning("NonUnicastPackets not supported for {Name}, using 0", iface.Name);
                }

                var networkMetric = new NetworkMetrics
                {
                    InterfaceName = PlatformHelpers.SanitizeInterfaceName(iface.Description, iface.Name),
                    ReceiveBytesTotal = stats.BytesReceived,
                    TransmitBytesTotal = stats.BytesSent,
                    ReceivePacketsTotal = stats.UnicastPacketsReceived + nonUnicastPacketsReceived,
                    TransmitPacketsTotal = stats.UnicastPacketsSent + nonUnicastPacketsSent,
                    ReceiveErrsTotal = stats.IncomingPacketsWithErrors,
                    TransmitErrsTotal = stats.OutgoingPacketsWithErrors
                };

                _logger.LogDebug("Network {Name}: RX={RxBytes}, TX={TxBytes}, RxPkts={RxPkts}, TxPkts={TxPkts}, RxErrs={RxErrs}, TxErrs={TxErrs}",
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

    /// <summary>
    /// Returns true only for real physical or logical network adapters.
    /// Filters out NDIS filter drivers, virtual switch extensions, and similar virtual layers
    /// that Windows exposes as separate NetworkInterface entries.
    /// </summary>

    private bool IsPhysicalOrLogicalInterface(NetworkInterface iface)
    {
        // 1. Interface must be up
        if (iface.OperationalStatus != OperationalStatus.Up)
            return false;

        var ipProps = iface.GetIPProperties();

        // 2. Must have at least one valid (non-loopback, non-link-local) IP
        bool hasValidIp = ipProps.UnicastAddresses.Any(addr =>
        {
            var ip = addr.Address;

            if (IPAddress.IsLoopback(ip))
                return false;

            // Exclude IPv4 APIPA
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                ip.ToString().StartsWith("169.254."))
                return false;

            // Exclude IPv6 link-local
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                ip.IsIPv6LinkLocal)
                return false;

            return true;
        });

        if (!hasValidIp)
            return false;

        // 3. Must have a default gateway (real egress capability)
        if (!ipProps.GatewayAddresses.Any())
            return false;

        // 4. Windows-specific filtering
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!WindowsAllowedInterfaceTypes.Contains(iface.NetworkInterfaceType))
                return false;

            // Exclude known NDIS filter drivers / virtual layers
            var desc = iface.Description;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                if (desc.Contains("Filter", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("Virtual Switch", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("Virtual Ethernet", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
