# Quick Start Guide

## Build Your First Trading Bot in 5 Minutes

This guide will help you create a simple trading bot that connects to an exchange and executes a basic trading strategy.

## Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK or later
- StockSharp NuGet packages

## Step 1: Create a New Project

```bash
dotnet new console -n MyTradingBot
cd MyTradingBot
```

## Step 2: Add StockSharp Packages

```xml
<ItemGroup>
  <PackageReference Include="StockSharp.Algo" Version="$(StockSharpVer)" />
  <PackageReference Include="StockSharp.Binance" Version="$(StockSharpVer)" />
</ItemGroup>
```

Or use NuGet Package Manager:
```bash
dotnet add package StockSharp.Algo
dotnet add package StockSharp.Binance
```

## Step 3: Create a Simple Bot

**File**: `Program.cs`

```csharp
using System;
using StockSharp.Algo;
using StockSharp.Binance;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

class Program
{
    static void Main()
    {
        // Step 1: Create connector
        var connector = new Connector();

        // Step 2: Add exchange adapter
        var adapter = new BinanceMessageAdapter(connector.TransactionIdGenerator)
        {
            Key = "YOUR_API_KEY",
            Secret = "YOUR_API_SECRET"
        };
        connector.Adapter.InnerAdapters.Add(adapter);

        // Step 3: Subscribe to connection events
        connector.Connected += () =>
        {
            Console.WriteLine("Connected!");

            // Lookup all securities
            connector.LookupAll();
        };

        connector.ConnectionError += error =>
        {
            Console.WriteLine($"Connection error: {error}");
        };

        // Step 4: Subscribe to security received event
        connector.SecurityReceived += (subscription, security) =>
        {
            Console.WriteLine($"Security: {security.Id}");

            // Subscribe to Level1 data for Bitcoin
            if (security.Code == "BTCUSDT")
            {
                connector.Subscribe(new Subscription(DataType.Level1, security));
            }
        };

        // Step 5: Subscribe to market data
        connector.Level1Received += (subscription, level1) =>
        {
            var price = level1.Changes.TryGetValue(Level1Fields.LastTradePrice);
            if (price != null)
            {
                Console.WriteLine($"BTC Price: {price}");
            }
        };

        // Step 6: Connect
        connector.Connect();

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();

        // Cleanup
        connector.Disconnect();
        connector.Dispose();
    }
}
```

## Step 4: Run the Bot

```bash
dotnet run
```

**Expected Output**:
```
Connected!
Security: BTCUSDT@Binance
BTC Price: 43250.50
BTC Price: 43251.00
...
```

## Understanding the Code

### 1. Connector Creation
```csharp
var connector = new Connector();
```
The `Connector` is your main interface to the trading system. It manages:
- Connections to exchanges
- Market data subscriptions
- Order execution
- Entity caching

**Location**: `Algo/Connector.cs:15`

### 2. Adapter Configuration
```csharp
var adapter = new BinanceMessageAdapter(connector.TransactionIdGenerator);
```
Each exchange requires a specific adapter that translates between StockSharp messages and the exchange's protocol.

**Location**: `Connectors/Binance/`

### 3. Connection Events
```csharp
connector.Connected += () => { /* ... */ };
```
Use events to react to connection state changes:
- `Connected` - Successfully connected
- `Disconnected` - Connection closed
- `ConnectionError` - Connection failed

### 4. Security Lookup
```csharp
connector.LookupAll();
```
Requests all available securities from the exchange. Results arrive via `SecurityReceived` event.

### 5. Market Data Subscription
```csharp
connector.Subscribe(new Subscription(DataType.Level1, security));
```
Subscribe to real-time market data:
- `DataType.Level1` - Best bid/ask, last price, volume
- `DataType.MarketDepth` - Full order book
- `DataType.Ticks` - Trade history
- `DataType.TimeFrame(...)` - Candles

### 6. Market Data Processing
```csharp
connector.Level1Received += (subscription, level1) => { /* ... */ };
```
Process incoming market data in real-time.

## Next Step: Add Trading Logic

Let's extend the bot to place orders:

```csharp
Security btcSecurity = null;
Portfolio portfolio = null;

connector.SecurityReceived += (subscription, security) =>
{
    if (security.Code == "BTCUSDT")
    {
        btcSecurity = security;
        Console.WriteLine($"Found BTC: {security.Id}");
    }
};

connector.PortfolioReceived += (subscription, pf) =>
{
    portfolio = pf;
    Console.WriteLine($"Portfolio: {pf.Name}");
};

connector.Connected += () =>
{
    Console.WriteLine("Connected!");

    // Lookup securities and portfolios
    connector.LookupAll();
    connector.Subscribe(connector.PortfolioLookup);
};

// Wait for data to be loaded
System.Threading.Thread.Sleep(3000);

// Place a limit buy order
if (btcSecurity != null && portfolio != null)
{
    var order = new Order
    {
        Security = btcSecurity,
        Portfolio = portfolio,
        Price = 40000, // Limit price
        Volume = 0.001m, // 0.001 BTC
        Side = Sides.Buy,
        Type = OrderTypes.Limit
    };

    connector.OrderReceived += (sub, ord) =>
    {
        Console.WriteLine($"Order state: {ord.State}");
    };

    connector.RegisterOrder(order);
    Console.WriteLine($"Order registered: {order.TransactionId}");
}
```

## Common Patterns

### Pattern 1: Wait for Connection
```csharp
var connected = false;
connector.Connected += () => { connected = true; };
connector.Connect();

while (!connected)
    System.Threading.Thread.Sleep(100);
```

### Pattern 2: Find Specific Security
```csharp
Security FindSecurity(Connector connector, string code)
{
    Security result = null;
    var found = new System.Threading.ManualResetEvent(false);

    connector.SecurityReceived += (sub, sec) =>
    {
        if (sec.Code == code)
        {
            result = sec;
            found.Set();
        }
    };

    connector.LookupAll();
    found.WaitOne(TimeSpan.FromSeconds(10));

    return result;
}
```

### Pattern 3: Track Order Status
```csharp
void RegisterAndTrack(Connector connector, Order order)
{
    connector.OrderReceived += OnOrderUpdate;
    connector.OrderRegisterFailReceived += OnOrderFail;

    connector.RegisterOrder(order);
}

void OnOrderUpdate(Subscription sub, Order order)
{
    Console.WriteLine($"Order {order.TransactionId}: {order.State}");

    if (order.State == OrderStates.Done)
    {
        Console.WriteLine($"Order filled at {order.Price}");
    }
}

void OnOrderFail(Subscription sub, OrderFail fail)
{
    Console.WriteLine($"Order failed: {fail.Error.Message}");
}
```

## Configuration Best Practices

### 1. Use Configuration Files
```csharp
// Save connection settings
var storage = connector.Save();
File.WriteAllText("settings.xml", storage.Serialize());

// Load connection settings
var loadedStorage = File.ReadAllText("settings.xml").Deserialize<SettingsStorage>();
connector.Load(loadedStorage);
```

### 2. Enable Logging
```csharp
using StockSharp.Logging;

var logManager = new LogManager();
logManager.Listeners.Add(new FileLogListener
{
    LogDirectory = "Logs"
});
logManager.Sources.Add(connector);
```

### 3. Handle Errors Gracefully
```csharp
connector.Error += error =>
{
    Console.WriteLine($"Error: {error}");
    // Log, notify, or take corrective action
};

connector.SubscriptionFailed += (sub, error, isSubscribe) =>
{
    Console.WriteLine($"Subscription failed: {error}");
};
```

## Complete Example: Simple Market Monitor

```csharp
using System;
using System.Linq;
using StockSharp.Algo;
using StockSharp.Binance;
using StockSharp.Messages;
using StockSharp.Logging;

class MarketMonitor
{
    static void Main()
    {
        // Setup
        var connector = new Connector();
        var logManager = new LogManager();
        logManager.Listeners.Add(new ConsoleLogListener());
        logManager.Sources.Add(connector);

        // Add Binance adapter
        connector.Adapter.InnerAdapters.Add(new BinanceMessageAdapter(connector.TransactionIdGenerator));

        // Connection handling
        connector.Connected += () => Console.WriteLine("=== Connected ===");
        connector.Disconnected += () => Console.WriteLine("=== Disconnected ===");

        connector.ConnectionError += error =>
        {
            Console.WriteLine($"Connection Error: {error.Message}");
        };

        // Track top 5 securities
        var tracked = new System.Collections.Generic.HashSet<string>
        {
            "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "DOGEUSDT"
        };

        connector.SecurityReceived += (sub, security) =>
        {
            if (tracked.Contains(security.Code))
            {
                Console.WriteLine($"Subscribing to {security.Code}");
                connector.Subscribe(new Subscription(DataType.Level1, security));
            }
        };

        // Display market data
        connector.Level1Received += (sub, level1) =>
        {
            var security = connector.GetSecurity(level1.SecurityId);
            var price = level1.Changes.TryGetValue(Level1Fields.LastTradePrice);
            var volume = level1.Changes.TryGetValue(Level1Fields.LastTradeVolume);

            if (price != null && volume != null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} | {security.Code,-10} | " +
                                $"Price: {price,10:F2} | Volume: {volume,8:F4}");
            }
        };

        // Start
        connector.Connect();
        connector.LookupAll();

        Console.WriteLine("Monitoring market data. Press any key to exit...");
        Console.ReadKey();

        // Cleanup
        connector.Disconnect();
        connector.Dispose();
    }
}
```

## Troubleshooting

### Issue: "Connection timeout"
**Solution**: Check your API credentials and network connection.

### Issue: "Security not found"
**Solution**: Wait for `SecurityReceived` events before accessing securities:
```csharp
var securities = new List<Security>();
connector.SecurityReceived += (sub, sec) => securities.Add(sec);
connector.LookupAll();
System.Threading.Thread.Sleep(2000); // Wait for lookup
```

### Issue: "Order rejected"
**Solution**: Verify:
- Security has correct `PriceStep` and `VolumeStep`
- Portfolio has sufficient balance
- Price and volume meet exchange requirements

### Issue: "No market data"
**Solution**: Ensure you subscribed after receiving the security:
```csharp
connector.SecurityReceived += (sub, security) =>
{
    connector.Subscribe(new Subscription(DataType.Level1, security));
};
```

## Next Steps

Now that you have a basic bot running:

1. **Add Strategy Logic** → [Strategy Basics](../05-Strategy-Framework/strategy-basics.md)
2. **Use Technical Indicators** → [Indicator Basics](../06-Indicators/indicator-basics.md)
3. **Implement Risk Management** → [Risk Rules](../08-Risk-And-Money-Management/risk-rules.md)
4. **Study Complete Example** → [SMA Crossover Strategy](../11-Examples/sma-crossover.md)
5. **Build Trading Terminal** → [Live Terminal Walkthrough](../11-Examples/live-terminal.md)

## See Also

- [Platform Overview](overview.md) - Architecture and components
- [Core Concepts](core-concepts.md) - Essential terminology
- [Connector](../02-Connection-Management/connector.md) - Detailed connector API
- [Order Management](../04-Trading/order-management.md) - Trading operations
- [Simple Bot Example](../11-Examples/simple-bot.md) - Complete minimal bot
