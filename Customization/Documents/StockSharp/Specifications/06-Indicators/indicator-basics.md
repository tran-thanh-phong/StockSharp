# Indicator Basics - StockSharp Indicator System

## Overview

The StockSharp indicator system provides a comprehensive framework for technical analysis indicators. All indicators inherit from `BaseIndicator` and implement the `IIndicator` interface, following a consistent pattern for processing market data and generating indicator values.

## Core Architecture

### 1. BaseIndicator Class

The foundation class for all indicators in StockSharp:

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Indicators\BaseIndicator.cs`

```csharp
public abstract class BaseIndicator : Cloneable<IIndicator>, IIndicator
{
    // Core properties
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; }
    public bool IsFormed { get; protected set; }
    public virtual int NumValuesToInitialize => 1;
    public IIndicatorContainer Container { get; }
    public Level1Fields? Source { get; set; }

    // Core methods
    public virtual void Reset();
    public virtual IIndicatorValue Process(IIndicatorValue input);
    protected abstract IIndicatorValue OnProcess(IIndicatorValue input);

    // Events
    public event Action<IIndicatorValue, IIndicatorValue> Changed;
    public event Action Reseted;
}
```

**Key Properties**:
- **Id**: Unique identifier for the indicator instance
- **Name**: Display name (automatically set from class name)
- **IsFormed**: Indicates whether the indicator has received enough data to produce valid values
- **NumValuesToInitialize**: Number of input values required before the indicator is formed
- **Container**: Historical storage of indicator values
- **Source**: Field to extract from input data (OpenPrice, ClosePrice, HighPrice, LowPrice, etc.)

### 2. IIndicator Interface

Defines the contract all indicators must implement:

```csharp
public interface IIndicator : IPersistable, ICloneable<IIndicator>
{
    Guid Id { get; }
    string Name { get; set; }
    bool IsFormed { get; }
    int NumValuesToInitialize { get; }
    IIndicatorContainer Container { get; }
    Level1Fields? Source { get; }
    IndicatorMeasures Measure { get; }
    DrawStyles Style { get; }
    Color? Color { get; }

    IIndicatorValue Process(IIndicatorValue input);
    void Reset();
    IIndicatorValue CreateValue(DateTimeOffset time, object[] values);

    event Action<IIndicatorValue, IIndicatorValue> Changed;
    event Action Reseted;
}
```

### 3. IIndicatorValue Interface

Represents a single indicator calculation result:

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Indicators\IIndicatorValue.cs`

```csharp
public interface IIndicatorValue : IComparable<IIndicatorValue>, IComparable
{
    IIndicator Indicator { get; }
    bool IsEmpty { get; }
    bool IsFinal { get; set; }
    bool IsFormed { get; set; }
    DateTimeOffset Time { get; }

    T GetValue<T>(Level1Fields? field = default);
    IEnumerable<object> ToValues();
    void FromValues(object[] values);
}
```

**Key Properties**:
- **IsEmpty**: True if the indicator couldn't calculate a value (not enough data, division by zero, etc.)
- **IsFinal**: True if this is the final value for this timestamp (important for real-time updates)
- **IsFormed**: True if the indicator was formed when this value was calculated
- **Time**: Timestamp of the input data that generated this value

### 4. Indicator Value Types

StockSharp provides several indicator value types:

#### DecimalIndicatorValue
Single decimal value indicator (most common):

```csharp
public class DecimalIndicatorValue : SingleIndicatorValue<decimal>
{
    public DecimalIndicatorValue(IIndicator indicator, decimal value, DateTimeOffset time);
    public DecimalIndicatorValue(IIndicator indicator, DateTimeOffset time); // Empty value

    public decimal Value { get; }
}
```

#### CandleIndicatorValue
For indicators that work with candle data:

```csharp
public class CandleIndicatorValue : SingleIndicatorValue<ICandleMessage>
{
    public CandleIndicatorValue(IIndicator indicator, ICandleMessage value);
    public override T GetValue<T>(Level1Fields? field);
}
```

#### ComplexIndicatorValue
For multi-value indicators (e.g., Bollinger Bands, MACD with histogram):

```csharp
public abstract class ComplexIndicatorValue<TIndicator> : BaseIndicatorValue, IComplexIndicatorValue
    where TIndicator : IComplexIndicator
{
    public IDictionary<IIndicator, IIndicatorValue> InnerValues { get; }
    public IIndicatorValue this[IIndicator indicator] { get; }
}
```

## IsFormed Property

The `IsFormed` property is critical for indicator reliability:

### Purpose
- Indicates whether the indicator has enough historical data to produce accurate values
- Prevents trading signals based on incomplete calculations

### Implementation Patterns

**Pattern 1: Buffer-based (most common)**
```csharp
protected override bool CalcIsFormed() => Buffer.Count >= Length;
```

**Pattern 2: Custom logic**
```csharp
protected override bool CalcIsFormed() => _gain.IsFormed; // RSI example
```

**Pattern 3: Immediate formation**
```csharp
protected override IIndicatorValue OnProcess(IIndicatorValue input)
{
    // Calculate value...

    if (input.IsFinal)
        IsFormed = true; // Forms immediately

    return new DecimalIndicatorValue(this, value, input.Time);
}
```

### NumValuesToInitialize

Tells users how many data points are needed:

```csharp
// Simple moving average needs 'Length' values
public override int NumValuesToInitialize => Length;

// RSI needs Length + 1 (needs previous value for delta calculation)
public override int NumValuesToInitialize => base.NumValuesToInitialize + 1;

// Complex indicators
public override int NumValuesToInitialize =>
    Mode == ComplexIndicatorModes.Parallel
        ? InnerIndicators.Select(i => i.NumValuesToInitialize).Max()
        : InnerIndicators.Select(i => i.NumValuesToInitialize).Sum() - (InnerIndicators.Count - 1);
```

## Process Method

The `Process` method is the main entry point for indicator calculations:

### Flow
1. User calls `Process(input)` with new data
2. Base class validates input
3. Calls `OnProcess(input)` for indicator-specific calculation
4. Adds result to Container if IsFinal = true
5. Raises Changed event
6. Returns indicator value

### Implementation

```csharp
public virtual IIndicatorValue Process(IIndicatorValue input)
{
    ArgumentNullException.ThrowIfNull(input);

    if (input.IsEmpty)
        return CreateValue(input.Time, []);

    var result = OnProcess(input); // Calls derived class implementation

    if (input.IsFinal)
    {
        result.IsFinal = input.IsFinal;
        Container.AddValue(input, result); // Store in history
    }

    if (!result.IsEmpty)
        RaiseChangedEvent(input, result);

    return result;
}
```

### OnProcess Implementation

Derived classes override `OnProcess` to implement calculation logic:

```csharp
protected abstract IIndicatorValue OnProcess(IIndicatorValue input);
```

**Example: Simple Moving Average**
```csharp
protected override decimal? OnProcessDecimal(IIndicatorValue input)
{
    var newValue = input.ToDecimal(Source);

    if (input.IsFinal)
    {
        Buffer.PushBack(newValue);
        return Buffer.Sum / Length;
    }

    // Handle non-final values (real-time updates)
    return (Buffer.SumNoFirst + newValue) / Length;
}
```

## Reset Method

Resets the indicator to initial state:

### When Called
- When indicator parameters change (e.g., Length property)
- When manually resetting a strategy
- Before starting a new backtest

### Implementation

```csharp
public override void Reset()
{
    _isFormed = false;
    Container.ClearValues(); // Clear historical values
    Reseted?.Invoke(); // Notify listeners

    // Reset inner indicators if any
    if (_resetTrackings.Count > 0)
    {
        foreach (var inner in _resetTrackings)
            inner.Reset();
    }
}
```

**Custom Reset Example**
```csharp
public override void Reset()
{
    base.Reset();

    _multiplier = 2m / (Length + 1); // EMA multiplier
    _prevFinalValue = 0;
    _currentValue = 0;
    _prevClosePrice = 0;
}
```

## Container and Historical Values

The `Container` property provides access to indicator history:

### IIndicatorContainer Interface

```csharp
public interface IIndicatorContainer
{
    int Count { get; }
    IEnumerable<IIndicatorValue> GetValues();
    void AddValue(IIndicatorValue input, IIndicatorValue result);
    void ClearValues();
}
```

### Usage Examples

```csharp
// Get last N values
var lastValues = indicator.Container.GetValues().TakeLast(10);

// Get current value
var currentValue = indicator.Container.GetValues().LastOrDefault();

// Count of stored values
var count = indicator.Container.Count;

// Iterate all values
foreach (var value in indicator.Container.GetValues())
{
    Console.WriteLine($"{value.Time}: {value.GetValue<decimal>()}");
}
```

## Source Field

The `Source` property allows selecting which price field to use:

### Available Sources

```csharp
public enum Level1Fields
{
    OpenPrice,      // Candle open price
    HighPrice,      // Candle high price
    LowPrice,       // Candle low price
    ClosePrice,     // Candle close price (most common)
    SpreadMiddle,   // (High + Low) / 2
    AveragePrice,   // (High + Low + Close) / 3
    VWAP,           // (High + Low + 2 * Close) / 4
    // ... and more
}
```

### Using Source in Indicators

```csharp
protected override decimal? OnProcessDecimal(IIndicatorValue input)
{
    // Extract value from input using Source field
    var newValue = input.ToDecimal(Source);

    // Use newValue in calculations
    Buffer.PushBack(newValue);
    return Buffer.Sum / Length;
}
```

### Setting Source

```csharp
var sma = new SimpleMovingAverage
{
    Length = 20,
    Source = Level1Fields.ClosePrice // Default for most indicators
};

var highSma = new SimpleMovingAverage
{
    Length = 20,
    Source = Level1Fields.HighPrice // Use high prices
};
```

## Indicator Measures

Indicates the value range/type for proper display:

```csharp
public enum IndicatorMeasures
{
    Price,            // Price-based (SMA, EMA, etc.)
    Percent,          // 0-100 range (RSI, Stochastic)
    MinusOnePlusOne,  // -1 to +1 range (MACD, ROC)
    Volume            // Volume-based (OBV, ADL)
}
```

### Implementation

```csharp
public override IndicatorMeasures Measure => IndicatorMeasures.Percent;
```

## Indicator Attributes

Indicators use attributes for metadata and UI configuration:

```csharp
[Display(
    ResourceType = typeof(LocalizedStrings),
    Name = LocalizedStrings.SMAKey,
    Description = LocalizedStrings.SimpleMovingAverageKey)]
[Doc("topics/api/indicators/list_of_indicators/sma.html")]
[IndicatorIn(typeof(DecimalIndicatorValue))]
[IndicatorOut(typeof(DecimalIndicatorValue))]
public class SimpleMovingAverage : LengthIndicator<decimal>
```

## Working with Indicators in Code

### Basic Usage

```csharp
// Create indicator
var sma = new SimpleMovingAverage { Length = 20 };

// Process candles
foreach (var candle in candles)
{
    var candleValue = new CandleIndicatorValue(sma, candle);
    var indicatorValue = sma.Process(candleValue);

    if (sma.IsFormed && !indicatorValue.IsEmpty)
    {
        var smaValue = indicatorValue.GetValue<decimal>();
        Console.WriteLine($"{candle.OpenTime}: {smaValue}");
    }
}
```

### Using with Strategies

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Create indicators
    var longSma = new SimpleMovingAverage { Length = 80 };
    var shortSma = new SimpleMovingAverage { Length = 30 };

    // Subscribe to candles and bind indicators
    SubscribeCandles(subscription)
        .Bind(longSma, shortSma, OnProcess) // Automatically processes both
        .Start();
}

private void OnProcess(ICandleMessage candle, decimal longValue, decimal shortValue)
{
    // longValue and shortValue are automatically extracted
    if (shortValue > longValue)
    {
        // Buy signal
    }
}
```

### Real-time vs Final Values

Important distinction for live trading:

```csharp
protected override IIndicatorValue OnProcess(IIndicatorValue input)
{
    var newValue = input.ToDecimal(Source);

    if (input.IsFinal)
    {
        // This is a completed candle - store permanently
        _buffer.PushBack(newValue);
        _lastFinalValue = CalculateValue();
        return new DecimalIndicatorValue(this, _lastFinalValue, input.Time);
    }
    else
    {
        // This is a real-time update - calculate but don't store
        var tempValue = CalculateValueWithTemp(newValue);
        return new DecimalIndicatorValue(this, tempValue, input.Time);
    }
}
```

## Persistence (Save/Load)

Indicators can save and restore their state:

```csharp
public override void Save(SettingsStorage storage)
{
    base.Save(storage);
    storage.Set(nameof(Length), Length);
    storage.Set(nameof(Width), Width);
}

public override void Load(SettingsStorage storage)
{
    base.Load(storage);
    Length = storage.GetValue<int>(nameof(Length));
    Width = storage.GetValue<decimal>(nameof(Width));
}
```

## Best Practices

1. **Always handle IsFinal correctly**
   - Store values permanently only when IsFinal = true
   - Support real-time updates for live trading

2. **Check IsFormed before using values**
   ```csharp
   if (indicator.IsFormed && !value.IsEmpty)
   {
       // Safe to use value
   }
   ```

3. **Reset when parameters change**
   ```csharp
   public int Length
   {
       get => _length;
       set
       {
           _length = value;
           Reset(); // Important!
       }
   }
   ```

4. **Use appropriate value types**
   - DecimalIndicatorValue for single values
   - CandleIndicatorValue for candle inputs
   - ComplexIndicatorValue for multi-value outputs

5. **Implement ToString for debugging**
   ```csharp
   public override string ToString() => base.ToString() + " " + Length;
   ```

6. **Set NumValuesToInitialize accurately**
   - Helps users understand data requirements
   - Critical for backtesting setup

## Common Patterns

### Pattern 1: Length-based Indicators

```csharp
public class MyIndicator : LengthIndicator<decimal>
{
    protected override decimal? OnProcessDecimal(IIndicatorValue input)
    {
        var value = input.ToDecimal(Source);

        if (input.IsFinal)
            Buffer.PushBack(value);

        return Buffer.Sum / Length;
    }
}
```

### Pattern 2: Stateful Indicators

```csharp
public class MyIndicator : BaseIndicator
{
    private decimal _accumulator;

    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        var value = input.ToDecimal(Source);
        var result = _accumulator;

        // Calculate new value
        result += value * 0.1m;

        if (input.IsFinal)
        {
            _accumulator = result;
            IsFormed = true;
        }

        return new DecimalIndicatorValue(this, result, input.Time);
    }

    public override void Reset()
    {
        base.Reset();
        _accumulator = 0;
    }
}
```

## Reference Documentation

- Official Docs: https://doc.stocksharp.com/topics/api/indicators.html
- Source Code: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Indicators\`
- Examples: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\07_Testing\01_History\SmaStrategy.cs`
