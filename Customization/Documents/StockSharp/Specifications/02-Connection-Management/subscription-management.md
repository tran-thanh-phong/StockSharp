# Subscription Management Documentation

## Overview

StockSharp's subscription system provides a unified way to receive market data, portfolio updates, orders, and other real-time information from trading systems. The subscription model is message-based and supports complex scenarios like historical data, real-time updates, and combination of both.

Key concepts:
- **Subscription** - Represents a data subscription request
- **DataType** - Specifies what type of data to subscribe to
- **Subscription States** - Track subscription lifecycle
- **Subscribe/UnSubscribe** - Methods to manage subscriptions
- **Subscription Events** - Notifications about subscription state changes

**File Locations:**
- `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Subscription.cs`
- `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\SubscriptionStates.cs`
- `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector_SubscriptionManager.cs`

---

## Subscription Class

### Overview

The `Subscription` class represents a single data subscription.

```csharp
public class Subscription : SubscriptionBase<Subscription>
```

### Constructors

```csharp
// Subscribe to specific data type
public Subscription(DataType dataType);

// Subscribe to data type for specific security
public Subscription(DataType dataType, Security security);
public Subscription(DataType dataType, SecurityMessage security);

// Subscribe using subscription message
public Subscription(ISubscriptionMessage subscriptionMessage);
public Subscription(ISubscriptionMessage subscriptionMessage, Security security);
public Subscription(ISubscriptionMessage subscriptionMessage, SecurityMessage security);

// Obsolete: Candle series
[Obsolete("Use DataType overload.")]
public Subscription(CandleSeries candleSeries);
```

### Properties

```csharp
// The subscription message
public ISubscriptionMessage SubscriptionMessage { get; }

// Security for this subscription (can be null for lookups)
public Security Security { get; }

// Security ID
public SecurityId SecurityId { get; }

// Data type being subscribed to
public DataType DataType { get; }

// Current subscription state
public SubscriptionStates State { get; }

// Transaction ID
public long TransactionId { get; }

// Time range (for historical data)
public DateTimeOffset? From { get; }
public DateTimeOffset? To { get; }

// Is this a subscription (vs unsubscription)
public bool IsSubscribe { get; }
```

---

## Data Types

### Common Data Types

```csharp
// Level 1 (best bid/ask, last price, volume)
DataType.Level1

// Market depth (order book)
DataType.MarketDepth

// Tick trades
DataType.Ticks

// Order log
DataType.OrderLog

// News
DataType.News

// Board (exchange) info
DataType.Board

// Transactions (orders, trades)
DataType.Transactions

// Position changes
DataType.PositionChanges

// Time frames (candles)
DataType.TimeFrame(TimeSpan.FromMinutes(5))

// Other candle types
DataType.Ticks(1000)         // 1000-tick candles
DataType.Volume(100)         // 100-volume candles
DataType.Range(0.5m)         // Range candles
DataType.PnF(...)           // Point & Figure
DataType.Renko(...)         // Renko
DataType.HeikinAshi(...)    // Heikin Ashi
```

### Creating Subscriptions

```csharp
var security = connector.GetSecurity(new SecurityId { SecurityCode = "AAPL", BoardCode = "NASDAQ" });

// Level1 subscription
var level1Sub = new Subscription(DataType.Level1, security);

// Market depth subscription
var depthSub = new Subscription(DataType.MarketDepth, security);

// Tick trades subscription
var ticksSub = new Subscription(DataType.Ticks, security);

// 5-minute candles subscription
var candlesSub = new Subscription(DataType.TimeFrame(TimeSpan.FromMinutes(5)), security);

// Historical data with time range
var historicalSub = new Subscription(
    new MarketDataMessage
    {
        DataType2 = DataType.TimeFrame(TimeSpan.FromMinutes(1)),
        SecurityId = security.ToSecurityId(),
        From = DateTime.Today.AddDays(-7),
        To = DateTime.Today,
        IsSubscribe = true
    },
    security
);
```

---

## Subscription States

### SubscriptionStates Enum

```csharp
public enum SubscriptionStates
{
    Stopped,   // Not active, initial state
    Active,    // Active, receiving data
    Error,     // Error occurred
    Finished,  // Finished (historical data completed)
    Online     // Online (historical data completed, now receiving real-time)
}
```

**File**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\SubscriptionStates.cs`

### State Transitions

```
Stopped → Active → Online   (for historical + real-time subscriptions)
Stopped → Active            (for real-time only subscriptions)
Stopped → Error             (on subscription failure)
Active → Finished           (when historical data is complete, no real-time)
Active → Error              (on unexpected error)
```

### Checking Subscription State

```csharp
// Extension methods
subscription.State.IsActive()    // Active, Finished, or Online
subscription.State == SubscriptionStates.Online  // Receiving real-time data

// Wait for subscription to be online
while (subscription.State != SubscriptionStates.Online)
{
    await Task.Delay(100);
}
```

---

## Subscribe Method

### Basic Subscription

```csharp
public Subscription Subscribe(Subscription subscription)
```

Starts a new subscription and returns the subscription object.

**Example:**

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

// Check subscription state
Console.WriteLine($"Subscription state: {subscription.State}");
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:55`

```csharp
private void Connector_Connected()
{
    // Lookup all securities after connection
    _connector.Subscribe(new(StockSharp.Messages.Extensions.LookupAllCriteriaMessage));
}
```

### Subscribe to Multiple Securities

```csharp
var securities = connector.Securities.Take(10);

foreach (var security in securities)
{
    connector.Subscribe(new Subscription(DataType.Level1, security));
}
```

### Subscribe with Events

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

// Handle subscription events
connector.SubscriptionStarted += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine("Subscription started!");
    }
};

connector.SubscriptionOnline += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine("Subscription online - receiving real-time data");
    }
};

connector.SubscriptionStopped += (sub, error) =>
{
    if (sub == subscription)
    {
        if (error == null)
            Console.WriteLine("Subscription stopped normally");
        else
            Console.WriteLine($"Subscription stopped with error: {error.Message}");
    }
};
```

---

## UnSubscribe Method

### Basic Unsubscription

```csharp
public void UnSubscribe(Subscription subscription)
```

Stops an active subscription.

**Example:**

```csharp
// Subscribe
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

// Later, unsubscribe
connector.UnSubscribe(subscription);
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\10_LiveTerminal\SecuritiesWindow.xaml.cs:112-118`

```csharp
private void QuotesClick(object sender, RoutedEventArgs e)
{
    var connector = Connector;

    foreach (var security in SecurityPicker.SelectedSecurities)
    {
        var subscription = FindSubscription(security, DataType.Level1);

        if (subscription != null)
            connector.UnSubscribe(subscription);  // Unsubscribe
        else
            connector.Subscribe(new(DataType.Level1, security));  // Subscribe
    }
}

private static Subscription FindSubscription(Security security, DataType dataType)
{
    return Connector.FindSubscriptions(security, dataType)
        .Where(s => s.SubscriptionMessage.To == null && s.State.IsActive())
        .FirstOrDefault();
}
```

---

## Finding Subscriptions

### Find Active Subscriptions

```csharp
// Find subscriptions by security and data type
IEnumerable<Subscription> FindSubscriptions(Security security, DataType dataType);

// Get all subscriptions
IEnumerable<Subscription> Subscriptions { get; }
```

**Example:**

```csharp
// Find all Level1 subscriptions for a security
var level1Subs = connector.FindSubscriptions(security, DataType.Level1);

foreach (var sub in level1Subs)
{
    Console.WriteLine($"Subscription {sub.TransactionId}: State={sub.State}");
}

// Get all active subscriptions
var allSubs = connector.Subscriptions.Where(s => s.State.IsActive());
Console.WriteLine($"Active subscriptions: {allSubs.Count()}");
```

---

## Subscription Events

### Core Events

```csharp
// Subscription started (Active state)
event Action<Subscription> SubscriptionStarted;

// Subscription online (receiving real-time data)
event Action<Subscription> SubscriptionOnline;

// Subscription stopped (normal or with error)
event Action<Subscription, Exception> SubscriptionStopped;

// Subscription failed (error during subscription)
event Action<Subscription, Exception, bool> SubscriptionFailed;

// Data received through subscription
event Action<Subscription, object> SubscriptionReceived;
```

### Data-Specific Events

```csharp
// Level1 data received
event Action<Subscription, Level1ChangeMessage> Level1Received;

// Order book received
event Action<Subscription, IOrderBookMessage> OrderBookReceived;

// Tick trade received
event Action<Subscription, ITickTradeMessage> TickTradeReceived;

// Order log received
event Action<Subscription, IOrderLogMessage> OrderLogReceived;

// Candle received
event Action<Subscription, ICandleMessage> CandleReceived;

// Security received (from lookup)
event Action<Subscription, Security> SecurityReceived;

// Board received (from lookup)
event Action<Subscription, ExchangeBoard> BoardReceived;

// Portfolio received
event Action<Subscription, Portfolio> PortfolioReceived;

// Position received
event Action<Subscription, Position> PositionReceived;

// Order received
event Action<Subscription, Order> OrderReceived;

// Own trade received
event Action<Subscription, MyTrade> OwnTradeReceived;

// News received
event Action<Subscription, News> NewsReceived;

// Data type received (from lookup)
event Action<Subscription, DataType> DataTypeReceived;
```

### Event Usage Example

```csharp
// Subscribe to Level1 data
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

// Handle Level1 updates
connector.Level1Received += (sub, level1Msg) =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Level1 update for {security.Code}:");

        if (level1Msg.Changes.ContainsKey(Level1Fields.LastPrice))
        {
            var lastPrice = (decimal)level1Msg.Changes[Level1Fields.LastPrice];
            Console.WriteLine($"  Last Price: {lastPrice}");
        }

        if (level1Msg.Changes.ContainsKey(Level1Fields.BestBidPrice))
        {
            var bidPrice = (decimal)level1Msg.Changes[Level1Fields.BestBidPrice];
            Console.WriteLine($"  Best Bid: {bidPrice}");
        }
    }
};

// Handle subscription lifecycle
connector.SubscriptionOnline += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Level1 subscription online for {security.Code}");
    }
};

connector.SubscriptionStopped += (sub, error) =>
{
    if (sub == subscription)
    {
        if (error == null)
            Console.WriteLine($"Level1 subscription stopped for {security.Code}");
        else
            Console.WriteLine($"Level1 subscription error: {error.Message}");
    }
};
```

---

## SubscriptionsOnConnect

### Overview

`SubscriptionsOnConnect` is a collection of subscriptions that are automatically sent when the connector connects.

```csharp
// Collection of subscriptions to send on connect
ISet<Subscription> SubscriptionsOnConnect { get; }
```

**Default subscriptions:**
- `SecurityLookup` - Download all available securities
- `PortfolioLookup` - Download all portfolios
- `OrderLookup` - Download all active orders

### Customizing Subscriptions on Connect

```csharp
var connector = new Connector();

// Clear default subscriptions
connector.SubscriptionsOnConnect.Clear();

// Add custom subscriptions
connector.SubscriptionsOnConnect.Add(connector.SecurityLookup);
connector.SubscriptionsOnConnect.Add(connector.PortfolioLookup);

// Add specific security subscription
var btcSecurity = new Security { Id = "BTC@Binance" };
connector.SubscriptionsOnConnect.Add(
    new Subscription(DataType.Level1, btcSecurity)
);

// Connect - subscriptions will be sent automatically
connector.Connect();
```

---

## Lookup Patterns

### Security Lookup (LookupAll)

Download all available securities from the exchange.

**Example:**

```csharp
// Subscribe to all securities
connector.Subscribe(new Subscription(Extensions.LookupAllCriteriaMessage));

// Handle securities as they arrive
connector.SecurityReceived += (subscription, security) =>
{
    Console.WriteLine($"Received security: {security.Code}");
};

// Wait for completion
connector.SubscriptionStopped += (subscription, error) =>
{
    if (subscription.DataType == DataType.Securities)
    {
        Console.WriteLine($"Downloaded {connector.Securities.Count()} securities");
    }
};
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:54-56`

```csharp
private void Connector_Connected()
{
    // Try lookup all securities
    _connector.Subscribe(new(StockSharp.Messages.Extensions.LookupAllCriteriaMessage));
}
```

### Filtered Security Lookup

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\10_LiveTerminal\SecuritiesWindow.xaml.cs:129-146`

```csharp
private void FindClick(object sender, RoutedEventArgs e)
{
    var wnd = new SecurityLookupWindow
    {
        ShowAllOption = Connector.Adapter.IsSupportSecuritiesLookupAll(),
        CriteriaMessage = new SecurityLookupMessage
        {
            SecurityId = new SecurityId
            {
                BoardCode = "IS"  // Filter by board
            }
        }
    };

    if (!wnd.ShowModal(this))
        return;

    // Subscribe with filter criteria
    Connector.Subscribe(new Subscription(wnd.CriteriaMessage));
}
```

### Portfolio Lookup

```csharp
// Download all portfolios
connector.Subscribe(connector.PortfolioLookup);

// Handle portfolios
connector.PortfolioReceived += (subscription, portfolio) =>
{
    Console.WriteLine($"Portfolio: {portfolio.Name}");
    Console.WriteLine($"  Balance: {portfolio.CurrentValue}");
};
```

### Order Lookup

```csharp
// Download active orders
connector.Subscribe(connector.OrderLookup);

// Handle orders
connector.OrderReceived += (subscription, order) =>
{
    Console.WriteLine($"Order: {order.Id}");
    Console.WriteLine($"  Security: {order.Security.Code}");
    Console.WriteLine($"  State: {order.State}");
};
```

---

## Market Data Subscriptions

### Level1 (Quotes) Subscription

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

connector.Level1Received += (sub, level1) =>
{
    if (sub == subscription)
    {
        foreach (var change in level1.Changes)
        {
            Console.WriteLine($"{change.Key}: {change.Value}");
        }
    }
};
```

### Order Book (Market Depth) Subscription

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\10_LiveTerminal\SecuritiesWindow.xaml.cs:65-98`

```csharp
private void DepthClick(object sender, RoutedEventArgs e)
{
    var connector = Connector;

    foreach (var security in SecurityPicker.SelectedSecurities)
    {
        var window = _quotesWindows.SafeAdd(security.ToSecurityId(), s =>
        {
            // Subscribe to order book flow
            connector.Subscribe(new Subscription(DataType.MarketDepth, security));

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

### Tick Trades Subscription

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Ticks, security));

connector.TickTradeReceived += (sub, trade) =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Trade: {trade.Price} x {trade.Volume} @ {trade.ServerTime}");
    }
};
```

### Candle Subscription

```csharp
// 5-minute candles
var subscription = connector.Subscribe(
    new Subscription(DataType.TimeFrame(TimeSpan.FromMinutes(5)), security)
);

connector.CandleReceived += (sub, candle) =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Candle: O={candle.OpenPrice} H={candle.HighPrice} " +
                         $"L={candle.LowPrice} C={candle.ClosePrice} V={candle.TotalVolume}");
    }
};
```

---

## Historical Data Subscriptions

### Historical with Time Range

```csharp
var subscription = connector.Subscribe(new Subscription(
    new MarketDataMessage
    {
        DataType2 = DataType.TimeFrame(TimeSpan.FromMinutes(1)),
        SecurityId = security.ToSecurityId(),
        From = DateTime.Today.AddDays(-30),
        To = DateTime.Today,
        IsSubscribe = true
    },
    security
));

// Track progress
var candleCount = 0;
connector.CandleReceived += (sub, candle) =>
{
    if (sub == subscription)
    {
        candleCount++;
        Console.WriteLine($"Received {candleCount} candles");
    }
};

// Handle completion
connector.SubscriptionFinished += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Historical download complete: {candleCount} candles");
    }
};
```

### Historical + Real-time

```csharp
// Download historical and continue with real-time
var subscription = connector.Subscribe(new Subscription(
    new MarketDataMessage
    {
        DataType2 = DataType.TimeFrame(TimeSpan.FromMinutes(1)),
        SecurityId = security.ToSecurityId(),
        From = DateTime.Today.AddDays(-7),
        // No "To" - will continue with real-time after historical
        IsSubscribe = true
    },
    security
));

// State progression: Stopped → Active → Online
connector.SubscriptionOnline += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine("Historical data downloaded, now receiving real-time");
    }
};
```

---

## Subscription Best Practices

### 1. Check Subscription State

```csharp
// Always check state before assuming subscription is active
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

// Wait for subscription to be active
var timeout = TimeSpan.FromSeconds(10);
var start = DateTime.Now;

while (!subscription.State.IsActive() && DateTime.Now - start < timeout)
{
    await Task.Delay(100);
}

if (subscription.State.IsActive())
{
    Console.WriteLine("Subscription active!");
}
else
{
    Console.WriteLine($"Subscription failed: {subscription.State}");
}
```

### 2. Handle Subscription Failures

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

connector.SubscriptionFailed += (sub, error, isSubscribe) =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Subscription failed: {error.Message}");

        // Implement retry logic
        if (isSubscribe && retryCount < maxRetries)
        {
            retryCount++;
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                Console.WriteLine($"Retrying subscription (attempt {retryCount})...");
                connector.Subscribe(new Subscription(DataType.Level1, security));
            });
        }
    }
};
```

### 3. Clean Up Subscriptions

```csharp
// Keep track of subscriptions
private readonly List<Subscription> _activeSubscriptions = new();

private void SubscribeToSecurity(Security security)
{
    var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));
    _activeSubscriptions.Add(subscription);
}

private void Cleanup()
{
    // Unsubscribe all
    foreach (var subscription in _activeSubscriptions)
    {
        connector.UnSubscribe(subscription);
    }

    _activeSubscriptions.Clear();
}

// Call cleanup when closing application
protected override void OnClosed(EventArgs e)
{
    Cleanup();
    base.OnClosed(e);
}
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\10_LiveTerminal\SecuritiesWindow.xaml.cs:28-44`

```csharp
protected override void OnClosed(EventArgs e)
{
    _quotesWindows.SyncDo(d => d.Values.ForEach(w =>
    {
        w.DeleteHideable();
        w.Close();
    }));

    var connector = Connector;

    if (connector != null)
    {
        if (_initialized)
            connector.OrderBookReceived -= TraderOnMarketDepthReceived;
    }

    base.OnClosed(e);
}
```

### 4. Filter Subscription Events

```csharp
// Store subscription reference
private Subscription _currentSubscription;

private void SubscribeToSecurity(Security security)
{
    _currentSubscription = connector.Subscribe(new Subscription(DataType.Level1, security));
}

// Filter events by subscription
connector.Level1Received += (subscription, level1) =>
{
    // Only process events for current subscription
    if (subscription == _currentSubscription)
    {
        ProcessLevel1Update(level1);
    }
};
```

### 5. Batch Subscriptions

```csharp
// Subscribe to multiple securities efficiently
var securities = GetTopSecurities(10);

foreach (var security in securities)
{
    connector.Subscribe(new Subscription(DataType.Level1, security));
}

// Single handler for all subscriptions
connector.Level1Received += (subscription, level1) =>
{
    var security = subscription.Security;
    UpdateQuoteDisplay(security, level1);
};
```

---

## Common Subscription Scenarios

### Scenario 1: Real-time Quotes Display

```csharp
var securities = GetWatchList();

foreach (var security in securities)
{
    var subscription = connector.Subscribe(new Subscription(DataType.Level1, security));

    connector.Level1Received += (sub, level1) =>
    {
        if (sub.Security == security)
        {
            UpdateQuoteGrid(security, level1);
        }
    };
}
```

### Scenario 2: Order Book Monitoring

```csharp
var subscription = connector.Subscribe(new Subscription(DataType.MarketDepth, security));

connector.OrderBookReceived += (sub, orderBook) =>
{
    if (sub == subscription)
    {
        var bestBid = orderBook.Bids.FirstOrDefault();
        var bestAsk = orderBook.Asks.FirstOrDefault();

        Console.WriteLine($"Best Bid: {bestBid?.Price} x {bestBid?.Volume}");
        Console.WriteLine($"Best Ask: {bestAsk?.Price} x {bestAsk?.Volume}");
    }
};
```

### Scenario 3: Chart with Historical + Live Data

```csharp
// Start with historical data
var subscription = connector.Subscribe(new Subscription(
    new MarketDataMessage
    {
        DataType2 = DataType.TimeFrame(TimeSpan.FromMinutes(5)),
        SecurityId = security.ToSecurityId(),
        From = DateTime.Today.AddDays(-30),
        IsSubscribe = true
    },
    security
));

var candles = new List<ICandleMessage>();

connector.CandleReceived += (sub, candle) =>
{
    if (sub == subscription)
    {
        candles.Add(candle);
        UpdateChart(candles);
    }
};

connector.SubscriptionOnline += sub =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Chart loaded {candles.Count} historical candles, now real-time");
    }
};
```

### Scenario 4: Multi-Exchange Aggregation

```csharp
// Subscribe to same security on multiple exchanges
var binanceBTC = GetSecurity("BTC@Binance");
var coinbaseBTC = GetSecurity("BTC@Coinbase");

var binanceSub = connector.Subscribe(new Subscription(DataType.Level1, binanceBTC));
var coinbaseSub = connector.Subscribe(new Subscription(DataType.Level1, coinbaseBTC));

connector.Level1Received += (subscription, level1) =>
{
    if (subscription == binanceSub)
    {
        UpdatePrice("Binance", level1);
    }
    else if (subscription == coinbaseSub)
    {
        UpdatePrice("Coinbase", level1);
    }

    // Check for arbitrage opportunity
    CheckArbitrage();
};
```

---

## See Also

- **[connector.md](connector.md)** - Connector class documentation
- **[adapters.md](adapters.md)** - Adapter subscription support
- **[configuration.md](configuration.md)** - Persisting subscriptions
- **Subscription Class** - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Subscription.cs`
- **SubscriptionStates** - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\SubscriptionStates.cs`
- **Sample: Securities Window** - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\10_LiveTerminal\SecuritiesWindow.xaml.cs`
