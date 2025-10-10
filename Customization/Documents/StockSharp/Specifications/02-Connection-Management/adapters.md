# MessageAdapter and BasketMessageAdapter Documentation

## Overview

MessageAdapters are the core components that translate StockSharp messages to specific trading system APIs and vice versa. StockSharp uses an adapter pattern to support 60+ exchanges and brokers through a unified interface.

Key concepts:
- **MessageAdapter** - Base class for single connection adapters
- **BasketMessageAdapter** - Aggregator managing multiple adapters simultaneously
- **InnerAdapters** - Collection of adapters in the basket
- Message routing and transformation between StockSharp format and native API

**File Locations:**
- `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\MessageAdapter.cs`
- `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\BasketMessageAdapter.cs`

---

## MessageAdapter (Base Class)

### Overview

The abstract `MessageAdapter` class defines the contract for all trading system adapters. Each exchange/broker implementation inherits from this class.

```csharp
public abstract class MessageAdapter : BaseLogReceiver, IMessageAdapter, INotifyPropertyChanged
```

### Constructor

```csharp
protected MessageAdapter(IdGenerator transactionIdGenerator)
```

**Parameters:**
- `transactionIdGenerator` - Transaction ID generator for unique message identification

### Core Properties

#### Identification

```csharp
// Unique adapter identifier
Guid Id { get; set; }

// Adapter name (customizable)
string Name { get; set; }

// Storage name (derived from namespace)
string StorageName { get; }

// Platform compatibility
Platforms Platform { get; protected set; }

// Adapter categories (Stock, Crypto, Forex, etc.)
MessageAdapterCategories Categories { get; }

// Feature name
string FeatureName { get; }

// Icon URI
Uri Icon { get; }
```

#### Supported Messages

```csharp
// Incoming message types this adapter supports
IEnumerable<MessageTypes> SupportedInMessages { get; set; }

// Possible supported messages with additional info
IEnumerable<MessageTypeInfo> PossibleSupportedMessages { get; set; }

// Result message types NOT supported
IEnumerable<MessageTypes> NotSupportedResultMessages { get; set; }
```

**Example:**

```csharp
// Check if adapter supports order registration
if (adapter.SupportedInMessages.Contains(MessageTypes.OrderRegister))
{
    // Can register orders through this adapter
}
```

#### Market Data Support

```csharp
// Get supported market data types for a security
IEnumerable<DataType> GetSupportedMarketDataTypes(
    SecurityId securityId,
    DateTimeOffset? from,
    DateTimeOffset? to);

// Fields for building candles
IEnumerable<Level1Fields> CandlesBuildFrom { get; }

// Check timeframe on request
bool CheckTimeFrameByRequest { get; }

// Only full (completed) candles
bool IsFullCandlesOnly { get; }

// Supports subscriptions
bool IsSupportSubscriptions { get; }

// Supports candle updates
bool IsSupportCandlesUpdates(MarketDataMessage subscription);

// Supports candle price levels
bool IsSupportCandlesPriceLevels(MarketDataMessage subscription);

// Supports partial downloading
bool IsSupportPartialDownloading { get; }

// Supported order book depths
IEnumerable<int> SupportedOrderBookDepths { get; }

// Supports order book increments
bool IsSupportOrderBookIncrements { get; }

// Supports execution P&L
bool IsSupportExecutionsPnL { get; }

// Security-specific news only
bool IsSecurityNewsOnly { get; }
```

#### Connection Settings

```csharp
// Reconnection settings
ReConnectionSettings ReConnectionSettings { get; }

// Heartbeat interval
TimeSpan HeartbeatInterval { get; set; }

// Heartbeat before connect
bool HeartbeatBeforConnect { get; }

// Iteration interval for historical data
TimeSpan IterationInterval { get; set; }

// Lookup timeout
TimeSpan? LookupTimeout { get; }
```

#### Order Management

```csharp
// Order condition type
Type OrderConditionType { get; }

// Replace command edits current order
bool IsReplaceCommandEditCurrent { get; }

// Auto-reply on transactional unsubscription
bool IsAutoReplyOnTransactonalUnsubscription { get; }

// Transaction log support
bool IsSupportTransactionLog { get; }
```

#### Identifiers

```csharp
// Native identifiers are persistable
bool IsNativeIdentifiersPersistable { get; }

// Uses native identifiers
bool IsNativeIdentifiers { get; }

// Associated board codes
string[] AssociatedBoards { get; }
```

#### Extended Features

```csharp
// Extended security fields
IEnumerable<Tuple<string, Type>> SecurityExtendedFields { get; }

// Extra setup required
bool ExtraSetup { get; }

// Enqueue subscriptions
bool EnqueueSubscriptions { get; set; }

// Position emulation required
bool? IsPositionsEmulationRequired { get; }

// Transaction ID generator
IdGenerator TransactionIdGenerator { get; set; }
```

### Core Methods

#### Message Processing

```csharp
// Send message to adapter
bool SendInMessage(Message message);

// Override to process incoming messages
protected abstract bool OnSendInMessage(Message message);

// Send outgoing message
protected internal virtual void SendOutMessage(Message message);

// Event raised when message sent out
event Action<Message> NewOutMessage;
```

#### Specialized Send Methods

```csharp
// Send disconnect message
protected void SendOutDisconnectMessage(bool expected);
protected void SendOutDisconnectMessage(Exception error);

// Send connection state
protected void SendOutConnectionState(ConnectionStates state);

// Send error message
protected void SendOutError(string description);
protected void SendOutError(Exception error);

// Send subscription responses
protected void SendSubscriptionReply(long originalTransactionId, Exception error = null);
protected void SendSubscriptionNotSupported(long originalTransactionId);
protected void SendSubscriptionFinished(long originalTransactionId, DateTimeOffset? nextFrom = null);
protected void SendSubscriptionOnline(long originalTransactionId);
protected void SendSubscriptionResult(ISubscriptionMessage message);
```

#### Historical Data

```csharp
// Get history step size
TimeSpan GetHistoryStepSize(
    SecurityId securityId,
    DataType dataType,
    out TimeSpan iterationInterval);

// Get max count for data type
int? GetMaxCount(DataType dataType);

// All downloading supported
bool IsAllDownloadingSupported(DataType dataType);

// Security required for data type
bool IsSecurityRequired(DataType dataType);
```

#### Order Book Builder

```csharp
// Create order log to market depth builder
IOrderLogMarketDepthBuilder CreateOrderLogMarketDepthBuilder(SecurityId securityId);
```

### Channel Configuration

```csharp
// Use input channel
bool UseInChannel { get; }

// Use output channel
bool UseOutChannel { get; }
```

### Persistence

```csharp
// Load adapter settings
public override void Load(SettingsStorage storage);

// Save adapter settings
public override void Save(SettingsStorage storage);

// Clone adapter
public virtual IMessageChannel Clone();
```

---

## BasketMessageAdapter

### Overview

`BasketMessageAdapter` is an adapter aggregator that allows operating multiple adapters simultaneously. It routes messages to appropriate adapters and aggregates their responses.

```csharp
public class BasketMessageAdapter : BaseLogReceiver, IMessageAdapter
```

**Key Features:**
- Manages multiple adapters through `InnerAdapters` collection
- Routes messages based on message type, security, portfolio
- Aggregates connection states
- Handles subscription fan-out to multiple adapters
- Wraps adapters with additional functionality (heartbeat, channels, storage, etc.)

### Constructor

```csharp
public BasketMessageAdapter(
    IdGenerator transactionIdGenerator,
    CandleBuilderProvider candleBuilderProvider,
    ISecurityMessageAdapterProvider securityAdapterProvider,
    IPortfolioMessageAdapterProvider portfolioAdapterProvider,
    StorageBuffer buffer)
```

**Parameters:**
- `transactionIdGenerator` - Transaction ID generator
- `candleBuilderProvider` - Candle builders provider
- `securityAdapterProvider` - Security-based adapter provider
- `portfolioAdapterProvider` - Portfolio-based adapter provider
- `buffer` - Storage buffer (optional)

### InnerAdapters Collection

The `InnerAdapters` property provides access to the collection of managed adapters with prioritization:

```csharp
public interface IInnerAdapterList :
    ISynchronizedCollection<IMessageAdapter>,
    INotifyList<IMessageAdapter>
{
    // Adapters sorted by speed priority
    IEnumerable<IMessageAdapter> SortedAdapters { get; }

    // Set adapter priority (lower = faster, -1 = disabled)
    int this[IMessageAdapter adapter] { get; set; }
}

// Access inner adapters
public IInnerAdapterList InnerAdapters { get; }
```

**Example:**

```csharp
var basket = connector.Adapter;

// Add adapter
var binanceAdapter = new BinanceMessageAdapter(transactionIdGenerator);
basket.InnerAdapters.Add(binanceAdapter);

// Set priority (0 = highest priority)
basket.InnerAdapters[binanceAdapter] = 0;

// Disable adapter
basket.InnerAdapters[binanceAdapter] = -1;

// Iterate sorted adapters
foreach (var adapter in basket.InnerAdapters.SortedAdapters)
{
    Console.WriteLine($"{adapter.Name}: Priority {basket.InnerAdapters[adapter]}");
}
```

### Adapter Providers

#### Security-Based Routing

```csharp
// Maps securities to specific adapters
public ISecurityMessageAdapterProvider SecurityAdapterProvider { get; }

// Try to find adapter by portfolio
public bool TryGetAdapter(string portfolioName, out IMessageAdapter adapter);
```

#### Portfolio-Based Routing

```csharp
// Maps portfolios to specific adapters
public IPortfolioMessageAdapterProvider PortfolioAdapterProvider { get; }
```

### Wrapper Adapters

The basket automatically wraps each adapter with additional functionality:

```csharp
// Supported wrappers (configurable via properties):

// Heartbeat monitoring
public bool SuppressReconnectingErrors { get; set; } = true;

// Candle compression
public bool SupportCandlesCompression { get; set; } = true;
public bool SendFinishedCandlesImmediatelly { get; set; }

// Level1 extension
public bool Level1Extend { get; set; }

// Order log processing
public bool SupportBuildingFromOrderLog { get; set; } = true;

// Storage integration
public bool SupportStorage { get; set; } = true;

// Order book truncation
public bool SupportOrderBookTruncate { get; set; } = true;

// Partial download
public bool SupportPartialDownload { get; set; } = true;

// Lookup tracking
public bool SupportLookupTracking { get; set; } = true;

// Offline mode
public bool SupportOffline { get; set; }

// Security "All" support
public bool SupportSecurityAll { get; set; } = true;

// Transaction ordering
public bool IsSupportTransactionLog { get; set; } = true;

// Ignore extra adapters
public bool IgnoreExtraAdapters { get; set; }

// Order book from Level1
public bool GenerateOrderBookFromLevel1 { get; set; } = true;
```

### Connection Management

#### Connection Behavior

```csharp
// Raise Connect/Disconnect event on first adapter
public bool ConnectDisconnectEventOnFirstAdapter { get; set; } = true;

// Restore subscriptions on error reconnect
public bool IsRestoreSubscriptionOnErrorReconnect { get; set; } = true;
```

### Managers

```csharp
// Latency calculation
public ILatencyManager LatencyManager { get; set; }

// P&L calculation
public IPnLManager PnLManager { get; set; }

// Commission calculation
public ICommissionManager CommissionManager { get; set; }

// Slippage tracking
public ISlippageManager SlippageManager { get; set; }
```

### Storage

```csharp
// Storage settings
public StorageCoreSettings StorageSettings { get; }

// Storage processor
public StorageProcessor StorageProcessor { get; }

// Storage buffer
public StorageBuffer Buffer { get; }

// Native ID storage
public INativeIdStorage NativeIdStorage { get; set; }

// Security mapping storage
public ISecurityMappingStorage SecurityMappingStorage { get; set; }

// Extended info storage
public IExtendedInfoStorage ExtendedInfoStorage { get; set; }
```

### Transaction ID Generator

```csharp
// Transaction ID generator for all adapters
public IdGenerator TransactionIdGenerator { get; set; }
```

### Channel Configuration

```csharp
// Use input channel
public bool UseInChannel { get; set; } = true;

// Use output channel
public bool UseOutChannel { get; set; } = true;
```

### Heartbeat Control

```csharp
// Apply heartbeat on/off for specific adapter
public void ApplyHeartbeat(IMessageAdapter adapter, bool on);
```

### Message Routing

The basket routes messages intelligently:

1. **By Adapter Property** - If message has `Adapter` set, routes to that adapter
2. **By Security** - For market data, routes based on security-to-adapter mapping
3. **By Portfolio** - For orders, routes based on portfolio-to-adapter mapping
4. **By Message Type** - Broadcasts to all adapters supporting the message type
5. **By Priority** - For subscriptions, uses first adapter by priority

---

## Supported Exchanges (60+)

StockSharp supports numerous exchanges through dedicated adapters:

### Cryptocurrency Exchanges
- Binance, Coinbase, Kraken, Bitfinex, Huobi, OKX, Bybit, KuCoin, Gate.io, Bitget, MEXC, HTX

### Stock & Options
- Interactive Brokers, TD Ameritrade, E*TRADE, Charles Schwab, Alpaca, Robinhood

### Forex & CFD
- OANDA, FXCM, IG, SAXO Bank, Dukascopy

### Russian Markets
- MOEX (Moscow Exchange), St. Petersburg Exchange

### Futures & Commodities
- CME, ICE, Eurex

### Aggregators
- CQG, Rithmic, dxFeed, LMAX

### Proprietary Trading
- Sterling, RTrader, Transaq, QUIK, SmartCOM, Alveo (Currenex)

### FIX Protocol
- Generic FIX adapter (FIX 4.2, 4.4, 5.0)

### Emulation
- Paper trading adapter
- Backtesting adapter (Historical Message Adapter)

---

## Creating Custom Adapters

### Basic Adapter Structure

```csharp
[MessageAdapterCategory(MessageAdapterCategories.Crypto)]
public class MyExchangeAdapter : MessageAdapter
{
    public MyExchangeAdapter(IdGenerator transactionIdGenerator)
        : base(transactionIdGenerator)
    {
        // Set supported messages
        SupportedInMessages = new[]
        {
            MessageTypes.Connect,
            MessageTypes.Disconnect,
            MessageTypes.MarketData,
            MessageTypes.OrderRegister,
            MessageTypes.OrderCancel,
        };

        // Set supported market data types
        this.AddSupportedMarketDataType(DataType.Ticks);
        this.AddSupportedMarketDataType(DataType.MarketDepth);
        this.AddSupportedMarketDataType(DataType.Level1);

        // Set associated boards
        Platform = Platforms.AnyCPU;
    }

    public override string[] AssociatedBoards => new[] { "MYEXCHANGE" };

    protected override bool OnSendInMessage(Message message)
    {
        switch (message.Type)
        {
            case MessageTypes.Connect:
                // Handle connect
                ProcessConnect((ConnectMessage)message);
                return true;

            case MessageTypes.Disconnect:
                // Handle disconnect
                ProcessDisconnect((DisconnectMessage)message);
                return true;

            case MessageTypes.MarketData:
                // Handle market data subscription
                ProcessMarketData((MarketDataMessage)message);
                return true;

            case MessageTypes.OrderRegister:
                // Handle order registration
                ProcessOrderRegister((OrderRegisterMessage)message);
                return true;

            case MessageTypes.OrderCancel:
                // Handle order cancellation
                ProcessOrderCancel((OrderCancelMessage)message);
                return true;

            default:
                return false;
        }
    }

    private void ProcessConnect(ConnectMessage message)
    {
        // Connect to exchange API
        try
        {
            // ... connection logic ...

            SendOutMessage(new ConnectMessage());
        }
        catch (Exception ex)
        {
            SendOutMessage(new ConnectMessage { Error = ex });
        }
    }

    private void ProcessDisconnect(DisconnectMessage message)
    {
        // Disconnect from exchange
        // ... disconnection logic ...

        SendOutMessage(new DisconnectMessage());
    }

    private void ProcessMarketData(MarketDataMessage message)
    {
        if (message.IsSubscribe)
        {
            // Subscribe to market data
            // ... subscription logic ...

            SendSubscriptionReply(message.TransactionId);
        }
        else
        {
            // Unsubscribe
            // ... unsubscribe logic ...

            SendSubscriptionReply(message.TransactionId);
        }
    }

    private void ProcessOrderRegister(OrderRegisterMessage message)
    {
        // Send order to exchange
        // ... order registration logic ...

        // Send execution report
        SendOutMessage(new ExecutionMessage
        {
            DataTypeEx = DataType.Transactions,
            OriginalTransactionId = message.TransactionId,
            OrderState = OrderStates.Active,
            // ... other fields ...
        });
    }

    private void ProcessOrderCancel(OrderCancelMessage message)
    {
        // Cancel order on exchange
        // ... cancellation logic ...

        // Send execution report
        SendOutMessage(new ExecutionMessage
        {
            DataTypeEx = DataType.Transactions,
            OriginalTransactionId = message.TransactionId,
            OrderState = OrderStates.Done,
            // ... other fields ...
        });
    }
}
```

### Registering Custom Adapter

```csharp
var connector = new Connector();

// Add custom adapter to basket
var myAdapter = new MyExchangeAdapter(connector.TransactionIdGenerator);
connector.Adapter.InnerAdapters.Add(myAdapter);

// Configure adapter settings
// myAdapter.SomeProperty = value;

// Save configuration
connector.Save().Serialize("config.json");
```

---

## Adapter Configuration Example

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:27-28`

```csharp
// Register all available connectors
ConfigManager.RegisterService<IMessageAdapterProvider>(
    new InMemoryMessageAdapterProvider(_connector.Adapter.InnerAdapters));

// Show configuration dialog (from StockSharp.Xaml)
if (_connector.Configure(this))
{
    _connector.Save().Serialize(_connectorFile);
}
```

---

## Message Flow

### Incoming Messages (User → Adapter)

```
User Code
  ↓
Connector.SendInMessage()
  ↓
BasketMessageAdapter.OnSendInMessage()
  ↓ (routes to appropriate adapter)
MessageAdapter.SendInMessage()
  ↓
MessageAdapter.OnSendInMessage()
  ↓
Trading System API
```

### Outgoing Messages (Adapter → User)

```
Trading System API
  ↓
MessageAdapter.SendOutMessage()
  ↓
MessageAdapter.NewOutMessage event
  ↓
BasketMessageAdapter.OnInnerAdapterNewOutMessage()
  ↓
BasketMessageAdapter.SendOutMessage()
  ↓
Connector.OnOutMessage()
  ↓
Connector events (Connected, NewOrder, etc.)
  ↓
User Code
```

---

## Common Adapter Patterns

### Check Adapter Capabilities

```csharp
var adapter = connector.Adapter.InnerAdapters.First();

// Check supported messages
if (adapter.IsMessageSupported(MessageTypes.OrderReplace))
{
    // Can modify orders
}

// Check market data support
if (adapter.IsMarketDataTypeSupported(DataType.MarketDepth))
{
    // Can subscribe to order book
}

// Check candle support
if (adapter.IsCandlesSupported(DataType.TimeFrame(TimeSpan.FromMinutes(1))))
{
    // Can get 1-minute candles
}
```

### Get Adapter for Security/Portfolio

```csharp
// Get adapter by portfolio
if (connector.Adapter.TryGetAdapter("MyPortfolio", out var adapter))
{
    Console.WriteLine($"Portfolio adapter: {adapter.Name}");
}
```

### Heartbeat Control

```csharp
var adapter = connector.Adapter.InnerAdapters.First();

// Disable heartbeat for specific adapter
connector.Adapter.ApplyHeartbeat(adapter, false);

// Re-enable heartbeat
connector.Adapter.ApplyHeartbeat(adapter, true);
```

---

## See Also

- **[connector.md](connector.md)** - Connector class documentation
- **[configuration.md](configuration.md)** - Adapter persistence and configuration
- **[subscription-management.md](subscription-management.md)** - Subscription routing
- **MessageAdapter Source** - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\MessageAdapter.cs`
- **BasketMessageAdapter Source** - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\BasketMessageAdapter.cs`
- **Connector Samples** - `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\`
