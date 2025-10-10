# Commission Calculation Specification

## Overview

The Commission Calculation system in StockSharp provides flexible and accurate tracking of trading costs. It supports multiple commission structures including per-order, per-trade, volume-based, and value-based commissions. The system is essential for realistic backtesting and live trading performance analysis.

## Core Architecture

### ICommissionManager Interface

```csharp
public interface ICommissionManager : IPersistable
{
    ISynchronizedCollection<ICommissionRule> Rules { get; }
    decimal Commission { get; }

    void Reset();
    decimal? Process(Message message);
}
```

**Key Responsibilities:**
- Maintain collection of commission rules
- Calculate total commission from all active rules
- Process execution messages
- Support multiple commission structures

### CommissionManager Implementation

```csharp
public class CommissionManager : ICommissionManager
{
    private readonly CachedSynchronizedSet<ICommissionRule> _rules = new(true);

    public ISynchronizedCollection<ICommissionRule> Rules => _rules;
    public decimal Commission { get; private set; }

    public decimal? Process(Message message)
    {
        switch (message.Type)
        {
            case MessageTypes.Reset:
                Reset();
                return null;

            case MessageTypes.Execution:
                if (_rules.Count == 0)
                    return null;

                var execMsg = (ExecutionMessage)message;
                decimal? commission = null;

                foreach (var rule in _rules.Cache)
                {
                    var ruleCom = rule.Process(execMsg);

                    if (ruleCom != null)
                        commission = (commission ?? 0) + ruleCom.Value;
                }

                if (commission != null)
                    Commission += commission.Value;

                return commission;

            default:
                return null;
        }
    }
}
```

**Features:**
- Multiple rule aggregation
- Thread-safe operations
- Cumulative commission tracking
- Message-based processing

## ICommissionRule Interface

### Interface Definition

```csharp
public interface ICommissionRule : IPersistable
{
    string Title { get; }
    Unit Value { get; }

    void Reset();
    decimal? Process(ExecutionMessage message);
}
```

### CommissionRule Base Class

```csharp
public abstract class CommissionRule : NotifiableObject, ICommissionRule
{
    public Unit Value { get; set; } = new();
    public string Title { get; private set; }

    public abstract decimal? Process(ExecutionMessage message);

    protected decimal? GetValue(decimal? price, decimal? volume)
    {
        // Absolute commission
        if (Value.Type != UnitTypes.Percent)
            return (decimal)Value;

        // Percentage of turnover
        if (price == null)
            return null;

        var vol = volume ?? 1m;
        var turnover = price.Value * vol;
        return (turnover * Value.Value) / 100m;
    }
}
```

**Base Features:**
- Unit-based values (absolute or percentage)
- Automatic title generation
- Persistence support
- Property change notification

## Unit Types

```csharp
public enum UnitTypes
{
    Absolute,    // Fixed amount (e.g., $5.00 per trade)
    Percent,     // Percentage of value (e.g., 0.1% of turnover)
    Point,       // Price points (e.g., 0.5 points)
    Step         // Price steps
}
```

**Examples:**
```csharp
// $5 per trade
new Unit(5, UnitTypes.Absolute)

// 0.1% of trade value
new Unit(0.1, UnitTypes.Percent)

// 0.5 price points
new Unit(0.5, UnitTypes.Point)
```

## Built-in Commission Rules

### 1. CommissionTradeRule - Per-Trade Commission

**Purpose:** Charges commission on each executed trade.

```csharp
public class CommissionTradeRule : CommissionRule
{
    public override decimal? Process(ExecutionMessage message)
    {
        if (message.HasTradeInfo())
            return GetValue(message.TradePrice, message.TradeVolume);

        return null;
    }
}
```

**Use Cases:**
```csharp
// Fixed $2.50 per trade
new CommissionTradeRule
{
    Value = new Unit(2.50m, UnitTypes.Absolute)
}

// 0.05% of trade value
new CommissionTradeRule
{
    Value = new Unit(0.05m, UnitTypes.Percent)
}

// Example calculations:
// Trade: 10 shares @ $100
// Absolute: $2.50
// Percent: $100 × 10 × 0.05% = $5.00
```

### 2. CommissionOrderRule - Per-Order Commission

**Purpose:** Charges commission when orders are registered or executed.

```csharp
public class CommissionOrderRule : CommissionRule
{
    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasOrderInfo())
            return null;

        var price = message.HasTradeInfo()
            ? message.TradePrice
            : message.OrderPrice;

        var volume = message.HasTradeInfo()
            ? message.TradeVolume
            : message.OrderVolume;

        return GetValue(price, volume);
    }
}
```

**Use Cases:**
```csharp
// $1 per order regardless of size
new CommissionOrderRule
{
    Value = new Unit(1m, UnitTypes.Absolute)
}

// 0.1% of order value
new CommissionOrderRule
{
    Value = new Unit(0.1m, UnitTypes.Percent)
}

// Charged on:
// - Order registration (uses order price/volume)
// - Trade execution (uses trade price/volume)
```

### 3. CommissionTradeVolumeRule - Volume-Based Commission

**Purpose:** Charges commission based on trade volume only.

```csharp
public class CommissionTradeVolumeRule : CommissionRule
{
    public override decimal? Process(ExecutionMessage message)
    {
        if (message.HasTradeInfo())
            return (decimal)(message.TradeVolume * Value);

        return null;
    }
}
```

**Use Cases:**
```csharp
// $0.01 per share
new CommissionTradeVolumeRule
{
    Value = new Unit(0.01m, UnitTypes.Absolute)
}

// Example:
// Trade: 100 shares @ $50
// Commission: 100 × $0.01 = $1.00
```

### 4. CommissionOrderVolumeRule - Order Volume-Based

**Purpose:** Charges commission based on order volume.

```csharp
public class CommissionOrderVolumeRule : CommissionRule
{
    public override decimal? Process(ExecutionMessage message)
    {
        if (message.HasOrderInfo())
        {
            var volume = message.HasTradeInfo()
                ? message.TradeVolume
                : message.OrderVolume;

            return volume * (decimal)Value;
        }

        return null;
    }
}
```

**Use Cases:**
```csharp
// $0.005 per contract
new CommissionOrderVolumeRule
{
    Value = new Unit(0.005m, UnitTypes.Absolute)
}
```

### 5. CommissionTurnOverRule - Turnover-Based Tiered Commission

**Purpose:** Charges commission once cumulative turnover reaches a threshold.

```csharp
public class CommissionTurnOverRule : CommissionRule
{
    private decimal _currentTurnOver;

    public decimal TurnOver { get; set; }

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        _currentTurnOver += message.GetTradePrice() * message.SafeGetVolume();

        if (_currentTurnOver < TurnOver)
            return null;

        return (decimal)Value;
    }

    public override void Reset()
    {
        _currentTurnOver = 0;
        base.Reset();
    }
}
```

**Use Cases:**
```csharp
// $100 fee after $1,000,000 turnover
new CommissionTurnOverRule
{
    TurnOver = 1_000_000,
    Value = new Unit(100, UnitTypes.Absolute)
}

// Example:
// Trades accumulate: $200k, $300k, $600k (total $1.1M)
// Fee charged once threshold crossed: $100
```

### 6. CommissionTradePriceRule - Price-Based Commission

**Purpose:** Charges commission based on trade price levels.

```csharp
public class CommissionTradePriceRule : CommissionRule
{
    public decimal Price { get; set; }

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        if (message.TradePrice >= Price)
            return GetValue(message.TradePrice, message.TradeVolume);

        return null;
    }
}
```

**Use Cases:**
```csharp
// Extra $5 commission for trades above $100
new CommissionTradePriceRule
{
    Price = 100,
    Value = new Unit(5, UnitTypes.Absolute)
}
```

### 7. CommissionTradeCountRule - Trade Count-Based

**Purpose:** Charges commission after a certain number of trades.

```csharp
public class CommissionTradeCountRule : CommissionRule
{
    private int _currentCount;

    public int Count { get; set; }

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        _currentCount++;

        if (_currentCount < Count)
            return null;

        return (decimal)Value;
    }

    public override void Reset()
    {
        _currentCount = 0;
        base.Reset();
    }
}
```

**Use Cases:**
```csharp
// $50 fee after every 100 trades
new CommissionTradeCountRule
{
    Count = 100,
    Value = new Unit(50, UnitTypes.Absolute)
}
```

### 8. CommissionOrderCountRule - Order Count-Based

**Purpose:** Charges commission based on number of orders.

**Use Cases:**
```csharp
// $25 fee after every 50 orders
new CommissionOrderCountRule
{
    Count = 50,
    Value = new Unit(25, UnitTypes.Absolute)
}
```

### 9. CommissionSecurityIdRule - Security-Specific Commission

**Purpose:** Applies commission only to specific securities.

```csharp
public class CommissionSecurityIdRule : CommissionRule
{
    public SecurityId SecurityId { get; set; }

    public override decimal? Process(ExecutionMessage message)
    {
        if (message.SecurityId != SecurityId)
            return null;

        if (message.HasTradeInfo())
            return GetValue(message.TradePrice, message.TradeVolume);

        return null;
    }
}
```

**Use Cases:**
```csharp
// Higher commission for specific stock
new CommissionSecurityIdRule
{
    SecurityId = new SecurityId
    {
        SecurityCode = "AAPL",
        BoardCode = "NASDAQ"
    },
    Value = new Unit(0.1m, UnitTypes.Percent)
}
```

### 10. CommissionSecurityTypeRule - Asset Class-Based

**Purpose:** Different commission rates for different security types.

**Use Cases:**
```csharp
// Options have higher commission
new CommissionSecurityTypeRule
{
    SecurityType = SecurityTypes.Option,
    Value = new Unit(5, UnitTypes.Absolute)
}

// Futures have lower commission
new CommissionSecurityTypeRule
{
    SecurityType = SecurityTypes.Future,
    Value = new Unit(2.50m, UnitTypes.Absolute)
}
```

### 11. CommissionBoardCodeRule - Exchange-Based

**Purpose:** Different commissions for different exchanges.

**Use Cases:**
```csharp
// NYSE commission
new CommissionBoardCodeRule
{
    BoardCode = "NYSE",
    Value = new Unit(0.05m, UnitTypes.Percent)
}

// NASDAQ commission
new CommissionBoardCodeRule
{
    BoardCode = "NASDAQ",
    Value = new Unit(0.03m, UnitTypes.Percent)
}
```

## Multiple Rule Scenarios

### Scenario 1: Broker + Exchange + Regulatory Fees

```csharp
var commissionManager = new CommissionManager();

// Broker commission: $0.01 per share
commissionManager.Rules.Add(new CommissionTradeVolumeRule
{
    Value = new Unit(0.01m, UnitTypes.Absolute)
});

// Exchange fee: 0.003% of value
commissionManager.Rules.Add(new CommissionTradeRule
{
    Value = new Unit(0.003m, UnitTypes.Percent)
});

// SEC fee (stocks only): 0.0000051% of value
commissionManager.Rules.Add(new CommissionSecurityTypeRule
{
    SecurityType = SecurityTypes.Stock,
    Value = new Unit(0.0000051m, UnitTypes.Percent)
});

// Trade: 100 shares @ $150
// Broker: 100 × $0.01 = $1.00
// Exchange: $15,000 × 0.003% = $0.45
// SEC: $15,000 × 0.0000051% = $0.0008
// Total: $1.45
```

### Scenario 2: Tiered Volume Pricing

```csharp
public class TieredCommissionRule : CommissionRule
{
    private decimal _monthlyVolume;

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        var volume = message.TradeVolume.Value;
        _monthlyVolume += volume;

        // Tiered pricing
        if (_monthlyVolume < 10_000)
            return volume * 0.01m;  // $0.01 per share
        else if (_monthlyVolume < 100_000)
            return volume * 0.005m; // $0.005 per share
        else
            return volume * 0.002m; // $0.002 per share
    }

    public override void Reset()
    {
        _monthlyVolume = 0;
        base.Reset();
    }
}

// Usage
commissionManager.Rules.Add(new TieredCommissionRule());

// Reset monthly
if (CurrentTime.Day == 1 && CurrentTime.Hour == 0)
    commissionManager.Reset();
```

### Scenario 3: Conditional Commission

```csharp
public class TimeBasedCommissionRule : CommissionRule
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Unit DiscountValue { get; set; }

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        var time = message.ServerTime.TimeOfDay;
        var value = (time >= StartTime && time <= EndTime)
            ? DiscountValue
            : Value;

        return GetValueWithUnit(value,
            message.TradePrice, message.TradeVolume);
    }
}

// Usage: Lower commission during off-peak hours
commissionManager.Rules.Add(new TimeBasedCommissionRule
{
    StartTime = TimeSpan.FromHours(9.5),  // 9:30 AM
    EndTime = TimeSpan.FromHours(16),     // 4:00 PM
    Value = new Unit(0.01m, UnitTypes.Absolute),
    DiscountValue = new Unit(0.005m, UnitTypes.Absolute)
});
```

## Integration with Strategy

### Basic Setup

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // Commission is tracked automatically by Strategy
        // Configure if needed

        base.OnStarted(time);
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        // Commission is in trade.Commission property
        if (trade.Commission != null)
        {
            this.AddInfoLog($"Trade commission: {trade.Commission}");
        }

        // Total strategy commission
        var totalCommission = Commission;

        base.OnNewMyTrade(trade);
    }
}
```

### Custom Commission Configuration

```csharp
public class CustomCommissionStrategy : Strategy
{
    private ICommissionManager _customCommissionManager;

    protected override void OnStarted(DateTimeOffset time)
    {
        // Create custom commission manager
        _customCommissionManager = new CommissionManager();

        // Add rules
        _customCommissionManager.Rules.Add(new CommissionTradeRule
        {
            Value = new Unit(0.05m, UnitTypes.Percent)
        });

        _customCommissionManager.Rules.Add(new CommissionTurnOverRule
        {
            TurnOver = 1_000_000,
            Value = new Unit(100, UnitTypes.Absolute)
        });

        base.OnStarted(time);
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        // Calculate commission using custom manager
        var execMsg = trade.ToMessage();
        var commission = _customCommissionManager.Process(execMsg);

        if (commission != null)
        {
            this.AddInfoLog($"Custom commission: {commission}");
        }

        base.OnNewMyTrade(trade);
    }
}
```

## Backtesting with Commission

### Accurate Backtesting

```csharp
public class BacktestStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        if (IsBacktesting)
        {
            // Configure realistic commission for backtesting
            var commissionAdapter = Connector.Adapter
                .InnerAdapters
                .OfType<CommissionMessageAdapter>()
                .FirstOrDefault();

            if (commissionAdapter != null)
            {
                commissionAdapter.CommissionManager.Rules.Clear();

                // Add realistic commission structure
                commissionAdapter.CommissionManager.Rules.Add(
                    new CommissionTradeVolumeRule
                    {
                        Value = new Unit(0.01m, UnitTypes.Absolute)
                    });

                commissionAdapter.CommissionManager.Rules.Add(
                    new CommissionTradeRule
                    {
                        Value = new Unit(0.003m, UnitTypes.Percent)
                    });
            }
        }

        base.OnStarted(time);
    }
}
```

### Commission Impact Analysis

```csharp
public class CommissionAnalyzer
{
    public void AnalyzeCommissionImpact(Strategy strategy)
    {
        var totalPnL = strategy.PnL;
        var totalCommission = strategy.Commission ?? 0;
        var netPnL = totalPnL - totalCommission;

        var commissionPercentage = totalPnL == 0
            ? 0
            : (totalCommission / Math.Abs(totalPnL)) * 100;

        Console.WriteLine($"Total PnL: {totalPnL:C}");
        Console.WriteLine($"Total Commission: {totalCommission:C}");
        Console.WriteLine($"Net PnL: {netPnL:C}");
        Console.WriteLine($"Commission as % of PnL: {commissionPercentage:F2}%");

        if (commissionPercentage > 30)
        {
            Console.WriteLine("WARNING: High commission impact on profitability!");
        }
    }
}
```

## Advanced Custom Rules

### Example: Market Maker Rebates

```csharp
public class MarketMakerCommissionRule : CommissionRule
{
    public decimal MakerRebate { get; set; }    // Negative = rebate
    public decimal TakerFee { get; set; }       // Positive = fee

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        var turnover = message.GetTradePrice() * message.SafeGetVolume();

        // Check if maker or taker based on order type
        var isMaker = message.IsMarketMaker;

        if (isMaker)
            return turnover * (MakerRebate / 100m);  // Negative = credit
        else
            return turnover * (TakerFee / 100m);      // Positive = cost
    }
}

// Usage
commissionManager.Rules.Add(new MarketMakerCommissionRule
{
    MakerRebate = -0.02m,  // 0.02% rebate
    TakerFee = 0.04m       // 0.04% fee
});

// Example:
// Maker trade: $10,000 × (-0.02%) = -$2 (rebate)
// Taker trade: $10,000 × 0.04% = $4 (fee)
```

### Example: Progressive Commission Discount

```csharp
public class LoyaltyCommissionRule : CommissionRule
{
    private int _tradeCount;
    private DateTime _periodStart;

    public int TradesForDiscount { get; set; } = 100;
    public decimal DiscountPercent { get; set; } = 10;

    public override decimal? Process(ExecutionMessage message)
    {
        if (!message.HasTradeInfo())
            return null;

        // Reset monthly
        if (message.ServerTime.Month != _periodStart.Month)
        {
            _tradeCount = 0;
            _periodStart = message.ServerTime.DateTime;
        }

        _tradeCount++;

        var baseCommission = GetValue(
            message.TradePrice, message.TradeVolume);

        if (baseCommission == null)
            return null;

        // Apply discount after threshold
        if (_tradeCount > TradesForDiscount)
        {
            var discount = baseCommission.Value
                * (DiscountPercent / 100m);
            return baseCommission.Value - discount;
        }

        return baseCommission;
    }
}
```

## Performance Considerations

### 1. Rule Evaluation Order

```csharp
// Efficient: Check count first, calculate last
commissionManager.Rules.Add(cheapCheckRule);
commissionManager.Rules.Add(expensiveCalculationRule);

// Less efficient: Expensive calculations for every trade
commissionManager.Rules.Add(expensiveCalculationRule);
commissionManager.Rules.Add(cheapCheckRule);
```

### 2. Caching

```csharp
public class CachedCommissionRule : CommissionRule
{
    private readonly Dictionary<decimal, decimal> _cache = [];

    public override decimal? Process(ExecutionMessage message)
    {
        var key = GetCacheKey(message);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var commission = CalculateCommission(message);
        _cache[key] = commission.Value;

        return commission;
    }
}
```

### 3. Batch Reset

```csharp
// Reset all commission tracking at period end
if (IsEndOfTradingDay())
{
    commissionManager.Reset();
}
```

## Testing Commission Rules

```csharp
[TestMethod]
public void TestCommissionCalculation()
{
    var rule = new CommissionTradeRule
    {
        Value = new Unit(0.1m, UnitTypes.Percent)
    };

    var message = new ExecutionMessage
    {
        DataType = DataType.Transactions,
        TradePrice = 100,
        TradeVolume = 10,
        HasTradeInfo = true
    };

    var commission = rule.Process(message);

    Assert.IsNotNull(commission);
    // $100 × 10 × 0.1% = $10
    Assert.AreEqual(10m, commission.Value);
}

[TestMethod]
public void TestMultipleRules()
{
    var manager = new CommissionManager();

    manager.Rules.Add(new CommissionTradeVolumeRule
    {
        Value = new Unit(0.01m, UnitTypes.Absolute)
    });

    manager.Rules.Add(new CommissionTradeRule
    {
        Value = new Unit(0.05m, UnitTypes.Percent)
    });

    var message = new ExecutionMessage
    {
        DataType = DataType.Transactions,
        TradePrice = 100,
        TradeVolume = 10,
        HasTradeInfo = true
    };

    var commission = manager.Process(message);

    // Volume: 10 × $0.01 = $0.10
    // Percent: $1000 × 0.05% = $0.50
    // Total: $0.60
    Assert.AreEqual(0.60m, commission);
    Assert.AreEqual(0.60m, manager.Commission);
}
```

## Best Practices

1. **Use realistic commissions in backtesting**
2. **Monitor commission as percentage of PnL**
3. **Reset periodic commission rules appropriately**
4. **Combine multiple rules for accurate modeling**
5. **Test commission calculations thoroughly**
6. **Document commission structure for strategies**
7. **Consider bid-ask spread as implicit commission**

## See Also

- [Risk Rules](risk-rules.md)
- [PnL Calculation](pnl-calculation.md)
- [Slippage Tracking](slippage-tracking.md)
- [Position Sizing](position-sizing.md)
- [Strategy Framework](../05-Strategy-Framework/strategies.md)
