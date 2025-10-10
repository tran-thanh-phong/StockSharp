# Core Concepts

## Overview

This document explains the essential concepts and terminology used throughout StockSharp. Understanding these concepts is crucial for building trading systems effectively.

## Key Entities

### Security

A **Security** represents a tradable instrument (stock, future, option, crypto, etc.).

**File**: `BusinessEntities/Security.cs`

**Key Properties**:
```csharp
public class Security
{
    // Identity
    public string Id { get; set; }          // Unique identifier
    public string Code { get; set; }        // Symbol/Ticker (e.g., "BTCUSDT")
    public ExchangeBoard Board { get; set; } // Trading board/exchange

    // Classification
    public SecurityTypes? Type { get; set; } // Stock, Future, Option, Crypto, etc.
    public CurrencyTypes? Currency { get; set; }

    // Trading Parameters
    public decimal? PriceStep { get; set; }  // Minimum price increment
    public decimal? VolumeStep { get; set; } // Minimum volume increment
    public decimal? MinVolume { get; set; }  // Minimum order size
    public decimal? Multiplier { get; set; } // Contract multiplier

    // Market Data
    public decimal? LastTrade { get; set; }  // Last trade price
    public Quote BestBid { get; set; }       // Best bid
    public Quote BestAsk { get; set; }       // Best ask
}
```

**Example**:
```csharp
var btc = new Security
{
    Id = "BTCUSDT@Binance",
    Code = "BTCUSDT",
    Board = ExchangeBoard.Binance,
    Type = SecurityTypes.CryptoCurrency,
    PriceStep = 0.01m,
    VolumeStep = 0.00001m
};
```

### Order

An **Order** represents a trading order with full lifecycle tracking.

**File**: `BusinessEntities/Order.cs`

**Key Properties**:
```csharp
public class Order
{
    // Identity
    public long TransactionId { get; set; }  // Local transaction ID
    public long? Id { get; set; }            // Exchange order ID
    public string StringId { get; set; }     // String-based ID

    // Core Parameters
    public Security Security { get; set; }
    public Portfolio Portfolio { get; set; }
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public decimal Balance { get; set; }      // Remaining volume
    public Sides Side { get; set; }           // Buy or Sell
    public OrderTypes Type { get; set; }      // Market, Limit, Conditional

    // State
    public OrderStates State { get; set; }    // None, Pending, Active, Done, Failed
    public DateTimeOffset Time { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }

    // Additional
    public string Comment { get; set; }
    public decimal? Commission { get; set; }
}
```

**Order States**:
- `None` - Initial state
- `Pending` - Submitted to connector
- `Active` - Accepted by exchange
- `Done` - Fully filled or cancelled
- `Failed` - Rejected

**Order Types**:
- `Market` - Execute at current market price
- `Limit` - Execute at specified price or better
- `Conditional` - Stop-loss, take-profit, algo orders

**Sides**:
- `Buy` - Long position
- `Sell` - Short position

### Portfolio

A **Portfolio** represents a trading account.

**File**: `BusinessEntities/Portfolio.cs`

```csharp
public class Portfolio : Position
{
    public string Name { get; set; }         // Account name
    public ExchangeBoard Board { get; set; }
    public PortfolioStates? State { get; set; }
    public CurrencyTypes? Currency { get; set; }
}
```

Extends `Position` class, so it has:
- `BeginValue` - Starting balance
- `CurrentValue` - Current balance
- `BlockedValue` - Blocked by active orders

### Position

A **Position** represents an open position for a security in a portfolio.

**File**: `BusinessEntities/Position.cs`

```csharp
public class Position
{
    public Portfolio Portfolio { get; set; }
    public Security Security { get; set; }

    // Size
    public decimal BeginValue { get; set; }    // Starting position
    public decimal CurrentValue { get; set; }  // Current position (+/-)
    public decimal BlockedValue { get; set; }  // Blocked by orders

    // Pricing
    public decimal? CurrentPrice { get; set; }
    public decimal? AveragePrice { get; set; }

    // PnL
    public decimal? RealizedPnL { get; set; }
    public decimal? UnrealizedPnL { get; set; }
}
```

**Sign Convention**:
- Positive `CurrentValue` = Long position
- Negative `CurrentValue` = Short position
- Zero `CurrentValue` = Flat (no position)

### Trade vs MyTrade

**Trade**: Market trade (any participant)
**MyTrade**: Your own executed trade

```csharp
// Market Trade
public class Trade
{
    public Security Security { get; set; }
    public long Id { get; set; }
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public Sides? OriginSide { get; set; }  // Aggressor side
    public DateTimeOffset Time { get; set; }
}

// Own Trade
public class MyTrade
{
    public Order Order { get; set; }
    public Trade Trade { get; set; }
    public decimal Commission { get; set; }
    public decimal? Slippage { get; set; }
    public decimal? PnL { get; set; }
}
```

## Message System

### Why Messages?

StockSharp uses a **message-based architecture** for several reasons:
1. **Testability** - Easy to mock and replay
2. **Serialization** - Can be stored and transmitted
3. **Type Safety** - Compile-time verification
4. **Decoupling** - Components communicate without direct references

### Key Message Types

**ExecutionMessage**: Multi-purpose message for orders/trades
```csharp
public class ExecutionMessage : ISecurityIdMessage
{
    public SecurityId SecurityId { get; set; }
    public DataType DataType { get; set; }  // Ticks, Transaction, OrderLog

    // Order fields
    public long? OrderId { get; set; }
    public decimal? OrderPrice { get; set; }
    public decimal? OrderVolume { get; set; }
    public OrderStates? OrderState { get; set; }

    // Trade fields
    public long? TradeId { get; set; }
    public decimal? TradePrice { get; set; }
    public decimal? TradeVolume { get; set; }
}
```

**QuoteChangeMessage**: Order book update
```csharp
public class QuoteChangeMessage
{
    public SecurityId SecurityId { get; set; }
    public QuoteChange[] Bids { get; set; }  // Price levels
    public QuoteChange[] Asks { get; set; }
    public bool IsSnapshot { get; set; }      // Full or incremental
}

public class QuoteChange
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
}
```

**Level1ChangeMessage**: Market data fields (85+ fields)
```csharp
public class Level1ChangeMessage
{
    public SecurityId SecurityId { get; set; }
    public IDictionary<Level1Fields, object> Changes { get; set; }
}

// Example fields
enum Level1Fields
{
    LastTradePrice, LastTradeVolume,
    BestBidPrice, BestBidVolume,
    BestAskPrice, BestAskVolume,
    OpenPrice, HighPrice, LowPrice, ClosePrice,
    Volume, OpenInterest,
    ImpliedVolatility, Delta, Gamma, Vega, Theta, Rho,
    // ... 85+ fields total
}
```

## Data Types

### DataType Class

The `DataType` class represents the type of market data:

```csharp
// Common data types
DataType.Level1            // Tick-by-tick quotes
DataType.MarketDepth       // Order book
DataType.Ticks             // Trades (Time & Sales)
DataType.OrderLog          // Exchange order log
DataType.News              // Market news

// Candle data types
DataType.TimeFrame(TimeSpan.FromMinutes(5))  // 5-minute candles
DataType.TimeFrame(TimeSpan.FromHours(1))    // 1-hour candles

// Can create custom types
var customType = DataType.Create(typeof(MyCustomData), args);
```

### Subscription

A `Subscription` wraps a data subscription request:

```csharp
// Subscribe to Level1 data
var sub1 = new Subscription(DataType.Level1, security);

// Subscribe to 5-minute candles
var candleType = TimeSpan.FromMinutes(5).TimeFrame();
var sub2 = new Subscription(candleType, security);

// Subscribe to order book
var sub3 = new Subscription(DataType.MarketDepth, security);

connector.Subscribe(sub1);
```

**Subscription State**:
- `Stopped` - Not active
- `Stopping` - Unsubscribing
- `Active` - Subscribed but not receiving data
- `Online` - Receiving data
- `Error` - Subscription failed

## Strategy Concepts

### Strategy Lifecycle

```
Create → Start → Starting → Started → Stopping → Stopped
                    ↓
                 OnStarted()
                 (Your logic)
                    ↓
                 Stop()
```

**States** (`ProcessStates`):
- `Stopped` - Not running
- `Starting` - Initialization phase
- `Started` - Running
- `Stopping` - Cleanup phase

### Strategy Parameters

Use `StrategyParam<T>` for configurable parameters:

```csharp
public class MyStrategy : Strategy
{
    private readonly StrategyParam<int> _period;

    public int Period
    {
        get => _period.Value;
        set => _period.Value = value;
    }

    public MyStrategy()
    {
        _period = Param(nameof(Period), 20)
            .SetGreaterThanZero()
            .SetDisplay("Period", "SMA period", "Indicators")
            .SetCanOptimize(true)
            .SetOptimize(10, 50, 5);  // Min, Max, Step
    }
}
```

Benefits:
- Automatic persistence
- UI property grid integration
- Optimization support
- Validation rules

### Trading Rules

Rules provide event-driven trading logic:

```csharp
// When order is fully filled
order
    .WhenMatched()
    .Do(() => LogInfo("Order filled!"))
    .Apply(this);

// When candle finishes
subscription
    .WhenCandleFinished()
    .Do(candle => ProcessCandle(candle))
    .Apply(this);

// When price changes
security
    .WhenChanged()
    .Do(() => CheckSignals())
    .Apply(this);
```

## Connection Concepts

### ConnectionState

Connector has these states:

```csharp
enum ConnectionStates
{
    Disconnected,  // Not connected
    Connecting,    // Establishing connection
    Connected,     // Active connection
    Disconnecting, // Closing connection
    Failed         // Connection failed
}
```

### Adapters

**MessageAdapter** is the base class for exchange connectors:
- Translates messages to/from exchange protocol
- Manages connection
- Handles authentication

**BasketMessageAdapter**: Contains multiple adapters
- Route orders to specific exchanges
- Aggregate market data from multiple sources

Example:
```csharp
var connector = new Connector();

// Add multiple exchanges
connector.Adapter.InnerAdapters.Add(new BinanceMessageAdapter(...));
connector.Adapter.InnerAdapters.Add(new InteractiveBrokersAdapter(...));
```

## Storage Concepts

### Market Data Storage

Store market data for replay/analysis:

```csharp
// Storage registry
var storageRegistry = new StorageRegistry
{
    DefaultDrive = new LocalMarketDataDrive("Data")
};

// Get storage for specific data type
var tickStorage = storageRegistry.GetTickMessageStorage(security.ToSecurityId());
var candleStorage = storageRegistry.GetCandleMessageStorage(candleType, security.ToSecurityId());

// Save data
tickStorage.Save(tickMessage);

// Load data
var ticks = tickStorage.Load(date);
```

### Entity Storage

Store securities and positions:

```csharp
// CSV-based entity storage
var entityRegistry = new CsvEntityRegistry("Data");

// Stores:
// - Securities
// - Positions
// - Exchange info
```

### Snapshots

Save/restore full connector state:

```csharp
var snapshotRegistry = new SnapshotRegistry("Snapshots");

// Snapshots store:
// - Security definitions
// - Positions
// - Orders
// - Trades
```

## Risk Management Concepts

### Risk Rules

Monitor and enforce trading limits:

```csharp
connector.RiskManager = new RiskManager
{
    Rules =
    {
        // Max loss limit
        new RiskPnLRule
        {
            Action = RiskActions.ClosePositions,
            Value = -1000 // $1000 max loss
        },

        // Max position size
        new RiskPositionSizeRule
        {
            Action = RiskActions.StopTrading,
            Value = 100
        }
    }
};
```

**Risk Actions**:
- `ClosePositions` - Close all positions
- `StopTrading` - Stop new orders
- `CancelOrders` - Cancel active orders

## Indicator Concepts

### Indicator

Technical indicators process market data:

```csharp
var sma = new SimpleMovingAverage { Length = 20 };

// Process candles
subscription
    .Bind(sma, (candle, smaValue) =>
    {
        LogInfo($"SMA: {smaValue}");
    })
    .Start();
```

### IsFormed

Indicators need minimum data to be reliable:

```csharp
if (sma.IsFormed)
{
    // Indicator has enough data
    var value = sma.GetCurrentValue();
}
```

## Important IDs and Tracking

### TransactionId

Local unique ID for tracking operations:
- Generated by `TransactionIdGenerator`
- Used to correlate requests and responses
- Assigned to orders, subscriptions, lookups

```csharp
var order = new Order { /* ... */ };
connector.RegisterOrder(order);
// order.TransactionId is automatically assigned

Console.WriteLine($"Order transaction: {order.TransactionId}");
```

### Order IDs

Multiple IDs track orders:
- `TransactionId` - Local ID (always assigned)
- `Id` - Exchange order ID (assigned by exchange)
- `StringId` - Alternative string-based ID

### SecurityId

Composite identifier for securities:

```csharp
public struct SecurityId
{
    public string SecurityCode { get; set; }  // "BTCUSDT"
    public string BoardCode { get; set; }     // "Binance"

    // Optional fields
    public SecurityTypes? SecurityType { get; set; }
    public string Native { get; set; }        // Exchange-specific ID
}

// Usage
var secId = new SecurityId
{
    SecurityCode = "BTCUSDT",
    BoardCode = "Binance"
};

var security = connector.GetSecurity(secId);
```

## Time Concepts

### Time Types

Multiple time fields exist for synchronization:

```csharp
// Order times
order.Time          // Server time (exchange)
order.LocalTime     // Local computer time
order.ServerTime    // Connector server time

// Message times
message.ServerTime  // When message was created
message.LocalTime   // When message was processed locally
```

### CurrentTime

Strategies and connectors have `CurrentTime`:

```csharp
// In strategy
var now = CurrentTime;

// Based on last message time, not system clock
// Important for backtesting consistency
```

## See Also

- [Platform Overview](overview.md) - Architecture details
- [Quick Start](quick-start.md) - First trading bot
- [Connector API](../12-Reference/connector-api.md) - Complete API reference
- [Strategy API](../12-Reference/strategy-api.md) - Strategy reference
- [Message Types](../12-Reference/message-types.md) - All message types
