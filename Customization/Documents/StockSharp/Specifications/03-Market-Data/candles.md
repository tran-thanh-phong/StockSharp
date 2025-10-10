# Candlestick Data

Candlestick data (OHLCV - Open, High, Low, Close, Volume) aggregates market trades into time-based or volume-based intervals. Candles are essential for technical analysis and strategy development.

## Overview

StockSharp supports multiple candle types:
- **TimeFrame**: Time-based candles (1min, 5min, 1hour, 1day, etc.)
- **Tick**: Candles based on number of trades
- **Volume**: Candles based on traded volume
- **Range**: Candles based on price range
- **Renko**: Fixed price movement candles
- **PnF (Point and Figure)**: Reversal-based candles
- **HeikinAshi**: Modified candlesticks smoothing price action

## CandleMessage

The base class for all candle types:

### Properties

```csharp
public abstract class CandleMessage : Message
{
    // Security identifier
    public SecurityId SecurityId { get; set; }

    // Candle opening time
    public DateTimeOffset OpenTime { get; set; }

    // Candle closing time
    public DateTimeOffset CloseTime { get; set; }

    // High/Low times
    public DateTimeOffset HighTime { get; set; }
    public DateTimeOffset LowTime { get; set; }

    // OHLC prices
    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal ClosePrice { get; set; }

    // Volume data
    public decimal TotalVolume { get; set; }
    public decimal? BuyVolume { get; set; }     // Buy side volume
    public decimal? SellVolume { get; set; }    // Sell side volume
    public decimal? OpenVolume { get; set; }    // Volume at open
    public decimal? CloseVolume { get; set; }   // Volume at close
    public decimal? HighVolume { get; set; }    // Volume at high
    public decimal? LowVolume { get; set; }     // Volume at low
    public decimal? RelativeVolume { get; set; }// Relative to average

    // Additional metrics
    public decimal TotalPrice { get; set; }     // Sum of prices (for VWAP)
    public decimal? OpenInterest { get; set; }  // Open interest (futures/options)

    // Tick statistics
    public int? TotalTicks { get; set; }        // Total number of ticks
    public int? UpTicks { get; set; }           // Number of upticks
    public int? DownTicks { get; set; }         // Number of downticks

    // Candle state
    public CandleStates State { get; set; }     // Active or Finished

    // Price levels (for advanced analysis)
    public IEnumerable<CandlePriceLevel> PriceLevels { get; set; }

    // Candle argument (timeframe, tick count, etc.)
    public abstract object Arg { get; }

    // Data type
    public DataType DataType { get; set; }

    // Build information
    public DataType BuildFrom { get; set; }     // Source data type
    public long SeqNum { get; set; }            // Sequence number
}
```

### Candle States

```csharp
public enum CandleStates
{
    None,        // Empty/doesn't exist
    Active,      // Candle is being formed
    Finished     // Candle is complete
}
```

## TimeFrame Candles

The most common candle type, based on fixed time intervals.

### TimeFrameCandleMessage

```csharp
public class TimeFrameCandleMessage : CandleMessage
{
    // Time frame (e.g., 1 minute, 5 minutes, 1 hour)
    public TimeSpan TimeFrame { get; }
}
```

### Subscribing to TimeFrame Candles

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

var connector = new Connector();
var security = connector.GetSecurity("AAPL@NASDAQ");

// Subscribe to 5-minute candles
var subscription = connector.Subscribe(new Subscription(
    DataType.TimeFrame(TimeSpan.FromMinutes(5)),
    security
));

// Alternative: Using extension method
var timeframe = TimeSpan.FromMinutes(5).TimeFrame();
var subscription = connector.Subscribe(new Subscription(timeframe, security));
```

### Common TimeFrames

```csharp
// Minute-based
TimeSpan.FromMinutes(1).TimeFrame()    // 1 minute
TimeSpan.FromMinutes(5).TimeFrame()    // 5 minutes
TimeSpan.FromMinutes(15).TimeFrame()   // 15 minutes
TimeSpan.FromMinutes(30).TimeFrame()   // 30 minutes

// Hour-based
TimeSpan.FromHours(1).TimeFrame()      // 1 hour
TimeSpan.FromHours(4).TimeFrame()      // 4 hours

// Day-based
TimeSpan.FromDays(1).TimeFrame()       // 1 day
TimeSpan.FromDays(7).TimeFrame()       // 1 week

// Using DataType helper
DataType.TimeFrame(TimeSpan.FromMinutes(5))
```

### Handling Candle Updates

```csharp
connector.CandleReceived += (subscription, candle) =>
{
    Console.WriteLine($"Candle: {candle.SecurityId}");
    Console.WriteLine($"  Time: {candle.OpenTime} - {candle.CloseTime}");
    Console.WriteLine($"  Open: {candle.OpenPrice}");
    Console.WriteLine($"  High: {candle.HighPrice}");
    Console.WriteLine($"  Low: {candle.LowPrice}");
    Console.WriteLine($"  Close: {candle.ClosePrice}");
    Console.WriteLine($"  Volume: {candle.TotalVolume}");
    Console.WriteLine($"  State: {candle.State}");
};
```

## Strategy with Candles

### Example from SmaCrossStrategy.cs

This is a complete example from the codebase showing candle usage in a strategy:

```csharp
using System;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;

public class SmaCrossStrategy : Strategy
{
    private readonly StrategyParam<DataType> _candleType;

    public DataType CandleType
    {
        get => _candleType.Value;
        set => _candleType.Value = value;
    }

    public SmaCrossStrategy()
    {
        _candleType = Param(nameof(CandleType), TimeSpan.FromMinutes(5).TimeFrame())
            .SetDisplay("Candle Type", "Type of candles to use for analysis", "General");
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        var fastSma = new SimpleMovingAverage { Length = 10 };
        var slowSma = new SimpleMovingAverage { Length = 20 };

        Indicators.Add(fastSma);
        Indicators.Add(slowSma);

        // Subscribe to candles - Line 84 from SmaCrossStrategy.cs
        var subscription = SubscribeCandles(CandleType);
        subscription
            .Bind(fastSma, slowSma, ProcessCandle)
            .Start();

        // Visualization (optional)
        var area = CreateChartArea();
        if (area != null)
        {
            DrawCandles(area, subscription);
            DrawIndicator(area, fastSma, System.Drawing.Color.Orange);
            DrawIndicator(area, slowSma, System.Drawing.Color.Blue);
        }
    }

    private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
    {
        // Only process finished candles
        if (candle.State != CandleStates.Finished)
            return;

        // Strategy logic based on indicator values
        // (calculated from candles)
    }
}
```

### Basic Strategy Pattern

```csharp
public class CandleStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to 5-minute candles
        var subscription = SubscribeCandles(
            DataType.TimeFrame(TimeSpan.FromMinutes(5))
        );

        // Handle candles
        subscription
            .Bind(ProcessCandle)
            .Start();
    }

    private void ProcessCandle(ICandleMessage candle)
    {
        // Only process finished candles
        if (candle.State != CandleStates.Finished)
            return;

        this.AddInfoLog($"Candle: O={candle.OpenPrice}, H={candle.HighPrice}, " +
                       $"L={candle.LowPrice}, C={candle.ClosePrice}, V={candle.TotalVolume}");

        // Your strategy logic here
    }
}
```

## Candle Building from Trades

StockSharp can build candles from trade data when the exchange doesn't provide native candle support:

```csharp
// Request candles to be built from trades
var subscription = connector.Subscribe(new Subscription(
    DataType.TimeFrame(TimeSpan.FromMinutes(5)),
    security
)
{
    SubscriptionMessage = new MarketDataMessage
    {
        BuildMode = MarketDataBuildModes.Build,     // Build from underlying data
        BuildFrom = DataType.Ticks                   // Build from trades
    }
});
```

### Build Modes

```csharp
public enum MarketDataBuildModes
{
    LoadAndBuild,  // Load if available, otherwise build
    Load,          // Only load from exchange
    Build          // Always build locally
}
```

## Other Candle Types

### Tick Candles

Candles based on number of trades:

```csharp
// 100-tick candles
var subscription = connector.Subscribe(new Subscription(
    DataType.Create(typeof(TickCandleMessage), 100),
    security
));
```

### Volume Candles

Candles based on traded volume:

```csharp
// 10000-volume candles
var subscription = connector.Subscribe(new Subscription(
    DataType.Create(typeof(VolumeCandleMessage), 10000m),
    security
));
```

### Range Candles

Candles based on price range:

```csharp
// 0.5 price range candles
var subscription = connector.Subscribe(new Subscription(
    DataType.Create(typeof(RangeCandleMessage), new Unit(0.5m)),
    security
));
```

### Renko Candles

Fixed price movement candles:

```csharp
// Renko with 1 point box size
var subscription = connector.Subscribe(new Subscription(
    DataType.Create(typeof(RenkoCandleMessage), new Unit(1m)),
    security
));
```

### Heikin-Ashi Candles

Smoothed candlesticks:

```csharp
// Heikin-Ashi 5-minute candles
var subscription = connector.Subscribe(new Subscription(
    DataType.Create(typeof(HeikinAshiCandleMessage), TimeSpan.FromMinutes(5)),
    security
));
```

## Practical Examples

### Example 1: Calculate VWAP from Candles

```csharp
var totalValue = 0m;
var totalVolume = 0m;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    // Calculate VWAP using candle data
    var avgPrice = (candle.OpenPrice + candle.HighPrice +
                    candle.LowPrice + candle.ClosePrice) / 4;
    totalValue += avgPrice * candle.TotalVolume;
    totalVolume += candle.TotalVolume;

    var vwap = totalVolume > 0 ? totalValue / totalVolume : 0;

    Console.WriteLine($"Candle at {candle.CloseTime:HH:mm}");
    Console.WriteLine($"  Close: {candle.ClosePrice:F2}");
    Console.WriteLine($"  VWAP: {vwap:F2}");
    Console.WriteLine($"  Deviation: {((candle.ClosePrice - vwap) / vwap * 100):+0.00;-0.00}%");
};
```

### Example 2: Detect Candlestick Patterns

```csharp
var previousCandle = (CandleMessage)null;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    if (previousCandle != null)
    {
        // Bullish engulfing pattern
        if (IsBearish(previousCandle) && IsBullish(candle) &&
            candle.OpenPrice < previousCandle.ClosePrice &&
            candle.ClosePrice > previousCandle.OpenPrice)
        {
            Console.WriteLine($"BULLISH ENGULFING at {candle.CloseTime}");
        }

        // Bearish engulfing pattern
        if (IsBullish(previousCandle) && IsBearish(candle) &&
            candle.OpenPrice > previousCandle.ClosePrice &&
            candle.ClosePrice < previousCandle.OpenPrice)
        {
            Console.WriteLine($"BEARISH ENGULFING at {candle.CloseTime}");
        }

        // Doji pattern (small body)
        var bodySize = Math.Abs(candle.ClosePrice - candle.OpenPrice);
        var range = candle.HighPrice - candle.LowPrice;
        if (bodySize < range * 0.1m)
        {
            Console.WriteLine($"DOJI at {candle.CloseTime}");
        }
    }

    previousCandle = (CandleMessage)candle;
};

bool IsBullish(ICandleMessage candle) => candle.ClosePrice > candle.OpenPrice;
bool IsBearish(ICandleMessage candle) => candle.ClosePrice < candle.OpenPrice;
```

### Example 3: Support/Resistance Levels

```csharp
var recentCandles = new Queue<ICandleMessage>();
var maxCandles = 50;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    recentCandles.Enqueue(candle);
    while (recentCandles.Count > maxCandles)
        recentCandles.Dequeue();

    if (recentCandles.Count >= maxCandles)
    {
        // Find resistance (highest highs)
        var resistance = recentCandles
            .OrderByDescending(c => c.HighPrice)
            .Take(5)
            .Average(c => c.HighPrice);

        // Find support (lowest lows)
        var support = recentCandles
            .OrderBy(c => c.LowPrice)
            .Take(5)
            .Average(c => c.LowPrice);

        Console.WriteLine($"Levels at {candle.CloseTime}:");
        Console.WriteLine($"  Resistance: {resistance:F2}");
        Console.WriteLine($"  Current: {candle.ClosePrice:F2}");
        Console.WriteLine($"  Support: {support:F2}");

        // Trading signals
        if (candle.ClosePrice >= resistance * 0.99m)
            Console.WriteLine("  => Near resistance (potential sell)");
        else if (candle.ClosePrice <= support * 1.01m)
            Console.WriteLine("  => Near support (potential buy)");
    }
};
```

### Example 4: Volume Analysis

```csharp
var volumeSum = 0m;
var candleCount = 0;
var avgVolumePeriod = 20;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    volumeSum += candle.TotalVolume;
    candleCount++;

    if (candleCount > avgVolumePeriod)
    {
        // Simple moving average of volume
        var avgVolume = volumeSum / candleCount;

        // Volume analysis
        var volumeRatio = candle.TotalVolume / avgVolume;

        Console.WriteLine($"Candle at {candle.CloseTime:HH:mm}");
        Console.WriteLine($"  Volume: {candle.TotalVolume:F0}");
        Console.WriteLine($"  Avg Volume: {avgVolume:F0}");
        Console.WriteLine($"  Ratio: {volumeRatio:F2}x");

        if (volumeRatio > 2.0m)
        {
            Console.WriteLine("  => UNUSUAL HIGH VOLUME");

            var priceChange = candle.ClosePrice - candle.OpenPrice;
            if (priceChange > 0)
                Console.WriteLine("  => Accumulation (buying)");
            else if (priceChange < 0)
                Console.WriteLine("  => Distribution (selling)");
        }

        // Update rolling average
        volumeSum -= volumeSum / avgVolumePeriod;
    }
};
```

### Example 5: Multi-Timeframe Analysis

```csharp
public class MultiTimeframeStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to multiple timeframes
        var shortTerm = SubscribeCandles(DataType.TimeFrame(TimeSpan.FromMinutes(5)));
        var mediumTerm = SubscribeCandles(DataType.TimeFrame(TimeSpan.FromMinutes(15)));
        var longTerm = SubscribeCandles(DataType.TimeFrame(TimeSpan.FromHours(1)));

        // Handle each timeframe
        shortTerm.Bind(c => ProcessCandle(c, "5min")).Start();
        mediumTerm.Bind(c => ProcessCandle(c, "15min")).Start();
        longTerm.Bind(c => ProcessCandle(c, "1hour")).Start();
    }

    private void ProcessCandle(ICandleMessage candle, string timeframe)
    {
        if (candle.State != CandleStates.Finished)
            return;

        this.AddInfoLog($"[{timeframe}] Close={candle.ClosePrice}, Volume={candle.TotalVolume}");

        // Align your trading with multiple timeframe trends
    }
}
```

### Example 6: Gap Detection

```csharp
var lastCandle = (ICandleMessage)null;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    if (lastCandle != null)
    {
        // Gap up: Current low > Previous high
        if (candle.LowPrice > lastCandle.HighPrice)
        {
            var gapSize = candle.LowPrice - lastCandle.HighPrice;
            var gapPercent = (gapSize / lastCandle.HighPrice) * 100;

            Console.WriteLine($"GAP UP at {candle.OpenTime}");
            Console.WriteLine($"  Gap Size: {gapSize:F2} ({gapPercent:F2}%)");
            Console.WriteLine($"  Previous High: {lastCandle.HighPrice:F2}");
            Console.WriteLine($"  Current Low: {candle.LowPrice:F2}");
        }
        // Gap down: Current high < Previous low
        else if (candle.HighPrice < lastCandle.LowPrice)
        {
            var gapSize = lastCandle.LowPrice - candle.HighPrice;
            var gapPercent = (gapSize / lastCandle.LowPrice) * 100;

            Console.WriteLine($"GAP DOWN at {candle.OpenTime}");
            Console.WriteLine($"  Gap Size: {gapSize:F2} ({gapPercent:F2}%)");
            Console.WriteLine($"  Previous Low: {lastCandle.LowPrice:F2}");
            Console.WriteLine($"  Current High: {candle.HighPrice:F2}");
        }
    }

    lastCandle = candle;
};
```

## Historical Candles

### Loading Historical Data

```csharp
// Load historical candles
var subscription = connector.Subscribe(new Subscription(
    DataType.TimeFrame(TimeSpan.FromMinutes(5)),
    security
)
{
    From = DateTimeOffset.Now.AddDays(-7),  // From 7 days ago
    To = DateTimeOffset.Now                  // To now
});

var candles = new List<ICandleMessage>();

connector.CandleReceived += (sub, candle) =>
{
    if (sub == subscription && candle.State == CandleStates.Finished)
    {
        candles.Add(candle);
    }
};

// Wait for subscription to finish
connector.SubscriptionFinished += (sub) =>
{
    if (sub == subscription)
    {
        Console.WriteLine($"Loaded {candles.Count} historical candles");
    }
};
```

## Best Practices

1. **Always Check State**: Only process finished candles for trading signals
2. **Use Appropriate Timeframe**: Match timeframe to your trading style
3. **Handle Active Candles**: Active candles update continuously until finished
4. **Memory Management**: Don't store unlimited candles; use rolling windows
5. **Multi-Timeframe**: Consider multiple timeframes for better context

```csharp
// Good: Check candle state
connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State == CandleStates.Finished)
    {
        // Process completed candle
        ProcessFinishedCandle(candle);
    }
    else if (candle.State == CandleStates.Active)
    {
        // Update UI or indicators
        UpdateCurrentCandle(candle);
    }
};

// Good: Rolling window for candles
var candleWindow = new Queue<ICandleMessage>();
var windowSize = 100;

connector.CandleReceived += (subscription, candle) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    candleWindow.Enqueue(candle);
    while (candleWindow.Count > windowSize)
        candleWindow.Dequeue();
};
```

## Unsubscribing from Candles

```csharp
// Unsubscribe using subscription object
connector.UnSubscribe(subscription);

// Or find and unsubscribe
var dataType = DataType.TimeFrame(TimeSpan.FromMinutes(5));
var subscriptions = connector.FindSubscriptions(security, dataType);
foreach (var sub in subscriptions)
{
    connector.UnSubscribe(sub);
}
```

## Related Topics

- [Level1 Data](level1-data.md) - Real-time quote data
- [Trades](trades.md) - Raw trade data (building blocks for candles)
- [Indicators](../05-Indicators/overview.md) - Technical indicators using candles
- [Strategies](../06-Strategies/overview.md) - Strategy development with candles
- [Storage](../07-Storage/overview.md) - Storing historical candle data
