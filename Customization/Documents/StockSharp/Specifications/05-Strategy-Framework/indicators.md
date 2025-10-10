# Using Indicators in Strategies

## Overview

StockSharp provides a comprehensive indicator library that integrates seamlessly with the Strategy framework. Indicators are automatically tracked for formation status and can be bound to data subscriptions for automatic value calculation.

**Location**: `StockSharp.Algo.Indicators`

## Strategy.Indicators Collection

### Purpose

The `Indicators` collection serves two main purposes:

1. **Formation Tracking**: Automatically tracks when all indicators are formed
2. **Lifecycle Management**: Manages indicator lifecycle with the strategy

```csharp
public INotifyList<IIndicator> Indicators { get; }
```

### IsFormed Property

The strategy's `IsFormed` property returns `true` only when ALL indicators in the collection are formed:

```csharp
public virtual bool IsFormed => _indicators.AllFormed;
```

This is crucial for preventing trading on incomplete data.

## Adding Indicators

### Basic Pattern

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Create indicator
    var sma = new SimpleMovingAverage { Length = 20 };

    // Add to collection for tracking
    Indicators.Add(sma);

    // Indicator is now tracked for formation
}
```

### Multiple Indicators

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    var fastSma = new SimpleMovingAverage { Length = 10 };
    var slowSma = new SimpleMovingAverage { Length = 20 };
    var rsi = new RelativeStrengthIndex { Length = 14 };

    // Add all indicators
    Indicators.Add(fastSma);
    Indicators.Add(slowSma);
    Indicators.Add(rsi);

    // IsFormed will be true only when ALL three are formed
}
```

## Indicator Formation

### What is Formation?

An indicator is "formed" when it has received enough data to produce valid values:

```csharp
// SMA with period 20 needs 20 candles to be formed
var sma = new SimpleMovingAverage { Length = 20 };

// After processing candles:
// 1-19 candles: sma.IsFormed = false
// 20+ candles: sma.IsFormed = true
```

### Checking Formation Status

```csharp
// Individual indicator
if (sma.IsFormed)
{
    // Indicator has enough data
    var value = sma.GetCurrentValue();
}

// All indicators in strategy
if (IsFormed)
{
    // All indicators have enough data
    ProcessTradingLogic();
}

// Combined with strategy state
if (IsFormedAndOnlineAndAllowTrading())
{
    // Strategy is fully ready for trading
    ExecuteTrades();
}
```

## Bind Method - Automatic Indicator Processing

### Basic Bind Pattern

The `Bind()` method automatically processes candles through indicators:

```csharp
var subscription = SubscribeCandles(CandleType);
subscription
    .Bind(indicator, ProcessCandle)
    .Start();

void ProcessCandle(ICandleMessage candle, decimal indicatorValue)
{
    // Automatically called with indicator value
    // Only for finished candles
}
```

### Example from SmaCrossStrategy

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Create indicators
    var fastSma = new SimpleMovingAverage { Length = FastPeriod };
    var slowSma = new SimpleMovingAverage { Length = SlowPeriod };

    // Add to collection
    Indicators.Add(fastSma);
    Indicators.Add(slowSma);

    // Bind both indicators to candle subscription
    var subscription = SubscribeCandles(CandleType);
    subscription
        .Bind(fastSma, slowSma, ProcessCandle)  // Bind both indicators
        .Start();
}

private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
{
    if (candle.State != CandleStates.Finished)
        return;

    if (!IsFormedAndOnlineAndAllowTrading())
        return;

    // Use indicator values directly
    if (fastValue > slowValue && Position <= 0)
    {
        BuyMarket(TradeVolume);
    }
    else if (fastValue < slowValue && Position >= 0)
    {
        SellMarket(TradeVolume);
    }
}
```

## Bind Method Signatures

### Single Indicator

```csharp
// Bind one indicator
subscription.Bind(indicator, (candle, value) =>
{
    // value is the indicator's current value
});
```

### Two Indicators

```csharp
// Bind two indicators
subscription.Bind(indicator1, indicator2, (candle, value1, value2) =>
{
    // value1 from indicator1, value2 from indicator2
});
```

### Three Indicators

```csharp
// Bind three indicators
subscription.Bind(indicator1, indicator2, indicator3,
    (candle, value1, value2, value3) =>
{
    // Three indicator values
});
```

### Four Indicators

```csharp
// Bind four indicators
subscription.Bind(indicator1, indicator2, indicator3, indicator4,
    (candle, value1, value2, value3, value4) =>
{
    // Four indicator values
});
```

## Manual Indicator Processing

### Process Individual Values

If you need more control, you can process indicators manually:

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    var sma = new SimpleMovingAverage { Length = 20 };
    Indicators.Add(sma);

    var subscription = SubscribeCandles(CandleType);

    subscription.WhenCandleFinished(this)
        .Do((ICandleMessage candle) =>
        {
            if (candle.State != CandleStates.Finished)
                return;

            // Manually process candle through indicator
            var result = sma.Process(candle);

            // Check if indicator produced a value
            if (result.IsFormed)
            {
                var value = result.GetValue<decimal>();
                ProcessTradingSignal(candle, value);
            }
        })
        .Apply(this);
}
```

### Access Current Value

```csharp
// Get current value (after processing)
if (indicator.IsFormed)
{
    var currentValue = indicator.GetCurrentValue();

    // Type-safe access
    decimal value = indicator.GetCurrentValue<decimal>();
}
```

### Access Historical Values

```csharp
// Get container with all values
var container = indicator.Container;

// Get value at specific index
if (container.Count > 5)
{
    var fifthValue = container[5].GetValue<decimal>();
}

// Get last N values
var last10 = container.GetValues(10);
```

## Common Indicators

### Moving Averages

#### Simple Moving Average (SMA)

```csharp
var sma = new SimpleMovingAverage
{
    Length = 20  // Period
};
Indicators.Add(sma);
```

#### Exponential Moving Average (EMA)

```csharp
var ema = new ExponentialMovingAverage
{
    Length = 20
};
Indicators.Add(ema);
```

#### Weighted Moving Average (WMA)

```csharp
var wma = new WeightedMovingAverage
{
    Length = 20
};
Indicators.Add(wma);
```

### Oscillators

#### Relative Strength Index (RSI)

```csharp
var rsi = new RelativeStrengthIndex
{
    Length = 14  // Standard RSI period
};
Indicators.Add(rsi);

// Typical usage
subscription.Bind(rsi, (candle, rsiValue) =>
{
    if (rsiValue < 30)
        LogInfo("Oversold: RSI = {0}", rsiValue);
    else if (rsiValue > 70)
        LogInfo("Overbought: RSI = {0}", rsiValue);
});
```

#### Stochastic Oscillator

```csharp
var stochastic = new Stochastic
{
    Period = 14,
    Smoothing = 3
};
Indicators.Add(stochastic);
```

#### MACD (Moving Average Convergence Divergence)

```csharp
var macd = new MovingAverageConvergenceDivergence
{
    FastLength = 12,
    SlowLength = 26,
    SignalLength = 9
};
Indicators.Add(macd);

subscription.Bind(macd, (candle, macdValue) =>
{
    var macdLine = macdValue;  // MACD line value
    var signal = macd.Signal.GetCurrentValue();  // Signal line
    var histogram = macdLine - signal;

    if (histogram > 0 && Position <= 0)
        BuyMarket(Volume);  // Bullish crossover
});
```

### Volatility Indicators

#### Bollinger Bands

```csharp
var bb = new BollingerBands
{
    Length = 20,
    Width = 2.0m  // Standard deviations
};
Indicators.Add(bb);

subscription.Bind(bb, (candle, middleBand) =>
{
    var upperBand = bb.UpBand.GetCurrentValue();
    var lowerBand = bb.LowBand.GetCurrentValue();
    var currentPrice = candle.ClosePrice;

    if (currentPrice <= lowerBand)
        LogInfo("Price at lower band - potential buy");
    else if (currentPrice >= upperBand)
        LogInfo("Price at upper band - potential sell");
});
```

#### Average True Range (ATR)

```csharp
var atr = new AverageTrueRange
{
    Length = 14
};
Indicators.Add(atr);

subscription.Bind(atr, (candle, atrValue) =>
{
    // Use ATR for stop loss calculation
    var stopDistance = atrValue * 2;
    LogInfo("Dynamic stop distance: {0}", stopDistance);
});
```

### Trend Indicators

#### Average Directional Index (ADX)

```csharp
var adx = new AverageDirectionalIndex
{
    Length = 14
};
Indicators.Add(adx);

subscription.Bind(adx, (candle, adxValue) =>
{
    if (adxValue > 25)
        LogInfo("Strong trend detected: ADX = {0}", adxValue);
    else
        LogInfo("Weak trend: ADX = {0}", adxValue);
});
```

#### Parabolic SAR

```csharp
var sar = new ParabolicSar
{
    Acceleration = 0.02m,
    Maximum = 0.2m
};
Indicators.Add(sar);
```

## Complete Multi-Indicator Example

```csharp
public class MultiIndicatorStrategy : Strategy
{
    private readonly StrategyParam<int> _smaPeriod;
    private readonly StrategyParam<int> _rsiPeriod;
    private readonly StrategyParam<decimal> _rsiOverbought;
    private readonly StrategyParam<decimal> _rsiOversold;

    public MultiIndicatorStrategy()
    {
        _smaPeriod = Param(nameof(SmaPeriod), 20)
            .SetGreaterThanZero()
            .SetDisplay("SMA Period", "Simple Moving Average period", "Indicators");

        _rsiPeriod = Param(nameof(RsiPeriod), 14)
            .SetGreaterThanZero()
            .SetDisplay("RSI Period", "RSI calculation period", "Indicators");

        _rsiOverbought = Param(nameof(RsiOverbought), 70m)
            .SetRange(50, 90)
            .SetDisplay("RSI Overbought", "RSI overbought level", "Indicators");

        _rsiOversold = Param(nameof(RsiOversold), 30m)
            .SetRange(10, 50)
            .SetDisplay("RSI Oversold", "RSI oversold level", "Indicators");
    }

    public int SmaPeriod
    {
        get => _smaPeriod.Value;
        set => _smaPeriod.Value = value;
    }

    public int RsiPeriod
    {
        get => _rsiPeriod.Value;
        set => _rsiPeriod.Value = value;
    }

    public decimal RsiOverbought
    {
        get => _rsiOverbought.Value;
        set => _rsiOverbought.Value = value;
    }

    public decimal RsiOversold
    {
        get => _rsiOversold.Value;
        set => _rsiOversold.Value = value;
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Create indicators
        var sma = new SimpleMovingAverage { Length = SmaPeriod };
        var rsi = new RelativeStrengthIndex { Length = RsiPeriod };
        var atr = new AverageTrueRange { Length = 14 };

        // Add to tracking collection
        Indicators.Add(sma);
        Indicators.Add(rsi);
        Indicators.Add(atr);

        // Subscribe and bind all indicators
        var subscription = SubscribeCandles(TimeSpan.FromMinutes(5).TimeFrame());
        subscription
            .Bind(sma, rsi, atr, ProcessCandle)
            .Start();

        // Setup charting
        var area = CreateChartArea();
        if (area != null)
        {
            DrawCandles(area, subscription);
            DrawIndicator(area, sma, System.Drawing.Color.Blue);
            DrawIndicator(area, rsi, System.Drawing.Color.Green);
            DrawOwnTrades(area);
        }
    }

    private void ProcessCandle(ICandleMessage candle,
        decimal smaValue, decimal rsiValue, decimal atrValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        if (!IsFormedAndOnlineAndAllowTrading())
            return;

        var currentPrice = candle.ClosePrice;

        // Trading logic combining multiple indicators
        if (Position <= 0)
        {
            // Long entry conditions:
            // 1. Price above SMA (uptrend)
            // 2. RSI oversold (potential reversal)
            if (currentPrice > smaValue && rsiValue < RsiOversold)
            {
                LogInfo("Long signal: Price={0}, SMA={1}, RSI={2}",
                    currentPrice, smaValue, rsiValue);

                var order = BuyMarket(Volume);

                // Use ATR for dynamic stop loss
                var stopDistance = atrValue * 2;
                var stopPrice = currentPrice - stopDistance;

                order.WhenMatched(this)
                    .Do(() =>
                    {
                        LogInfo("Long entry filled. Stop at {0}", stopPrice);
                        // Place protective stop
                        SellStop(stopPrice, Volume);
                    })
                    .Apply(this);
            }
        }
        else if (Position >= 0)
        {
            // Short entry conditions:
            // 1. Price below SMA (downtrend)
            // 2. RSI overbought (potential reversal)
            if (currentPrice < smaValue && rsiValue > RsiOverbought)
            {
                LogInfo("Short signal: Price={0}, SMA={1}, RSI={2}",
                    currentPrice, smaValue, rsiValue);

                var order = SellMarket(Volume);

                // Use ATR for dynamic stop loss
                var stopDistance = atrValue * 2;
                var stopPrice = currentPrice + stopDistance;

                order.WhenMatched(this)
                    .Do(() =>
                    {
                        LogInfo("Short entry filled. Stop at {0}", stopPrice);
                        // Place protective stop
                        BuyStop(stopPrice, Volume);
                    })
                    .Apply(this);
            }
        }
    }
}
```

## Custom Indicators

### Creating Custom Indicators

You can create custom indicators by inheriting from `BaseIndicator`:

```csharp
public class MyCustomIndicator : BaseIndicator
{
    private readonly Queue<decimal> _buffer = new();

    public int Length { get; set; } = 10;

    public override bool IsFormed => _buffer.Count >= Length;

    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        var value = input.GetValue<decimal>();
        _buffer.Enqueue(value);

        if (_buffer.Count > Length)
            _buffer.Dequeue();

        if (!IsFormed)
            return new DecimalIndicatorValue(this);

        // Calculate custom value
        var customValue = CalculateValue();

        return new DecimalIndicatorValue(this, customValue);
    }

    private decimal CalculateValue()
    {
        // Your custom calculation
        return _buffer.Average();
    }
}
```

### Using Custom Indicators

```csharp
var myIndicator = new MyCustomIndicator { Length = 20 };
Indicators.Add(myIndicator);

subscription.Bind(myIndicator, (candle, value) =>
{
    LogInfo("Custom indicator value: {0}", value);
});
```

## Best Practices

### 1. Always Add to Indicators Collection

```csharp
// Good - tracked for formation
var sma = new SimpleMovingAverage { Length = 20 };
Indicators.Add(sma);

// Bad - not tracked, strategy won't wait for formation
var sma = new SimpleMovingAverage { Length = 20 };
// Missing Indicators.Add()
```

### 2. Check IsFormed Before Trading

```csharp
// Good
if (IsFormedAndOnlineAndAllowTrading())
{
    // All indicators are formed
    ExecuteTrades();
}

// Bad - may trade on incomplete data
ExecuteTrades(); // No formation check!
```

### 3. Use Bind() for Simplicity

```csharp
// Good - automatic processing
subscription.Bind(indicator, ProcessCandle).Start();

// Less good - manual processing (more complex)
subscription.WhenCandleFinished(this)
    .Do(candle =>
    {
        var result = indicator.Process(candle);
        ProcessCandle(candle, result.GetValue<decimal>());
    })
    .Apply(this);
```

### 4. Initialize Indicators in Constructor

```csharp
// Good - parameters set in constructor
var sma = new SimpleMovingAverage { Length = SmaPeriod };

// Bad - forgetting to set parameters
var sma = new SimpleMovingAverage(); // Uses default length!
```

### 5. Handle Formation Timing

```csharp
// Indicators with different periods form at different times
var fastSma = new SimpleMovingAverage { Length = 10 };  // Forms after 10 candles
var slowSma = new SimpleMovingAverage { Length = 50 };  // Forms after 50 candles

Indicators.Add(fastSma);
Indicators.Add(slowSma);

// IsFormed will be true only after 50 candles (slowest indicator)
```

## Common Pitfalls

### 1. Not Adding to Indicators Collection

```csharp
// WRONG - indicator not tracked
var sma = new SimpleMovingAverage { Length = 20 };
subscription.Bind(sma, ProcessCandle).Start();
// Strategy may start trading before SMA is formed!

// CORRECT
var sma = new SimpleMovingAverage { Length = 20 };
Indicators.Add(sma);  // Now tracked
subscription.Bind(sma, ProcessCandle).Start();
```

### 2. Accessing Value Before Formation

```csharp
// WRONG - may throw exception or return invalid value
var value = indicator.GetCurrentValue();  // Indicator might not be formed!

// CORRECT
if (indicator.IsFormed)
{
    var value = indicator.GetCurrentValue();
}
```

### 3. Processing Building Candles

```csharp
// WRONG - processes every tick update
subscription.Bind(indicator, (candle, value) =>
{
    ProcessSignal(value);  // Called too often!
});

// CORRECT - only finished candles
subscription.Bind(indicator, (candle, value) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    ProcessSignal(value);
});
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy fundamentals
- [events-and-rules.md](events-and-rules.md) - Event-driven trading
- [parameters.md](parameters.md) - Indicator period parameters
