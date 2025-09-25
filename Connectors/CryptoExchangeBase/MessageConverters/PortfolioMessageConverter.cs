namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Converter for CryptoExchange.Net account data to StockSharp PositionChangeMessage.
/// </summary>
public class PortfolioMessageConverter : IMessageConverter<object, PositionChangeMessage>
{
    private readonly string _portfolioName;

    /// <summary>
    /// Initialize portfolio converter with portfolio name.
    /// </summary>
    /// <param name="portfolioName">Portfolio identifier (e.g., "BINANCE", "BINANCE_TESTNET")</param>
    public PortfolioMessageConverter(string portfolioName)
    {
        _portfolioName = portfolioName ?? throw new ArgumentNullException(nameof(portfolioName));
    }

    /// <inheritdoc />
    public bool CanConvert(Type sourceType, Type targetType)
    {
        return targetType == typeof(PositionChangeMessage) && IsBalanceType(sourceType);
    }

    /// <inheritdoc />
    public PositionChangeMessage Convert(object source, SecurityId securityId)
    {
        if (!IsBalanceType(source.GetType()))
        {
            throw new InvalidOperationException($"Unsupported source type for portfolio conversion: {source.GetType().Name}");
        }

        var asset = GetPropertyValue<string>(source, "Asset", "Currency", "Coin", "a") ?? "UNKNOWN";
        var free = GetPropertyValue<decimal?>(source, "Available", "Free", "f") ?? 0;
        var locked = GetPropertyValue<decimal?>(source, "Locked", "Reserved", "l") ?? 0;
        var total = free + locked;

        // Handle wallet balance format
        var walletBalance = GetPropertyValue<decimal?>(source, "WalletBalance", "Balance");
        if (walletBalance.HasValue)
        {
            total = walletBalance.Value;
            free = total - locked;
        }

        var timestamp = GetPropertyValue<DateTimeOffset?>(source, "UpdateTime", "Timestamp", "E") ?? DateTimeOffset.UtcNow;

        return new PositionChangeMessage
        {
            SecurityId = new SecurityId { SecurityCode = asset, BoardCode = _portfolioName },
            ServerTime = timestamp,
            LocalTime = DateTimeOffset.Now
        }
        .TryAdd(PositionChangeTypes.CurrentValue, free)
        .TryAdd(PositionChangeTypes.BlockedValue, locked);
    }

    /// <inheritdoc />
    Message IMessageConverter.Convert(object source, SecurityId securityId) => Convert(source, securityId);

    /// <summary>
    /// Convert account info object to multiple position change messages.
    /// </summary>
    /// <param name="accountInfo">Account information object</param>
    /// <param name="portfolioName">Portfolio name</param>
    /// <returns>Collection of position change messages for each asset</returns>
    public static IEnumerable<PositionChangeMessage> ConvertAccountInfo(object accountInfo, string portfolioName)
    {
        var balances = GetPropertyValue<object[]>(accountInfo, "Balances", "Assets");
        if (balances == null || balances.Length == 0)
        {
            yield break;
        }

        var converter = new PortfolioMessageConverter(portfolioName);

        foreach (var balance in balances)
        {
            if (balance == null) continue;

            try
            {
                var positionMessage = converter.Convert(balance, new SecurityId());

                // Only return balances with non-zero values
                var currentValue = positionMessage.TryGetDecimal(PositionChangeTypes.CurrentValue) ?? 0;
                var blockedValue = positionMessage.TryGetDecimal(PositionChangeTypes.BlockedValue) ?? 0;

                if (currentValue > 0 || blockedValue > 0)
                {
                    yield return positionMessage;
                }
            }
            catch (Exception)
            {
                // Skip invalid balance entries
                continue;
            }
        }
    }

    private static T? GetPropertyValue<T>(object obj, params string[] propertyNames)
    {
        var type = obj.GetType();

        foreach (var propName in propertyNames)
        {
            var property = type.GetProperty(propName);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
        }

        return default(T);
    }

    private static CurrencyTypes DetermineCurrency(string asset)
    {
        return asset?.ToUpperInvariant() switch
        {
            "USD" or "USDT" or "USDC" or "BUSD" or "TUSD" => CurrencyTypes.USD,
            "EUR" or "EURS" => CurrencyTypes.EUR,
            "GBP" => CurrencyTypes.GBP,
            "JPY" => CurrencyTypes.JPY,
            "CNY" or "CNH" => CurrencyTypes.CNY,
            "RUB" => CurrencyTypes.RUB,
            "BTC" => CurrencyTypes.BTC,
            "ETH" => CurrencyTypes.ETH,
            _ => CurrencyTypes.USD // Default fallback
        };
    }

    private static bool IsBalanceType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("balance") ||
               typeName.Contains("asset") ||
               typeName.Contains("wallet") ||
               typeName.Contains("account");
    }
}

/// <summary>
/// Converter for position updates (futures trading).
/// </summary>
public class PositionMessageConverter : IMessageConverter<object, PositionChangeMessage>
{
    /// <inheritdoc />
    public bool CanConvert(Type sourceType, Type targetType)
    {
        return targetType == typeof(PositionChangeMessage) && IsPositionType(sourceType);
    }

    /// <inheritdoc />
    public PositionChangeMessage Convert(object source, SecurityId securityId)
    {
        if (!IsPositionType(source.GetType()))
        {
            throw new InvalidOperationException($"Unsupported source type for position conversion: {source.GetType().Name}");
        }

        var symbol = GetPropertyValue<string>(source, "Symbol", "s") ?? securityId.SecurityCode;
        var positionSize = GetPropertyValue<decimal?>(source, "PositionAmount", "Size", "pa") ?? 0;
        var entryPrice = GetPropertyValue<decimal?>(source, "EntryPrice", "AveragePrice", "ep") ?? 0;
        var unrealizedPnl = GetPropertyValue<decimal?>(source, "UnrealizedPnl", "PnL", "up") ?? 0;
        var timestamp = GetPropertyValue<DateTimeOffset?>(source, "UpdateTime", "Timestamp", "E") ?? DateTimeOffset.UtcNow;

        // Determine if position is long or short
        var side = positionSize >= 0 ? Sides.Buy : Sides.Sell;

        return new PositionChangeMessage
        {
            SecurityId = new SecurityId { SecurityCode = symbol, BoardCode = securityId.BoardCode },
            ServerTime = timestamp,
            LocalTime = DateTimeOffset.Now
        }
        .TryAdd(PositionChangeTypes.CurrentValue, Math.Abs(positionSize))
        .TryAdd(PositionChangeTypes.AveragePrice, entryPrice)
        .TryAdd(PositionChangeTypes.UnrealizedPnL, unrealizedPnl);
    }

    /// <inheritdoc />
    Message IMessageConverter.Convert(object source, SecurityId securityId) => Convert(source, securityId);

    private static T? GetPropertyValue<T>(object obj, params string[] propertyNames)
    {
        var type = obj.GetType();

        foreach (var propName in propertyNames)
        {
            var property = type.GetProperty(propName);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
        }

        return default(T);
    }

    private static bool IsPositionType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("position") ||
               (typeName.Contains("account") && typeName.Contains("update"));
    }
}