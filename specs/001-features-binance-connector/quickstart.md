# Quickstart Guide: Binance Connector

## Overview
This guide demonstrates how to set up and use the StockSharp CryptoExchange framework, starting with Binance connectors. The framework uses CryptoExchange.Net and Binance.Net libraries to provide rapid multi-exchange support while maintaining StockSharp compatibility. The framework enables easy expansion to 20+ cryptocurrency exchanges with minimal additional development.

## Prerequisites
- StockSharp platform installed
- Binance account with API trading enabled
- API key and secret from Binance account settings
- .NET 8.0 or 9.0 runtime
- NuGet packages: CryptoExchange.Net, Binance.Net (automatically restored during build)

## Quick Setup (5 minutes)

### Step 1: Build CryptoExchange Framework
```bash
# From repository root
dotnet build Connectors/CryptoExchangeBase/CryptoExchangeBase.csproj
dotnet build Connectors/BinanceSpot/BinanceSpot.csproj
dotnet build Connectors/BinanceFutures/BinanceFutures.csproj
```

### Step 2: Configure Connector
```csharp
using StockSharp.CryptoExchange.Binance.Spot;  // or BinanceFutures
using StockSharp.Algo;
using StockSharp.Messages;

// Create connector instance
var connector = new Connector();

// Add Binance Spot adapter (extends CryptoExchangeAdapterBase)
var adapter = new BinanceSpotMessageAdapter(connector.TransactionIdGenerator)
{
    Key = "your-api-key-here",
    Secret = "your-api-secret-here".Secure(),
    UseTestnet = true,  // Set false for live trading

    // CryptoExchange.Net provides automatic rate limiting and reconnection
    AutoReconnect = true,
    ReconnectInterval = TimeSpan.FromSeconds(5)
};

connector.Adapter.InnerAdapters.Add(adapter);
```

### Step 3: Connect and Subscribe
```csharp
// Handle connection events
connector.Connected += () => Console.WriteLine("Connected to Binance");
connector.ConnectionError += error => Console.WriteLine($"Connection error: {error}");

// Connect
await connector.ConnectAsync();

// Subscribe to market data
var security = new Security
{
    Id = "BTCUSDT@BNB",  // Bitcoin/USDT on Binance
    Code = "BTCUSDT",
    Board = ExchangeBoard.Binance
};

connector.RegisterSecurity(security);
connector.Subscribe(security, MarketDataTypes.Level1);
connector.Subscribe(security, MarketDataTypes.MarketDepth);
```

## Basic Trading Example

### Market Data Monitoring
```csharp
// Handle tick data
connector.NewTrade += trade =>
{
    Console.WriteLine($"Trade: {trade.Security.Code} @ {trade.Price} x {trade.Volume}");
};

// Handle order book updates
connector.MarketDepthChanged += depth =>
{
    var bestBid = depth.BestBid?.Price;
    var bestAsk = depth.BestAsk?.Price;
    Console.WriteLine($"Spread: {bestBid} - {bestAsk}");
};
```

### Placing Orders
```csharp
// Create and register order
var order = new Order
{
    Security = security,
    Type = OrderTypes.Limit,
    Direction = Sides.Buy,
    Volume = 0.001m,  // 0.001 BTC
    Price = 50000m    // Limit price
};

// Handle order events
connector.OrderRegisterFailed += (order, error) =>
    Console.WriteLine($"Order failed: {error.Message}");

connector.OrderChanged += order =>
    Console.WriteLine($"Order {order.Id}: {order.State}");

// Register order
connector.RegisterOrder(order);
```

### Account Monitoring
```csharp
// Subscribe to portfolio updates
var portfolio = new Portfolio { Name = "Binance" };
connector.Subscribe(portfolio);

// Handle balance changes
connector.PortfolioChanged += portfolio =>
{
    Console.WriteLine($"Balance: {portfolio.CurrentValue} {portfolio.Currency}");
};

// Handle position updates
connector.PositionChanged += position =>
{
    Console.WriteLine($"Position {position.Security.Code}: {position.CurrentValue}");
};
```

## Configuration Options

### Environment Settings
```csharp
var adapter = new BinanceMessageAdapter(transactionIdGenerator)
{
    // Production settings
    UseTestnet = false,
    Key = Environment.GetEnvironmentVariable("BINANCE_API_KEY"),
    Secret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET"),

    // Optional settings
    RestClientOptions = options =>
    {
        options.RequestTimeout = TimeSpan.FromSeconds(30);
        options.ReceiveWindow = TimeSpan.FromSeconds(5);
    },

    WebSocketOptions = options =>
    {
        options.ReconnectInterval = TimeSpan.FromSeconds(5);
        options.MaxReconnectAttempts = 10;
    }
};
```

### Logging Configuration
```csharp
// Enable detailed logging
connector.LogLevel = LogLevels.Debug;
connector.Log += message =>
{
    Console.WriteLine($"[{message.Time}] {message.Level}: {message.Message}");
};
```

## Testing Your Setup

### Validation Checklist
- [ ] Connection establishes successfully
- [ ] Market data streams are received
- [ ] Order placement and cancellation work
- [ ] Account balances are displayed correctly
- [ ] Position updates are received

### Test Script
```csharp
async Task TestConnector()
{
    try
    {
        // 1. Test connection
        await connector.ConnectAsync();
        Assert(connector.ConnectionState == ConnectionStates.Connected,
               "Connection failed");

        // 2. Test market data
        var tickReceived = false;
        connector.NewTrade += _ => tickReceived = true;
        connector.Subscribe(security, MarketDataTypes.Trades);

        await Task.Delay(5000);  // Wait 5 seconds
        Assert(tickReceived, "No trade data received");

        // 3. Test account data
        var balanceReceived = false;
        connector.PortfolioChanged += _ => balanceReceived = true;

        await Task.Delay(2000);
        Assert(balanceReceived, "No balance data received");

        // 4. Test order (testnet only)
        if (adapter.UseTestnet)
        {
            var testOrder = new Order
            {
                Security = security,
                Type = OrderTypes.Limit,
                Direction = Sides.Buy,
                Volume = 0.001m,
                Price = 1000m  // Very low price to avoid execution
            };

            connector.RegisterOrder(testOrder);
            await Task.Delay(2000);

            Assert(testOrder.State != OrderStates.Failed,
                   "Test order registration failed");

            // Cancel test order
            connector.CancelOrder(testOrder);
        }

        Console.WriteLine("✅ All tests passed!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Test failed: {ex.Message}");
    }
    finally
    {
        await connector.DisconnectAsync();
    }
}
```

## Common Issues and Solutions

### Authentication Errors
```
Error: Invalid API key or signature
Solution: Verify API key/secret, check system time sync, ensure API permissions include spot trading
```

### Rate Limiting
```
Error: Too many requests (HTTP 429)
Solution: Implement request throttling, use WebSocket streams for real-time data instead of REST polling
```

### Symbol Not Found
```
Error: Invalid symbol BTCUSD
Solution: Use correct Binance symbol format (BTCUSDT, not BTCUSD)
```

### Precision Errors
```
Error: LOT_SIZE filter failure
Solution: Check symbol info for minimum quantity and step size requirements
```

## Performance Optimization

### Best Practices
1. **Use WebSocket streams** for real-time data instead of REST API polling
2. **Batch order operations** when placing multiple orders
3. **Cache symbol information** to avoid repeated API calls
4. **Implement proper error handling** with exponential backoff
5. **Monitor rate limits** to prevent API violations

### Memory Management
```csharp
// Dispose connector properly
using var connector = new Connector();
// ... use connector ...
// Automatic disposal on using scope exit
```

## Expanding to Other Exchanges

The CryptoExchange framework makes adding new exchanges simple:

### Adding Bybit Support (Example)
```csharp
// 1. Create new project: Connectors/Bybit/Bybit.csproj
// 2. Add NuGet reference to Bybit.Net
// 3. Implement adapter (2-3 days of work):

public class BybitMessageAdapter : CryptoExchangeAdapterBase<BybitRestClient, BybitSocketClient>
{
    public BybitMessageAdapter(IdGenerator transactionIdGenerator)
        : base(transactionIdGenerator)
    {
        // Bybit-specific configuration
        DisplayName = "Bybit";
        Description = "Bybit cryptocurrency exchange adapter";
    }

    // Override exchange-specific methods as needed
    protected override string GetBoardCode() => "BYBIT";
}
```

### Available Exchanges via CryptoExchange.Net
- **Derivatives**: Bybit, BitMEX, OKX, Deribit
- **Spot Trading**: KuCoin, Bitget, Gate.io, Huobi, Kraken
- **Regional**: Bitfinex (US/International), Coinbase Pro, Gemini

Each exchange takes approximately 2-3 days to implement using the shared framework.

## Next Steps
- Explore advanced order types (stop-loss, OCO orders)
- Add additional exchanges (Bybit, KuCoin, OKX)
- Implement custom trading strategies using StockSharp.Algo
- Set up historical data collection using StockSharp.Hydra
- Configure portfolio risk management rules
- Optimize performance for high-frequency use cases

## Support Resources
- Binance API Documentation: https://binance-docs.github.io/apidocs/
- StockSharp Documentation: https://stocksharp.com/doc/
- Community Forum: https://stocksharp.com/forum/
- GitHub Issues: https://github.com/StockSharp/StockSharp/issues