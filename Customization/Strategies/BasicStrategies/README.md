# BasicStrategies Library

This library contains simple trading strategies for StockSharp framework, following the KISS (Keep It Simple, Stupid) principle.

## Strategies

### SmaCrossStrategy

A simple SMA crossover strategy that generates buy/sell signals when fast and slow Simple Moving Averages cross.

#### Parameters
- **Fast Period**: Period for fast SMA (default: 10, optimizable: 5-25)
- **Slow Period**: Period for slow SMA (default: 20, optimizable: 10-50)
- **Trade Volume**: Volume for each trade (default: 1, optimizable: 1-10)
- **Candle Type**: Timeframe for analysis (default: 5 minutes)

#### Logic
- **Buy Signal**: Fast SMA crosses above Slow SMA
- **Sell Signal**: Fast SMA crosses below Slow SMA
- Uses market orders for immediate execution
- Automatic chart visualization with indicators

#### Features
- Clean, readable code (~130 lines)
- Comprehensive parameter optimization support
- Built-in chart integration
- Event-driven architecture
- Compatible with StockSharp backtesting

## Usage

```csharp
using BasicStrategies;

var strategy = new SmaCrossStrategy
{
    FastPeriod = 10,
    SlowPeriod = 20,
    TradeVolume = 1,
    Security = security,
    Connector = connector
};

strategy.Start();
```

## Integration

This library is referenced by the TradingWorkstation sample and can be used in any StockSharp application by adding the project reference.