# Risk Management - Risk Rules Specification

## Overview

The Risk Management system in StockSharp provides comprehensive protection mechanisms to prevent excessive losses and enforce trading discipline. The system is built around the `IRiskManager` interface and implements various risk rules that monitor trading activity and trigger protective actions when limits are breached.

## Core Architecture

### IRiskManager Interface

```csharp
public interface IRiskManager : ILogSource, IPersistable
{
    INotifyList<IRiskRule> Rules { get; }
    void Reset();
    IEnumerable<IRiskRule> ProcessRules(Message message);
}
```

**Key Responsibilities:**
- Maintain a collection of risk rules
- Process incoming messages through all active rules
- Return activated rules that require action
- Support persistence for configuration storage

### RiskManager Implementation

```csharp
public class RiskManager : BaseLogReceiver, IRiskManager
{
    private readonly CachedSynchronizedList<IRiskRule> _rules = [];

    public INotifyList<IRiskRule> Rules => _rules;

    public virtual void Reset()
    {
        _rules.Cache.ForEach(r => r.Reset());
    }

    public IEnumerable<IRiskRule> ProcessRules(Message message)
    {
        if (message.Type == MessageTypes.Reset)
        {
            Reset();
            return [];
        }

        return [.. _rules.Cache.Where(r => r.ProcessMessage(message))];
    }
}
```

**Features:**
- Thread-safe rule collection management
- Automatic parent assignment for logging
- Batch rule processing
- Configuration persistence

## IRiskRule Interface

### Interface Definition

```csharp
public interface IRiskRule : ILogSource, IPersistable
{
    string Title { get; }
    RiskActions Action { get; set; }
    void Reset();
    bool ProcessMessage(Message message);
}
```

### RiskRule Base Class

```csharp
public abstract class RiskRule : BaseLogReceiver, IRiskRule, INotifyPropertyChanged
{
    protected abstract string GetTitle();

    public string Title { get; private set; }
    public RiskActions Action { get; set; }

    public virtual void Reset() { }
    public abstract bool ProcessMessage(Message message);
}
```

**Base Implementation Features:**
- Dynamic title generation
- Property change notification
- Persistence support
- Logging integration

## RiskActions Enum

```csharp
public enum RiskActions
{
    ClosePositions,    // Close all open positions
    StopTrading,       // Stop strategy execution
    CancelOrders       // Cancel all active orders
}
```

### Action Behavior

#### ClosePositions
- Immediately closes all open positions
- Creates market orders in opposite direction
- Executes before strategy stops
- Use for: Loss limit breach, drawdown protection

#### StopTrading
- Sets strategy state to Stopping
- No new orders accepted
- Existing orders remain active
- Use for: Maximum loss reached, error conditions

#### CancelOrders
- Cancels all active orders
- Does not close positions
- Strategy continues running
- Use for: Order frequency limits, temporary suspension

## Built-in Risk Rules

### 1. RiskPnLRule - Profit/Loss Monitoring

**Purpose:** Monitors portfolio value changes and triggers actions when PnL thresholds are breached.

```csharp
public class RiskPnLRule : RiskRule
{
    public Unit PnL { get; set; }  // Threshold value

    public override bool ProcessMessage(Message message)
    {
        if (message.Type != MessageTypes.PositionChange)
            return false;

        var pfMsg = (PositionChangeMessage)message;
        if (!pfMsg.IsMoney())
            return false;

        var currValue = pfMsg.TryGetDecimal(PositionChangeTypes.CurrentValue);
        if (currValue == null)
            return false;

        // Compare against threshold
        if (PnL.Type == UnitTypes.Limit)
        {
            if (PnL.Value > 0)
                return PnL.Value <= currValue.Value;
            else
                return PnL.Value >= currValue.Value;
        }

        // Compare against initial value + PnL change
        return (_initValue + PnL) >= currValue.Value;
    }
}
```

**Configuration:**
- `PnL`: Unit value (absolute or relative)
- `Action`: RiskActions to execute
- Tracks initial portfolio value automatically
- Supports both profit and loss limits

**Use Cases:**
```csharp
// Stop trading if loss exceeds $10,000
new RiskPnLRule
{
    PnL = new Unit(-10000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
}

// Close positions if drawdown exceeds 5%
new RiskPnLRule
{
    PnL = new Unit(-5, UnitTypes.Percent),
    Action = RiskActions.ClosePositions
}

// Take profit at 15% gain
new RiskPnLRule
{
    PnL = new Unit(15, UnitTypes.Percent),
    Action = RiskActions.ClosePositions
}
```

### 2. RiskPositionSizeRule - Position Size Limits

**Purpose:** Prevents excessive position sizes that could lead to unmanageable risk.

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

**Configuration:**
- `Position`: Maximum position size (positive or negative)
- Monitors long and short positions separately
- Triggers on position changes

**Use Cases:**
```csharp
// Limit long positions to 100 contracts
new RiskPositionSizeRule
{
    Position = 100,
    Action = RiskActions.CancelOrders
}

// Limit short positions to -50 contracts
new RiskPositionSizeRule
{
    Position = -50,
    Action = RiskActions.ClosePositions
}
```

### 3. RiskOrderVolumeRule - Order Size Limits

**Purpose:** Prevents accidentally large orders from being submitted.

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
                return orderReplace.Volume > 0 && orderReplace.Volume >= Volume;
            }

            default:
                return false;
        }
    }
}
```

**Configuration:**
- `Volume`: Maximum allowed order size
- Checks both new orders and order modifications
- Blocks order before submission

**Use Cases:**
```csharp
// Prevent orders larger than 50 contracts
new RiskOrderVolumeRule
{
    Volume = 50,
    Action = RiskActions.CancelOrders
}
```

### 4. RiskOrderFreqRule - Order Frequency Control

**Purpose:** Prevents excessive order submission that could lead to broker penalties or system issues.

**Configuration:**
- `Count`: Maximum orders allowed
- `Interval`: Time period for counting
- Tracks order submission rate

### 5. RiskTradeFreqRule - Trade Frequency Monitoring

**Purpose:** Monitors trade execution frequency to detect churning or system issues.

**Configuration:**
- `Count`: Maximum trades allowed
- `Interval`: Time period for counting
- Tracks actual trade executions

### 6. RiskCommissionRule - Commission Limits

**Purpose:** Stops trading when commission costs exceed acceptable levels.

**Configuration:**
- `Commission`: Maximum commission amount
- Integrates with CommissionManager
- Monitors total commission costs

### 7. RiskSlippageRule - Slippage Monitoring

**Purpose:** Detects poor execution quality through slippage tracking.

**Configuration:**
- `Slippage`: Maximum acceptable slippage
- Integrates with SlippageManager
- Monitors execution quality

### 8. RiskOrderPriceRule - Price Limit Validation

**Purpose:** Prevents orders with unreasonable prices from being submitted.

**Configuration:**
- Minimum/maximum price limits
- Reference price validation
- Protection against fat-finger errors

## Integration with Strategy

### Strategy Configuration

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // Configure risk rules
        RiskManager.Rules.Clear();

        // Add daily loss limit
        RiskManager.Rules.Add(new RiskPnLRule
        {
            PnL = new Unit(-5000, UnitTypes.Absolute),
            Action = RiskActions.StopTrading
        });

        // Add position size limit
        RiskManager.Rules.Add(new RiskPositionSizeRule
        {
            Position = 100,
            Action = RiskActions.CancelOrders
        });

        // Add order size validation
        RiskManager.Rules.Add(new RiskOrderVolumeRule
        {
            Volume = 50,
            Action = RiskActions.CancelOrders
        });

        base.OnStarted(time);
    }
}
```

### Automatic Risk Processing

The Strategy class automatically processes risk rules:

```csharp
private RiskActions? ProcessRisk(Func<Message> getMessage)
{
    if (RiskManager.Rules.Count == 0)
        return null;

    foreach (var rule in RiskManager.ProcessRules(getMessage()))
    {
        LogWarning("Activating risk rule: {0}, Action: {1}",
            rule.Title, rule.Action);

        switch (rule.Action)
        {
            case RiskActions.ClosePositions:
                ClosePosition();
                return rule.Action;

            case RiskActions.StopTrading:
                Stop();
                return rule.Action;

            case RiskActions.CancelOrders:
                CancelActiveOrders();
                return rule.Action;
        }
    }

    return null;
}
```

### Risk Check Points

Risk rules are evaluated at key points:

1. **Order Registration**: Before submitting new orders
2. **Order Modification**: Before changing order parameters
3. **Trade Execution**: After each trade is matched
4. **Position Changes**: When positions are updated
5. **Error Events**: When exceptions occur

## Custom Risk Rules

### Creating Custom Rules

```csharp
public class CustomDrawdownRule : RiskRule
{
    private decimal _peakValue;

    public decimal MaxDrawdownPercent { get; set; }

    protected override string GetTitle()
    {
        return $"Drawdown {MaxDrawdownPercent}%";
    }

    public override void Reset()
    {
        _peakValue = 0;
        base.Reset();
    }

    public override bool ProcessMessage(Message message)
    {
        if (message.Type != MessageTypes.PositionChange)
            return false;

        var pfMsg = (PositionChangeMessage)message;
        if (!pfMsg.IsMoney())
            return false;

        var currValue = pfMsg.TryGetDecimal(PositionChangeTypes.CurrentValue);
        if (currValue == null)
            return false;

        // Track peak value
        if (currValue > _peakValue)
            _peakValue = currValue.Value;

        // Calculate drawdown
        if (_peakValue == 0)
            return false;

        var drawdown = (_peakValue - currValue.Value) / _peakValue * 100;

        return drawdown >= MaxDrawdownPercent;
    }

    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);
        storage.SetValue(nameof(MaxDrawdownPercent), MaxDrawdownPercent);
    }

    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);
        MaxDrawdownPercent = storage.GetValue<decimal>(nameof(MaxDrawdownPercent));
    }
}
```

### Advanced Custom Rule Example

```csharp
public class TimeBasedRiskRule : RiskRule
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal MaxPositionDuringPeriod { get; set; }

    protected override string GetTitle()
    {
        return $"Time Risk {StartTime}-{EndTime}";
    }

    public override bool ProcessMessage(Message message)
    {
        var currentTime = message.GetServerTime().TimeOfDay;

        // Only active during specified period
        if (currentTime < StartTime || currentTime > EndTime)
            return false;

        if (message.Type != MessageTypes.PositionChange)
            return false;

        var posMsg = (PositionChangeMessage)message;
        var currValue = posMsg.TryGetDecimal(PositionChangeTypes.CurrentValue);

        if (currValue == null)
            return false;

        return Math.Abs(currValue.Value) >= MaxPositionDuringPeriod;
    }
}
```

## Best Practices

### 1. Layer Multiple Rules

Use multiple complementary rules for robust protection:

```csharp
// Daily loss limit
RiskManager.Rules.Add(new RiskPnLRule
{
    PnL = new Unit(-10000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});

// Drawdown limit
RiskManager.Rules.Add(new CustomDrawdownRule
{
    MaxDrawdownPercent = 10,
    Action = RiskActions.ClosePositions
});

// Position size limit
RiskManager.Rules.Add(new RiskPositionSizeRule
{
    Position = 200,
    Action = RiskActions.CancelOrders
});
```

### 2. Choose Appropriate Actions

- **CancelOrders**: For temporary conditions, frequency limits
- **ClosePositions**: For loss limits, drawdown breaches
- **StopTrading**: For critical errors, daily limits reached

### 3. Test Risk Rules

Always test risk rules in simulation before live trading:

```csharp
[TestMethod]
public void TestPnLRiskRule()
{
    var rule = new RiskPnLRule
    {
        PnL = new Unit(-5000, UnitTypes.Absolute),
        Action = RiskActions.StopTrading
    };

    var msg = new PositionChangeMessage
    {
        SecurityId = SecurityId.Money,
        PortfolioName = "Test"
    }
    .TryAdd(PositionChangeTypes.CurrentValue, -6000);

    Assert.IsTrue(rule.ProcessMessage(msg));
}
```

### 4. Monitor Rule Activations

Log all rule activations for analysis:

```csharp
RiskManager.Rules.Add(new RiskPnLRule
{
    PnL = new Unit(-5000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});

// Rule activation is automatically logged by Strategy:
// "Activating risk rule: PnL -5000, Action: StopTrading"
```

### 5. Reset Rules Appropriately

```csharp
// Reset daily at market open
if (CurrentTime.TimeOfDay == MarketOpenTime)
{
    RiskManager.Reset();
}

// Or reset when strategy restarts
protected override void OnStarted(DateTimeOffset time)
{
    if (!KeepStatistics)
        RiskManager.Reset();

    base.OnStarted(time);
}
```

## Common Scenarios

### Scenario 1: Day Trading Protection

```csharp
// Daily loss limit
RiskManager.Rules.Add(new RiskPnLRule
{
    PnL = new Unit(-2000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});

// Maximum trades per hour
RiskManager.Rules.Add(new RiskTradeFreqRule
{
    Count = 20,
    Interval = TimeSpan.FromHours(1),
    Action = RiskActions.CancelOrders
});

// Order size limit
RiskManager.Rules.Add(new RiskOrderVolumeRule
{
    Volume = 10,
    Action = RiskActions.CancelOrders
});
```

### Scenario 2: Swing Trading Protection

```csharp
// Maximum drawdown
RiskManager.Rules.Add(new CustomDrawdownRule
{
    MaxDrawdownPercent = 15,
    Action = RiskActions.ClosePositions
});

// Position concentration limit
RiskManager.Rules.Add(new RiskPositionSizeRule
{
    Position = 500,
    Action = RiskActions.CancelOrders
});

// Profit target
RiskManager.Rules.Add(new RiskPnLRule
{
    PnL = new Unit(10000, UnitTypes.Absolute),
    Action = RiskActions.ClosePositions
});
```

### Scenario 3: High-Frequency Trading Protection

```csharp
// Order frequency limit
RiskManager.Rules.Add(new RiskOrderFreqRule
{
    Count = 100,
    Interval = TimeSpan.FromSeconds(1),
    Action = RiskActions.CancelOrders
});

// Commission cost limit
RiskManager.Rules.Add(new RiskCommissionRule
{
    Commission = new Unit(5000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});

// Slippage monitoring
RiskManager.Rules.Add(new RiskSlippageRule
{
    Slippage = new Unit(1000, UnitTypes.Absolute),
    Action = RiskActions.StopTrading
});
```

## Persistence

### Save/Load Configuration

```csharp
// Save
var storage = new SettingsStorage();
RiskManager.Save(storage);
File.WriteAllText("risk_config.json", storage.Serialize());

// Load
var json = File.ReadAllText("risk_config.json");
var storage = json.Deserialize<SettingsStorage>();
RiskManager.Load(storage);
```

### Rule Collections

```csharp
// Save rule collection
storage.SetValue("Rules",
    RiskManager.Rules.Select(r => r.SaveEntire(false)).ToArray());

// Load rule collection
RiskManager.Rules.Clear();
RiskManager.Rules.AddRange(
    storage.GetValue<SettingsStorage[]>("Rules")
        .Select(s => s.LoadEntire<IRiskRule>()));
```

## See Also

- [PnL Calculation](pnl-calculation.md)
- [Commission Calculation](commission-calculation.md)
- [Slippage Tracking](slippage-tracking.md)
- [Position Sizing](position-sizing.md)
- [Strategy Framework](../05-Strategy-Framework/strategies.md)
