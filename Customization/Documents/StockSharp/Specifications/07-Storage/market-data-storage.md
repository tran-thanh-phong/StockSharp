# Market Data Storage Specification

## Overview

Market data storage provides persistent storage for time-series trading data including ticks, candles, order books, and other market data types. The system uses a hierarchical file-based storage with date partitioning and supports multiple formats.

**File Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Storages\`

## Core Interfaces

### IStorageRegistry

The main entry point for accessing market data storage.

**File**: `IStorageRegistry.cs`

```csharp
public interface IStorageRegistry : IMessageStorageRegistry
{
    // Default storage location
    IMarketDataDrive DefaultDrive { get; set; }

    // Exchange information provider
    IExchangeInfoProvider ExchangeInfoProvider { get; }

    // Get market data storage
    IMarketDataStorage GetStorage(Security security, Type dataType, object arg,
        IMarketDataDrive drive = null, StorageFormats format = StorageFormats.Binary);
}
```

**Key Properties**:
- `DefaultDrive`: Default storage drive (typically LocalMarketDataDrive)
- `ExchangeInfoProvider`: Provides exchange and board information
- Storage format defaults to Binary for performance

### IMarketDataStorage

Represents storage for a specific data type and security.

**File**: `IMarketDataStorage.cs`

```csharp
public interface IMarketDataStorage
{
    // Available dates with data
    IEnumerable<DateTime> Dates { get; }

    // Data type stored
    DataType DataType { get; }

    // Security identifier
    SecurityId SecurityId { get; }

    // Storage drive
    IMarketDataStorageDrive Drive { get; }

    // Whether to append only new data
    bool AppendOnlyNew { get; set; }

    // Save data
    int Save(IEnumerable<Message> data);

    // Load data for specific date
    IEnumerable<Message> Load(DateTime date);

    // Delete data
    void Delete(DateTime date);
    void Delete(IEnumerable<Message> data);

    // Get metadata
    IMarketDataMetaInfo GetMetaInfo(DateTime date);

    // Serializer
    IMarketDataSerializer Serializer { get; }
}
```

## Storage Implementation

### LocalMarketDataDrive

File-based storage implementation for local disk.

**File**: `LocalMarketDataDrive.cs`

**Directory Structure**:
```
[Path]/
  [FirstLetter]/
    [SecurityId]/
      [yyyy_MM_dd]/
        ticks.bin
        candles_tf_5m.bin
        orderLog.bin
        quotes.bin
        level1.bin
```

**Key Features**:
- Date-based partitioning (yyyy_MM_dd format)
- Security grouping by first letter (for performance)
- Separate files per data type and date
- Index file for fast access (index.bin)

**Example**:
```
E:\MarketData\
  A\
    AAPL@NASDAQ\
      2024_01_15\
        ticks.bin
        candles_tf_5m.bin
      2024_01_16\
        ticks.bin
        candles_tf_5m.bin
```

### StorageRegistry

Main implementation of IStorageRegistry with caching.

**File**: `StorageRegistry.cs`

```csharp
public class StorageRegistry : Disposable, IStorageRegistry
{
    // Cached storages
    private readonly SynchronizedDictionary<Tuple<SecurityId, IMarketDataStorageDrive>,
        IMarketDataStorage<QuoteChangeMessage>> _depthStorages;
    private readonly SynchronizedDictionary<Tuple<SecurityId, IMarketDataStorageDrive>,
        IMarketDataStorage<Level1ChangeMessage>> _level1Storages;
    private readonly SynchronizedDictionary<Tuple<SecurityId, IMarketDataStorageDrive>,
        IMarketDataStorage<CandleMessage>> _candleStorages;
    // ... other storage types

    // Get storage for specific data type
    public IMarketDataStorage GetStorage(Security security, Type dataType, object arg,
        IMarketDataDrive drive = null, StorageFormats format = StorageFormats.Binary)
    {
        return GetStorage(security?.ToSecurityId() ?? default, dataType, arg, drive, format);
    }
}
```

**Caching Strategy**:
- Storages are cached by (SecurityId, Drive) tuple
- Thread-safe access with SynchronizedDictionary
- Automatic creation on first access

## Storage Methods by Data Type

### 1. Tick Data (Trades)

```csharp
// Get tick storage
var tickStorage = storageRegistry.GetTickMessageStorage(securityId);

// Save ticks
var ticks = new List<ExecutionMessage>
{
    new ExecutionMessage
    {
        DataTypeEx = DataType.Ticks,
        ServerTime = DateTime.Now,
        TradeId = 1,
        SecurityId = securityId,
        TradeVolume = 100,
        TradePrice = 150.5m
    }
};
int savedCount = tickStorage.Save(ticks);

// Load ticks for specific date
var loadedTicks = tickStorage.Load(new DateTime(2024, 1, 15));
```

**Implementation**: `GetTickMessageStorage()`
- Uses `ExecutionMessage` with `ExecutionTypes.Tick`
- Stores trade ID, price, volume, time
- Binary format uses `TickBinarySerializer`
- CSV format uses `TickCsvSerializer`

### 2. Candle Data

```csharp
// Get candle storage for 5-minute timeframe
var candleStorage = storageRegistry.GetCandleMessageStorage(
    typeof(TimeFrameCandleMessage),
    securityId,
    TimeSpan.FromMinutes(5)
);

// Save candles
var candles = new List<TimeFrameCandleMessage>
{
    new TimeFrameCandleMessage
    {
        SecurityId = securityId,
        OpenTime = DateTime.Now,
        OpenPrice = 150.0m,
        HighPrice = 151.0m,
        LowPrice = 149.5m,
        ClosePrice = 150.5m,
        TotalVolume = 1000
    }
};
candleStorage.Save(candles);

// Load candles for date range
var loadedCandles = candleStorage.Load(
    new DateTime(2024, 1, 15),
    new DateTime(2024, 1, 16)
);
```

**Supported Candle Types**:
- `TimeFrameCandleMessage`: Time-based candles
- `VolumeCandleMessage`: Volume-based candles
- `TickCandleMessage`: Tick count candles
- `RenkoCandleMessage`: Renko candles
- `PnFCandleMessage`: Point & Figure candles
- `RangeCandleMessage`: Range candles

**File Naming**:
- TimeFrame: `candles_tf_5m.bin`
- Volume: `candles_v_1000.bin`
- Tick: `candles_t_100.bin`

### 3. Order Book (Market Depth)

```csharp
// Get quote storage
var quoteStorage = storageRegistry.GetQuoteMessageStorage(securityId);

// Save quotes
var quotes = new List<QuoteChangeMessage>
{
    new QuoteChangeMessage
    {
        SecurityId = securityId,
        ServerTime = DateTime.Now,
        Bids = new[]
        {
            new QuoteChange(150.0m, 100),
            new QuoteChange(149.9m, 200)
        },
        Asks = new[]
        {
            new QuoteChange(150.1m, 150),
            new QuoteChange(150.2m, 250)
        }
    }
};
quoteStorage.Save(quotes);

// Load quotes
var loadedQuotes = quoteStorage.Load(new DateTime(2024, 1, 15));
```

**Features**:
- Stores full order book snapshots
- Optional incremental mode (pass-through)
- Efficient binary compression
- File: `quotes.bin` or `quotes.csv`

### 4. Level1 Data

```csharp
// Get Level1 storage
var level1Storage = storageRegistry.GetLevel1MessageStorage(securityId);

// Save Level1 changes
var level1Messages = new List<Level1ChangeMessage>
{
    new Level1ChangeMessage
    {
        SecurityId = securityId,
        ServerTime = DateTime.Now
    }
    .TryAdd(Level1Fields.LastTradePrice, 150.5m)
    .TryAdd(Level1Fields.BestBidPrice, 150.0m)
    .TryAdd(Level1Fields.BestAskPrice, 150.1m)
};
level1Storage.Save(level1Messages);

// Load Level1 data
var loadedLevel1 = level1Storage.Load(new DateTime(2024, 1, 15));
```

**Level1 Fields**:
- Last trade price, volume
- Best bid/ask prices and volumes
- Open, high, low, close
- Trading status
- Other market statistics

### 5. Order Log

```csharp
// Get order log storage
var orderLogStorage = storageRegistry.GetOrderLogMessageStorage(securityId);

// Save order log entries
var orderLogs = new List<ExecutionMessage>
{
    new ExecutionMessage
    {
        DataTypeEx = DataType.OrderLog,
        SecurityId = securityId,
        ServerTime = DateTime.Now,
        OrderId = 123456,
        OrderPrice = 150.0m,
        OrderVolume = 100,
        Side = Sides.Buy,
        OrderState = OrderStates.Active
    }
};
orderLogStorage.Save(orderLogs);

// Load order log
var loadedOrderLog = orderLogStorage.Load(new DateTime(2024, 1, 15));
```

## Date-Based Partitioning

### Date Management

```csharp
// Get all available dates for a storage
var dates = tickStorage.Dates;
foreach (var date in dates)
{
    Console.WriteLine($"Data available for: {date:yyyy-MM-dd}");
}

// Load data for specific date
var data = tickStorage.Load(date);

// Delete data for specific date
tickStorage.Delete(date);
```

### Date Format

- **Directory Name**: `yyyy_MM_dd` (e.g., `2024_01_15`)
- **Date Cache**: Stored in `[datatype][format]Dates2.bin`
- **UTC Time**: All dates stored in UTC
- **Optimization**: Date list cached for performance

### Date Caching

**File**: `LocalMarketDataStorageDrive.cs` (lines 45-86)

```csharp
// Date cache structure
private readonly Lazy<CachedSynchronizedOrderedDictionary<DateTime, DateTime>> _datesDict;

// Cache management
public void ClearDatesCache()
{
    if (Directory.Exists(_path))
    {
        lock (_cacheSync)
        {
            File.Delete(_datesPath);
        }
    }
    ResetCache();
}
```

## Index System

### Building Index

The index provides fast access to available securities and data types.

```csharp
var drive = new LocalMarketDataDrive(@"E:\MarketData");

// Build index asynchronously
await drive.BuildIndexAsync(
    logs: myLogReceiver,
    updateProgress: (current, total) =>
    {
        Console.WriteLine($"Processing: {current}/{total}");
    },
    cancellationToken: CancellationToken.None
);
```

**Index Features**:
- Binary format (`index.bin`)
- Stores: Securities, DataTypes, Formats, Dates
- Version tracking (v1.1)
- Automatic updates on save/delete
- Periodic auto-save (configurable interval)

**Index Structure**:
```csharp
// Index contents
class Index : CachedSynchronizedDictionary<SecurityId,
    Dictionary<StorageFormats, Dictionary<DataType, HashSet<DateTime>>>>
{
    // Methods
    public IEnumerable<SecurityId> AvailableSecurities { get; }
    public IEnumerable<DataType> GetAvailableDataTypes(SecurityId securityId, StorageFormats format);
    public IEnumerable<DateTime> GetDates(SecurityId securityId, DataType dataType, StorageFormats format);
}
```

### Using Index

```csharp
var drive = new LocalMarketDataDrive(@"E:\MarketData");

// Get all securities
var securities = drive.AvailableSecurities;

// Get available data types for security
var dataTypes = drive.GetAvailableDataTypes(securityId, StorageFormats.Binary);

// Check if specific data exists
var dates = drive.GetStorageDrive(securityId, DataType.Ticks, StorageFormats.Binary).Dates;
```

## Practical Examples

### Example 1: Save and Load Random Ticks

**From**: `Samples/03_Storage/01_Random/Program.cs`

```csharp
// Create security
var security = new Security
{
    Id = "AAPL@NASDAQ",
    PriceStep = 0.1m,
    Decimals = 1,
};

var securityId = security.ToSecurityId();
var trades = new List<ExecutionMessage>();

// Generate 1000 random ticks
const int count = 1000;
var begin = DateTime.Today;

for (var i = 0; i < count; i++)
{
    var t = new ExecutionMessage
    {
        DataTypeEx = DataType.Ticks,
        ServerTime = begin + TimeSpan.FromMinutes(i),
        TradeId = i + 1,
        SecurityId = securityId,
        TradeVolume = RandomGen.GetInt(1, 10),
        TradePrice = RandomGen.GetInt(1, 100) * security.PriceStep ?? 1m + 99
    };
    trades.Add(t);
}

// Create storage registry
var storageRegistry = new StorageRegistry()
{
    DefaultDrive = new LocalMarketDataDrive(),
};

// Get tick storage
var tradeStorage = storageRegistry.GetTickMessageStorage(securityId);

// Save ticks
tradeStorage.Save(trades);

// Load and display ticks
for (var d = begin; d < begin + TimeSpan.FromDays(1); d += TimeSpan.FromDays(1))
{
    var loadedTrades = tradeStorage.Load(d);
    foreach (var trade in loadedTrades)
    {
        Console.WriteLine($"Trade {trade.TradeId}: {trade}");
    }
}

// Delete ticks
tradeStorage.Delete(DateTime.Today, DateTime.Today + TimeSpan.FromMinutes(1000));
```

### Example 2: Load Historical Data from Local Drive

**From**: `Samples/03_Storage/02_Local/Program.cs`

```csharp
// Setup local drive
var pathHistory = Paths.HistoryDataPath;
var localDrive = new LocalMarketDataDrive(pathHistory);

// Get all available securities
var securities = localDrive.AvailableSecurities;
foreach (var sec in securities)
{
    Console.WriteLine(sec);
}

var secId = Paths.HistoryDefaultSecurity.ToSecurityId();

var storageRegistry = new StorageRegistry()
{
    DefaultDrive = localDrive,
};

// Load candles
var candleStorage = storageRegistry.GetTimeFrameCandleMessageStorage(
    secId,
    TimeSpan.FromMinutes(1),
    format: StorageFormats.Binary
);
var candles = candleStorage.Load(
    new DateTime(2020, 4, 1),
    new DateTime(2020, 4, 2)
);

foreach (var candle in candles)
{
    Console.WriteLine(candle);
}

// Load trades
var tradeStorage = storageRegistry.GetTickMessageStorage(
    secId,
    format: StorageFormats.Binary
);
var trades = tradeStorage.Load(
    new DateTime(2020, 4, 1),
    new DateTime(2020, 4, 2)
);

foreach (var trade in trades)
{
    Console.WriteLine(trade);
}

// Load market depth
var marketDepthStorage = storageRegistry.GetQuoteMessageStorage(
    secId,
    format: StorageFormats.Binary
);
var marketDepths = marketDepthStorage.Load(
    new DateTime(2020, 4, 1),
    new DateTime(2020, 4, 2)
);

foreach (var marketDepth in marketDepths)
{
    Console.WriteLine(marketDepth);
}

// Load Level1
var level1Storage = storageRegistry.GetLevel1MessageStorage(
    secId,
    format: StorageFormats.Binary
);
var levels1 = level1Storage.Load(
    new DateTime(2020, 4, 1),
    new DateTime(2020, 4, 2)
);

foreach (var level1 in levels1)
{
    Console.WriteLine(level1);
}
```

### Example 3: Multi-Day Data Operations

```csharp
var storageRegistry = new StorageRegistry()
{
    DefaultDrive = new LocalMarketDataDrive(@"E:\MarketData"),
};

var securityId = "AAPL@NASDAQ".ToSecurityId();
var tickStorage = storageRegistry.GetTickMessageStorage(securityId);

// Load data for multiple days
var startDate = new DateTime(2024, 1, 1);
var endDate = new DateTime(2024, 1, 31);

var allTicks = new List<ExecutionMessage>();

for (var date = startDate; date <= endDate; date = date.AddDays(1))
{
    if (tickStorage.Dates.Contains(date))
    {
        var dailyTicks = tickStorage.Load(date);
        allTicks.AddRange(dailyTicks);

        // Get metadata for the day
        var metaInfo = tickStorage.GetMetaInfo(date);
        Console.WriteLine($"{date:yyyy-MM-dd}: {metaInfo.Count} ticks");
    }
}

Console.WriteLine($"Total ticks loaded: {allTicks.Count}");
```

## Performance Considerations

### 1. AppendOnlyNew Mode

```csharp
var storage = storageRegistry.GetTickMessageStorage(securityId);

// Enable append-only mode for better performance
storage.AppendOnlyNew = true;

// This will only add new data, skipping duplicates
storage.Save(newTicks);
```

### 2. Batch Operations

```csharp
// Collect data in memory first
var batch = new List<ExecutionMessage>();

for (int i = 0; i < 10000; i++)
{
    batch.Add(CreateTick(i));
}

// Save in one operation
int savedCount = storage.Save(batch);
```

### 3. Date Range Queries

```csharp
// Extension method for loading date ranges
public static IEnumerable<TMessage> Load<TMessage>(
    this IMarketDataStorage<TMessage> storage,
    DateTime from,
    DateTime to) where TMessage : Message
{
    for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
    {
        if (storage.Dates.Contains(date))
        {
            foreach (var msg in storage.Load(date))
            {
                if (msg.ServerTime >= from && msg.ServerTime <= to)
                    yield return msg;
            }
        }
    }
}
```

### 4. Index Usage

```csharp
// Use index to quickly find available data
var drive = new LocalMarketDataDrive(@"E:\MarketData");

// Build index once (takes time initially)
await drive.BuildIndexAsync(null, (c, t) => {}, CancellationToken.None);

// Fast subsequent access
var securities = drive.AvailableSecurities; // From index
var dataTypes = drive.GetAvailableDataTypes(securityId, StorageFormats.Binary); // From index
```

## Error Handling

```csharp
try
{
    var storage = storageRegistry.GetTickMessageStorage(securityId);
    var ticks = storage.Load(DateTime.Today);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("DirectoryNotExist"))
{
    Console.WriteLine("Data directory does not exist");
}
catch (Exception ex)
{
    Console.WriteLine($"Error loading data: {ex.Message}");
}
```

## Best Practices

1. **Use Binary Format**: Default to binary format for production (better performance)
2. **Enable Index**: Build and maintain index for large datasets
3. **Batch Saves**: Collect data and save in batches rather than individual messages
4. **Date Partitioning**: Leverage date-based partitioning for efficient queries
5. **Cache Storages**: Reuse storage instances from StorageRegistry (they are cached)
6. **UTC Time**: Always work with UTC times for consistency
7. **Metadata**: Use GetMetaInfo() to check data availability before loading
8. **Cleanup**: Delete old data periodically to manage disk space

## Related Components

- **StorageFormats**: Binary vs CSV format selection
- **Entity Storage**: For non-time-series data (securities, portfolios)
- **Snapshot Storage**: For state persistence
- **Remote Storage**: Network-based storage access
