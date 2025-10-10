# Strategy Framework Specifications

## Overview

This directory contains comprehensive specifications for StockSharp's Strategy Framework, the foundation for building algorithmic trading strategies.

## Contents

### [1. strategy-basics.md](strategy-basics.md)
**Strategy Fundamentals**

Complete guide to the Strategy base class:
- Strategy lifecycle (Start, OnStarted, OnStopping, Stop)
- ProcessState enum and state management
- Core properties (Security, Portfolio, Volume, Position)
- Connector assignment and integration
- Complete SmaCrossStrategy example
- Working time management
- Trading modes and state checking
- Best practices and common patterns

**Key Topics:**
- Lifecycle methods: `OnStarted()`, `OnStopping()`, `OnStopped()`
- State management: `ProcessStates.Started`, `ProcessStates.Stopping`, `ProcessStates.Stopped`
- Strategy readiness: `IsFormed`, `IsOnline`, `IsFormedAndOnlineAndAllowTrading()`
- Configuration: `DisposeOnStop`, `CancelOrdersWhenStopping`, `WaitAllTrades`

### [2. parameters.md](parameters.md)
**Strategy Parameters and Configuration**

Comprehensive guide to the StrategyParam<T> system:
- Creating parameters with `Param()` method
- Configuration methods (SetDisplay, SetCanOptimize, SetOptimize)
- Validation (SetGreaterThanZero, SetNotNegative, SetRange, SetRequired)
- Visibility control (SetHidden, SetReadOnly, SetBasic)
- Parameter persistence and optimization
- Complete examples from SmaCrossStrategy

**Key Topics:**
- Type-safe generic parameters
- Method chaining for configuration
- Validation attributes and rules
- Optimization range configuration
- UI integration with Display attributes
- Nullable parameter handling

### [3. events-and-rules.md](events-and-rules.md)
**Event-Driven Trading with Market Rules**

Complete guide to StockSharp's rule system:
- Market rule patterns (WhenMatched, WhenCanceled, WhenRegistered)
- Candle-based rules (WhenCandleFinished)
- Rule operators (Do, Once, Until, Apply)
- Rule composition (And, Or, Exclusive)
- Practical examples (bracket orders, multi-order coordination)
- Best practices and common pitfalls

**Key Topics:**
- Order state rules: `WhenMatched()`, `WhenCanceled()`, `WhenRegistered()`
- Trade rules: `WhenNewTrade()`, `WhenAllTrades()`, `WhenPartiallyMatched()`
- Failure handling: `WhenRegisterFailed()`, `WhenCancelFailed()`
- Rule lifecycle: `Do()`, `Once()`, `Until()`, `Apply()`
- Logical operators: `And()`, `Or()`, `Exclusive()`

### [4. indicators.md](indicators.md)
**Using Indicators in Strategies**

Guide to integrating technical indicators:
- Strategy.Indicators collection and formation tracking
- IsFormed property and indicator readiness
- Bind method for automatic indicator processing
- Manual indicator processing
- Common indicators (SMA, EMA, RSI, MACD, Bollinger Bands, ATR)
- Multi-indicator strategies
- Custom indicator creation

**Key Topics:**
- Indicator formation: `IsFormed`, `Indicators.Add()`
- Automatic processing: `Bind(indicator, ProcessCandle)`
- Multiple indicators: `Bind(ind1, ind2, ind3, ProcessCandle)`
- Accessing values: `GetCurrentValue()`, `Container`
- Built-in indicators: Moving Averages, Oscillators, Volatility, Trend
- Complete multi-indicator strategy example

### [5. position-tracking.md](position-tracking.md)
**Strategy Position Management**

Comprehensive position tracking guide:
- Strategy.Position property
- GetPositionValue method for multi-security tracking
- Position calculations (orders vs trades)
- Multi-security and multi-portfolio positions
- Position-based trading logic
- Position change events
- Risk management with position limits

**Key Topics:**
- Current position: `Position` property
- Specific positions: `GetPositionValue(security, portfolio)`
- Position in trades: `MyTrade.Position`
- Multi-security tracking
- Position-based decisions
- Flattening and reversing positions
- Scale in/out patterns

### [6. risk-management.md](risk-management.md)
**Built-in Risk Controls**

Complete guide to risk management:
- Strategy.RiskManager and RiskRules
- Risk actions (ClosePositions, StopTrading, CancelOrders)
- Built-in risk rules:
  - MaxDrawdownRule
  - MaxOrdersRule
  - MaxPositionRule
  - PnLRule
  - CommissionRule
  - OrderVolumeRule
  - OrderFrequencyRule
- Custom risk rules
- Risk event handling
- Complete risk-aware strategy example

**Key Topics:**
- Risk rule configuration in constructor
- Rule actions and triggers
- Dynamic risk rules
- Custom risk rule implementation
- Risk event monitoring
- Best practices for different environments (backtest vs live)

### [7. statistics.md](statistics.md)
**Performance Metrics and Tracking**

Guide to strategy statistics:
- StatisticManager and performance tracking
- Core metrics (PnL, Commission, Slippage, Latency)
- PnLManager for profit/loss calculation
- Statistic parameters (win rate, profit factor, Sharpe ratio, drawdown)
- Performance events (PnLChanged, CommissionChanged)
- Statistics persistence
- Complete statistics-aware strategy example

**Key Topics:**
- P&L tracking: `PnL`, `PnLManager`, `RealizedPnL`, `UnrealizedPnL`
- Cost tracking: `Commission`, `Slippage`
- Performance metrics: Win rate, Profit factor, Sharpe ratio
- Risk metrics: Maximum drawdown, Recovery factor
- Statistic events: `PnLChanged`, `CommissionChanged`, `SlippageChanged`
- Statistics persistence with `KeepStatistics`
- Risk-adjusted metrics and risk-free rate

## Quick Reference

### Essential Strategy Template

```csharp
public class MyStrategy : Strategy
{
    // 1. Parameters
    private readonly StrategyParam<int> _period;

    public MyStrategy()
    {
        _period = Param(nameof(Period), 14)
            .SetGreaterThanZero()
            .SetDisplay("Period", "Indicator period", "Settings");
    }

    public int Period
    {
        get => _period.Value;
        set => _period.Value = value;
    }

    // 2. Lifecycle
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Create indicators
        var indicator = new SimpleMovingAverage { Length = Period };
        Indicators.Add(indicator);

        // Subscribe and bind
        var subscription = SubscribeCandles(TimeSpan.FromMinutes(5).TimeFrame());
        subscription.Bind(indicator, ProcessCandle).Start();
    }

    // 3. Trading Logic
    private void ProcessCandle(ICandleMessage candle, decimal indicatorValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        if (!IsFormedAndOnlineAndAllowTrading())
            return;

        // Your trading logic here
    }
}
```

### Common Patterns

#### Order with Rules
```csharp
var order = BuyMarket(Volume);
RegisterOrder(order);

order.WhenMatched(this)
    .Do(() => LogInfo("Order filled"))
    .Apply(this);
```

#### Position-Based Trading
```csharp
if (signal > 0 && Position <= 0)
    BuyMarket(Volume);
else if (signal < 0 && Position >= 0)
    SellMarket(Volume);
```

#### Risk Management Setup
```csharp
RiskRules = new[]
{
    new MaxDrawdownRule { MaxDrawdown = 1000m, Action = RiskActions.StopTrading },
    new PnLRule { MinPnL = -500m, Action = RiskActions.StopTrading }
};
```

#### Statistics Monitoring
```csharp
PnLChanged += () =>
{
    if (PnL >= ProfitTarget)
        Stop();
};
```

## Complete Example

See [strategy-basics.md](strategy-basics.md) for the complete `SmaCrossStrategy` implementation that demonstrates:
- Parameter configuration
- Indicator usage
- Event-driven candle processing
- Position-aware trading logic
- Chart integration

## Learning Path

**Recommended reading order:**

1. **Start with Basics**: Read `strategy-basics.md` to understand the Strategy lifecycle
2. **Configure Parameters**: Read `parameters.md` to learn parameter management
3. **Add Indicators**: Read `indicators.md` to integrate technical analysis
4. **Event-Driven Logic**: Read `events-and-rules.md` to build reactive strategies
5. **Track Positions**: Read `position-tracking.md` to manage positions
6. **Control Risk**: Read `risk-management.md` to add safety measures
7. **Monitor Performance**: Read `statistics.md` to track strategy metrics

## Best Practices Summary

### Strategy Design
- Always call `base` methods in lifecycle overrides
- Initialize indicators in `OnStarted()`, not constructor
- Check `IsFormedAndOnlineAndAllowTrading()` before trading
- Add all indicators to `Indicators` collection for formation tracking

### Parameters
- Use descriptive parameter names and display attributes
- Add validation with `SetGreaterThanZero()`, `SetRange()`, etc.
- Configure optimization ranges appropriately
- Group related parameters with categories

### Event-Driven Programming
- Always call `Apply(this)` to activate rules
- Use `Once()` for one-time events like order matching
- Handle all order outcomes (matched, canceled, failed)
- Check candle state before processing

### Indicators
- Add indicators to `Indicators` collection
- Use `Bind()` for automatic processing
- Check `IsFormed` before using indicator values
- Handle different formation periods appropriately

### Position Management
- Check current position before placing orders
- Handle null position values with `??` operator
- Implement position limits
- Log position changes

### Risk Management
- Always configure risk rules
- Use appropriate actions for different rule types
- Test rules in backtesting first
- Different limits for backtesting vs live trading

### Statistics
- Monitor key metrics (win rate, drawdown, profit factor)
- Wait for sufficient trade count before drawing conclusions
- Account for commission in P&L calculations
- Log statistics periodically

## Additional Resources

- **StockSharp Documentation**: https://doc.stocksharp.com
- **API Reference**: See XML documentation in source code
- **Sample Strategies**: `Samples/06_Strategies/` directory
- **Community Forum**: https://stocksharp.com/forum

## See Also

- **Connector Specifications**: For market data and order execution
- **Indicator Library**: For available technical indicators
- **Backtesting Guide**: For testing strategies on historical data
- **Risk Management**: For advanced risk control patterns

---

**Note**: All file paths in examples use absolute paths as per StockSharp conventions. Adjust paths according to your installation directory.
