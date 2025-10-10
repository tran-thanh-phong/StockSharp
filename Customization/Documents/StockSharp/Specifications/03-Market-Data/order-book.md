# Order Book (Market Depth)

Order book data, also known as market depth, provides detailed information about pending buy and sell orders at different price levels. This is essential for understanding market liquidity and order flow.

## Overview

Order book data in StockSharp is represented by the `QuoteChangeMessage` class, which contains:
- Arrays of bid quotes (buy orders)
- Arrays of ask quotes (sell orders)
- Order book state (snapshot vs incremental)
- Timestamp and security information

## QuoteChangeMessage

The main class for order book data.

### Properties

```csharp
public class QuoteChangeMessage : BaseSubscriptionIdMessage<QuoteChangeMessage>
{
    // Security identifier
    public SecurityId SecurityId { get; set; }

    // Bid quotes (buy orders), sorted by price descending
    public QuoteChange[] Bids { get; set; }

    // Ask quotes (sell orders), sorted by price ascending
    public QuoteChange[] Asks { get; set; }

    // Server timestamp
    public DateTimeOffset ServerTime { get; set; }

    // Order book state (snapshot/incremental)
    public QuoteChangeStates? State { get; set; }

    // Currency
    public CurrencyTypes? Currency { get; set; }

    // Sequence number
    public long SeqNum { get; set; }

    // Whether positions are initialized
    public bool HasPositions { get; set; }

    // Data type this was built from (e.g., Level1)
    public DataType BuildFrom { get; set; }

    // Whether quotes are filtered
    public bool IsFiltered { get; set; }

    // Data type
    public override DataType DataType => DataType.MarketDepth;
}
```

## QuoteChange Structure

Each quote (bid or ask) is represented by the `QuoteChange` structure:

```csharp
public struct QuoteChange
{
    // Price level
    public decimal Price { get; set; }

    // Volume at this price level
    public decimal Volume { get; set; }

    // Board code (exchange/board identifier)
    public string BoardCode { get; set; }

    // Position in the order book (optional)
    public int? StartPosition { get; set; }

    // End position (optional)
    public int? EndPosition { get; set; }

    // Action (add, update, delete) for incremental updates
    public QuoteChangeActions? Action { get; set; }

    // Condition (optional)
    public QuoteConditions? Condition { get; set; }
}
```

## Order Book States

The `QuoteChangeStates` enum indicates the type of update:

```csharp
public enum QuoteChangeStates
{
    SnapshotStarted,     // Snapshot transmission started
    SnapshotBuilding,    // Snapshot being built
    SnapshotComplete,    // Complete snapshot
    Increment            // Incremental update
}
```

### Snapshot vs Incremental Updates

**Snapshot**: Complete order book state
- Contains all price levels with their volumes
- Replaces previous order book completely
- Typically sent on initial subscription or periodically

**Incremental**: Only changes since last update
- Contains only modified, added, or deleted levels
- More efficient for high-frequency updates
- Requires maintaining local order book state

## Subscribing to Order Book

### Basic Subscription

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

var connector = new Connector();

// Subscribe to market depth for a security
var security = connector.GetSecurity("AAPL@NASDAQ");
var subscription = connector.Subscribe(new Subscription(DataType.MarketDepth, security));
```

### Advanced Subscription Options

```csharp
// Subscribe with maximum depth
var subscription = connector.Subscribe(new Subscription(DataType.MarketDepth, security)
{
    SubscriptionMessage = new MarketDataMessage
    {
        MaxDepth = 20  // Request up to 20 levels
    }
});

// Subscribe with historical data range
var subscription = connector.Subscribe(new Subscription(DataType.MarketDepth, security)
{
    From = DateTimeOffset.Now.AddDays(-1),
    To = DateTimeOffset.Now
});
```

## Handling Order Book Updates

### OrderBookReceived Event

The primary event for receiving order book updates:

```csharp
connector.OrderBookReceived += (subscription, depth) =>
{
    Console.WriteLine($"Order Book: {depth.SecurityId} at {depth.ServerTime}");
    Console.WriteLine($"State: {depth.State}, Bids: {depth.Bids.Length}, Asks: {depth.Asks.Length}");

    // Process bids
    Console.WriteLine("Bids:");
    foreach (var bid in depth.Bids.Take(5))
    {
        Console.WriteLine($"  {bid.Price:F2} x {bid.Volume}");
    }

    // Process asks
    Console.WriteLine("Asks:");
    foreach (var ask in depth.Asks.Take(5))
    {
        Console.WriteLine($"  {ask.Price:F2} x {ask.Volume}");
    }
};
```

### Complete Example from SecuritiesWindow.xaml.cs

This example shows a complete implementation from the LiveTerminal sample:

```csharp
private void DepthClick(object sender, RoutedEventArgs e)
{
    var connector = Connector;

    foreach (var security in SecurityPicker.SelectedSecurities)
    {
        var window = _quotesWindows.SafeAdd(security.ToSecurityId(), s =>
        {
            // Subscribe to order book flow
            connector.Subscribe(new(DataType.MarketDepth, security));

            // Create order book window
            var wnd = new QuotesWindow
            {
                Title = security.Id + " " + LocalizedStrings.MarketDepth
            };
            wnd.MakeHideable();
            return wnd;
        });

        if (window.Visibility == Visibility.Visible)
            window.Hide();
        else
            window.Show();

        if (!_initialized)
        {
            connector.OrderBookReceived += TraderOnMarketDepthReceived;
            _initialized = true;
        }
    }
}

private void TraderOnMarketDepthReceived(Subscription subscription, IOrderBookMessage depth)
{
    var wnd = _quotesWindows.TryGetValue(depth.SecurityId);

    if (wnd != null)
        wnd.DepthCtrl.UpdateDepth(depth);
}
```

## Working with Bids and Asks Arrays

### Accessing Best Bid/Ask

```csharp
// Get best bid (highest buy price)
var bestBid = depth.Bids.FirstOrDefault();
if (bestBid != null)
{
    Console.WriteLine($"Best Bid: {bestBid.Price} x {bestBid.Volume}");
}

// Get best ask (lowest sell price)
var bestAsk = depth.Asks.FirstOrDefault();
if (bestAsk != null)
{
    Console.WriteLine($"Best Ask: {bestAsk.Price} x {bestAsk.Volume}");
}

// Helper methods
var bestBidQuote = depth.GetBestBid();
var bestAskQuote = depth.GetBestAsk();
```

### Calculating Spread

```csharp
var bestBid = depth.GetBestBid();
var bestAsk = depth.GetBestAsk();

if (bestBid != null && bestAsk != null)
{
    var spread = bestAsk.Value.Price - bestBid.Value.Price;
    var spreadPercent = (spread / bestBid.Value.Price) * 100;

    Console.WriteLine($"Spread: {spread:F2} ({spreadPercent:F4}%)");
}
```

### Iterating Through Depth Levels

```csharp
// Show top 10 bid levels
Console.WriteLine("Top 10 Bids:");
foreach (var bid in depth.Bids.Take(10))
{
    Console.WriteLine($"  Price: {bid.Price:F2}, Volume: {bid.Volume}, Board: {bid.BoardCode}");
}

// Show top 10 ask levels
Console.WriteLine("Top 10 Asks:");
foreach (var ask in depth.Asks.Take(10))
{
    Console.WriteLine($"  Price: {ask.Price:F2}, Volume: {ask.Volume}, Board: {ask.BoardCode}");
}
```

## Practical Examples

### Example 1: Liquidity Analysis

```csharp
connector.OrderBookReceived += (subscription, depth) =>
{
    // Calculate total bid volume (buy pressure)
    var totalBidVolume = depth.Bids.Sum(b => b.Volume);

    // Calculate total ask volume (sell pressure)
    var totalAskVolume = depth.Asks.Sum(a => a.Volume);

    // Calculate imbalance
    var totalVolume = totalBidVolume + totalAskVolume;
    var bidRatio = totalVolume > 0 ? (totalBidVolume / totalVolume) * 100 : 0;

    Console.WriteLine($"{depth.SecurityId}:");
    Console.WriteLine($"  Bid Volume: {totalBidVolume}");
    Console.WriteLine($"  Ask Volume: {totalAskVolume}");
    Console.WriteLine($"  Bid Ratio: {bidRatio:F2}%");

    if (bidRatio > 60)
        Console.WriteLine("  => Strong buying pressure");
    else if (bidRatio < 40)
        Console.WriteLine("  => Strong selling pressure");
};
```

### Example 2: Depth Visualization

```csharp
void VisualizeDepth(QuoteChangeMessage depth, int levels = 5)
{
    var bestBid = depth.GetBestBid();
    var bestAsk = depth.GetBestAsk();

    if (bestBid == null || bestAsk == null)
        return;

    var midPrice = (bestBid.Value.Price + bestAsk.Value.Price) / 2;

    Console.WriteLine($"\n{depth.SecurityId} Depth at {depth.ServerTime:HH:mm:ss}");
    Console.WriteLine($"Mid Price: {midPrice:F2}\n");

    Console.WriteLine("ASKS (Sell Orders)");
    Console.WriteLine("Price      | Volume    | Distance");
    Console.WriteLine("-----------|-----------|----------");

    foreach (var ask in depth.Asks.Take(levels).Reverse())
    {
        var distance = ask.Price - midPrice;
        Console.WriteLine($"{ask.Price,10:F2} | {ask.Volume,9:F0} | +{distance:F2}");
    }

    Console.WriteLine("-----------|-----------|----------");
    Console.WriteLine($"{"MID",10} | {"",9} | {midPrice:F2}");
    Console.WriteLine("-----------|-----------|----------");

    foreach (var bid in depth.Bids.Take(levels))
    {
        var distance = midPrice - bid.Price;
        Console.WriteLine($"{bid.Price,10:F2} | {bid.Volume,9:F0} | -{distance:F2}");
    }

    Console.WriteLine("-----------|-----------|----------");
    Console.WriteLine("BIDS (Buy Orders)\n");
}

connector.OrderBookReceived += (subscription, depth) =>
{
    VisualizeDepth(depth, 5);
};
```

### Example 3: Volume-Weighted Average Price (VWAP) Calculation

```csharp
connector.OrderBookReceived += (subscription, depth) =>
{
    // Calculate bid-side VWAP for top 5 levels
    var topBids = depth.Bids.Take(5).ToArray();
    var bidVwap = topBids.Sum(b => b.Price * b.Volume) / topBids.Sum(b => b.Volume);

    // Calculate ask-side VWAP for top 5 levels
    var topAsks = depth.Asks.Take(5).ToArray();
    var askVwap = topAsks.Sum(a => a.Price * a.Volume) / topAsks.Sum(a => a.Volume);

    Console.WriteLine($"{depth.SecurityId}:");
    Console.WriteLine($"  Bid VWAP (5 levels): {bidVwap:F2}");
    Console.WriteLine($"  Ask VWAP (5 levels): {askVwap:F2}");
    Console.WriteLine($"  VWAP Spread: {(askVwap - bidVwap):F2}");
};
```

### Example 4: Order Book Snapshot Handler

```csharp
connector.OrderBookReceived += (subscription, depth) =>
{
    switch (depth.State)
    {
        case QuoteChangeStates.SnapshotStarted:
            Console.WriteLine($"Snapshot started for {depth.SecurityId}");
            break;

        case QuoteChangeStates.SnapshotBuilding:
            Console.WriteLine($"Building snapshot for {depth.SecurityId}...");
            break;

        case QuoteChangeStates.SnapshotComplete:
            Console.WriteLine($"Snapshot complete for {depth.SecurityId}");
            Console.WriteLine($"  Bid levels: {depth.Bids.Length}");
            Console.WriteLine($"  Ask levels: {depth.Asks.Length}");
            break;

        case QuoteChangeStates.Increment:
            // Process incremental update
            Console.WriteLine($"Incremental update for {depth.SecurityId}");
            break;

        default:
            // Regular update (no state specified)
            break;
    }
};
```

### Example 5: Trading Strategy with Order Book

```csharp
public class OrderBookStrategy : Strategy
{
    private const decimal SpreadThreshold = 0.05m; // 5%
    private const int MinDepthLevels = 5;

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to order book
        var subscription = Connector.Subscribe(new Subscription(DataType.MarketDepth, Security));

        Connector.OrderBookReceived += OnOrderBookReceived;
    }

    private void OnOrderBookReceived(Subscription subscription, IOrderBookMessage depth)
    {
        if (depth.SecurityId != Security.ToSecurityId())
            return;

        var bestBid = depth.GetBestBid();
        var bestAsk = depth.GetBestAsk();

        if (bestBid == null || bestAsk == null)
            return;

        // Check if we have sufficient depth
        if (depth.Bids.Length < MinDepthLevels || depth.Asks.Length < MinDepthLevels)
            return;

        // Calculate spread percentage
        var spread = bestAsk.Value.Price - bestBid.Value.Price;
        var spreadPercent = (spread / bestBid.Value.Price) * 100;

        // Check if spread is too wide
        if (spreadPercent > SpreadThreshold)
        {
            this.AddWarningLog($"Wide spread detected: {spreadPercent:F2}%");
            return;
        }

        // Calculate volume imbalance
        var bidVolume = depth.Bids.Take(MinDepthLevels).Sum(b => b.Volume);
        var askVolume = depth.Asks.Take(MinDepthLevels).Sum(a => a.Volume);
        var totalVolume = bidVolume + askVolume;

        if (totalVolume == 0)
            return;

        var bidRatio = (bidVolume / totalVolume) * 100;

        // Trading logic based on order book imbalance
        if (Position == 0)
        {
            if (bidRatio > 65) // Strong buying pressure
            {
                this.AddInfoLog($"Buy signal: Bid ratio = {bidRatio:F2}%");
                BuyMarket(Volume);
            }
            else if (bidRatio < 35) // Strong selling pressure
            {
                this.AddInfoLog($"Sell signal: Bid ratio = {bidRatio:F2}%");
                SellMarket(Volume);
            }
        }
    }

    protected override void OnStopped(DateTimeOffset time)
    {
        Connector.OrderBookReceived -= OnOrderBookReceived;
        base.OnStopped(time);
    }
}
```

## Unsubscribing from Order Book

```csharp
// Unsubscribe using subscription object
connector.UnSubscribe(subscription);

// Or find and unsubscribe
var subscriptions = connector.FindSubscriptions(security, DataType.MarketDepth);
foreach (var sub in subscriptions)
{
    connector.UnSubscribe(sub);
}
```

## Best Practices

1. **Check for Null**: Always verify bids/asks arrays are not empty before accessing
2. **Handle States**: Process snapshot and incremental updates appropriately
3. **Performance**: Order book updates can be very frequent; optimize your handlers
4. **Depth Levels**: Request only the depth levels you need (MaxDepth parameter)
5. **Memory Management**: Unsubscribe when no longer needed to free resources

```csharp
// Good: Safe access to best quotes
var bestBid = depth.Bids.FirstOrDefault();
var bestAsk = depth.Asks.FirstOrDefault();

if (bestBid != null && bestAsk != null)
{
    // Process quotes safely
}

// Better: Use helper methods
var bestBid = depth.GetBestBid();
var bestAsk = depth.GetBestAsk();

if (bestBid.HasValue && bestAsk.HasValue)
{
    var spread = bestAsk.Value.Price - bestBid.Value.Price;
}
```

## Related Topics

- [Level1 Data](level1-data.md) - Top of book quotes
- [Trades (Time & Sales)](trades.md) - Executed trades
- [Order Log](order-log.md) - Order-by-order data
- [Market Data Basics](../01-Getting-Started/core-concepts.md) - Overview of market data types
