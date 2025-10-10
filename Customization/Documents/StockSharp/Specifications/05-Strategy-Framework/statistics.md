# Performance Statistics

## Overview

StockSharp's Strategy framework includes a comprehensive statistics system that automatically tracks trading performance metrics. The `StatisticManager` calculates and maintains various statistics including P&L, win rates, Sharpe ratio, drawdown, and many more.

**Location**: `StockSharp.Algo.Statistics.StatisticManager`

## Strategy.StatisticManager Property

### Accessing Statistics

```csharp
public IStatisticManager StatisticManager { get; protected set; }
```

Every strategy has a built-in `StatisticManager`:

```csharp
// Access statistic manager
var stats = StatisticManager;

// Get all parameters
foreach (var param in stats.Parameters)
{
    LogInfo("{0}: {1}", param.Name, param.Value);
}
```

### Available Statistics

The `StatisticManager` provides numerous pre-built statistic parameters:

- **P&L Metrics**: Total P&L, Realized/Unrealized P&L
- **Trade Statistics**: Win rate, average trade, profit factor
- **Risk Metrics**: Sharpe ratio, maximum drawdown, recovery factor
- **Order Statistics**: Total orders, filled orders, canceled orders
- **Performance**: ROI, annual return
- **And many more...**

## Core Performance Properties

### PnL - Profit and Loss

Total profit/loss without commission:

```csharp
// Get current P&L
var currentPnL = PnL;

LogInfo("Current P&L: {0}", currentPnL);

// P&L is updated automatically as trades occur
```

### Commission

Total commission paid:

```csharp
// Get total commission
var totalCommission = Commission;

LogInfo("Total commission: {0}", totalCommission ?? 0);

// Net P&L after commission
var netPnL = PnL - (Commission ?? 0);
```

### Slippage

Total slippage experienced:

```csharp
// Get total slippage
var totalSlippage = Slippage;

LogInfo("Total slippage: {0}", totalSlippage ?? 0);
```

### Latency

Total order latency (registration + cancellation):

```csharp
// Get total latency
var totalLatency = Latency;

if (totalLatency.HasValue)
{
    var avgLatency = totalLatency.Value / Orders.Count();
    LogInfo("Average latency: {0:F2}ms",
        avgLatency.TotalMilliseconds);
}
```

## P&L Tracking

### PnLManager

The strategy uses `PnLManager` to calculate profit and loss:

```csharp
public IPnLManager PnLManager { get; set; }
```

### P&L Components

```csharp
// Realized P&L (from closed trades)
var realizedPnL = PnLManager.RealizedPnL;

// Unrealized P&L (from open positions)
var unrealizedPnL = PnLManager.UnrealizedPnL;

// Total P&L
var totalPnL = PnL;  // realizedPnL + unrealizedPnL
```

### P&L Events

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Subscribe to P&L changes
    PnLChanged += OnPnLChanged;
    CommissionChanged += OnCommissionChanged;
    SlippageChanged += OnSlippageChanged;
}

private void OnPnLChanged()
{
    LogInfo("P&L updated: {0}", PnL);

    // Check if profit target reached
    if (PnL >= ProfitTarget)
    {
        LogInfo("Profit target reached!");
        Stop();
    }
}

private void OnCommissionChanged()
{
    LogInfo("Commission updated: {0}", Commission);
}

private void OnSlippageChanged()
{
    LogInfo("Slippage updated: {0}", Slippage);
}

protected override void OnStopped()
{
    PnLChanged -= OnPnLChanged;
    CommissionChanged -= OnCommissionChanged;
    SlippageChanged -= OnSlippageChanged;

    base.OnStopped();
}
```

### UnrealizedPnLInterval

Controls how often unrealized P&L is recalculated:

```csharp
// Default is 1 minute
public TimeSpan UnrealizedPnLInterval { get; set; } = TimeSpan.FromMinutes(1);

// Change update frequency
UnrealizedPnLInterval = TimeSpan.FromSeconds(30);
```

## Statistic Parameters

### Accessing Individual Parameters

```csharp
// Get all parameters
var parameters = StatisticManager.Parameters;

// Find specific parameter by type
var winRateParam = parameters
    .OfType<WinRateParameter>()
    .FirstOrDefault();

if (winRateParam != null)
{
    var winRate = winRateParam.Value;
    LogInfo("Win rate: {0:P2}", winRate);
}
```

### Common Statistic Parameters

#### Win Rate

```csharp
var winRate = parameters
    .OfType<WinRateParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Win rate: {0:P2}", winRate);  // e.g., "Win rate: 65.00%"
```

#### Profit Factor

```csharp
var profitFactor = parameters
    .OfType<ProfitFactorParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Profit factor: {0:F2}", profitFactor);
```

#### Sharpe Ratio

```csharp
var sharpeRatio = parameters
    .OfType<SharpeRatioParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Sharpe ratio: {0:F2}", sharpeRatio);
```

#### Maximum Drawdown

```csharp
var maxDrawdown = parameters
    .OfType<MaxDrawdownParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Max drawdown: {0:C}", maxDrawdown);
```

#### Average Trade

```csharp
var avgTrade = parameters
    .OfType<AverageTradeParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Average trade P&L: {0:C}", avgTrade);
```

#### Trade Count

```csharp
var tradeCount = parameters
    .OfType<TradeCountParameter>()
    .FirstOrDefault()?.Value ?? 0;

LogInfo("Total trades: {0}", tradeCount);
```

## Complete Statistics Example

```csharp
public class StatisticsAwareStrategy : Strategy
{
    private readonly StrategyParam<decimal> _profitTarget;
    private readonly StrategyParam<decimal> _maxDrawdown;
    private readonly StrategyParam<decimal> _minWinRate;

    public StatisticsAwareStrategy()
    {
        _profitTarget = Param(nameof(ProfitTarget), 5000m)
            .SetGreaterThanZero()
            .SetDisplay("Profit Target", "Stop at this profit", "Statistics");

        _maxDrawdown = Param(nameof(MaxDrawdown), 1000m)
            .SetGreaterThanZero()
            .SetDisplay("Max Drawdown", "Stop at this drawdown", "Statistics");

        _minWinRate = Param(nameof(MinWinRate), 0.5m)
            .SetRange(0, 1)
            .SetDisplay("Min Win Rate", "Required win rate", "Statistics");
    }

    public decimal ProfitTarget
    {
        get => _profitTarget.Value;
        set => _profitTarget.Value = value;
    }

    public decimal MaxDrawdown
    {
        get => _maxDrawdown.Value;
        set => _maxDrawdown.Value = value;
    }

    public decimal MinWinRate
    {
        get => _minWinRate.Value;
        set => _minWinRate.Value = value;
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to statistic events
        PnLChanged += OnPnLChanged;

        // Log initial statistics
        LogStatistics();

        // Configure unrealized P&L update frequency
        UnrealizedPnLInterval = TimeSpan.FromSeconds(30);

        // Setup trading logic...
        var sma = new SimpleMovingAverage { Length = 20 };
        Indicators.Add(sma);

        var subscription = SubscribeCandles(TimeSpan.FromMinutes(5).TimeFrame());
        subscription.Bind(sma, ProcessCandle).Start();
    }

    private void ProcessCandle(ICandleMessage candle, decimal smaValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        if (!IsFormedAndOnlineAndAllowTrading())
            return;

        // Check statistics before trading
        if (!CheckStatistics())
        {
            LogWarning("Statistics check failed, not trading");
            return;
        }

        // Trading logic
        var price = candle.ClosePrice;

        if (price > smaValue && Position <= 0)
        {
            BuyMarket(Volume);
        }
        else if (price < smaValue && Position >= 0)
        {
            SellMarket(Volume);
        }
    }

    private bool CheckStatistics()
    {
        var params = StatisticManager.Parameters;

        // Check win rate if we have enough trades
        var tradeCount = params
            .OfType<TradeCountParameter>()
            .FirstOrDefault()?.Value ?? 0;

        if (tradeCount >= 10)
        {
            var winRate = params
                .OfType<WinRateParameter>()
                .FirstOrDefault()?.Value ?? 0;

            if (winRate < MinWinRate)
            {
                LogWarning("Win rate too low: {0:P2} < {1:P2}",
                    winRate, MinWinRate);
                return false;
            }
        }

        // Check drawdown
        var drawdown = params
            .OfType<MaxDrawdownParameter>()
            .FirstOrDefault()?.Value ?? 0;

        if (Math.Abs(drawdown) > MaxDrawdown)
        {
            LogWarning("Max drawdown exceeded: {0:C} > {1:C}",
                Math.Abs(drawdown), MaxDrawdown);
            Stop();
            return false;
        }

        return true;
    }

    private void OnPnLChanged()
    {
        // Log P&L changes
        LogInfo("P&L: {0:C}, Commission: {1:C}, Net: {2:C}",
            PnL, Commission ?? 0, PnL - (Commission ?? 0));

        // Check profit target
        if (PnL >= ProfitTarget)
        {
            LogInfo("Profit target reached: {0:C}", PnL);
            LogStatistics();
            Stop();
        }

        // Periodic statistics logging
        if (Orders.Count() % 10 == 0)
        {
            LogStatistics();
        }
    }

    private void LogStatistics()
    {
        var params = StatisticManager.Parameters;

        LogInfo("=== Strategy Statistics ===");

        // P&L
        LogInfo("P&L: {0:C}", PnL);
        LogInfo("Commission: {0:C}", Commission ?? 0);
        LogInfo("Slippage: {0:C}", Slippage ?? 0);
        LogInfo("Net P&L: {0:C}", PnL - (Commission ?? 0));

        // Trade statistics
        var tradeCount = params.OfType<TradeCountParameter>()
            .FirstOrDefault()?.Value ?? 0;
        LogInfo("Total trades: {0}", tradeCount);

        if (tradeCount > 0)
        {
            var winRate = params.OfType<WinRateParameter>()
                .FirstOrDefault()?.Value ?? 0;
            LogInfo("Win rate: {0:P2}", winRate);

            var avgTrade = params.OfType<AverageTradeParameter>()
                .FirstOrDefault()?.Value ?? 0;
            LogInfo("Average trade: {0:C}", avgTrade);

            var profitFactor = params.OfType<ProfitFactorParameter>()
                .FirstOrDefault()?.Value ?? 0;
            LogInfo("Profit factor: {0:F2}", profitFactor);
        }

        // Risk metrics
        var maxDrawdown = params.OfType<MaxDrawdownParameter>()
            .FirstOrDefault()?.Value ?? 0;
        LogInfo("Max drawdown: {0:C}", maxDrawdown);

        if (tradeCount > 10)
        {
            var sharpeRatio = params.OfType<SharpeRatioParameter>()
                .FirstOrDefault()?.Value ?? 0;
            LogInfo("Sharpe ratio: {0:F2}", sharpeRatio);
        }

        // Order statistics
        var orderCount = Orders.Count();
        LogInfo("Total orders: {0}", orderCount);

        var activeOrders = Orders.Count(o => o.State.IsActive());
        LogInfo("Active orders: {0}", activeOrders);

        // Position
        LogInfo("Current position: {0}", Position);

        // Latency
        if (Latency.HasValue && orderCount > 0)
        {
            var avgLatency = Latency.Value.TotalMilliseconds / orderCount;
            LogInfo("Avg latency: {0:F2}ms", avgLatency);
        }

        LogInfo("========================");
    }

    protected override void OnStopped()
    {
        PnLChanged -= OnPnLChanged;

        // Log final statistics
        LogInfo("Strategy stopped. Final statistics:");
        LogStatistics();

        base.OnStopped();
    }
}
```

## Statistic Manager Operations

### Add Statistics

Statistics are automatically updated as trades occur:

```csharp
// Automatically called by strategy framework
StatisticManager.AddMyTrade(tradeInfo);      // When trade occurs
StatisticManager.AddNewOrder(order);         // When order registered
StatisticManager.AddChangedOrder(order);     // When order changes
StatisticManager.AddPnL(time, pnl, commission);  // P&L updates
StatisticManager.AddPosition(time, position);    // Position changes
```

### Reset Statistics

```csharp
// Reset all statistics
StatisticManager.Reset();

// Called automatically on strategy Reset()
public override void Reset()
{
    base.Reset();  // Resets StatisticManager
}
```

### Statistics Persistence

Statistics can be saved/loaded with strategy:

```csharp
// Save with statistics
var storage = new SettingsStorage();
strategy.Save(storage);  // Saves statistics if KeepStatistics = true

// Load with statistics
strategy.Load(storage);

// Control statistics persistence
strategy.KeepStatistics = true;  // Keep stats across restarts
```

## Risk-Free Rate

Set the risk-free rate for Sharpe ratio calculation:

```csharp
// Annual risk-free rate (e.g., 3%)
public decimal RiskFreeRate { get; set; } = 0.03m;

// Used in Sharpe ratio and other risk-adjusted metrics
```

## Performance Metrics Display

### In UI Property Grid

Statistics are automatically displayed in UI:

```csharp
[Display(
    ResourceType = typeof(LocalizedStrings),
    Name = LocalizedStrings.PnLKey,
    Description = LocalizedStrings.TotalPnLKey,
    GroupName = LocalizedStrings.StatisticsKey,
    Order = 100)]
[ReadOnly(true)]
[Browsable(false)]
public decimal PnL => PnLManager.GetPnL();
```

### Custom Display

```csharp
// Create custom statistics display
private string GetStatisticsSummary()
{
    var sb = new StringBuilder();
    sb.AppendLine("Strategy Performance:");
    sb.AppendLine($"P&L: {PnL:C}");
    sb.AppendLine($"Commission: {Commission:C}");
    sb.AppendLine($"Net P&L: {PnL - (Commission ?? 0):C}");

    var params = StatisticManager.Parameters;

    var trades = params.OfType<TradeCountParameter>()
        .FirstOrDefault()?.Value ?? 0;
    sb.AppendLine($"Trades: {trades}");

    if (trades > 0)
    {
        var winRate = params.OfType<WinRateParameter>()
            .FirstOrDefault()?.Value ?? 0;
        sb.AppendLine($"Win Rate: {winRate:P2}");
    }

    return sb.ToString();
}
```

## Best Practices

### 1. Monitor Key Statistics

```csharp
// Monitor critical metrics
private void CheckStatistics()
{
    var params = StatisticManager.Parameters;

    var winRate = params.OfType<WinRateParameter>()
        .FirstOrDefault()?.Value ?? 0;

    var drawdown = params.OfType<MaxDrawdownParameter>()
        .FirstOrDefault()?.Value ?? 0;

    var profitFactor = params.OfType<ProfitFactorParameter>()
        .FirstOrDefault()?.Value ?? 0;

    if (winRate < 0.5m || Math.Abs(drawdown) > 1000m || profitFactor < 1.0m)
    {
        LogWarning("Poor performance detected");
        // Take action
    }
}
```

### 2. Log Statistics Periodically

```csharp
// Log every N trades
if (MyTrades.Count() % 10 == 0)
{
    LogStatistics();
}

// Or on schedule
this.WhenIntervalElapsed(TimeSpan.FromMinutes(15))
    .Do(() => LogStatistics())
    .Apply(this);
```

### 3. Use Statistics for Decision Making

```csharp
// Adjust strategy based on performance
if (GetWinRate() < MinAcceptableWinRate)
{
    // Reduce position size or stop trading
    Volume *= 0.5m;
    LogWarning("Reducing volume due to low win rate");
}
```

### 4. Enable Statistics Persistence

```csharp
// Keep statistics across strategy restarts
KeepStatistics = true;

// Useful for live trading sessions that may restart
```

### 5. Wait for Sufficient Data

```csharp
// Don't draw conclusions from insufficient data
var tradeCount = StatisticManager.Parameters
    .OfType<TradeCountParameter>()
    .FirstOrDefault()?.Value ?? 0;

if (tradeCount < 30)
{
    LogInfo("Insufficient trades for statistics ({0})", tradeCount);
    return;
}

// Now can reliably use win rate, Sharpe ratio, etc.
```

## Common Pitfalls

### 1. Not Waiting for Enough Trades

```csharp
// WRONG - statistic not meaningful with few trades
var winRate = GetWinRate();
if (winRate < 0.5m)
    Stop();  // May stop after just 1-2 trades!

// CORRECT
var tradeCount = GetTradeCount();
if (tradeCount >= 30)
{
    var winRate = GetWinRate();
    if (winRate < 0.5m)
        Stop();
}
```

### 2. Ignoring Commission

```csharp
// WRONG - doesn't account for costs
if (PnL > 0)
    LogInfo("Profitable");

// CORRECT
var netPnL = PnL - (Commission ?? 0);
if (netPnL > 0)
    LogInfo("Profitable after commission");
```

### 3. Not Checking for Null

```csharp
// WRONG - may throw NullReferenceException
var avgLatency = Latency.Value.TotalMilliseconds / Orders.Count();

// CORRECT
if (Latency.HasValue && Orders.Any())
{
    var avgLatency = Latency.Value.TotalMilliseconds / Orders.Count();
}
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy lifecycle and properties
- [risk-management.md](risk-management.md) - Using statistics for risk control
- [position-tracking.md](position-tracking.md) - Position-based statistics
