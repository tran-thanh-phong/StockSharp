# cTrader Connector Feature Specification

## Overview

This document outlines the comprehensive specification for integrating cTrader as a new Market Data & Transaction Connector into the StockSharp trading platform. The connector will provide full trading capabilities with limited unit tests covering happy path scenarios.

## Project Information

- **Library Project Location**: `Customization/Connectors/CTrader/`
- **Test Project Location**: `Customization/Tests/Tests.Connectors.CTrader/`
- **Type**: Class library targeting .NET 6
- **Architecture**: Message-based connector following StockSharp patterns
- **Primary SDK**: cTrader OpenAPI.Net (official C# SDK)

## Research Findings

### cTrader Open API Capabilities

**Connection Methods:**
- TCP with SSL (primary)
- WebSocket protocol support
- OAuth2 authentication flow
- Protobuf message serialization (high performance)
- JSON serialization alternative

**Market Data Features:**
- Real-time spot price events (`ProtoOASubscribeSpotsReq`)
- Market depth/order book streaming (`ProtoOASubscribeDepthQuotesReq`)
- Live trend bars/candles subscription
- Historical OHLCV data retrieval
- Trade tick data streaming

**Trading Features:**
- Full order lifecycle management
- Market, limit, and stop orders
- Position tracking and management
- Portfolio/account balance monitoring
- Commission and fee tracking

### StockSharp Architecture Analysis

Based on BitStamp connector analysis, the following patterns will be implemented:

**Core Structure:**
- Main adapter class inheriting from `AsyncMessageAdapter`
- Partial classes for market data, transactions, and settings
- Native API wrapper layer for cTrader SDK integration
- Message-based communication system

**Required Message Types:**
- `ExecutionMessage` for trades and orders
- `QuoteChangeMessage` for market depth
- `Level1ChangeMessage` for price updates
- `SecurityMessage` for instrument definitions
- `PortfolioMessage` and `PositionChangeMessage` for account data

## Implementation Plan

### Phase 1: Core Infrastructure

#### 1.1 Project Setup
```
Customization/Connectors/CTrader/
├── CTrader.csproj                      # .NET 6 class library
├── CTraderMessageAdapter.cs            # Main adapter class
├── CTraderMessageAdapter_MarketData.cs # Market data handling
├── CTraderMessageAdapter_Transaction.cs # Trading operations
├── CTraderMessageAdapter_Settings.cs   # Configuration
├── CTraderOrderCondition.cs           # Custom order conditions
├── Native/
│   ├── OpenApiClient.cs               # cTrader SDK wrapper
│   ├── Extensions.cs                  # Format conversion utilities
│   └── Models/                        # cTrader-specific data models
├── Properties/
│   ├── AssemblyInfo.cs
│   └── usings.cs
└── Tests/                             # Limited unit tests
    ├── ConnectionTests.cs
    ├── MarketDataTests.cs
    └── TradingTests.cs
```

#### 1.2 Dependencies
```xml
<PackageReference Include="cTrader.OpenAPI.Net" Version="1.4.4" />
```

#### 1.3 Project Configuration
- Import `common_connectors.props` for StockSharp dependencies
- Target framework: `net6.0`
- Enable nullable reference types
- Include localization support

### Phase 2: Connection Management

#### 2.1 Authentication System
- OAuth2 application registration flow
- Client ID and secret management
- Token refresh mechanism
- Secure credential storage

#### 2.2 Connection Infrastructure
- TCP with SSL connection (primary)
- WebSocket fallback support
- Connection state management
- Automatic reconnection logic
- Heartbeat and keep-alive mechanisms

#### 2.3 Error Handling
- StockSharp error message integration
- Connection loss detection
- Rate limiting compliance
- Graceful degradation strategies

### Phase 3: Market Data Implementation

#### 3.1 Real-time Data Streaming
```csharp
// Market depth subscription
protected override async ValueTask OnMarketDepthSubscriptionAsync(
    MarketDataMessage mdMsg, CancellationToken cancellationToken)

// Tick data subscription
protected override async ValueTask OnTicksSubscriptionAsync(
    MarketDataMessage mdMsg, CancellationToken cancellationToken)

// Spot price updates
private void OnSpotEvent(ProtoOASpotEvent spotEvent)
```

#### 3.2 Historical Data Retrieval
```csharp
// Historical candles
protected override async ValueTask OnTFCandlesSubscriptionAsync(
    MarketDataMessage mdMsg, CancellationToken cancellationToken)

// Time-range data requests
private async Task<IEnumerable<Candle>> GetHistoricalCandles(
    string symbol, TimeSpan timeFrame, DateTime from, DateTime to)
```

#### 3.3 Symbol Management
```csharp
// Security lookup
public override async ValueTask SecurityLookupAsync(
    SecurityLookupMessage lookupMsg, CancellationToken cancellationToken)

// Symbol conversion utilities
public static class CTraderExtensions
{
    public static SecurityId ToStockSharp(this string cTraderSymbol)
    public static string ToCTrader(this SecurityId securityId)
}
```

### Phase 4: Trading Operations

#### 4.1 Order Management
```csharp
// Order registration
public override async ValueTask RegisterOrderAsync(
    OrderRegisterMessage regMsg, CancellationToken cancellationToken)

// Order cancellation
public override async ValueTask CancelOrderAsync(
    OrderCancelMessage cancelMsg, CancellationToken cancellationToken)

// Order status tracking
public override async ValueTask OrderStatusAsync(
    OrderStatusMessage statusMsg, CancellationToken cancellationToken)
```

#### 4.2 Position and Portfolio Tracking
```csharp
// Portfolio lookup
public override async ValueTask PortfolioLookupAsync(
    PortfolioLookupMessage lookupMsg, CancellationToken cancellationToken)

// Position updates
private void OnPositionEvent(ProtoOAPositionEvent positionEvent)

// Account balance monitoring
private void OnAccountAuthRes(ProtoOAAccountAuthRes accountAuth)
```

#### 4.3 Trade Execution Processing
```csharp
// Trade confirmations
private void OnExecutionEvent(ProtoOAExecutionEvent executionEvent)

// Order state changes
private void OnOrderEvent(ProtoOAOrderEvent orderEvent)
```

### Phase 5: Data Type Support

#### 5.1 Supported Market Data Types
- `DataType.Ticks` - Real-time trade data
- `DataType.MarketDepth` - Order book updates
- `DataType.Level1` - Best bid/ask prices
- `DataType.Candles` - OHLCV data (multiple timeframes)

#### 5.2 Supported Timeframes
```csharp
public static IEnumerable<TimeSpan> AllTimeFrames { get; } = new[]
{
    TimeSpan.FromMinutes(1),
    TimeSpan.FromMinutes(5),
    TimeSpan.FromMinutes(15),
    TimeSpan.FromMinutes(30),
    TimeSpan.FromHours(1),
    TimeSpan.FromHours(4),
    TimeSpan.FromDays(1),
    TimeSpan.FromDays(7),
    TimeSpan.FromDays(30)
};
```

#### 5.3 Security Types
- Forex pairs (major, minor, exotic)
- CFDs (indices, commodities, cryptocurrencies)
- Stocks and ETFs (if supported by broker)

### Phase 6: Configuration and Settings

#### 6.1 Connection Settings
```csharp
public class CTraderMessageAdapter : AsyncMessageAdapter
{
    [CategoryLoc(LocalizedStrings.ConnectionKey)]
    [DisplayNameLoc(LocalizedStrings.ApplicationIdKey)]
    public string ApplicationId { get; set; }

    [CategoryLoc(LocalizedStrings.ConnectionKey)]
    [DisplayNameLoc(LocalizedStrings.ApplicationSecretKey)]
    public SecureString ApplicationSecret { get; set; }

    [CategoryLoc(LocalizedStrings.ConnectionKey)]
    [DisplayNameLoc(LocalizedStrings.EnvironmentKey)]
    public CTraderEnvironment Environment { get; set; } = CTraderEnvironment.Demo;
}
```

#### 6.2 Board Configuration
```csharp
public override string[] AssociatedBoards { get; } = new[] { BoardCodes.CTrader };
```

### Phase 7: Testing Strategy

#### 7.1 Unit Test Coverage (Limited Scope)
```csharp
[TestClass]
public class CTraderConnectionTests
{
    [TestMethod]
    public async Task ConnectAsync_ValidCredentials_EstablishesConnection()

    [TestMethod]
    public async Task DisconnectAsync_ActiveConnection_DisconnectsCleanly()
}

[TestClass]
public class CTraderMarketDataTests
{
    [TestMethod]
    public async Task SubscribeTicks_ValidSymbol_ReceivesTickData()

    [TestMethod]
    public async Task SubscribeMarketDepth_ValidSymbol_ReceivesOrderBook()
}

[TestClass]
public class CTraderTradingTests
{
    [TestMethod]
    public async Task RegisterOrder_MarketOrder_ExecutesSuccessfully()

    [TestMethod]
    public async Task CancelOrder_ActiveOrder_CancelsSuccessfully()
}
```

#### 7.2 Integration Test Considerations
- Demo account testing environment
- Mock API responses for unit tests
- Connection resilience testing
- Error scenario validation

## Technical Requirements

### Performance Considerations
- Async/await patterns for non-blocking operations
- Efficient memory usage with object pooling
- Minimal allocations in hot paths
- Thread-safe concurrent operations

### Error Handling Strategy
- Comprehensive exception mapping
- Graceful connection recovery
- Rate limiting compliance
- Logging integration with StockSharp

### Resource Management
- Proper disposal of connections and resources
- Connection pooling for multiple accounts
- Memory leak prevention
- Clean shutdown procedures

## Compliance and Security

### Authentication Security
- OAuth2 best practices implementation
- Secure token storage
- Automatic token refresh
- Credential encryption at rest

### API Compliance
- Rate limiting adherence
- Terms of service compliance
- Data usage policy compliance
- Market data redistribution rules

## Success Criteria

### Functional Requirements
1. ✅ Establish secure connection to cTrader Open API
2. ✅ Subscribe to real-time market data (ticks, depth, level1)
3. ✅ Retrieve historical candle data with time range support
4. ✅ Execute market and limit orders successfully
5. ✅ Track order status and position changes
6. ✅ Monitor portfolio and account balances
7. ✅ Handle connection loss and automatic reconnection

### Technical Requirements
1. ✅ Follow StockSharp message-based architecture
2. ✅ Implement proper async/await patterns
3. ✅ Ensure thread-safe operations
4. ✅ Maintain consistent error handling
5. ✅ Provide comprehensive logging
6. ✅ Support configuration management

### Quality Requirements
1. ✅ Pass all happy path unit tests
2. ✅ Demonstrate stable connection handling
3. ✅ Show acceptable performance characteristics
4. ✅ Maintain code quality standards
5. ✅ Include proper documentation

## Future Enhancements

### Phase 2 Considerations
- Advanced order types (OCO, trailing stop)
- Multi-account support
- Enhanced position sizing algorithms
- Advanced risk management features
- Extended historical data retention
- Custom indicator integration

### Integration Opportunities
- StockSharp Designer visual strategy support
- Hydra data storage integration
- Risk management system integration
- Portfolio optimization tools
- Advanced charting capabilities

---

**Document Version**: 1.0
**Created**: 2025-09-28
**Status**: Approved for Implementation