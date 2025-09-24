# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture Overview

StockSharp is a comprehensive trading platform with multi-tiered architecture:

### Core Components
- **BusinessEntities**: Core trading entities (Security, Order, Trade, Portfolio, Position)
- **Messages**: Message-based communication system for data exchange
- **Algo**: Main algorithmic trading engine with adapters, connectors, and strategies
- **Configuration**: System configuration management
- **Localization**: Multi-language support system

### Key Architectural Patterns
- **Message-Based Architecture**: All data flows through typed messages (ExecutionMessage, QuoteChangeMessage, etc.)
- **Adapter Pattern**: Market data and trading adapters wrap different broker/exchange APIs
- **Provider Pattern**: Abstracted access to securities, portfolios, and market data
- **Strategy Framework**: Base classes for algorithmic strategies with event-driven execution

### Major Modules
- **Connectors**: Exchange/broker integrations (60+ supported exchanges including Binance, Interactive Brokers, MT4/MT5)
- **Samples**: Organized examples by category (Basic, Candles, Storage, Indicators, Strategies, Testing)
- **Media**: Resource management and localized media files
- **Diagram.Core**: Visual strategy designer components
- **Analytics**: Multi-language analytics (C#, F#, Python support)

## Development Commands

### Build
```bash
# Build entire solution
dotnet build StockSharp.sln

# Build specific project
dotnet build Algo/Algo.csproj

# Release build
dotnet build StockSharp.sln -c Release
```

### Testing
```bash
# Run all tests
dotnet test Tests/Tests.csproj

# Run tests with specific target framework
dotnet test Tests/Tests.csproj --framework net8.0
dotnet test Tests/Tests.csproj --framework net9.0

# Run specific test class
dotnet test Tests/Tests.csproj --filter "TestClassName"
```

### Package Management
- All package versions centralized in `common_versions.props`
- Test projects use `common_target_tests.props` for MSTest framework
- Framework targets: net8.0 and net9.0 for test projects

## Project Structure Guidelines

### Core Dependencies
- **Ecng Libraries**: Foundation libraries (Ecng.Common, Ecng.Collections, etc.)
- **Math.NET Numerics**: Mathematical computations
- **Protobuf**: Message serialization
- **GeneticSharp**: Genetic algorithm optimization

### Target Frameworks
- Standard libraries: netstandard2.0/2.1 + net6.0-windows
- Test projects: net8.0, net9.0
- Uses C# 12.0 language features

### Connector Development
- Each connector is a separate project in `Connectors/` directory  
- Inherit from `MessageAdapter` for market data/trading adapters
- Implement message handling for supported data types
- Follow naming pattern: ConnectorName (e.g., Binance, BitStamp)

### Strategy Development
- Inherit from `Strategy` base class in Algo namespace
- Use event-driven programming model with rules and subscriptions
- Access market data through `Connector` and subscriptions
- Sample strategies available in `Samples/06_Strategies/`

## Testing Strategy
- Unit tests in `Tests/` project using MSTest framework
- Comprehensive test coverage for:
  - Market emulation and backtesting
  - Indicator calculations  
  - Storage systems
  - Strategy execution
  - Risk management
  - Statistical analysis

## Key Files to Understand
- `Algo/Connector.cs`: Main trading connector interface
- `BusinessEntities/Security.cs`: Security definition model
- `Messages/`: Core message types for system communication
- `Samples/`: Working examples for different use cases

## Sample Categories
1. **Basic**: Connection, market depth, orders
2. **Candles**: Real-time and historical candle data
3. **Storage**: Local, remote, and Hydra server data storage
4. **Indicators**: Technical analysis indicators
5. **Chart**: Charting and visualization
6. **Strategies**: Algorithmic trading strategies
7. **Testing**: Backtesting and optimization
8. **Advanced**: Multi-connection and data storage
9. **Cross-Platform**: Console applications

## When Working with Strategies
- Use `MarketRuleHelper` for event-driven rule creation
- Subscribe to market data through `Connector.Subscribe()`
- Handle order states and execution through event handlers
- Implement proper risk management and position sizing
- Test strategies using historical data before live trading