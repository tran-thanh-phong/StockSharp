namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Converter for CryptoExchange.Net trade/order data to StockSharp ExecutionMessage.
/// </summary>
public class ExecutionMessageConverter : IMessageConverter<object, ExecutionMessage>
{
    /// <inheritdoc />
    public bool CanConvert(Type sourceType, Type targetType)
    {
        return targetType == typeof(ExecutionMessage) &&
               (IsTradeType(sourceType) || IsOrderType(sourceType));
    }

    /// <inheritdoc />
    public ExecutionMessage Convert(object source, SecurityId securityId)
    {
        return source switch
        {
            _ when IsTradeType(source.GetType()) => ConvertTrade(source, securityId),
            _ when IsOrderType(source.GetType()) => ConvertOrder(source, securityId),
            _ => throw new InvalidOperationException($"Unsupported source type: {source.GetType().Name}")
        };
    }

    /// <inheritdoc />
    Message IMessageConverter.Convert(object source, SecurityId securityId) => Convert(source, securityId);

    private static ExecutionMessage ConvertTrade(object trade, SecurityId securityId)
    {
        // Use reflection to extract common trade properties from CryptoExchange.Net objects
        var tradeType = trade.GetType();

        var tradeId = GetPropertyValue<long?>(trade, "Id", "TradeId", "T") ?? 0;
        var price = GetPropertyValue<decimal>(trade, "Price", "P") ?? 0;
        var quantity = GetPropertyValue<decimal>(trade, "Quantity", "Volume", "Q") ?? 0;
        var timestamp = GetPropertyValue<DateTimeOffset?>(trade, "Timestamp", "TradeTime", "Time", "E") ?? DateTimeOffset.UtcNow;
        var isBuyerMaker = GetPropertyValue<bool?>(trade, "IsBuyerMaker", "BuyerIsMaker", "m") ?? false;

        return new ExecutionMessage
        {
            SecurityId = securityId,
            DataType = DataType.Ticks,
            TradeId = tradeId,
            TradePrice = price,
            TradeVolume = quantity,
            OriginSide = isBuyerMaker ? Sides.Sell : Sides.Buy, // Buyer is maker = taker was seller
            ServerTime = timestamp,
            LocalTime = DateTimeOffset.Now
        };
    }

    private static ExecutionMessage ConvertOrder(object order, SecurityId securityId)
    {
        // Use reflection to extract common order properties from CryptoExchange.Net objects
        var orderType = order.GetType();

        var orderId = GetPropertyValue<long?>(order, "Id", "OrderId", "i") ?? 0;
        var clientOrderId = GetPropertyValue<string>(order, "ClientOrderId", "c");
        var status = GetPropertyValue<string>(order, "Status", "X") ?? "UNKNOWN";
        var side = GetPropertyValue<string>(order, "Side", "S") ?? "UNKNOWN";
        var orderTypeStr = GetPropertyValue<string>(order, "Type", "OrderType", "o") ?? "UNKNOWN";
        var price = GetPropertyValue<decimal?>(order, "Price", "p") ?? 0;
        var quantity = GetPropertyValue<decimal?>(order, "Quantity", "q") ?? 0;
        var executedQty = GetPropertyValue<decimal?>(order, "ExecutedQuantity", "QuantityFilled", "z") ?? 0;
        var timestamp = GetPropertyValue<DateTimeOffset?>(order, "CreateTime", "UpdateTime", "T", "E") ?? DateTimeOffset.UtcNow;

        var orderState = MapOrderStatus(status);
        var orderSide = MapOrderSide(side);
        var mappedOrderType = MapOrderType(orderTypeStr);

        return new ExecutionMessage
        {
            SecurityId = securityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OrderId = orderId,
            OriginalTransactionId = ParseLongOrDefault(clientOrderId),
            OrderState = orderState,
            Side = orderSide,
            OrderType = mappedOrderType,
            Price = price,
            Volume = quantity,
            Balance = quantity - executedQty,
            ServerTime = timestamp,
            LocalTime = DateTimeOffset.Now
        };
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

    private static OrderStates MapOrderStatus(string status)
    {
        return status?.ToUpperInvariant() switch
        {
            "NEW" => OrderStates.Active,
            "PARTIALLY_FILLED" => OrderStates.Active,
            "FILLED" => OrderStates.Done,
            "CANCELED" => OrderStates.Done,
            "PENDING_CANCEL" => OrderStates.Active,
            "REJECTED" => OrderStates.Failed,
            "EXPIRED" => OrderStates.Done,
            _ => OrderStates.Pending
        };
    }

    private static Sides MapOrderSide(string side)
    {
        return side?.ToUpperInvariant() switch
        {
            "BUY" => Sides.Buy,
            "SELL" => Sides.Sell,
            _ => Sides.Buy
        };
    }

    private static OrderTypes MapOrderType(string orderType)
    {
        return orderType?.ToUpperInvariant() switch
        {
            "LIMIT" => OrderTypes.Limit,
            "MARKET" => OrderTypes.Market,
            // "STOP_LOSS" => OrderTypes.StopLoss, // Not available in current StockSharp version
            // "STOP_LOSS_LIMIT" => OrderTypes.StopLimit, // Not available in current StockSharp version
            // "TAKE_PROFIT" => OrderTypes.TakeProfit, // Not available in current StockSharp version
            // "TAKE_PROFIT_LIMIT" => OrderTypes.TakeProfitLimit, // Not available in current StockSharp version
            _ => OrderTypes.Limit
        };
    }

    private static long ParseLongOrDefault(string? value)
    {
        return long.TryParse(value, out var result) ? result : 0;
    }

    private static bool IsTradeType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("trade") ||
               typeName.Contains("tick") ||
               typeName.Contains("execution");
    }

    private static bool IsOrderType(Type type)
    {
        var typeName = type.Name.ToLowerInvariant();
        return typeName.Contains("order") &&
               !typeName.Contains("orderbook") &&
               !typeName.Contains("book");
    }
}