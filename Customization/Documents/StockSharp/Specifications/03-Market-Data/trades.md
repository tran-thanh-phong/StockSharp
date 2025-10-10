# Trades (Time & Sales)

Market trades, also known as Time & Sales data, provide information about actual executed transactions. This includes the trade price, volume, timestamp, and additional metadata about each trade.

## Overview

Trade data in StockSharp is represented by `ExecutionMessage` with `DataType.Ticks`. It contains:
- Trade price and volume
- Trade timestamp
- Trade initiator (buyer or seller)
- Tick direction (up or down)
- Trade ID
- Additional trade properties

## Trade vs MyTrade

StockSharp distinguishes between two types of trades:

| **Market Trade** | **My Trade (Own Trade)** |
|-----------------|-------------------------|
| All executed trades on the exchange | Only trades resulting from your orders |
| `DataType.Ticks` | `DataType.Transactions` |
| Subscribe via `DataType.Ticks` | Automatic with order execution |
| `TickTradeReceived` event | `OwnTradeReceived` event |
| Public market data | Private trading data |

## ExecutionMessage for Ticks

When `DataType = DataType.Ticks`, the ExecutionMessage represents a market trade:

### Key Properties

```csharp
public class ExecutionMessage : BaseSubscriptionIdMessage<ExecutionMessage>
{
    // Data type indicator
    public DataType DataType { get; set; } // = DataType.Ticks

    // Security identifier
    public SecurityId SecurityId { get; set; }

    // Trade price
    public decimal? TradePrice { get; set; }

    // Trade volume
    public decimal? TradeVolume { get; set; }

    // Trade ID (numeric)
    public long? TradeId { get; set; }

    // Trade ID (string, for exchanges not using numeric IDs)
    public string TradeStringId { get; set; }

    // Server timestamp
    public DateTimeOffset ServerTime { get; set; }

    // Trade initiator (buyer or seller)
    public Sides? OriginSide { get; set; }

    // Tick direction (up or down)
    public bool? IsUpTick { get; set; }

    // Is system trade
    public bool? IsSystem { get; set; }

    // Open interest (for futures/options)
    public decimal? OpenInterest { get; set; }

    // Currency
    public CurrencyTypes? Currency { get; set; }

    // Sequence number
    public long SeqNum { get; set; }
}
```

### Trade Initiator (OriginSide)

The `OriginSide` property indicates who initiated the trade:
- `Sides.Buy`: Trade initiated by a buyer (aggressive buy, hitting ask)
- `Sides.Sell`: Trade initiated by a seller (aggressive sell, hitting bid)
- `null`: Unknown or not provided by exchange

### Tick Direction (IsUpTick)

The `IsUpTick` property indicates price movement:
- `true`: Price increased (uptick)
- `false`: Price decreased (downtick)
- `null`: Unchanged or unknown

## Subscribing to Trade Data

### Basic Subscription

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

var connector = new Connector();

// Subscribe to trades for a security
var security = connector.GetSecurity("AAPL@NASDAQ");
var subscription = connector.Subscribe(new Subscription(DataType.Ticks, security));
```

### Historical Trades

```csharp
// Subscribe to historical trades
var subscription = connector.Subscribe(new Subscription(DataType.Ticks, security)
{
    From = DateTimeOffset.Now.AddDays(-1),
    To = DateTimeOffset.Now
});
```

### Real-time Trades Only

```csharp
// Subscribe to real-time trades only (from now)
var subscription = connector.Subscribe(new Subscription(DataType.Ticks, security)
{
    From = DateTimeOffset.Now
});
```

## Handling Trade Data

### TickTradeReceived Event

The primary event for receiving trade data:

```csharp
connector.TickTradeReceived += (subscription, trade) =>
{
    Console.WriteLine($"Trade: {trade.SecurityId}");
    Console.WriteLine($"  Price: {trade.TradePrice}");
    Console.WriteLine($"  Volume: {trade.TradeVolume}");
    Console.WriteLine($"  Time: {trade.ServerTime:HH:mm:ss.fff}");
    Console.WriteLine($"  ID: {trade.TradeId}");
    Console.WriteLine($"  Side: {trade.OriginSide}");
};
```

### Subscription-Based Handler

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Ticks, security));

connector.TickTradeReceived += (sub, trade) =>
{
    // Filter by subscription
    if (sub != subscription)
        return;

    Console.WriteLine($"Trade: {trade.TradePrice} x {trade.TradeVolume}");
};
```

### Strategy Implementation

```csharp
public class TradeMonitorStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to trades
        var subscription = Connector.Subscribe(new Subscription(DataType.Ticks, Security));

        Connector.TickTradeReceived += OnTradeReceived;
    }

    private void OnTradeReceived(Subscription subscription, ITickTradeMessage trade)
    {
        if (trade.SecurityId != Security.ToSecurityId())
            return;

        this.AddInfoLog($"Trade: {trade.Price} x {trade.Volume} at {trade.ServerTime:HH:mm:ss}");

        // Strategy logic based on trades
        ProcessTrade(trade);
    }

    private void ProcessTrade(ITickTradeMessage trade)
    {
        // Your trading logic here
    }

    protected override void OnStopped(DateTimeOffset time)
    {
        Connector.TickTradeReceived -= OnTradeReceived;
        base.OnStopped(time);
    }
}
```

## Practical Examples

### Example 1: Trade Volume Profile

```csharp
var volumeProfile = new Dictionary<decimal, decimal>();
var priceStep = 0.01m; // Price bucket size

connector.TickTradeReceived += (subscription, trade) =>
{
    // Round price to nearest price step
    var priceLevel = Math.Round(trade.TradePrice.Value / priceStep) * priceStep;

    // Accumulate volume at each price level
    if (!volumeProfile.ContainsKey(priceLevel))
        volumeProfile[priceLevel] = 0;

    volumeProfile[priceLevel] += trade.TradeVolume.Value;
};

// Display volume profile
void DisplayVolumeProfile()
{
    Console.WriteLine("Volume Profile:");
    foreach (var level in volumeProfile.OrderByDescending(kv => kv.Key))
    {
        var bars = new string('█', (int)(level.Value / 100));
        Console.WriteLine($"{level.Key:F2}: {bars} ({level.Value:F0})");
    }
}
```

### Example 2: Tick Direction Analysis

```csharp
var upTicks = 0;
var downTicks = 0;
var neutralTicks = 0;

connector.TickTradeReceived += (subscription, trade) =>
{
    if (trade.IsUpTick == true)
        upTicks++;
    else if (trade.IsUpTick == false)
        downTicks++;
    else
        neutralTicks++;

    var total = upTicks + downTicks + neutralTicks;
    if (total > 0 && total % 100 == 0)
    {
        var upPercent = (upTicks * 100.0) / total;
        Console.WriteLine($"Tick Direction: Up={upPercent:F1}%, Down={100-upPercent-neutralTicks*100.0/total:F1}%");

        if (upPercent > 60)
            Console.WriteLine("  => Bullish momentum");
        else if (upPercent < 40)
            Console.WriteLine("  => Bearish momentum");
    }
};
```

### Example 3: Trade Aggressor Analysis

```csharp
var buyerInitiated = 0m;  // Aggressive buyers (hitting ask)
var sellerInitiated = 0m; // Aggressive sellers (hitting bid)

connector.TickTradeReceived += (subscription, trade) =>
{
    var volume = trade.TradeVolume.Value;

    if (trade.OriginSide == Sides.Buy)
        buyerInitiated += volume;
    else if (trade.OriginSide == Sides.Sell)
        sellerInitiated += volume;

    var total = buyerInitiated + sellerInitiated;
    if (total > 0)
    {
        var buyRatio = (buyerInitiated / total) * 100;
        Console.WriteLine($"{trade.SecurityId} Aggressor Ratio:");
        Console.WriteLine($"  Buyer Initiated: {buyRatio:F2}%");
        Console.WriteLine($"  Seller Initiated: {(100 - buyRatio):F2}%");

        if (buyRatio > 60)
            Console.WriteLine("  => Strong buying pressure");
        else if (buyRatio < 40)
            Console.WriteLine("  => Strong selling pressure");
    }
};
```

### Example 4: Large Trade Alert

```csharp
var averageVolume = 100m; // Average trade size
var largeTradeThreshold = 5; // 5x average is considered large

connector.TickTradeReceived += (subscription, trade) =>
{
    var volume = trade.TradeVolume.Value;

    // Update running average (simple moving average)
    averageVolume = (averageVolume * 0.99m) + (volume * 0.01m);

    // Check for large trades
    if (volume > averageVolume * largeTradeThreshold)
    {
        Console.WriteLine($"LARGE TRADE ALERT!");
        Console.WriteLine($"  Security: {trade.SecurityId}");
        Console.WriteLine($"  Price: {trade.TradePrice}");
        Console.WriteLine($"  Volume: {volume:F0} (avg: {averageVolume:F0})");
        Console.WriteLine($"  Size: {(volume / averageVolume):F1}x average");
        Console.WriteLine($"  Side: {trade.OriginSide}");
        Console.WriteLine($"  Time: {trade.ServerTime:HH:mm:ss}");
    }
};
```

### Example 5: Trade Price Levels

```csharp
var lastTrade = (price: 0m, time: DateTimeOffset.MinValue);

connector.TickTradeReceived += (subscription, trade) =>
{
    var price = trade.TradePrice.Value;
    var time = trade.ServerTime;

    if (lastTrade.time != DateTimeOffset.MinValue)
    {
        var priceChange = price - lastTrade.price;
        var percentChange = (priceChange / lastTrade.price) * 100;
        var timeDiff = (time - lastTrade.time).TotalSeconds;

        Console.WriteLine($"Trade at {time:HH:mm:ss.fff}");
        Console.WriteLine($"  Price: {price:F2} ({priceChange:+0.00;-0.00} / {percentChange:+0.00;-0.00}%)");
        Console.WriteLine($"  Volume: {trade.TradeVolume}");
        Console.WriteLine($"  Time since last: {timeDiff:F2}s");

        // Alert on significant price moves
        if (Math.Abs(percentChange) > 0.5m) // 0.5% move
        {
            Console.WriteLine($"  *** SIGNIFICANT MOVE: {percentChange:F2}% ***");
        }
    }

    lastTrade = (price, time);
};
```

### Example 6: VWAP (Volume-Weighted Average Price)

```csharp
var totalValue = 0m;
var totalVolume = 0m;
var sessionStart = DateTimeOffset.Now.Date;

connector.TickTradeReceived += (subscription, trade) =>
{
    // Reset at start of new session
    if (trade.ServerTime.Date > sessionStart)
    {
        totalValue = 0;
        totalVolume = 0;
        sessionStart = trade.ServerTime.Date;
    }

    // Update VWAP calculation
    var value = trade.TradePrice.Value * trade.TradeVolume.Value;
    totalValue += value;
    totalVolume += trade.TradeVolume.Value;

    var vwap = totalVolume > 0 ? totalValue / totalVolume : 0;
    var currentPrice = trade.TradePrice.Value;
    var deviation = ((currentPrice - vwap) / vwap) * 100;

    Console.WriteLine($"{trade.ServerTime:HH:mm:ss}");
    Console.WriteLine($"  Price: {currentPrice:F2}");
    Console.WriteLine($"  VWAP: {vwap:F2}");
    Console.WriteLine($"  Deviation: {deviation:+0.00;-0.00}%");

    // Trading signals based on VWAP
    if (deviation < -1.0m)
        Console.WriteLine("  => Price below VWAP (potential buy)");
    else if (deviation > 1.0m)
        Console.WriteLine("  => Price above VWAP (potential sell)");
};
```

### Example 7: Time & Sales Window

```csharp
var recentTrades = new Queue<ITickTradeMessage>();
var maxTrades = 20; // Keep last 20 trades

connector.TickTradeReceived += (subscription, trade) =>
{
    // Add to queue
    recentTrades.Enqueue(trade);

    // Maintain size limit
    while (recentTrades.Count > maxTrades)
        recentTrades.Dequeue();

    // Display recent trades
    Console.Clear();
    Console.WriteLine($"Time & Sales for {trade.SecurityId}");
    Console.WriteLine("Time       | Price    | Volume  | Side");
    Console.WriteLine("-----------|----------|---------|------");

    foreach (var t in recentTrades.Reverse())
    {
        var side = t.OriginSide == Sides.Buy ? "BUY" :
                   t.OriginSide == Sides.Sell ? "SELL" : "---";

        var arrow = t.IsUpTick == true ? "↑" :
                    t.IsUpTick == false ? "↓" : " ";

        Console.WriteLine($"{t.ServerTime:HH:mm:ss.fff} | {t.Price,8:F2}{arrow} | {t.Volume,7:F0} | {side}");
    }
};
```

## OwnTradeReceived Event

For your own trades (resulting from your orders), use the `OwnTradeReceived` event:

```csharp
connector.OwnTradeReceived += (subscription, trade) =>
{
    Console.WriteLine("My Trade Executed:");
    Console.WriteLine($"  Order ID: {trade.OrderId}");
    Console.WriteLine($"  Trade ID: {trade.TradeId}");
    Console.WriteLine($"  Price: {trade.TradePrice}");
    Console.WriteLine($"  Volume: {trade.TradeVolume}");
    Console.WriteLine($"  Portfolio: {trade.PortfolioName}");
    Console.WriteLine($"  Commission: {trade.Commission}");
    Console.WriteLine($"  Slippage: {trade.Slippage}");
};
```

### Own Trade in Strategy

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to own trades
        Connector.OwnTradeReceived += OnOwnTradeReceived;
    }

    private void OnOwnTradeReceived(Subscription subscription, ITickTradeMessage trade)
    {
        this.AddInfoLog($"Trade executed: {trade.Price} x {trade.Volume}");

        // Calculate realized P&L, update position, etc.
    }

    protected override void OnStopped(DateTimeOffset time)
    {
        Connector.OwnTradeReceived -= OnOwnTradeReceived;
        base.OnStopped(time);
    }
}
```

## Unsubscribing from Trades

```csharp
// Unsubscribe using subscription object
connector.UnSubscribe(subscription);

// Or find and unsubscribe
var subscriptions = connector.FindSubscriptions(security, DataType.Ticks);
foreach (var sub in subscriptions)
{
    connector.UnSubscribe(sub);
}
```

## Best Practices

1. **Filter by Security**: Always check SecurityId when handling multiple subscriptions
2. **Handle Nulls**: TradePrice, TradeVolume, etc. can be null in some cases
3. **Performance**: Trade data can be very high frequency; optimize your handlers
4. **Use Appropriate Storage**: For large volumes, consider storing trades in database
5. **Aggregation**: Aggregate trades into candles for analysis rather than processing each tick

```csharp
// Good: Safe null handling
connector.TickTradeReceived += (subscription, trade) =>
{
    if (trade.TradePrice.HasValue && trade.TradeVolume.HasValue)
    {
        var price = trade.TradePrice.Value;
        var volume = trade.TradeVolume.Value;
        // Process trade
    }
};

// Better: Use ITickTradeMessage properties
connector.TickTradeReceived += (subscription, trade) =>
{
    var price = trade.Price;   // Non-nullable
    var volume = trade.Volume; // Non-nullable
    // Process trade
};
```

## Market Data Impact

Trade data automatically updates Security properties when `UpdateSecurityLastQuotes = true`:

```csharp
connector.UpdateSecurityLastQuotes = true;

// After trades are received, security is automatically updated
var security = connector.GetSecurity("AAPL@NASDAQ");
Console.WriteLine($"Last Trade: {security.LastTick?.TradePrice}");
Console.WriteLine($"Last Trade Time: {security.LastTick?.ServerTime}");
```

## Related Topics

- [Level1 Data](level1-data.md) - Top of book quotes and statistics
- [Order Book](order-book.md) - Detailed market depth
- [Order Log](order-log.md) - Order-by-order data
- [Candles](candles.md) - Aggregated OHLCV data
- [Order Management](../04-Trading/orders.md) - Working with own trades
