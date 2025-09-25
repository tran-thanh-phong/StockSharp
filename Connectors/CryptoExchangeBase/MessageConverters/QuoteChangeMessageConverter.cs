namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Converter for CryptoExchange.Net order book data to StockSharp QuoteChangeMessage.
/// </summary>
public class QuoteChangeMessageConverter : IMessageConverter<object, QuoteChangeMessage>
{
    /// <inheritdoc />
    public bool CanConvert(Type sourceType, Type targetType)
    {
        return targetType == typeof(QuoteChangeMessage) && IsOrderBookType(sourceType);
    }

    /// <inheritdoc />
    public QuoteChangeMessage Convert(object source, SecurityId securityId)
    {
        if (!IsOrderBookType(source.GetType()))
        {
            throw new InvalidOperationException($"Unsupported source type for order book conversion: {source.GetType().Name}");
        }

        var timestamp = GetPropertyValue<DateTimeOffset?>(source, "Timestamp", "LastUpdateTime", "UpdateTime", "E") ?? DateTimeOffset.UtcNow;
        var bidsData = GetOrderBookSide(source, "Bids", "B", "b");
        var asksData = GetOrderBookSide(source, "Asks", "A", "a");

        var bids = ConvertOrderBookEntries(bidsData, true);
        var asks = ConvertOrderBookEntries(asksData, false);

        // Validate order book integrity
        ValidateOrderBook(bids, asks, securityId);

        return new QuoteChangeMessage
        {
            SecurityId = securityId,
            Bids = bids,
            Asks = asks,
            ServerTime = timestamp,
            LocalTime = DateTimeOffset.Now,
            IsByLevel = true,
            BuildFrom = DataType.MarketDepth
        };
    }

    /// <inheritdoc />
    Message IMessageConverter.Convert(object source, SecurityId securityId) => Convert(source, securityId);

    private static object? GetOrderBookSide(object orderBook, params string[] propertyNames)
    {
        var type = orderBook.GetType();

        foreach (var propName in propertyNames)
        {
            var property = type.GetProperty(propName);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(orderBook);
                if (value != null)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static QuoteChange[] ConvertOrderBookEntries(object? entries, bool isBid)
    {
        if (entries == null)
            return Array.Empty<QuoteChange>();

        var result = new List<QuoteChange>();

        // Handle different collection types (arrays, IEnumerable, etc.)
        if (entries is System.Collections.IEnumerable enumerable)
        {
            foreach (var entry in enumerable)
            {
                if (entry == null) continue;

                var price = GetEntryValue<decimal>(entry, "Price", "P", 0);
                var volume = GetEntryValue<decimal>(entry, "Quantity", "Volume", "Q", 1);

                if (price > 0 && volume > 0)
                {
                    result.Add(new QuoteChange(price, volume));
                }
            }
        }
        // Handle array format [price, quantity] common in many exchanges
        else if (entries is decimal[][] arrayEntries)
        {
            foreach (var entry in arrayEntries)
            {
                if (entry.Length >= 2 && entry[0] > 0 && entry[1] > 0)
                {
                    result.Add(new QuoteChange(entry[0], entry[1]));
                }
            }
        }

        // Sort according to order book rules
        var sorted = isBid
            ? result.OrderByDescending(q => q.Price).ToArray() // Bids: highest price first
            : result.OrderBy(q => q.Price).ToArray();           // Asks: lowest price first

        return sorted;
    }

    private static T GetEntryValue<T>(object entry, string propertyName, string alternativeName, int arrayIndex)
        where T : struct
    {
        var type = entry.GetType();

        // Try property access first
        var property = type.GetProperty(propertyName) ?? type.GetProperty(alternativeName);
        if (property != null && property.CanRead)
        {
            var value = property.GetValue(entry);
            if (value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
        }

        // Try array access if entry is an array
        if (entry is decimal[] array && array.Length > arrayIndex)
        {
            return (T)Convert.ChangeType(array[arrayIndex], typeof(T));
        }

        return default(T);
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

    private static void ValidateOrderBook(QuoteChange[] bids, QuoteChange[] asks, SecurityId securityId)
    {
        // Check for crossed market
        if (bids.Length > 0 && asks.Length > 0)
        {
            var bestBid = bids[0].Price;
            var bestAsk = asks[0].Price;

            if (bestBid >= bestAsk)
            {
                throw new InvalidOperationException(
                    $"Crossed market detected for {securityId}: best bid {bestBid} >= best ask {bestAsk}");
            }
        }

        // Validate bid sorting (descending)
        for (int i = 1; i < bids.Length; i++)
        {
            if (bids[i].Price > bids[i - 1].Price)
            {
                throw new InvalidOperationException(
                    $"Invalid bid sorting for {securityId}: price {bids[i].Price} > previous {bids[i - 1].Price}");
            }
        }

        // Validate ask sorting (ascending)
        for (int i = 1; i < asks.Length; i++)
        {
            if (asks[i].Price < asks[i - 1].Price)
            {
                throw new InvalidOperationException(
                    $"Invalid ask sorting for {securityId}: price {asks[i].Price} < previous {asks[i - 1].Price}");
            }
        }
    }

    private static bool IsOrderBookType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("orderbook") ||
               typeName.Contains("depth") ||
               typeName.Contains("book") ||
               (typeName.Contains("market") && typeName.Contains("data"));
    }
}