using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace StockMonitor;

/// <summary>
/// Stock monitor device that fetches real-time stock prices from TWSE API.
/// Uses adaptive polling: 10 seconds during trading hours, 1 hour during off-hours.
/// Architecture: StockMonitorDevice -> TwseStockParser (IRequestResponseProtocolParser)
///                                       -> HttpCommunication -> TwseStockClient
/// </summary>
public class StockMonitorDevice : DeviceBase
{
    private readonly TwseStockParser _parser;
    private readonly ILogger<StockMonitorDevice> _deviceLogger;
    private readonly TimeZoneInfo _taiwanTimeZone;

    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;

    // Stock codes to monitor (configured via DeviceConfiguration.Properties)
    private readonly List<string> _stocks;

    // Polling intervals
    private TimeSpan _tradingHoursInterval = TimeSpan.FromSeconds(10);
    private TimeSpan _offHoursInterval = TimeSpan.FromHours(1);

    // Trading hours in Taiwan time (9:00 AM - 1:30 PM)
    private static readonly TimeOnly TradingStart = new(9, 0);
    private static readonly TimeOnly TradingEnd = new(13, 30);

    // Last known prices for change detection
    private readonly Dictionary<string, decimal> _lastPrices = new();

    /// <summary>
    /// Creates a StockMonitorDevice with the standard WedaBuilder pattern.
    /// </summary>
    public StockMonitorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    /// <summary>
    /// Creates a StockMonitorDevice with explicit configuration.
    /// Parser is created first, then its Communication is passed to DeviceBase.
    /// </summary>
    public StockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : this(context, configuration, CreateParser(context, configuration))
    {
    }

    /// <summary>
    /// Internal constructor that accepts a pre-created parser.
    /// Passes parser.Communication to DeviceBase, stores parser reference.
    /// </summary>
    private StockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        TwseStockParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser;
        _deviceLogger = context.GetLogger<StockMonitorDevice>();
        _taiwanTimeZone = TimeZoneInfo.FindSystemTimeZoneById(GetTaiwanTimeZoneId());

        // Get stock codes from configuration
        _stocks = GetStockCodesFromConfiguration(configuration);

        // Configure polling intervals from configuration
        ConfigurePollingIntervals(configuration);

        // Enable telemetry tracking for logging
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        _deviceLogger.LogInformation(
            "StockMonitorDevice initialized with {Count} stock codes: {Codes}",
            _stocks.Count,
            string.Join(", ", _stocks));
    }

    private static string GetTaiwanTimeZoneId()
    {
        return OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    /// <summary>
    /// Creates the full parser stack: TwseStockParser -> HttpCommunication -> TwseStockClient
    /// </summary>
    private static TwseStockParser CreateParser(IWedaApplicationContext context, DeviceConfiguration configuration)
    {
        var httpClient = CreateHttpClient();
        var stockClient = new TwseStockClient(httpClient, context.GetLogger<TwseStockClient>());
        var settings = new ConnectionSettings();
        var httpCommLogger = context.GetLogger<HttpCommunication>();
        var httpCommunication = new HttpCommunication(stockClient, settings, httpCommLogger);

        var stockCodes = GetStockCodesFromConfiguration(configuration);
        return new TwseStockParser(
            httpCommunication,
            stockCodes,
            context.GetLogger<TwseStockParser>());
    }

    private static List<string> GetStockCodesFromConfiguration(DeviceConfiguration configuration)
    {
        var stockCodes = configuration.Sensors.Select(sensor => sensor.Name).ToList();
        if (stockCodes.Any())
        {
            return stockCodes;
        }
        return ["2395"]; // Default: Advantech
    }

    private void ConfigurePollingIntervals(DeviceConfiguration configuration)
    {
        if (configuration.Properties.TryGetValue("TradingHoursIntervalMs", out var tradingInterval))
        {
            _tradingHoursInterval = TimeSpan.FromMilliseconds(Convert.ToInt32(tradingInterval));
        }
        if (configuration.Properties.TryGetValue("OffHoursIntervalMs", out var offHoursInterval))
        {
            _offHoursInterval = TimeSpan.FromMilliseconds(Convert.ToInt32(offHoursInterval));
        }
    }

    /// <summary>
    /// Builds sensor mapping from configuration for the parser.
    /// Maps stock codes to ResourceIds.
    /// </summary>
    private SensorMapping BuildSensorMapping()
    {
        var mapping = new SensorMapping();

        foreach (var sensor in Configuration.Sensors.Where(s => s.Config.Enabled))
        {
            // Get stock code from sensor name or parameters
            var stockCode = sensor.Name;
            if (sensor.Parameters.TryGetValue("StockCode", out var code))
            {
                stockCode = code?.ToString() ?? sensor.Name;
            }

            mapping.FieldToResourceId[stockCode] = sensor.ResourceId;
            mapping.FieldToSensorType[stockCode] = SensorType.Other;
        }

        return mapping;
    }

    public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Delegate to parser (which uses HttpCommunication -> TwseStockClient)
        var sensorMapping = BuildSensorMapping();
        var measures = await _parser.ReadSensorDataAsync(sensorMapping, cancellationToken);

        if (measures.Count > 0)
        {
            RaiseDataReceived(measures);
        }

        return measures;
    }

    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _deviceLogger.LogWarning("StockMonitorDevice does not support commands: {Command}", command.DeviceCmd);
        return Task.FromResult(false);
    }

    protected override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        _telemetryTask = Task.Run(async () =>
        {
            _deviceLogger.LogInformation("Starting stock telemetry task with adaptive polling");
            await Task.Delay(1000, cts); // Wait 1 second before first fetch

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var isMarketOpen = IsMarketOpen();
                    var interval = isMarketOpen ? _tradingHoursInterval : _offHoursInterval;

                    if (isMarketOpen)
                    {
                        _deviceLogger.LogDebug("Market is open, polling every {Interval} seconds", interval.TotalSeconds);
                    }
                    else
                    {
                        var nextOpen = GetNextMarketOpen();
                        var timeUntilOpen = nextOpen - GetTaiwanTime();
                        _deviceLogger.LogInformation(
                            "Market is closed. Next open: {NextOpen:yyyy-MM-dd HH:mm} (in {Hours:F1} hours). Polling every {Interval} minutes.",
                            nextOpen, timeUntilOpen.TotalHours, interval.TotalMinutes);
                    }

                    // Read and send telemetry
                    var measures = await ReadTelemetryAsync(cts);
                    if (measures.Count > 0)
                    {
                        await SendTelemetryAsync(ToAsyncEnumerable(measures), cts);
                    }

                    await Task.Delay(interval, cts);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    _deviceLogger.LogInformation("Telemetry task cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _deviceLogger.LogError(ex, "Error in telemetry task");
                    await Task.Delay(TimeSpan.FromSeconds(30), cts);
                }
            }
        }, cts);

        _healthTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReportHealth;
            _deviceLogger.LogDebug("Starting health reporting task with period {Period}ms", period);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await ReportHealthAsync(cts);
                }
                catch (Exception ex)
                {
                    _deviceLogger.LogError(ex, "Error in health reporting task");
                }
                await Task.Delay(period, cts);
            }
        }, cts);

        return Task.CompletedTask;
    }

    #region Trading Hours Logic

    private DateTime GetTaiwanTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _taiwanTimeZone);
    }

    private bool IsMarketOpen()
    {
        var taiwanNow = GetTaiwanTime();

        // Check weekday
        if (taiwanNow.DayOfWeek == DayOfWeek.Saturday || taiwanNow.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Check trading hours
        var currentTime = TimeOnly.FromDateTime(taiwanNow);
        return currentTime >= TradingStart && currentTime <= TradingEnd;
    }

    private DateTime GetNextMarketOpen()
    {
        var taiwanNow = GetTaiwanTime();
        var currentDate = DateOnly.FromDateTime(taiwanNow);
        var currentTime = TimeOnly.FromDateTime(taiwanNow);

        // If before trading hours today (weekday)
        if (currentTime < TradingStart &&
            taiwanNow.DayOfWeek != DayOfWeek.Saturday &&
            taiwanNow.DayOfWeek != DayOfWeek.Sunday)
        {
            return currentDate.ToDateTime(TradingStart);
        }

        // Find next weekday
        var nextDate = currentDate.AddDays(1);
        while (nextDate.DayOfWeek == DayOfWeek.Saturday || nextDate.DayOfWeek == DayOfWeek.Sunday)
        {
            nextDate = nextDate.AddDays(1);
        }

        return nextDate.ToDateTime(TradingStart);
    }

    #endregion

    private void OnDataReceived(object? sender, Weda.SubNode.Abstractions.Events.DataReceivedEvent e)
    {
        _deviceLogger.LogInformation("Received {Count} telemetry measures", e.Data.Count);

        foreach (var measure in e.Data)
        {
            var stockCode = measure.Metadata?.TryGetValue("StockCode", out var code) == true ? code?.ToString() : "N/A";
            var stockName = measure.Metadata?.TryGetValue("StockName", out var name) == true ? name?.ToString() : "";
            var field = measure.Metadata?.TryGetValue("Field", out var f) == true ? f?.ToString() : "Value";
            var unit = measure.Metadata?.TryGetValue("Unit", out var u) == true ? u?.ToString() : "";

            _deviceLogger.LogInformation(
                "  [{Code}] {Name} | {Field}: {Value} {Unit}",
                stockCode, stockName, field, measure.Value, unit);
        }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    ~StockMonitorDevice()
    {
        DataReceived -= OnDataReceived;
    }
}