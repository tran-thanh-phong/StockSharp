# Connection Management Specifications

## Overview

This directory contains comprehensive documentation for StockSharp's Connection Management system. These specifications cover how to connect to trading systems, configure adapters, manage subscriptions, and persist configurations.

## Documents

### 1. [connector.md](connector.md) - Connector Class Documentation

The main entry point for connecting to trading systems.

**Topics covered:**
- Connector lifecycle (Connect, Disconnect states)
- Connection events (Connected, Disconnected, ConnectionError)
- Entity providers (Securities, Portfolios, Positions)
- Configuration properties (CheckSteps, UpdateSecurityByLevel1, etc.)
- Subscriptions on connect
- Risk management integration
- Complete working examples

**Key classes:**
- `Connector` - Main connection class
- `IConnector` - Connector interface
- `ConnectionStates` - Connection state enumeration

**When to use this document:**
- Setting up a new trading application
- Managing connection lifecycle
- Configuring connector behavior
- Understanding entity providers

---

### 2. [adapters.md](adapters.md) - MessageAdapter Documentation

Deep dive into the adapter pattern and supported exchanges.

**Topics covered:**
- MessageAdapter base class architecture
- BasketMessageAdapter for multi-exchange connections
- InnerAdapters collection and prioritization
- 60+ supported exchanges and brokers
- Adapter capabilities and feature detection
- Creating custom adapters
- Message routing and transformation
- Wrapper adapters (heartbeat, storage, channels)

**Key classes:**
- `MessageAdapter` - Base adapter class
- `BasketMessageAdapter` - Adapter aggregator
- `IInnerAdapterList` - Adapter collection with priorities
- `IMessageAdapterProvider` - Adapter provider interface

**When to use this document:**
- Connecting to specific exchanges
- Understanding adapter capabilities
- Building custom exchange integrations
- Configuring multi-exchange setups
- Managing adapter priorities

---

### 3. [configuration.md](configuration.md) - Configuration and Persistence

How to save and load connector/adapter configurations.

**Topics covered:**
- IPersistable interface
- SettingsStorage usage patterns
- Connector.Load/Save methods
- Adapter configuration persistence
- Serialization formats (JSON, XML, Binary)
- Secure credential storage
- Configuration versioning
- Environment-specific configurations

**Key classes:**
- `IPersistable` - Persistence interface
- `SettingsStorage` - Settings container
- Configuration extension methods

**When to use this document:**
- Saving connector configurations to files
- Loading saved configurations
- Managing multiple configuration profiles
- Implementing secure credential storage
- Configuration migration

---

### 4. [subscription-management.md](subscription-management.md) - Subscription Management

Comprehensive guide to data subscriptions.

**Topics covered:**
- Subscription class and lifecycle
- DataType specifications
- Subscription states (Stopped, Active, Online, Finished, Error)
- Subscribe/UnSubscribe methods
- Subscription events
- SubscriptionsOnConnect pattern
- Lookup patterns (Securities, Portfolios, Orders)
- Market data subscriptions (Level1, MarketDepth, Ticks, Candles)
- Historical data with time ranges
- Best practices and common scenarios

**Key classes:**
- `Subscription` - Subscription class
- `SubscriptionStates` - State enumeration
- `DataType` - Data type definitions
- Subscription events

**When to use this document:**
- Subscribing to market data
- Managing subscription lifecycle
- Downloading historical data
- Implementing real-time data feeds
- Handling subscription errors

---

## Quick Start Guide

### Basic Connection Setup

```csharp
using StockSharp.Algo;
using StockSharp.Messages;
using Ecng.Serialization;

// 1. Create connector
var connector = new Connector();

// 2. Configure connector
connector.CheckSteps = true;
connector.UpdateSecurityByLevel1 = true;

// 3. Add adapter (example: Binance)
var adapter = new BinanceMessageAdapter(connector.TransactionIdGenerator)
{
    Key = "your-api-key",
    Secret = "your-api-secret"
};
connector.Adapter.InnerAdapters.Add(adapter);

// 4. Subscribe to events
connector.Connected += () => Console.WriteLine("Connected!");
connector.ConnectionError += ex => Console.WriteLine($"Error: {ex.Message}");

// 5. Connect
connector.Connect();
```

### Loading Saved Configuration

```csharp
var connector = new Connector();

// Load configuration from file
if (File.Exists("config.json"))
{
    connector.Load("config.json".Deserialize<SettingsStorage>());
}

// Connect
connector.Connect();
```

### Subscribing to Market Data

```csharp
// After connection
connector.Connected += () =>
{
    // Lookup all securities
    connector.Subscribe(new Subscription(Extensions.LookupAllCriteriaMessage));
};

// Handle securities
connector.SecurityReceived += (subscription, security) =>
{
    Console.WriteLine($"Security: {security.Code}");

    // Subscribe to Level1 data
    connector.Subscribe(new Subscription(DataType.Level1, security));
};

// Handle Level1 updates
connector.Level1Received += (subscription, level1) =>
{
    Console.WriteLine($"Price update: {level1.Changes[Level1Fields.LastPrice]}");
};
```

---

## Document Relationships

```
connector.md (Main Entry Point)
    ├── adapters.md (How connectors use adapters)
    │   └── Custom adapter creation
    ├── configuration.md (How to persist connector settings)
    │   └── Adapter configuration
    └── subscription-management.md (How to subscribe to data)
        └── Subscription lifecycle
```

---

## Code Examples Location

All examples reference actual code from the StockSharp repository:

- **Basic Samples**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\`
  - `01_ConnectAndDownloadInstruments` - Basic connection and security lookup
  - `02_MarketDepths` - Order book subscription
  - `03_Orders` - Order management

- **Strategy Samples**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\06_Strategies\`
  - `10_LiveTerminal` - Complete live trading terminal with subscriptions

- **Advanced Samples**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\09_Advanced\`
  - `01_MultiConnect` - Multiple adapter connections

---

## Architecture Overview

```
┌─────────────────────────────────────────┐
│         Your Application                │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │         Connector                 │ │
│  │  (Connection Management)          │ │
│  │                                   │ │
│  │  ┌─────────────────────────────┐ │ │
│  │  │  BasketMessageAdapter       │ │ │
│  │  │                             │ │ │
│  │  │  ┌─────────┐  ┌─────────┐  │ │ │
│  │  │  │ Binance │  │Coinbase │  │ │ │
│  │  │  │ Adapter │  │ Adapter │  │ │ │
│  │  │  └────┬────┘  └────┬────┘  │ │ │
│  │  └───────┼────────────┼───────┘ │ │
│  └──────────┼────────────┼─────────┘ │
└─────────────┼────────────┼───────────┘
              │            │
              ▼            ▼
        ┌─────────┐  ┌──────────┐
        │ Binance │  │ Coinbase │
        │   API   │  │   API    │
        └─────────┘  └──────────┘
```

---

## Key Concepts Summary

### Connection Management
- **Connector** manages overall connection state
- **Adapters** handle exchange-specific protocols
- **Connection events** notify application of state changes
- **Auto-reconnection** handled by ReConnectionSettings

### Data Flow
- **Incoming**: User → Connector → BasketAdapter → Adapter → Exchange
- **Outgoing**: Exchange → Adapter → BasketAdapter → Connector → Events → User

### Subscription Model
- **Request-based**: Subscribe to specific data types
- **State machine**: Stopped → Active → Online/Finished
- **Event-driven**: Receive data through typed events
- **Automatic management**: SubscriptionsOnConnect for common patterns

### Configuration
- **IPersistable** pattern for all configurable components
- **SettingsStorage** for hierarchical settings
- **JSON/XML/Binary** serialization support
- **Version-safe** loading with defaults

---

## Best Practices

### 1. Connection Management
- Always subscribe to `ConnectionError` event
- Implement reconnection logic for production systems
- Use `IsRestoreSubscriptionOnNormalReconnect` for user-initiated reconnects
- Clean up resources in `Dispose` or application close handlers

### 2. Adapter Configuration
- Store credentials securely (use encryption or secure storage)
- Set appropriate adapter priorities for multi-exchange scenarios
- Configure heartbeat intervals based on exchange requirements
- Test adapter capabilities before subscribing

### 3. Subscription Management
- Check subscription state before assuming data flow
- Implement error handling for subscription failures
- Clean up subscriptions when no longer needed
- Use subscription events to track lifecycle

### 4. Configuration Persistence
- Use environment-specific configuration files
- Implement configuration validation after loading
- Handle load errors gracefully with fallbacks
- Version your configurations for future migrations

---

## Common Patterns

### Pattern 1: Single Exchange Connection

```csharp
var connector = new Connector();
var adapter = new BinanceMessageAdapter(connector.TransactionIdGenerator);
connector.Adapter.InnerAdapters.Add(adapter);
connector.Connect();
```

### Pattern 2: Multi-Exchange Connection

```csharp
var connector = new Connector();

var binance = new BinanceMessageAdapter(connector.TransactionIdGenerator);
connector.Adapter.InnerAdapters[binance] = 0;  // Highest priority

var coinbase = new CoinbaseMessageAdapter(connector.TransactionIdGenerator);
connector.Adapter.InnerAdapters[coinbase] = 1;

connector.Connect();
```

### Pattern 3: Configuration File

```csharp
// Save
connector.Save().Serialize("config.json");

// Load
var connector = new Connector();
connector.Load("config.json".Deserialize<SettingsStorage>());
```

### Pattern 4: Auto-Subscribe on Connect

```csharp
connector.SubscriptionsOnConnect.Add(connector.SecurityLookup);
connector.SubscriptionsOnConnect.Add(connector.PortfolioLookup);
connector.Connect();
```

---

## Troubleshooting Guide

### Connection Issues

**Problem**: Connector won't connect
- Check adapter configuration (API keys, endpoints)
- Verify network connectivity
- Check adapter logs for specific errors
- Ensure adapter supports required message types

**Problem**: Connection drops frequently
- Adjust heartbeat settings
- Check network stability
- Review reconnection settings
- Verify API rate limits not exceeded

### Subscription Issues

**Problem**: No data received after subscription
- Verify subscription state (should be Active or Online)
- Check if adapter supports the data type
- Ensure security is valid and tradeable
- Review subscription events for errors

**Problem**: Historical data incomplete
- Check From/To date range
- Verify adapter supports historical data
- Check for rate limiting
- Review subscription finished event

### Configuration Issues

**Problem**: Configuration won't load
- Validate JSON/XML format
- Check for missing required fields
- Verify adapter types are available
- Handle version mismatches

---

## Related Documentation

- **StockSharp Official Docs**: https://doc.stocksharp.com/
- **API Reference**: https://doc.stocksharp.com/api/
- **GitHub Repository**: https://github.com/StockSharp/StockSharp
- **Community Forum**: https://stocksharp.com/forum/

---

## Version Information

**StockSharp Version**: 5.x+
**Last Updated**: 2025-01-10
**Author**: StockSharp Documentation Team

---

## Feedback

For questions, issues, or suggestions about these specifications:
- Open an issue on GitHub
- Post on the StockSharp forum
- Contact support@stocksharp.com
