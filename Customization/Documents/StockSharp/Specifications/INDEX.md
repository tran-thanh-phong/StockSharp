# StockSharp Feature Specifications

## Quick Navigation Index

This comprehensive documentation provides detailed specifications for all StockSharp features to help developers and AI tools build auto-trading systems effectively.

---

## 01. Getting Started

Start here if you're new to StockSharp or want a quick overview.

- [Platform Overview](01-Getting-Started/overview.md) - Architecture, components, and design patterns
- [Quick Start Guide](01-Getting-Started/quick-start.md) - Build your first trading bot in 5 minutes
- [Core Concepts](01-Getting-Started/core-concepts.md) - Essential terminology and concepts

---

## 02. Connection Management

Learn how to connect to exchanges and brokers.

- [Connector](02-Connection-Management/connector.md) - Main Connector class and connection lifecycle
- [Adapters](02-Connection-Management/adapters.md) - Message adapters and broker integration
- [Configuration](02-Connection-Management/configuration.md) - Connection settings and persistence
- [Subscription Management](02-Connection-Management/subscription-management.md) - Managing market data subscriptions

---

## 03. Market Data

Access real-time and historical market data.

- [Level 1 Data](03-Market-Data/level1-data.md) - Tick-by-tick quotes and 85+ market data fields
- [Order Book (Level 2)](03-Market-Data/order-book.md) - Market depth and order book data
- [Trades](03-Market-Data/trades.md) - Market trades (Time & Sales)
- [Candles](03-Market-Data/candles.md) - OHLCV candlestick data
- [Order Log](03-Market-Data/order-log.md) - Exchange order log data
- [News](03-Market-Data/news.md) - Market news and headlines

---

## 04. Trading

Execute and manage trades.

- [Order Management](04-Trading/order-management.md) - Register, cancel, and modify orders
- [Order Types](04-Trading/order-types.md) - Market, limit, and conditional orders
- [Position Management](04-Trading/position-management.md) - Track and calculate positions
- [Portfolio Management](04-Trading/portfolio-management.md) - Account and portfolio tracking
- [Transaction Tracking](04-Trading/transaction-tracking.md) - Transaction IDs and order lifecycle

---

## 05. Strategy Framework

Build algorithmic trading strategies.

- [Strategy Basics](05-Strategy-Framework/strategy-basics.md) - Strategy base class and lifecycle
- [Parameters](05-Strategy-Framework/parameters.md) - Strategy parameters and optimization
- [Events and Rules](05-Strategy-Framework/events-and-rules.md) - Event-driven trading logic
- [Indicators Integration](05-Strategy-Framework/indicators.md) - Using technical indicators
- [Position Tracking](05-Strategy-Framework/position-tracking.md) - Strategy-level position management
- [Risk Management](05-Strategy-Framework/risk-management.md) - Built-in risk controls
- [Statistics](05-Strategy-Framework/statistics.md) - PnL, commission, and performance metrics

---

## 06. Indicators

Technical analysis indicators for strategies.

- [Indicator Basics](06-Indicators/indicator-basics.md) - Using indicators in your strategies
- [Trend Indicators](06-Indicators/trend-indicators.md) - SMA, EMA, MACD, Ichimoku
- [Momentum Indicators](06-Indicators/momentum-indicators.md) - RSI, Stochastic, CCI
- [Volatility Indicators](06-Indicators/volatility-indicators.md) - Bollinger Bands, ATR
- [Volume Indicators](06-Indicators/volume-indicators.md) - OBV, VWAP, MFI
- [Custom Indicators](06-Indicators/custom-indicators.md) - Creating your own indicators

---

## 07. Storage

Persist market data and trading state.

- [Market Data Storage](07-Storage/market-data-storage.md) - Store ticks, candles, quotes
- [Entity Storage](07-Storage/entity-storage.md) - Persist securities and positions
- [Snapshot Storage](07-Storage/snapshot-storage.md) - State snapshots for fast restart
- [Storage Formats](07-Storage/storage-formats.md) - Binary vs CSV formats

---

## 08. Risk & Money Management

Control risk and calculate performance.

- [Risk Rules](08-Risk-And-Money-Management/risk-rules.md) - Risk manager and rule types
- [PnL Calculation](08-Risk-And-Money-Management/pnl-calculation.md) - Profit and loss tracking
- [Commission Calculation](08-Risk-And-Money-Management/commission-calculation.md) - Commission rules
- [Slippage Tracking](08-Risk-And-Money-Management/slippage-tracking.md) - Slippage measurement
- [Position Sizing](08-Risk-And-Money-Management/position-sizing.md) - Volume and position limits

---

## 09. Messages

Understanding the message-based architecture.

- [Message System](09-Messages/message-system.md) - Message architecture overview
- [Market Data Messages](09-Messages/market-data-messages.md) - Level1, quotes, trades, candles
- [Transaction Messages](09-Messages/transaction-messages.md) - Order registration and management
- [Lookup Messages](09-Messages/lookup-messages.md) - Security and portfolio lookup
- [System Messages](09-Messages/system-messages.md) - Connection and time synchronization

---

## 10. Advanced Features

Advanced functionality for complex scenarios.

- [Basket Orders](10-Advanced-Features/basket-orders.md) - Multi-leg order execution
- [Conditional Orders](10-Advanced-Features/conditional-orders.md) - Stop-loss, take-profit, algo orders
- [Multiple Connections](10-Advanced-Features/multiple-connections.md) - Multi-broker support
- [Security Mapping](10-Advanced-Features/security-mapping.md) - Symbol mapping across exchanges
- [Optimization](10-Advanced-Features/optimization.md) - Strategy optimization and backtesting

---

## 11. Examples

Practical examples and walkthroughs.

- [Simple Bot](11-Examples/simple-bot.md) - Minimal trading bot example
- [SMA Crossover Strategy](11-Examples/sma-crossover.md) - Complete SMA crossover implementation
- [Market Making](11-Examples/market-making.md) - Market making strategy pattern
- [Arbitrage](11-Examples/arbitrage.md) - Cross-exchange arbitrage pattern
- [Live Terminal Walkthrough](11-Examples/live-terminal.md) - Full terminal application guide

---

## 12. Reference

Complete API reference and best practices.

- [Connector API Reference](12-Reference/connector-api.md) - Complete Connector API
- [Strategy API Reference](12-Reference/strategy-api.md) - Complete Strategy API
- [Message Types Reference](12-Reference/message-types.md) - All message types
- [Level1 Fields Reference](12-Reference/level1-fields.md) - All 85+ Level1 fields
- [Error Handling](12-Reference/error-handling.md) - Error handling patterns
- [Best Practices](12-Reference/best-practices.md) - Development best practices

---

## Quick Links by Use Case

### I want to...

**Get started quickly**
→ [Quick Start Guide](01-Getting-Started/quick-start.md) → [Simple Bot Example](11-Examples/simple-bot.md)

**Connect to an exchange**
→ [Connector](02-Connection-Management/connector.md) → [Adapters](02-Connection-Management/adapters.md) → [Configuration](02-Connection-Management/configuration.md)

**Subscribe to market data**
→ [Subscription Management](02-Connection-Management/subscription-management.md) → [Level 1 Data](03-Market-Data/level1-data.md) → [Candles](03-Market-Data/candles.md)

**Execute trades**
→ [Order Management](04-Trading/order-management.md) → [Order Types](04-Trading/order-types.md)

**Build a strategy**
→ [Strategy Basics](05-Strategy-Framework/strategy-basics.md) → [SMA Crossover Example](11-Examples/sma-crossover.md)

**Use technical indicators**
→ [Indicator Basics](06-Indicators/indicator-basics.md) → [Indicators Integration](05-Strategy-Framework/indicators.md)

**Control risk**
→ [Risk Rules](08-Risk-And-Money-Management/risk-rules.md) → [Position Sizing](08-Risk-And-Money-Management/position-sizing.md)

**Store market data**
→ [Market Data Storage](07-Storage/market-data-storage.md) → [Storage Formats](07-Storage/storage-formats.md)

**Optimize strategies**
→ [Parameters](05-Strategy-Framework/parameters.md) → [Optimization](10-Advanced-Features/optimization.md)

**Understand the architecture**
→ [Platform Overview](01-Getting-Started/overview.md) → [Message System](09-Messages/message-system.md)

---

## Document Conventions

Each specification document follows this structure:

1. **Overview** - What the feature does and why you need it
2. **Key Classes/Interfaces** - Main types with file locations
3. **Properties/Methods** - Important API members
4. **Code Examples** - Practical usage from LiveTerminal sample
5. **Common Patterns** - Typical usage scenarios
6. **Dependencies** - Required features and related components
7. **See Also** - Related specifications

---

## Contributing

This documentation is based on StockSharp core version and the LiveTerminal sample project located at:
`Samples/06_Strategies/10_LiveTerminal/`

All code references point to actual implementation in the codebase for easy navigation.

---

## Version

Documentation Version: 1.0
StockSharp Version: Based on current codebase
Last Updated: 2025-10-10
