# StockSharp Connector Feature Specification

## Overview

This document provides a comprehensive specification for developing new connectors in the StockSharp trading platform. It is based on the analysis of the BitStamp connector implementation, which serves as a reference architecture for cryptocurrency exchange integrations.

## Architecture Overview

### Core Design Pattern

StockSharp connectors follow a well-defined architectural pattern:

1. **MessageAdapter Pattern**: All connectors inherit from `AsyncMessageAdapter` (or `MessageAdapter`)
2. **Native Layer Separation**: Native API integration is isolated in a `Native/` subdirectory
3. **Partial Classes**: Functionality is split across multiple partial class files for maintainability
4. **Event-Driven Model**: Asynchronous message-based communication throughout

### Project Structure

```
ConnectorName/
├── ConnectorName.csproj              # Project file with dependencies
├── ConnectorNameMessageAdapter.cs    # Main adapter class
├── ConnectorNameMessageAdapter_MarketData.cs    # Market data methods
├── ConnectorNameMessageAdapter_Transaction.cs   # Trading operations
├── ConnectorNameMessageAdapter_Settings.cs      # Settings and persistence
├── ConnectorNameOrderCondition.cs    # Custom order conditions (optional)
├── Native/
│   ├── HttpClient.cs                 # REST API client
│   ├── WebSocketClient.cs            # WebSocket client (if applicable)
│   ├── Extensions.cs                 # Utility extensions
│   └── Model/                        # Native data models
│       ├── Order.cs
│       ├── Trade.cs
│       ├── OrderBook.cs
│       ├── Symbol.cs
│       └── ...
└── Properties/
    ├── AssemblyInfo.cs
    └── usings.cs
```

## Component Specifications

### 1. Project Configuration (.csproj)

**Purpose**: Define build settings, dependencies, and framework targets

**Key Elements**:
- Import shared connector properties file
- Reference core StockSharp projects (Messages, Media.Names, Algo)
- Add external dependencies (REST clients, WebSocket libraries, JSON parsers)

**Example** (from BitStamp):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\common_connectors_websocket.props" />
</Project>
```

**Common Dependencies**:
- `Ecng.Net.SocketIO` - WebSocket support
- `Newtonsoft.Json` or `System.Text.Json` - JSON serialization
- Exchange-specific SDKs (if available)

### 2. Main MessageAdapter Class

**Purpose**: Core connector logic, connection management, message routing

**Required Implementation**:

```csharp
[OrderCondition(typeof(ConnectorNameOrderCondition))]
public partial class ConnectorNameMessageAdapter : AsyncMessageAdapter
{
    // Fields for client instances
    private HttpClient _httpClient;
    private WebSocketClient _webSocketClient;

    // Constructor
    public ConnectorNameMessageAdapter(IdGenerator transactionIdGenerator)
        : base(transactionIdGenerator)
    {
        HeartbeatInterval = DefaultHeartbeatInterval;

        // Declare capabilities
        this.AddMarketDataSupport();
        this.AddTransactionalSupport();

        // Declare supported message types
        this.RemoveSupportedMessage(MessageTypes.OrderReplace); // if not supported

        // Declare supported data types
        this.AddSupportedMarketDataType(DataType.Ticks);
        this.AddSupportedMarketDataType(DataType.MarketDepth);
        this.AddSupportedMarketDataType(DataType.Level1);
        this.AddSupportedCandleTimeFrames(AllTimeFrames);
    }

    // Connection management
    public override async ValueTask ConnectAsync(ConnectMessage connectMsg, CancellationToken cancellationToken);
    public override ValueTask DisconnectAsync(DisconnectMessage disconnectMsg, CancellationToken cancellationToken);
    public override ValueTask ResetAsync(ResetMessage resetMsg, CancellationToken cancellationToken);

    // Heartbeat
    public override async ValueTask TimeAsync(TimeMessage timeMsg, CancellationToken cancellationToken);

    // Board association
    public override string[] AssociatedBoards { get; }
}
```

**Key Responsibilities**:
- Initialize and manage native API clients
- Validate authentication credentials
- Subscribe/unsubscribe to native client events
- Handle connection state changes
- Implement heartbeat/ping logic
- Dispose resources properly

### 3. Market Data Partial Class (_MarketData.cs)

**Purpose**: Handle all market data subscriptions and historical data requests

**Required Methods**:

```csharp
partial class ConnectorNameMessageAdapter
{
    // Event handlers for real-time data
    private void SessionOnNewTrade(string symbol, Trade trade);
    private void SessionOnNewOrderBook(string symbol, OrderBook book);
    private void SessionOnNewOrderLog(string symbol, OrderStates state, Order order);

    // Subscription methods
    protected override async ValueTask OnTicksSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken);
    protected override async ValueTask OnMarketDepthSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken);
    protected override async ValueTask OnOrderLogSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken);
    protected override async ValueTask OnTFCandlesSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken);

    // Security lookup
    public override async ValueTask SecurityLookupAsync(SecurityLookupMessage lookupMsg, CancellationToken cancellationToken);
}
```

**Pattern for Subscription Methods**:
1. Send subscription reply immediately
2. Handle historical data if requested (mdMsg.From/To)
3. Subscribe to real-time updates if not history-only
4. Send subscription result/finished
5. For unsubscribe, remove subscription from native client

**Data Conversion**:
- Convert native data models to StockSharp messages:
  - Trades → `ExecutionMessage` with `DataType.Ticks`
  - Order book → `QuoteChangeMessage`
  - Candles → `TimeFrameCandleMessage`
  - Level1 → `Level1ChangeMessage`

### 4. Transaction Partial Class (_Transaction.cs)

**Purpose**: Handle trading operations and account management

**Required Methods**:

```csharp
partial class ConnectorNameMessageAdapter
{
    // Order operations
    public override async ValueTask RegisterOrderAsync(OrderRegisterMessage regMsg, CancellationToken cancellationToken);
    public override async ValueTask CancelOrderAsync(OrderCancelMessage cancelMsg, CancellationToken cancellationToken);
    public override async ValueTask CancelOrderGroupAsync(OrderGroupCancelMessage cancelMsg, CancellationToken cancellationToken);

    // Order status tracking
    public override async ValueTask OrderStatusAsync(OrderStatusMessage statusMsg, CancellationToken cancellationToken);

    // Portfolio/account data
    public override async ValueTask PortfolioLookupAsync(PortfolioLookupMessage lookupMsg, CancellationToken cancellationToken);

    // Helper methods
    private void ProcessOrder(NativeOrder order, decimal balance, long transId, long origTransId);
    private void ProcessTrade(NativeTransaction transaction);
}
```

**Order Registration Flow**:
1. Validate order parameters
2. Convert StockSharp order to native format
3. Send order to exchange via native client
4. Store order tracking info (transaction ID mapping)
5. Send `ExecutionMessage` with order confirmation

**Order Tracking**:
- Maintain dictionary mapping exchange order IDs to transaction IDs
- Track remaining order balance
- Update order state as executions occur
- Handle partial fills correctly

**Portfolio Updates**:
- Query account balances
- Convert to `PortfolioMessage` and `PositionChangeMessage`
- Include commission rates if available

### 5. Settings Partial Class (_Settings.cs)

**Purpose**: Authentication, configuration, persistence

**Required Implementation**:

```csharp
public partial class ConnectorNameMessageAdapter : IKeySecretAdapter
{
    // Authentication properties
    [Display(...)]
    [BasicSetting]
    public SecureString Key { get; set; }

    [Display(...)]
    [BasicSetting]
    public SecureString Secret { get; set; }

    // Additional settings
    [Display(...)]
    public TimeSpan BalanceCheckInterval { get; set; }

    // Persistence
    public override void Save(SettingsStorage storage);
    public override void Load(SettingsStorage storage);

    // Display
    public override string ToString();
}
```

**Attributes**:
- `[MediaIcon(...)]` - Icon for UI
- `[Doc(...)]` - Documentation URL
- `[Display(...)]` - UI labels and grouping
- `[MessageAdapterCategory(...)]` - Connector capabilities classification
- `[BasicSetting]` - Essential settings shown in simplified UI

**Security**:
- Use `SecureString` for sensitive data (API keys, secrets)
- Never log sensitive information
- Properly dispose of SecureString instances

### 6. Order Condition Class (Optional)

**Purpose**: Define custom order types and conditions

**Example**:
```csharp
[Serializable]
[DataContract]
[Display(...)]
public class ConnectorNameOrderCondition : BaseWithdrawOrderCondition, IStopLossOrderCondition
{
    [DataMember]
    [Display(...)]
    public decimal? StopPrice { get; set; }

    // Implement interface properties
}
```

Use when exchange supports:
- Stop-loss orders
- Take-profit orders
- Trailing stops
- Conditional orders
- Withdrawal operations

### 7. Native Layer Components

#### HTTP Client

**Purpose**: REST API communication

**Key Features**:
- Authentication (HMAC signatures, OAuth, etc.)
- Rate limiting
- Error handling
- Request/response logging
- Nonce generation for signatures

**Pattern**:
```csharp
class HttpClient : BaseLogReceiver
{
    private readonly SecureString _key;
    private readonly HashAlgorithm _hasher;
    private readonly IdGenerator _nonceGen;

    public HttpClient(SecureString key, SecureString secret);

    // API methods
    public ValueTask<IEnumerable<Symbol>> GetSymbols(CancellationToken ct);
    public ValueTask<IEnumerable<Trade>> GetTrades(string symbol, CancellationToken ct);
    public ValueTask<OrderBook> GetOrderBook(string symbol, CancellationToken ct);
    public ValueTask<NativeOrder> PlaceOrder(..., CancellationToken ct);
    public ValueTask<bool> CancelOrder(long orderId, CancellationToken ct);

    private RestRequest ApplyAuthentication(RestRequest request, Uri url);
    private async ValueTask<T> MakeRequest<T>(Uri url, RestRequest request, CancellationToken ct);
}
```

#### WebSocket Client

**Purpose**: Real-time data streaming

**Key Features**:
- Connection management (connect, disconnect, reconnect)
- Channel subscriptions
- Message parsing
- Event dispatching

**Pattern**:
```csharp
class WebSocketClient : BaseLogReceiver
{
    public event Action<ConnectionStates> StateChanged;
    public event Action<Exception> Error;
    public event Action<string, Trade> NewTrade;
    public event Action<string, OrderBook> NewOrderBook;

    public async Task Connect(CancellationToken ct);
    public void Disconnect();

    public async Task SubscribeTrades(long transactionId, string symbol, CancellationToken ct);
    public async Task UnsubscribeTrades(long transactionId, string symbol, CancellationToken ct);

    private void OnMessage(string message);
}
```

#### Native Models

**Purpose**: Data transfer objects for API responses

**Guidelines**:
- Keep models simple (properties only)
- Use nullable types where appropriate
- Add JSON serialization attributes
- Provide conversion methods to StockSharp types

**Example**:
```csharp
class Symbol
{
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal MinimumOrder { get; set; }
    public int BaseDecimals { get; set; }
}

class Trade
{
    public long Id { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public DateTime Time { get; set; }
    public int Type { get; set; } // 0=buy, 1=sell
}
```

#### Extensions

**Purpose**: Utility methods for conversions

**Common Extensions**:
```csharp
static class Extensions
{
    // Symbol conversions
    public static SecurityId ToStockSharp(this string nativeSymbol);
    public static string ToNative(this SecurityId securityId);

    // Side conversions
    public static Sides ToSide(this int nativeType);
    public static int ToNative(this Sides side);

    // Time conversions
    public static DateTime ToUtcDateTime(this long unixTimestamp);
    public static long ToUnixTimestamp(this DateTime dateTime);
}
```

## Supported Features Matrix

### Market Data

| Feature | Interface | Message Type | Notes |
|---------|-----------|--------------|-------|
| Ticks | `OnTicksSubscriptionAsync` | `ExecutionMessage` (DataType.Ticks) | Trade history + real-time |
| Order Book | `OnMarketDepthSubscriptionAsync` | `QuoteChangeMessage` | Full depth snapshots |
| Order Log | `OnOrderLogSubscriptionAsync` | `ExecutionMessage` (DataType.OrderLog) | Individual order events |
| Level1 | - | `Level1ChangeMessage` | Best bid/ask, last price |
| Candles | `OnTFCandlesSubscriptionAsync` | `TimeFrameCandleMessage` | Historical + real-time |

### Trading Operations

| Feature | Method | Message Type | Notes |
|---------|--------|--------------|-------|
| Market Orders | `RegisterOrderAsync` | `OrderRegisterMessage` | Immediate execution |
| Limit Orders | `RegisterOrderAsync` | `OrderRegisterMessage` | Price-based execution |
| Stop Orders | `RegisterOrderAsync` | `OrderRegisterMessage` | Requires OrderCondition |
| Cancel Order | `CancelOrderAsync` | `OrderCancelMessage` | Single order cancellation |
| Cancel All | `CancelOrderGroupAsync` | `OrderGroupCancelMessage` | Mass cancellation |
| Order Status | `OrderStatusAsync` | `OrderStatusMessage` | Status updates |

### Account Management

| Feature | Method | Message Type | Notes |
|---------|--------|--------------|-------|
| Portfolio Lookup | `PortfolioLookupAsync` | `PortfolioMessage` | Account info |
| Position Tracking | `PortfolioLookupAsync` | `PositionChangeMessage` | Per-instrument positions |
| Balance Updates | `PortfolioLookupAsync` | `PositionChangeMessage` | Available/blocked amounts |

### Additional Features

| Feature | Support | Notes |
|---------|---------|-------|
| Securities Lookup | Required | `SecurityLookupAsync` implementation |
| Historical Data | Optional | Ranges via From/To parameters |
| Real-time Data | Recommended | WebSocket preferred over polling |
| Order Modification | Optional | `ReplaceOrderAsync` if supported |
| Withdrawals | Optional | Via custom OrderCondition |

## Implementation Guidelines

### 1. Connection Management

**Best Practices**:
- Validate credentials before connecting
- Check for existing connections (prevent duplicates)
- Subscribe to native client events before connecting
- Handle connection state changes gracefully
- Implement proper cleanup in `DisconnectAsync` and `ResetAsync`
- Use `lock` for thread-safe client access

**Example Pattern**:
```csharp
public override async ValueTask ConnectAsync(ConnectMessage connectMsg, CancellationToken cancellationToken)
{
    // Validate
    if (this.IsTransactional())
    {
        if (Key.IsEmpty())
            throw new InvalidOperationException(LocalizedStrings.KeyNotSpecified);
        if (Secret.IsEmpty())
            throw new InvalidOperationException(LocalizedStrings.SecretNotSpecified);
    }

    // Check state
    if (_httpClient != null)
        throw new InvalidOperationException(LocalizedStrings.NotDisconnectPrevTime);

    // Initialize
    _httpClient = new HttpClient(Key, Secret) { Parent = this };
    _webSocketClient = new WebSocketClient() { Parent = this };

    // Subscribe to events
    SubscribeToClientEvents();

    // Connect
    await _webSocketClient.Connect(cancellationToken);
}
```

### 2. Error Handling

**Guidelines**:
- Catch exceptions at API boundaries
- Send error information via messages (set `Error` property)
- Log errors using `SendOutError()` or `AddErrorLog()`
- Don't throw exceptions in event handlers
- Validate input parameters

**Example**:
```csharp
try
{
    var result = await _httpClient.PlaceOrder(..., cancellationToken);
    SendOutMessage(new ExecutionMessage { ... });
}
catch (Exception ex)
{
    SendOutMessage(new ExecutionMessage
    {
        OriginalTransactionId = regMsg.TransactionId,
        OrderState = OrderStates.Failed,
        Error = ex
    });
}
```

### 3. Message Conversion

**Key Principles**:
- Always set `OriginalTransactionId` for response messages
- Use `ServerTime` from exchange when available
- Populate all relevant fields (don't leave null if data available)
- Handle partial data gracefully
- Preserve precision (use `decimal` for prices/volumes)

**Common Conversions**:

Tick Data:
```csharp
SendOutMessage(new ExecutionMessage
{
    DataTypeEx = DataType.Ticks,
    SecurityId = symbol.ToStockSharp(),
    TradeId = trade.Id,
    TradePrice = trade.Price,
    TradeVolume = trade.Amount,
    ServerTime = trade.Time,
    OriginSide = trade.Side.ToStockSharp(),
    OriginalTransactionId = mdMsg.TransactionId
});
```

Order Book:
```csharp
SendOutMessage(new QuoteChangeMessage
{
    SecurityId = symbol.ToStockSharp(),
    Bids = orderBook.Bids.Select(e => new QuoteChange(e.Price, e.Size)).ToArray(),
    Asks = orderBook.Asks.Select(e => new QuoteChange(e.Price, e.Size)).ToArray(),
    ServerTime = orderBook.Timestamp,
});
```

### 4. Subscription Handling

**Standard Flow**:
1. Send subscription reply: `SendSubscriptionReply(mdMsg.TransactionId)`
2. Process historical data if `mdMsg.From`/`mdMsg.To` specified
3. Subscribe to real-time if `!mdMsg.IsHistoryOnly()`
4. Send result: `SendSubscriptionResult(mdMsg)` or `SendSubscriptionFinished(mdMsg.TransactionId)`

**Historical Data Pattern**:
```csharp
if (mdMsg.From is not null || mdMsg.To is not null)
{
    var from = mdMsg.From?.UtcDateTime ?? DateTime.Today;
    var to = mdMsg.To?.UtcDateTime ?? DateTime.UtcNow;
    var left = mdMsg.Count ?? long.MaxValue;

    var trades = await _httpClient.GetTrades(symbol, from, to, cancellationToken);

    foreach (var trade in trades.OrderBy(t => t.Time))
    {
        if (trade.Time < from) continue;
        if (trade.Time > to) break;

        SendOutMessage(ConvertTrade(trade, mdMsg.TransactionId));

        if (--left <= 0) break;
    }
}
```

### 5. Order Management

**Order Tracking**:
```csharp
private readonly Dictionary<long, RefPair<long, decimal>> _orderInfo = new();

// On order registration
_orderInfo.Add(exchangeOrderId, RefTuple.Create(transactionId, orderVolume));

// On execution
var info = _orderInfo.TryGetValue(exchangeOrderId);
info.Second -= executedVolume; // Update remaining balance

// On complete fill
if (info.Second == 0)
    _orderInfo.Remove(exchangeOrderId);
```

**Execution Updates**:
```csharp
// Trade execution
SendOutMessage(new ExecutionMessage
{
    DataTypeEx = DataType.Transactions,
    OrderId = exchangeOrderId,
    TradeId = executionId,
    TradePrice = price,
    TradeVolume = volume,
    OriginalTransactionId = transactionId,
    // ... other fields
});

// Order state update
SendOutMessage(new ExecutionMessage
{
    DataTypeEx = DataType.Transactions,
    OrderId = exchangeOrderId,
    Balance = remainingVolume,
    OrderState = remainingVolume > 0 ? OrderStates.Active : OrderStates.Done,
    HasOrderInfo = true,
    OriginalTransactionId = transactionId,
    // ... other fields
});
```

### 6. Testing Strategy

**Unit Tests**:
- Test message conversions (native ↔ StockSharp)
- Test authentication signature generation
- Test error handling
- Test subscription management

**Integration Tests**:
- Test connection/disconnection
- Test market data subscriptions
- Test order placement/cancellation
- Test portfolio updates

**Manual Testing Checklist**:
- [ ] Connect with valid credentials
- [ ] Connect with invalid credentials (should fail gracefully)
- [ ] Subscribe to ticks
- [ ] Subscribe to order book
- [ ] Subscribe to historical candles
- [ ] Place market order
- [ ] Place limit order
- [ ] Cancel order
- [ ] Query portfolio
- [ ] Reconnect after disconnect

## Common Pitfalls

### 1. Thread Safety
**Problem**: Concurrent access to native clients
**Solution**: Use locks around client operations

### 2. Memory Leaks
**Problem**: Event handlers not unsubscribed
**Solution**: Unsubscribe in `DisconnectAsync` and `DisposeManaged`

### 3. Transaction ID Mismatches
**Problem**: Response messages without `OriginalTransactionId`
**Solution**: Always include transaction ID in responses

### 4. Incomplete Disconnection
**Problem**: Resources not cleaned up
**Solution**: Implement both `DisconnectAsync` and `ResetAsync` properly

### 5. Missing Error Handling
**Problem**: Unhandled exceptions crash adapter
**Solution**: Wrap async operations in try-catch

### 6. Precision Loss
**Problem**: Using `float` or `double` for financial values
**Solution**: Always use `decimal` for prices and volumes

## Dependencies and References

### Core StockSharp Projects
- `BusinessEntities` - Core trading entities
- `Messages` - Message definitions
- `Algo` - Adapter base classes
- `Media.Names` - Resource management

### Common External Libraries
- `Ecng.Common` - Core utilities
- `Ecng.Collections` - Collection extensions
- `Ecng.Net` - Network utilities
- `Newtonsoft.Json` - JSON serialization
- `RestSharp` (or similar) - HTTP client
- WebSocket library (various options)

### Target Frameworks
- netstandard2.0/2.1
- net6.0-windows (for desktop features)

## Version Control and Deployment

### Assembly Versioning
```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
```

### NuGet Packaging
- Follow StockSharp naming: `StockSharp.ConnectorName`
- Include XML documentation
- Mark as private if not for public release
- Tag appropriately (crypto, forex, stocks, etc.)

## Documentation Requirements

### Code Documentation
- XML comments on all public types and members
- Include `<summary>` for classes and methods
- Use `<param>` for parameters
- Add `<returns>` for return values
- Reference related topics with `<seealso>`

### External Documentation
- Create connector guide in `/docs`
- Include authentication setup steps
- Document supported features
- Provide code examples
- List known limitations

## Conclusion

This specification provides a comprehensive blueprint for developing StockSharp connectors. By following these patterns and guidelines, you can create robust, maintainable connectors that integrate seamlessly with the StockSharp ecosystem.

For questions or clarifications, refer to existing connector implementations (BitStamp, Binance, Interactive Brokers) or consult the StockSharp documentation.
