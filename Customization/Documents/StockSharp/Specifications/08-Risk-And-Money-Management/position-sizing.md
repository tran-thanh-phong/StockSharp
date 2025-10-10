# Position Sizing & Volume Management Specification

## Overview

Position sizing is a critical risk management component that determines how much capital to allocate to each trade. StockSharp provides multiple mechanisms for controlling position sizes, including volume limits, position-based rules, risk-based sizing, and portfolio allocation strategies. Proper position sizing is essential for capital preservation and optimizing risk-adjusted returns.

## Position Size Limits

### RiskPositionSizeRule

**Purpose:** Enforces maximum position size limits to prevent excessive concentration risk.

```csharp
public class RiskPositionSizeRule : RiskRule
{
    public decimal Position { get; set; }

    public override bool ProcessMessage(Message message)
    {
        if (message.Type != MessageTypes.PositionChange)
            return false;

        var posMsg = (PositionChangeMessage)message;
        var currValue = posMsg.TryGetDecimal(PositionChangeTypes.CurrentValue);

        if (currValue == null)
            return false;

        if (Position > 0)
            return currValue >= Position;
        else
            return currValue <= Position;
    }
}
```

**Usage:**
```csharp
// Limit long positions to 100 contracts
RiskManager.Rules.Add(new RiskPositionSizeRule
{
    Position = 100,
    Action = RiskActions.CancelOrders
});

// Limit short positions to -50 contracts
RiskManager.Rules.Add(new RiskPositionSizeRule
{
    Position = -50,
    Action = RiskActions.CancelOrders
});
```

### RiskOrderVolumeRule

**Purpose:** Prevents individual orders from exceeding size limits.

```csharp
public class RiskOrderVolumeRule : RiskRule
{
    public decimal Volume { get; set; }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Type)
        {
            case MessageTypes.OrderRegister:
            {
                var orderReg = (OrderRegisterMessage)message;
                return orderReg.Volume >= Volume;
            }

            case MessageTypes.OrderReplace:
            {
                var orderReplace = (OrderReplaceMessage)message;
                return orderReplace.Volume > 0
                    && orderReplace.Volume >= Volume;
            }

            default:
                return false;
        }
    }
}
```

**Usage:**
```csharp
// Maximum 50 contracts per order
RiskManager.Rules.Add(new RiskOrderVolumeRule
{
    Volume = 50,
    Action = RiskActions.CancelOrders
});
```

## Position Tracking

### Strategy.Position Property

```csharp
public class Strategy
{
    /// <summary>
    /// Current position for the strategy's security and portfolio.
    /// </summary>
    public decimal Position =>
        GetPositionValue(Security, Portfolio) ?? 0;
}
```

**Accessing Current Position:**
```csharp
protected override void OnStarted(DateTimeOffset time)
{
    var currentPosition = Position;
    this.AddInfoLog($"Starting position: {currentPosition}");

    base.OnStarted(time);
}

protected override void OnNewMyTrade(MyTrade trade)
{
    var newPosition = Position;
    this.AddInfoLog($"Position after trade: {newPosition}");

    base.OnNewMyTrade(trade);
}
```

### Position Change Events

```csharp
public class PositionTrackingStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        PositionChanged += OnPositionChanged;
        base.OnStarted(time);
    }

    private void OnPositionChanged(Position position)
    {
        this.AddInfoLog($"Position changed: {position.CurrentValue}");

        // React to position changes
        if (Math.Abs(position.CurrentValue) > 100)
        {
            this.AddWarningLog("Position size exceeds 100!");
        }
    }
}
```

## Risk-Based Position Sizing

### Fixed Risk per Trade

**Concept:** Risk a fixed percentage of capital on each trade.

```csharp
public class FixedRiskStrategy : Strategy
{
    public decimal RiskPerTradePercent { get; set; } = 1.0m;  // 1% risk

    protected decimal CalculatePositionSize(
        decimal entryPrice,
        decimal stopLoss)
    {
        var portfolio = Portfolio;
        if (portfolio == null)
            return 0;

        var capitalAtRisk = portfolio.CurrentValue
            * (RiskPerTradePercent / 100m);

        var riskPerUnit = Math.Abs(entryPrice - stopLoss);

        if (riskPerUnit == 0)
            return 0;

        var positionSize = capitalAtRisk / riskPerUnit;

        // Round to lot size
        return Math.Floor(positionSize);
    }
}

// Example usage:
// Portfolio Value: $100,000
// Risk per Trade: 1% = $1,000
// Entry Price: $50
// Stop Loss: $48
// Risk per Share: $2
// Position Size: $1,000 / $2 = 500 shares
```

### Kelly Criterion

**Concept:** Optimize position size based on win rate and risk/reward ratio.

```csharp
public class KellyPositionSizer
{
    public decimal WinRate { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }

    public decimal CalculateKellyPercentage()
    {
        if (AverageLoss == 0)
            return 0;

        var winLossRatio = AverageWin / AverageLoss;

        // Kelly Formula: f = (bp - q) / b
        // f = fraction to bet
        // b = win/loss ratio
        // p = probability of win
        // q = probability of loss (1 - p)

        var kellyPct = ((winLossRatio * WinRate) - (1 - WinRate))
            / winLossRatio;

        // Use fractional Kelly for safety (e.g., 0.25 Kelly)
        return Math.Max(0, kellyPct * 0.25m);
    }

    public decimal CalculatePositionSize(decimal portfolioValue)
    {
        var kellyPct = CalculateKellyPercentage();
        return portfolioValue * kellyPct;
    }
}

// Example usage:
var sizer = new KellyPositionSizer
{
    WinRate = 0.55m,        // 55% win rate
    AverageWin = 150m,      // Average win: $150
    AverageLoss = 100m      // Average loss: $100
};

var kellyPct = sizer.CalculateKellyPercentage();
// Result: ~8.3% (using 0.25 fractional Kelly)

var positionSize = sizer.CalculatePositionSize(100_000);
// Position size: $8,300
```

### Volatility-Based Sizing

**Concept:** Adjust position size based on market volatility.

```csharp
public class VolatilityBasedSizer
{
    public decimal TargetRisk { get; set; } = 1000m;  // Target $ risk
    public decimal AverageTrueRange { get; set; }     // ATR value

    public decimal CalculatePositionSize(
        decimal currentPrice,
        decimal atrMultiplier = 2m)
    {
        if (AverageTrueRange == 0 || currentPrice == 0)
            return 0;

        // Risk per unit = ATR × multiplier
        var riskPerUnit = AverageTrueRange * atrMultiplier;

        // Position size = Target Risk / Risk per Unit
        var positionSize = TargetRisk / riskPerUnit;

        return Math.Floor(positionSize);
    }
}

// Example usage:
var sizer = new VolatilityBasedSizer
{
    TargetRisk = 1000m,          // Risk $1,000
    AverageTrueRange = 2.50m     // ATR = $2.50
};

var size = sizer.CalculatePositionSize(
    currentPrice: 100m,
    atrMultiplier: 2m
);
// Position size: $1,000 / ($2.50 × 2) = 200 shares
```

## Portfolio Allocation Strategies

### Equal Weight Allocation

```csharp
public class EqualWeightAllocator
{
    public decimal TotalCapital { get; set; }
    public int NumberOfPositions { get; set; }

    public decimal GetPositionAllocation()
    {
        return TotalCapital / NumberOfPositions;
    }

    public decimal GetPositionSize(
        decimal allocationAmount,
        decimal currentPrice)
    {
        if (currentPrice == 0)
            return 0;

        return Math.Floor(allocationAmount / currentPrice);
    }
}

// Example usage:
var allocator = new EqualWeightAllocator
{
    TotalCapital = 100_000m,
    NumberOfPositions = 5
};

var allocation = allocator.GetPositionAllocation();
// Each position: $20,000

var shares = allocator.GetPositionSize(allocation, 50m);
// Position size: 400 shares
```

### Risk Parity Allocation

```csharp
public class RiskParityAllocator
{
    private readonly Dictionary<string, decimal> _volatilities = new();

    public void SetVolatility(string symbol, decimal volatility)
    {
        _volatilities[symbol] = volatility;
    }

    public Dictionary<string, decimal> CalculateWeights()
    {
        var inverseVols = _volatilities.ToDictionary(
            kvp => kvp.Key,
            kvp => 1m / kvp.Value
        );

        var sumInverseVols = inverseVols.Values.Sum();

        return inverseVols.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value / sumInverseVols
        );
    }

    public decimal GetPositionSize(
        string symbol,
        decimal totalCapital,
        decimal currentPrice)
    {
        var weights = CalculateWeights();

        if (!weights.TryGetValue(symbol, out var weight))
            return 0;

        var allocation = totalCapital * weight;
        return Math.Floor(allocation / currentPrice);
    }
}

// Example usage:
var allocator = new RiskParityAllocator();
allocator.SetVolatility("AAPL", 0.20m);  // 20% volatility
allocator.SetVolatility("MSFT", 0.15m);  // 15% volatility
allocator.SetVolatility("GOOGL", 0.25m); // 25% volatility

var weights = allocator.CalculateWeights();
// AAPL: ~33%, MSFT: ~44%, GOOGL: ~23%
```

### Maximum Diversification

```csharp
public class MaxDiversificationAllocator
{
    private readonly Dictionary<string, decimal> _correlations = new();
    private readonly Dictionary<string, decimal> _volatilities = new();

    public void SetCorrelation(string symbol1, string symbol2, decimal correlation)
    {
        var key = GetKey(symbol1, symbol2);
        _correlations[key] = correlation;
    }

    public void SetVolatility(string symbol, decimal volatility)
    {
        _volatilities[symbol] = volatility;
    }

    private string GetKey(string s1, string s2)
    {
        // Ensure consistent key ordering
        return string.Compare(s1, s2) < 0 ? $"{s1}_{s2}" : $"{s2}_{s1}";
    }

    public decimal GetCorrelation(string symbol1, string symbol2)
    {
        if (symbol1 == symbol2)
            return 1m;

        var key = GetKey(symbol1, symbol2);
        return _correlations.TryGetValue(key, out var corr) ? corr : 0m;
    }

    // Simplified weight calculation
    public Dictionary<string, decimal> CalculateWeights()
    {
        var symbols = _volatilities.Keys.ToList();
        var n = symbols.Count;

        // Initialize with equal weights
        var weights = symbols.ToDictionary(s => s, s => 1m / n);

        // Iterative optimization (simplified)
        for (int iter = 0; iter < 100; iter++)
        {
            // Adjust weights to maximize diversification ratio
            // (Full implementation would use portfolio optimization)
        }

        return weights;
    }
}
```

## Dynamic Position Sizing

### Performance-Based Adjustment

```csharp
public class PerformanceBasedSizer
{
    private decimal _basePositionSize;
    private decimal _currentPositionSize;

    public decimal MinPositionSize { get; set; } = 10;
    public decimal MaxPositionSize { get; set; } = 100;
    public decimal AdjustmentFactor { get; set; } = 0.1m;

    public PerformanceBasedSizer(decimal basePositionSize)
    {
        _basePositionSize = basePositionSize;
        _currentPositionSize = basePositionSize;
    }

    public void AdjustForWin()
    {
        // Increase position size after win
        _currentPositionSize *= (1 + AdjustmentFactor);
        _currentPositionSize = Math.Min(_currentPositionSize, MaxPositionSize);
    }

    public void AdjustForLoss()
    {
        // Decrease position size after loss
        _currentPositionSize *= (1 - AdjustmentFactor);
        _currentPositionSize = Math.Max(_currentPositionSize, MinPositionSize);
    }

    public void Reset()
    {
        _currentPositionSize = _basePositionSize;
    }

    public decimal GetCurrentSize()
    {
        return Math.Floor(_currentPositionSize);
    }
}

// Usage in strategy:
public class AdaptiveStrategy : Strategy
{
    private readonly PerformanceBasedSizer _sizer;

    public AdaptiveStrategy()
    {
        _sizer = new PerformanceBasedSizer(basePositionSize: 50)
        {
            MinPositionSize = 10,
            MaxPositionSize = 100,
            AdjustmentFactor = 0.1m
        };
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        if (trade.PnL.HasValue)
        {
            if (trade.PnL.Value > 0)
                _sizer.AdjustForWin();
            else
                _sizer.AdjustForLoss();

            this.AddInfoLog($"Position size adjusted to: {_sizer.GetCurrentSize()}");
        }

        base.OnNewMyTrade(trade);
    }

    protected Order CreateOrder(Sides side)
    {
        var volume = _sizer.GetCurrentSize();

        return new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = volume,
            Type = OrderTypes.Market
        };
    }
}
```

### Drawdown-Based Adjustment

```csharp
public class DrawdownAdjustedSizer
{
    private decimal _peakEquity;
    private decimal _basePositionSize;

    public decimal MaxDrawdownPercent { get; set; } = 10m;
    public decimal MinPositionSizePercent { get; set; } = 50m;

    public DrawdownAdjustedSizer(decimal basePositionSize)
    {
        _basePositionSize = basePositionSize;
        _peakEquity = 0;
    }

    public decimal CalculatePositionSize(decimal currentEquity)
    {
        // Update peak
        if (currentEquity > _peakEquity)
            _peakEquity = currentEquity;

        if (_peakEquity == 0)
            return _basePositionSize;

        // Calculate current drawdown
        var drawdown = (_peakEquity - currentEquity) / _peakEquity * 100;

        if (drawdown <= 0)
            return _basePositionSize;

        // Reduce position size proportionally
        var reduction = (drawdown / MaxDrawdownPercent);
        reduction = Math.Min(reduction, 1m);

        var minSize = _basePositionSize * (MinPositionSizePercent / 100m);
        var adjustedSize = _basePositionSize * (1 - reduction);

        return Math.Max(adjustedSize, minSize);
    }
}

// Usage:
var sizer = new DrawdownAdjustedSizer(basePositionSize: 100)
{
    MaxDrawdownPercent = 10m,
    MinPositionSizePercent = 50m
};

// Peak equity: $100,000, Current: $95,000
var size = sizer.CalculatePositionSize(95_000);
// Drawdown: 5%, Position reduced to ~75 contracts

// Peak equity: $100,000, Current: $90,000
size = sizer.CalculatePositionSize(90_000);
// Drawdown: 10%, Position reduced to 50 contracts (minimum)
```

## Practical Examples

### Example 1: Complete Risk-Based Strategy

```csharp
public class RiskManagedStrategy : Strategy
{
    // Risk parameters
    public decimal RiskPerTradePercent { get; set; } = 1.0m;
    public decimal MaxPositionSize { get; set; } = 200;
    public decimal StopLossPercent { get; set; } = 2.0m;

    protected override void OnStarted(DateTimeOffset time)
    {
        // Configure position size limit
        RiskManager.Rules.Add(new RiskPositionSizeRule
        {
            Position = MaxPositionSize,
            Action = RiskActions.CancelOrders
        });

        base.OnStarted(time);
    }

    protected Order CreateRiskBasedOrder(
        Sides side,
        decimal entryPrice)
    {
        var stopLossPrice = side == Sides.Buy
            ? entryPrice * (1 - StopLossPercent / 100m)
            : entryPrice * (1 + StopLossPercent / 100m);

        var volume = CalculatePositionSize(entryPrice, stopLossPrice);

        // Respect maximum position size
        volume = Math.Min(volume, MaxPositionSize);

        this.AddInfoLog(
            $"Order: {side} {volume} @ {entryPrice}, " +
            $"Stop: {stopLossPrice}");

        return new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = volume,
            Price = entryPrice,
            Type = OrderTypes.Limit
        };
    }

    private decimal CalculatePositionSize(
        decimal entryPrice,
        decimal stopLoss)
    {
        var portfolio = Portfolio;
        if (portfolio == null)
            return 0;

        var capitalAtRisk = portfolio.CurrentValue
            * (RiskPerTradePercent / 100m);

        var riskPerUnit = Math.Abs(entryPrice - stopLoss);

        if (riskPerUnit == 0)
            return 0;

        return Math.Floor(capitalAtRisk / riskPerUnit);
    }
}
```

### Example 2: Portfolio-Based Multi-Security Strategy

```csharp
public class PortfolioStrategy : Strategy
{
    private readonly Dictionary<Security, decimal> _targetWeights = new();
    private readonly Dictionary<Security, decimal> _currentWeights = new();

    public decimal TotalCapital { get; set; } = 100_000m;

    protected override void OnStarted(DateTimeOffset time)
    {
        // Define target weights
        _targetWeights[LookupSecurity("AAPL")] = 0.25m;
        _targetWeights[LookupSecurity("MSFT")] = 0.25m;
        _targetWeights[LookupSecurity("GOOGL")] = 0.25m;
        _targetWeights[LookupSecurity("AMZN")] = 0.25m;

        // Initial rebalance
        Rebalance();

        base.OnStarted(time);
    }

    private void Rebalance()
    {
        var portfolioValue = GetTotalPortfolioValue();

        foreach (var kvp in _targetWeights)
        {
            var security = kvp.Key;
            var targetWeight = kvp.Value;

            var targetValue = portfolioValue * targetWeight;
            var currentValue = GetSecurityValue(security);
            var difference = targetValue - currentValue;

            if (Math.Abs(difference) > portfolioValue * 0.01m) // 1% threshold
            {
                var side = difference > 0 ? Sides.Buy : Sides.Sell;
                var volume = Math.Abs(difference) / security.LastTrade.Price;

                RegisterOrder(new Order
                {
                    Security = security,
                    Portfolio = Portfolio,
                    Side = side,
                    Volume = Math.Floor(volume),
                    Type = OrderTypes.Market
                });
            }
        }
    }

    private decimal GetTotalPortfolioValue()
    {
        return Portfolio.CurrentValue;
    }

    private decimal GetSecurityValue(Security security)
    {
        var position = this.GetPositionValue(security, Portfolio) ?? 0;
        var lastPrice = security.LastTrade?.Price ?? 0;
        return position * lastPrice;
    }

    private Security LookupSecurity(string code)
    {
        return Connector.LookupById(code);
    }
}
```

### Example 3: Scaling In/Out Strategy

```csharp
public class ScalingStrategy : Strategy
{
    public decimal InitialPositionPercent { get; set; } = 33m;  // Start with 33%
    public decimal MaxPosition { get; set; } = 100;

    private decimal _targetPosition;

    protected void EnterPosition(Sides side, decimal price)
    {
        // Initial entry: 33% of max position
        var initialSize = Math.Floor(MaxPosition * (InitialPositionPercent / 100m));

        _targetPosition = MaxPosition;

        RegisterOrder(new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = initialSize,
            Price = price,
            Type = OrderTypes.Limit,
            Comment = "Initial Entry"
        });

        this.AddInfoLog($"Initial entry: {initialSize} @ {price}");
    }

    protected void ScaleIn(Sides side, decimal price)
    {
        var currentPosition = Math.Abs(Position);
        var remaining = _targetPosition - currentPosition;

        if (remaining <= 0)
        {
            this.AddWarningLog("Already at maximum position");
            return;
        }

        // Add 33% of remaining
        var scaleSize = Math.Floor(remaining * (InitialPositionPercent / 100m));

        RegisterOrder(new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = scaleSize,
            Price = price,
            Type = OrderTypes.Limit,
            Comment = "Scale In"
        });

        this.AddInfoLog($"Scale in: {scaleSize} @ {price}");
    }

    protected void ScaleOut(decimal percent)
    {
        var currentPosition = Position;

        if (currentPosition == 0)
            return;

        var exitSize = Math.Floor(Math.Abs(currentPosition) * (percent / 100m));
        var side = currentPosition > 0 ? Sides.Sell : Sides.Buy;

        RegisterOrder(new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = exitSize,
            Type = OrderTypes.Market,
            Comment = $"Scale Out {percent}%"
        });

        this.AddInfoLog($"Scale out: {percent}% ({exitSize} contracts)");
    }

    protected void ExitFull()
    {
        var currentPosition = Position;

        if (currentPosition == 0)
            return;

        var side = currentPosition > 0 ? Sides.Sell : Sides.Buy;

        RegisterOrder(new Order
        {
            Security = Security,
            Portfolio = Portfolio,
            Side = side,
            Volume = Math.Abs(currentPosition),
            Type = OrderTypes.Market,
            Comment = "Full Exit"
        });

        this.AddInfoLog($"Full exit: {Math.Abs(currentPosition)} contracts");
    }
}
```

## Best Practices

### 1. Never Risk More Than You Can Afford

```csharp
// Limit risk per trade to 1-2% of capital
public const decimal MAX_RISK_PER_TRADE = 2.0m;

private decimal CalculateSafePositionSize(
    decimal entryPrice,
    decimal stopLoss)
{
    var portfolio = Portfolio;
    if (portfolio == null)
        return 0;

    var maxRisk = portfolio.CurrentValue * (MAX_RISK_PER_TRADE / 100m);
    var riskPerUnit = Math.Abs(entryPrice - stopLoss);

    return Math.Floor(maxRisk / riskPerUnit);
}
```

### 2. Use Position Size Limits

```csharp
// Always set maximum position size
RiskManager.Rules.Add(new RiskPositionSizeRule
{
    Position = MAX_POSITION_SIZE,
    Action = RiskActions.CancelOrders
});
```

### 3. Adjust for Correlation

```csharp
// Reduce position size for correlated securities
private decimal AdjustForCorrelation(
    decimal baseSize,
    Security security)
{
    var correlation = GetAverageCorrelation(security);

    // Reduce size if highly correlated with existing positions
    if (correlation > 0.7m)
        return baseSize * 0.5m;

    return baseSize;
}
```

### 4. Monitor Concentration Risk

```csharp
private bool CheckConcentrationLimit()
{
    var totalValue = GetTotalPortfolioValue();
    var positionValue = Math.Abs(Position) * Security.LastTrade.Price;
    var concentration = positionValue / totalValue * 100;

    // Warn if single position exceeds 20%
    if (concentration > 20)
    {
        this.AddWarningLog($"High concentration: {concentration:F1}%");
        return false;
    }

    return true;
}
```

### 5. Test Position Sizing Logic

```csharp
[TestMethod]
public void TestPositionSizeCalculation()
{
    var strategy = new RiskManagedStrategy
    {
        RiskPerTradePercent = 1.0m
    };

    // Simulate portfolio
    var portfolio = new Portfolio
    {
        Name = "Test",
        CurrentValue = 100_000m
    };

    strategy.Portfolio = portfolio;

    // Test calculation
    var size = strategy.CalculatePositionSize(
        entryPrice: 100m,
        stopLoss: 98m
    );

    // Expected: $1,000 risk / $2 per share = 500 shares
    Assert.AreEqual(500, size);
}
```

## See Also

- [Risk Rules](risk-rules.md)
- [PnL Calculation](pnl-calculation.md)
- [Commission Calculation](commission-calculation.md)
- [Slippage Tracking](slippage-tracking.md)
- [Strategy Framework](../05-Strategy-Framework/strategies.md)
- [Portfolio Management](../04-Trading/portfolios.md)
