# Position Management Specification

## Overview

This specification covers position tracking and management in StockSharp, including position creation, updates, P&L calculation, and event handling. Positions represent the current holdings for a security in a specific portfolio.

## Table of Contents

1. [Position Class](#position-class)
2. [Getting Positions](#getting-positions)
3. [Position Properties](#position-properties)
4. [Position Events](#position-events)
5. [P&L Tracking](#pnl-tracking)
6. [Position Storage](#position-storage)
7. [Practical Examples](#practical-examples)

---

## Position Class

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Position.cs`

### Overview

```csharp
public class Position : NotifiableObject, ILocalTimeMessage, IServerTimeMessage
{
    // Position identification
    public Portfolio Portfolio { get; set; }
    public Security Security { get; set; }
    public string StrategyId { get; set; }
    public Sides? Side { get; set; }

    // Position values
    public decimal? BeginValue { get; set; }      // Starting position
    public decimal? CurrentValue { get; set; }    // Current position
    public decimal? BlockedValue { get; set; }    // Blocked in orders

    // Price tracking
    public decimal? CurrentPrice { get; set; }    // Last price
    public decimal? AveragePrice { get; set; }    // Average entry price

    // P&L
    public decimal? UnrealizedPnL { get; set; }   // Open P&L
    public decimal? RealizedPnL { get; set; }     // Closed P&L

    // Additional fields
    public decimal? VariationMargin { get; set; }
    public decimal? Commission { get; set; }
    public decimal? LiquidationPrice { get; set; }
    public decimal? Leverage { get; set; }

    // Timing
    public DateTimeOffset LastChangeTime { get; set; }
    public DateTimeOffset LocalTime { get; set; }
}
```

---

## Getting Positions

### GetPosition Method

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs:498`

**Signature**:
```csharp
public Position GetPosition(
    Portfolio portfolio,
    Security security,
    string strategyId = "",
    Sides? side = null,
    string clientCode = "",
    string depoName = "",
    TPlusLimits? limitType = null)
```

**Description**: Gets or creates a position for the specified parameters. If the position doesn't exist, it will be created automatically.

### Basic Usage

```csharp
// Get position for security in portfolio
var position = _connector.GetPosition(_portfolio, _security);

// Current position value
decimal currentQty = position.CurrentValue ?? 0;

Console.WriteLine($"Position: {currentQty}");
Console.WriteLine($"Average Price: {position.AveragePrice}");
Console.WriteLine($"Unrealized P&L: {position.UnrealizedPnL}");
```

### Strategy-Specific Positions

```csharp
// Get position for specific strategy
var strategyPosition = _connector.GetPosition(
    portfolio: _portfolio,
    security: _security,
    strategyId: "MyStrategy"
);

Console.WriteLine($"Strategy position: {strategyPosition.CurrentValue}");
```

### Side-Specific Positions

For exchanges that maintain separate long/short positions:

```csharp
// Get long position
var longPosition = _connector.GetPosition(
    portfolio: _portfolio,
    security: _security,
    side: Sides.Buy
);

// Get short position
var shortPosition = _connector.GetPosition(
    portfolio: _portfolio,
    security: _security,
    side: Sides.Sell
);

Console.WriteLine($"Long: {longPosition.CurrentValue}");
Console.WriteLine($"Short: {shortPosition.CurrentValue}");
```

### All Positions

```csharp
// Get all positions
foreach (var position in _connector.Positions)
{
    Console.WriteLine($"{position.Portfolio.Name} - {position.Security.Code}: {position.CurrentValue}");
}
```

---

## Position Properties

### Position Values

#### BeginValue

```csharp
/// <summary>
/// Position size at the beginning of the trading session
/// </summary>
public decimal? BeginValue { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

// Check if position existed at session start
if (position.BeginValue.HasValue && position.BeginValue.Value != 0)
{
    Console.WriteLine($"Started session with: {position.BeginValue}");
}
```

#### CurrentValue

```csharp
/// <summary>
/// Current position size
/// Positive = long, Negative = short, Zero = flat
/// </summary>
public decimal? CurrentValue { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);
decimal qty = position.CurrentValue ?? 0;

if (qty > 0)
    Console.WriteLine($"Long {qty} shares");
else if (qty < 0)
    Console.WriteLine($"Short {Math.Abs(qty)} shares");
else
    Console.WriteLine("No position");
```

#### BlockedValue

```csharp
/// <summary>
/// Position size registered for active orders
/// Amount tied up in pending orders
/// </summary>
public decimal? BlockedValue { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

decimal available = position.CurrentValue ?? 0;
decimal blocked = position.BlockedValue ?? 0;
decimal free = available - blocked;

Console.WriteLine($"Total: {available}, Blocked: {blocked}, Free: {free}");
```

### Price Properties

#### CurrentPrice

```csharp
/// <summary>
/// Last known market price for the security
/// </summary>
public decimal? CurrentPrice { get; set; }
```

#### AveragePrice

```csharp
/// <summary>
/// Average entry price of the position
/// Calculated from all trades
/// </summary>
public decimal? AveragePrice { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

if (position.AveragePrice.HasValue)
{
    decimal entryPrice = position.AveragePrice.Value;
    decimal currentPrice = position.CurrentPrice ?? 0;
    decimal priceDiff = currentPrice - entryPrice;

    Console.WriteLine($"Entry: {entryPrice}, Current: {currentPrice}, Diff: {priceDiff}");
}
```

### P&L Properties

#### UnrealizedPnL

```csharp
/// <summary>
/// Unrealized profit/loss on current open position
/// Mark-to-market P&L
/// </summary>
public decimal? UnrealizedPnL { get; set; }
```

#### RealizedPnL

```csharp
/// <summary>
/// Realized profit/loss from closed trades
/// Actual P&L from executed trades
/// </summary>
public decimal? RealizedPnL { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

decimal unrealizedPnL = position.UnrealizedPnL ?? 0;
decimal realizedPnL = position.RealizedPnL ?? 0;
decimal totalPnL = unrealizedPnL + realizedPnL;

Console.WriteLine($"Unrealized P&L: {unrealizedPnL:C}");
Console.WriteLine($"Realized P&L: {realizedPnL:C}");
Console.WriteLine($"Total P&L: {totalPnL:C}");
```

### Additional Properties

#### VariationMargin

```csharp
/// <summary>
/// Variation margin for futures/derivatives
/// Daily settlement amount
/// </summary>
public decimal? VariationMargin { get; set; }
```

#### Commission

```csharp
/// <summary>
/// Total commission paid on the position
/// </summary>
public decimal? Commission { get; set; }
```

#### LiquidationPrice

```csharp
/// <summary>
/// Price at which position will be force-closed (margin trading)
/// </summary>
public decimal? LiquidationPrice { get; set; }
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

if (position.LiquidationPrice.HasValue)
{
    decimal currentPrice = position.CurrentPrice ?? 0;
    decimal liquidationPrice = position.LiquidationPrice.Value;
    decimal buffer = Math.Abs(currentPrice - liquidationPrice);
    decimal bufferPercent = (buffer / currentPrice) * 100;

    Console.WriteLine($"Liquidation price: {liquidationPrice}");
    Console.WriteLine($"Current price: {currentPrice}");
    Console.WriteLine($"Buffer: {buffer:F2} ({bufferPercent:F2}%)");

    if (bufferPercent < 5)
    {
        Console.WriteLine("WARNING: Close to liquidation!");
    }
}
```

#### Leverage

```csharp
/// <summary>
/// Margin leverage used for the position
/// </summary>
public decimal? Leverage { get; set; }
```

---

## Position Events

### PositionReceived Event

**Signature**:
```csharp
event Action<Subscription, Position> PositionReceived;
```

**Description**: Fired when a position update is received from the broker.

**Usage**:
```csharp
_connector.PositionReceived += (subscription, position) =>
{
    Console.WriteLine($"Position update: {position.Security.Code}");
    Console.WriteLine($"  Portfolio: {position.Portfolio.Name}");
    Console.WriteLine($"  Current: {position.CurrentValue}");
    Console.WriteLine($"  Average Price: {position.AveragePrice}");
    Console.WriteLine($"  Unrealized P&L: {position.UnrealizedPnL}");

    // Update UI
    UpdatePositionGrid(position);
};
```

### NewPosition Event

**Signature**:
```csharp
event Action<Position> NewPosition;
```

**Description**: Fired when a new position is created (first time seen).

**Usage**:
```csharp
_connector.NewPosition += (position) =>
{
    Console.WriteLine($"New position opened: {position.Security.Code}");
    Console.WriteLine($"  Initial value: {position.CurrentValue}");

    // Add to tracking
    _openPositions.Add(position);
};
```

### Example: Complete Position Tracking

```csharp
public class PositionTracker
{
    private readonly Connector _connector;
    private readonly Dictionary<string, Position> _positions = new();

    public PositionTracker(Connector connector)
    {
        _connector = connector;

        _connector.NewPosition += OnNewPosition;
        _connector.PositionReceived += OnPositionReceived;
    }

    private void OnNewPosition(Position position)
    {
        var key = GetPositionKey(position);
        _positions[key] = position;

        Console.WriteLine($"[NEW] {position.Security.Code}: {position.CurrentValue}");
    }

    private void OnPositionReceived(Subscription s, Position position)
    {
        var key = GetPositionKey(position);
        var oldPosition = _positions.GetValueOrDefault(key);

        if (oldPosition != null)
        {
            // Check for changes
            if (oldPosition.CurrentValue != position.CurrentValue)
            {
                Console.WriteLine($"[CHG] {position.Security.Code}: " +
                    $"{oldPosition.CurrentValue} -> {position.CurrentValue}");
            }
        }

        _positions[key] = position;

        // Check if position closed
        if (position.CurrentValue == 0 && oldPosition?.CurrentValue != 0)
        {
            Console.WriteLine($"[CLOSED] {position.Security.Code}");
            Console.WriteLine($"  Realized P&L: {position.RealizedPnL}");
        }
    }

    private string GetPositionKey(Position position)
    {
        return $"{position.Portfolio.Name}_{position.Security.Code}_{position.StrategyId}";
    }

    public Position GetPosition(Security security)
    {
        return _connector.GetPosition(_connector.Portfolios.First(), security);
    }

    public IEnumerable<Position> GetAllPositions()
    {
        return _positions.Values.Where(p => p.CurrentValue != 0);
    }
}
```

---

## P&L Tracking

### Real-Time P&L Calculation

```csharp
public class PnLCalculator
{
    public static decimal CalculateUnrealizedPnL(Position position)
    {
        if (!position.CurrentValue.HasValue || position.CurrentValue == 0)
            return 0;

        if (!position.AveragePrice.HasValue || !position.CurrentPrice.HasValue)
            return 0;

        decimal qty = position.CurrentValue.Value;
        decimal entryPrice = position.AveragePrice.Value;
        decimal currentPrice = position.CurrentPrice.Value;

        // P&L = (Current Price - Entry Price) * Quantity
        decimal pnl = (currentPrice - entryPrice) * qty;

        return pnl;
    }

    public static decimal CalculatePnLPercent(Position position)
    {
        if (!position.AveragePrice.HasValue || position.AveragePrice == 0)
            return 0;

        decimal unrealizedPnL = CalculateUnrealizedPnL(position);
        decimal investment = position.AveragePrice.Value * Math.Abs(position.CurrentValue ?? 0);

        if (investment == 0)
            return 0;

        return (unrealizedPnL / investment) * 100;
    }
}
```

**Usage**:
```csharp
var position = _connector.GetPosition(_portfolio, _security);

decimal pnl = PnLCalculator.CalculateUnrealizedPnL(position);
decimal pnlPercent = PnLCalculator.CalculatePnLPercent(position);

Console.WriteLine($"Unrealized P&L: {pnl:C} ({pnlPercent:F2}%)");
```

### P&L Manager

StockSharp includes a built-in P&L manager:

```csharp
// Configure P&L manager
_connector.PnLManager = new PnLManager();

// P&L will be automatically calculated and updated in positions
_connector.PositionReceived += (s, position) =>
{
    Console.WriteLine($"Position P&L: {position.UnrealizedPnL}");
};
```

---

## Position Storage

### IPositionStorage Interface

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Storages\IPositionStorage.cs`

```csharp
public interface IPositionStorage
{
    // Get or create position
    Position GetOrCreatePosition(
        Portfolio portfolio,
        Security security,
        string strategyId,
        Sides? side,
        string clientCode,
        string depoName,
        TPlusLimits? limitType,
        Func<Portfolio, Security, string, Sides?, string, string, TPlusLimits?, Position> createPosition,
        out bool isNew);

    // Get all positions
    IEnumerable<Position> Positions { get; }

    // Save/Load
    void Save(Position position);
    void Load(Position position);
}
```

### In-Memory Storage

```csharp
// Default storage (in-memory)
var connector = new Connector();
// Uses InMemoryPositionStorage internally
```

### Custom Storage

```csharp
// Custom position storage
public class DatabasePositionStorage : IPositionStorage
{
    private readonly Database _db;

    public Position GetOrCreatePosition(
        Portfolio portfolio,
        Security security,
        string strategyId,
        Sides? side,
        string clientCode,
        string depoName,
        TPlusLimits? limitType,
        Func<Portfolio, Security, string, Sides?, string, string, TPlusLimits?, Position> createPosition,
        out bool isNew)
    {
        // Load from database
        var position = _db.LoadPosition(portfolio, security, strategyId);

        if (position == null)
        {
            position = createPosition(portfolio, security, strategyId, side, clientCode, depoName, limitType);
            isNew = true;
        }
        else
        {
            isNew = false;
        }

        return position;
    }

    // Implement other methods...
}

// Use custom storage
var connector = new Connector(
    securityStorage: securityStorage,
    positionStorage: new DatabasePositionStorage(),
    exchangeInfoProvider: exchangeInfoProvider
);
```

---

## Practical Examples

### Example 1: Basic Position Tracking

**From**: `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\03_Orders\MainWindow.xaml.cs:143`

```csharp
_connector.PositionReceived += (subscription, position) =>
{
    Dispatcher.Invoke(() =>
    {
        Console.WriteLine($"Position: {position.Security.Code} = {position.CurrentValue}");

        // Update UI
        PositionGrid.Positions.TryAdd(position);
    });
};
```

### Example 2: Position Monitor

```csharp
public class PositionMonitor
{
    private readonly Connector _connector;
    private readonly Portfolio _portfolio;

    public PositionMonitor(Connector connector, Portfolio portfolio)
    {
        _connector = connector;
        _portfolio = portfolio;

        _connector.PositionReceived += OnPositionReceived;
    }

    private void OnPositionReceived(Subscription s, Position position)
    {
        // Only monitor our portfolio
        if (position.Portfolio != _portfolio)
            return;

        // Print position details
        PrintPosition(position);

        // Check for risk limits
        CheckRiskLimits(position);
    }

    private void PrintPosition(Position position)
    {
        Console.WriteLine($"\n{position.Security.Code} Position:");
        Console.WriteLine($"  Current: {position.CurrentValue ?? 0}");
        Console.WriteLine($"  Avg Price: {position.AveragePrice ?? 0:F2}");
        Console.WriteLine($"  Current Price: {position.CurrentPrice ?? 0:F2}");
        Console.WriteLine($"  Unrealized P&L: {position.UnrealizedPnL ?? 0:C}");
        Console.WriteLine($"  Realized P&L: {position.RealizedPnL ?? 0:C}");

        if (position.BlockedValue.HasValue && position.BlockedValue > 0)
        {
            Console.WriteLine($"  Blocked: {position.BlockedValue}");
        }
    }

    private void CheckRiskLimits(Position position)
    {
        // Check position size limit
        const decimal MaxPosition = 1000;
        if (Math.Abs(position.CurrentValue ?? 0) > MaxPosition)
        {
            Console.WriteLine($"WARNING: Position size {position.CurrentValue} exceeds limit {MaxPosition}");
        }

        // Check P&L limit
        const decimal MaxLoss = -10000;
        if ((position.UnrealizedPnL ?? 0) < MaxLoss)
        {
            Console.WriteLine($"WARNING: Unrealized loss {position.UnrealizedPnL:C} exceeds limit {MaxLoss:C}");
        }
    }

    public void PrintAllPositions()
    {
        Console.WriteLine("\n=== All Positions ===");

        foreach (var position in _connector.Positions)
        {
            if (position.Portfolio != _portfolio)
                continue;

            if (position.CurrentValue == 0)
                continue;

            PrintPosition(position);
        }
    }
}
```

### Example 3: Position Risk Manager

```csharp
public class PositionRiskManager
{
    private readonly Connector _connector;
    private readonly Portfolio _portfolio;
    private readonly decimal _maxPositionSize;
    private readonly decimal _maxLossPerPosition;

    public PositionRiskManager(
        Connector connector,
        Portfolio portfolio,
        decimal maxPositionSize,
        decimal maxLossPerPosition)
    {
        _connector = connector;
        _portfolio = portfolio;
        _maxPositionSize = maxPositionSize;
        _maxLossPerPosition = maxLossPerPosition;

        _connector.PositionReceived += CheckPosition;
    }

    private void CheckPosition(Subscription s, Position position)
    {
        if (position.Portfolio != _portfolio)
            return;

        // Check position size
        if (Math.Abs(position.CurrentValue ?? 0) > _maxPositionSize)
        {
            Console.WriteLine($"Position size limit exceeded for {position.Security.Code}");
            ReducePosition(position);
        }

        // Check loss limit
        if ((position.UnrealizedPnL ?? 0) < -_maxLossPerPosition)
        {
            Console.WriteLine($"Loss limit exceeded for {position.Security.Code}");
            ClosePosition(position);
        }
    }

    private void ReducePosition(Position position)
    {
        decimal currentSize = position.CurrentValue ?? 0;
        decimal excess = Math.Abs(currentSize) - _maxPositionSize;

        if (excess <= 0)
            return;

        // Create order to reduce position
        var order = new Order
        {
            Security = position.Security,
            Portfolio = position.Portfolio,
            Side = currentSize > 0 ? Sides.Sell : Sides.Buy,
            Volume = excess,
            Type = OrderTypes.Market,
            Comment = "Risk management: Reduce position"
        };

        _connector.RegisterOrder(order);
    }

    private void ClosePosition(Position position)
    {
        decimal currentSize = position.CurrentValue ?? 0;

        if (currentSize == 0)
            return;

        // Create order to close position
        var order = new Order
        {
            Security = position.Security,
            Portfolio = position.Portfolio,
            Side = currentSize > 0 ? Sides.Sell : Sides.Buy,
            Volume = Math.Abs(currentSize),
            Type = OrderTypes.Market,
            Comment = "Risk management: Close position"
        };

        _connector.RegisterOrder(order);
    }
}
```

### Example 4: Portfolio P&L Summary

```csharp
public class PortfolioPnLSummary
{
    private readonly Connector _connector;
    private readonly Portfolio _portfolio;

    public PortfolioPnLSummary(Connector connector, Portfolio portfolio)
    {
        _connector = connector;
        _portfolio = portfolio;
    }

    public void PrintSummary()
    {
        decimal totalUnrealizedPnL = 0;
        decimal totalRealizedPnL = 0;
        decimal totalCommission = 0;
        int positionCount = 0;

        Console.WriteLine($"\n=== Portfolio P&L Summary: {_portfolio.Name} ===");

        foreach (var position in _connector.Positions)
        {
            if (position.Portfolio != _portfolio)
                continue;

            if (position.CurrentValue == 0)
                continue;

            positionCount++;
            totalUnrealizedPnL += position.UnrealizedPnL ?? 0;
            totalRealizedPnL += position.RealizedPnL ?? 0;
            totalCommission += position.Commission ?? 0;

            Console.WriteLine($"{position.Security.Code}:");
            Console.WriteLine($"  Qty: {position.CurrentValue}");
            Console.WriteLine($"  Unrealized: {position.UnrealizedPnL:C}");
            Console.WriteLine($"  Realized: {position.RealizedPnL:C}");
        }

        Console.WriteLine($"\nTotal Positions: {positionCount}");
        Console.WriteLine($"Total Unrealized P&L: {totalUnrealizedPnL:C}");
        Console.WriteLine($"Total Realized P&L: {totalRealizedPnL:C}");
        Console.WriteLine($"Total Commission: {totalCommission:C}");
        Console.WriteLine($"Net P&L: {(totalUnrealizedPnL + totalRealizedPnL - totalCommission):C}");
    }
}
```

### Example 5: Strategy Positions

```csharp
public class StrategyPositionTracker
{
    private readonly Connector _connector;
    private readonly string _strategyId;

    public StrategyPositionTracker(Connector connector, string strategyId)
    {
        _connector = connector;
        _strategyId = strategyId;
    }

    public Position GetStrategyPosition(Portfolio portfolio, Security security)
    {
        return _connector.GetPosition(
            portfolio: portfolio,
            security: security,
            strategyId: _strategyId
        );
    }

    public IEnumerable<Position> GetAllStrategyPositions()
    {
        return _connector.Positions
            .Where(p => p.StrategyId == _strategyId && p.CurrentValue != 0);
    }

    public void PrintStrategyPositions()
    {
        Console.WriteLine($"\n=== Strategy Positions: {_strategyId} ===");

        foreach (var position in GetAllStrategyPositions())
        {
            Console.WriteLine($"{position.Security.Code}:");
            Console.WriteLine($"  Portfolio: {position.Portfolio.Name}");
            Console.WriteLine($"  Position: {position.CurrentValue}");
            Console.WriteLine($"  P&L: {position.UnrealizedPnL:C}");
        }
    }
}
```

---

## Best Practices

### 1. Always Check for Null Values

```csharp
var position = _connector.GetPosition(_portfolio, _security);

// Safe access
decimal qty = position.CurrentValue ?? 0;
decimal avgPrice = position.AveragePrice ?? 0;
decimal pnl = position.UnrealizedPnL ?? 0;
```

### 2. Subscribe to Events Before Connecting

```csharp
// Subscribe first
_connector.NewPosition += OnNewPosition;
_connector.PositionReceived += OnPositionReceived;

// Then connect
_connector.Connect();
```

### 3. Handle Position Changes Incrementally

```csharp
_connector.PositionReceived += (s, position) =>
{
    // Position object is updated incrementally
    // Don't assume full snapshot on each update
    UpdatePositionDisplay(position);
};
```

### 4. Use Strategy IDs for Multi-Strategy Systems

```csharp
// Separate positions per strategy
var strategy1Pos = _connector.GetPosition(_portfolio, _security, strategyId: "Strategy1");
var strategy2Pos = _connector.GetPosition(_portfolio, _security, strategyId: "Strategy2");
```

---

## Related Specifications

- [Order Management](./order-management.md) - Order operations
- [Portfolio Management](./portfolio-management.md) - Account management
- [Transaction Tracking](./transaction-tracking.md) - Order-to-position flow

---

## References

- **Source Files**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Position.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Connector.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Algo\Storages\IPositionStorage.cs`

- **Sample Projects**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\03_Orders`
