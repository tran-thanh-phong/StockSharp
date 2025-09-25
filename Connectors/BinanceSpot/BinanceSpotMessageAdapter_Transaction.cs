using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;

namespace StockSharp.Binance.Spot;

/// <summary>
/// Transaction handling (orders) for BinanceSpotMessageAdapter.
/// </summary>
public partial class BinanceSpotMessageAdapter
{
    private readonly ConcurrentDictionary<long, long> _orderIdMapping = new(); // StockSharp ID -> Binance ID
    private readonly ConcurrentDictionary<long, long> _reverseOrderMapping = new(); // Binance ID -> StockSharp ID

    /// <inheritdoc />
    protected override void ProcessOrderRegisterMessage(OrderRegisterMessage message)
    {
        Task.Run(async () =>
        {
            try
            {
                if (RestClient == null)
                {
                    SendOrderError(message, "Not connected to REST API");
                    return;
                }

                var symbol = message.SecurityId.SecurityCode;
                if (!IsValidBinanceSymbol(symbol))
                {
                    SendOrderError(message, $"Invalid symbol format: {symbol}");
                    return;
                }

                // Convert StockSharp order to Binance format
                var side = ToBinanceOrderSide(message.Side);
                var orderType = ToBinanceOrderType(message.OrderType);
                var timeInForce = ToBinanceTimeInForce(message.TimeInForce);

                BinancePlacedOrder result;

                // Handle different order types
                if (message.OrderType == OrderTypes.Market)
                {
                    result = await PlaceMarketOrder(symbol, side, message.Volume, message);
                }
                else if (message.OrderType == OrderTypes.Limit)
                {
                    if (!message.Price.HasValue)
                    {
                        SendOrderError(message, "Limit orders require price");
                        return;
                    }

                    result = await PlaceLimitOrder(symbol, side, message.Volume, message.Price.Value, timeInForce, message);
                }
                else
                {
                    SendOrderError(message, $"Order type {message.OrderType} not yet supported");
                    return;
                }

                if (result != null)
                {
                    // Store order mapping
                    _orderIdMapping[message.TransactionId] = result.Id;
                    _reverseOrderMapping[result.Id] = message.TransactionId;

                    // Send confirmation
                    SendOrderConfirmation(message, result);

                    this.AddInfoLog("Order {0} registered: {1} {2} {3} @ {4}",
                        result.Id, side, message.Volume, symbol, message.Price);
                }
            }
            catch (Exception ex)
            {
                SendOrderError(message, ex.Message);
                this.AddErrorLog("Order registration failed for {0}: {1}", message.SecurityId.SecurityCode, ex.Message);
            }
        });
    }

    /// <inheritdoc />
    protected override void ProcessOrderCancelMessage(OrderCancelMessage message)
    {
        Task.Run(async () =>
        {
            try
            {
                if (RestClient == null)
                {
                    SendCancelError(message, "Not connected to REST API");
                    return;
                }

                var symbol = message.SecurityId.SecurityCode;
                if (!IsValidBinanceSymbol(symbol))
                {
                    SendCancelError(message, $"Invalid symbol format: {symbol}");
                    return;
                }

                // Find Binance order ID
                if (!_orderIdMapping.TryGetValue(message.TransactionId, out var binanceOrderId))
                {
                    SendCancelError(message, $"Order not found for transaction {message.TransactionId}");
                    return;
                }

                var result = await RestClient.SpotApi.Trading.CancelOrderAsync(symbol, binanceOrderId);

                if (result.Success)
                {
                    // Send cancellation confirmation
                    SendCancelConfirmation(message, result.Data);

                    this.AddInfoLog("Order {0} cancelled successfully", binanceOrderId);
                }
                else
                {
                    SendCancelError(message, result.Error?.Message ?? "Unknown cancellation error");
                }
            }
            catch (Exception ex)
            {
                SendCancelError(message, ex.Message);
                this.AddErrorLog("Order cancellation failed: {0}", ex.Message);
            }
        });
    }

    /// <inheritdoc />
    protected override void ProcessOrderStatusMessage(OrderStatusMessage message)
    {
        Task.Run(async () =>
        {
            try
            {
                if (RestClient == null)
                {
                    SendStatusError(message, "Not connected to REST API");
                    return;
                }

                var symbol = message.SecurityId.SecurityCode;
                if (!IsValidBinanceSymbol(symbol))
                {
                    SendStatusError(message, $"Invalid symbol format: {symbol}");
                    return;
                }

                // Find Binance order ID
                if (!_orderIdMapping.TryGetValue(message.TransactionId, out var binanceOrderId))
                {
                    SendStatusError(message, $"Order not found for transaction {message.TransactionId}");
                    return;
                }

                var result = await RestClient.SpotApi.Trading.GetOrderAsync(symbol, binanceOrderId);

                if (result.Success)
                {
                    // Send status update
                    SendStatusUpdate(message, result.Data);
                }
                else
                {
                    SendStatusError(message, result.Error?.Message ?? "Unknown status error");
                }
            }
            catch (Exception ex)
            {
                SendStatusError(message, ex.Message);
                this.AddErrorLog("Order status check failed: {0}", ex.Message);
            }
        });
    }

    /// <summary>
    /// Place market order on Binance.
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="side">Order side</param>
    /// <param name="quantity">Order quantity</param>
    /// <param name="message">Original message</param>
    /// <returns>Placed order result</returns>
    private async Task<BinancePlacedOrder?> PlaceMarketOrder(string symbol, OrderSide side, decimal quantity, OrderRegisterMessage message)
    {
        var result = await RestClient!.SpotApi.Trading.PlaceOrderAsync(
            symbol,
            side,
            SpotOrderType.Market,
            quantity: quantity,
            newClientOrderId: message.TransactionId.ToString());

        if (result.Success)
        {
            return result.Data;
        }

        SendOrderError(message, result.Error?.Message ?? "Market order failed");
        return null;
    }

    /// <summary>
    /// Place limit order on Binance.
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="side">Order side</param>
    /// <param name="quantity">Order quantity</param>
    /// <param name="price">Order price</param>
    /// <param name="timeInForce">Time in force</param>
    /// <param name="message">Original message</param>
    /// <returns>Placed order result</returns>
    private async Task<BinancePlacedOrder?> PlaceLimitOrder(string symbol, OrderSide side, decimal quantity, decimal price, TimeInForce timeInForce, OrderRegisterMessage message)
    {
        var result = await RestClient!.SpotApi.Trading.PlaceOrderAsync(
            symbol,
            side,
            SpotOrderType.Limit,
            timeInForce: timeInForce,
            quantity: quantity,
            price: price,
            newClientOrderId: message.TransactionId.ToString());

        if (result.Success)
        {
            return result.Data;
        }

        SendOrderError(message, result.Error?.Message ?? "Limit order failed");
        return null;
    }

    /// <summary>
    /// Convert StockSharp order side to Binance.
    /// </summary>
    /// <param name="side">StockSharp side</param>
    /// <returns>Binance order side</returns>
    private static OrderSide ToBinanceOrderSide(Sides side)
    {
        return side switch
        {
            Sides.Buy => OrderSide.Buy,
            Sides.Sell => OrderSide.Sell,
            _ => OrderSide.Buy
        };
    }

    /// <summary>
    /// Convert StockSharp order type to Binance.
    /// </summary>
    /// <param name="orderType">StockSharp order type</param>
    /// <returns>Binance order type</returns>
    private static SpotOrderType ToBinanceOrderType(OrderTypes orderType)
    {
        return orderType switch
        {
            OrderTypes.Market => SpotOrderType.Market,
            OrderTypes.Limit => SpotOrderType.Limit,
            _ => SpotOrderType.Limit
        };
    }

    /// <summary>
    /// Convert StockSharp time in force to Binance.
    /// </summary>
    /// <param name="timeInForce">StockSharp time in force</param>
    /// <returns>Binance time in force</returns>
    private static TimeInForce ToBinanceTimeInForce(TimeInForce? timeInForce)
    {
        return timeInForce switch
        {
            Messages.TimeInForce.GTC => TimeInForce.GoodTillCanceled,
            Messages.TimeInForce.IOC => TimeInForce.ImmediateOrCancel,
            Messages.TimeInForce.FOK => TimeInForce.FillOrKill,
            _ => TimeInForce.GoodTillCanceled
        };
    }

    /// <summary>
    /// Send order registration confirmation.
    /// </summary>
    /// <param name="originalMessage">Original order message</param>
    /// <param name="binanceOrder">Binance order result</param>
    private void SendOrderConfirmation(OrderRegisterMessage originalMessage, BinancePlacedOrder binanceOrder)
    {
        var executionMessage = new ExecutionMessage
        {
            SecurityId = originalMessage.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = originalMessage.TransactionId,
            OrderId = binanceOrder.Id,
            OrderState = MapBinanceOrderStatus(binanceOrder.Status),
            Side = originalMessage.Side,
            OrderType = originalMessage.OrderType,
            Volume = originalMessage.Volume,
            Price = originalMessage.Price,
            Balance = binanceOrder.Quantity - binanceOrder.QuantityFilled,
            ServerTime = binanceOrder.CreateTime,
            LocalTime = DateTimeOffset.Now
        };

        SendOutMessage(executionMessage);
    }

    /// <summary>
    /// Send order cancellation confirmation.
    /// </summary>
    /// <param name="originalMessage">Original cancel message</param>
    /// <param name="binanceOrder">Binance order result</param>
    private void SendCancelConfirmation(OrderCancelMessage originalMessage, BinanceOrder binanceOrder)
    {
        var executionMessage = new ExecutionMessage
        {
            SecurityId = originalMessage.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = originalMessage.TransactionId,
            OrderId = binanceOrder.Id,
            OrderState = OrderStates.Done,
            IsCancellation = true,
            ServerTime = DateTimeOffset.UtcNow,
            LocalTime = DateTimeOffset.Now
        };

        SendOutMessage(executionMessage);
    }

    /// <summary>
    /// Send order status update.
    /// </summary>
    /// <param name="originalMessage">Original status message</param>
    /// <param name="binanceOrder">Binance order data</param>
    private void SendStatusUpdate(OrderStatusMessage originalMessage, BinanceOrder binanceOrder)
    {
        var executionMessage = new ExecutionMessage
        {
            SecurityId = originalMessage.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = originalMessage.TransactionId,
            OrderId = binanceOrder.Id,
            OrderState = MapBinanceOrderStatus(binanceOrder.Status),
            Volume = binanceOrder.Quantity,
            Price = binanceOrder.Price,
            Balance = binanceOrder.Quantity - binanceOrder.QuantityFilled,
            ServerTime = binanceOrder.UpdateTime ?? binanceOrder.CreateTime,
            LocalTime = DateTimeOffset.Now
        };

        SendOutMessage(executionMessage);
    }

    /// <summary>
    /// Map Binance order status to StockSharp.
    /// </summary>
    /// <param name="status">Binance order status</param>
    /// <returns>StockSharp order state</returns>
    private static OrderStates MapBinanceOrderStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.New => OrderStates.Active,
            OrderStatus.PartiallyFilled => OrderStates.Active,
            OrderStatus.Filled => OrderStates.Done,
            OrderStatus.Canceled => OrderStates.Done,
            OrderStatus.PendingCancel => OrderStates.Active,
            OrderStatus.Rejected => OrderStates.Failed,
            OrderStatus.Expired => OrderStates.Done,
            _ => OrderStates.Pending
        };
    }

    /// <summary>
    /// Send order registration error.
    /// </summary>
    /// <param name="message">Original message</param>
    /// <param name="error">Error description</param>
    private void SendOrderError(OrderRegisterMessage message, string error)
    {
        SendOutMessage(new ExecutionMessage
        {
            SecurityId = message.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = message.TransactionId,
            OrderState = OrderStates.Failed,
            Error = new InvalidOperationException(error),
            LocalTime = DateTimeOffset.Now
        });
    }

    /// <summary>
    /// Send order cancellation error.
    /// </summary>
    /// <param name="message">Original message</param>
    /// <param name="error">Error description</param>
    private void SendCancelError(OrderCancelMessage message, string error)
    {
        SendOutMessage(new ExecutionMessage
        {
            SecurityId = message.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = message.TransactionId,
            Error = new InvalidOperationException(error),
            LocalTime = DateTimeOffset.Now
        });
    }

    /// <summary>
    /// Send order status error.
    /// </summary>
    /// <param name="message">Original message</param>
    /// <param name="error">Error description</param>
    private void SendStatusError(OrderStatusMessage message, string error)
    {
        SendOutMessage(new ExecutionMessage
        {
            SecurityId = message.SecurityId,
            DataType = DataType.Transactions,
            ExecutionType = ExecutionTypes.Transaction,
            OriginalTransactionId = message.TransactionId,
            Error = new InvalidOperationException(error),
            LocalTime = DateTimeOffset.Now
        });
    }
}