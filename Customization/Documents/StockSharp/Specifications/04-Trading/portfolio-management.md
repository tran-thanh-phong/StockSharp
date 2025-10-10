# Portfolio Management Specification

## Overview

This specification covers portfolio (trading account) management in StockSharp, including portfolio properties, states, lookup operations, and real-time tracking. Portfolios represent trading accounts that hold cash and securities.

## Table of Contents

1. [Portfolio Class](#portfolio-class)
2. [Portfolio Properties](#portfolio-properties)
3. [Portfolio States](#portfolio-states)
4. [Portfolio Lookup](#portfolio-lookup)
5. [Portfolio Events](#portfolio-events)
6. [Account Values](#account-values)
7. [Practical Examples](#practical-examples)

---

## Portfolio Class

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Portfolio.cs`

### Class Hierarchy

```csharp
// Portfolio inherits from Position
public class Portfolio : Position
{
    // Portfolio-specific properties
    public string Name { get; set; }
    public ExchangeBoard Board { get; set; }
    public PortfolioStates? State { get; set; }

    // Inherited from Position:
    // - BeginValue (starting cash/value)
    // - CurrentValue (current cash/value)
    // - BlockedValue (blocked/margin)
    // - Commission
    // - Currency
    // - etc.
}
```

### Key Concept

> A Portfolio in StockSharp inherits from Position, meaning it has all position tracking capabilities plus portfolio-specific features. This allows tracking both account-level values and individual security positions using the same base class.

---

## Portfolio Properties

### Core Properties

#### Name

```csharp
/// <summary>
/// Portfolio code name (account identifier)
/// </summary>
public string Name { get; set; }
```

**Usage**:
```csharp
var portfolio = _connector.Portfolios.FirstOrDefault();
Console.WriteLine($"Portfolio: {portfolio.Name}");
```

#### Board

```csharp
/// <summary>
/// Exchange board for which the portfolio is active
/// </summary>
public ExchangeBoard Board { get; set; }
```

**Usage**:
```csharp
if (portfolio.Board != null)
{
    Console.WriteLine($"Trading on: {portfolio.Board.Code}");
}
```

#### State

```csharp
/// <summary>
/// Portfolio state (Online/Blocked)
/// </summary>
public PortfolioStates? State { get; set; }
```

**Values**:
```csharp
public enum PortfolioStates
{
    Online,   // Active and trading
    Blocked   // Restricted or suspended
}
```

**Usage**:
```csharp
if (portfolio.State == PortfolioStates.Online)
{
    Console.WriteLine("Portfolio is active for trading");
}
else if (portfolio.State == PortfolioStates.Blocked)
{
    Console.WriteLine("Portfolio is blocked");
}
```

### Inherited Properties from Position

#### BeginValue

```csharp
/// <summary>
/// Account balance at beginning of session
/// </summary>
public decimal? BeginValue { get; set; }
```

**Usage**:
```csharp
Console.WriteLine($"Starting balance: {portfolio.BeginValue:C}");
```

#### CurrentValue

```csharp
/// <summary>
/// Current account balance/equity
/// </summary>
public decimal? CurrentValue { get; set; }
```

**Usage**:
```csharp
decimal equity = portfolio.CurrentValue ?? 0;
Console.WriteLine($"Current equity: {equity:C}");
```

#### BlockedValue

```csharp
/// <summary>
/// Blocked amount (margin, pending orders, etc.)
/// </summary>
public decimal? BlockedValue { get; set; }
```

**Usage**:
```csharp
decimal total = portfolio.CurrentValue ?? 0;
decimal blocked = portfolio.BlockedValue ?? 0;
decimal available = total - blocked;

Console.WriteLine($"Total: {total:C}");
Console.WriteLine($"Blocked: {blocked:C}");
Console.WriteLine($"Available: {available:C}");
```

#### Commission

```csharp
/// <summary>
/// Total commission paid
/// </summary>
public decimal? Commission { get; set; }
```

#### Currency

```csharp
/// <summary>
/// Account currency
/// </summary>
public CurrencyTypes? Currency { get; set; }
```

**Usage**:
```csharp
var currency = portfolio.Currency ?? CurrencyTypes.USD;
Console.WriteLine($"Currency: {currency}");
```

---

## Portfolio States

**Location**: `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\PortfolioStates.cs`

### Enumeration

```csharp
public enum PortfolioStates
{
    /// <summary>
    /// Portfolio is online and active for trading
    /// </summary>
    Online,

    /// <summary>
    /// Portfolio is blocked/suspended
    /// </summary>
    Blocked
}
```

### State Handling

```csharp
_connector.PortfolioReceived += (subscription, portfolio) =>
{
    switch (portfolio.State)
    {
        case PortfolioStates.Online:
            Console.WriteLine($"{portfolio.Name} is ready for trading");
            EnableTrading(portfolio);
            break;

        case PortfolioStates.Blocked:
            Console.WriteLine($"{portfolio.Name} is blocked");
            DisableTrading(portfolio);
            break;

        case null:
            Console.WriteLine($"{portfolio.Name} state unknown");
            break;
    }
};
```

---

## Portfolio Lookup

### Automatic Subscription

```csharp
// Subscribe on connect (default behavior)
_connector.SubscriptionsOnConnect.Add(_connector.PortfolioLookup);

_connector.Connect();
// Portfolios will be automatically loaded
```

### Manual Subscription

```csharp
// Subscribe to portfolio updates
_connector.Subscribe(_connector.PortfolioLookup);
```

### PortfolioLookup Subscription

```csharp
/// <summary>
/// Predefined portfolio lookup subscription
/// </summary>
public Subscription PortfolioLookup { get; }
```

**Usage**:
```csharp
// Customize portfolio lookup
var portfolioLookup = new Subscription(new PortfolioLookupMessage
{
    // Optional filters
    PortfolioName = "MyAccount",
    Currency = CurrencyTypes.USD
});

_connector.Subscribe(portfolioLookup);
```

### Getting Portfolios

```csharp
// Get all portfolios
var portfolios = _connector.Portfolios;

foreach (var portfolio in portfolios)
{
    Console.WriteLine($"{portfolio.Name}: {portfolio.CurrentValue:C}");
}

// Get specific portfolio by name
var portfolio = _connector.Portfolios.FirstOrDefault(p => p.Name == "MyAccount");

if (portfolio != null)
{
    Console.WriteLine($"Found portfolio: {portfolio.Name}");
}
```

---

## Portfolio Events

### PortfolioReceived Event

**Signature**:
```csharp
event Action<Subscription, Portfolio> PortfolioReceived;
```

**Description**: Fired when portfolio data is received or updated.

**Usage**:
```csharp
_connector.PortfolioReceived += (subscription, portfolio) =>
{
    Console.WriteLine($"\nPortfolio Update: {portfolio.Name}");
    Console.WriteLine($"  State: {portfolio.State}");
    Console.WriteLine($"  Current Value: {portfolio.CurrentValue:C}");
    Console.WriteLine($"  Blocked: {portfolio.BlockedValue:C}");
    Console.WriteLine($"  Currency: {portfolio.Currency}");

    // Update UI
    UpdatePortfolioDisplay(portfolio);
};
```

### NewPortfolio Event

**Signature**:
```csharp
event Action<Portfolio> NewPortfolio;
```

**Description**: Fired when a new portfolio is discovered.

**Usage**:
```csharp
_connector.NewPortfolio += (portfolio) =>
{
    Console.WriteLine($"New portfolio discovered: {portfolio.Name}");

    // Add to dropdown
    PortfolioComboBox.Items.Add(portfolio);
};
```

### Example: Complete Portfolio Tracking

```csharp
public class PortfolioTracker
{
    private readonly Connector _connector;
    private readonly Dictionary<string, Portfolio> _portfolios = new();

    public PortfolioTracker(Connector connector)
    {
        _connector = connector;

        _connector.NewPortfolio += OnNewPortfolio;
        _connector.PortfolioReceived += OnPortfolioReceived;
    }

    private void OnNewPortfolio(Portfolio portfolio)
    {
        _portfolios[portfolio.Name] = portfolio;
        Console.WriteLine($"[NEW] Portfolio: {portfolio.Name}");
        PrintPortfolioInfo(portfolio);
    }

    private void OnPortfolioReceived(Subscription s, Portfolio portfolio)
    {
        var old = _portfolios.GetValueOrDefault(portfolio.Name);

        _portfolios[portfolio.Name] = portfolio;

        // Detect changes
        if (old != null)
        {
            if (old.State != portfolio.State)
            {
                Console.WriteLine($"[STATE] {portfolio.Name}: {old.State} -> {portfolio.State}");
            }

            if (old.CurrentValue != portfolio.CurrentValue)
            {
                Console.WriteLine($"[VALUE] {portfolio.Name}: {old.CurrentValue:C} -> {portfolio.CurrentValue:C}");
            }
        }
    }

    private void PrintPortfolioInfo(Portfolio portfolio)
    {
        Console.WriteLine($"  State: {portfolio.State}");
        Console.WriteLine($"  Begin Value: {portfolio.BeginValue:C}");
        Console.WriteLine($"  Current Value: {portfolio.CurrentValue:C}");
        Console.WriteLine($"  Blocked: {portfolio.BlockedValue:C}");
        Console.WriteLine($"  Available: {(portfolio.CurrentValue ?? 0) - (portfolio.BlockedValue ?? 0):C}");
        Console.WriteLine($"  Currency: {portfolio.Currency}");
    }

    public void PrintAllPortfolios()
    {
        Console.WriteLine("\n=== All Portfolios ===");

        foreach (var portfolio in _portfolios.Values)
        {
            Console.WriteLine($"\n{portfolio.Name}:");
            PrintPortfolioInfo(portfolio);
        }
    }
}
```

---

## Account Values

### Calculating Available Balance

```csharp
public static decimal GetAvailableBalance(Portfolio portfolio)
{
    decimal current = portfolio.CurrentValue ?? 0;
    decimal blocked = portfolio.BlockedValue ?? 0;

    return current - blocked;
}
```

**Usage**:
```csharp
var available = GetAvailableBalance(portfolio);

if (available >= orderValue)
{
    Console.WriteLine("Sufficient funds for order");
}
else
{
    Console.WriteLine($"Insufficient funds. Need: {orderValue:C}, Have: {available:C}");
}
```

### Calculating Daily P&L

```csharp
public static decimal GetDailyPnL(Portfolio portfolio)
{
    if (!portfolio.BeginValue.HasValue || !portfolio.CurrentValue.HasValue)
        return 0;

    return portfolio.CurrentValue.Value - portfolio.BeginValue.Value;
}
```

**Usage**:
```csharp
decimal dailyPnL = GetDailyPnL(portfolio);
decimal dailyPnLPercent = portfolio.BeginValue > 0
    ? (dailyPnL / portfolio.BeginValue.Value) * 100
    : 0;

Console.WriteLine($"Daily P&L: {dailyPnL:C} ({dailyPnLPercent:F2}%)");
```

### Portfolio Metrics

```csharp
public class PortfolioMetrics
{
    public static void PrintMetrics(Portfolio portfolio)
    {
        decimal begin = portfolio.BeginValue ?? 0;
        decimal current = portfolio.CurrentValue ?? 0;
        decimal blocked = portfolio.BlockedValue ?? 0;
        decimal commission = portfolio.Commission ?? 0;

        decimal available = current - blocked;
        decimal dailyPnL = current - begin;
        decimal dailyPnLPercent = begin > 0 ? (dailyPnL / begin) * 100 : 0;

        Console.WriteLine($"\n=== Portfolio Metrics: {portfolio.Name} ===");
        Console.WriteLine($"Begin Value:      {begin:C}");
        Console.WriteLine($"Current Value:    {current:C}");
        Console.WriteLine($"Blocked:          {blocked:C}");
        Console.WriteLine($"Available:        {available:C}");
        Console.WriteLine($"Commission:       {commission:C}");
        Console.WriteLine($"Daily P&L:        {dailyPnL:C} ({dailyPnLPercent:F2}%)");
        Console.WriteLine($"State:            {portfolio.State}");
        Console.WriteLine($"Currency:         {portfolio.Currency}");
    }
}
```

---

## Practical Examples

### Example 1: Portfolio Selection UI

```csharp
public class PortfolioSelector
{
    private readonly Connector _connector;
    private readonly ComboBox _portfolioComboBox;

    public PortfolioSelector(Connector connector, ComboBox comboBox)
    {
        _connector = connector;
        _portfolioComboBox = comboBox;

        _connector.NewPortfolio += OnNewPortfolio;
        _connector.PortfolioReceived += OnPortfolioReceived;
    }

    private void OnNewPortfolio(Portfolio portfolio)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _portfolioComboBox.Items.Add(portfolio);

            // Auto-select first portfolio
            if (_portfolioComboBox.SelectedItem == null)
            {
                _portfolioComboBox.SelectedItem = portfolio;
            }
        });
    }

    private void OnPortfolioReceived(Subscription s, Portfolio portfolio)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Refresh display
            _portfolioComboBox.Items.Refresh();
        });
    }

    public Portfolio SelectedPortfolio =>
        _portfolioComboBox.SelectedItem as Portfolio;
}
```

### Example 2: Portfolio Monitor

```csharp
public class PortfolioMonitor
{
    private readonly Connector _connector;
    private readonly Portfolio _portfolio;
    private readonly decimal _warningThreshold;

    public PortfolioMonitor(
        Connector connector,
        Portfolio portfolio,
        decimal warningThreshold = 0.10m) // 10%
    {
        _connector = connector;
        _portfolio = portfolio;
        _warningThreshold = warningThreshold;

        _connector.PortfolioReceived += OnPortfolioReceived;
    }

    private void OnPortfolioReceived(Subscription s, Portfolio portfolio)
    {
        if (portfolio.Name != _portfolio.Name)
            return;

        CheckPortfolioHealth(portfolio);
    }

    private void CheckPortfolioHealth(Portfolio portfolio)
    {
        // Check if portfolio is blocked
        if (portfolio.State == PortfolioStates.Blocked)
        {
            Console.WriteLine($"ALERT: Portfolio {portfolio.Name} is BLOCKED!");
            OnPortfolioBlocked();
            return;
        }

        // Check available balance
        decimal current = portfolio.CurrentValue ?? 0;
        decimal blocked = portfolio.BlockedValue ?? 0;
        decimal available = current - blocked;

        if (available < current * _warningThreshold)
        {
            Console.WriteLine($"WARNING: Low available balance: {available:C} / {current:C}");
        }

        // Check daily drawdown
        decimal begin = portfolio.BeginValue ?? current;
        decimal dailyPnL = current - begin;
        decimal drawdownPercent = begin > 0 ? (dailyPnL / begin) : 0;

        if (drawdownPercent < -_warningThreshold)
        {
            Console.WriteLine($"WARNING: Daily drawdown: {drawdownPercent:P2}");
        }
    }

    private void OnPortfolioBlocked()
    {
        // Cancel all active orders
        _connector.CancelOrders(portfolio: _portfolio);

        // Notify user
        Console.WriteLine("All orders cancelled due to portfolio block");
    }
}
```

### Example 3: Multi-Portfolio Manager

```csharp
public class MultiPortfolioManager
{
    private readonly Connector _connector;
    private readonly Dictionary<string, PortfolioInfo> _portfolios = new();

    private class PortfolioInfo
    {
        public Portfolio Portfolio { get; set; }
        public decimal MaxAllocation { get; set; }
        public decimal CurrentAllocation { get; set; }
    }

    public MultiPortfolioManager(Connector connector)
    {
        _connector = connector;

        _connector.NewPortfolio += OnNewPortfolio;
        _connector.PortfolioReceived += OnPortfolioReceived;
    }

    private void OnNewPortfolio(Portfolio portfolio)
    {
        _portfolios[portfolio.Name] = new PortfolioInfo
        {
            Portfolio = portfolio,
            MaxAllocation = 1.0m, // 100% by default
            CurrentAllocation = 0
        };
    }

    private void OnPortfolioReceived(Subscription s, Portfolio portfolio)
    {
        if (!_portfolios.TryGetValue(portfolio.Name, out var info))
            return;

        info.Portfolio = portfolio;
        UpdateAllocation(info);
    }

    private void UpdateAllocation(PortfolioInfo info)
    {
        decimal equity = info.Portfolio.CurrentValue ?? 0;
        decimal blocked = info.Portfolio.BlockedValue ?? 0;

        if (equity > 0)
        {
            info.CurrentAllocation = blocked / equity;
        }
    }

    public Portfolio GetPortfolioWithCapacity(decimal requiredAmount)
    {
        foreach (var info in _portfolios.Values)
        {
            if (info.Portfolio.State != PortfolioStates.Online)
                continue;

            decimal equity = info.Portfolio.CurrentValue ?? 0;
            decimal blocked = info.Portfolio.BlockedValue ?? 0;
            decimal available = equity - blocked;
            decimal maxBlocked = equity * info.MaxAllocation;
            decimal capacity = maxBlocked - blocked;

            if (capacity >= requiredAmount && available >= requiredAmount)
            {
                return info.Portfolio;
            }
        }

        return null;
    }

    public void SetMaxAllocation(string portfolioName, decimal maxAllocationPercent)
    {
        if (_portfolios.TryGetValue(portfolioName, out var info))
        {
            info.MaxAllocation = maxAllocationPercent;
        }
    }

    public void PrintStatus()
    {
        Console.WriteLine("\n=== Multi-Portfolio Status ===");

        foreach (var info in _portfolios.Values)
        {
            var p = info.Portfolio;
            decimal equity = p.CurrentValue ?? 0;

            Console.WriteLine($"\n{p.Name}:");
            Console.WriteLine($"  State: {p.State}");
            Console.WriteLine($"  Equity: {equity:C}");
            Console.WriteLine($"  Allocation: {info.CurrentAllocation:P2} / {info.MaxAllocation:P2}");
            Console.WriteLine($"  Available: {(equity - (p.BlockedValue ?? 0)):C}");
        }
    }
}
```

### Example 4: Portfolio Risk Calculator

```csharp
public class PortfolioRiskCalculator
{
    public static decimal CalculateMaxOrderSize(
        Portfolio portfolio,
        decimal riskPercent,
        decimal entryPrice,
        decimal stopPrice)
    {
        decimal equity = portfolio.CurrentValue ?? 0;
        decimal blocked = portfolio.BlockedValue ?? 0;
        decimal available = equity - blocked;

        // Risk amount
        decimal riskAmount = equity * riskPercent;

        // Risk per share
        decimal riskPerShare = Math.Abs(entryPrice - stopPrice);

        if (riskPerShare == 0)
            return 0;

        // Shares to risk
        decimal shares = riskAmount / riskPerShare;

        // Check if we can afford it
        decimal orderValue = shares * entryPrice;

        if (orderValue > available)
        {
            // Scale down to available funds
            shares = available / entryPrice;
        }

        return Math.Floor(shares);
    }

    public static bool CanAffordOrder(
        Portfolio portfolio,
        decimal price,
        decimal volume)
    {
        decimal available = (portfolio.CurrentValue ?? 0) - (portfolio.BlockedValue ?? 0);
        decimal orderValue = price * volume;

        return available >= orderValue;
    }
}
```

**Usage**:
```csharp
// Calculate position size with 2% risk
decimal maxShares = PortfolioRiskCalculator.CalculateMaxOrderSize(
    portfolio: _portfolio,
    riskPercent: 0.02m,  // 2%
    entryPrice: 100.00m,
    stopPrice: 98.00m    // 2% stop
);

Console.WriteLine($"Max shares to risk 2%: {maxShares}");

// Check if we can afford the order
bool canAfford = PortfolioRiskCalculator.CanAffordOrder(
    portfolio: _portfolio,
    price: 100.00m,
    volume: maxShares
);

if (canAfford)
{
    var order = new Order
    {
        Security = _security,
        Portfolio = _portfolio,
        Side = Sides.Buy,
        Volume = maxShares,
        Type = OrderTypes.Limit,
        Price = 100.00m
    };

    _connector.RegisterOrder(order);
}
```

### Example 5: Portfolio Data Source for UI

```csharp
public class PortfolioDataSource
{
    private readonly Connector _connector;
    private readonly ObservableCollection<Portfolio> _portfolios = new();

    public PortfolioDataSource(Connector connector)
    {
        _connector = connector;

        _connector.NewPortfolio += OnNewPortfolio;
        _connector.PortfolioReceived += OnPortfolioReceived;
    }

    private void OnNewPortfolio(Portfolio portfolio)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _portfolios.Add(portfolio);
        });
    }

    private void OnPortfolioReceived(Subscription s, Portfolio portfolio)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Portfolio object is updated in-place
            // UI binding will auto-refresh if using INotifyPropertyChanged
        });
    }

    public ObservableCollection<Portfolio> Portfolios => _portfolios;

    public Portfolio GetPortfolioByName(string name)
    {
        return _portfolios.FirstOrDefault(p => p.Name == name);
    }
}
```

**XAML Usage**:
```xml
<ComboBox ItemsSource="{Binding PortfolioDataSource.Portfolios}"
          DisplayMemberPath="Name"
          SelectedItem="{Binding SelectedPortfolio}" />
```

### Example 6: Anonymous and Simulator Portfolios

```csharp
// Anonymous portfolio (for orders without account)
var anonymousPortfolio = Portfolio.AnonymousPortfolio;
Console.WriteLine($"Anonymous: {anonymousPortfolio.Name}");

// Simulator portfolio for backtesting
var simPortfolio = Portfolio.CreateSimulator();
Console.WriteLine($"Simulator starting balance: {simPortfolio.BeginValue:C}");

// Use simulator portfolio
var order = new Order
{
    Security = _security,
    Portfolio = simPortfolio,
    Side = Sides.Buy,
    Volume = 100,
    Type = OrderTypes.Market
};
```

---

## Best Practices

### 1. Subscribe to Portfolios Before Trading

```csharp
// Subscribe to portfolio updates on connect
_connector.SubscriptionsOnConnect.Add(_connector.PortfolioLookup);

_connector.PortfolioReceived += OnPortfolioReceived;
_connector.Connect();
```

### 2. Always Check Portfolio State

```csharp
private bool CanTrade(Portfolio portfolio)
{
    if (portfolio == null)
        return false;

    if (portfolio.State != PortfolioStates.Online)
    {
        Console.WriteLine($"Portfolio {portfolio.Name} is not online");
        return false;
    }

    return true;
}
```

### 3. Verify Sufficient Funds

```csharp
private bool HasSufficientFunds(Portfolio portfolio, decimal requiredAmount)
{
    decimal available = (portfolio.CurrentValue ?? 0) - (portfolio.BlockedValue ?? 0);

    if (available < requiredAmount)
    {
        Console.WriteLine($"Insufficient funds. Need: {requiredAmount:C}, Have: {available:C}");
        return false;
    }

    return true;
}
```

### 4. Handle Null Values

```csharp
// Safe access to portfolio values
decimal equity = portfolio.CurrentValue ?? 0;
decimal blocked = portfolio.BlockedValue ?? 0;
decimal available = equity - blocked;
```

---

## Related Specifications

- [Position Management](./position-management.md) - Position tracking (Portfolio inherits from Position)
- [Order Management](./order-management.md) - Order operations requiring portfolio
- [Transaction Tracking](./transaction-tracking.md) - Transaction lifecycle

---

## References

- **Source Files**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Portfolio.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\BusinessEntities\Position.cs`
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Messages\PortfolioStates.cs`

- **Sample Projects**:
  - `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\01_Basic\03_Orders`
