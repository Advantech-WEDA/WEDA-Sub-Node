using System.Text.Json.Serialization;

namespace StockMonitor;

/// <summary>
/// TWSE stock quote API response model.
/// API: https://mis.twse.com.tw/stock/api/getStockInfo.jsp
/// </summary>
public class TwseStockResponse
{
    /// <summary>
    /// Message info array containing stock quotes
    /// </summary>
    [JsonPropertyName("msgArray")]
    public List<TwseStockQuote>? MsgArray { get; set; }

    /// <summary>
    /// Response status ("OK" = success)
    /// </summary>
    [JsonPropertyName("rtmessage")]
    public string? RtMessage { get; set; }

    /// <summary>
    /// Response code ("0000" = success)
    /// </summary>
    [JsonPropertyName("rtcode")]
    public string? RtCode { get; set; }

    /// <summary>
    /// Query time
    /// </summary>
    [JsonPropertyName("queryTime")]
    public TwseQueryTime? QueryTime { get; set; }

    public bool IsSuccess => RtCode == "0000";
}

public class TwseQueryTime
{
    /// <summary>
    /// System date (e.g., "20241128")
    /// </summary>
    [JsonPropertyName("sysDate")]
    public string? SysDate { get; set; }

    /// <summary>
    /// Stock info item count
    /// </summary>
    [JsonPropertyName("stockInfoItem")]
    public int StockInfoItem { get; set; }

    /// <summary>
    /// Stock info count
    /// </summary>
    [JsonPropertyName("stockInfo")]
    public int StockInfo { get; set; }

    /// <summary>
    /// Session string (e.g., "session")
    /// </summary>
    [JsonPropertyName("sessionStr")]
    public string? SessionStr { get; set; }

    /// <summary>
    /// System time (e.g., "14:30:00")
    /// </summary>
    [JsonPropertyName("sysTime")]
    public string? SysTime { get; set; }

    /// <summary>
    /// Show chart flag
    /// </summary>
    [JsonPropertyName("showChart")]
    public bool ShowChart { get; set; }

    /// <summary>
    /// Session key
    /// </summary>
    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; set; }

    /// <summary>
    /// Session from time
    /// </summary>
    [JsonPropertyName("sessionFromTime")]
    public long SessionFromTime { get; set; }

    /// <summary>
    /// Session latest time
    /// </summary>
    [JsonPropertyName("sessionLatestTime")]
    public long SessionLatestTime { get; set; }
}

/// <summary>
/// Individual stock quote data from TWSE API.
/// </summary>
public class TwseStockQuote
{
    /// <summary>
    /// Stock code (e.g., "2395")
    /// </summary>
    [JsonPropertyName("c")]
    public string? Code { get; set; }

    /// <summary>
    /// Company short name (e.g., "研華")
    /// </summary>
    [JsonPropertyName("n")]
    public string? Name { get; set; }

    /// <summary>
    /// Full company name
    /// </summary>
    [JsonPropertyName("nf")]
    public string? FullName { get; set; }

    /// <summary>
    /// Current trade price (成交價)
    /// "-" means no trade yet
    /// </summary>
    [JsonPropertyName("z")]
    public string? TradePrice { get; set; }

    /// <summary>
    /// Trade volume for current tick (成交量)
    /// </summary>
    [JsonPropertyName("tv")]
    public string? TradeVolume { get; set; }

    /// <summary>
    /// Accumulated trade volume (累積成交量)
    /// </summary>
    [JsonPropertyName("v")]
    public string? AccumulatedVolume { get; set; }

    /// <summary>
    /// Open price (開盤價)
    /// </summary>
    [JsonPropertyName("o")]
    public string? OpenPrice { get; set; }

    /// <summary>
    /// Highest price (最高價)
    /// </summary>
    [JsonPropertyName("h")]
    public string? HighPrice { get; set; }

    /// <summary>
    /// Lowest price (最低價)
    /// </summary>
    [JsonPropertyName("l")]
    public string? LowPrice { get; set; }

    /// <summary>
    /// Yesterday's closing price (昨收價)
    /// </summary>
    [JsonPropertyName("y")]
    public string? YesterdayPrice { get; set; }

    /// <summary>
    /// Upper limit price (漲停價)
    /// </summary>
    [JsonPropertyName("u")]
    public string? UpperLimitPrice { get; set; }

    /// <summary>
    /// Lower limit price (跌停價)
    /// </summary>
    [JsonPropertyName("w")]
    public string? LowerLimitPrice { get; set; }

    /// <summary>
    /// Data timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("tlong")]
    public string? TimestampLong { get; set; }

    /// <summary>
    /// Data time (e.g., "14:30:00")
    /// </summary>
    [JsonPropertyName("t")]
    public string? Time { get; set; }

    /// <summary>
    /// Data date (e.g., "20241128")
    /// </summary>
    [JsonPropertyName("d")]
    public string? Date { get; set; }

    /// <summary>
    /// Best 5 bid prices (買價), separated by "_"
    /// </summary>
    [JsonPropertyName("b")]
    public string? BidPrices { get; set; }

    /// <summary>
    /// Best 5 bid volumes (買量), separated by "_"
    /// </summary>
    [JsonPropertyName("g")]
    public string? BidVolumes { get; set; }

    /// <summary>
    /// Best 5 ask prices (賣價), separated by "_"
    /// </summary>
    [JsonPropertyName("a")]
    public string? AskPrices { get; set; }

    /// <summary>
    /// Best 5 ask volumes (賣量), separated by "_"
    /// </summary>
    [JsonPropertyName("f")]
    public string? AskVolumes { get; set; }

    /// <summary>
    /// Exchange type (tse = listed, otc = OTC)
    /// </summary>
    [JsonPropertyName("ex")]
    public string? Exchange { get; set; }

    // === Computed properties ===

    /// <summary>
    /// Gets the current trade price as decimal.
    /// Returns null if no trade yet ("-").
    /// </summary>
    public decimal? CurrentPrice =>
        TradePrice != null && TradePrice != "-" && decimal.TryParse(TradePrice, out var price)
            ? price
            : null;

    /// <summary>
    /// Gets yesterday's closing price as decimal.
    /// </summary>
    public decimal? PreviousClose =>
        YesterdayPrice != null && decimal.TryParse(YesterdayPrice, out var price)
            ? price
            : null;

    /// <summary>
    /// Price change from yesterday.
    /// </summary>
    public decimal? PriceChange =>
        CurrentPrice.HasValue && PreviousClose.HasValue
            ? CurrentPrice.Value - PreviousClose.Value
            : null;

    /// <summary>
    /// Price change percentage from yesterday.
    /// </summary>
    public decimal? PriceChangePercent =>
        PriceChange.HasValue && PreviousClose.HasValue && PreviousClose.Value != 0
            ? Math.Round(PriceChange.Value / PreviousClose.Value * 100, 2)
            : null;

    /// <summary>
    /// Data timestamp as DateTimeOffset.
    /// </summary>
    public DateTimeOffset? Timestamp =>
        TimestampLong != null && long.TryParse(TimestampLong, out var ts)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
            : null;
}
