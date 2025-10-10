# Level1 Market Data

Level1 market data provides real-time market information for securities including prices, volumes, statistics, and various market indicators. This is the most common type of market data subscription in trading applications.

## Overview

Level1 data is represented by the `Level1ChangeMessage` class and contains over 85 different fields covering:
- Price data (last trade, best bid/ask, OHLC)
- Volume and liquidity metrics
- Options Greeks (Delta, Gamma, Vega, Theta, Rho)
- Financial ratios (P/E, P/B, ROE, ROA)
- Market statistics (volatility, ATR, beta)
- Trading parameters (margin, commissions, limits)

## Level1Fields Enum

The `Level1Fields` enum defines all available Level1 fields. Key field categories:

### Price Fields
```csharp
OpenPrice           // Opening price
HighPrice           // Highest price
LowPrice            // Lowest price
ClosePrice          // Closing price
LastTradePrice      // Last trade price
BestBidPrice        // Best bid price
BestAskPrice        // Best ask price
SettlementPrice     // Settlement price
TheorPrice          // Theoretical price
SpreadMiddle        // Middle of spread
```

### Volume Fields
```csharp
Volume              // Volume per session
LastTradeVolume     // Last trade volume
BestBidVolume       // Best bid volume
BestAskVolume       // Best ask volume
BidsVolume          // Total bids volume
AsksVolume          // Total asks volume
MinVolume           // Minimum volume allowed
MaxVolume           // Maximum volume allowed
```

### Trading Information
```csharp
LastTradeTime       // Time of last trade
LastTradeId         // Last trade ID
LastTradeOrigin     // Initiator (buyer/seller)
LastTradeUpDown     // Tick direction
TradesCount         // Number of trades
State               // Security state
```

### Options Greeks
```csharp
ImpliedVolatility   // Implied volatility
Delta               // Option delta
Gamma               // Option gamma
Vega                // Option vega
Theta               // Option theta
Rho                 // Option rho
```

### Financial Metrics
```csharp
PriceEarnings       // P/E ratio
PriceBook           // P/B ratio
PriceSales          // P/S ratio
ReturnOnEquity      // ROE
ReturnOnAssets      // ROA
Beta                // Beta coefficient
AverageTrueRange    // ATR
```

### Market Parameters
```csharp
PriceStep           // Minimum price step
VolumeStep          // Minimum volume step
MinPrice            // Minimum price
MaxPrice            // Maximum price
MarginBuy           // Initial margin to buy
MarginSell          // Initial margin to sell
Multiplier          // Lot multiplier
CommissionTaker     // Taker commission
CommissionMaker     // Maker commission
```

## Level1ChangeMessage

The `Level1ChangeMessage` class represents Level1 market data updates.

### Properties

```csharp
public class Level1ChangeMessage : BaseChangeMessage<Level1ChangeMessage, Level1Fields>
{
    // Security identifier
    public SecurityId SecurityId { get; set; }

    // Server timestamp
    public DateTimeOffset ServerTime { get; set; }

    // Sequence number for ordering
    public long SeqNum { get; set; }

    // Dictionary of field changes
    public IDictionary<Level1Fields, object> Changes { get; }

    // Data type
    public override DataType DataType => DataType.Level1;
}
```

### Accessing Field Values

```csharp
// Get specific field value
var lastPrice = (decimal)message.Changes[Level1Fields.LastTradePrice];
var volume = (decimal)message.Changes[Level1Fields.Volume];

// Try get field value safely
if (message.Changes.TryGetValue(Level1Fields.BestBidPrice, out var bidPrice))
{
    Console.WriteLine($"Best Bid: {bidPrice}");
}

// Check if field exists
if (message.Changes.ContainsKey(Level1Fields.OpenInterest))
{
    var oi = message.Changes[Level1Fields.OpenInterest];
}
```

## Subscribing to Level1 Data

### Basic Subscription

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

// Subscribe to Level1 data for a security
var security = connector.GetSecurity("AAPL@NASDAQ");
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));
```

### Handling Level1 Updates

```csharp
// Event-based approach
connector.ValuesChanged += (security, changes, serverTime, localTime) =>
{
    Console.WriteLine($"{security.Id} - {serverTime}");

    foreach (var change in changes)
    {
        Console.WriteLine($"  {change.Key}: {change.Value}");
    }
};

// Or use the subscription directly
connector.SubscriptionReceived += (subscription, message) =>
{
    if (message is Level1ChangeMessage level1)
    {
        Console.WriteLine($"Level1: {level1.SecurityId}");

        foreach (var change in level1.Changes)
        {
            Console.WriteLine($"  {change.Key}: {change.Value}");
        }
    }
};
```

### Strategy Subscription

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to Level1 for strategy security
        var subscription = SubscribeLevel1();

        // Handle Level1 updates
        subscription
            .Bind(ProcessLevel1)
            .Start();
    }

    private void ProcessLevel1(Level1ChangeMessage message)
    {
        if (message.Changes.TryGetValue(Level1Fields.LastTradePrice, out var price))
        {
            this.AddInfoLog($"Last price: {price}");
        }
    }
}
```

## GetSecurityValue Method

The `GetSecurityValue` extension method provides convenient access to Level1 field values from the Security object:

```csharp
using StockSharp.Algo;

var security = connector.GetSecurity("AAPL@NASDAQ");

// Get last trade price
var lastPrice = security.GetSecurityValue<decimal>(Level1Fields.LastTradePrice);

// Get best bid/ask
var bestBid = security.GetSecurityValue<decimal>(Level1Fields.BestBidPrice);
var bestAsk = security.GetSecurityValue<decimal>(Level1Fields.BestAskPrice);

// Get volume
var volume = security.GetSecurityValue<decimal>(Level1Fields.Volume);

// Get with default value if not available
var openInterest = security.GetSecurityValue<decimal?>(Level1Fields.OpenInterest) ?? 0;
```

## UpdateSecurityLastQuotes Feature

The `UpdateSecurityLastQuotes` property in Connector controls whether Security objects are automatically updated with the latest quotes from Level1 data:

```csharp
// Enable automatic quote updates (default: true)
connector.UpdateSecurityLastQuotes = true;

// Access quotes directly from Security object
var security = connector.GetSecurity("AAPL@NASDAQ");
Console.WriteLine($"Best Bid: {security.BestBid?.Price}");
Console.WriteLine($"Best Ask: {security.BestAsk?.Price}");
Console.WriteLine($"Last Trade: {security.LastTick?.TradePrice}");

// Disable automatic updates for performance
connector.UpdateSecurityLastQuotes = false;
```

### How It Works

When `UpdateSecurityLastQuotes = true`:
1. Level1 changes are processed by the connector
2. Best bid/ask prices are extracted from Level1 fields
3. Security.BestBid and Security.BestAsk are automatically updated
4. Last trade information updates Security.LastTick

This provides convenient access to current market data without manual subscription handling.

## Practical Examples

### Example 1: Monitor Price Changes

```csharp
var security = connector.GetSecurity("MSFT@NASDAQ");
connector.Subscribe(new Subscription(DataType.Level1, security));

connector.ValuesChanged += (sec, changes, serverTime, localTime) =>
{
    if (sec != security)
        return;

    foreach (var change in changes)
    {
        if (change.Key == Level1Fields.LastTradePrice)
        {
            var price = (decimal)change.Value;
            Console.WriteLine($"New price: {price} at {serverTime}");
        }
    }
};
```

### Example 2: Track Bid-Ask Spread

```csharp
var security = connector.GetSecurity("TSLA@NASDAQ");
connector.Subscribe(new Subscription(DataType.Level1, security));

connector.ValuesChanged += (sec, changes, serverTime, localTime) =>
{
    if (sec != security)
        return;

    var hasBid = changes.Any(c => c.Key == Level1Fields.BestBidPrice);
    var hasAsk = changes.Any(c => c.Key == Level1Fields.BestAskPrice);

    if (hasBid || hasAsk)
    {
        var bid = sec.GetSecurityValue<decimal?>(Level1Fields.BestBidPrice);
        var ask = sec.GetSecurityValue<decimal?>(Level1Fields.BestAskPrice);

        if (bid.HasValue && ask.HasValue)
        {
            var spread = ask.Value - bid.Value;
            var spreadPercent = (spread / bid.Value) * 100;
            Console.WriteLine($"Spread: {spread:F2} ({spreadPercent:F4}%)");
        }
    }
};
```

### Example 3: Options Greeks Monitoring

```csharp
var option = connector.GetSecurity("AAPL240119C00150000@OPRA");
connector.Subscribe(new Subscription(DataType.Level1, option));

connector.ValuesChanged += (sec, changes, serverTime, localTime) =>
{
    if (sec != option)
        return;

    var greekFields = new[]
    {
        Level1Fields.Delta,
        Level1Fields.Gamma,
        Level1Fields.Vega,
        Level1Fields.Theta,
        Level1Fields.ImpliedVolatility
    };

    if (changes.Any(c => greekFields.Contains(c.Key)))
    {
        Console.WriteLine($"Greeks Update at {serverTime}:");

        foreach (var field in greekFields)
        {
            var value = sec.GetSecurityValue<decimal?>(field);
            if (value.HasValue)
                Console.WriteLine($"  {field}: {value.Value:F4}");
        }
    }
};
```

### Example 4: Volume Profile Tracking

```csharp
var security = connector.GetSecurity("SPY@ARCA");
connector.Subscribe(new Subscription(DataType.Level1, security));

var sessionVolume = 0m;
var tradeCount = 0;

connector.ValuesChanged += (sec, changes, serverTime, localTime) =>
{
    if (sec != security)
        return;

    foreach (var change in changes)
    {
        switch (change.Key)
        {
            case Level1Fields.Volume:
                sessionVolume = (decimal)change.Value;
                break;

            case Level1Fields.TradesCount:
                tradeCount = (int)change.Value;
                break;

            case Level1Fields.VWAP:
                var vwap = (decimal)change.Value;
                Console.WriteLine($"VWAP: {vwap:F2}, Volume: {sessionVolume}, Trades: {tradeCount}");
                break;
        }
    }
};
```

## Best Practices

1. **Selective Field Monitoring**: Only process fields you need to reduce CPU usage
2. **Use GetSecurityValue**: Safer than direct dictionary access with null checking
3. **Check Field Availability**: Not all connectors provide all fields
4. **Performance Considerations**: Level1 updates can be frequent; optimize your handlers
5. **Unsubscribe When Done**: Clean up subscriptions to free resources

```csharp
// Good: Check field before processing
if (message.Changes.ContainsKey(Level1Fields.LastTradePrice))
{
    var price = (decimal)message.Changes[Level1Fields.LastTradePrice];
    // Process price
}

// Better: Use TryGetValue
if (message.Changes.TryGetValue(Level1Fields.LastTradePrice, out var price))
{
    // Process price
}

// Unsubscribe when done
connector.UnSubscribe(subscription);
```

## Related Topics

- [Order Book (Market Depth)](order-book.md) - Detailed bid/ask levels
- [Trades (Time & Sales)](trades.md) - Individual trade data
- [Candles](candles.md) - OHLCV candlestick data
- [Security Definition](../02-Core-Concepts/securities.md) - Security entity details
