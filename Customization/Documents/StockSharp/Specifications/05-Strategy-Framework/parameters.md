# Strategy Parameters

## Overview

Strategy parameters provide a robust system for configuring strategies with validation, optimization support, and UI integration. The `StrategyParam<T>` class is the foundation of parameter management in StockSharp strategies.

**Location**: `StockSharp.Algo.Strategies.StrategyParam<T>`

## StrategyParam<T> Class

### Basic Structure

```csharp
public class StrategyParam<T> : NotifiableObject, IStrategyParam
{
    public T Value { get; set; }
    public string Id { get; }
    public bool CanOptimize { get; set; }
    public object OptimizeFrom { get; set; }
    public object OptimizeTo { get; set; }
    public object OptimizeStep { get; set; }
}
```

### Key Features

- **Type-safe**: Generic parameter with compile-time type checking
- **Validation**: Built-in validators for range, nullability, and custom rules
- **Optimization**: Support for parameter optimization in backtesting
- **UI Integration**: Display attributes for property grids and editors
- **Change Notification**: Implements `INotifyPropertyChanged` for data binding

## Creating Parameters with Param() Method

The `Strategy` base class provides a `Param<T>()` method for creating parameters:

```csharp
protected StrategyParam<T> Param<T>(string id, T initialValue = default)
```

### Basic Usage

```csharp
public class MyStrategy : Strategy
{
    private readonly StrategyParam<int> _period;
    private readonly StrategyParam<decimal> _stopLoss;

    public MyStrategy()
    {
        // Create parameter with default value
        _period = Param(nameof(Period), 14);

        // Create parameter with explicit default
        _stopLoss = Param(nameof(StopLoss), 10m);
    }

    public int Period
    {
        get => _period.Value;
        set => _period.Value = value;
    }

    public decimal StopLoss
    {
        get => _stopLoss.Value;
        set => _stopLoss.Value = value;
    }
}
```

## Parameter Configuration Methods

All configuration methods return `this` for method chaining.

### SetDisplay - UI Display Settings

```csharp
SetDisplay(string displayName, string description, string category)
```

Configures how the parameter appears in UI property grids:

```csharp
_fastPeriod = Param(nameof(FastPeriod), 10)
    .SetDisplay(
        "Fast SMA Period",                           // Display name
        "Period for fast Simple Moving Average",     // Description
        "Indicators"                                  // Category/Group
    );
```

### SetCanOptimize - Optimization Support

```csharp
SetCanOptimize(bool canOptimize)
```

Enables or disables parameter optimization:

```csharp
_volume = Param(nameof(Volume), 1m)
    .SetCanOptimize(false); // Cannot be optimized

_period = Param(nameof(Period), 14)
    .SetCanOptimize(true);  // Can be optimized
```

### SetOptimize - Optimization Range

```csharp
SetOptimize(T optimizeFrom, T optimizeTo, T optimizeStep)
```

Defines the range and step for optimization:

```csharp
_fastPeriod = Param(nameof(FastPeriod), 10)
    .SetOptimize(
        5,   // From value
        25,  // To value
        5    // Step
    );
// Will test: 5, 10, 15, 20, 25
```

## Validation Methods

### SetGreaterThanZero

```csharp
SetGreaterThanZero()
```

Requires value to be greater than zero:

```csharp
_period = Param(nameof(Period), 10)
    .SetGreaterThanZero();

// Valid: _period.Value = 1;
// Invalid: _period.Value = 0; // Throws ArgumentOutOfRangeException
// Invalid: _period.Value = -5; // Throws ArgumentOutOfRangeException
```

Supported types: `int`, `long`, `decimal`, `double`, `float`, `TimeSpan`, `Unit`

### SetNotNegative

```csharp
SetNotNegative()
```

Requires value to be zero or positive:

```csharp
_minVolume = Param(nameof(MinVolume), 0m)
    .SetNotNegative();

// Valid: _minVolume.Value = 0;
// Valid: _minVolume.Value = 10;
// Invalid: _minVolume.Value = -1; // Throws ArgumentOutOfRangeException
```

### SetNullOrMoreZero

```csharp
SetNullOrMoreZero()
```

Allows null or values greater than zero:

```csharp
_takeProfit = Param<decimal?>(nameof(TakeProfit))
    .SetNullOrMoreZero();

// Valid: _takeProfit.Value = null;
// Valid: _takeProfit.Value = 10;
// Invalid: _takeProfit.Value = 0; // Throws ArgumentOutOfRangeException
```

### SetNullOrNotNegative

```csharp
SetNullOrNotNegative()
```

Allows null or non-negative values:

```csharp
_maxVolume = Param<decimal?>(nameof(MaxVolume))
    .SetNullOrNotNegative();

// Valid: _maxVolume.Value = null;
// Valid: _maxVolume.Value = 0;
// Valid: _maxVolume.Value = 100;
// Invalid: _maxVolume.Value = -1; // Throws ArgumentOutOfRangeException
```

### SetRange

```csharp
SetRange(T min, T max)
```

Constrains value to a specific range:

```csharp
_rsiPeriod = Param(nameof(RsiPeriod), 14)
    .SetRange(2, 100);

// Valid: _rsiPeriod.Value = 14;
// Invalid: _rsiPeriod.Value = 1; // Below minimum
// Invalid: _rsiPeriod.Value = 101; // Above maximum
```

### SetRequired

```csharp
SetRequired()
```

Marks parameter as required (cannot be null):

```csharp
_security = Param<Security>(nameof(Security))
    .SetRequired();

// Invalid: _security.Value = null; // Throws validation exception
```

### SetStep

```csharp
SetStep(T step, T baseValue = default)
```

Enforces that value must equal `base + N * step`:

```csharp
_lotSize = Param(nameof(LotSize), 100m)
    .SetStep(100m);

// Valid: 0, 100, 200, 300, ...
// Invalid: 50, 150, 250, ... // Not multiples of 100
```

## Visibility and Behavior

### SetHidden

```csharp
SetHidden(bool hidden = true)
```

Hides parameter from UI property grids:

```csharp
_internalState = Param(nameof(InternalState), 0)
    .SetHidden(); // Not shown in UI
```

### SetReadOnly

```csharp
SetReadOnly(bool value = true)
```

Makes parameter read-only in UI:

```csharp
_id = Param(nameof(Id), Guid.NewGuid())
    .SetReadOnly(); // Cannot be edited in UI
```

### SetBasic

```csharp
SetBasic(bool basic = true)
```

Marks parameter as basic (shown in simplified views):

```csharp
_volume = Param(nameof(Volume), 1m)
    .SetBasic(true); // Shown in basic parameter view
```

## Complete Example from SmaCrossStrategy

```csharp
public class SmaCrossStrategy : Strategy
{
    private readonly StrategyParam<int> _fastPeriod;
    private readonly StrategyParam<int> _slowPeriod;
    private readonly StrategyParam<decimal> _tradeVolume;
    private readonly StrategyParam<DataType> _candleType;

    public SmaCrossStrategy()
    {
        // Fast period: 5-25, step 5, optimizable
        _fastPeriod = Param(nameof(FastPeriod), 10)
            .SetGreaterThanZero()
            .SetDisplay(
                "Fast SMA Period",
                "Period for fast Simple Moving Average",
                "Indicators"
            )
            .SetCanOptimize(true)
            .SetOptimize(5, 25, 5);

        // Slow period: 10-50, step 5, optimizable
        _slowPeriod = Param(nameof(SlowPeriod), 20)
            .SetGreaterThanZero()
            .SetDisplay(
                "Slow SMA Period",
                "Period for slow Simple Moving Average",
                "Indicators"
            )
            .SetCanOptimize(true)
            .SetOptimize(10, 50, 5);

        // Trade volume: 1-10, step 1, optimizable
        _tradeVolume = Param(nameof(TradeVolume), 1m)
            .SetGreaterThanZero()
            .SetDisplay(
                "Trade Volume",
                "Trade volume size",
                "Trading"
            )
            .SetCanOptimize(true)
            .SetOptimize(1, 10, 1);

        // Candle type: not optimizable
        _candleType = Param(nameof(CandleType), TimeSpan.FromMinutes(5).TimeFrame())
            .SetDisplay(
                "Candle Type",
                "Type of candles to use for analysis",
                "General"
            );
    }

    // Public properties for parameter access
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
}
```

## Advanced Usage

### Complex Types

Parameters can be any type, including complex objects:

```csharp
// Security parameter
_security = Param<Security>(nameof(Security))
    .SetDisplay("Security", "Trading instrument", "General")
    .SetRequired();

// Portfolio parameter
_portfolio = Param<Portfolio>(nameof(Portfolio))
    .SetDisplay("Portfolio", "Trading portfolio", "General")
    .SetCanOptimize(false);

// TimeFrame parameter
_timeFrame = Param(nameof(TimeFrame), TimeSpan.FromMinutes(5).TimeFrame())
    .SetDisplay("TimeFrame", "Candle timeframe", "Data");

// Enum parameter
_orderType = Param(nameof(OrderType), OrderTypes.Limit)
    .SetDisplay("Order Type", "Type of orders to place", "Trading");
```

### Nullable Parameters

```csharp
// Optional decimal parameter
_stopLoss = Param<decimal?>(nameof(StopLoss), null)
    .SetNullOrMoreZero()
    .SetDisplay("Stop Loss", "Stop loss in ticks (optional)", "Risk");

// Optional timespan parameter
_maxAge = Param<TimeSpan?>(nameof(MaxAge), null)
    .SetNullOrNotNegative()
    .SetDisplay("Max Age", "Maximum signal age (optional)", "Filters");
```

### Method Chaining

All configuration methods support chaining for concise parameter setup:

```csharp
_parameter = Param(nameof(Parameter), defaultValue)
    .SetGreaterThanZero()           // Validation
    .SetRange(1, 100)               // Additional validation
    .SetDisplay("Name", "Desc", "Cat") // UI display
    .SetCanOptimize(true)           // Enable optimization
    .SetOptimize(1, 100, 1)         // Optimization range
    .SetBasic(true);                // Show in basic view
```

## Parameter Access

### Reading Values

```csharp
// Direct access
var period = _period.Value;

// Through property
var period = Period;
```

### Setting Values

```csharp
// Direct assignment
_period.Value = 20;

// Through property
Period = 20;

// Validation is automatically applied
try
{
    _period.Value = -5; // Throws ArgumentOutOfRangeException
}
catch (ArgumentOutOfRangeException ex)
{
    // Handle validation error
}
```

## Parameter Collection

All strategy parameters are stored in the `Parameters` collection:

```csharp
// Access all parameters
foreach (var param in Parameters)
{
    Console.WriteLine($"{param.Id} = {param.Value}");
}

// Find specific parameter
if (Parameters.TryGetValue("FastPeriod", out var param))
{
    Console.WriteLine($"Fast Period: {param.Value}");
}
```

## Persistence

Parameters support save/load operations:

```csharp
// Save strategy parameters
var storage = new SettingsStorage();
strategy.Save(storage);

// Load strategy parameters
strategy.Load(storage);
```

## Best Practices

### 1. Use Descriptive Names

```csharp
// Good
_fastPeriod = Param(nameof(FastPeriod), 10)
    .SetDisplay("Fast Period", "Period for fast moving average", "Indicators");

// Bad
_p1 = Param("p1", 10)
    .SetDisplay("P1", "Param 1", "General");
```

### 2. Set Appropriate Defaults

```csharp
// Use common, sensible defaults
_rsiPeriod = Param(nameof(RsiPeriod), 14);  // Standard RSI period
_volume = Param(nameof(Volume), 1m);        // Minimal volume
```

### 3. Add Validation

```csharp
// Always validate parameter values
_period = Param(nameof(Period), 14)
    .SetGreaterThanZero()
    .SetRange(2, 200);
```

### 4. Configure Optimization Carefully

```csharp
// Reasonable ranges prevent excessive computation
_period = Param(nameof(Period), 14)
    .SetOptimize(
        5,   // Minimum practical value
        50,  // Maximum practical value
        5    // Step size (don't make too small)
    );
```

### 5. Group Related Parameters

```csharp
_fastPeriod = Param(nameof(FastPeriod), 10)
    .SetDisplay("Fast Period", "...", "Moving Averages");

_slowPeriod = Param(nameof(SlowPeriod), 20)
    .SetDisplay("Slow Period", "...", "Moving Averages");

_stopLoss = Param(nameof(StopLoss), 10m)
    .SetDisplay("Stop Loss", "...", "Risk Management");
```

### 6. Document Complex Parameters

```csharp
_complexParam = Param(nameof(ComplexParam), defaultValue)
    .SetDisplay(
        "Parameter Name",
        "Detailed description explaining what this parameter does, " +
        "valid ranges, and how it affects strategy behavior",
        "Category"
    );
```

## Common Parameter Patterns

### Indicator Parameters

```csharp
_period = Param(nameof(Period), 14)
    .SetGreaterThanZero()
    .SetRange(2, 200)
    .SetDisplay("Period", "Indicator period", "Indicators")
    .SetCanOptimize(true)
    .SetOptimize(5, 50, 5);
```

### Volume Parameters

```csharp
_volume = Param(nameof(Volume), 1m)
    .SetGreaterThanZero()
    .SetDisplay("Volume", "Order volume", "Trading")
    .SetCanOptimize(true)
    .SetOptimize(1, 10, 1);
```

### Price Level Parameters

```csharp
_stopLoss = Param<decimal?>(nameof(StopLoss), 10m)
    .SetNullOrMoreZero()
    .SetDisplay("Stop Loss", "Stop loss in ticks", "Risk Management")
    .SetCanOptimize(true)
    .SetOptimize(5, 50, 5);
```

### Time Parameters

```csharp
_holdingPeriod = Param(nameof(HoldingPeriod), TimeSpan.FromMinutes(30))
    .SetGreaterThanZero()
    .SetDisplay("Holding Period", "Minimum holding time", "Timing")
    .SetCanOptimize(false);
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy fundamentals
- [events-and-rules.md](events-and-rules.md) - Using parameters in rules
