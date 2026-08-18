using System.Text.Json.Serialization;

namespace AirQualityMonitor.Models;

/// <summary>
/// Air quality monitoring data from MOENV API.
/// API: https://data.moenv.gov.tw/api/v2/aqx_p_136
/// </summary>
public class AirQualityRecords : Dictionary<string, AirQualityRecord>
{
    public AirQualityRecords(IEnumerable<AirQualityRecord> records)
    {
        foreach (var record in records)
        {
            TryAdd(record.ItemEngName!, record);
        }
    }
}