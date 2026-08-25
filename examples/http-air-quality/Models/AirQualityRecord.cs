using System.Text.Json.Serialization;

namespace AirQualityMonitor.Models;

/// <summary>
/// Air quality monitoring data from MOENV API.
/// API: https://data.moenv.gov.tw/api/v2/aqx_p_136
/// </summary>
public class AirQualityRecord
{
    [JsonPropertyName("siteid")]
    public string? SiteId { get; set; }

    [JsonPropertyName("sitename")]
    public string? SiteName { get; set; }

    [JsonPropertyName("county")]
    public string? County { get; set; }

    [JsonPropertyName("itemid")]
    public string? ItemId { get; set; }

    [JsonPropertyName("itemname")]
    public string? ItemName { get; set; }

    [JsonPropertyName("itemengname")]
    public string? ItemEngName { get; set; }

    [JsonPropertyName("itemunit")]
    public string? ItemUnit { get; set; }

    [JsonPropertyName("monitordate")]
    public string? MonitorDate { get; set; }

    [JsonPropertyName("concentration")]
    public string? Concentration { get; set; }
}