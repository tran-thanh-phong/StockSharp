# Auto Trading Basic Flow Feature

## Overview

This feature implements a complete auto trading application demonstrating the full trading lifecycle using StockSharp as the trading engine with CTrader connector. It serves as a comprehensive learning resource to understand how StockSharp components work together in a production trading system.

## Purpose

This feature is designed to help developers:
- Understand StockSharp's message-based architecture
- Learn connector integration patterns
- Master order lifecycle management
- Implement risk management with TP/SL protective orders
- Build production-ready trading UIs
- Handle external trading signals
- Process exchange-based market data signals

## Documentation Structure

This feature specification is organized into the following documents:

### 1. [spec.md](./spec.md) - Main Feature Specification
Complete feature specification covering:
- System architecture overview
- Component specifications
- Data models
- Implementation flows
- Error handling
- Testing scenarios
- Future enhancements

### 2. [architecture.md](./architecture.md) - System Architecture
Detailed architectural documentation including:
- Layered architecture
- Component diagram
- Data flow architecture
- Threading model
- Message flow
- State management
- Configuration hierarchy
- Deployment architecture

### 3. [components.md](./components.md) - Component Breakdown
Detailed component implementation specifications:
- Signal Manager
- Trading Strategy
- Risk Calculator
- Webhook Server
- UI Components
- Integration patterns

## Key Features

### Signal Processing
- **TradingView Webhooks**: HTTP endpoint to receive trading signals from TradingView alerts
- **Exchange Signals**: Real-time market data processing with technical indicators
- **Signal Normalization**: Unified signal format from multiple sources

### Order Management
- **Order Registration**: Create and submit orders to broker
- **Order Monitoring**: Real-time order state tracking
- **Order Cancellation**: Cancel active orders
- **Order History**: Complete audit trail

### Risk Management
- **Position Sizing**: Automatic calculation based on risk percentage
- **Protective Orders**: Take Profit and Stop Loss management
- **Risk Rules**: Maximum position size, daily loss limits
- **Server/Local TP/SL**: Support for both broker-side and StockSharp-managed protective orders

### User Interface
- **Connection Panel**: Broker connection and configuration
- **Order Grid**: Real-time order display and management
- **Trade Grid**: Trade history and PnL tracking
- **Portfolio Grid**: Account balance, equity, and margin monitoring
- **Log Monitor**: Real-time application logging

## Technology Stack

- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Trading Engine**: StockSharp 5.x
- **Broker Connector**: CTrader (OpenAPI.Net)
- **Webhook Server**: ASP.NET Core Minimal API
- **Logging**: StockSharp LogManager with file and GUI listeners
- **Data Storage**: CSV files and binary storage

## Code References

This feature leverages existing StockSharp components and samples:

### Core StockSharp Components
- `Algo/Connector.cs` - Main trading connector
- `Algo/Strategies/Strategy.cs` - Strategy base class
- `Algo/Strategies/Protective/ProtectiveController.cs` - TP/SL management
- `Algo/Risk/RiskManager.cs` - Risk management framework
- `Messages/` - Message type definitions

### Custom Components
- `Customization/Connectors/CTrader/` - CTrader connector implementation
- `Customization/Strategies/BasicStrategies/SmaCrossStrategy.cs` - Example strategy

### Reference Samples
- `Samples/01_Basic/03_Orders/` - Basic order management
- `Samples/06_Strategies/10_LiveTerminal/` - Complete trading terminal reference

## Quick Start Guide

### Prerequisites
1. .NET 8.0 SDK installed
2. Visual Studio 2022 or VS Code
3. CTrader demo or live account
4. CTrader OpenAPI credentials (App ID, Secret, Access Token)

### Setup Steps
1. Review the [spec.md](./spec.md) for complete feature understanding
2. Study the [architecture.md](./architecture.md) for system design
3. Read [components.md](./components.md) for implementation details
4. Reference the code samples mentioned above
5. Implement components following the specifications

### Implementation Order
1. **Phase 1**: Set up infrastructure
   - Create project structure
   - Initialize Connector and LogManager
   - Build basic UI with MainWindow

2. **Phase 2**: Implement Signal Manager
   - Create SignalManager class
   - Implement webhook endpoint
   - Add market data signal processing

3. **Phase 3**: Build Trading Strategy
   - Create SignalDrivenStrategy class
   - Implement signal handling
   - Add order management logic

4. **Phase 4**: Add Risk Management
   - Implement RiskCalculator
   - Add position sizing logic
   - Integrate protective orders

5. **Phase 5**: Complete UI
   - Add all UI windows
   - Bind to connector events
   - Add configuration panels

6. **Phase 6**: Testing & Refinement
   - Test with demo account
   - Validate all flows
   - Add error handling
   - Performance optimization

## Trading Flow Example

### TradingView Signal Flow
```
1. TradingView sends alert → Webhook POST /api/tradingview/webhook
2. Webhook server receives JSON payload
3. SignalManager processes and validates signal
4. Strategy receives signal event
5. Strategy validates trading conditions
6. RiskCalculator computes position size
7. Order created and registered via Connector
8. Order sent to CTrader API
9. Order accepted → UI updates
10. Order filled → Trade created
11. TP/SL orders created automatically
12. Position tracked until closed
```

### Exchange Signal Flow
```
1. Connector subscribes to candle data
2. Candles received and processed by indicators
3. Indicator crossover detected
4. SignalManager generates trading signal
5. [Same as steps 4-12 above]
```

## Key Learning Outcomes

After implementing this feature, you will understand:

### StockSharp Architecture
- Message-based communication patterns
- Event-driven processing model
- Adapter pattern for connectors
- Strategy framework design

### Order Management
- Order lifecycle (pending → active → filled/cancelled)
- Transaction vs market data messages
- Order state management
- Trade execution handling

### Risk Management
- Position sizing calculations
- Protective order strategies
- Risk rule enforcement
- Portfolio monitoring

### UI Development
- StockSharp WPF controls (OrderGrid, TradeGrid, PortfolioGrid)
- Real-time data binding
- Thread-safe UI updates (GuiAsync pattern)
- Multi-window coordination

## Testing Scenarios

### Manual Testing Checklist
- [ ] Connect to CTrader demo account
- [ ] Send test webhook from TradingView
- [ ] Place market order and verify execution
- [ ] Place limit order and verify acceptance
- [ ] Test TP/SL automatic creation
- [ ] Test TP triggering (profitable trade)
- [ ] Test SL triggering (losing trade)
- [ ] Test order cancellation
- [ ] Test reconnection after disconnect
- [ ] Test with insufficient balance
- [ ] Test with invalid symbols
- [ ] Test risk rule enforcement

### Integration Testing
- [ ] Connector connection/disconnection
- [ ] Order registration and state updates
- [ ] Trade execution and position tracking
- [ ] Protective order lifecycle
- [ ] Webhook endpoint response
- [ ] Error handling and recovery

## Performance Characteristics

### Expected Performance
- **Order Latency**: 100-500ms (network + broker processing)
- **Signal Processing**: <10ms per signal
- **UI Update Rate**: 60 FPS (WPF rendering)
- **Memory Usage**: ~100MB baseline + market data cache
- **Concurrent Positions**: 10-20 positions

### Optimization Tips
- Use async/await for all I/O operations
- Minimize UI thread work (delegate to background)
- Use concurrent collections for shared state
- Batch UI updates where possible
- Configure appropriate heartbeat intervals

## Security Considerations

### Credential Management
- Use `SecureString` for passwords and tokens
- Never log credentials
- Store encrypted configuration (future enhancement)

### Webhook Security
- HTTPS only in production
- IP whitelist for webhook endpoint
- Request signature verification (future)
- Rate limiting to prevent abuse

### Trading Safety
- Order validation before submission
- Risk rule enforcement
- Position size limits
- Daily loss limits

## Future Enhancements

### Planned Features
1. **Multi-Strategy Support**: Run multiple strategies concurrently
2. **Backtesting Integration**: Test strategies on historical data
3. **Chart Visualization**: Real-time charts with trades overlay
4. **Performance Analytics**: Win rate, profit factor, Sharpe ratio
5. **Alert System**: Email/SMS notifications
6. **Strategy Optimization**: Genetic algorithm parameter tuning
7. **Database Storage**: Persist trades and performance metrics
8. **Web Dashboard**: Remote monitoring interface

### Scalability Enhancements
- Order conflict resolution for multi-strategy
- Shared market data subscriptions
- Aggregated risk management
- Parallel signal processing

## Common Issues and Solutions

### Connection Issues
**Problem**: Cannot connect to CTrader
**Solutions**:
- Verify credentials (App ID, Secret, Access Token)
- Check network connectivity
- Ensure correct environment (Demo/Live)
- Review logs for specific error messages

### Order Rejection
**Problem**: Orders rejected by broker
**Solutions**:
- Verify sufficient account balance
- Check minimum/maximum position sizes
- Validate symbol is tradable
- Ensure market is open

### Webhook Not Receiving Signals
**Problem**: TradingView webhooks not arriving
**Solutions**:
- Check webhook URL is accessible (use ngrok for local testing)
- Verify TradingView alert configuration
- Check webhook endpoint logs
- Test with Postman first

### TP/SL Not Created
**Problem**: Protective orders not created after fill
**Solutions**:
- Verify EnableTPSL parameter is true
- Check signal has TP/SL prices
- Review protective controller logs
- Ensure broker supports TP/SL orders

## Support and Resources

### Documentation
- [StockSharp Official Docs](https://doc.stocksharp.com)
- [CTrader OpenAPI Documentation](https://help.ctrader.com/open-api/)
- [TradingView Webhook Guide](https://www.tradingview.com/support/solutions/43000529348-about-webhooks/)

### Sample Code
- StockSharp Samples: `Samples/` directory
- CTrader Connector: `Customization/Connectors/CTrader/`
- Basic Strategy: `Customization/Strategies/BasicStrategies/`

### Community
- StockSharp Forum
- CTrader Community
- GitHub Issues

## Contributing

When extending this feature:
1. Follow StockSharp coding conventions
2. Add comprehensive logging
3. Include error handling
4. Update documentation
5. Add unit tests where applicable
6. Test with demo account before live trading

## License

This feature follows the StockSharp licensing model.

## Changelog

### Version 1.0.0 (Initial Specification)
- Complete feature specification
- System architecture design
- Component breakdown
- Reference implementation guide
- Testing scenarios
- Documentation structure

---

**Note**: This is a specification document for a learning feature. Always test thoroughly with a demo account before using any automated trading system with real money. Trading carries risk, and you should never trade with money you cannot afford to lose.
