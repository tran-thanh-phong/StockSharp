# Order Types Specification

## Overview

This specification provides comprehensive documentation of all order types supported by StockSharp, including market orders, limit orders, conditional orders, and their various configurations using TimeInForce and OrderCondition parameters.

## Table of Contents

1. [Order Type Enumeration](#order-type-enumeration)
2. [Market Orders](#market-orders)
3. [Limit Orders](#limit-orders)
4. [Conditional Orders](#conditional-orders)
5. [TimeInForce Options](#timeinforce-options)
6. [Order Properties](#order-properties)
7. [Order Conditions](#order-conditions)
8. [Practical Examples](#practical-examples)

---

## Order Type Enumeration

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderTypes.cs`

```csharp
public enum OrderTypes
{
    /// <summary>
    /// Limit order - executed at specified price or better
    /// </summary>
    Limit,

    /// <summary>
    /// Market order - executed at best available price
    /// </summary>
    Market,

    /// <summary>
    /// Conditional order - stop-loss, take-profit, or algo order
    /// </summary>
    Conditional,
}
```

### Auto-Detection

If `Type` is not specified, it will be auto-detected based on `Price`:

```csharp
// In RegisterOrder method
order.Type ??= order.Price > 0 ? OrderTypes.Limit : OrderTypes.Market;
```

---

## Market Orders

### Description

Market orders are executed immediately at the best available price in the market. They have the highest priority for execution but no price guarantee.

### Characteristics

- **Execution**: Immediate (subject to liquidity)
- **Price**: Best available bid (sell) or ask (buy)
- **Guarantee**: Execution priority, not price
- **Risk**: Slippage in volatile markets

### Properties

```csharp
var marketOrder = new Order
{
    Security = security,
    Portfolio = portfolio,
    Side = Sides.Buy,           // Buy or Sell
    Volume = 100,               // Required
    Type = OrderTypes.Market,   // Explicit or Price = 0
    Price = 0,                  // Market orders have no price limit
};
```

### When to Use

- Need guaranteed execution
- High liquidity instruments
- Time is more important than price
- Closing positions quickly

### Advantages

- Fast execution
- Guaranteed fill (in liquid markets)
- Simple to implement

### Disadvantages

- No price control
- Potential slippage
- Higher costs in illiquid markets

### Example

```csharp
// Explicit market order
var order = new Order
{
    Security = _eurusd,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 1000,
    Type = OrderTypes.Market
};

_connector.RegisterOrder(order);
```

```csharp
// Implicit market order (Price = 0)
var order = new Order
{
    Security = _eurusd,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 500,
    // Type will auto-detect as Market because Price is not set
};

_connector.RegisterOrder(order);
```

---

## Limit Orders

### Description

Limit orders are executed only at the specified price or better. They provide price control but no execution guarantee.

### Characteristics

- **Execution**: When market reaches limit price
- **Price**: Specified limit price or better
- **Guarantee**: Price limit, not execution
- **Risk**: May not execute

### Properties

```csharp
var limitOrder = new Order
{
    Security = security,
    Portfolio = portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Limit,    // Required for limit orders
    Price = 105.50m,            // Required - must be > 0
    TimeInForce = TimeInForce.PutInQueue,  // Optional
    ExpiryDate = DateTimeOffset.Now.AddDays(1),  // Optional
};
```

### Validation

```csharp
if (order.Price == 0 && order.Type == OrderTypes.Limit)
    throw new ArgumentException("Limit order must have price");
```

### When to Use

- Price is more important than execution speed
- Want to control entry/exit price
- Willing to wait for better price
- Passive trading strategy

### Advantages

- Price control
- Can get better fills
- Provide market liquidity
- Lower spreads

### Disadvantages

- May not execute
- Requires monitoring
- Queue position matters

### Example: Basic Limit Order

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Limit,
    Price = 99.50m,  // Buy only at 99.50 or lower
};

_connector.RegisterOrder(order);
```

### Example: Limit Order with Expiry

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 50,
    Type = OrderTypes.Limit,
    Price = 102.00m,
    // Expire in 4 hours
    ExpiryDate = DateTimeOffset.Now.AddHours(4)
};

_connector.RegisterOrder(order);
```

### Example: Iceberg Limit Order

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 10000,          // Total volume
    VisibleVolume = 100,     // Show only 100 at a time
    Type = OrderTypes.Limit,
    Price = 100.00m
};

// Order will show 100, refill as executed
_connector.RegisterOrder(order);
```

---

## Conditional Orders

### Description

Conditional orders (stop orders, algo orders) are activated only when specific conditions are met. They require an `OrderCondition` object that defines the activation criteria.

### Characteristics

- **Execution**: When condition is triggered
- **Price**: Depends on condition type
- **Guarantee**: Conditional activation
- **Risk**: Slippage on trigger

### Properties

```csharp
var conditionalOrder = new Order
{
    Security = security,
    Portfolio = portfolio,
    Side = Sides.Sell,
    Volume = 100,
    Type = OrderTypes.Conditional,  // Required
    Condition = orderCondition,     // Required - adapter-specific
};
```

### Validation

```csharp
if (order.Type == OrderTypes.Conditional && order.Condition == null)
    throw new ArgumentException("Condition not specified for conditional order");
```

### When to Use

- Stop-loss protection
- Take-profit targets
- Breakout strategies
- Automated risk management

### Condition Types

Different adapters support different condition types. Check adapter capabilities:

```csharp
var conditionType = _connector.Adapter.OrderConditionType;

if (conditionType == null)
{
    Console.WriteLine("Adapter doesn't support conditional orders");
    return;
}
```

---

## TimeInForce Options

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\TimeInForce.cs`

### Enumeration

```csharp
public enum TimeInForce
{
    /// <summary>
    /// Good Till Cancelled (GTC)
    /// Order remains active until filled or cancelled
    /// </summary>
    PutInQueue,

    /// <summary>
    /// Fill Or Kill (FOK)
    /// Order must be filled immediately in full or cancelled
    /// </summary>
    MatchOrCancel,

    /// <summary>
    /// Immediate Or Cancel (IOC)
    /// Fill any amount immediately, cancel the rest
    /// </summary>
    CancelBalance,
}
```

### 1. PutInQueue (GTC - Good Till Cancelled)

**Description**: Order stays active until completely filled or manually cancelled.

**Use Cases**:
- Long-term limit orders
- Passive liquidity provision
- Don't care about execution timing

**Example**:
```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Limit,
    Price = 99.00m,
    TimeInForce = TimeInForce.PutInQueue,  // Stays until filled or cancelled
};
```

### 2. MatchOrCancel (FOK - Fill Or Kill)

**Description**: Order must be completely filled immediately or cancelled entirely. No partial fills allowed.

**Use Cases**:
- Large orders requiring immediate execution
- Avoiding partial fills
- Liquidity-sensitive strategies

**Example**:
```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 1000,  // All 1000 must execute immediately
    Type = OrderTypes.Limit,
    Price = 100.50m,
    TimeInForce = TimeInForce.MatchOrCancel,  // All or nothing
};
```

### 3. CancelBalance (IOC - Immediate Or Cancel)

**Description**: Fill whatever amount is available immediately, cancel the rest. Partial fills accepted.

**Use Cases**:
- Testing market liquidity
- Urgent partial execution acceptable
- Minimizing market impact

**Example**:
```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 500,
    Type = OrderTypes.Limit,
    Price = 101.00m,
    TimeInForce = TimeInForce.CancelBalance,  // Partial fills OK
};

// Might execute 300, cancel remaining 200
```

### Comparison Table

| TimeInForce | Partial Fills | Duration | Use Case |
|-------------|---------------|----------|----------|
| PutInQueue (GTC) | Yes | Until cancelled | Passive trading |
| MatchOrCancel (FOK) | No | Immediate | All-or-nothing execution |
| CancelBalance (IOC) | Yes | Immediate | Quick partial execution |

---

## Order Properties

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Order.cs`

### Core Properties

```csharp
public class Order
{
    // REQUIRED PROPERTIES

    /// <summary>
    /// Security to trade
    /// </summary>
    public Security Security { get; set; }

    /// <summary>
    /// Portfolio/Account
    /// </summary>
    public Portfolio Portfolio { get; set; }

    /// <summary>
    /// Buy or Sell
    /// </summary>
    public Sides Side { get; set; }

    /// <summary>
    /// Order volume
    /// </summary>
    public decimal Volume { get; set; }

    // OPTIONAL PROPERTIES

    /// <summary>
    /// Order type (auto-detected if not set)
    /// </summary>
    public OrderTypes? Type { get; set; }

    /// <summary>
    /// Limit price (required for Limit orders)
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public TimeInForce? TimeInForce { get; set; }

    /// <summary>
    /// Order expiry time (null = GTC)
    /// </summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>
    /// Visible volume for iceberg orders
    /// </summary>
    public decimal? VisibleVolume { get; set; }

    /// <summary>
    /// Order condition (for conditional orders)
    /// </summary>
    public OrderCondition Condition { get; set; }

    /// <summary>
    /// User comment
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// User-defined order ID
    /// </summary>
    public string UserOrderId { get; set; }

    /// <summary>
    /// Strategy ID
    /// </summary>
    public string StrategyId { get; set; }

    // POSITION PROPERTIES

    /// <summary>
    /// Position effect (Open/Close)
    /// </summary>
    public OrderPositionEffects? PositionEffect { get; set; }

    /// <summary>
    /// Margin leverage
    /// </summary>
    public int? Leverage { get; set; }

    // EXECUTION PROPERTIES

    /// <summary>
    /// Post-only (maker-only) order
    /// </summary>
    public bool? PostOnly { get; set; }

    /// <summary>
    /// Minimum execution volume
    /// </summary>
    public decimal? MinVolume { get; set; }

    // READ-ONLY PROPERTIES (Set by system)

    /// <summary>
    /// Transaction ID (auto-generated)
    /// </summary>
    public long TransactionId { get; set; }

    /// <summary>
    /// Order state
    /// </summary>
    public OrderStates State { get; set; }

    /// <summary>
    /// Remaining volume
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Average execution price
    /// </summary>
    public decimal? AveragePrice { get; set; }

    /// <summary>
    /// Exchange order ID
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// Exchange order ID (string)
    /// </summary>
    public string StringId { get; set; }
}
```

### Sides Enumeration

```csharp
public enum Sides
{
    Buy,   // Buy/Long
    Sell   // Sell/Short
}
```

---

## Order Conditions

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderCondition.cs`

### Base OrderCondition Class

```csharp
public abstract class OrderCondition : Cloneable<OrderCondition>
{
    /// <summary>
    /// Condition parameters (adapter-specific)
    /// </summary>
    public IDictionary<string, object> Parameters { get; set; }
}
```

### Common Condition Interfaces

#### IStopLossOrderCondition

```csharp
public interface IStopLossOrderCondition
{
    /// <summary>
    /// Activation price (stop price)
    /// </summary>
    decimal? ActivationPrice { get; set; }

    /// <summary>
    /// Close position price (null = market)
    /// </summary>
    decimal? ClosePositionPrice { get; set; }

    /// <summary>
    /// Trailing stop-loss
    /// </summary>
    bool IsTrailing { get; set; }
}
```

#### ITakeProfitOrderCondition

```csharp
public interface ITakeProfitOrderCondition
{
    /// <summary>
    /// Activation price (take-profit level)
    /// </summary>
    decimal? ActivationPrice { get; set; }

    /// <summary>
    /// Close position price (null = market)
    /// </summary>
    decimal? ClosePositionPrice { get; set; }
}
```

### Getting Adapter Condition Type

```csharp
// Get the condition type supported by current adapter
var conditionType = _connector.Adapter.OrderConditionType;

if (conditionType == null)
{
    Console.WriteLine("Conditional orders not supported");
    return;
}

// Create instance
var condition = Activator.CreateInstance(conditionType) as OrderCondition;
```

---

## Practical Examples

### Example 1: Market Order (Buy)

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Market,
    Comment = "Quick entry"
};

_connector.RegisterOrder(order);
```

### Example 2: Limit Order with GTC

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 50,
    Type = OrderTypes.Limit,
    Price = 110.00m,
    TimeInForce = TimeInForce.PutInQueue,  // GTC
    Comment = "Take profit at 110"
};

_connector.RegisterOrder(order);
```

### Example 3: FOK Order

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 1000,
    Type = OrderTypes.Limit,
    Price = 99.50m,
    TimeInForce = TimeInForce.MatchOrCancel,  // FOK - all or nothing
    Comment = "Large order - need full execution"
};

_connector.RegisterOrder(order);
```

### Example 4: IOC Order

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 500,
    Type = OrderTypes.Limit,
    Price = 100.00m,
    TimeInForce = TimeInForce.CancelBalance,  // IOC - partial OK
    Comment = "Exit quickly"
};

_connector.RegisterOrder(order);
```

### Example 5: Stop-Loss Order

```csharp
// Get adapter's condition type
var conditionType = _connector.Adapter.OrderConditionType;
var condition = Activator.CreateInstance(conditionType) as OrderCondition;

// Configure stop-loss
if (condition is IStopLossOrderCondition stopLoss)
{
    stopLoss.ActivationPrice = 95.00m;        // Trigger at 95.00
    stopLoss.ClosePositionPrice = null;       // Market order when triggered
    stopLoss.IsTrailing = false;              // Fixed stop
}

var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 100,
    Type = OrderTypes.Conditional,
    Condition = condition,
    Comment = "Stop-loss protection"
};

_connector.RegisterOrder(order);
```

### Example 6: Trailing Stop-Loss

```csharp
var conditionType = _connector.Adapter.OrderConditionType;
var condition = Activator.CreateInstance(conditionType) as OrderCondition;

if (condition is IStopLossOrderCondition stopLoss)
{
    stopLoss.ActivationPrice = 98.00m;        // Initial stop
    stopLoss.ClosePositionPrice = null;       // Market on trigger
    stopLoss.IsTrailing = true;               // Follows price up
}

var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 100,
    Type = OrderTypes.Conditional,
    Condition = condition
};

_connector.RegisterOrder(order);
```

### Example 7: Take-Profit Order

```csharp
var conditionType = _connector.Adapter.OrderConditionType;
var condition = Activator.CreateInstance(conditionType) as OrderCondition;

if (condition is ITakeProfitOrderCondition takeProfit)
{
    takeProfit.ActivationPrice = 110.00m;     // Trigger at 110
    takeProfit.ClosePositionPrice = 109.90m;  // Limit order at 109.90
}

var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Sell,
    Volume = 100,
    Type = OrderTypes.Conditional,
    Condition = condition
};

_connector.RegisterOrder(order);
```

### Example 8: Iceberg Order

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 10000,          // Total volume
    VisibleVolume = 100,     // Show only 100
    Type = OrderTypes.Limit,
    Price = 100.00m,
    TimeInForce = TimeInForce.PutInQueue,
    Comment = "Large iceberg order"
};

// Market sees only 100 at a time
// Automatically refills as executed
_connector.RegisterOrder(order);
```

### Example 9: Post-Only Order (Maker)

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Limit,
    Price = 99.50m,
    PostOnly = true,  // Only add liquidity, never take
    Comment = "Maker order only"
};

// Will be rejected if would execute immediately
_connector.RegisterOrder(order);
```

### Example 10: Order with Leverage

```csharp
var order = new Order
{
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Limit,
    Price = 100.00m,
    Leverage = 5,  // 5x leverage
    Comment = "Margin order with 5x leverage"
};

_connector.RegisterOrder(order);
```

### Example 11: Complete Order with All Options

```csharp
var order = new Order
{
    // Required
    Security = _security,
    Portfolio = _portfolio,
    Side = Sides.Buy,
    Volume = 100,

    // Type and price
    Type = OrderTypes.Limit,
    Price = 100.00m,

    // Time control
    TimeInForce = TimeInForce.PutInQueue,
    ExpiryDate = DateTimeOffset.Now.AddDays(7),

    // Visibility
    VisibleVolume = 10,  // Iceberg

    // Execution
    PostOnly = true,     // Maker only
    MinVolume = 50,      // Minimum fill

    // Position
    PositionEffect = OrderPositionEffects.Open,
    Leverage = 3,

    // Tracking
    Comment = "Complete example order",
    UserOrderId = "USER-12345",
    StrategyId = "Strategy-ABC"
};

_connector.RegisterOrder(order);
```

---

## Order Type Selection Guide

### Choose Market Order When:
- Speed is critical
- Guaranteed execution needed
- High liquidity available
- Closing emergency positions

### Choose Limit Order When:
- Price control is important
- Can wait for better price
- Providing liquidity
- Setting entry/exit targets

### Choose Conditional Order When:
- Need automated risk management
- Stop-loss/take-profit required
- Breakout trading
- Unmonitored positions

---

## Best Practices

### 1. Validate Before Registration

```csharp
private bool ValidateOrder(Order order)
{
    // Check required fields
    if (order.Security == null || order.Portfolio == null)
        return false;

    // Check type-specific requirements
    if (order.Type == OrderTypes.Limit && order.Price <= 0)
    {
        Console.WriteLine("Limit orders require price > 0");
        return false;
    }

    if (order.Type == OrderTypes.Conditional && order.Condition == null)
    {
        Console.WriteLine("Conditional orders require condition");
        return false;
    }

    // Check volume
    if (order.Volume <= 0)
    {
        Console.WriteLine("Volume must be positive");
        return false;
    }

    return true;
}
```

### 2. Use TimeInForce Appropriately

```csharp
// For passive orders
order.TimeInForce = TimeInForce.PutInQueue;

// For urgent all-or-nothing
order.TimeInForce = TimeInForce.MatchOrCancel;

// For quick partial execution
order.TimeInForce = TimeInForce.CancelBalance;
```

### 3. Check Adapter Capabilities

```csharp
// Check conditional order support
if (_connector.Adapter.OrderConditionType == null)
{
    Console.WriteLine("Use limit orders instead of stop orders");
}

// Check post-only support
var capabilities = _connector.Adapter.GetSupportedMessages();
if (!capabilities.Contains(MessageTypes.OrderRegister))
{
    Console.WriteLine("Check specific order type support");
}
```

### 4. Handle Auto-Type Detection

```csharp
// Explicit is better
order.Type = OrderTypes.Limit;
order.Price = 100.00m;

// Instead of relying on auto-detection
// order.Price = 100.00m;  // Type will be Limit
```

---

## Related Specifications

- [Order Management](./order-management.md) - Order operations
- [Transaction Tracking](./transaction-tracking.md) - Order lifecycle
- [Position Management](./position-management.md) - Position tracking

---

## References

- **Source Files**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderTypes.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\TimeInForce.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderCondition.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Order.cs`
