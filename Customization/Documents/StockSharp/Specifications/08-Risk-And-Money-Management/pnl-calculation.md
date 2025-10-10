# Profit & Loss (PnL) Calculation Specification

## Overview

The PnL (Profit and Loss) calculation system in StockSharp provides comprehensive tracking of trading profitability in real-time. The system distinguishes between realized PnL (from closed positions) and unrealized PnL (from open positions), offering accurate performance metrics essential for trading strategies.

## Core Architecture

### IPnLManager Interface

```csharp
public interface IPnLManager : IPersistable
{
    decimal RealizedPnL { get; }
    decimal UnrealizedPnL { get; }

    void Reset();
    void UpdateSecurity(Level1ChangeMessage l1Msg);
    PnLInfo ProcessMessage(Message message,
        ICollection<PortfolioPnLManager> changedPortfolios = null);
}
```

**Key Components:**
- `RealizedPnL`: Profit/loss from closed positions (locked in)
- `UnrealizedPnL`: Profit/loss from open positions (mark-to-market)
- `ProcessMessage`: Updates PnL based on incoming messages
- `UpdateSecurity`: Updates instrument specifications for accurate calculations

### PnLManager Implementation

```csharp
public class PnLManager : IPnLManager
{
    private readonly CachedSynchronizedDictionary<string, PortfolioPnLManager>
        _managersByPf = new(StringComparer.InvariantCultureIgnoreCase);

    private readonly Dictionary<long, PortfolioPnLManager> _managersByTransId = [];
    private readonly Dictionary<long, PortfolioPnLManager> _managersByOrderId = [];
    private readonly Dictionary<SecurityId, Level1ChangeMessage> _secLevel1 = [];

    public decimal RealizedPnL { get; private set; }

    public decimal UnrealizedPnL =>
        _managersByPf.CachedValues.Sum(m => m.UnrealizedPnL);
}
```

**Architecture:**
- Portfolio-based organization
- Security specifications caching
- Order-to-portfolio mapping
- Thread-safe operations

## PnL Calculation Methods

### 1. Realized PnL (Closed Positions)

Realized PnL is calculated when a position is closed (fully or partially).

**Formula:**
```
RealizedPnL = (Exit Price - Entry Price) × Volume × Multiplier × Side Factor

Where:
- Side Factor: +1 for long position, -1 for short position
- Multiplier: StepPrice / PriceStep × Leverage × LotMultiplier
```

**Example - Long Position:**
```csharp
// Buy 10 contracts @ 100
// Sell 10 contracts @ 105

Entry Price: 100
Exit Price: 105
Volume: 10
Side: Long (factor = -1)
Multiplier: 1

PnL = (100 - 105) × 10 × 1 × (-1) = 50
```

**Example - Short Position:**
```csharp
// Sell 10 contracts @ 105
// Buy 10 contracts @ 100

Entry Price: 105
Exit Price: 100
Volume: 10
Side: Short (factor = +1)
Multiplier: 1

PnL = (105 - 100) × 10 × 1 × (+1) = 50
```

### 2. Unrealized PnL (Open Positions)

Unrealized PnL is calculated based on current market price vs. entry price.

**Formula:**
```
UnrealizedPnL = Sum of all open positions:
    (Entry Price - Current Market Price) × Volume × Multiplier × Side Factor
```

**Market Price Selection:**
- For long positions: Use BidPrice (can sell at bid)
- For short positions: Use AskPrice (need to buy at ask)
- Fallback: LastTradePrice if bid/ask not available

**Example:**
```csharp
// Open Position: Long 10 contracts @ 100
// Current Bid: 103

Entry Price: 100
Market Price: 103
Volume: 10
Side: Long (factor = -1)

UnrealizedPnL = (100 - 103) × 10 × (-1) = 30
```

## PnLInfo Class

```csharp
public class PnLInfo
{
    public DateTimeOffset ServerTime { get; }
    public decimal ClosedVolume { get; }
    public decimal PnL { get; }

    public PnLInfo(DateTimeOffset serverTime,
                   decimal closedVolume,
                   decimal pnL)
    {
        ServerTime = serverTime;
        ClosedVolume = closedVolume;
        PnL = pnL;
    }
}
```

**Properties:**
- `ServerTime`: Timestamp of the trade
- `ClosedVolume`: Volume that closed existing position
- `PnL`: Realized profit/loss from this trade

**Use Cases:**
- Track per-trade profitability
- Calculate average profit per trade
- Analyze trade quality
- Detect duplicate trade processing

## Position Tracking Algorithm

### FIFO (First-In-First-Out)

StockSharp uses FIFO for position tracking:

```csharp
public class PnLQueue
{
    private Sides _openedPosSide;
    private readonly SynchronizedStack<RefPair<decimal, decimal>> _openedTrades = [];

    public PnLInfo Process(ExecutionMessage trade)
    {
        var closedVolume = 0m;
        var pnl = 0m;
        var volume = trade.SafeGetVolume();
        var price = trade.GetTradePrice();

        // Close existing positions (FIFO)
        if (_openedTrades.Count > 0 && _openedPosSide != trade.Side)
        {
            while (volume > 0 && _openedTrades.Count > 0)
            {
                var currTrade = _openedTrades.Peek();
                var diff = Math.Min(currTrade.Second, volume);

                closedVolume += diff;
                pnl += GetPnL(currTrade.First, diff,
                              _openedPosSide, price);

                volume -= diff;
                currTrade.Second -= diff;

                if (currTrade.Second == 0)
                    _openedTrades.Pop();
            }
        }

        // Open new position with remaining volume
        if (volume > 0)
        {
            _openedPosSide = trade.Side;
            _openedTrades.Push(RefTuple.Create(price, volume));
        }

        return new PnLInfo(trade.ServerTime, closedVolume,
                          pnl * _multiplier);
    }
}
```

### Example: FIFO Calculation

```csharp
// Trade Sequence:
// 1. Buy 5 @ 100
// 2. Buy 3 @ 102
// 3. Sell 6 @ 105

// Step 1: Open position stack
// [(100, 5)]

// Step 2: Open position stack
// [(100, 5), (102, 3)]

// Step 3: Close positions (FIFO)
// Close 5 from (100, 5): PnL = (100 - 105) × 5 × (-1) = 25
// Close 1 from (102, 3): PnL = (102 - 105) × 1 × (-1) = 3
// Remaining open: [(102, 2)]

// Total Realized PnL: 25 + 3 = 28
// Unrealized PnL (if bid = 104): (102 - 104) × 2 × (-1) = 4
```

## Average Price Calculation

### Position Average Price

```csharp
public decimal GetAveragePrice()
{
    if (_openedTrades.Count == 0)
        return 0;

    var totalCost = 0m;
    var totalVolume = 0m;

    foreach (var trade in _openedTrades)
    {
        totalCost += trade.First * trade.Second;
        totalVolume += trade.Second;
    }

    return totalVolume == 0 ? 0 : totalCost / totalVolume;
}
```

**Example:**
```csharp
// Open Positions:
// Buy 10 @ 100 = 1000
// Buy 5 @ 110 = 550
// Total: 15 @ ? = 1550

// Average Price = 1550 / 15 = 103.33
```

## Multiplier Calculation

The multiplier converts price differences to actual profit/loss values:

```csharp
private void UpdateMultiplier()
{
    var stepPrice = StepPrice;

    _multiplier = (stepPrice == null ? 1 : stepPrice.Value / PriceStep)
                  × Leverage
                  × LotMultiplier;
}
```

**Components:**

### PriceStep
Minimum price increment for the instrument.
```csharp
// Example: ES futures
PriceStep = 0.25  // Quarter point
```

### StepPrice
Value of one price step.
```csharp
// Example: ES futures
StepPrice = 12.50  // Each 0.25 point = $12.50
```

### Leverage
Leverage multiplier (default = 1 for no leverage).
```csharp
// Example: Crypto with 10x leverage
Leverage = 10
```

### LotMultiplier
Contract size multiplier (default = 1).
```csharp
// Example: Standard lot = 100,000 units
LotMultiplier = 100000
```

### Full Multiplier Example

```csharp
// ES Futures Example:
PriceStep = 0.25
StepPrice = 12.50
Leverage = 1
LotMultiplier = 1

Multiplier = (12.50 / 0.25) × 1 × 1 = 50

// Price difference of 1 point = $50
// Buy @ 4000, Sell @ 4001
// PnL = (4000 - 4001) × 1 × 50 × (-1) = $50
```

## Data Source Configuration

### PnLManager Data Sources

```csharp
public class PnLManager : IPnLManager
{
    // Configuration flags
    public bool UseTick { get; set; } = true;
    public bool UseOrderLog { get; set; }
    public bool UseOrderBook { get; set; }
    public bool UseLevel1 { get; set; }
    public bool UseCandles { get; set; } = true;
}
```

**Data Source Priority:**
1. **OrderBook (Quotes)**: Most accurate for unrealized PnL
2. **Level1**: Good for current prices
3. **Ticks**: Real-time trade prices
4. **Candles**: Lower frequency but sufficient for many strategies
5. **OrderLog**: Detailed market data

**Configuration Example:**
```csharp
var pnlManager = new PnLManager
{
    UseTick = true,          // Track tick prices
    UseOrderBook = true,     // Use bid/ask for unrealized PnL
    UseLevel1 = true,        // Update on Level1 changes
    UseCandles = true,       // Update on candle close
    UseOrderLog = false      // Skip order log (performance)
};
```

## Integration with Strategy

### Basic Setup

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // PnLManager is already initialized by Strategy base class
        // Default configuration: UseOrderBook = true, UseCandles = true

        base.OnStarted(time);
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        // PnL is automatically calculated
        var realizedPnL = PnLManager.RealizedPnL;
        var unrealizedPnL = PnLManager.UnrealizedPnL;
        var totalPnL = realizedPnL + unrealizedPnL;

        this.AddInfoLog($"Trade PnL: {trade.PnL}, Total: {totalPnL}");

        base.OnNewMyTrade(trade);
    }
}
```

### Advanced Configuration

```csharp
public class AdvancedStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        // Custom PnL manager with specific configuration
        PnLManager = new PnLManager
        {
            UseTick = true,
            UseOrderBook = true,
            UseLevel1 = false,
            UseCandles = false,
            UseOrderLog = false
        };

        // Subscribe to PnL changes
        PnLChanged += OnPnLChanged;

        base.OnStarted(time);
    }

    private void OnPnLChanged()
    {
        var pnl = PnL;

        // Check daily profit target
        if (pnl >= 1000)
        {
            this.AddInfoLog("Daily profit target reached!");
            ClosePosition();
        }

        // Check stop loss
        if (pnl <= -500)
        {
            this.AddWarningLog("Stop loss hit!");
            Stop();
        }
    }
}
```

## Portfolio-Level PnL

### PortfolioPnLManager

```csharp
public class PortfolioPnLManager : IPnLManager
{
    private readonly Dictionary<SecurityId, PnLQueue> _securityPnLs = [];

    public string PortfolioName { get; }
    public decimal RealizedPnL { get; private set; }
    public decimal UnrealizedPnL =>
        _securityPnLs.Values.Sum(q => q.UnrealizedPnL);

    public bool ProcessMyTrade(ExecutionMessage trade, out PnLInfo info)
    {
        var queue = _securityPnLs.SafeAdd(trade.SecurityId, CreateQueue);

        info = queue.Process(trade);
        RealizedPnL += info.PnL;

        return true;
    }
}
```

**Features:**
- Per-portfolio tracking
- Multiple securities support
- Aggregated PnL calculation
- Security-specific queues

### Multi-Portfolio Example

```csharp
var pnlManager = new PnLManager();

// Process trades from different portfolios
var trade1 = new ExecutionMessage
{
    PortfolioName = "Account1",
    SecurityId = new SecurityId { SecurityCode = "AAPL" },
    // ... trade details
};

var trade2 = new ExecutionMessage
{
    PortfolioName = "Account2",
    SecurityId = new SecurityId { SecurityCode = "MSFT" },
    // ... trade details
};

var changedPortfolios = new List<PortfolioPnLManager>();
pnlManager.ProcessMessage(trade1, changedPortfolios);
pnlManager.ProcessMessage(trade2, changedPortfolios);

// Total PnL across all portfolios
var totalPnL = pnlManager.RealizedPnL + pnlManager.UnrealizedPnL;

// Per-portfolio PnL
foreach (var pfManager in changedPortfolios)
{
    Console.WriteLine($"{pfManager.PortfolioName}: " +
                     $"Realized={pfManager.RealizedPnL}, " +
                     $"Unrealized={pfManager.UnrealizedPnL}");
}
```

## Real-Time Updates

### Unrealized PnL Refresh

```csharp
public class Strategy
{
    private TimeSpan _unrealizedPnLInterval = TimeSpan.FromMinutes(1);

    public TimeSpan UnrealizedPnLInterval
    {
        get => _unrealizedPnLInterval;
        set => _unrealizedPnLInterval = value;
    }
}
```

**Update Triggers:**
1. Market data update (tick, quote, candle)
2. Position change
3. Periodic timer (default: 1 minute)
4. Manual request

**Configuration:**
```csharp
// Update unrealized PnL every 30 seconds
strategy.UnrealizedPnLInterval = TimeSpan.FromSeconds(30);

// Update on every market data tick
strategy.PnLManager.UseTick = true;
```

## Advanced Scenarios

### Scenario 1: Scaling In/Out

```csharp
// Initial Position: Buy 10 @ 100
// Scale In: Buy 10 @ 102
// Scale Out: Sell 5 @ 105
// Final: Close 15 @ 108

// Position Stack Evolution:
// After first trade: [(100, 10)]
// After scale in:    [(100, 10), (102, 10)]
// After scale out:   [(100, 5), (102, 10)]
//   - Closed 5 from (100, 10)
//   - Realized PnL: (100 - 105) × 5 × (-1) = 25
// After final close: []
//   - Closed 5 from (100, 5): (100 - 108) × 5 × (-1) = 40
//   - Closed 10 from (102, 10): (102 - 108) × 10 × (-1) = 60
//   - Total Realized PnL: 25 + 40 + 60 = 125
```

### Scenario 2: Position Reversal

```csharp
// Long Position: Buy 10 @ 100
// Reverse to Short: Sell 20 @ 105

// Processing:
// 1. Close long position: 10 contracts
//    Realized PnL: (100 - 105) × 10 × (-1) = 50
// 2. Open short position: 10 contracts @ 105
//    Position Stack: [(105, 10)] with Side = Sell

// Later close at 103:
// Realized PnL: (105 - 103) × 10 × (+1) = 20

// Total Realized PnL: 50 + 20 = 70
```

### Scenario 3: Multiple Securities

```csharp
var strategy = new Strategy();

// Trade Security 1
var trade1 = CreateTrade("AAPL", Sides.Buy, 100, 150);
strategy.PnLManager.ProcessMessage(trade1.ToMessage());

// Trade Security 2
var trade2 = CreateTrade("MSFT", Sides.Buy, 50, 250);
strategy.PnLManager.ProcessMessage(trade2.ToMessage());

// Total PnL aggregates across all securities
var totalPnL = strategy.PnL;

// Per-security PnL requires tracking PortfolioPnLManager
```

## Performance Considerations

### 1. Caching

```csharp
public class PnLQueue
{
    private decimal? _unrealizedPnL;

    public decimal UnrealizedPnL
    {
        get
        {
            if (_unrealizedPnL is decimal unrealPnL)
                return unrealPnL;

            // Calculate and cache
            unrealPnL = CalculateUnrealizedPnL();
            _unrealizedPnL = unrealPnL;
            return unrealPnL;
        }
    }
}
```

### 2. Batch Processing

```csharp
// Process multiple messages efficiently
var changedPortfolios = new List<PortfolioPnLManager>();

foreach (var message in messages)
{
    pnlManager.ProcessMessage(message, changedPortfolios);
}

// Update UI once for all changes
UpdatePnLDisplay(pnlManager.RealizedPnL, pnlManager.UnrealizedPnL);
```

### 3. Selective Updates

```csharp
// Only update unrealized PnL when needed
if (CurrentTime - lastUpdateTime > UnrealizedPnLInterval)
{
    var unrealizedPnL = PnLManager.UnrealizedPnL;
    OnPnLChanged(unrealizedPnL);
    lastUpdateTime = CurrentTime;
}
```

## Testing PnL Calculations

```csharp
[TestMethod]
public void TestPnLCalculation()
{
    var pnlManager = new PnLManager();
    var securityId = new SecurityId { SecurityCode = "TEST" };

    // Update security info
    pnlManager.UpdateSecurity(new Level1ChangeMessage
    {
        SecurityId = securityId,
        ServerTime = DateTime.Now
    }
    .TryAdd(Level1Fields.PriceStep, 0.01m)
    .TryAdd(Level1Fields.StepPrice, 1m));

    // Register order
    pnlManager.ProcessMessage(new OrderRegisterMessage
    {
        SecurityId = securityId,
        PortfolioName = "Test",
        TransactionId = 1
    });

    // Execute buy trade
    var buyTrade = new ExecutionMessage
    {
        SecurityId = securityId,
        PortfolioName = "Test",
        Side = Sides.Buy,
        TradePrice = 100,
        TradeVolume = 10,
        TransactionId = 1,
        OriginalTransactionId = 1,
        ServerTime = DateTime.Now,
        DataType = DataType.Transactions,
        HasTradeInfo = true
    };

    var info1 = pnlManager.ProcessMessage(buyTrade);
    Assert.IsNotNull(info1);
    Assert.AreEqual(0, info1.ClosedVolume);
    Assert.AreEqual(0, pnlManager.RealizedPnL);

    // Execute sell trade
    var sellTrade = new ExecutionMessage
    {
        SecurityId = securityId,
        PortfolioName = "Test",
        Side = Sides.Sell,
        TradePrice = 105,
        TradeVolume = 10,
        TransactionId = 2,
        OriginalTransactionId = 2,
        ServerTime = DateTime.Now,
        DataType = DataType.Transactions,
        HasTradeInfo = true
    };

    var info2 = pnlManager.ProcessMessage(sellTrade);
    Assert.IsNotNull(info2);
    Assert.AreEqual(10, info2.ClosedVolume);
    Assert.AreEqual(50, info2.PnL);  // (100 - 105) × 10 × (-1) = 50
    Assert.AreEqual(50, pnlManager.RealizedPnL);
}
```

## See Also

- [Risk Rules](risk-rules.md)
- [Commission Calculation](commission-calculation.md)
- [Slippage Tracking](slippage-tracking.md)
- [Position Sizing](position-sizing.md)
- [Strategy Framework](../05-Strategy-Framework/strategies.md)
