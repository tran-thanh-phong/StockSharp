# Order Log (Order-by-Order Data)

Order log provides the most granular level of market data, containing every individual order action (add, modify, cancel) on the exchange. This is the raw data stream before it's aggregated into order book or trade data.

## Overview

Order log data is represented by `ExecutionMessage` with `DataType.OrderLog`. It contains:
- Individual order operations (new, modify, cancel, execution)
- Order-by-order information with full details
- Market microstructure data
- High-frequency trading opportunities
- Order flow analysis

This is the lowest-latency market data type available, as it comes directly from the exchange matching engine.

## ExecutionMessage for OrderLog

When `DataType = DataType.OrderLog`, the ExecutionMessage represents an individual order action:

### Key Properties

```csharp
public class ExecutionMessage : BaseSubscriptionIdMessage<ExecutionMessage>
{
    // Data type indicator
    public DataType DataType { get; set; } // = DataType.OrderLog

    // Security identifier
    public SecurityId SecurityId { get; set; }

    // Server timestamp
    public DateTimeOffset ServerTime { get; set; }

    // Order price
    public decimal OrderPrice { get; set; }

    // Order volume
    public decimal? OrderVolume { get; set; }

    // Order side (Buy or Sell)
    public Sides Side { get; set; }

    // Order ID
    public long? OrderId { get; set; }

    // Order state
    public OrderStates? OrderState { get; set; }

    // Trade information (when order is executed)
    public bool HasTradeInfo { get; }
    public decimal? TradePrice { get; set; }
    public decimal? TradeVolume { get; set; }
    public long? TradeId { get; set; }

    // Order action (for incremental updates)
    public QuoteChangeActions? Action { get; set; }

    // Open interest (futures/options)
    public decimal? OpenInterest { get; set; }

    // Sequence number
    public long SeqNum { get; set; }
}
```

## Order vs Trade in OrderLog

The order log distinguishes between order placements and trade executions:

### Order Entry

```csharp
// New order added to the book
HasOrderInfo = true
HasTradeInfo = false
OrderId = 12345
OrderPrice = 100.50
OrderVolume = 100
Side = Sides.Buy
OrderState = OrderStates.Active
```

### Trade Execution

```csharp
// Order executed (trade occurred)
HasOrderInfo = true
HasTradeInfo = true
OrderId = 12345
OrderPrice = 100.50
OrderVolume = 50  // Remaining volume
TradePrice = 100.50
TradeVolume = 50  // Executed volume
TradeId = 67890
```

### Order Cancellation

```csharp
// Order cancelled/removed
HasOrderInfo = true
HasTradeInfo = false
OrderId = 12345
OrderState = OrderStates.Done or OrderStates.Failed
```

## Subscribing to Order Log

### Basic Subscription

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

var connector = new Connector();

// Subscribe to order log for a security
var security = connector.GetSecurity("AAPL@NASDAQ");
var subscription = connector.Subscribe(new Subscription(DataType.OrderLog, security));
```

### Historical Order Log

```csharp
// Subscribe to historical order log
var subscription = connector.Subscribe(new Subscription(DataType.OrderLog, security)
{
    From = DateTimeOffset.Now.AddDays(-1),
    To = DateTimeOffset.Now
});
```

## Handling Order Log Data

### OrderLogReceived Event

The primary event for receiving order log data:

```csharp
connector.OrderLogReceived += (subscription, order) =>
{
    Console.WriteLine($"Order Log: {order.SecurityId} at {order.ServerTime:HH:mm:ss.fff}");
    Console.WriteLine($"  OrderId: {order.OrderId}");
    Console.WriteLine($"  Side: {order.Side}");
    Console.WriteLine($"  Price: {order.OrderPrice}");
    Console.WriteLine($"  Volume: {order.OrderVolume}");
    Console.WriteLine($"  State: {order.OrderState}");

    if (order.HasTradeInfo)
    {
        Console.WriteLine($"  TRADE:");
        Console.WriteLine($"    TradeId: {order.TradeId}");
        Console.WriteLine($"    TradePrice: {order.TradePrice}");
        Console.WriteLine($"    TradeVolume: {order.TradeVolume}");
    }
};
```

### Distinguishing Order Types

```csharp
connector.OrderLogReceived += (subscription, order) =>
{
    if (order.HasTradeInfo)
    {
        // This is a trade execution
        Console.WriteLine($"TRADE: {order.Side} {order.TradeVolume}@{order.TradePrice}");
    }
    else if (order.OrderState == OrderStates.Active)
    {
        // New order added
        Console.WriteLine($"NEW ORDER: {order.Side} {order.OrderVolume}@{order.OrderPrice}");
    }
    else if (order.OrderState == OrderStates.Done)
    {
        // Order cancelled or completed
        Console.WriteLine($"ORDER REMOVED: {order.OrderId}");
    }
};
```

## Order Flow Analysis

### Example 1: Order Flow Imbalance

```csharp
var buyOrders = 0;
var sellOrders = 0;
var buyVolume = 0m;
var sellVolume = 0m;

connector.OrderLogReceived += (subscription, order) =>
{
    // Only count new orders (not trades or cancellations)
    if (order.HasOrderInfo && !order.HasTradeInfo &&
        order.OrderState == OrderStates.Active)
    {
        if (order.Side == Sides.Buy)
        {
            buyOrders++;
            buyVolume += order.OrderVolume ?? 0;
        }
        else
        {
            sellOrders++;
            sellVolume += order.OrderVolume ?? 0;
        }

        var totalOrders = buyOrders + sellOrders;
        if (totalOrders > 0 && totalOrders % 100 == 0)
        {
            var buyRatio = (buyOrders * 100.0) / totalOrders;
            var volumeRatio = buyVolume / (buyVolume + sellVolume) * 100;

            Console.WriteLine($"Order Flow Analysis:");
            Console.WriteLine($"  Buy Orders: {buyRatio:F2}%");
            Console.WriteLine($"  Buy Volume: {volumeRatio:F2}%");

            if (buyRatio > 60)
                Console.WriteLine("  => Strong buying interest");
            else if (buyRatio < 40)
                Console.WriteLine("  => Strong selling interest");
        }
    }
};
```

### Example 2: Large Order Detection

```csharp
var averageSize = 100m;
var largeOrderThreshold = 5; // 5x average

connector.OrderLogReceived += (subscription, order) =>
{
    // Only track new orders
    if (order.HasOrderInfo && !order.HasTradeInfo &&
        order.OrderState == OrderStates.Active)
    {
        var volume = order.OrderVolume ?? 0;

        // Update rolling average
        averageSize = (averageSize * 0.99m) + (volume * 0.01m);

        // Detect large orders
        if (volume > averageSize * largeOrderThreshold)
        {
            Console.WriteLine($"LARGE ORDER DETECTED!");
            Console.WriteLine($"  Time: {order.ServerTime:HH:mm:ss.fff}");
            Console.WriteLine($"  Side: {order.Side}");
            Console.WriteLine($"  Price: {order.OrderPrice:F2}");
            Console.WriteLine($"  Size: {volume:F0} ({(volume / averageSize):F1}x avg)");

            // This could indicate institutional activity
            if (order.Side == Sides.Buy)
                Console.WriteLine("  => Potential institutional buying");
            else
                Console.WriteLine("  => Potential institutional selling");
        }
    }
};
```

### Example 3: Order Execution Tracking

```csharp
var orderLifetime = new Dictionary<long, (DateTimeOffset placed, decimal volume)>();

connector.OrderLogReceived += (subscription, order) =>
{
    if (!order.OrderId.HasValue)
        return;

    var orderId = order.OrderId.Value;

    // Track order placement
    if (order.HasOrderInfo && !order.HasTradeInfo &&
        order.OrderState == OrderStates.Active &&
        !orderLifetime.ContainsKey(orderId))
    {
        orderLifetime[orderId] = (order.ServerTime, order.OrderVolume ?? 0);
    }

    // Track order execution
    if (order.HasTradeInfo && orderLifetime.TryGetValue(orderId, out var info))
    {
        var lifetime = (order.ServerTime - info.placed).TotalMilliseconds;
        var fillPercent = ((order.TradeVolume ?? 0) / info.volume) * 100;

        Console.WriteLine($"Order {orderId} EXECUTED:");
        Console.WriteLine($"  Lifetime: {lifetime:F0}ms");
        Console.WriteLine($"  Fill: {fillPercent:F2}%");
        Console.WriteLine($"  Price: {order.TradePrice:F2}");

        // Fast execution might indicate aggressive order or high liquidity
        if (lifetime < 100)
            Console.WriteLine("  => FAST EXECUTION");
    }

    // Clean up completed orders
    if (order.OrderState == OrderStates.Done)
        orderLifetime.Remove(orderId);
};
```

### Example 4: Market Making Detection

```csharp
var orderPlacements = new Dictionary<decimal, (int buys, int sells)>();

connector.OrderLogReceived += (subscription, order) =>
{
    if (order.HasOrderInfo && !order.HasTradeInfo &&
        order.OrderState == OrderStates.Active)
    {
        var price = order.OrderPrice;

        if (!orderPlacements.ContainsKey(price))
            orderPlacements[price] = (0, 0);

        var (buys, sells) = orderPlacements[price];

        if (order.Side == Sides.Buy)
            orderPlacements[price] = (buys + 1, sells);
        else
            orderPlacements[price] = (buys, sells + 1);

        // Detect two-sided quoting (market making)
        if (buys > 0 && sells > 0)
        {
            Console.WriteLine($"MARKET MAKING detected at {price:F2}");
            Console.WriteLine($"  Buy orders: {buys}");
            Console.WriteLine($"  Sell orders: {sells}");
        }
    }
};
```

### Example 5: Spoofing Detection (High-Frequency Order Cancellation)

```csharp
var recentOrders = new Queue<(long orderId, DateTimeOffset time)>();
var orderCancellations = new Dictionary<long, DateTimeOffset>();

connector.OrderLogReceived += (subscription, order) =>
{
    if (!order.OrderId.HasValue)
        return;

    var orderId = order.OrderId.Value;

    // Track new orders
    if (order.OrderState == OrderStates.Active && !order.HasTradeInfo)
    {
        recentOrders.Enqueue((orderId, order.ServerTime));

        // Keep only last 100 orders
        while (recentOrders.Count > 100)
            recentOrders.Dequeue();
    }

    // Track cancellations
    if (order.OrderState == OrderStates.Done && !order.HasTradeInfo)
    {
        orderCancellations[orderId] = order.ServerTime;

        // Find when this order was placed
        var placement = recentOrders.FirstOrDefault(o => o.orderId == orderId);
        if (placement != default)
        {
            var lifetime = (order.ServerTime - placement.time).TotalMilliseconds;

            // Very short-lived orders might indicate spoofing
            if (lifetime < 500) // Less than 500ms
            {
                Console.WriteLine($"FAST CANCELLATION detected!");
                Console.WriteLine($"  OrderId: {orderId}");
                Console.WriteLine($"  Lifetime: {lifetime:F0}ms");
                Console.WriteLine($"  Price: {order.OrderPrice:F2}");
                Console.WriteLine($"  => Potential spoofing/layering");
            }
        }
    }
};
```

## Low Latency Features

Order log is the lowest latency market data available. Key optimizations:

### 1. Direct Message Processing

```csharp
// Process order log with minimal overhead
connector.OrderLogReceived += (subscription, order) =>
{
    // Direct access to message fields
    var price = order.OrderPrice;
    var volume = order.OrderVolume ?? 0;
    var side = order.Side;

    // Fast decision making
    if (side == Sides.Buy && volume > 1000)
    {
        // React immediately to large buy orders
        ProcessLargeOrder(price, volume);
    }
};
```

### 2. Sequence Number Tracking

```csharp
var expectedSeqNum = 0L;

connector.OrderLogReceived += (subscription, order) =>
{
    // Detect missed messages
    if (order.SeqNum != 0 && expectedSeqNum != 0)
    {
        if (order.SeqNum != expectedSeqNum)
        {
            Console.WriteLine($"GAP DETECTED: Expected {expectedSeqNum}, got {order.SeqNum}");
            // Request retransmission or handle gap
        }
    }

    expectedSeqNum = order.SeqNum + 1;
};
```

### 3. Pre-Trade Analysis

```csharp
// Use order log for pre-trade impact analysis
connector.OrderLogReceived += (subscription, order) =>
{
    if (order.HasOrderInfo && !order.HasTradeInfo)
    {
        // Before placing your order, analyze current order flow
        AnalyzeMarketDepth(order);
    }
};

void AnalyzeMarketDepth(ExecutionMessage order)
{
    // Calculate potential market impact before trading
    // Based on incoming orders and cancellations
}
```

## Building Order Book from Order Log

You can reconstruct the order book from order log data:

```csharp
var orderBook = new Dictionary<long, (decimal price, decimal volume, Sides side)>();

connector.OrderLogReceived += (subscription, order) =>
{
    if (!order.OrderId.HasValue)
        return;

    var orderId = order.OrderId.Value;

    // Add order to book
    if (order.OrderState == OrderStates.Active && !order.HasTradeInfo)
    {
        orderBook[orderId] = (order.OrderPrice, order.OrderVolume ?? 0, order.Side);
    }

    // Update order (partial fill)
    if (order.HasTradeInfo && orderBook.ContainsKey(orderId))
    {
        var (price, volume, side) = orderBook[orderId];
        var remainingVolume = volume - (order.TradeVolume ?? 0);

        if (remainingVolume > 0)
            orderBook[orderId] = (price, remainingVolume, side);
        else
            orderBook.Remove(orderId);
    }

    // Remove order
    if (order.OrderState == OrderStates.Done)
    {
        orderBook.Remove(orderId);
    }

    // Calculate order book statistics
    var bids = orderBook.Values.Where(o => o.side == Sides.Buy);
    var asks = orderBook.Values.Where(o => o.side == Sides.Sell);

    Console.WriteLine($"Order Book: {bids.Count()} bids, {asks.Count()} asks");
};
```

## Strategy with Order Log

```csharp
public class OrderLogStrategy : Strategy
{
    private decimal _buyPressure;
    private decimal _sellPressure;
    private const decimal PressureThreshold = 1000m;

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to order log
        var subscription = Connector.Subscribe(new Subscription(DataType.OrderLog, Security));

        Connector.OrderLogReceived += OnOrderLogReceived;
    }

    private void OnOrderLogReceived(Subscription subscription, IOrderLogMessage order)
    {
        if (order.SecurityId != Security.ToSecurityId())
            return;

        // Track order pressure (orders added to book)
        if (!order.HasTradeInfo && order.OrderState == OrderStates.Active)
        {
            var volume = order.OrderVolume ?? 0;

            if (order.Side == Sides.Buy)
                _buyPressure += volume;
            else
                _sellPressure += volume;
        }

        // Decay pressure over time
        _buyPressure *= 0.999m;
        _sellPressure *= 0.999m;

        // Trading logic based on order flow
        if (Position == 0)
        {
            if (_buyPressure > _sellPressure + PressureThreshold)
            {
                this.AddInfoLog($"Buy pressure: {_buyPressure:F0} vs {_sellPressure:F0}");
                BuyMarket(Volume);
            }
            else if (_sellPressure > _buyPressure + PressureThreshold)
            {
                this.AddInfoLog($"Sell pressure: {_sellPressure:F0} vs {_buyPressure:F0}");
                SellMarket(Volume);
            }
        }
    }

    protected override void OnStopped(DateTimeOffset time)
    {
        Connector.OrderLogReceived -= OnOrderLogReceived;
        base.OnStopped(time);
    }
}
```

## Connector Support

Not all connectors support order log data. Check adapter capabilities:

```csharp
// Check if adapter supports order log
var adapter = connector.Adapter;
if (adapter.IsMarketDataTypeSupported(DataType.OrderLog))
{
    Console.WriteLine("Order log is supported");
}
else
{
    Console.WriteLine("Order log is NOT supported");
}
```

Common exchanges supporting order log:
- LSE (London Stock Exchange)
- MOEX (Moscow Exchange)
- Plaza II connectors
- Some futures exchanges (CME, Eurex with specific data feeds)

## Best Practices

1. **Check Connector Support**: Verify order log is available for your exchange
2. **Handle High Frequency**: Order log updates are extremely frequent
3. **Use Sequence Numbers**: Track gaps in data stream
4. **Memory Management**: Don't store unlimited orders; use rolling windows
5. **Performance**: Optimize handlers for low latency

```csharp
// Good: Efficient order log processing
connector.OrderLogReceived += (subscription, order) =>
{
    // Quick checks first
    if (!order.OrderId.HasValue)
        return;

    // Process only relevant orders
    if (order.OrderPrice < minPrice || order.OrderPrice > maxPrice)
        return;

    // Efficient logic
    ProcessOrder(order);
};
```

## Unsubscribing from Order Log

```csharp
// Unsubscribe using subscription object
connector.UnSubscribe(subscription);

// Or find and unsubscribe
var subscriptions = connector.FindSubscriptions(security, DataType.OrderLog);
foreach (var sub in subscriptions)
{
    connector.UnSubscribe(sub);
}
```

## Related Topics

- [Order Book](order-book.md) - Aggregated order book view
- [Trades](trades.md) - Executed trades
- [Level1 Data](level1-data.md) - Top of book data
- [High-Frequency Trading](../08-Advanced/high-frequency.md) - HFT strategies with order log
