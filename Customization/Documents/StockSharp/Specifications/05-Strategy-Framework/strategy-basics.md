# Strategy Framework Basics

## Overview

The `Strategy` base class is the foundation of StockSharp's algorithmic trading framework. It provides a complete lifecycle management system, market data integration, order execution, and event-driven rule processing.

**Location**: `StockSharp.Algo.Strategies.Strategy`

## Strategy Base Class

### Key Properties

```csharp
public partial class Strategy : BaseLogReceiver, INotifyPropertyChangedEx,
    IMarketRuleContainer, ICloneable<Strategy>, IMarketDataProvider,
    ISubscriptionProvider, ISecurityProvider, ITransactionProvider,
    IScheduledTask, ICustomTypeDescriptor, ITimeProvider,
    IPortfolioProvider, IPositionProvider
```

### Core Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Unique strategy identifier |
| `Name` | `string` | Strategy display name |
| `Connector` | `Connector` | Connection to trading system |
| `Security` | `Security` | Primary security for trading |
| `Portfolio` | `Portfolio` | Portfolio for orders |
| `Volume` | `decimal` | Default operational volume |
| `ProcessState` | `ProcessStates` | Current execution state |
| `Position` | `decimal` | Current strategy position |
| `IsOnline` | `bool` | True when strategy and subscriptions are online |
| `IsFormed` | `bool` | True when all indicators are formed |

## Strategy Lifecycle

### ProcessState Enum

The strategy lifecycle is managed through the `ProcessStates` enum:

```csharp
public enum ProcessStates
{
    Stopped,   // Strategy is stopped
    Started,   // Strategy is running
    Stopping   // Strategy is in the process of stopping
}
```

### Lifecycle Methods

#### 1. OnStarted(DateTimeOffset time)

Called when strategy transitions to `ProcessStates.Started`:

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Initialize indicators
    // Subscribe to market data
    // Set up rules
}
```

**Default Behavior**:
- Initializes error state to `LogLevels.Info`
- Sets up statistic manager parameters
- Subscribes to portfolio and order lookups

#### 2. OnStopping()

Called when strategy transitions to `ProcessStates.Stopping`:

```csharp
protected override void OnStopping()
{
    // Unsubscribe from market data if UnsubscribeOnStop is true
    // Cancel active orders if CancelOrdersWhenStopping is true

    base.OnStopping();
}
```

**Default Behavior**:
- Unsubscribes from market data (if `UnsubscribeOnStop = true`)
- Cancels active orders (if `CancelOrdersWhenStopping = true`)
- Removes all rules from the rules container

#### 3. OnStopped()

Called when strategy transitions to `ProcessStates.Stopped`:

```csharp
protected override void OnStopped()
{
    // Cleanup resources
    // Final logging

    base.OnStopped();
}
```

**Default Behavior**:
- Updates `TotalWorkingTime`
- Clears `StartedTime`
- Disposes strategy if `DisposeOnStop = true`

### Start/Stop Methods

#### Start()

```csharp
public virtual void Start()
{
    // Transitions strategy to ProcessStates.Started
}
```

#### Stop()

```csharp
public virtual void Stop()
{
    // Transitions strategy to ProcessStates.Stopping
}
```

#### Stop(Exception error)

```csharp
public void Stop(Exception error)
{
    // Logs error and stops strategy
    // Sets LastError property
}
```

## Complete Example: SMA Cross Strategy

Here's the complete implementation from `SmaCrossStrategy.cs`:

```csharp
using System;
using System.Collections.Generic;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using StockSharp.BusinessEntities;

namespace BasicStrategies
{
    public class SmaCrossStrategy : Strategy
    {
        // Strategy parameters (see parameters.md for details)
        private readonly StrategyParam<int> _fastPeriod;
        private readonly StrategyParam<int> _slowPeriod;
        private readonly StrategyParam<decimal> _tradeVolume;
        private readonly StrategyParam<DataType> _candleType;

        // Internal state
        private decimal _prevFastValue;
        private decimal _prevSlowValue;
        private bool _isFirstValue = true;

        // Public parameter accessors
        public int FastPeriod
        {
            get => _fastPeriod.Value;
            set => _fastPeriod.Value = value;
        }

        public int SlowPeriod
        {
            get => _slowPeriod.Value;
            set => _slowPeriod.Value = value;
        }

        public decimal TradeVolume
        {
            get => _tradeVolume.Value;
            set => _tradeVolume.Value = value;
        }

        public DataType CandleType
        {
            get => _candleType.Value;
            set => _candleType.Value = value;
        }

        public SmaCrossStrategy()
        {
            // Initialize parameters with Param() method
            _fastPeriod = Param(nameof(FastPeriod), 10)
                .SetGreaterThanZero()
                .SetDisplay("Fast SMA Period", "Period for fast Simple Moving Average", "Indicators")
                .SetCanOptimize(true)
                .SetOptimize(5, 25, 5);

            _slowPeriod = Param(nameof(SlowPeriod), 20)
                .SetGreaterThanZero()
                .SetDisplay("Slow SMA Period", "Period for slow Simple Moving Average", "Indicators")
                .SetCanOptimize(true)
                .SetOptimize(10, 50, 5);

            _tradeVolume = Param(nameof(TradeVolume), 1m)
                .SetGreaterThanZero()
                .SetDisplay("Trade Volume", "Trade volume size", "Trading")
                .SetCanOptimize(true)
                .SetOptimize(1, 10, 1);

            _candleType = Param(nameof(CandleType), TimeSpan.FromMinutes(5).TimeFrame())
                .SetDisplay("Candle Type", "Type of candles to use for analysis", "General");
        }

        // Specify required securities for strategy
        public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
        {
            return new[] { (Security, CandleType) };
        }

        protected override void OnStarted(DateTimeOffset time)
        {
            base.OnStarted(time);

            // Create indicators
            var fastSma = new SimpleMovingAverage { Length = FastPeriod };
            var slowSma = new SimpleMovingAverage { Length = SlowPeriod };

            // Add to Indicators collection for IsFormed tracking
            Indicators.Add(fastSma);
            Indicators.Add(slowSma);

            // Subscribe to candles and bind indicators
            var subscription = SubscribeCandles(CandleType);
            subscription
                .Bind(fastSma, slowSma, ProcessCandle)
                .Start();

            // Set up charting (optional)
            var area = CreateChartArea();
            if (area != null)
            {
                DrawCandles(area, subscription);
                DrawIndicator(area, fastSma, System.Drawing.Color.Orange);
                DrawIndicator(area, slowSma, System.Drawing.Color.Blue);
                DrawOwnTrades(area);
            }
        }

        private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
        {
            // Only process finished candles
            if (candle.State != CandleStates.Finished)
                return;

            // Check if strategy is ready to trade
            if (!IsFormedAndOnlineAndAllowTrading())
                return;

            // Initialize previous values on first candle
            if (_isFirstValue)
            {
                _prevFastValue = fastValue;
                _prevSlowValue = slowValue;
                _isFirstValue = false;
                return;
            }

            // Detect crossover
            var isFastAboveCurrent = fastValue > slowValue;
            var isFastAbovePrev = _prevFastValue > _prevSlowValue;

            _prevFastValue = fastValue;
            _prevSlowValue = slowValue;

            // No change in cross state
            if (isFastAboveCurrent == isFastAbovePrev)
                return;

            // Execute trades on crossover
            if (isFastAboveCurrent)
                BuyMarket(TradeVolume);  // Fast crossed above slow - buy signal
            else
                SellMarket(TradeVolume); // Fast crossed below slow - sell signal
        }
    }
}
```

## Key Concepts

### 1. Strategy Initialization

The constructor should:
- Initialize all strategy parameters using `Param()` method
- Set default values
- Configure parameter validation and optimization ranges
- NOT access Connector, Security, or Portfolio (not yet assigned)

### 2. Strategy State Management

```csharp
// Check current state
if (ProcessState == ProcessStates.Started)
{
    // Strategy is running
}

// Check if ready to trade
if (IsFormed && IsOnline && TradingMode == StrategyTradingModes.Full)
{
    // Can execute trades
}
```

### 3. Working Time Management

Strategies respect `WorkingTime` settings:

```csharp
public WorkingTime WorkingTime { get; set; }
```

The framework automatically prevents trading outside working hours.

### 4. Trading Modes

```csharp
public enum StrategyTradingModes
{
    Full,                 // Allow all trading
    Disabled,            // No trading allowed
    CancelOrdersOnly,    // Can only cancel existing orders
    ReducePositionOnly   // Can only reduce position
}
```

## Best Practices

### 1. Always Call Base Methods

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time); // IMPORTANT: Call base first

    // Your initialization code
}
```

### 2. Check Strategy State

```csharp
private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
{
    if (!IsFormedAndOnlineAndAllowTrading())
        return;

    // Trading logic
}
```

### 3. Handle Errors Gracefully

```csharp
try
{
    // Strategy logic
}
catch (Exception ex)
{
    Stop(ex); // Stops strategy and logs error
}
```

### 4. Clean Up Resources

```csharp
protected override void OnStopped()
{
    // Dispose any custom resources
    // Clear collections
    // Reset state variables

    base.OnStopped();
}
```

## Common Properties and Settings

### Lifecycle Control

| Property | Default | Description |
|----------|---------|-------------|
| `DisposeOnStop` | `false` | Auto-dispose when stopped |
| `WaitRulesOnStop` | `false` | Wait for rules to finish before stopping |
| `CancelOrdersWhenStopping` | `true` | Cancel active orders when stopping |
| `WaitAllTrades` | `false` | Wait for all trades before stopping |
| `UnsubscribeOnStop` | `true` | Unsubscribe from data when stopping |

### Logging

| Property | Default | Description |
|----------|---------|-------------|
| `LogLevel` | `Inherit` | Logging level for strategy |
| `CommentMode` | `Disabled` | Auto-fill order comments |

### Time Management

| Property | Default | Description |
|----------|---------|-------------|
| `StartedTime` | - | When strategy started |
| `TotalWorkingTime` | - | Total running time |
| `OrdersKeepTime` | `1 day` | How long to keep orders in memory |

## See Also

- [parameters.md](parameters.md) - Strategy parameters and configuration
- [events-and-rules.md](events-and-rules.md) - Event-driven trading rules
- [indicators.md](indicators.md) - Using indicators in strategies
- [position-tracking.md](position-tracking.md) - Position management
- [risk-management.md](risk-management.md) - Risk controls
- [statistics.md](statistics.md) - Performance metrics
