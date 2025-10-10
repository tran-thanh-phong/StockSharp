# Configuration and Persistence Documentation

## Overview

StockSharp provides a comprehensive configuration and persistence system based on the `IPersistable` interface and `SettingsStorage` class. This allows saving and loading connector configurations, adapter settings, strategies, and other components to/from files or databases.

Key concepts:
- **IPersistable** - Interface for objects that can be persisted
- **SettingsStorage** - Dictionary-like storage for settings
- **Load/Save** - Methods for deserialization and serialization
- **Serialization formats** - JSON, XML, Binary supported through extension methods

**Core Files:**
- IPersistable interface: Part of Ecng library
- SettingsStorage: Part of Ecng.Serialization
- Connector: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs` (lines 1055-1133)

---

## IPersistable Interface

### Definition

```csharp
public interface IPersistable
{
    // Load settings from storage
    void Load(SettingsStorage storage);

    // Save settings to storage
    void Save(SettingsStorage storage);
}
```

All major StockSharp components implement this interface:
- `Connector`
- `MessageAdapter` (and all adapter implementations)
- `BasketMessageAdapter`
- `Strategy`
- `Subscription`
- `ReConnectionSettings`
- Many other components

---

## SettingsStorage Class

### Overview

`SettingsStorage` is a dictionary-based container for storing configuration values. It supports:
- Strongly-typed get/set operations
- Nested storage (storage within storage)
- Collections of values
- Complex object serialization

### Basic Usage

```csharp
// Create new storage
var storage = new SettingsStorage();

// Set values
storage.SetValue("Host", "api.example.com");
storage.SetValue("Port", 8080);
storage.SetValue("UseSSL", true);

// Get values
var host = storage.GetValue<string>("Host");
var port = storage.GetValue<int>("Port");
var useSSL = storage.GetValue<bool>("UseSSL");

// Get with default
var timeout = storage.GetValue("Timeout", 30);  // Returns 30 if not found
```

### Nested Storage

```csharp
// Create nested storage
var dbSettings = new SettingsStorage();
dbSettings.SetValue("ConnectionString", "Server=localhost;Database=trading");
dbSettings.SetValue("Timeout", 60);

storage.SetValue("Database", dbSettings);

// Retrieve nested storage
var retrievedDbSettings = storage.GetValue<SettingsStorage>("Database");
var connectionString = retrievedDbSettings.GetValue<string>("ConnectionString");
```

### Collections

```csharp
// Store array
var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
storage.SetValue("Symbols", symbols);

// Retrieve array
var retrievedSymbols = storage.GetValue<string[]>("Symbols");

// Store collection of storages
var adapterSettings = adapters.Select(a => a.Save()).ToArray();
storage.SetValue("Adapters", adapterSettings);

// Retrieve collection of storages
var retrievedAdapterSettings = storage.GetValue<IEnumerable<SettingsStorage>>("Adapters");
```

---

## Connector Configuration

### Save Connector Settings

```csharp
public override void Save(SettingsStorage storage)
```

The Connector saves these settings:

**Basic Settings:**
- `OrdersKeepCount` - Number of orders to keep in memory
- `UpdateSecurityLastQuotes` - Update security quotes flag
- `UpdateSecurityByLevel1` - Update security from Level1 flag
- `UpdateSecurityByDefinition` - Update security from definition flag
- `UpdatePortfolioByChange` - Update portfolio from changes flag
- `OverrideSecurityData` - Override security data flag
- `CheckSteps` - Price/volume step validation flag

**Risk Management:**
- `RiskManager` - Risk manager settings (if configured)

**Adapter:**
- `Adapter` - BasketMessageAdapter configuration

**Time:**
- `MarketTimeChangedInterval` - Time change interval
- `SupportAssociatedSecurity` - Associated security support flag

**Subscriptions:**
- `SubscriptionsOnConnect` - Subscriptions to execute on connect
- `IsRestoreSubscriptionOnNormalReconnect` - Restore subscription flag
- `IsAutoUnSubscribeOnDisconnect` - Auto-unsubscribe flag

**Storage:**
- `Buffer` - Storage buffer settings (if configured)

**Example:**

```csharp
var connector = new Connector();

// Configure connector
connector.CheckSteps = true;
connector.OrdersKeepCount = 5000;
connector.UpdateSecurityByLevel1 = true;

// Configure adapter
// ... (see adapter configuration section)

// Save settings
var storage = connector.Save();

// The returned storage contains all settings
Console.WriteLine($"Saved {storage.Count} settings");
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:40`

```csharp
if (_connector.Configure(this))
{
    // Save connector configuration to file
    _connector.Save().Serialize(_connectorFile);
}
```

### Load Connector Settings

```csharp
public override void Load(SettingsStorage storage)
```

The Connector loads all settings that were previously saved.

**Example:**

```csharp
var connector = new Connector();

// Load from SettingsStorage
var storage = LoadStorageFromSomewhere();
connector.Load(storage);

// Connector is now configured with loaded settings
```

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:30-33`

```csharp
if (File.Exists(_connectorFile))
{
    // Load connector configuration from file
    _connector.Load(_connectorFile.Deserialize<SettingsStorage>());
}
```

---

## Adapter Configuration

### Save Adapter Settings

```csharp
public override void Save(SettingsStorage storage)
```

**MessageAdapter** saves:
- `Id` - Adapter unique identifier
- `HeartbeatInterval` - Heartbeat interval
- `SupportedInMessages` - Supported message types
- `ReConnectionSettings` - Reconnection settings
- `EnqueueSubscriptions` - Enqueue subscriptions flag
- `IterationInterval` - Iteration interval
- Plus adapter-specific settings (credentials, endpoints, etc.)

**BasketMessageAdapter** saves:
- `InnerAdapters` - Collection of all inner adapters with their settings and priorities

**Example:**

```csharp
// Save single adapter
var adapter = new BinanceMessageAdapter(transactionIdGenerator)
{
    Key = "your-api-key",
    Secret = "your-api-secret"
};

var adapterStorage = adapter.Save();

// Save basket with multiple adapters
var basket = connector.Adapter;
var basketStorage = basket.Save();

// basketStorage contains all inner adapters and their configurations
```

### Load Adapter Settings

```csharp
public override void Load(SettingsStorage storage)
```

**Example:**

```csharp
var adapter = new BinanceMessageAdapter(transactionIdGenerator);

// Load settings
adapter.Load(storage);

// Adapter is now configured with API keys, etc.
```

### BasketMessageAdapter Save Format

The basket saves adapters in this format:

```csharp
{
    "InnerAdapters": [
        {
            "AdapterType": "Binance",
            "AdapterSettings": {
                "Key": "...",
                "Secret": "...",
                // ... other adapter settings
            },
            "Priority": 0
        },
        {
            "AdapterType": "Coinbase",
            "AdapterSettings": {
                // ... coinbase settings
            },
            "Priority": 1
        }
    ]
}
```

---

## Serialization to Files

### JSON Serialization

The most common format for StockSharp configurations:

```csharp
using Ecng.Serialization;

// Save to JSON file
var storage = connector.Save();
storage.Serialize("connector.json");

// Load from JSON file
var loadedStorage = "connector.json".Deserialize<SettingsStorage>();
connector.Load(loadedStorage);
```

**Example JSON Output:**

```json
{
  "OrdersKeepCount": 1000,
  "UpdateSecurityLastQuotes": true,
  "UpdateSecurityByLevel1": true,
  "CheckSteps": false,
  "MarketTimeChangedInterval": "00:00:00.0100000",
  "SubscriptionsOnConnect": [
    {
      "Type": "SecurityLookup"
    },
    {
      "Type": "PortfolioLookup"
    }
  ],
  "Adapter": {
    "InnerAdapters": [
      {
        "AdapterType": "Binance",
        "AdapterSettings": {
          "Key": "your-api-key",
          "Secret": "your-secret"
        },
        "Priority": 0
      }
    ]
  }
}
```

### XML Serialization

```csharp
// Save to XML
storage.SerializeToXml("connector.xml");

// Load from XML
var loadedStorage = "connector.xml".DeserializeFromXml<SettingsStorage>();
connector.Load(loadedStorage);
```

### Binary Serialization

```csharp
// Save to binary
storage.SerializeToBinary("connector.bin");

// Load from binary
var loadedStorage = "connector.bin".DeserializeFromBinary<SettingsStorage>();
connector.Load(loadedStorage);
```

---

## Common Configuration Patterns

### Complete Save/Load Example

**From Sample**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs:19-41`

```csharp
public partial class MainWindow
{
    private readonly Connector _connector = new();
    private const string _connectorFile = "ConnectorFile.json";

    public MainWindow()
    {
        InitializeComponent();

        // Register available adapters
        ConfigManager.RegisterService<IMessageAdapterProvider>(
            new InMemoryMessageAdapterProvider(_connector.Adapter.InnerAdapters));

        // Load existing configuration if file exists
        if (File.Exists(_connectorFile))
        {
            _connector.Load(_connectorFile.Deserialize<SettingsStorage>());
        }
    }

    private void Setting_Click(object sender, RoutedEventArgs e)
    {
        // Show configuration UI dialog
        if (_connector.Configure(this))
        {
            // Save configuration when user clicks OK
            _connector.Save().Serialize(_connectorFile);
        }
    }
}
```

### Configuration with Multiple Adapters

```csharp
var connector = new Connector();

// Add Binance adapter
var binance = new BinanceMessageAdapter(connector.TransactionIdGenerator)
{
    Key = "binance-key",
    Secret = "binance-secret"
};
connector.Adapter.InnerAdapters.Add(binance);
connector.Adapter.InnerAdapters[binance] = 0; // Priority 0 (highest)

// Add Coinbase adapter
var coinbase = new CoinbaseMessageAdapter(connector.TransactionIdGenerator)
{
    Key = "coinbase-key",
    Secret = "coinbase-secret"
};
connector.Adapter.InnerAdapters.Add(coinbase);
connector.Adapter.InnerAdapters[coinbase] = 1; // Priority 1

// Configure connector settings
connector.CheckSteps = true;
connector.OrdersKeepCount = 5000;

// Save complete configuration
connector.Save().Serialize("multi-exchange-config.json");

// Later, load configuration
var newConnector = new Connector();
newConnector.Load("multi-exchange-config.json".Deserialize<SettingsStorage>());

// All adapters and settings are restored
Console.WriteLine($"Loaded {newConnector.Adapter.InnerAdapters.Count} adapters");
```

### Incremental Configuration

```csharp
var connector = new Connector();

// Load base configuration
if (File.Exists("base-config.json"))
{
    connector.Load("base-config.json".Deserialize<SettingsStorage>());
}

// Override/add specific settings
connector.OrdersKeepCount = 10000;
connector.CheckSteps = true;

// Add additional adapter
var newAdapter = new InteractiveBrokersMessageAdapter(connector.TransactionIdGenerator);
connector.Adapter.InnerAdapters.Add(newAdapter);

// Save updated configuration
connector.Save().Serialize("updated-config.json");
```

### Secure Credential Storage

**IMPORTANT**: Never store API keys/secrets in plain text in production!

```csharp
// Bad: Plain text storage
adapter.Key = "my-api-key";
adapter.Secret = "my-secret";
adapter.Save().Serialize("config.json");  // Keys saved in plain text!

// Better: Encrypt sensitive data
var storage = adapter.Save();

// Encrypt sensitive fields
var encryptedKey = Encrypt(storage.GetValue<string>("Key"));
var encryptedSecret = Encrypt(storage.GetValue<string>("Secret"));

storage.SetValue("Key", encryptedKey);
storage.SetValue("Secret", encryptedSecret);

storage.Serialize("config.json");

// On load: Decrypt sensitive fields
var loadedStorage = "config.json".Deserialize<SettingsStorage>();

loadedStorage.SetValue("Key", Decrypt(loadedStorage.GetValue<string>("Key")));
loadedStorage.SetValue("Secret", Decrypt(loadedStorage.GetValue<string>("Secret")));

adapter.Load(loadedStorage);

// Best: Use Windows Credential Manager, Azure Key Vault, or secure secret storage
// and only store credential IDs in configuration
```

### Configuration Versioning

```csharp
// Add version to configuration
var storage = connector.Save();
storage.SetValue("ConfigVersion", "1.0.0");
storage.SetValue("CreatedDate", DateTime.Now);
storage.Serialize("config.json");

// Load with version check
var loadedStorage = "config.json".Deserialize<SettingsStorage>();
var configVersion = loadedStorage.GetValue("ConfigVersion", "0.0.0");

if (Version.Parse(configVersion) < new Version("1.0.0"))
{
    // Migrate old configuration
    MigrateConfiguration(loadedStorage);
}

connector.Load(loadedStorage);
```

---

## Extension Methods

### Load Extension Methods

```csharp
// Load with null check
public static void LoadIfNotNull(this IPersistable persistable, SettingsStorage storage)
{
    if (storage != null)
        persistable.Load(storage);
}
```

**Example:**

```csharp
// Safe load - does nothing if storage is null
connector.LoadIfNotNull(maybeNullStorage);
```

### Save Extension Methods

```csharp
// Save entire object tree
public static SettingsStorage SaveEntire(this IPersistable persistable, bool throwOnError = true)
{
    // Saves object and all nested IPersistable objects
}

// Load entire object tree
public static T LoadEntire<T>(this SettingsStorage storage) where T : IPersistable, new()
{
    // Creates instance and loads entire tree
}
```

**Example:**

```csharp
// Save complex object tree
var strategy = new MyStrategy
{
    Security = security,
    Portfolio = portfolio,
    Connector = connector
};

var storage = strategy.SaveEntire();
storage.Serialize("strategy.json");

// Load entire tree
var loadedStrategy = "strategy.json"
    .Deserialize<SettingsStorage>()
    .LoadEntire<MyStrategy>();
```

---

## Strategy Configuration

Strategies also implement `IPersistable`:

```csharp
public class MyStrategy : Strategy
{
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public int Quantity { get; set; }

    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);

        storage.SetValue(nameof(StopLoss), StopLoss);
        storage.SetValue(nameof(TakeProfit), TakeProfit);
        storage.SetValue(nameof(Quantity), Quantity);
    }

    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);

        StopLoss = storage.GetValue<decimal>(nameof(StopLoss));
        TakeProfit = storage.GetValue<decimal>(nameof(TakeProfit));
        Quantity = storage.GetValue<int>(nameof(Quantity));
    }
}

// Save strategy configuration
var strategy = new MyStrategy
{
    StopLoss = 0.02m,
    TakeProfit = 0.05m,
    Quantity = 100
};

strategy.Save().Serialize("strategy-config.json");

// Load strategy configuration
var loadedStrategy = new MyStrategy();
loadedStrategy.Load("strategy-config.json".Deserialize<SettingsStorage>());
```

---

## Reconnection Settings

The `ReConnectionSettings` class is automatically persisted with adapters:

```csharp
// Configure reconnection
adapter.ReConnectionSettings.Interval = TimeSpan.FromSeconds(10);
adapter.ReConnectionSettings.AttemptCount = 5;

// Save (includes reconnection settings)
var storage = adapter.Save();

// ReConnectionSettings are automatically saved/loaded
```

---

## Common Storage Keys

### Connector Keys

| Key | Type | Description |
|-----|------|-------------|
| `OrdersKeepCount` | `int` | Number of orders to keep |
| `UpdateSecurityLastQuotes` | `bool` | Update last quotes flag |
| `UpdateSecurityByLevel1` | `bool` | Update from Level1 flag |
| `UpdateSecurityByDefinition` | `bool` | Update from definition flag |
| `UpdatePortfolioByChange` | `bool` | Update portfolio flag |
| `CheckSteps` | `bool` | Validate price/volume steps |
| `OverrideSecurityData` | `bool` | Override security data |
| `MarketTimeChangedInterval` | `TimeSpan` | Time change interval |
| `SubscriptionsOnConnect` | `SettingsStorage[]` | Auto subscriptions |
| `IsRestoreSubscriptionOnNormalReconnect` | `bool` | Restore subscriptions |
| `IsAutoUnSubscribeOnDisconnect` | `bool` | Auto unsubscribe |
| `Adapter` | `SettingsStorage` | Adapter configuration |
| `RiskManager` | `SettingsStorage` | Risk manager settings |
| `Buffer` | `SettingsStorage` | Buffer settings |

### Adapter Keys

| Key | Type | Description |
|-----|------|-------------|
| `Id` | `Guid` | Adapter ID |
| `HeartbeatInterval` | `TimeSpan` | Heartbeat interval |
| `SupportedInMessages` | `string[]` | Supported message types |
| `ReConnectionSettings` | `SettingsStorage` | Reconnection settings |
| `EnqueueSubscriptions` | `bool` | Enqueue flag |
| `IterationInterval` | `TimeSpan` | Iteration interval |

### BasketMessageAdapter Keys

| Key | Type | Description |
|-----|------|-------------|
| `InnerAdapters` | `SettingsStorage[]` | Array of adapter configurations |

Each inner adapter storage contains:
- `AdapterType` - Type name of the adapter
- `AdapterSettings` - Adapter-specific settings
- `Priority` - Adapter priority (0 = highest)

---

## Best Practices

### 1. Always Load Before Configure

```csharp
// Good
var connector = new Connector();
if (File.Exists(configFile))
{
    connector.Load(configFile.Deserialize<SettingsStorage>());
}
// Now configure or show UI

// Bad
var connector = new Connector();
ConfigureConnector(connector);  // May overwrite loaded settings
connector.Load(configFile.Deserialize<SettingsStorage>());
```

### 2. Use Descriptive Configuration Files

```csharp
// Good - Clear naming
connector.Save().Serialize("connector-binance-prod.json");
strategy.Save().Serialize("strategy-sma-crossover-btc.json");

// Bad - Unclear naming
connector.Save().Serialize("config.json");
strategy.Save().Serialize("settings.json");
```

### 3. Validate After Load

```csharp
connector.Load(storage);

// Validate critical settings
if (connector.Adapter.InnerAdapters.Count == 0)
{
    throw new InvalidOperationException("No adapters configured!");
}

// Check adapter configuration
foreach (var adapter in connector.Adapter.InnerAdapters)
{
    if (string.IsNullOrEmpty(adapter.Name))
    {
        throw new InvalidOperationException("Adapter not properly configured!");
    }
}
```

### 4. Handle Load Errors Gracefully

```csharp
try
{
    if (File.Exists(configFile))
    {
        connector.Load(configFile.Deserialize<SettingsStorage>());
    }
}
catch (Exception ex)
{
    LogError($"Failed to load configuration: {ex.Message}");

    // Fall back to default configuration
    ConfigureDefaultConnector(connector);

    // Optionally backup corrupt file
    File.Copy(configFile, $"{configFile}.corrupt.bak");
}
```

### 5. Separate Environment Configurations

```csharp
// Environment-specific config files
var environment = GetEnvironment(); // "dev", "test", "prod"
var configFile = $"connector-{environment}.json";

if (File.Exists(configFile))
{
    connector.Load(configFile.Deserialize<SettingsStorage>());
}
else
{
    throw new FileNotFoundException($"Configuration for {environment} not found!");
}
```

---

## See Also

- **[connector.md](connector.md)** - Connector class documentation
- **[adapters.md](adapters.md)** - MessageAdapter configuration details
- **[subscription-management.md](subscription-management.md)** - Subscription persistence
- **Connector.Load** - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:1055-1100`
- **Connector.Save** - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:1102-1133`
- **Sample Implementation** - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\01_ConnectAndDownloadInstruments\MainWindow.xaml.cs`
