using StockMonitor.Models;

namespace StockMonitor.Tests;

/// <summary>Synthesized TWSE responses for tests.</summary>
internal static class TwseFixtures
{
    /// <summary>A successful response carrying one fully-populated quote per requested code.</summary>
    internal static TwseStockResponse QuotesFor(params string[] codes) => new()
    {
        RtCode = "0000",
        MsgArray = [.. codes.Select(code => new TwseStockQuote
        {
            Code = code,
            Name = $"Company {code}",
            TradePrice = "412.50",
            OpenPrice = "410.00",
            HighPrice = "415.00",
            LowPrice = "409.00",
            YesterdayPrice = "408.00",
            AccumulatedVolume = "12345",
            TimestampLong = "1755000000000",
        })],
    };
}
