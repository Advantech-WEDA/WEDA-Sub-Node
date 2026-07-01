using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Net;

namespace daq_feature_proxy.Communication;

public record DeviceCapability(
    string DeviceId,
    IReadOnlyDictionary<string, string> ShortIdToNameMap);  // resourceShortId → sensor name

public class DeviceCapabilityClient
{
    private const string CapQuerySubject = "eco1j.weda.dm.subnode.cap.query.req";
    private const int CapQueryTimeoutMs = 10000;

    private readonly NatsClient _natsClient;
    private readonly ILogger _logger;

    public DeviceCapabilityClient(NatsClient natsClient, ILogger logger)
    {
        _natsClient = natsClient;
        _logger = logger;
    }

    // Returns DeviceCapability(DeviceId, ShortIdToNameMap) for the target DAQ device.
    // Queries all SubNodes (no deviceIds filter) and matches by deviceName on the client side.
    // Retries up to 3× before returning null — null means proxy cannot start.
    public async Task<DeviceCapability?> QueryCapabilityAsync(
        string daqDeviceName, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            reqSeqId = "query-subnode-caps",
            data = new { }
        });

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CapQueryTimeoutMs);
            try
            {
                var reply = await _natsClient.RequestAsync<string, string>(
                    CapQuerySubject, payload, cancellationToken: cts.Token);

                if (reply.Data is null) continue;

                var capability = ParseCapability(reply.Data, daqDeviceName);
                if (capability is not null)
                {
                    _logger.LogInformation(
                        "DAQ capability query succeeded for '{DeviceName}': DeviceId={DeviceId}, {Count} feature sensors mapped",
                        daqDeviceName, capability.DeviceId, capability.ShortIdToNameMap.Count);
                    return capability;
                }

                _logger.LogWarning(
                    "Capability response did not contain device '{DeviceName}' on attempt {Attempt}",
                    daqDeviceName, attempt + 1);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Capability query attempt {Attempt} timed out for device '{DeviceName}'",
                    attempt + 1, daqDeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Capability query attempt {Attempt} failed for device '{DeviceName}'",
                    attempt + 1, daqDeviceName);
            }
        }

        _logger.LogError(
            "Could not retrieve DAQ device capabilities after 3 attempts for '{DeviceName}' — proxy will not start",
            daqDeviceName);
        return null;
    }

    private static DeviceCapability? ParseCapability(string json, string deviceName)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (!data.TryGetProperty("subNodes", out var subNodes)) return null;

        foreach (var node in subNodes.EnumerateArray())
        {
            if (!node.TryGetProperty("deviceName", out var dn) || dn.GetString() != deviceName) continue;
            if (!node.TryGetProperty("deviceId", out var did)) return null;

            var deviceId = did.GetString();
            if (string.IsNullOrEmpty(deviceId)) return null;

            if (!node.TryGetProperty("capabilities", out var caps)) return null;
            if (!caps.TryGetProperty("sensors", out var sensors)) return null;

            var shortIdToName = new Dictionary<string, string>();
            foreach (var sensor in sensors.EnumerateArray())
            {
                if (!sensor.TryGetProperty("sensorGroup", out var g) || g.GetString() == "SYS") continue;
                if (!sensor.TryGetProperty("name", out var n) || !sensor.TryGetProperty("resourceId", out var r)) continue;

                var name = n.GetString();
                var resourceId = r.GetString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(resourceId)
                    || name == "daqraw_vibration_payload") continue;

                // resourceShortId = last 5 chars of resourceId UUID (excluding hyphens)
                var rawId = resourceId.Replace("-", "");
                if (rawId.Length < 5) continue;
                shortIdToName[rawId[^5..]] = name;
            }

            return new DeviceCapability(deviceId, shortIdToName);
        }

        return null;
    }
}
