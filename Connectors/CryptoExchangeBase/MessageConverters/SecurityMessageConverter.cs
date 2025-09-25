namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Converter for CryptoExchange.Net symbol data to StockSharp SecurityMessage.
/// </summary>
public class SecurityMessageConverter : IMessageConverter<object, SecurityMessage>
{
    /// <inheritdoc />
    public bool CanConvert(Type sourceType, Type targetType)
    {
        return targetType == typeof(SecurityMessage) && IsSymbolType(sourceType);
    }

    /// <inheritdoc />
    public SecurityMessage Convert(object source, SecurityId securityId)
    {
        if (!IsSymbolType(source.GetType()))
        {
            throw new InvalidOperationException($"Unsupported source type for security conversion: {source.GetType().Name}");
        }

        var symbol = GetPropertyValue<string>(source, "Name", "Symbol", "BaseAsset") ?? securityId.SecurityCode;
        var baseAsset = GetPropertyValue<string>(source, "BaseAsset", "Base", "BaseCurrency") ?? "";
        var quoteAsset = GetPropertyValue<string>(source, "QuoteAsset", "Quote", "QuoteCurrency") ?? "";
        var status = GetPropertyValue<string>(source, "Status", "State") ?? "TRADING";

        // Extract trading rules and filters
        var minPrice = GetFilterValue(source, "PRICE_FILTER", "minPrice") ?? 0.00000001m;
        var maxPrice = GetFilterValue(source, "PRICE_FILTER", "maxPrice") ?? 1000000m;
        var tickSize = GetFilterValue(source, "PRICE_FILTER", "tickSize") ?? 0.00000001m;

        var minQty = GetFilterValue(source, "LOT_SIZE", "minQty") ?? 0.00000001m;
        var maxQty = GetFilterValue(source, "LOT_SIZE", "maxQty") ?? 100000000m;
        var stepSize = GetFilterValue(source, "LOT_SIZE", "stepSize") ?? 0.00000001m;

        var minNotional = GetFilterValue(source, "MIN_NOTIONAL", "minNotional") ?? 0.001m;

        // Determine security type based on board code or symbol characteristics
        var securityType = DetermineSecurityType(securityId.BoardCode, symbol);

        return new SecurityMessage
        {
            SecurityId = securityId,
            Name = $"{baseAsset}/{quoteAsset}",
            ShortName = symbol,
            SecurityType = securityType,
            PriceStep = tickSize,
            VolumeStep = stepSize,
            MinVolume = minQty,
            MaxVolume = maxQty,
            Multiplier = 1m,
            Currency = CurrencyTypes.USD, // Default, can be overridden
            State = MapSecurityState(status),
            LocalTime = DateTimeOffset.Now,

            // Additional metadata
            UnderlyingSecurityCode = baseAsset,
            Class = quoteAsset,

            // Lot size and notional constraints
            Decimals = GetDecimalPlaces(tickSize),
            VolumeDecimals = GetDecimalPlaces(stepSize)
        };
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

    private static decimal? GetFilterValue(object symbol, string filterType, string valueProperty)
    {
        // Try to extract filter values from Binance-style filters array
        var filters = GetPropertyValue<object[]>(symbol, "Filters");
        if (filters != null)
        {
            foreach (var filter in filters)
            {
                var type = GetPropertyValue<string>(filter, "FilterType", "Type");
                if (type == filterType)
                {
                    var value = GetPropertyValue<string>(filter, valueProperty);
                    if (decimal.TryParse(value, out var result))
                    {
                        return result;
                    }
                }
            }
        }

        // Fallback: try direct properties
        return GetPropertyValue<decimal?>(symbol, valueProperty);
    }

    private static SecurityTypes DetermineSecurityType(string? boardCode, string symbol)
    {
        if (boardCode != null)
        {
            return boardCode.ToUpperInvariant() switch
            {
                var b when b.Contains("FUTURES") || b.Contains("FAPI") => SecurityTypes.Future,
                var b when b.Contains("OPTION") => SecurityTypes.Option,
                var b when b.Contains("SPOT") || b.Contains("SAPI") => SecurityTypes.Stock,
                _ => SecurityTypes.Stock
            };
        }

        // Analyze symbol format for type hints
        if (symbol.Contains("PERP") || symbol.EndsWith("USD") || symbol.Contains("_"))
        {
            return SecurityTypes.Future;
        }

        return SecurityTypes.Stock; // Default to spot trading
    }

    private static SecurityStates MapSecurityState(string status)
    {
        return status?.ToUpperInvariant() switch
        {
            "TRADING" => SecurityStates.Trading,
            "HALT" => SecurityStates.Stoped,
            "BREAK" => SecurityStates.Stoped,
            "PRE_TRADING" => SecurityStates.Trading,
            "POST_TRADING" => SecurityStates.Trading,
            _ => SecurityStates.Trading
        };
    }

    private static int GetDecimalPlaces(decimal value)
    {
        if (value == 0) return 0;

        var str = value.ToString("0.##################", System.Globalization.CultureInfo.InvariantCulture);
        var decimalIndex = str.IndexOf('.');

        return decimalIndex == -1 ? 0 : str.Length - decimalIndex - 1;
    }

    private static bool IsSymbolType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("symbol") ||
               typeName.Contains("instrument") ||
               typeName.Contains("market") ||
               typeName.Contains("pair") ||
               (typeName.Contains("exchange") && typeName.Contains("info"));
    }
}