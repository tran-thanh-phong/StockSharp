# Order Management Specification

## Overview

This specification covers order operations in StockSharp, including registration, cancellation, modification, and event handling. The order management system provides comprehensive control over order lifecycle with built-in validation and error handling.

## Table of Contents

1. [Order Registration](#order-registration)
2. [Order Cancellation](#order-cancellation)
3. [Order Modification](#order-modification)
4. [Order Events](#order-events)
5. [Error Handling](#error-handling)
6. [Practical Examples](#practical-examples)

---

## Order Registration

### RegisterOrder Method

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:538`

**Signature**:
```csharp
public void RegisterOrder(Order order)
```

**Description**: Registers a new order with the trading system. The method performs validation, initializes transaction tracking, and sends the order to the adapter.

### Registration Process

1. **Validation** - Verifies order parameters
2. **Transaction ID Assignment** - Generates unique transaction ID
3. **State Initialization** - Sets order state to `Pending`
4. **Adapter Submission** - Sends order to trading adapter
5. **Event Notification** - Raises `OrderReceived` event

### Order Validation Rules

The `RegisterOrder` method performs the following checks:

```csharp
// State validation
if (order.TransactionId != 0)
    throw new ArgumentException("Order already has TransactionId", nameof(order));

if (order.State != OrderStates.None)
    throw new ArgumentException("Order already has State", nameof(order));

if (order.Id != null || !order.StringId.IsEmpty())
    throw new ArgumentException("Order already has ID", nameof(order));

// Required fields validation
if (order.Security == null)
    throw new ArgumentException("Security not specified", nameof(order));

if (order.Portfolio == null)
    throw new ArgumentException("Portfolio not specified", nameof(order));

// Volume validation (except conditional orders)
if (order.Type != OrderTypes.Conditional)
{
    if (order.Volume <= 0)
        throw new ArgumentOutOfRangeException(nameof(order), order.Volume, "Invalid volume");
}

// Type auto-detection
order.Type ??= order.Price > 0 ? OrderTypes.Limit : OrderTypes.Market;

// Price validation for limit orders
if (order.Price == 0 && order.Type == OrderTypes.Limit)
    throw new ArgumentException("Limit order must have price", nameof(order));

// Conditional order validation
if (order.Type == OrderTypes.Conditional && order.Condition == null)
    throw new ArgumentException("Condition not specified for conditional order", nameof(order));
```

### Price and Volume Step Validation

When `CheckSteps` property is enabled:

```csharp
// Price step validation
if (order.Price > 0)
{
    var priceStep = order.Security.PriceStep;
    if (priceStep != null && (order.Price % priceStep.Value) != 0)
        throw new ArgumentException($"Price {order.Price} is not multiple of price step {priceStep.Value}");
}

// Volume step validation
var volumeStep = order.Security.VolumeStep;
if (volumeStep != null && (order.Volume % volumeStep.Value) != 0)
    throw new ArgumentException($"Volume {order.Volume} is not multiple of volume step {volumeStep.Value}");
```

### Transaction ID Generation

```csharp
// Automatic transaction ID generation
if (order.TransactionId == 0)
    order.TransactionId = TransactionIdGenerator.GetNextId();

// Order is tracked in entity cache by transaction ID
_entityCache.AddOrderByRegistrationId(order);
```

### Order Initialization

```csharp
private void InitNewOrder(Order order)
{
    // Set initial balance equal to volume
    order.Balance = order.Volume;

    // Generate transaction ID
    if (order.TransactionId == 0)
        order.TransactionId = TransactionIdGenerator.GetNextId();

    // Set local time
    order.LocalTime = CurrentTime;

    // Set initial state to Pending
    order.ApplyNewState(OrderStates.Pending, this);

    // Register in cache
    _entityCache.AddOrderByRegistrationId(order);

    // Send outgoing message
    SendOutMessage(order.ToMessage());
}
```

---

## Order Cancellation

### CancelOrder Method

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:648`

**Signature**:
```csharp
public void CancelOrder(Order order)
```

**Description**: Cancels an existing active order.

### Cancellation Process

```csharp
public void CancelOrder(Order order)
{
    long transactionId = 0;

    try
    {
        this.AddOrderInfoLog(order, nameof(CancelOrder));

        // Validate order has transaction ID
        CheckOnOld(order);

        // Generate new transaction ID for cancel operation
        transactionId = TransactionIdGenerator.GetNextId();

        // Track cancellation in cache
        _entityCache.AddOrderByCancelationId(order, transactionId);

        // Send cancel message to adapter
        OnCancelOrder(order, transactionId);
    }
    catch (Exception ex)
    {
        if (transactionId == 0)
            transactionId = TransactionIdGenerator.GetNextId();

        SendOrderFailed(order, OrderOperations.Cancel, ex, transactionId);
    }
}
```

### Old Order Validation

```csharp
private static void CheckOnOld(Order order)
{
    if (order == null)
        throw new ArgumentNullException(nameof(order));

    if (order.TransactionId == 0)
        throw new ArgumentException("Order has no TransactionId", nameof(order));
}
```

### Cancel Message Creation

```csharp
protected void OnCancelOrder(Order order, long transactionId)
{
    SendInMessage(order.CreateCancelMessage(GetSecurityId(order.Security), transactionId));
}
```

---

## Order Modification

StockSharp supports two types of order modification:

### 1. EditOrder Method (In-Place Edit)

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:578`

**Signature**:
```csharp
public void EditOrder(Order order, Order changes)
```

**Description**: Edits an order in-place without canceling and re-registering. Not all adapters support this operation.

**Capability Check**:
```csharp
// Check if adapter supports edit operation
public bool? IsOrderEditable(Order order)
    => _entityCache.TryGetAdapter(order)?.IsReplaceCommandEditCurrent;
```

**Process**:
```csharp
public void EditOrder(Order order, Order changes)
{
    try
    {
        this.AddOrderInfoLog(order, nameof(EditOrder));

        // Validate existing order
        CheckOnOld(order);

        // Validate changes
        CheckOnNew(changes);

        // Warn if adapter doesn't support edit
        if (IsOrderEditable(order) != true)
            LogWarning("Order {0} is not editable.", order.TransactionId);

        // Generate transaction ID for edit
        var transactionId = TransactionIdGenerator.GetNextId();

        // Track edition in cache
        _entityCache.AddOrderByEditionId(order, transactionId);

        // Set transaction ID in changes
        changes.TransactionId = transactionId;

        // Send edit message
        OnEditOrder(order, changes);
    }
    catch (Exception ex)
    {
        SendOrderFailed(order, OrderOperations.Edit, ex, changes.TransactionId);
    }
}
```

**Usage Example**:
```csharp
// Create order changes object
var changes = new Order
{
    Price = 105.50m,  // New price
    Volume = 200      // New volume (optional)
};

// Check if editable
if (connector.IsOrderEditable(existingOrder) == true)
{
    connector.EditOrder(existingOrder, changes);
}
```

### 2. ReRegisterOrder Method (Cancel & Replace)

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:609`

**Signature**:
```csharp
public void ReRegisterOrder(Order oldOrder, Order newOrder)
```

**Description**: Cancels an existing order and registers a new one atomically (if supported by adapter).

**Capability Check**:
```csharp
// Check if adapter supports replace operation
public bool? IsOrderReplaceable(Order order)
    => _entityCache.TryGetAdapter(order)?.IsMessageSupported(MessageTypes.OrderReplace);
```

**Process**:
```csharp
public void ReRegisterOrder(Order oldOrder, Order newOrder)
{
    try
    {
        this.AddOrderInfoLog(oldOrder, nameof(ReRegisterOrder));

        // Validate securities match
        if (oldOrder.Security != newOrder.Security)
            throw new ArgumentException("Securities mismatch", nameof(newOrder));

        // Validate old order
        CheckOnOld(oldOrder);

        // Validate new order
        CheckOnNew(newOrder);

        // Warn if adapter doesn't support replace
        if (IsOrderReplaceable(oldOrder) != true)
            LogWarning("Order {0} is not replaceable.", oldOrder.TransactionId);

        // Initialize new order
        InitNewOrder(newOrder);

        // Track cancellation with new order's transaction ID
        _entityCache.AddOrderByCancelationId(oldOrder, newOrder.TransactionId);

        // Send replace message
        OnReRegisterOrder(oldOrder, newOrder);
    }
    catch (Exception ex)
    {
        var transactionId = newOrder.TransactionId;

        if (transactionId == 0 || newOrder.State != OrderStates.None)
            transactionId = TransactionIdGenerator.GetNextId();

        // Send failures for both operations
        SendOrderFailed(oldOrder, OrderOperations.Cancel, ex, transactionId);
        SendOrderFailed(newOrder, OrderOperations.Register, ex, transactionId);
    }
}
```

**Usage Example**:
```csharp
// Create completely new order with different parameters
var newOrder = new Order
{
    Security = oldOrder.Security,
    Portfolio = oldOrder.Portfolio,
    Side = oldOrder.Side,
    Price = 108.00m,     // Different price
    Volume = 300,        // Different volume
    Type = OrderTypes.Limit,
    TimeInForce = TimeInForce.PutInQueue
};

// Check if replaceable
if (connector.IsOrderReplaceable(oldOrder) == true)
{
    connector.ReRegisterOrder(oldOrder, newOrder);
}
else
{
    // Fallback: manual cancel and register
    connector.CancelOrder(oldOrder);
    // Wait for cancellation confirmation before registering new order
}
```

---

## Order Events

### OrderReceived Event

**Description**: Fired when an order update is received from the trading system.

**Signature**:
```csharp
event Action<Subscription, Order> OrderReceived;
```

**Usage**:
```csharp
_connector.OrderReceived += (subscription, order) =>
{
    Console.WriteLine($"Order {order.TransactionId}: State={order.State}, Balance={order.Balance}");

    // Handle different states
    switch (order.State)
    {
        case OrderStates.Pending:
            Console.WriteLine("Order is pending registration...");
            break;

        case OrderStates.Active:
            Console.WriteLine($"Order is active. Balance: {order.Balance}");
            break;

        case OrderStates.Done:
            if (order.Balance == 0)
                Console.WriteLine("Order fully executed");
            else
                Console.WriteLine("Order cancelled");
            break;

        case OrderStates.Failed:
            Console.WriteLine($"Order failed: {order}");
            break;
    }

    // Add to UI grid
    OrderGrid.Orders.TryAdd(order);
};
```

### OrderRegisterFailReceived Event

**Description**: Fired when order registration fails.

**Signature**:
```csharp
event Action<Subscription, OrderFail> OrderRegisterFailReceived;
```

**Usage**:
```csharp
_connector.OrderRegisterFailReceived += (subscription, fail) =>
{
    Console.WriteLine($"Order registration failed:");
    Console.WriteLine($"  TransactionId: {fail.TransactionId}");
    Console.WriteLine($"  Error: {fail.Error.Message}");
    Console.WriteLine($"  Order: {fail.Order}");

    // Display in UI
    OrderGrid.AddRegistrationFail(fail);

    // Log error
    _connector.AddErrorLog($"Failed to register order: {fail.Error}");
};
```

### OrderCancelFailReceived Event

**Description**: Fired when order cancellation fails.

**Signature**:
```csharp
event Action<Subscription, OrderFail> OrderCancelFailReceived;
```

### OrderEditFailReceived Event

**Description**: Fired when order edit fails.

**Signature**:
```csharp
event Action<Subscription, OrderFail> OrderEditFailReceived;
```

---

## Error Handling

### OrderFail Class

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\OrderFail.cs`

**Properties**:
```csharp
public class OrderFail
{
    // The failed order
    public Order Order { get; set; }

    // Error details
    public Exception Error { get; set; }

    // Server time when error occurred
    public DateTimeOffset ServerTime { get; set; }

    // Transaction ID of the operation
    public long TransactionId { get; set; }
}
```

### SendOrderFailed Method

```csharp
private void SendOrderFailed(Order order, OrderOperations operation, Exception error, long originalTransactionId)
{
    var fail = new OrderFail
    {
        Order = order,
        Error = error,
        ServerTime = CurrentTime,
        TransactionId = originalTransactionId,
    };

    _entityCache.AddOrderFailById(fail, operation, originalTransactionId);

    SendOutMessage(fail.ToMessage(originalTransactionId));
}
```

### Error Scenarios

#### 1. Registration Errors

```csharp
try
{
    _connector.RegisterOrder(order);
}
catch (ArgumentNullException ex)
{
    // Required field missing
    Console.WriteLine($"Missing required field: {ex.ParamName}");
}
catch (ArgumentOutOfRangeException ex)
{
    // Invalid value (e.g., negative volume)
    Console.WriteLine($"Invalid value: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Validation error (e.g., price step mismatch)
    Console.WriteLine($"Validation error: {ex.Message}");
}
```

#### 2. Adapter Errors

Captured in `OrderRegisterFailReceived` event:

```csharp
_connector.OrderRegisterFailReceived += (s, fail) =>
{
    // Parse common error types
    if (fail.Error.Message.Contains("Insufficient funds"))
    {
        Console.WriteLine("Not enough money in account");
    }
    else if (fail.Error.Message.Contains("Invalid price"))
    {
        Console.WriteLine("Price outside allowed range");
    }
    else if (fail.Error.Message.Contains("Market closed"))
    {
        Console.WriteLine("Trading session is closed");
    }
    else
    {
        Console.WriteLine($"Order rejected: {fail.Error.Message}");
    }
};
```

---

## Practical Examples

### Example 1: Simple Order Registration

**From**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\03_Orders\MainWindow.xaml.cs:90`

```csharp
private void Buy_Click(object sender, RoutedEventArgs e)
{
    var order = new Order
    {
        Security = SecurityEditor.SelectedSecurity,
        Portfolio = PortfolioEditor.SelectedPortfolio,
        Price = decimal.Parse(TextBoxPrice.Text),
        Volume = 1,
        Side = Sides.Buy,
    };

    _connector.RegisterOrder(order);
}
```

### Example 2: Advanced Order with All Parameters

```csharp
private void RegisterAdvancedOrder()
{
    var order = new Order
    {
        // Required fields
        Security = _security,
        Portfolio = _portfolio,
        Side = Sides.Buy,
        Volume = 100,

        // Type and price
        Type = OrderTypes.Limit,
        Price = 105.50m,

        // Time in force
        TimeInForce = TimeInForce.PutInQueue,  // GTC

        // Optional fields
        Comment = "My first order",
        UserOrderId = "USER-001",

        // Expiry (if not GTC)
        // ExpiryDate = DateTimeOffset.Now.AddHours(4),

        // Iceberg order
        // VisibleVolume = 10,  // Show only 10, total is 100

        // Position effect (for derivatives)
        // PositionEffect = OrderPositionEffects.Open,

        // Leverage (for margin trading)
        // Leverage = 3,
    };

    try
    {
        _connector.RegisterOrder(order);
        Console.WriteLine($"Order registered with TransactionId: {order.TransactionId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to register order: {ex.Message}");
    }
}
```

### Example 3: Conditional Stop-Loss Order

```csharp
private void RegisterStopLoss(decimal stopPrice)
{
    // Get adapter-specific condition type
    var adapter = _connector.Adapter;
    var conditionType = adapter.OrderConditionType;

    if (conditionType == null)
    {
        Console.WriteLine("Adapter doesn't support conditional orders");
        return;
    }

    // Create condition
    var condition = Activator.CreateInstance(conditionType) as OrderCondition;

    // Configure stop-loss (adapter-specific)
    if (condition is IStopLossOrderCondition stopLoss)
    {
        stopLoss.ActivationPrice = stopPrice;
        stopLoss.ClosePositionPrice = null;  // Market order on trigger
    }

    var order = new Order
    {
        Security = _security,
        Portfolio = _portfolio,
        Side = Sides.Sell,
        Volume = 100,
        Type = OrderTypes.Conditional,
        Condition = condition,
    };

    _connector.RegisterOrder(order);
}
```

### Example 4: Complete Order Lifecycle Handling

```csharp
public class OrderManager
{
    private readonly Connector _connector;
    private readonly Dictionary<long, Order> _activeOrders = new();

    public OrderManager(Connector connector)
    {
        _connector = connector;

        // Subscribe to events
        _connector.OrderReceived += OnOrderReceived;
        _connector.OrderRegisterFailReceived += OnOrderRegisterFail;
        _connector.OrderCancelFailReceived += OnOrderCancelFail;
    }

    private void OnOrderReceived(Subscription s, Order order)
    {
        switch (order.State)
        {
            case OrderStates.Pending:
                Console.WriteLine($"[{order.TransactionId}] Order sent to exchange");
                break;

            case OrderStates.Active:
                _activeOrders[order.TransactionId] = order;
                Console.WriteLine($"[{order.TransactionId}] Order active. Balance: {order.Balance}");

                // Check for partial fill
                if (order.Balance < order.Volume)
                {
                    var filled = order.Volume - order.Balance;
                    Console.WriteLine($"[{order.TransactionId}] Partially filled: {filled}/{order.Volume}");
                }
                break;

            case OrderStates.Done:
                _activeOrders.Remove(order.TransactionId);

                if (order.Balance == 0)
                {
                    Console.WriteLine($"[{order.TransactionId}] Order fully executed");
                }
                else
                {
                    Console.WriteLine($"[{order.TransactionId}] Order cancelled. Balance: {order.Balance}");
                }
                break;

            case OrderStates.Failed:
                _activeOrders.Remove(order.TransactionId);
                Console.WriteLine($"[{order.TransactionId}] Order failed");
                break;
        }
    }

    private void OnOrderRegisterFail(Subscription s, OrderFail fail)
    {
        Console.WriteLine($"Registration failed: {fail.Error.Message}");

        // Retry logic (if applicable)
        if (fail.Error.Message.Contains("timeout"))
        {
            Console.WriteLine("Retrying order registration...");
            // Retry with new order object
        }
    }

    private void OnOrderCancelFail(Subscription s, OrderFail fail)
    {
        Console.WriteLine($"Cancellation failed: {fail.Error.Message}");

        // Order might already be executed
        // Wait for order updates to get final state
    }

    public void CancelAllOrders()
    {
        foreach (var order in _activeOrders.Values.ToList())
        {
            _connector.CancelOrder(order);
        }
    }
}
```

### Example 5: Mass Order Cancellation

```csharp
// Cancel all orders
_connector.CancelOrders();

// Cancel all stop orders
_connector.CancelOrders(isStopOrder: true);

// Cancel all orders for specific security
_connector.CancelOrders(security: _security);

// Cancel all buy orders for specific portfolio
_connector.CancelOrders(
    portfolio: _portfolio,
    direction: Sides.Buy
);

// Cancel all orders on specific board
_connector.CancelOrders(board: _board);

// Complex filter
_connector.CancelOrders(
    isStopOrder: false,
    portfolio: _portfolio,
    direction: Sides.Sell,
    security: _security,
    securityType: SecurityTypes.Stock
);
```

---

## Best Practices

### 1. Always Subscribe to Events Before Connecting

```csharp
// Subscribe first
_connector.OrderReceived += OnOrderReceived;
_connector.OrderRegisterFailReceived += OnOrderRegisterFail;

// Then connect
_connector.Connect();
```

### 2. Validate Orders Before Registration

```csharp
private bool ValidateOrder(Order order)
{
    if (order.Security == null || order.Portfolio == null)
    {
        Console.WriteLine("Security and Portfolio are required");
        return false;
    }

    if (order.Volume <= 0)
    {
        Console.WriteLine("Volume must be positive");
        return false;
    }

    if (order.Type == OrderTypes.Limit && order.Price <= 0)
    {
        Console.WriteLine("Limit orders require price");
        return false;
    }

    return true;
}
```

### 3. Track Orders by TransactionId

```csharp
private readonly Dictionary<long, Order> _ordersByTransactionId = new();

private void RegisterAndTrack(Order order)
{
    _connector.RegisterOrder(order);
    _ordersByTransactionId[order.TransactionId] = order;
}
```

### 4. Handle Latency Tracking

```csharp
_connector.OrderReceived += (s, order) =>
{
    if (order.LatencyRegistration.HasValue)
    {
        Console.WriteLine($"Registration latency: {order.LatencyRegistration.Value.TotalMilliseconds}ms");
    }

    if (order.LatencyCancellation.HasValue)
    {
        Console.WriteLine($"Cancellation latency: {order.LatencyCancellation.Value.TotalMilliseconds}ms");
    }
};
```

### 5. Enable Step Validation for Production

```csharp
// Enable in production to prevent exchange rejections
_connector.CheckSteps = true;
```

---

## Related Specifications

- [Order Types](./order-types.md) - Detailed order type documentation
- [Position Management](./position-management.md) - Position tracking
- [Transaction Tracking](./transaction-tracking.md) - Transaction lifecycle
- [Portfolio Management](./portfolio-management.md) - Account management

---

## References

- **Source Files**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Order.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\OrderFail.cs`

- **Sample Projects**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\03_Orders`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\05_Chart\02_ActiveOrders`
