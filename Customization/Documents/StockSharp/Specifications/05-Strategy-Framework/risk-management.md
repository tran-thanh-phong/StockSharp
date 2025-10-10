# Built-in Risk Management

## Overview

StockSharp provides a comprehensive risk management system that automatically monitors and controls trading risks. The `RiskManager` can prevent trades, cancel orders, or stop the strategy when risk limits are exceeded.

**Location**: `StockSharp.Algo.Risk.RiskManager`

## Strategy.RiskManager Property

### Accessing Risk Manager

```csharp
public IRiskManager RiskManager { get; set; }
```

Every strategy has a built-in `RiskManager` instance:

```csharp
// Access risk manager
var riskManager = RiskManager;

// Add risk rules
RiskManager.Rules.Add(new MaxDrawdownRule
{
    MaxDrawdown = 1000m
});
```

### Risk Rules Collection

```csharp
public INotifyList<IRiskRule> RiskRules { get; set; }
```

Provides access to all active risk rules:

```csharp
// Set risk rules from strategy
RiskRules = new[]
{
    new MaxDrawdownRule { MaxDrawdown = 1000m },
    new MaxOrdersRule { MaxOrders = 100 },
    new CommissionRule { MaxCommission = 500m }
};

// Rules are automatically applied to RiskManager
```

## Risk Actions

When a risk rule is triggered, it can perform one of three actions:

```csharp
public enum RiskActions
{
    ClosePositions,  // Close all open positions
    StopTrading,     // Stop the strategy
    CancelOrders     // Cancel all active orders
}
```

### Action Behavior

```csharp
// When risk rule triggers:
switch (rule.Action)
{
    case RiskActions.ClosePositions:
        ClosePosition();  // Closes all positions
        break;

    case RiskActions.StopTrading:
        Stop();  // Stops the strategy
        break;

    case RiskActions.CancelOrders:
        CancelActiveOrders();  // Cancels all active orders
        break;
}
```

## Built-in Risk Rules

### 1. MaxDrawdown Rule

Triggers when drawdown exceeds the specified amount:

```csharp
var maxDrawdownRule = new MaxDrawdownRule
{
    MaxDrawdown = 1000m,  // Maximum drawdown in currency
    Action = RiskActions.StopTrading  // Stop strategy on breach
};

RiskManager.Rules.Add(maxDrawdownRule);
```

### 2. MaxOrders Rule

Limits the total number of orders:

```csharp
var maxOrdersRule = new MaxOrdersRule
{
    MaxOrders = 100,  // Maximum number of orders
    Action = RiskActions.StopTrading
};

RiskManager.Rules.Add(maxOrdersRule);
```

### 3. MaxPosition Rule

Limits the maximum position size:

```csharp
var maxPositionRule = new MaxPositionRule
{
    MaxPosition = 10,  // Maximum position size
    Action = RiskActions.ClosePositions
};

RiskManager.Rules.Add(maxPositionRule);
```

### 4. Commission Rule

Triggers when total commission exceeds limit:

```csharp
var commissionRule = new CommissionRule
{
    MaxCommission = 500m,  // Maximum total commission
    Action = RiskActions.StopTrading
};

RiskManager.Rules.Add(commissionRule);
```

### 5. Slippage Rule

Triggers when total slippage exceeds limit:

```csharp
var slippageRule = new SlippageRule
{
    MaxSlippage = 100m,  // Maximum total slippage
    Action = RiskActions.StopTrading
};

RiskManager.Rules.Add(slippageRule);
```

### 6. PnL Rule

Triggers when profit/loss exceeds limits:

```csharp
var pnlRule = new PnLRule
{
    MinPnL = -1000m,  // Minimum P&L (max loss)
    MaxPnL = 5000m,   // Maximum P&L (profit target)
    Action = RiskActions.StopTrading
};

RiskManager.Rules.Add(pnlRule);
```

### 7. Order Price Rule

Validates order prices:

```csharp
var orderPriceRule = new OrderPriceRule
{
    MinPrice = 10m,   // Minimum order price
    MaxPrice = 1000m, // Maximum order price
    Action = RiskActions.CancelOrders
};

RiskManager.Rules.Add(orderPriceRule);
```

### 8. Order Volume Rule

Validates order volumes:

```csharp
var orderVolumeRule = new OrderVolumeRule
{
    MinVolume = 1m,    // Minimum order volume
    MaxVolume = 100m,  // Maximum order volume
    Action = RiskActions.CancelOrders
};

RiskManager.Rules.Add(orderVolumeRule);
```

### 9. Order Frequency Rule

Limits order frequency:

```csharp
var orderFrequencyRule = new OrderFrequencyRule
{
    MaxOrders = 10,  // Maximum orders
    Period = TimeSpan.FromMinutes(1),  // Per time period
    Action = RiskActions.CancelOrders
};

RiskManager.Rules.Add(orderFrequencyRule);
```

## Setting Up Risk Rules

### In Strategy Constructor

```csharp
public class MyStrategy : Strategy
{
    public MyStrategy()
    {
        // Initialize parameters...

        // Setup risk rules
        RiskManager.Rules.Add(new MaxDrawdownRule
        {
            MaxDrawdown = 1000m,
            Action = RiskActions.StopTrading
        });

        RiskManager.Rules.Add(new MaxOrdersRule
        {
            MaxOrders = 100,
            Action = RiskActions.StopTrading
        });
    }
}
```

### Using RiskRules Property

```csharp
public class MyStrategy : Strategy
{
    public MyStrategy()
    {
        // Set all rules at once
        RiskRules = new[]
        {
            new MaxDrawdownRule
            {
                MaxDrawdown = 1000m,
                Action = RiskActions.StopTrading
            },
            new PnLRule
            {
                MinPnL = -500m,
                MaxPnL = 2000m,
                Action = RiskActions.StopTrading
            },
            new MaxPositionRule
            {
                MaxPosition = 10,
                Action = RiskActions.ClosePositions
            }
        };
    }
}
```

### Dynamic Risk Rules

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Add rules dynamically based on conditions
    if (IsBacktesting)
    {
        RiskManager.Rules.Add(new MaxOrdersRule
        {
            MaxOrders = 1000,
            Action = RiskActions.StopTrading
        });
    }
    else
    {
        // Stricter rules for live trading
        RiskManager.Rules.Add(new MaxOrdersRule
        {
            MaxOrders = 100,
            Action = RiskActions.StopTrading
        });

        RiskManager.Rules.Add(new MaxDrawdownRule
        {
            MaxDrawdown = 500m,
            Action = RiskActions.StopTrading
        });
    }
}
```

## Risk Rule Events

### Monitoring Risk Events

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Monitor when risk rules are triggered
    Error += OnStrategyError;
}

private void OnStrategyError(Strategy strategy, Exception error)
{
    if (error is RiskException riskEx)
    {
        LogError("Risk rule triggered: {0}", riskEx.Message);
        // Handle risk breach
    }
}
```

## Complete Risk Management Example

```csharp
public class RiskAwareStrategy : Strategy
{
    private readonly StrategyParam<decimal> _maxDrawdown;
    private readonly StrategyParam<decimal> _profitTarget;
    private readonly StrategyParam<decimal> _maxDailyLoss;
    private readonly StrategyParam<int> _maxOrders;

    public RiskAwareStrategy()
    {
        // Risk parameters
        _maxDrawdown = Param(nameof(MaxDrawdown), 1000m)
            .SetGreaterThanZero()
            .SetDisplay("Max Drawdown", "Maximum allowed drawdown", "Risk");

        _profitTarget = Param(nameof(ProfitTarget), 5000m)
            .SetGreaterThanZero()
            .SetDisplay("Profit Target", "Stop strategy at this profit", "Risk");

        _maxDailyLoss = Param(nameof(MaxDailyLoss), 500m)
            .SetGreaterThanZero()
            .SetDisplay("Max Daily Loss", "Maximum loss per day", "Risk");

        _maxOrders = Param(nameof(MaxOrders), 100)
            .SetGreaterThanZero()
            .SetDisplay("Max Orders", "Maximum number of orders", "Risk");

        // Setup risk rules
        SetupRiskRules();
    }

    public decimal MaxDrawdown
    {
        get => _maxDrawdown.Value;
        set => _maxDrawdown.Value = value;
    }

    public decimal ProfitTarget
    {
        get => _profitTarget.Value;
        set => _profitTarget.Value = value;
    }

    public decimal MaxDailyLoss
    {
        get => _maxDailyLoss.Value;
        set => _maxDailyLoss.Value = value;
    }

    public int MaxOrders
    {
        get => _maxOrders.Value;
        set => _maxOrders.Value = value;
    }

    private void SetupRiskRules()
    {
        RiskRules = new IRiskRule[]
        {
            // Stop trading on maximum drawdown
            new MaxDrawdownRule
            {
                MaxDrawdown = MaxDrawdown,
                Action = RiskActions.StopTrading
            },

            // Stop trading at profit target or max loss
            new PnLRule
            {
                MinPnL = -MaxDailyLoss,  // Max loss
                MaxPnL = ProfitTarget,    // Profit target
                Action = RiskActions.StopTrading
            },

            // Limit total orders
            new MaxOrdersRule
            {
                MaxOrders = MaxOrders,
                Action = RiskActions.StopTrading
            },

            // Close positions on excessive position size
            new MaxPositionRule
            {
                MaxPosition = 20,  // Hard position limit
                Action = RiskActions.ClosePositions
            },

            // Prevent runaway commission costs
            new CommissionRule
            {
                MaxCommission = 1000m,
                Action = RiskActions.StopTrading
            },

            // Order validation
            new OrderVolumeRule
            {
                MinVolume = 1m,
                MaxVolume = 10m,
                Action = RiskActions.CancelOrders
            },

            // Prevent order spam
            new OrderFrequencyRule
            {
                MaxOrders = 10,
                Period = TimeSpan.FromMinutes(1),
                Action = RiskActions.CancelOrders
            }
        };
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        LogInfo("Risk rules configured:");
        foreach (var rule in RiskManager.Rules)
        {
            LogInfo("  - {0}: {1}", rule.Name, rule.Action);
        }

        // Monitor risk events
        Error += OnStrategyError;
    }

    private void OnStrategyError(Strategy strategy, Exception error)
    {
        if (error is RiskException)
        {
            LogError("RISK BREACH: {0}", error.Message);
            LogInfo("Current P&L: {0}", PnL);
            LogInfo("Current Position: {0}", Position);
            LogInfo("Total Orders: {0}", Orders.Count());

            // Custom risk breach handling
            HandleRiskBreach();
        }
    }

    private void HandleRiskBreach()
    {
        // Send notification
        SendNotification("Risk rule triggered");

        // Close all positions immediately
        if (Position != 0)
        {
            LogWarning("Flattening position: {0}", Position);
            ClosePosition();
        }

        // Cancel pending orders
        CancelActiveOrders();
    }

    protected override void OnStopped()
    {
        Error -= OnStrategyError;

        LogInfo("Strategy stopped. Final statistics:");
        LogInfo("  P&L: {0}", PnL);
        LogInfo("  Position: {0}", Position);
        LogInfo("  Total Orders: {0}", Orders.Count());
        LogInfo("  Commission: {0}", Commission);

        base.OnStopped();
    }
}
```

## Custom Risk Rules

### Creating Custom Rules

```csharp
public class TimeOfDayRiskRule : IRiskRule
{
    public TimeSpan MarketOpen { get; set; } = new TimeSpan(9, 30, 0);
    public TimeSpan MarketClose { get; set; } = new TimeSpan(16, 0, 0);
    public RiskActions Action { get; set; } = RiskActions.CancelOrders;

    public string Name => "Time of Day";
    public string Title => "Prevent trading outside market hours";

    public bool ProcessMessage(Message message)
    {
        if (message is OrderRegisterMessage orderMsg)
        {
            var time = orderMsg.LocalTime.TimeOfDay;

            // Check if outside trading hours
            if (time < MarketOpen || time > MarketClose)
            {
                LogWarning("Order blocked - outside market hours: {0}", time);
                return true;  // Trigger risk action
            }
        }

        return false;
    }

    public void Reset()
    {
        // Reset rule state if needed
    }
}
```

### Using Custom Rules

```csharp
public class MyStrategy : Strategy
{
    public MyStrategy()
    {
        RiskManager.Rules.Add(new TimeOfDayRiskRule
        {
            MarketOpen = new TimeSpan(9, 30, 0),
            MarketClose = new TimeSpan(16, 0, 0),
            Action = RiskActions.CancelOrders
        });
    }
}
```

## Risk Management Best Practices

### 1. Always Set Risk Limits

```csharp
// Good - has risk limits
RiskRules = new[]
{
    new MaxDrawdownRule { MaxDrawdown = 1000m, Action = RiskActions.StopTrading },
    new PnLRule { MinPnL = -500m, Action = RiskActions.StopTrading }
};

// Bad - no risk protection
// RiskManager has no rules!
```

### 2. Test Risk Rules in Backtesting

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    // Verify risk rules are active
    if (RiskManager.Rules.Count == 0)
    {
        LogWarning("WARNING: No risk rules configured!");
    }

    LogInfo("Active risk rules: {0}", RiskManager.Rules.Count);
}
```

### 3. Use Appropriate Actions

```csharp
// Position limits -> Close positions
new MaxPositionRule
{
    MaxPosition = 10,
    Action = RiskActions.ClosePositions  // Close, don't just stop
};

// P&L limits -> Stop trading
new PnLRule
{
    MinPnL = -1000m,
    Action = RiskActions.StopTrading  // Stop everything
};

// Order validation -> Cancel orders
new OrderVolumeRule
{
    MaxVolume = 100m,
    Action = RiskActions.CancelOrders  // Just cancel bad orders
};
```

### 4. Log Risk Events

```csharp
private void OnStrategyError(Strategy strategy, Exception error)
{
    if (error is RiskException)
    {
        LogError("RISK BREACH: {0}", error.Message);
        LogInfo("P&L: {0}, Position: {1}, Orders: {2}",
            PnL, Position, Orders.Count());

        // Alert operator
        SendAlert("Risk rule triggered");
    }
}
```

### 5. Different Rules for Backtesting vs Live

```csharp
if (IsBacktesting)
{
    // Looser limits for backtesting
    RiskManager.Rules.Add(new MaxOrdersRule
    {
        MaxOrders = 10000,
        Action = RiskActions.StopTrading
    });
}
else
{
    // Strict limits for live trading
    RiskManager.Rules.Add(new MaxOrdersRule
    {
        MaxOrders = 100,
        Action = RiskActions.StopTrading
    });

    RiskManager.Rules.Add(new MaxDrawdownRule
    {
        MaxDrawdown = 500m,
        Action = RiskActions.StopTrading
    });
}
```

## Risk Rule Persistence

Risk rules are saved with strategy settings:

```csharp
// Save strategy (includes risk rules)
var storage = new SettingsStorage();
strategy.Save(storage);

// Load strategy (restores risk rules)
strategy.Load(storage);
```

## Resetting Risk Manager

```csharp
// Reset all risk rule states
RiskManager.Reset();

// Called automatically on strategy Reset()
public override void Reset()
{
    base.Reset();  // Resets RiskManager internally
}
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy lifecycle and error handling
- [position-tracking.md](position-tracking.md) - Position-based risk management
- [statistics.md](statistics.md) - Performance metrics for risk assessment
