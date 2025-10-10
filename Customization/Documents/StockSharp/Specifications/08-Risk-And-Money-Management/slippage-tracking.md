# Slippage Tracking Specification

## Overview

Slippage tracking in StockSharp measures the difference between expected execution price and actual execution price. This is critical for evaluating strategy performance, optimizing execution algorithms, and identifying market impact. The system tracks both positive and negative slippage across all trades.

## Core Architecture

### ISlippageManager Interface

```csharp
public interface ISlippageManager : IPersistable
{
    decimal Slippage { get; }

    void Reset();
    decimal? ProcessMessage(Message message);
}
```

**Key Responsibilities:**
- Track cumulative slippage
- Calculate per-trade slippage
- Monitor order book prices
- Distinguish between planned and actual execution prices

### SlippageManager Implementation

```csharp
public class SlippageManager : ISlippageManager
{
    private readonly SynchronizedDictionary<SecurityId, RefPair<decimal, decimal>>
        _bestPrices = [];

    private readonly SynchronizedDictionary<long, (Sides side, decimal price)>
        _plannedPrices = [];

    public decimal Slippage { get; private set; }
    public bool CalculateNegative { get; set; } = true;

    public void Reset()
    {
        Slippage = 0;
        _bestPrices.Clear();
        _plannedPrices.Clear();
    }
}
```

**Features:**
- Security-specific price tracking
- Order-level planned price storage
- Configurable negative slippage calculation
- Thread-safe operations

## Slippage Calculation

### Formula

```
Slippage = (Actual Price - Expected Price) × Volume × Direction Factor

Where:
- Direction Factor: +1 for buy (higher = worse), -1 for sell (lower = worse)
- Expected Price: Best ask (buy) or best bid (sell) at order time
- Actual Price: Execution price from trade
```

### Example Calculations

#### Buy Order Slippage

```csharp
// Order placed when:
// Best Ask: $100.00

// Trade executed at: $100.05
// Volume: 10 shares

// Slippage = (100.05 - 100.00) × 10 × 1 = $0.50 (negative, paid more)

// Trade executed at: $99.95
// Volume: 10 shares

// Slippage = (99.95 - 100.00) × 10 × 1 = -$0.50 (positive, paid less)
```

#### Sell Order Slippage

```csharp
// Order placed when:
// Best Bid: $100.00

// Trade executed at: $99.95
// Volume: 10 shares

// Slippage = (100.00 - 99.95) × 10 × 1 = $0.50 (negative, received less)

// Trade executed at: $100.05
// Volume: 10 shares

// Slippage = (100.00 - 100.05) × 10 × 1 = -$0.50 (positive, received more)
```

## Processing Flow

### 1. Capture Best Prices

```csharp
public decimal? ProcessMessage(Message message)
{
    switch (message.Type)
    {
        case MessageTypes.Level1Change:
        {
            var l1Msg = (Level1ChangeMessage)message;
            var pair = _bestPrices.SafeAdd(l1Msg.SecurityId);

            var bidPrice = l1Msg.TryGetDecimal(Level1Fields.BestBidPrice);
            if (bidPrice != null)
                pair.First = bidPrice.Value;

            var askPrice = l1Msg.TryGetDecimal(Level1Fields.BestAskPrice);
            if (askPrice != null)
                pair.Second = askPrice.Value;

            break;
        }

        case MessageTypes.QuoteChange:
        {
            var quotesMsg = (QuoteChangeMessage)message;

            if (quotesMsg.State != null)
                break;

            var pair = _bestPrices.SafeAdd(quotesMsg.SecurityId);

            var bid = quotesMsg.GetBestBid();
            if (bid != null)
                pair.First = bid.Value.Price;

            var ask = quotesMsg.GetBestAsk();
            if (ask != null)
                pair.Second = ask.Value.Price;

            break;
        }
    }

    return null;
}
```

### 2. Store Planned Prices

```csharp
case MessageTypes.OrderRegister:
{
    var regMsg = (OrderRegisterMessage)message;

    var prices = _bestPrices.TryGetValue(regMsg.SecurityId);

    if (prices != null)
    {
        // Use opposite side of market:
        // Buy orders use ask price
        // Sell orders use bid price
        var price = regMsg.Side == Sides.Buy
            ? prices.Second  // Ask
            : prices.First;  // Bid

        if (price != 0)
            _plannedPrices[regMsg.TransactionId] = (regMsg.Side, price);
    }

    break;
}
```

### 3. Calculate Trade Slippage

```csharp
case MessageTypes.Execution:
{
    var execMsg = (ExecutionMessage)message;

    if (execMsg.HasTradeInfo())
    {
        if (_plannedPrices.TryGetValue(
            execMsg.OriginalTransactionId, out var t))
        {
            if (execMsg.TradePrice == null)
                return null;

            // Calculate price difference
            var diff = t.side == Sides.Buy
                ? execMsg.TradePrice.Value - t.price
                : t.price - execMsg.TradePrice.Value;

            var volume = execMsg.TradeVolume ?? 1m;
            var weighted = diff * volume;

            // Optionally exclude positive slippage
            if (!CalculateNegative && weighted < 0)
                weighted = 0;

            Slippage += weighted;

            // Cleanup when order completed
            if (execMsg.HasOrderInfo() &&
                (execMsg.OrderState == OrderStates.Done ||
                 execMsg.Balance == 0))
            {
                _plannedPrices.Remove(execMsg.OriginalTransactionId);
            }

            return weighted;
        }
    }

    break;
}
```

## MyTrade.Slippage Property

### Automatic Slippage Assignment

```csharp
public class Strategy
{
    public bool TryAddMyTrade(MyTrade trade)
    {
        // ... other processing ...

        if (trade.Slippage is decimal slippage)
        {
            Slippage = (Slippage ?? 0) + slippage;
            isSlipChanged = true;
        }

        // ... more processing ...

        if (isSlipChanged)
            RaiseSlippageChanged();

        return true;
    }
}
```

### Accessing Trade Slippage

```csharp
protected override void OnNewMyTrade(MyTrade trade)
{
    if (trade.Slippage.HasValue)
    {
        this.AddInfoLog($"Trade slippage: {trade.Slippage:C}");

        if (trade.Slippage.Value > 10)
        {
            this.AddWarningLog("High slippage detected!");
        }
    }

    base.OnNewMyTrade(trade);
}
```

## Slippage Configuration

### Enable/Disable Negative Slippage

```csharp
var slippageManager = new SlippageManager
{
    // Calculate both positive and negative slippage (default)
    CalculateNegative = true
};

// Or track only negative slippage
var slippageManager2 = new SlippageManager
{
    CalculateNegative = false  // Only count when execution is worse
};
```

**Comparison:**

| Scenario | CalculateNegative = true | CalculateNegative = false |
|----------|-------------------------|---------------------------|
| Buy @ 100, Expected 100.05 | Slippage: -0.50 | Slippage: 0 |
| Buy @ 100.10, Expected 100.05 | Slippage: +0.50 | Slippage: +0.50 |
| Sell @ 100, Expected 99.95 | Slippage: -0.50 | Slippage: 0 |
| Sell @ 99.90, Expected 99.95 | Slippage: +0.50 | Slippage: +0.50 |

## Integration with Strategy

### Basic Monitoring

```csharp
public class SlippageAwareStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // Subscribe to slippage changes
        SlippageChanged += OnSlippageChanged;

        base.OnStarted(time);
    }

    private void OnSlippageChanged()
    {
        var avgSlippage = Slippage / MyTrades.Count();

        this.AddInfoLog($"Total Slippage: {Slippage:C}, " +
                       $"Avg: {avgSlippage:C}");

        // Check slippage threshold
        if (avgSlippage > 5)
        {
            this.AddWarningLog("Average slippage exceeds $5!");
        }
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        if (trade.Slippage.HasValue)
        {
            var slippagePct = Math.Abs(trade.Slippage.Value) /
                (trade.Trade.Price * trade.Trade.Volume) * 100;

            this.AddInfoLog($"Trade slippage: {slippagePct:F4}%");
        }

        base.OnNewMyTrade(trade);
    }
}
```

### Advanced Slippage Analysis

```csharp
public class SlippageAnalyzer
{
    private readonly List<decimal> _slippages = [];

    public void RecordSlippage(MyTrade trade)
    {
        if (trade.Slippage.HasValue)
            _slippages.Add(trade.Slippage.Value);
    }

    public SlippageStatistics GetStatistics()
    {
        if (_slippages.Count == 0)
            return new SlippageStatistics();

        return new SlippageStatistics
        {
            Total = _slippages.Sum(),
            Average = _slippages.Average(),
            Median = CalculateMedian(_slippages),
            StdDev = CalculateStdDev(_slippages),
            Min = _slippages.Min(),
            Max = _slippages.Max(),
            Count = _slippages.Count,
            PositiveCount = _slippages.Count(s => s < 0),  // Better than expected
            NegativeCount = _slippages.Count(s => s > 0)   // Worse than expected
        };
    }

    private decimal CalculateMedian(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2;
        else
            return sorted[mid];
    }

    private decimal CalculateStdDev(List<decimal> values)
    {
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        return (decimal)Math.Sqrt((double)(sumOfSquares / values.Count));
    }
}

public class SlippageStatistics
{
    public decimal Total { get; set; }
    public decimal Average { get; set; }
    public decimal Median { get; set; }
    public decimal StdDev { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public int Count { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }

    public decimal PositivePercentage => Count == 0
        ? 0
        : (PositiveCount / (decimal)Count) * 100;

    public decimal NegativePercentage => Count == 0
        ? 0
        : (NegativeCount / (decimal)Count) * 100;
}
```

### Usage in Strategy

```csharp
public class AnalyzingStrategy : Strategy
{
    private readonly SlippageAnalyzer _analyzer = new();

    protected override void OnNewMyTrade(MyTrade trade)
    {
        _analyzer.RecordSlippage(trade);

        // Analyze after every 100 trades
        if (MyTrades.Count() % 100 == 0)
        {
            var stats = _analyzer.GetStatistics();

            this.AddInfoLog(
                $"Slippage Stats - " +
                $"Avg: {stats.Average:C}, " +
                $"Median: {stats.Median:C}, " +
                $"StdDev: {stats.StdDev:C}, " +
                $"Positive: {stats.PositivePercentage:F1}%, " +
                $"Negative: {stats.NegativePercentage:F1}%");
        }

        base.OnNewMyTrade(trade);
    }
}
```

## Slippage Risk Management

### Risk Rule Integration

```csharp
public class RiskSlippageRule : RiskRule
{
    public Unit Slippage { get; set; }

    protected override string GetTitle()
    {
        return $"Slippage {Slippage}";
    }

    public override bool ProcessMessage(Message message)
    {
        // Implementation in StockSharp
        // Triggers when total slippage exceeds threshold
        return false;
    }
}

// Usage
RiskManager.Rules.Add(new RiskSlippageRule
{
    Slippage = new Unit(1000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});
```

### Dynamic Order Type Adjustment

```csharp
public class AdaptiveExecutionStrategy : Strategy
{
    private decimal _recentSlippageAvg;
    private const int SampleSize = 10;

    protected override void OnNewMyTrade(MyTrade trade)
    {
        if (trade.Slippage.HasValue)
        {
            UpdateSlippageAverage(trade.Slippage.Value);
        }

        base.OnNewMyTrade(trade);
    }

    private void UpdateSlippageAverage(decimal slippage)
    {
        var recentTrades = MyTrades
            .TakeLast(SampleSize)
            .Where(t => t.Slippage.HasValue)
            .ToList();

        if (recentTrades.Count > 0)
        {
            _recentSlippageAvg = recentTrades
                .Average(t => t.Slippage.Value);
        }
    }

    protected Order CreateAdaptiveOrder(
        Sides side, decimal volume, decimal? limitPrice = null)
    {
        // Use limit orders if high slippage detected
        if (Math.Abs(_recentSlippageAvg) > 2)
        {
            this.AddInfoLog("High slippage detected, using limit order");

            return new Order
            {
                Type = OrderTypes.Limit,
                Side = side,
                Volume = volume,
                Price = limitPrice ?? GetConservativePrice(side)
            };
        }

        // Use market orders when slippage is acceptable
        return new Order
        {
            Type = OrderTypes.Market,
            Side = side,
            Volume = volume
        };
    }

    private decimal GetConservativePrice(Sides side)
    {
        var security = Security;
        var currentPrice = security.LastTrade?.Price ?? 0;

        // Add buffer based on recent slippage
        var buffer = Math.Abs(_recentSlippageAvg) * 1.5m;

        return side == Sides.Buy
            ? currentPrice + buffer
            : currentPrice - buffer;
    }
}
```

## Slippage by Order Type

### Market Orders vs Limit Orders

```csharp
public class ExecutionQualityAnalyzer
{
    private readonly Dictionary<OrderTypes, List<decimal>> _slippageByType = new();

    public void RecordSlippage(Order order, decimal? slippage)
    {
        if (!slippage.HasValue)
            return;

        if (!_slippageByType.ContainsKey(order.Type))
            _slippageByType[order.Type] = [];

        _slippageByType[order.Type].Add(slippage.Value);
    }

    public void PrintAnalysis()
    {
        Console.WriteLine("Slippage by Order Type:");
        Console.WriteLine("========================");

        foreach (var kvp in _slippageByType)
        {
            var avg = kvp.Value.Average();
            var count = kvp.Value.Count;

            Console.WriteLine($"{kvp.Key}: " +
                            $"Avg={avg:C}, " +
                            $"Count={count}");
        }
    }
}
```

## Time-Based Slippage Analysis

### Slippage by Time of Day

```csharp
public class TimeBasedSlippageAnalyzer
{
    private readonly Dictionary<int, List<decimal>> _slippageByHour = new();

    public void RecordSlippage(DateTimeOffset time, decimal slippage)
    {
        var hour = time.Hour;

        if (!_slippageByHour.ContainsKey(hour))
            _slippageByHour[hour] = [];

        _slippageByHour[hour].Add(slippage);
    }

    public void PrintHourlyAnalysis()
    {
        Console.WriteLine("Slippage by Hour:");
        Console.WriteLine("=================");

        for (int hour = 9; hour <= 16; hour++)
        {
            if (_slippageByHour.TryGetValue(hour, out var slippages)
                && slippages.Count > 0)
            {
                var avg = slippages.Average();
                var count = slippages.Count;

                Console.WriteLine($"{hour:D2}:00 - " +
                                $"Avg={avg:C}, " +
                                $"Count={count}");
            }
        }
    }

    public int GetBestExecutionHour()
    {
        return _slippageByHour
            .Where(kvp => kvp.Value.Count >= 10)  // Min sample size
            .OrderBy(kvp => Math.Abs(kvp.Value.Average()))
            .First()
            .Key;
    }
}
```

## Backtesting Considerations

### Realistic Slippage Modeling

```csharp
public class RealisticSlippageAdapter : MessageAdapter
{
    private readonly decimal _minSlippagePct;
    private readonly decimal _maxSlippagePct;

    public RealisticSlippageAdapter(
        decimal minSlippagePct = 0.01m,
        decimal maxSlippagePct = 0.05m)
    {
        _minSlippagePct = minSlippagePct;
        _maxSlippagePct = maxSlippagePct;
    }

    protected override void OnSendInMessage(Message message)
    {
        if (message is OrderRegisterMessage orderMsg)
        {
            // Apply slippage model
            var slippagePct = GetSlippageForOrder(orderMsg);

            // Adjust expected execution price
            var slippage = orderMsg.Side == Sides.Buy
                ? orderMsg.Price * (1 + slippagePct)
                : orderMsg.Price * (1 - slippagePct);

            // Store for later execution simulation
        }

        base.OnSendInMessage(message);
    }

    private decimal GetSlippageForOrder(OrderRegisterMessage order)
    {
        // Model slippage based on:
        // 1. Order size
        // 2. Market conditions
        // 3. Time of day
        // 4. Volatility

        var baseSlippage = _minSlippagePct;

        // Larger orders have more slippage
        if (order.Volume > 100)
            baseSlippage += 0.01m;

        // Random component for realism
        var random = new Random().NextDouble();
        var randomComponent = (decimal)random
            * (_maxSlippagePct - _minSlippagePct);

        return baseSlippage + randomComponent;
    }
}
```

## Best Practices

### 1. Monitor Slippage Trends

```csharp
// Track moving average
private decimal GetMovingAverageSlippage(int periods = 20)
{
    var recentSlippages = MyTrades
        .TakeLast(periods)
        .Where(t => t.Slippage.HasValue)
        .Select(t => t.Slippage.Value)
        .ToList();

    return recentSlippages.Count > 0
        ? recentSlippages.Average()
        : 0;
}
```

### 2. Set Slippage Alerts

```csharp
private void CheckSlippageAlert(MyTrade trade)
{
    if (!trade.Slippage.HasValue)
        return;

    var slippagePct = Math.Abs(trade.Slippage.Value)
        / (trade.Trade.Price * trade.Trade.Volume) * 100;

    if (slippagePct > 0.5m)  // 0.5% threshold
    {
        this.AddWarningLog($"High slippage: {slippagePct:F2}%");

        // Consider using limit orders
        _useMarketOrders = false;
    }
}
```

### 3. Adjust Strategy Based on Slippage

```csharp
private bool ShouldTrade()
{
    var recentSlippage = GetMovingAverageSlippage();

    // Don't trade if slippage too high
    if (Math.Abs(recentSlippage) > 10)
    {
        this.AddInfoLog("Skipping trade due to high slippage");
        return false;
    }

    return true;
}
```

### 4. Include Slippage in Performance Metrics

```csharp
public class EnhancedPerformanceMetrics
{
    public decimal GrossPnL { get; set; }
    public decimal Commission { get; set; }
    public decimal Slippage { get; set; }

    public decimal NetPnL => GrossPnL - Commission - Slippage;

    public decimal SlippageAsPercentOfPnL =>
        GrossPnL == 0 ? 0 : (Slippage / Math.Abs(GrossPnL)) * 100;

    public void PrintReport()
    {
        Console.WriteLine("Performance Report:");
        Console.WriteLine($"Gross PnL: {GrossPnL:C}");
        Console.WriteLine($"Commission: {Commission:C}");
        Console.WriteLine($"Slippage: {Slippage:C}");
        Console.WriteLine($"Net PnL: {NetPnL:C}");
        Console.WriteLine($"Slippage Impact: {SlippageAsPercentOfPnL:F2}%");
    }
}
```

## See Also

- [Risk Rules](risk-rules.md)
- [PnL Calculation](pnl-calculation.md)
- [Commission Calculation](commission-calculation.md)
- [Position Sizing](position-sizing.md)
- [Strategy Framework](../05-Strategy-Framework/strategies.md)
