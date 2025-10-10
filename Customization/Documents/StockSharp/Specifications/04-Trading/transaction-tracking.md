# Transaction Tracking Specification

## Overview

This specification covers the transaction lifecycle in StockSharp, including transaction ID generation, order state transitions, correlation mechanisms, and tracking patterns. Understanding transaction tracking is critical for reliable order management and system debugging.

## Table of Contents

1. [Transaction ID System](#transaction-id-system)
2. [Order Lifecycle States](#order-lifecycle-states)
3. [Transaction Correlation](#transaction-correlation)
4. [Original Transaction ID](#original-transaction-id)
5. [Order Tracking Patterns](#order-tracking-patterns)
6. [Message Flow](#message-flow)
7. [Practical Examples](#practical-examples)

---

## Transaction ID System

### Overview

Transaction IDs are unique identifiers assigned to every order operation (register, cancel, modify). They enable tracking and correlation throughout the order lifecycle.

### TransactionIdGenerator

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:147`

```csharp
public class Connector
{
    /// <summary>
    /// Transaction ID generator
    /// </summary>
    public IdGenerator TransactionIdGenerator
    {
        get => Adapter.TransactionIdGenerator;
        set => Adapter.TransactionIdGenerator = value;
    }
}
```

### Default Generator

```csharp
// Default: MillisecondIncrementalIdGenerator
// Generates incrementing IDs based on millisecond timestamps
var connector = new Connector();
// TransactionIdGenerator is automatically initialized
```

### Custom Generator

```csharp
// Use custom generator
_connector.TransactionIdGenerator = new IncrementalIdGenerator();

// Or sequential generator
_connector.TransactionIdGenerator = new SequentialIdGenerator
{
    Current = 1000  // Start from 1000
};
```

### Generation Pattern

```csharp
// Automatic generation in RegisterOrder
public void RegisterOrder(Order order)
{
    // ...
    if (order.TransactionId == 0)
        order.TransactionId = TransactionIdGenerator.GetNextId();
    // ...
}

// Manual generation
long transactionId = _connector.TransactionIdGenerator.GetNextId();
```

### Transaction ID Properties

```csharp
public class Order
{
    /// <summary>
    /// Transaction ID
    /// Automatically assigned when order is registered
    /// Unique per operation
    /// </summary>
    public long TransactionId { get; set; }
}
```

---

## Order Lifecycle States

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderStates.cs`

### State Enumeration

```csharp
public enum OrderStates
{
    /// <summary>
    /// Not sent to trading system
    /// Initial state before registration
    /// </summary>
    None,

    /// <summary>
    /// Pending registration
    /// Sent to adapter, waiting for exchange confirmation
    /// </summary>
    Pending,

    /// <summary>
    /// Active on exchange
    /// Order is in the order book
    /// </summary>
    Active,

    /// <summary>
    /// Done (filled or cancelled)
    /// Final state - no longer active
    /// </summary>
    Done,

    /// <summary>
    /// Failed/Rejected
    /// Order was not accepted
    /// </summary>
    Failed
}
```

### State Transitions

```
None → Pending → Active → Done
  ↓       ↓        ↓
  Failed  Failed   Failed
```

### Typical Flow

#### 1. New Order Registration

```
State: None (before RegisterOrder)
  ↓
TransactionId assigned
  ↓
State: Pending (after RegisterOrder)
  ↓
Sent to exchange
  ↓
State: Active (exchange confirms)
  ↓
Executions occur
  ↓
State: Done (fully filled or cancelled)
```

#### 2. Registration Failure

```
State: None
  ↓
TransactionId assigned
  ↓
State: Pending
  ↓
Exchange rejects
  ↓
State: Failed
  ↓
OrderRegisterFailReceived event
```

#### 3. Cancellation

```
State: Active
  ↓
CancelOrder called
  ↓
New TransactionId for cancel operation
  ↓
Cancel sent to exchange
  ↓
State: Done (cancel confirmed)
```

### State Validation

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:196`

```csharp
// Before registration
private void CheckOnNew(Order order)
{
    if (order.TransactionId != 0)
        throw new ArgumentException("Order already has TransactionId");

    if (order.State != OrderStates.None)
        throw new ArgumentException("Order already has State");

    if (order.Id != null || !order.StringId.IsEmpty())
        throw new ArgumentException("Order already has ID");
}

// Before modification/cancellation
private static void CheckOnOld(Order order)
{
    if (order.TransactionId == 0)
        throw new ArgumentException("Order has no TransactionId");
}
```

### State Checking Examples

```csharp
// Check if order is pending
if (order.State == OrderStates.Pending)
{
    Console.WriteLine("Order sent, awaiting confirmation");
}

// Check if order is active
if (order.State == OrderStates.Active)
{
    Console.WriteLine($"Order active with balance: {order.Balance}");
}

// Check if order is final
if (order.State == OrderStates.Done || order.State == OrderStates.Failed)
{
    Console.WriteLine("Order in final state");
}

// Check if order can be cancelled
bool canCancel = order.State == OrderStates.Active || order.State == OrderStates.Pending;

if (canCancel)
{
    _connector.CancelOrder(order);
}
```

---

## Transaction Correlation

### TransactionId in Different Operations

Each operation type gets its own transaction ID:

#### 1. Order Registration

```csharp
var order = new Order { /* ... */ };

// TransactionId = 0 (not yet assigned)
_connector.RegisterOrder(order);

// TransactionId = 12345 (auto-assigned)
Console.WriteLine($"Registration TransactionId: {order.TransactionId}");
```

#### 2. Order Cancellation

```csharp
// Order has TransactionId from registration (e.g., 12345)
long orderTransactionId = order.TransactionId;

// Cancel generates NEW transaction ID
_connector.CancelOrder(order);

// Inside CancelOrder:
long cancelTransactionId = TransactionIdGenerator.GetNextId(); // e.g., 12346

// Track correlation
_entityCache.AddOrderByCancelationId(order, cancelTransactionId);
```

#### 3. Order Modification

```csharp
// Original order TransactionId: 12345
var changes = new Order { Price = 105.00m };

// Edit generates NEW transaction ID
_connector.EditOrder(order, changes);

// Inside EditOrder:
long editTransactionId = TransactionIdGenerator.GetNextId(); // e.g., 12347
changes.TransactionId = editTransactionId;
```

### Correlation in EntityCache

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\EntityCache.cs`

```csharp
internal class EntityCache
{
    // Track orders by registration transaction ID
    public void AddOrderByRegistrationId(Order order)
    {
        _ordersByTransactionId[order.TransactionId] = order;
    }

    // Track orders by cancellation transaction ID
    public void AddOrderByCancelationId(Order order, long transactionId)
    {
        _ordersByCancelTransactionId[transactionId] = order;
    }

    // Track orders by edition transaction ID
    public void AddOrderByEditionId(Order order, long transactionId)
    {
        _ordersByEditTransactionId[transactionId] = order;
    }
}
```

---

## Original Transaction ID

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\IOriginalTransactionIdMessage.cs`

### Interface

```csharp
/// <summary>
/// Interface for messages that reference an original transaction
/// </summary>
public interface IOriginalTransactionIdMessage
{
    /// <summary>
    /// Original transaction ID being referenced
    /// </summary>
    long OriginalTransactionId { get; set; }
}
```

### Used By

```csharp
// ExecutionMessage (order updates, trades)
public class ExecutionMessage : IOriginalTransactionIdMessage
{
    public long TransactionId { get; set; }          // New transaction ID
    public long OriginalTransactionId { get; set; }  // References original operation
}

// ErrorMessage (failures)
public class ErrorMessage : IOriginalTransactionIdMessage
{
    public long OriginalTransactionId { get; set; }  // Failed transaction
}

// BaseResultMessage (responses)
public abstract class BaseResultMessage : IOriginalTransactionIdMessage
{
    public long OriginalTransactionId { get; set; }
}
```

### Usage Pattern

```csharp
// Order registration
order.TransactionId = 12345;
_connector.RegisterOrder(order);

// Exchange confirms order
// ExecutionMessage received:
{
    TransactionId = 0,              // May be 0 for updates
    OriginalTransactionId = 12345,  // References registration
    OrderId = 98765,                // Exchange order ID
    OrderState = OrderStates.Active
}

// Cancel order
long cancelTransactionId = 12346;
_connector.CancelOrder(order);  // Uses cancelTransactionId internally

// Cancellation confirmed
// ExecutionMessage received:
{
    TransactionId = 0,
    OriginalTransactionId = 12346,  // References cancel operation
    OrderState = OrderStates.Done
}
```

### Correlation Flow Diagram

```
User Operation          TransactionId    OriginalTransactionId    Exchange ID
----------------------------------------------------------------------------------
RegisterOrder(order)    12345           -                         -
  ↓
Exchange Confirm        -               12345                     98765
  ↓
CancelOrder(order)      12346           -                         -
  ↓
Cancel Confirm          -               12346                     98765
  ↓
Final Update            -               12346                     98765
                                        (OrderState.Done)
```

---

## Order Tracking Patterns

### Pattern 1: Track by TransactionId

```csharp
public class TransactionTracker
{
    private readonly Dictionary<long, Order> _ordersByTransactionId = new();

    public void RegisterOrder(Order order, Connector connector)
    {
        connector.RegisterOrder(order);

        // Track by transaction ID
        _ordersByTransactionId[order.TransactionId] = order;

        Console.WriteLine($"Tracking order with TransactionId: {order.TransactionId}");
    }

    public Order GetOrderByTransactionId(long transactionId)
    {
        return _ordersByTransactionId.GetValueOrDefault(transactionId);
    }
}
```

### Pattern 2: Track by Exchange ID

```csharp
public class ExchangeOrderTracker
{
    private readonly Dictionary<long, Order> _ordersByExchangeId = new();

    public void OnOrderReceived(Subscription s, Order order)
    {
        if (order.Id.HasValue)
        {
            _ordersByExchangeId[order.Id.Value] = order;
            Console.WriteLine($"Tracking order with Exchange ID: {order.Id}");
        }
    }

    public Order GetOrderByExchangeId(long exchangeId)
    {
        return _ordersByExchangeId.GetValueOrDefault(exchangeId);
    }
}
```

### Pattern 3: Complete Order Lifecycle Tracking

```csharp
public class OrderLifecycleTracker
{
    private class OrderTracking
    {
        public Order Order { get; set; }
        public long RegistrationTransactionId { get; set; }
        public long? CancellationTransactionId { get; set; }
        public long? ModificationTransactionId { get; set; }
        public long? ExchangeOrderId { get; set; }
        public List<OrderStates> StateHistory { get; set; } = new();
        public DateTimeOffset RegistrationTime { get; set; }
        public DateTimeOffset? CompletionTime { get; set; }
    }

    private readonly Dictionary<long, OrderTracking> _tracking = new();
    private readonly Connector _connector;

    public OrderLifecycleTracker(Connector connector)
    {
        _connector = connector;
        _connector.OrderReceived += OnOrderReceived;
    }

    public void RegisterOrder(Order order)
    {
        _connector.RegisterOrder(order);

        var tracking = new OrderTracking
        {
            Order = order,
            RegistrationTransactionId = order.TransactionId,
            RegistrationTime = DateTimeOffset.Now,
            StateHistory = { order.State }
        };

        _tracking[order.TransactionId] = tracking;

        Console.WriteLine($"[{order.TransactionId}] Order registered");
    }

    public void CancelOrder(Order order)
    {
        if (!_tracking.TryGetValue(order.TransactionId, out var tracking))
            return;

        // Cancel will generate new transaction ID internally
        // We track it via order updates

        _connector.CancelOrder(order);

        Console.WriteLine($"[{order.TransactionId}] Cancellation requested");
    }

    private void OnOrderReceived(Subscription s, Order order)
    {
        if (!_tracking.TryGetValue(order.TransactionId, out var tracking))
            return;

        // Track state changes
        if (tracking.StateHistory.LastOrDefault() != order.State)
        {
            tracking.StateHistory.Add(order.State);
            Console.WriteLine($"[{order.TransactionId}] State: {order.State}");
        }

        // Track exchange ID
        if (order.Id.HasValue && !tracking.ExchangeOrderId.HasValue)
        {
            tracking.ExchangeOrderId = order.Id;
            Console.WriteLine($"[{order.TransactionId}] Exchange ID: {order.Id}");
        }

        // Track completion
        if ((order.State == OrderStates.Done || order.State == OrderStates.Failed)
            && !tracking.CompletionTime.HasValue)
        {
            tracking.CompletionTime = DateTimeOffset.Now;
            var duration = tracking.CompletionTime.Value - tracking.RegistrationTime;

            Console.WriteLine($"[{order.TransactionId}] Completed in {duration.TotalSeconds:F2}s");
            PrintLifecycle(tracking);
        }
    }

    private void PrintLifecycle(OrderTracking tracking)
    {
        Console.WriteLine($"\n=== Order Lifecycle: {tracking.RegistrationTransactionId} ===");
        Console.WriteLine($"Registration Time: {tracking.RegistrationTime}");
        Console.WriteLine($"Completion Time: {tracking.CompletionTime}");
        Console.WriteLine($"Exchange ID: {tracking.ExchangeOrderId}");
        Console.WriteLine($"State History: {string.Join(" -> ", tracking.StateHistory)}");

        if (tracking.CancellationTransactionId.HasValue)
        {
            Console.WriteLine($"Cancellation TransactionId: {tracking.CancellationTransactionId}");
        }
    }

    public void PrintAllActiveOrders()
    {
        Console.WriteLine("\n=== Active Orders ===");

        foreach (var tracking in _tracking.Values)
        {
            if (tracking.Order.State == OrderStates.Active ||
                tracking.Order.State == OrderStates.Pending)
            {
                Console.WriteLine($"[{tracking.RegistrationTransactionId}] " +
                    $"{tracking.Order.Security.Code} {tracking.Order.Side} " +
                    $"{tracking.Order.Volume} @ {tracking.Order.Price} " +
                    $"(State: {tracking.Order.State})");
            }
        }
    }
}
```

---

## Message Flow

### Registration Message Flow

```
Client                          Connector                    Adapter                     Exchange
  |                                |                           |                            |
  | RegisterOrder(order)           |                           |                            |
  |─────────────────────────────> |                           |                            |
  |                                |                           |                            |
  |                                | Generate TransactionId    |                            |
  |                                | Set State = Pending       |                            |
  |                                |                           |                            |
  |                                | OrderRegisterMessage      |                            |
  |                                |────────────────────────> |                            |
  |                                |                           |                            |
  |                                |                           | Send to exchange           |
  |                                |                           |─────────────────────────> |
  |                                |                           |                            |
  | OrderReceived (Pending)        |                           |                            |
  | <──────────────────────────────|                           |                            |
  |                                |                           |                            |
  |                                |                           |        Order Confirmed     |
  |                                |                           | <──────────────────────────|
  |                                |                           |                            |
  |                                | ExecutionMessage          |                            |
  |                                | (OriginalTransactionId)   |                            |
  |                                | <─────────────────────────|                            |
  |                                |                           |                            |
  | OrderReceived (Active)         |                           |                            |
  | <──────────────────────────────|                           |                            |
```

### Cancellation Message Flow

```
Client                          Connector                    Adapter                     Exchange
  |                                |                           |                            |
  | CancelOrder(order)             |                           |                            |
  |─────────────────────────────> |                           |                            |
  |                                |                           |                            |
  |                                | Generate Cancel           |                            |
  |                                | TransactionId             |                            |
  |                                |                           |                            |
  |                                | OrderCancelMessage        |                            |
  |                                |────────────────────────> |                            |
  |                                |                           |                            |
  |                                |                           | Send cancel to exchange    |
  |                                |                           |─────────────────────────> |
  |                                |                           |                            |
  |                                |                           |        Cancel Confirmed    |
  |                                |                           | <──────────────────────────|
  |                                |                           |                            |
  |                                | ExecutionMessage          |                            |
  |                                | (OriginalTransactionId    |                            |
  |                                |  = Cancel TransactionId)  |                            |
  |                                | <─────────────────────────|                            |
  |                                |                           |                            |
  | OrderReceived (Done)           |                           |                            |
  | <──────────────────────────────|                           |                            |
```

---

## Practical Examples

### Example 1: Simple Transaction Tracking

```csharp
public class SimpleTransactionTracker
{
    private readonly Connector _connector;

    public SimpleTransactionTracker(Connector connector)
    {
        _connector = connector;
        _connector.OrderReceived += OnOrderReceived;
        _connector.OrderRegisterFailReceived += OnOrderFail;
    }

    public void PlaceOrder(Security security, Portfolio portfolio, decimal price, decimal volume)
    {
        var order = new Order
        {
            Security = security,
            Portfolio = portfolio,
            Side = Sides.Buy,
            Type = OrderTypes.Limit,
            Price = price,
            Volume = volume
        };

        _connector.RegisterOrder(order);

        Console.WriteLine($"[{order.TransactionId}] Order placed");
    }

    private void OnOrderReceived(Subscription s, Order order)
    {
        Console.WriteLine($"[{order.TransactionId}] " +
            $"State: {order.State}, " +
            $"Balance: {order.Balance}, " +
            $"Exchange ID: {order.Id}");
    }

    private void OnOrderFail(Subscription s, OrderFail fail)
    {
        Console.WriteLine($"[{fail.TransactionId}] Failed: {fail.Error.Message}");
    }
}
```

### Example 2: Transaction Correlation

```csharp
public class TransactionCorrelator
{
    private class Transaction
    {
        public long Id { get; set; }
        public OrderOperations Operation { get; set; }
        public Order Order { get; set; }
        public DateTimeOffset Time { get; set; }
        public long? OriginalTransactionId { get; set; }
    }

    private readonly List<Transaction> _transactions = new();

    public void TrackRegistration(Order order)
    {
        _transactions.Add(new Transaction
        {
            Id = order.TransactionId,
            Operation = OrderOperations.Register,
            Order = order,
            Time = DateTimeOffset.Now
        });
    }

    public void TrackCancellation(Order order, long cancelTransactionId)
    {
        _transactions.Add(new Transaction
        {
            Id = cancelTransactionId,
            Operation = OrderOperations.Cancel,
            Order = order,
            Time = DateTimeOffset.Now,
            OriginalTransactionId = order.TransactionId
        });
    }

    public void PrintTransactionHistory(Order order)
    {
        Console.WriteLine($"\n=== Transaction History for Order ===");

        var transactions = _transactions
            .Where(t => t.Order == order || t.OriginalTransactionId == order.TransactionId)
            .OrderBy(t => t.Time);

        foreach (var tx in transactions)
        {
            Console.WriteLine($"[{tx.Id}] {tx.Operation} at {tx.Time}");

            if (tx.OriginalTransactionId.HasValue)
            {
                Console.WriteLine($"  └─ References: {tx.OriginalTransactionId}");
            }
        }
    }
}
```

### Example 3: Order State Machine

```csharp
public class OrderStateMachine
{
    private readonly Order _order;
    private OrderStates _currentState;

    public OrderStateMachine(Order order)
    {
        _order = order;
        _currentState = order.State;
    }

    public bool CanTransitionTo(OrderStates newState)
    {
        return (_currentState, newState) switch
        {
            (OrderStates.None, OrderStates.Pending) => true,
            (OrderStates.Pending, OrderStates.Active) => true,
            (OrderStates.Pending, OrderStates.Failed) => true,
            (OrderStates.Active, OrderStates.Active) => true,  // Updates
            (OrderStates.Active, OrderStates.Done) => true,
            (OrderStates.Active, OrderStates.Failed) => true,
            _ => false
        };
    }

    public void TransitionTo(OrderStates newState)
    {
        if (!CanTransitionTo(newState))
        {
            throw new InvalidOperationException(
                $"Invalid state transition: {_currentState} -> {newState}");
        }

        Console.WriteLine($"[{_order.TransactionId}] State: {_currentState} -> {newState}");

        _currentState = newState;
    }

    public bool IsActive => _currentState == OrderStates.Active;
    public bool IsFinal => _currentState == OrderStates.Done || _currentState == OrderStates.Failed;
}
```

### Example 4: Transaction Logger

```csharp
public class TransactionLogger
{
    private readonly string _logFile;

    public TransactionLogger(string logFile)
    {
        _logFile = logFile;
    }

    public void LogRegistration(Order order)
    {
        var logEntry = $"[{DateTimeOffset.Now}] REGISTER " +
            $"TransactionId={order.TransactionId} " +
            $"Security={order.Security.Code} " +
            $"Side={order.Side} " +
            $"Price={order.Price} " +
            $"Volume={order.Volume}";

        File.AppendAllText(_logFile, logEntry + Environment.NewLine);
    }

    public void LogStateChange(Order order, OrderStates oldState, OrderStates newState)
    {
        var logEntry = $"[{DateTimeOffset.Now}] STATE_CHANGE " +
            $"TransactionId={order.TransactionId} " +
            $"ExchangeId={order.Id} " +
            $"From={oldState} " +
            $"To={newState} " +
            $"Balance={order.Balance}";

        File.AppendAllText(_logFile, logEntry + Environment.NewLine);
    }

    public void LogCancellation(Order order, long cancelTransactionId)
    {
        var logEntry = $"[{DateTimeOffset.Now}] CANCEL " +
            $"OrderTransactionId={order.TransactionId} " +
            $"CancelTransactionId={cancelTransactionId} " +
            $"ExchangeId={order.Id}";

        File.AppendAllText(_logFile, logEntry + Environment.NewLine);
    }

    public void LogError(long transactionId, Exception error)
    {
        var logEntry = $"[{DateTimeOffset.Now}] ERROR " +
            $"TransactionId={transactionId} " +
            $"Error={error.Message}";

        File.AppendAllText(_logFile, logEntry + Environment.NewLine);
    }
}
```

---

## Best Practices

### 1. Always Track TransactionId

```csharp
var order = new Order { /* ... */ };
_connector.RegisterOrder(order);

// ALWAYS save the transaction ID
long transactionId = order.TransactionId;
_myOrders[transactionId] = order;
```

### 2. Understand State Transitions

```csharp
_connector.OrderReceived += (s, order) =>
{
    switch (order.State)
    {
        case OrderStates.Pending:
            // Order sent, not yet confirmed
            Console.WriteLine("Waiting for exchange confirmation...");
            break;

        case OrderStates.Active:
            // Order confirmed by exchange
            Console.WriteLine($"Order active with ID: {order.Id}");
            break;

        case OrderStates.Done:
            // Check if filled or cancelled
            if (order.Balance == 0)
                Console.WriteLine("Order fully filled");
            else
                Console.WriteLine("Order cancelled");
            break;

        case OrderStates.Failed:
            Console.WriteLine("Order rejected");
            break;
    }
};
```

### 3. Correlate Operations

```csharp
// Track registration
long registerTransactionId = order.TransactionId;

// Cancel generates new transaction
_connector.CancelOrder(order);
// Internally generates cancelTransactionId

// Correlation happens via OriginalTransactionId in responses
```

### 4. Handle Latency

```csharp
_connector.OrderReceived += (s, order) =>
{
    // Registration latency
    if (order.LatencyRegistration.HasValue)
    {
        Console.WriteLine($"Registration took: {order.LatencyRegistration.Value.TotalMilliseconds}ms");

        if (order.LatencyRegistration.Value.TotalSeconds > 1)
        {
            Console.WriteLine("WARNING: High registration latency");
        }
    }
};
```

### 5. Log All Transactions

```csharp
var logger = new TransactionLogger("transactions.log");

_connector.OrderReceived += (s, order) =>
{
    logger.LogStateChange(order, oldState, order.State);
};

_connector.OrderRegisterFailReceived += (s, fail) =>
{
    logger.LogError(fail.TransactionId, fail.Error);
};
```

---

## Related Specifications

- [Order Management](./order-management.md) - Order operations using transactions
- [Order Types](./order-types.md) - Different order types and their lifecycle
- [Position Management](./position-management.md) - How orders affect positions

---

## References

- **Source Files**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\OrderStates.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\IOriginalTransactionIdMessage.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\ExecutionMessage.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\EntityCache.cs`
