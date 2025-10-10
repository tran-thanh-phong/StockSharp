# Platform Overview

## Introduction

StockSharp is a comprehensive C# trading platform designed for building algorithmic trading systems, trading terminals, and market data applications. It provides a unified API to connect to 60+ exchanges and brokers, process real-time market data, execute trades, and develop sophisticated trading strategies.

## Core Philosophy

StockSharp follows these key principles:

- **Message-Based Architecture**: All data flows through typed messages for consistency and testability
- **Adapter Pattern**: Unified interface across different brokers/exchanges
- **Event-Driven**: Asynchronous processing with events and rules
- **Storage-First**: Built-in persistence for market data and trading state
- **Strategy Framework**: Base classes and patterns for algorithmic strategies

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  Your Trading Application                │
│            (Strategies, UI, Analytics)                   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                    Connector                             │
│  (Unified Trading API - Orders, Positions, Data)        │
└────┬────────────────────────────────────────────┬───────┘
     │                                             │
┌────▼──────────────────┐           ┌────────────▼────────┐
│  Message Adapters     │           │  Storage System     │
│  (Exchange/Broker)    │           │  (Market Data,      │
│  - Binance           │           │   Entity Storage)   │
│  - Interactive Brokers│           │                     │
│  - CTrader           │           └─────────────────────┘
│  - 60+ others        │
└──────────────────────┘
```

### Layer Breakdown

#### 1. Business Entities Layer
**Location**: `BusinessEntities/`

High-level objects representing trading concepts:
- **Security** - Trading instruments (stocks, futures, options, crypto)
- **Order** - Trade orders with full lifecycle
- **Portfolio** - Trading accounts
- **Position** - Open positions
- **Trade/MyTrade** - Market and own trades
- **MarketDepth** - Order book

**Purpose**: Provides object-oriented, mutable entities for application logic.

#### 2. Messages Layer
**Location**: `Messages/`

Low-level, immutable messages for data exchange:
- **ExecutionMessage** - Orders, trades, order log
- **QuoteChangeMessage** - Order book updates
- **Level1ChangeMessage** - Market data (price, volume, etc.)
- **CandleMessage** - OHLCV candles
- **SecurityMessage** - Security definitions
- **PortfolioMessage** - Portfolio updates

**Purpose**: Type-safe, serializable communication protocol.

#### 3. Connector Layer
**Location**: `Algo/Connector.cs`

Central hub that:
- Manages connections to exchanges/brokers
- Converts between Messages and BusinessEntities
- Handles subscriptions
- Routes orders and market data
- Manages adapters

**Key Interface**: `IConnector`

#### 4. Adapter Layer
**Location**: `Connectors/`

Exchange-specific implementations:
- `MessageAdapter` base class
- Native protocol implementations (REST, WebSocket, FIX)
- Message translation to/from exchange formats
- 60+ supported exchanges

#### 5. Strategy Layer
**Location**: `Algo/Strategies/Strategy.cs`

Framework for building algorithms:
- **Strategy** base class with lifecycle management
- Event-driven rules system
- Technical indicators integration
- Position and risk management
- PnL and performance tracking

#### 6. Storage Layer
**Location**: `Algo/Storages/`

Data persistence:
- Market data storage (ticks, candles, order books)
- Entity storage (securities, positions)
- Snapshot storage for fast restart
- Multiple formats (CSV, Binary)

## Key Components

### Connector
**File**: `Algo/Connector.cs:15`

The main entry point for all trading operations:

```csharp
var connector = new Connector(
    securityStorage,
    positionStorage,
    exchangeInfoProvider,
    storageRegistry,
    snapshotRegistry
);

// Events
connector.Connected += () => { /* handle connection */ };
connector.SecurityReceived += (sub, security) => { /* new security */ };
connector.OrderReceived += (sub, order) => { /* order update */ };

// Operations
connector.Connect();
connector.Subscribe(new Subscription(DataType.Level1, security));
connector.RegisterOrder(order);
```

### Strategy
**File**: `Algo/Strategies/Strategy.cs`

Base class for algorithmic strategies:

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // Subscribe to data
        var subscription = SubscribeCandles(candleType);

        // Set up rules
        subscription
            .WhenCandleFinished()
            .Do(candle => { /* trading logic */ })
            .Apply(this);
    }
}
```

### Message Adapters
**File**: `Messages/MessageAdapter.cs`

Interface to exchanges:

```csharp
public abstract class MessageAdapter
{
    // Override to send messages to exchange
    protected abstract void OnSendInMessage(Message message);

    // Call to send messages from exchange
    protected void SendOutMessage(Message message);
}
```

## Data Flow

### Market Data Flow

```
Exchange → Adapter → Connector → Strategy/Application
          (Messages)  (Converts to  (Business Logic)
                       Entities)
```

### Order Flow

```
Strategy → Connector → Adapter → Exchange
 (Order)   (Validates,  (Converts   (Executes)
           Tracks)      to Protocol)
```

## Message-Based Architecture

All communication uses strongly-typed messages:

**Advantages**:
- **Testable**: Easy to mock and replay
- **Serializable**: Can be stored and replayed
- **Type-Safe**: Compile-time checks
- **Decoupled**: Components communicate via messages

**Example Message Flow**:
1. Strategy calls `BuyMarket(volume)`
2. Creates `Order` entity
3. Converted to `OrderRegisterMessage`
4. Adapter sends to exchange
5. Exchange responds with order status
6. Converted to `ExecutionMessage`
7. Connector updates `Order` entity
8. Strategy receives `OrderReceived` event

## Design Patterns

### 1. Provider Pattern
Abstracted access to data:
- `ISecurityProvider` - Access to securities
- `IPortfolioProvider` - Access to portfolios
- `IMarketDataProvider` - Access to market data

### 2. Storage Pattern
Persistence abstraction:
- `ISecurityStorage` - Security definitions
- `IPositionStorage` - Position tracking
- `IMarketDataStorage<T>` - Time-series data

### 3. Subscription Pattern
Declarative data subscriptions:
```csharp
var subscription = new Subscription(DataType.Level1, security);
connector.Subscribe(subscription);
```

### 4. Rule Pattern
Event-driven logic:
```csharp
order
    .WhenMatched()
    .Do(() => LogInfo("Order filled"))
    .Apply(this);
```

### 5. Parameter Pattern
Configurable strategies:
```csharp
private readonly StrategyParam<int> _period;
public int Period
{
    get => _period.Value;
    set => _period.Value = value;
}
```

## Technology Stack

**Language**: C# 12.0
**Frameworks**:
- .NET Standard 2.0/2.1 (libraries)
- .NET 6.0+ Windows (UI projects)
- .NET 8.0/9.0 (test projects)

**Dependencies**:
- **Ecng** - Foundation libraries (collections, serialization, logging)
- **Math.NET Numerics** - Mathematical computations
- **Protobuf** - Message serialization
- **GeneticSharp** - Optimization algorithms

**UI Framework**: WPF with XAML (Windows)

## Target Use Cases

### 1. Algorithmic Trading
Build automated trading strategies:
- Technical analysis strategies
- Statistical arbitrage
- Market making
- High-frequency trading

### 2. Trading Terminals
Full-featured trading applications:
- Order management
- Market data visualization
- Multi-account support
- Risk management

**Example**: `Samples/06_Strategies/10_LiveTerminal/`

### 3. Market Data Applications
Real-time and historical data processing:
- Data recording and replay
- Analytics and research
- Backtesting systems

### 4. Integration Systems
Connect multiple systems:
- Multi-exchange connectivity
- Order routing
- Risk aggregation
- Reporting systems

## Performance Characteristics

- **Latency**: Optimized for low-latency trading
  - Message passing with minimal allocations
  - Efficient collections (CachedSynchronizedSet)
  - Background processing threads

- **Throughput**: Handle high message volumes
  - Message queues with priorities
  - Batch processing for storage
  - Configurable buffering

- **Memory**: Efficient memory usage
  - Configurable entity caching (`OrdersKeepCount`)
  - Object pooling in hot paths
  - Snapshot storage for restart

## Extensibility Points

StockSharp is designed to be extended:

1. **Custom Adapters** - Add new exchange/broker support
2. **Custom Indicators** - Implement technical indicators
3. **Custom Order Types** - Define conditional orders
4. **Custom Storage** - Implement storage backends
5. **Custom Risk Rules** - Add risk management logic
6. **Custom Commission Rules** - Define fee structures

## Sample Projects

**Location**: `Samples/`

Organized by complexity:
- **01_Basic** - Connection, orders, market data
- **02_Candles** - Candlestick data
- **03_Storage** - Data persistence
- **04_Indicators** - Technical analysis
- **05_Chart** - Visualization
- **06_Strategies** - Algorithmic trading
- **07_Testing** - Backtesting
- **08_Advanced** - Complex scenarios
- **09_CrossPlatform** - Console apps

### Key Sample: LiveTerminal
**Location**: `Samples/06_Strategies/10_LiveTerminal/`

A complete trading terminal demonstrating:
- Multi-adapter connection management
- Securities browser
- Order book visualization
- Order management
- Strategy execution and monitoring
- Portfolio/position tracking
- Data persistence

## Getting Started Path

For new developers:

1. **Understand Core Concepts** → [Core Concepts](core-concepts.md)
2. **Build Simple Bot** → [Quick Start](quick-start.md)
3. **Study LiveTerminal** → [Live Terminal Walkthrough](../11-Examples/live-terminal.md)
4. **Build First Strategy** → [SMA Crossover](../11-Examples/sma-crossover.md)
5. **Explore Advanced Features** → [Advanced Features](../10-Advanced-Features/)

## See Also

- [Core Concepts](core-concepts.md) - Essential terminology
- [Quick Start Guide](quick-start.md) - First trading bot
- [Connector](../02-Connection-Management/connector.md) - Connection management
- [Strategy Basics](../05-Strategy-Framework/strategy-basics.md) - Building strategies
- [Message System](../09-Messages/message-system.md) - Message architecture details
