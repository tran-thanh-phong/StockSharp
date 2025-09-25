namespace StockSharp.CryptoExchange.Extensions;

/// <summary>
/// Extension methods for CryptoExchange framework.
/// </summary>
public static class CryptoExchangeExtensions
{
    /// <summary>
    /// Convert StockSharp TimeSpan to exchange-specific interval string.
    /// </summary>
    /// <param name="timeFrame">StockSharp time frame</param>
    /// <param name="exchangeType">Exchange type for format conversion</param>
    /// <returns>Exchange interval string</returns>
    public static string ToExchangeInterval(this TimeSpan timeFrame, ExchangeType exchangeType = ExchangeType.Binance)
    {
        return exchangeType switch
        {
            ExchangeType.Binance => ToBinanceInterval(timeFrame),
            ExchangeType.Bybit => ToBybitInterval(timeFrame),
            ExchangeType.OKX => ToOKXInterval(timeFrame),
            _ => ToBinanceInterval(timeFrame) // Default to Binance format
        };
    }

    /// <summary>
    /// Convert exchange interval string to StockSharp TimeSpan.
    /// </summary>
    /// <param name="interval">Exchange interval string</param>
    /// <param name="exchangeType">Exchange type</param>
    /// <returns>StockSharp TimeSpan</returns>
    public static TimeSpan FromExchangeInterval(string interval, ExchangeType exchangeType = ExchangeType.Binance)
    {
        return exchangeType switch
        {
            ExchangeType.Binance => FromBinanceInterval(interval),
            ExchangeType.Bybit => FromBybitInterval(interval),
            ExchangeType.OKX => FromOKXInterval(interval),
            _ => FromBinanceInterval(interval) // Default
        };
    }

    /// <summary>
    /// Convert StockSharp OrderType to exchange-specific order type.
    /// </summary>
    /// <param name="orderType">StockSharp order type</param>
    /// <param name="exchangeType">Exchange type</param>
    /// <returns>Exchange order type string</returns>
    public static string ToExchangeOrderType(this OrderTypes orderType, ExchangeType exchangeType = ExchangeType.Binance)
    {
        return exchangeType switch
        {
            ExchangeType.Binance => orderType switch
            {
                OrderTypes.Market => "MARKET",
                OrderTypes.Limit => "LIMIT",
                OrderTypes.StopLoss => "STOP_LOSS",
                OrderTypes.StopLimit => "STOP_LOSS_LIMIT",
                OrderTypes.TakeProfit => "TAKE_PROFIT",
                OrderTypes.TakeProfitLimit => "TAKE_PROFIT_LIMIT",
                _ => "LIMIT"
            },
            _ => orderType.ToString().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Convert StockSharp Sides to exchange-specific side string.
    /// </summary>
    /// <param name="side">StockSharp side</param>
    /// <returns>Exchange side string</returns>
    public static string ToExchangeSide(this Sides side)
    {
        return side switch
        {
            Sides.Buy => "BUY",
            Sides.Sell => "SELL",
            _ => "BUY"
        };
    }

    /// <summary>
    /// Convert StockSharp TimeInForce to exchange-specific time in force.
    /// </summary>
    /// <param name="timeInForce">StockSharp time in force</param>
    /// <param name="exchangeType">Exchange type</param>
    /// <returns>Exchange time in force string</returns>
    public static string? ToExchangeTimeInForce(this TimeInForce? timeInForce, ExchangeType exchangeType = ExchangeType.Binance)
    {
        if (!timeInForce.HasValue) return null;

        return exchangeType switch
        {
            ExchangeType.Binance => timeInForce.Value switch
            {
                TimeInForce.GTC => "GTC",
                TimeInForce.IOC => "IOC",
                TimeInForce.FOK => "FOK",
                _ => "GTC"
            },
            _ => timeInForce.Value.ToString().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Validate symbol format for exchange.
    /// </summary>
    /// <param name="symbol">Symbol to validate</param>
    /// <param name="exchangeType">Exchange type</param>
    /// <returns>True if valid</returns>
    public static bool IsValidSymbol(string symbol, ExchangeType exchangeType = ExchangeType.Binance)
    {
        if (string.IsNullOrEmpty(symbol)) return false;

        return exchangeType switch
        {
            ExchangeType.Binance => IsValidBinanceSymbol(symbol),
            ExchangeType.Bybit => IsValidBybitSymbol(symbol),
            _ => !string.IsNullOrWhiteSpace(symbol)
        };
    }

    /// <summary>
    /// Format decimal value according to exchange precision rules.
    /// </summary>
    /// <param name="value">Value to format</param>
    /// <param name="precision">Decimal precision</param>
    /// <returns>Formatted value</returns>
    public static decimal FormatPrecision(this decimal value, decimal precision)
    {
        if (precision <= 0) return value;

        var multiplier = 1m / precision;
        return Math.Floor(value * multiplier) / multiplier;
    }

    /// <summary>
    /// Get decimal places count from step size.
    /// </summary>
    /// <param name="stepSize">Step size value</param>
    /// <returns>Number of decimal places</returns>
    public static int GetDecimalPlaces(this decimal stepSize)
    {
        if (stepSize >= 1) return 0;

        var str = stepSize.ToString("0.##################");
        var decimalIndex = str.IndexOf('.');

        return decimalIndex == -1 ? 0 : str.Length - decimalIndex - 1;
    }

    /// <summary>
    /// Create error message from CryptoExchange.Net error.
    /// </summary>
    /// <param name="error">CryptoExchange.Net error</param>
    /// <returns>StockSharp error message</returns>
    public static ErrorMessage CreateErrorMessage(this Error error)
    {
        return new ErrorMessage
        {
            Error = new InvalidOperationException($"[{error.Code}] {error.Message}"),
            LocalTime = DateTimeOffset.Now
        };
    }

    private static string ToBinanceInterval(TimeSpan timeFrame)
    {
        return timeFrame switch
        {
            { TotalMinutes: 1 } => "1m",
            { TotalMinutes: 3 } => "3m",
            { TotalMinutes: 5 } => "5m",
            { TotalMinutes: 15 } => "15m",
            { TotalMinutes: 30 } => "30m",
            { TotalHours: 1 } => "1h",
            { TotalHours: 2 } => "2h",
            { TotalHours: 4 } => "4h",
            { TotalHours: 6 } => "6h",
            { TotalHours: 8 } => "8h",
            { TotalHours: 12 } => "12h",
            { TotalDays: 1 } => "1d",
            { TotalDays: 3 } => "3d",
            { TotalDays: 7 } => "1w",
            _ => "1m"
        };
    }

    private static TimeSpan FromBinanceInterval(string interval)
    {
        return interval?.ToLowerInvariant() switch
        {
            "1m" => TimeSpan.FromMinutes(1),
            "3m" => TimeSpan.FromMinutes(3),
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "30m" => TimeSpan.FromMinutes(30),
            "1h" => TimeSpan.FromHours(1),
            "2h" => TimeSpan.FromHours(2),
            "4h" => TimeSpan.FromHours(4),
            "6h" => TimeSpan.FromHours(6),
            "8h" => TimeSpan.FromHours(8),
            "12h" => TimeSpan.FromHours(12),
            "1d" => TimeSpan.FromDays(1),
            "3d" => TimeSpan.FromDays(3),
            "1w" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private static string ToBybitInterval(TimeSpan timeFrame) => ToBinanceInterval(timeFrame); // Same format
    private static TimeSpan FromBybitInterval(string interval) => FromBinanceInterval(interval); // Same format

    private static string ToOKXInterval(TimeSpan timeFrame)
    {
        return timeFrame switch
        {
            { TotalMinutes: 1 } => "1m",
            { TotalMinutes: 3 } => "3m",
            { TotalMinutes: 5 } => "5m",
            { TotalMinutes: 15 } => "15m",
            { TotalMinutes: 30 } => "30m",
            { TotalHours: 1 } => "1H",
            { TotalHours: 2 } => "2H",
            { TotalHours: 4 } => "4H",
            { TotalHours: 6 } => "6H",
            { TotalHours: 12 } => "12H",
            { TotalDays: 1 } => "1D",
            { TotalDays: 7 } => "1W",
            _ => "1m"
        };
    }

    private static TimeSpan FromOKXInterval(string interval)
    {
        return interval?.ToUpperInvariant() switch
        {
            "1M" => TimeSpan.FromMinutes(1),
            "3M" => TimeSpan.FromMinutes(3),
            "5M" => TimeSpan.FromMinutes(5),
            "15M" => TimeSpan.FromMinutes(15),
            "30M" => TimeSpan.FromMinutes(30),
            "1H" => TimeSpan.FromHours(1),
            "2H" => TimeSpan.FromHours(2),
            "4H" => TimeSpan.FromHours(4),
            "6H" => TimeSpan.FromHours(6),
            "12H" => TimeSpan.FromHours(12),
            "1D" => TimeSpan.FromDays(1),
            "1W" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private static bool IsValidBinanceSymbol(string symbol)
    {
        // Binance symbols: BTCUSDT, ETHBTC, etc. (uppercase, no separators)
        return symbol.All(char.IsLetterOrDigit) &&
               symbol.All(char.IsUpper) &&
               symbol.Length >= 6 &&
               symbol.Length <= 20;
    }

    private static bool IsValidBybitSymbol(string symbol)
    {
        // Bybit symbols: BTCUSDT, BTC-31MAR23, etc.
        return symbol.All(c => char.IsLetterOrDigit(c) || c == '-') &&
               symbol.Length >= 3;
    }
}

/// <summary>
/// Supported exchange types.
/// </summary>
public enum ExchangeType
{
    Binance,
    Bybit,
    OKX,
    KuCoin,
    Bitget
}