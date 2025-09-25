using Binance.Net.Enums;

namespace StockSharp.Binance.Spot;

/// <summary>
/// Settings and configuration for BinanceSpotMessageAdapter.
/// </summary>
public partial class BinanceSpotMessageAdapter
{
    private TimeSpan _receiveWindow = TimeSpan.FromSeconds(5);
    private int _maxReconnectAttempts = 10;
    private BinanceKlineInterval _defaultCandleInterval = BinanceKlineInterval.OneMinute;

    /// <summary>
    /// Receive window for API requests (default: 5 seconds).
    /// </summary>
    public TimeSpan ReceiveWindow
    {
        get => _receiveWindow;
        set
        {
            _receiveWindow = value;
            if (RestClient != null)
            {
                // Update existing client configuration if available
                this.AddInfoLog("Receive window updated to {0}", value);
            }
        }
    }

    /// <summary>
    /// Maximum number of reconnection attempts (default: 10).
    /// </summary>
    public int MaxReconnectAttempts
    {
        get => _maxReconnectAttempts;
        set
        {
            _maxReconnectAttempts = Math.Max(1, value);
            if (SocketClient != null)
            {
                this.AddInfoLog("Max reconnect attempts updated to {0}", _maxReconnectAttempts);
            }
        }
    }

    /// <summary>
    /// Default candle interval for subscriptions.
    /// </summary>
    public BinanceKlineInterval DefaultCandleInterval
    {
        get => _defaultCandleInterval;
        set => _defaultCandleInterval = value;
    }

    /// <summary>
    /// Enable/disable order book local management.
    /// </summary>
    public bool MaintainLocalOrderBook { get; set; } = true;

    /// <summary>
    /// Order book depth levels to maintain.
    /// </summary>
    public int OrderBookDepth { get; set; } = 20;

    /// <summary>
    /// Enable detailed logging for debugging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }

    /// <summary>
    /// Custom request timeout for REST API calls.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validate trading symbols against exchange info.
    /// </summary>
    public bool ValidateSymbols { get; set; } = true;

    /// <summary>
    /// Configure additional REST client settings.
    /// </summary>
    /// <param name="options">REST options to configure</param>
    protected override void ConfigureRestClientOptions(BinanceRestOptions options)
    {
        base.ConfigureRestClientOptions(options);

        options.RequestTimeout = RequestTimeout;
        options.ReceiveWindow = ReceiveWindow;

        if (EnableDetailedLogging)
        {
            options.LogLevel = LogLevel.Debug;
            options.LogWriters.Add(new DebugLogWriter());
        }

        // Configure proxy if needed
        if (!string.IsNullOrEmpty(Proxy?.Address.ToString()))
        {
            options.Proxy = Proxy;
            this.AddInfoLog("Using proxy: {0}", Proxy.Address);
        }
    }

    /// <summary>
    /// Configure additional WebSocket client settings.
    /// </summary>
    /// <param name="options">Socket options to configure</param>
    protected override void ConfigureSocketClientOptions(BinanceSocketOptions options)
    {
        base.ConfigureSocketClientOptions(options);

        options.MaxReconnectAttempts = MaxReconnectAttempts;

        if (EnableDetailedLogging)
        {
            options.LogLevel = LogLevel.Debug;
            options.LogWriters.Add(new DebugLogWriter());
        }

        // Configure proxy if needed
        if (!string.IsNullOrEmpty(Proxy?.Address.ToString()))
        {
            options.Proxy = Proxy;
        }

        // Configure socket specific settings
        options.SocketNoDelay = true;
        options.SocketReceiveBufferSize = 8192;
    }

    /// <summary>
    /// Validate symbol format for Binance.
    /// </summary>
    /// <param name="symbol">Symbol to validate</param>
    /// <returns>True if valid</returns>
    protected bool IsValidBinanceSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return false;

        // Binance symbols are uppercase, alphanumeric, no separators
        return symbol.All(char.IsLetterOrDigit) &&
               symbol.All(char.IsUpper) &&
               symbol.Length >= 6 &&
               symbol.Length <= 20;
    }

    /// <summary>
    /// Convert StockSharp TimeSpan to Binance interval.
    /// </summary>
    /// <param name="timeFrame">StockSharp time frame</param>
    /// <returns>Binance interval</returns>
    protected BinanceKlineInterval ToBinanceInterval(TimeSpan timeFrame)
    {
        return timeFrame switch
        {
            { TotalMinutes: 1 } => BinanceKlineInterval.OneMinute,
            { TotalMinutes: 3 } => BinanceKlineInterval.ThreeMinutes,
            { TotalMinutes: 5 } => BinanceKlineInterval.FiveMinutes,
            { TotalMinutes: 15 } => BinanceKlineInterval.FifteenMinutes,
            { TotalMinutes: 30 } => BinanceKlineInterval.ThirtyMinutes,
            { TotalHours: 1 } => BinanceKlineInterval.OneHour,
            { TotalHours: 2 } => BinanceKlineInterval.TwoHour,
            { TotalHours: 4 } => BinanceKlineInterval.FourHour,
            { TotalHours: 6 } => BinanceKlineInterval.SixHour,
            { TotalHours: 8 } => BinanceKlineInterval.EightHour,
            { TotalHours: 12 } => BinanceKlineInterval.TwelveHour,
            { TotalDays: 1 } => BinanceKlineInterval.OneDay,
            { TotalDays: 3 } => BinanceKlineInterval.ThreeDay,
            { TotalDays: 7 } => BinanceKlineInterval.OneWeek,
            _ => DefaultCandleInterval
        };
    }

    /// <summary>
    /// Custom debug log writer for detailed logging.
    /// </summary>
    private class DebugLogWriter : ILogWriter
    {
        public void Write(LogVerbosity level, string message)
        {
            System.Diagnostics.Debug.WriteLine($"[Binance.Net] {level}: {message}");
        }
    }
}