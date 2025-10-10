# Event-Driven Trading with Market Rules

## Overview

StockSharp's rule system provides a powerful event-driven framework for responding to market events. Rules allow you to define actions that execute when specific conditions are met, creating reactive trading strategies.

**Location**: `StockSharp.Algo.MarketRuleHelper`

## Core Concepts

### What is a Market Rule?

A market rule is an event handler that:
- Listens for specific market events (order changes, trades, candles, etc.)
- Executes an action when the event occurs
- Can be combined with other rules using logical operators
- Automatically manages its lifecycle and cleanup

### Rule Lifecycle

```csharp
Rule Creation → Activation → Execution → Completion/Disposal
```

## Basic Rule Pattern

### WhenMatched - Order Filled

Triggers when an order is completely filled:

```csharp
order.WhenMatched(this)
    .Do(() =>
    {
        // Order is fully matched
        LogInfo("Order {0} completely filled!", order.TransactionId);
    })
    .Apply(this);
```

### WhenCanceled - Order Canceled

Triggers when an order is canceled:

```csharp
order.WhenCanceled(this)
    .Do(() =>
    {
        // Order was canceled
        LogInfo("Order {0} canceled", order.TransactionId);
    })
    .Apply(this);
```

### WhenRegistered - Order Registered

Triggers when an order is successfully registered on the exchange:

```csharp
order.WhenRegistered(this)
    .Do(() =>
    {
        // Order is now active on exchange
        LogInfo("Order {0} registered with ID {1}", order.TransactionId, order.Id);
    })
    .Apply(this);
```

### WhenRegisterFailed - Order Registration Failed

Triggers when order registration fails:

```csharp
order.WhenRegisterFailed(this)
    .Do((OrderFail fail) =>
    {
        // Order registration failed
        LogError("Order registration failed: {0}", fail.Error.Message);
    })
    .Apply(this);
```

### WhenPartiallyMatched - Partial Fill

Triggers each time an order is partially filled:

```csharp
order.WhenPartiallyMatched(this)
    .Do(() =>
    {
        LogInfo("Order partial fill: {0}/{1}",
            order.Volume - order.Balance, order.Volume);
    })
    .Apply(this);
```

### WhenNewTrade - Trade Execution

Triggers each time a trade occurs for an order:

```csharp
order.WhenNewTrade(this)
    .Do((MyTrade trade) =>
    {
        LogInfo("Trade executed: {0} at {1}", trade.Trade.Volume, trade.Trade.Price);
    })
    .Apply(this);
```

### WhenAllTrades - All Trades Received

Triggers when all trades for a completed order are received:

```csharp
order.WhenAllTrades(this)
    .Do((IEnumerable<MyTrade> trades) =>
    {
        var totalVolume = trades.Sum(t => t.Trade.Volume);
        var avgPrice = trades.Average(t => t.Trade.Price);
        LogInfo("All trades received. Total: {0} at avg {1}", totalVolume, avgPrice);
    })
    .Apply(this);
```

## Candle Rules

### WhenCandleFinished - Candle Completion

The most common pattern for candle-based strategies - triggers when a candle completes:

```csharp
// Using Bind() pattern (recommended)
var subscription = SubscribeCandles(CandleType);
subscription
    .Bind(indicator, ProcessCandle)
    .Start();

void ProcessCandle(ICandleMessage candle, decimal indicatorValue)
{
    // Automatically called only for finished candles
    if (candle.State != CandleStates.Finished)
        return;

    // Your trading logic here
}
```

Alternative explicit rule pattern:

```csharp
var subscription = SubscribeCandles(CandleType);

subscription.WhenCandleFinished(this)
    .Do((ICandleMessage candle) =>
    {
        // Process completed candle
        ProcessTradingSignal(candle);
    })
    .Apply(this);
```

## Rule Operators

### Do - Execute Action

Specifies the action to execute when the rule triggers:

```csharp
order.WhenMatched(this)
    .Do(() =>
    {
        // Action code here
        LogInfo("Order matched!");
    })
    .Apply(this);
```

With parameter:

```csharp
order.WhenNewTrade(this)
    .Do((MyTrade trade) =>
    {
        // Access the trade that triggered the rule
        LogInfo("Trade price: {0}", trade.Trade.Price);
    })
    .Apply(this);
```

### Once - Single Execution

Makes a rule trigger only once, then automatically removes itself:

```csharp
order.WhenMatched(this)
    .Do(() => LogInfo("Order matched!"))
    .Once()  // Will only trigger once
    .Apply(this);
```

This is the default behavior for many order rules (matched, canceled, registered).

### Until - Conditional Termination

Rule continues until the condition returns true:

```csharp
order.WhenPartiallyMatched(this)
    .Do(() =>
    {
        LogInfo("Partial fill: {0}", order.Balance);
    })
    .Until(() =>
    {
        // Stop when order is 75% filled
        return order.Balance <= order.Volume * 0.25m;
    })
    .Apply(this);
```

### Apply - Activate Rule

Adds the rule to the strategy's rule container and activates it:

```csharp
order.WhenMatched(this)
    .Do(() => LogInfo("Matched!"))
    .Apply(this);  // 'this' is the strategy instance
```

Without `Apply()`, the rule will not be active!

## Rule Composition

### Or - Logical OR

Triggers when any of the rules trigger:

```csharp
order.WhenMatched(this)
    .Or(order.WhenCanceled(this))
    .Do(() =>
    {
        // Triggered when order is EITHER matched OR canceled
        LogInfo("Order finished: {0}", order.State);
    })
    .Apply(this);
```

Multiple conditions:

```csharp
order1.WhenMatched(this)
    .Or(order2.WhenMatched(this), order3.WhenMatched(this))
    .Do(() =>
    {
        LogInfo("At least one order matched");
    })
    .Apply(this);
```

### And - Logical AND

Triggers only when all rules have triggered:

```csharp
order1.WhenMatched(this)
    .And(order2.WhenMatched(this))
    .Do(() =>
    {
        // Triggered only when BOTH orders are matched
        LogInfo("Both orders filled!");
    })
    .Apply(this);
```

### Exclusive - Mutual Exclusivity

When one rule triggers, automatically removes the other:

```csharp
var matchedRule = order.WhenMatched(this)
    .Do(() => LogInfo("Order matched"))
    .Apply(this);

var canceledRule = order.WhenCanceled(this)
    .Do(() => LogInfo("Order canceled"))
    .Apply(this);

// When either triggers, the other is automatically removed
matchedRule.Exclusive(canceledRule);
```

## Practical Examples

### Example 1: Simple Order with Cleanup

```csharp
private void PlaceOrder()
{
    var order = this.BuyLimit(Price, Volume);

    // Register the order
    RegisterOrder(order);

    // Handle successful fill
    order.WhenMatched(this)
        .Do(() =>
        {
            LogInfo("Order filled at {0}", order.Price);
            // Position opened, consider protective orders
            PlaceStopLoss();
        })
        .Apply(this);

    // Handle cancellation
    order.WhenCanceled(this)
        .Do(() =>
        {
            LogWarning("Order was canceled");
            // Maybe try again with different price
            RetryOrder();
        })
        .Apply(this);

    // Handle registration failure
    order.WhenRegisterFailed(this)
        .Do((OrderFail fail) =>
        {
            LogError("Failed to register order: {0}", fail.Error.Message);
        })
        .Apply(this);
}
```

### Example 2: Order Completion (Match OR Cancel)

```csharp
private void PlaceOrderWithTracking(Order order)
{
    RegisterOrder(order);

    // Track when order is no longer active (matched OR canceled OR failed)
    order.WhenMatched(this)
        .Or(order.WhenCanceled(this), order.WhenRegisterFailed(this))
        .Do(() =>
        {
            // Order is finished (any final state)
            LogInfo("Order {0} finished in state: {1}",
                order.TransactionId, order.State);

            // Clean up any related state
            RemoveOrderFromTracking(order);
        })
        .Apply(this);
}
```

### Example 3: Waiting for All Trades

```csharp
private void PlaceOrderAndWaitForTrades()
{
    var order = this.BuyMarket(Volume);
    RegisterOrder(order);

    // Wait for order to be fully matched
    var matchedRule = order.WhenMatched(this);

    // Wait for all trades to be received
    var allTradesRule = order.WhenAllTrades(this);

    // Both conditions must be met
    matchedRule.And(allTradesRule)
        .Do(() =>
        {
            // Order fully matched AND all trades received
            LogInfo("Order complete with all trades confirmed");
            ProcessCompletedOrder(order);
        })
        .Apply(this);
}
```

### Example 4: Bracket Order Pattern

```csharp
private void PlaceBracketOrder()
{
    // Entry order
    var entryOrder = this.BuyLimit(entryPrice, Volume);
    RegisterOrder(entryOrder);

    Order takeProfitOrder = null;
    Order stopLossOrder = null;

    // When entry filled, place protective orders
    entryOrder.WhenMatched(this)
        .Do(() =>
        {
            LogInfo("Entry filled, placing protective orders");

            // Place take profit
            takeProfitOrder = this.SellLimit(entryPrice + takeProfit, Volume);
            RegisterOrder(takeProfitOrder);

            // Place stop loss
            stopLossOrder = this.SellStop(entryPrice - stopLoss, Volume);
            RegisterOrder(stopLossOrder);

            // When take profit fills, cancel stop loss
            var tpRule = takeProfitOrder.WhenMatched(this)
                .Do(() =>
                {
                    LogInfo("Take profit hit!");
                    if (stopLossOrder.State.IsActive())
                        CancelOrder(stopLossOrder);
                })
                .Apply(this);

            // When stop loss fills, cancel take profit
            var slRule = stopLossOrder.WhenMatched(this)
                .Do(() =>
                {
                    LogInfo("Stop loss hit!");
                    if (takeProfitOrder.State.IsActive())
                        CancelOrder(takeProfitOrder);
                })
                .Apply(this);

            // Make rules mutually exclusive
            tpRule.Exclusive(slRule);
        })
        .Apply(this);

    // If entry canceled, no need for protective orders
    entryOrder.WhenCanceled(this)
        .Do(() =>
        {
            LogInfo("Entry order canceled");
        })
        .Apply(this);
}
```

### Example 5: Candle-Based Trading with Rules

```csharp
protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    var fastSma = new SimpleMovingAverage { Length = FastPeriod };
    var slowSma = new SimpleMovingAverage { Length = SlowPeriod };

    Indicators.Add(fastSma);
    Indicators.Add(slowSma);

    var subscription = SubscribeCandles(CandleType);

    // Method 1: Using Bind (recommended)
    subscription
        .Bind(fastSma, slowSma, ProcessCandle)
        .Start();

    // Method 2: Using explicit rules
    subscription.WhenCandleFinished(this)
        .Do((ICandleMessage candle) =>
        {
            if (!IsFormedAndOnlineAndAllowTrading())
                return;

            var fastValue = fastSma.GetCurrentValue();
            var slowValue = slowSma.GetCurrentValue();

            ProcessCandle(candle, fastValue, slowValue);
        })
        .Apply(this);
}

private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
{
    if (candle.State != CandleStates.Finished)
        return;

    // Your strategy logic
    if (fastValue > slowValue && Position <= 0)
    {
        var order = this.BuyMarket(Volume);
        RegisterOrder(order);

        // Track order completion
        order.WhenMatched(this)
            .Do(() => LogInfo("Buy order filled at {0}", candle.ClosePrice))
            .Apply(this);
    }
}
```

### Example 6: Multi-Order Coordination

```csharp
private void PlaceScaledEntry()
{
    var orders = new List<Order>();

    // Place 3 orders at different prices
    for (int i = 0; i < 3; i++)
    {
        var price = basePrice - i * priceStep;
        var order = this.BuyLimit(price, Volume / 3);
        RegisterOrder(order);
        orders.Add(order);
    }

    // Track partial fills
    var fillCount = 0;
    foreach (var order in orders)
    {
        order.WhenMatched(this)
            .Do(() =>
            {
                fillCount++;
                LogInfo("Order {0}/3 filled", fillCount);

                if (fillCount == 3)
                {
                    LogInfo("All orders filled, average price: {0}",
                        orders.Average(o => o.Price));
                }
            })
            .Apply(this);
    }

    // Cancel all remaining orders after timeout
    this.WhenIntervalElapsed(TimeSpan.FromMinutes(5))
        .Do(() =>
        {
            LogInfo("Timeout - canceling remaining orders");
            foreach (var order in orders)
            {
                if (order.State.IsActive())
                    CancelOrder(order);
            }
        })
        .Once()
        .Apply(this);
}
```

## Advanced Patterns

### Chaining Rules

```csharp
order1.WhenMatched(this)
    .Do(() =>
    {
        var order2 = this.SellLimit(targetPrice, Volume);
        RegisterOrder(order2);

        order2.WhenMatched(this)
            .Do(() =>
            {
                LogInfo("Both orders completed");
            })
            .Apply(this);
    })
    .Apply(this);
```

### Conditional Rules

```csharp
order.WhenPartiallyMatched(this)
    .Do(() =>
    {
        if (order.Balance < order.Volume * 0.5m)
        {
            // More than half filled, place protective order
            PlaceStopLoss();
        }
    })
    .Until(() => order.State.IsFinal())
    .Apply(this);
```

### Dynamic Rule Creation

```csharp
private void CreateDynamicRules(List<Order> orders)
{
    // Create rules for each order dynamically
    foreach (var order in orders)
    {
        order.WhenMatched(this)
            .Do(() => HandleOrderFill(order))
            .Apply(this);
    }
}
```

## Rule Management

### Accessing Rules

```csharp
// All active rules
var activeRules = Rules.Count;

// Check if rules exist
if (Rules.Any())
{
    LogInfo("Strategy has {0} active rules", Rules.Count);
}
```

### Manual Rule Removal

```csharp
// Remove specific rule (rarely needed - usually automatic)
if (this.TryRemoveRule(rule))
{
    LogInfo("Rule removed successfully");
}
```

### Suspending Rules

```csharp
// Suspend all rules temporarily
this.SuspendRules(() =>
{
    // Create multiple rules at once
    CreateMultipleRules();
});
// Rules resume after action completes
```

## Best Practices

### 1. Always Call Apply()

```csharp
// Good
order.WhenMatched(this)
    .Do(() => LogInfo("Matched"))
    .Apply(this);  // Rule is active

// Bad - rule will never trigger!
order.WhenMatched(this)
    .Do(() => LogInfo("Matched"));  // Missing Apply()
```

### 2. Use Once() for One-Time Events

```csharp
// Good - automatically removes after triggering
order.WhenMatched(this)
    .Do(() => LogInfo("Matched"))
    .Once()
    .Apply(this);

// Also good - Once() is implicit for matched/canceled/registered
order.WhenMatched(this)
    .Do(() => LogInfo("Matched"))
    .Apply(this);  // Already one-time by default
```

### 3. Handle All Order Outcomes

```csharp
// Good - handles all possible outcomes
var order = this.BuyLimit(price, volume);
RegisterOrder(order);

order.WhenMatched(this)
    .Do(() => HandleSuccess())
    .Apply(this);

order.WhenCanceled(this)
    .Do(() => HandleCancellation())
    .Apply(this);

order.WhenRegisterFailed(this)
    .Do(fail => HandleError(fail))
    .Apply(this);
```

### 4. Use Exclusive for Mutually Exclusive Events

```csharp
var matchRule = order.WhenMatched(this).Do(() => {...}).Apply(this);
var cancelRule = order.WhenCanceled(this).Do(() => {...}).Apply(this);

matchRule.Exclusive(cancelRule); // When one triggers, removes the other
```

### 5. Clean Up in Rules

```csharp
order.WhenMatched(this)
    .Or(order.WhenCanceled(this))
    .Do(() =>
    {
        // Clean up regardless of outcome
        CleanupOrderState(order);
    })
    .Apply(this);
```

### 6. Check State in Candle Rules

```csharp
subscription.Bind(indicator, (candle, value) =>
{
    if (candle.State != CandleStates.Finished)
        return;

    if (!IsFormedAndOnlineAndAllowTrading())
        return;

    // Trading logic here
});
```

## Common Pitfalls

### 1. Forgetting Apply()

```csharp
// WRONG - Rule never activates
order.WhenMatched(this).Do(() => LogInfo("Matched"));

// CORRECT
order.WhenMatched(this).Do(() => LogInfo("Matched")).Apply(this);
```

### 2. Not Checking Candle State

```csharp
// WRONG - Triggers on building candles too
subscription.WhenCandleFinished(this)
    .Do(candle =>
    {
        ProcessCandle(candle); // May process incomplete candles!
    })
    .Apply(this);

// CORRECT
subscription.WhenCandleFinished(this)
    .Do(candle =>
    {
        if (candle.State != CandleStates.Finished)
            return;

        ProcessCandle(candle);
    })
    .Apply(this);
```

### 3. Creating Infinite Loops

```csharp
// WRONG - Creates infinite loop!
subscription.WhenCandleFinished(this)
    .Do(candle =>
    {
        // This subscribes to MORE candles on every candle!
        SubscribeCandles(CandleType);
    })
    .Apply(this);

// CORRECT - Subscribe once in OnStarted
protected override void OnStarted(DateTimeOffset time)
{
    SubscribeCandles(CandleType);
}
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy fundamentals
- [parameters.md](parameters.md) - Strategy parameters
- [indicators.md](indicators.md) - Using indicators with rules
