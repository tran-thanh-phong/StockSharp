# Connector Class Documentation

## Overview

The `Connector` class is the primary interface for creating connections to trading systems in StockSharp. It implements the `IConnector` interface and provides a unified API for connecting to multiple exchanges, managing market data subscriptions, executing trades, and handling portfolios and positions.

The connector acts as a central hub that:
- Manages connection lifecycle (connect/disconnect)
- Provides access to securities, portfolios, and positions through entity providers
- Handles market data and transaction subscriptions
- Processes incoming messages and raises corresponding events
- Persists configuration settings

**File Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs`

---

## Key Interfaces

The Connector implements multiple interfaces:
- `IConnector` - Main trading system connection interface
- `IMessageChannel` - Message processing channel
- `IPersistable` - Configuration persistence (Load/Save)
- `ILogReceiver` - Logging support
- `IMarketDataProvider` - Market data access
- `ITransactionProvider` - Order and trade operations
- `ISecurityProvider` - Security management
- `ISubscriptionProvider` - Subscription management
- `IPortfolioProvider` - Portfolio access
- `IPositionProvider` - Position access

---

## Constructor

### Basic Constructor

```csharp
public Connector()
```

Creates a connector with default in-memory storages:
- `InMemorySecurityStorage` - for securities metadata
- `InMemoryPositionStorage` - for positions
- `InMemoryExchangeInfoProvider` - for exchange/board information

### Advanced Constructor

```csharp
public Connector(
    ISecurityStorage securityStorage,
    IPositionStorage positionStorage,
    IExchangeInfoProvider exchangeInfoProvider,
    IStorageRegistry storageRegistry = null,
    SnapshotRegistry snapshotRegistry = null,
    StorageBuffer buffer = null,
    bool initAdapter = true,
    bool initChannels = true)
```

**Parameters:**
- `securityStorage` - Securities metadata storage
- `positionStorage` - Position storage
- `exchangeInfoProvider` - Exchange and trading board provider
- `storageRegistry` - Market data storage (optional)
- `snapshotRegistry` - Snapshot storage registry (optional)
- `buffer` - Storage buffer (optional)
- `initAdapter` - Initialize basket adapter (default: true)
- `initChannels` - Initialize message channels (default: true)

---

## Connection Lifecycle

### Connection States

The connector maintains a `ConnectionState` property that tracks the current state:

```csharp
public enum ConnectionStates
{
    Disconnected,  // Not connected
    Connecting,    // Connection in progress
    Connected,     // Successfully connected
    Disconnecting, // Disconnection in progress
    Failed        // Connection failed
}
```

### Connect Method

```csharp
public void Connect()
```

Initiates connection to configured trading systems.

**Behavior:**
- Validates current state (must be Disconnected or Failed)
- Sets state to `Connecting`
- Calls `OnConnect()` virtual method
- Starts time message timer if `TimeChange` is enabled
- Sends `ConnectMessage` to adapter

**Example:**

```csharp
var connector = new Connector();

// Configure adapter (see configuration.md)
// ...

// Subscribe to events
connector.Connected += () => Console.WriteLine("Connected!");
connector.ConnectionError += ex => Console.WriteLine($"Error: {ex.Message}");

// Connect
connector.Connect();
```

### Disconnect Method

```csharp
public void Disconnect()
```

Initiates disconnection from trading systems.

**Behavior:**
- Validates current state (must be Connected)
- Sets state to `Disconnecting`
- Unsubscribes all subscriptions if `IsAutoUnSubscribeOnDisconnect` is true
- Calls `OnDisconnect()` virtual method
- Sends `DisconnectMessage` to adapter

**Example:**

```csharp
connector.Disconnect();
```

---

## Connection Events

### Core Events

```csharp
// Raised when connection is established
event Action Connected;

// Raised when disconnected
event Action Disconnected;

// Raised on connection errors
event Action<Exception> ConnectionError;

// Extended events per adapter
event Action<IMessageAdapter> ConnectedEx;
event Action<IMessageAdapter> DisconnectedEx;
event Action<IMessageAdapter, Exception> ConnectionErrorEx;

// Connection state changes
event Action<IMessageAdapter> ConnectionLost;
event Action<IMessageAdapter> ConnectionRestored;
```

### Event Usage Example

```csharp
var connector = new Connector();

connector.Connected += () =>
{
    Console.WriteLine("Connected to trading system");

    // Subscribe to market data after connection
    var security = connector.Securities.First();
    connector.Subscribe(new Subscription(DataType.Level1, security));
};

connector.Disconnected += () =>
{
    Console.WriteLine("Disconnected from trading system");
};

connector.ConnectionError += ex =>
{
    Console.WriteLine($"Connection error: {ex.Message}");
    // Implement reconnection logic here
};

connector.Connect();
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:48-50`

```csharp
_connector.Connected += Connector_Connected;
_connector.Connect();

private void Connector_Connected()
{
    // try lookup all securities
    _connector.Subscribe(new(StockSharp.Messages.Extensions.LookupAllCriteriaMessage));
}
```

---

## Entity Providers

The Connector provides access to trading entities through provider interfaces:

### Securities Provider

```csharp
// All loaded securities
IEnumerable<Security> Securities { get; }

// Get security by ID
Security GetSecurity(SecurityId securityId);

// Lookup securities
Security LookupById(SecurityId id);

// Security storage
ISecurityStorage SecurityStorage { get; }

// Events
event Action<IEnumerable<Security>> Added;    // ISecurityProvider.Added
event Action<IEnumerable<Security>> Removed;  // ISecurityProvider.Removed
event Action Cleared;                          // ISecurityProvider.Cleared
```

### Portfolios Provider

```csharp
// All portfolios
IEnumerable<Portfolio> Portfolios { get; }

// Events (see IPortfolioProvider)
event Action<Portfolio> NewPortfolio;      // [Obsolete] Use PortfolioReceived
event Action<Portfolio> PortfolioChanged;  // [Obsolete] Use PortfolioReceived
event Action<Subscription, Portfolio> PortfolioReceived;
```

### Positions Provider

```csharp
// All positions
IEnumerable<Position> Positions { get; }

// Get or create position
Position GetPosition(
    Portfolio portfolio,
    Security security,
    string strategyId = "",
    Sides? side = null,
    string clientCode = "",
    string depoName = "",
    TPlusLimits? limitType = null);

// Position storage
IPositionStorage PositionStorage { get; }

// Events
event Action<Position> NewPosition;       // [Obsolete] Use PositionReceived
event Action<Position> PositionChanged;   // [Obsolete] Use PositionReceived
event Action<Subscription, Position> PositionReceived;
```

### Exchange Info Provider

```csharp
// All exchange boards
IEnumerable<ExchangeBoard> ExchangeBoards { get; }

// Exchange info provider
IExchangeInfoProvider ExchangeInfoProvider { get; }

// Get session state (obsolete)
[Obsolete("Use BoardReceived event.")]
SessionStates? GetSessionState(ExchangeBoard board);
```

---

## Configuration Properties

### Security Updates

```csharp
// Update Security.LastTick, BestBid, BestAsk on order book/trade updates
// Default: true
public bool UpdateSecurityLastQuotes { get; set; } = true;

// Update Security fields from Level1ChangeMessage
// Default: true
public bool UpdateSecurityByLevel1 { get; set; } = true;

// Update Security fields from SecurityMessage
// Default: true
public bool UpdateSecurityByDefinition { get; set; } = true;
```

### Portfolio Updates

```csharp
// Update Portfolio fields from PositionChangeMessage
// Default: true
public bool UpdatePortfolioByChange { get; set; } = true;
```

### Order Validation

```csharp
// Check that Order.Price and Order.Volume are multiples of price/volume steps
// Default: false
public bool CheckSteps { get; set; }
```

**Example:**

```csharp
var connector = new Connector
{
    CheckSteps = true,  // Validate order prices/volumes
    UpdateSecurityByLevel1 = true,  // Update security from level1 data
    UpdateSecurityLastQuotes = true // Update best bid/ask
};
```

### Order Management

```csharp
// Number of orders to keep in memory
// Default: 1000
// int.MaxValue = keep all, 0 = don't store
public int OrdersKeepCount { get; set; } = 1000;
```

### Reconnection Settings

```csharp
// Restore subscriptions on normal reconnect (user-initiated disconnect/connect)
// Default: true
public bool IsRestoreSubscriptionOnNormalReconnect { get; set; } = true;

// Automatically unsubscribe all on disconnect
// Default: true
public bool IsAutoUnSubscribeOnDisconnect { get; set; } = true;
```

### Market Time

```csharp
// Time message interval
// Default: 10ms
public TimeSpan MarketTimeChangedInterval { get; set; } = TimeSpan.FromMilliseconds(10);

// Enable time increment
// Default: true
public bool TimeChange { get; set; } = true;

// Time change event
event Action<TimeSpan> CurrentTimeChanged;
```

---

## Subscriptions On Connect

Configure automatic subscriptions that are sent immediately after connection:

```csharp
// Collection of subscriptions to send on connect
ISet<Subscription> SubscriptionsOnConnect { get; }

// Predefined lookup subscriptions
Subscription SecurityLookup { get; }  // Subscribe to security lookups
Subscription BoardLookup { get; }     // Subscribe to board lookups
Subscription DataTypeLookup { get; }  // Subscribe to data type lookups
Subscription PortfolioLookup { get; } // Subscribe to portfolio lookups
Subscription OrderLookup { get; }     // Subscribe to order status
```

**Default Configuration:**

By default, the connector adds these subscriptions:
- `SecurityLookup`
- `PortfolioLookup`
- `OrderLookup`

**Example:**

```csharp
var connector = new Connector();

// Add custom subscription on connect
connector.SubscriptionsOnConnect.Add(
    new Subscription(DataType.Level1, new Security { Id = "AAPL@NASDAQ" })
);

// Remove default subscriptions if not needed
connector.SubscriptionsOnConnect.Clear();

// Add only what you need
connector.SubscriptionsOnConnect.Add(connector.SecurityLookup);
```

---

## Adapter Access

```csharp
// Basket message adapter containing all configured adapters
public BasketMessageAdapter Adapter { get; }

// Transaction ID generator
public IdGenerator TransactionIdGenerator { get; set; }

// Security ID generator
public SecurityIdGenerator SecurityIdGenerator { get; set; }
```

---

## Risk Management

```csharp
// Risk control manager
public IRiskManager RiskManager { get; set; } = new RiskManager();

// Latency calculation manager
public ILatencyManager LatencyManager { get; set; }

// P&L manager
public IPnLManager PnLManager { get; set; }

// Commission calculation manager
public ICommissionManager CommissionManager { get; set; }

// Slippage manager
public ISlippageManager SlippageManager { get; set; }
```

---

## Storage

```csharp
// Market data storage registry
public IStorageRegistry StorageRegistry { get; }

// Snapshot storage registry
public SnapshotRegistry SnapshotRegistry { get; }

// Storage buffer
public StorageBuffer Buffer { get; }

// Override security data with new values
public bool OverrideSecurityData { get; set; }
```

---

## Error Handling

```csharp
// Number of errors through Error event
public int ErrorCount { get; private set; }

// Data processing error event
event Action<Exception> Error;
```

**Example:**

```csharp
connector.Error += ex =>
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Total errors: {connector.ErrorCount}");
};
```

---

## Complete Example from MainWindow.xaml.cs

**File**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs`

```csharp
public partial class MainWindow
{
    private readonly Connector _connector = new();
    private const string _connectorFile = "ConnectorFile.json";

    public MainWindow()
    {
        InitializeComponent();

        // Register all available connectors
        ConfigManager.RegisterService<IMessageAdapterProvider>(
            new InMemoryMessageAdapterProvider(_connector.Adapter.InnerAdapters));

        // Load saved configuration
        if (File.Exists(_connectorFile))
        {
            _connector.Load(_connectorFile.Deserialize<SettingsStorage>());
        }
    }

    private void Setting_Click(object sender, RoutedEventArgs e)
    {
        // Show configuration dialog
        if (_connector.Configure(this))
        {
            // Save configuration
            _connector.Save().Serialize(_connectorFile);
        }
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        // Bind connector to UI controls
        SecurityPicker.SecurityProvider = _connector;
        SecurityPicker.MarketDataProvider = _connector;

        // Subscribe to connected event
        _connector.Connected += Connector_Connected;

        // Connect
        _connector.Connect();
    }

    private void Connector_Connected()
    {
        // Lookup all securities after connection
        _connector.Subscribe(
            new Subscription(StockSharp.Messages.Extensions.LookupAllCriteriaMessage)
        );
    }

    private void SecurityPicker_SecuritySelected(Security security)
    {
        if (security == null) return;

        // Subscribe to Level1 data for selected security
        _connector.Subscribe(new Subscription(DataType.Level1, security));
    }
}
```

---

## Disposal

```csharp
protected override void DisposeManaged()
```

The connector implements `IDisposable`. On disposal:
1. Automatically disconnects if connected
2. Stops time timer
3. Cleans up resources
4. Sends `ResetMessage` to adapters

**Usage:**

```csharp
using (var connector = new Connector())
{
    // Use connector
    connector.Connect();
    // ...
} // Automatically disposed and disconnected
```

---

## See Also

- **[adapters.md](adapters.md)** - MessageAdapter and BasketMessageAdapter documentation
- **[configuration.md](configuration.md)** - Configuration persistence (Load/Save)
- **[subscription-management.md](subscription-management.md)** - Subscription lifecycle and management
- **IConnector Interface** - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\IConnector.cs`
- **Connector Implementation** - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs`
- **Sample: Connect and Download** - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\`
