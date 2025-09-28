# cTrader Connector Quickstart Guide

## Prerequisites

### 1. cTrader Account Setup
- Create cTrader account (demo or live) at https://ctrader.com/
- Register application for API access at https://openapi.ctrader.com/
- Obtain Application ID and Application Secret
- Note your cTrader Account ID for trading

### 2. Development Environment
- .NET 6 SDK installed
- StockSharp platform references
- cTrader.OpenAPI.Net NuGet package (v1.4.4+)

## Basic Connection Setup

### Step 1: Configure Connection Settings
```csharp
using StockSharp.CTrader;

// Create connector instance
var connector = new CTraderMessageAdapter(TransactionIdGenerator.GetNextId())
{
    ApplicationId = "your_application_id",
    ApplicationSecret = new SecureString("your_application_secret"),
    Environment = CTraderEnvironment.Demo,  // or Live
    AccountId = 12345,  // Your cTrader account ID
    HeartbeatInterval = TimeSpan.FromSeconds(30)
};
```

### Step 2: Handle Connection Events
```csharp
// Connection state monitoring
connector.ConnectionStateChanged += (newState, oldState) =>
{
    Console.WriteLine($"Connection: {oldState} → {newState}");

    switch (newState)
    {
        case ConnectionStates.Connected:
            Console.WriteLine("Successfully connected to cTrader");
            break;
        case ConnectionStates.Failed:
            Console.WriteLine("Connection failed - check credentials");
            break;
        case ConnectionStates.Disconnected:
            Console.WriteLine("Disconnected from cTrader");
            break;
    }
};

// Error handling
connector.Error += error =>
{
    Console.WriteLine($"Error: {error.Message}");
};
```

### Step 3: Establish Connection
```csharp
// Connect to cTrader
try
{
    await connector.ConnectAsync();
    Console.WriteLine("Connection initiated...");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
```

## Market Data Subscription

### Step 4: Subscribe to Real-time Market Data
```csharp
// Security definition
var eurUsd = new Security
{
    Id = "EURUSD@CTrader",
    Code = "EURUSD",
    Board = ExchangeBoard.Associated
};

// Subscribe to Level 1 data (best bid/ask)
connector.Subscribe(eurUsd, MarketDataTypes.Level1);

// Subscribe to market depth (order book)
connector.Subscribe(eurUsd, MarketDataTypes.MarketDepth);

// Subscribe to tick data (trades)
connector.Subscribe(eurUsd, MarketDataTypes.Trades);

// Handle market data updates
connector.MarketDepthReceived += depth =>
{
    Console.WriteLine($"{depth.Security.Code}: " +
                     $"Bid={depth.BestBid?.Price} " +
                     $"Ask={depth.BestAsk?.Price}");
};

connector.NewTrade += trade =>
{
    Console.WriteLine($"Trade: {trade.Security.Code} " +
                     $"{trade.Price} x {trade.Volume} @ {trade.Time}");
};
```

### Step 5: Request Historical Data
```csharp
// Request 1-hour candles for last 7 days
var from = DateTime.Today.AddDays(-7);
var to = DateTime.Today;

connector.Subscribe(eurUsd, new CandleSeries
{
    CandleType = typeof(TimeFrameCandle),
    Arg = TimeSpan.FromHours(1)
}, from, to);

// Handle historical candles
connector.CandleReceived += candle =>
{
    var tf = (TimeFrameCandle)candle;
    Console.WriteLine($"Candle: {tf.OpenTime} " +
                     $"O={tf.OpenPrice} H={tf.HighPrice} " +
                     $"L={tf.LowPrice} C={tf.ClosePrice} V={tf.TotalVolume}");
};
```

## Trading Operations

### Step 6: Place Market Order
```csharp
// Create and register market order
var order = new Order
{
    Security = eurUsd,
    Direction = Sides.Buy,
    Volume = 100000,  // 1 standard lot
    Type = OrderTypes.Market
};

connector.RegisterOrder(order);

// Handle order state changes
connector.OrderChanged += order =>
{
    Console.WriteLine($"Order {order.Id}: {order.State} " +
                     $"Volume={order.Volume} Balance={order.Balance}");

    if (order.State == OrderStates.Done)
    {
        Console.WriteLine($"Order completed: {order.Volume} @ {order.Price}");
    }
};

// Handle trade executions
connector.NewMyTrade += trade =>
{
    Console.WriteLine($"Trade executed: {trade.Order.Security.Code} " +
                     $"{trade.Trade.Price} x {trade.Trade.Volume}");
};
```

### Step 7: Place Limit Order
```csharp
// Create limit order
var limitOrder = new Order
{
    Security = eurUsd,
    Direction = Sides.Buy,
    Volume = 50000,   // 0.5 lot
    Price = 1.0840m,  // Limit price
    Type = OrderTypes.Limit,
    TimeInForce = TimeInForce.GoodTillCancel
};

connector.RegisterOrder(limitOrder);
```

### Step 8: Cancel Order
```csharp
// Cancel pending order
if (limitOrder.State == OrderStates.Active)
{
    connector.CancelOrder(limitOrder);
}
```

## Portfolio Monitoring

### Step 9: Monitor Portfolio and Positions
```csharp
// Request portfolio information
connector.LookupPortfolios();

// Handle portfolio updates
connector.PositionReceived += position =>
{
    Console.WriteLine($"Position: {position.Security.Code} " +
                     $"{position.CurrentValue} @ {position.AveragePrice} " +
                     $"P&L: {position.UnrealizedPnL}");
};

connector.PortfolioReceived += portfolio =>
{
    Console.WriteLine($"Portfolio: {portfolio.Name} " +
                     $"Balance={portfolio.BeginValue} " +
                     $"Equity={portfolio.CurrentValue} " +
                     $"Margin={portfolio.BlockedValue}");
};
```

## Complete Example

### Minimal Trading Application
```csharp
using System;
using System.Security;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.CTrader;
using StockSharp.BusinessEntities;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Create connector
        var connector = new Connector();
        var adapter = new CTraderMessageAdapter(connector.TransactionIdGenerator)
        {
            ApplicationId = "your_app_id",
            ApplicationSecret = CreateSecureString("your_app_secret"),
            Environment = CTraderEnvironment.Demo,
            AccountId = 12345
        };

        connector.Adapter.InnerAdapters.Add(adapter);

        // 2. Handle events
        connector.Connected += () => Console.WriteLine("Connected!");
        connector.NewSecurity += security => Console.WriteLine($"Security: {security.Code}");
        connector.MarketDepthReceived += depth =>
            Console.WriteLine($"{depth.Security.Code}: {depth.BestBid?.Price}/{depth.BestAsk?.Price}");

        // 3. Connect and lookup securities
        await connector.ConnectAsync();
        connector.LookupSecurities(new Security { Code = "EURUSD" });

        // 4. Wait for securities and start trading
        await Task.Delay(5000);

        var eurUsd = connector.Securities.FirstOrDefault(s => s.Code == "EURUSD");
        if (eurUsd != null)
        {
            // Subscribe to market data
            connector.Subscribe(eurUsd, MarketDataTypes.Level1);

            // Place test order after market data received
            await Task.Delay(2000);
            var order = new Order
            {
                Security = eurUsd,
                Direction = Sides.Buy,
                Volume = 1000,  // Micro lot for testing
                Type = OrderTypes.Market
            };
            connector.RegisterOrder(order);
        }

        // 5. Keep running
        Console.WriteLine("Press any key to disconnect...");
        Console.ReadKey();

        connector.Dispose();
    }

    static SecureString CreateSecureString(string value)
    {
        var secure = new SecureString();
        foreach (char c in value)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }
}
```

## Testing Scenarios

### Scenario 1: Connection Verification
```csharp
// Test connection establishment
1. Configure valid demo account credentials
2. Call ConnectAsync()
3. Verify Connected event fires within 10 seconds
4. Check LookupSecurities returns available instruments
5. Confirm HeartbeatInterval maintains connection

Expected Result:
- ConnectionState.Connected
- Securities list populated
- No authentication errors
```

### Scenario 2: Market Data Flow
```csharp
// Test real-time data subscription
1. Connect to demo account
2. Subscribe to EURUSD Level1 data
3. Subscribe to EURUSD market depth
4. Verify data updates received within 5 seconds
5. Check data quality (reasonable prices, positive volumes)

Expected Result:
- MarketDepthReceived events with bid/ask updates
- Level1 data shows current market prices
- Data latency under 100ms (per requirements)
```

### Scenario 3: Order Execution
```csharp
// Test basic order lifecycle
1. Connect and subscribe to market data
2. Place small market order (1000 units)
3. Verify order acceptance within 1 second
4. Confirm trade execution notification
5. Check position update reflects new holding

Expected Result:
- Order state: Pending → Active → Done
- Trade notification with execution details
- Position updated with correct volume and P&L
```

### Scenario 4: Error Handling
```csharp
// Test error scenarios
1. Connect with invalid credentials → Authentication error
2. Place order with invalid volume → Validation error
3. Disconnect during operation → Reconnection attempt
4. Subscribe to non-existent symbol → Subscription error

Expected Result:
- Appropriate error messages for each scenario
- System remains stable after errors
- Automatic recovery where possible
```

## Configuration Tips

### Security Best Practices
- Store credentials securely (use SecureString for secrets)
- Use demo environment for testing and development
- Never log or expose API credentials
- Implement proper error handling for authentication failures

### Performance Optimization
- Limit concurrent subscriptions to 10 instruments initially
- Use appropriate timeframes for historical data requests
- Implement local caching for frequently accessed data
- Monitor memory usage during extended operations

### Production Considerations
- Implement comprehensive logging for troubleshooting
- Add health monitoring and alerting
- Plan for network connectivity issues
- Test failover and recovery procedures
- Validate all order parameters before submission

## Troubleshooting

### Common Issues
1. **Authentication Failed**: Verify Application ID/Secret and account access
2. **Connection Timeout**: Check network connectivity and firewall settings
3. **Invalid Symbol**: Ensure symbol exists and is tradeable on cTrader
4. **Insufficient Margin**: Check account balance and leverage settings
5. **Market Closed**: Verify trading hours for target instruments

### Debug Configuration
```csharp
// Enable detailed logging
connector.LogLevel = LogLevels.Debug;
connector.LogReceive = true;
connector.LogSend = true;

// Monitor message flow
connector.NewInMessage += message => Console.WriteLine($"IN: {message}");
connector.NewOutMessage += message => Console.WriteLine($"OUT: {message}");
```

This quickstart guide provides the foundation for integrating cTrader with StockSharp. Start with the basic connection and market data scenarios before implementing trading operations.