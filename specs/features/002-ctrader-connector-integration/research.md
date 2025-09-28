# Research: cTrader Connector Integration

## Research Overview

This document consolidates technical research for implementing a cTrader connector for the StockSharp platform, integrating cTrader's OpenAPI.Net SDK with StockSharp's message-based architecture.

## cTrader OpenAPI.Net SDK Integration

### Decision: Use Official cTrader.OpenAPI.Net NuGet Package
**Rationale**:
- Official SDK maintained by Spotware (cTrader developers)
- Written using RX streams for efficient async operations
- Uses channels and array pools to minimize allocations
- Targets .NET 6 (compatible with our requirements)
- Provides both TCP/SSL and WebSocket connection options

**Alternatives Considered**:
- Direct Protocol Buffer implementation - Rejected due to complexity and maintenance burden
- REST API wrapper - Rejected due to limited functionality compared to full SDK
- Third-party SDK - Rejected due to lack of official support

### Key SDK Capabilities Identified:
- OAuth2 authentication flow with token refresh
- Real-time market data streaming (spots, depth, trades)
- Historical data retrieval with pagination
- Order lifecycle management (place, modify, cancel)
- Portfolio and position tracking
- Built-in rate limiting and reconnection logic

## StockSharp MessageAdapter Architecture

### Decision: Inherit from AsyncMessageAdapter with Partial Classes
**Rationale**:
- Follows established pattern from BitStamp and other connectors
- AsyncMessageAdapter provides async/await support for modern APIs
- Partial classes allow logical separation of concerns
- Message-based architecture ensures loose coupling and testability

**Core Message Types Mapping**:
- `ConnectMessage` → cTrader OAuth2 authentication
- `MarketDataMessage` → cTrader subscription requests
- `ExecutionMessage` → Order states and trade confirmations
- `QuoteChangeMessage` → Market depth updates
- `SecurityMessage` → Symbol definitions and specifications
- `PortfolioMessage` → Account balance information
- `PositionChangeMessage` → Position updates and P&L

### Partial Class Organization:
- `CTraderMessageAdapter.cs` - Core connection and lifecycle
- `CTraderMessageAdapter_MarketData.cs` - Market data subscriptions
- `CTraderMessageAdapter_Transaction.cs` - Trading operations
- `CTraderMessageAdapter_Settings.cs` - Configuration properties

## OAuth2 Authentication Implementation

### Decision: Implement OAuth2 with Secure Token Storage
**Rationale**:
- cTrader requires OAuth2 for production API access
- Secure token storage prevents credential exposure
- Automatic refresh ensures uninterrupted trading
- Follows security best practices for financial applications

**Implementation Approach**:
- Use SecureString for sensitive configuration data
- Implement token refresh logic in background
- Handle authentication failures gracefully with user notifications
- Support both demo and live environment configurations

**Alternatives Considered**:
- Basic API key authentication - Not available for cTrader API
- Session-based authentication - Not suitable for long-running connections

## Performance Optimization for <100ms Latency

### Decision: Multi-layered Performance Strategy
**Rationale**:
- Financial trading requires low latency for competitive advantage
- cTrader SDK already optimized with RX streams and pooling
- Additional optimizations needed at application level

**Performance Techniques**:
1. **Connection Level**:
   - TCP with SSL (primary) for lowest latency
   - WebSocket fallback for compatibility
   - Connection pooling for multiple accounts

2. **Data Processing**:
   - Pre-allocated object pools for frequent allocations
   - Efficient message conversion using extension methods
   - Minimal allocations in hot paths

3. **Threading**:
   - Dedicated threads for market data processing
   - Async/await for non-blocking operations
   - ThreadSafe collections for concurrent access

4. **Benchmarking**:
   - Performance counters for latency measurement
   - Memory allocation tracking
   - Throughput monitoring for optimization validation

## Error Handling and Reconnection Strategies

### Decision: Multi-level Error Handling with Graceful Degradation
**Rationale**:
- Trading systems require high reliability
- Network issues are common in financial markets
- Users need clear feedback on system status

**Error Handling Levels**:
1. **Connection Errors**:
   - Automatic reconnection with exponential backoff
   - Connection state notifications to user
   - Fallback from TCP to WebSocket if needed

2. **Authentication Errors**:
   - Complete disconnection with user alert (per clarification)
   - Token refresh attempts before failure
   - Clear error messages for troubleshooting

3. **Market Data Errors**:
   - Individual subscription retry logic
   - Subscription failure notifications
   - Graceful handling of unsupported symbols

4. **Trading Errors**:
   - Order rejection handling with reason codes
   - Position synchronization on reconnection
   - Trade confirmation validation

## Message Type Mappings

### Market Data Message Flows:
```
cTrader ProtoOASpotEvent → StockSharp Level1ChangeMessage
cTrader ProtoOADepthEvent → StockSharp QuoteChangeMessage
cTrader ProtoOATickData → StockSharp ExecutionMessage (Ticks)
cTrader ProtoOATrendBar → StockSharp TimeFrameCandleMessage
```

### Trading Message Flows:
```
StockSharp OrderRegisterMessage → cTrader ProtoOANewOrderReq
cTrader ProtoOAExecutionEvent → StockSharp ExecutionMessage (Transactions)
StockSharp OrderCancelMessage → cTrader ProtoOACancelOrderReq
cTrader ProtoOAOrderEvent → StockSharp ExecutionMessage (Order Updates)
```

### Portfolio Message Flows:
```
cTrader ProtoOATrader → StockSharp PortfolioMessage
cTrader ProtoOAPosition → StockSharp PositionChangeMessage
cTrader ProtoOAAsset → StockSharp SecurityMessage
```

## Configuration and Settings

### Decision: Follow StockSharp Configuration Patterns
**Rationale**:
- Consistency with existing connectors
- Localization support through LocalizedStrings
- UI integration with StockSharp Designer
- Secure credential management

**Configuration Properties**:
- `ApplicationId`: OAuth2 client ID
- `ApplicationSecret`: OAuth2 client secret (SecureString)
- `Environment`: Demo/Live environment selection
- `BalanceCheckInterval`: Portfolio refresh frequency
- `ReConnectionSettings`: Automatic reconnection configuration

## Testing Strategy

### Decision: Limited Unit Testing with Comprehensive Integration Tests
**Rationale**:
- As specified in requirements - limited unit test coverage
- Focus on happy path scenarios for rapid development
- Integration tests provide better coverage for connector functionality
- Mock objects for offline testing

**Test Categories**:
1. **Connection Tests**: Authentication, connection establishment, heartbeat
2. **Market Data Tests**: Subscription handling, data parsing, error scenarios
3. **Trading Tests**: Order placement, execution, cancellation workflows
4. **Integration Tests**: End-to-end workflows with demo account

## Implementation Dependencies

### Required NuGet Packages:
```xml
<PackageReference Include="cTrader.OpenAPI.Net" Version="1.4.4" />
<!-- StockSharp packages imported via common_connectors.props -->
```

### Project References:
- StockSharp.Messages
- StockSharp.BusinessEntities
- Ecng.Common
- Ecng.Collections
- Ecng.Serialization

## Security Considerations

### Decision: Defense-in-Depth Security Approach
**Rationale**:
- Financial data requires maximum security
- Regulatory compliance considerations
- Protection against credential theft

**Security Measures**:
- OAuth2 with secure token storage
- SSL/TLS for all communications
- No credential logging or storage in plain text
- Rate limiting compliance to prevent API abuse
- Input validation for all user data
- Secure disposal of sensitive objects

## Conclusion

The research establishes a clear technical foundation for implementing the cTrader connector using established StockSharp patterns while leveraging the official cTrader OpenAPI.Net SDK. The approach balances performance requirements (<100ms latency) with maintainability and security considerations. The message-based architecture ensures seamless integration with the existing StockSharp ecosystem while providing full trading capabilities.

**Next Phase**: Design detailed data models and API contracts based on this research foundation.