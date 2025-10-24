# BitStamp Portfolio & Position Management - Quick Reference

## Overview

This document provides a gap analysis between the StockSharp portfolio/position management specifications and the BitStamp connector implementation, serving as a quick reference for developers working with the BitStamp adapter.

**Related Specifications:**
- `Customization/Documents/StockSharp/Specifications/04-Trading/portfolio-management.md`
- `Customization/Documents/StockSharp/Specifications/04-Trading/position-management.md`

**Implementation File:**
- `Connectors/BitStamp/BitStampMessageAdapter_Transaction.cs:381`

---

## Gap Analysis Summary

### ✅ Implemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Portfolio Name | ✅ Implemented | Format: `BitStamp_{KeyId}` |
| Portfolio Board Code | ✅ Implemented | Set to `BoardCodes.BitStamp` |
| Position CurrentValue | ✅ Implemented | Available balance per currency |
| Position CurrentPrice | ✅ Implemented | Current market price |
| Position BlockedValue | ✅ Implemented | Amount locked in orders |
| Commission (Taker Fee) | ✅ Implemented | Via Level1ChangeMessage |
| Subscription Handling | ✅ Implemented | Subscribe/Unsubscribe support |
| Auto-refresh on Order Changes | ✅ Implemented | Called after withdrawals, cancellations |

### ⚠️ Partially Implemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Portfolio State | ⚠️ Missing | No Online/Blocked state reporting |
| Currency Type | ⚠️ Missing | Not explicitly set in PortfolioMessage |
| Position P&L Tracking | ⚠️ Limited | No UnrealizedPnL or RealizedPnL |
| Position Average Price | ⚠️ Missing | No AveragePrice calculation |

### ❌ Missing Features

| Feature | Status | Impact |
|---------|--------|--------|
| Portfolio.BeginValue | ❌ Not provided | Cannot calculate daily P&L |
| Portfolio.CurrentValue | ❌ Not provided | No total equity tracking |
| Portfolio.BlockedValue | ❌ Not provided | No portfolio-level margin info |
| Portfolio.Commission | ❌ Not provided | No total commission tracking |
| Portfolio.State | ❌ Not provided | Cannot detect blocked accounts |
| Position.BeginValue | ❌ Not provided | Cannot track session starting positions |
| Position.UnrealizedPnL | ❌ Not provided | No open position P&L |
| Position.RealizedPnL | ❌ Not provided | No closed trade P&L |
| Position.AveragePrice | ❌ Not provided | Cannot calculate entry price |
| Position.VariationMargin | ❌ Not provided | N/A for crypto spot trading |
| Position.LiquidationPrice | ❌ Not provided | N/A for spot trading |
| Position.Leverage | ❌ Not provided | N/A for spot trading |
| Position.Side | ❌ Not provided | BitStamp uses net positions |
| Position.StrategyId | ❌ Not provided | No multi-strategy support |

---

## Implementation Details

### Portfolio Name Generation

**Location:** `BitStampMessageAdapter_Transaction.cs:5`

```csharp
private string PortfolioName => nameof(BitStamp) + "_" + Key.ToId();
```

**Result:** Portfolio name like `BitStamp_12345`

### PortfolioLookupAsync Implementation

**Location:** `BitStampMessageAdapter_Transaction.cs:381`

```csharp
public override async ValueTask PortfolioLookupAsync(
    PortfolioLookupMessage lookupMsg,
    CancellationToken cancellationToken)
{
    // 1. Handle subscription messages
    if (lookupMsg != null)
    {
        SendSubscriptionReply(lookupMsg.TransactionId);
        if (!lookupMsg.IsSubscribe)
            return;
    }

    var transactionId = lookupMsg?.TransactionId ?? 0;
    var pfName = PortfolioName;

    // 2. Send portfolio message (basic info only)
    SendOutMessage(new PortfolioMessage
    {
        PortfolioName = pfName,
        BoardCode = BoardCodes.BitStamp,
        OriginalTransactionId = transactionId,
    });

    // 3. Fetch balances from BitStamp API
    var tuple = await _httpClient.GetBalances(null, cancellationToken);

    // 4. Send position changes for each currency
    foreach (var pair in tuple.Item1)
    {
        var currValue = pair.Value.First;    // Available balance
        var currPrice = pair.Value.Second;   // Current price
        var blockValue = pair.Value.Third;   // Blocked in orders

        if (currValue == null && currPrice == null && blockValue == null)
            continue;

        var msg = this.CreatePositionChangeMessage(pfName,
            pair.Key.ToUpperInvariant().ToStockSharp(false));

        msg.TryAdd(PositionChangeTypes.CurrentValue, currValue, true);
        msg.TryAdd(PositionChangeTypes.CurrentPrice, currPrice, true);
        msg.TryAdd(PositionChangeTypes.BlockedValue, blockValue, true);

        SendOutMessage(msg);
    }

    // 5. Send commission fees
    foreach (var pair in tuple.Item2)
    {
        SendOutMessage(new Level1ChangeMessage
        {
            SecurityId = pair.Key.ToStockSharp(),
            ServerTime = CurrentTime.ConvertToUtc()
        }.TryAdd(Level1Fields.CommissionTaker, pair.Value));
    }

    _lastTimeBalanceCheck = CurrentTime;

    if (lookupMsg != null)
        SendSubscriptionResult(lookupMsg);
}
```

### What Data is Sent

#### 1. PortfolioMessage
```csharp
new PortfolioMessage
{
    PortfolioName = "BitStamp_12345",
    BoardCode = BoardCodes.BitStamp,
    OriginalTransactionId = transactionId
    // Missing: State, Currency, CurrentValue, BlockedValue, Commission
}
```

#### 2. PositionChangeMessage (per currency)
```csharp
var msg = CreatePositionChangeMessage(pfName, securityId);
msg.TryAdd(PositionChangeTypes.CurrentValue, currValue, true);    // Available balance
msg.TryAdd(PositionChangeTypes.CurrentPrice, currPrice, true);    // Market price
msg.TryAdd(PositionChangeTypes.BlockedValue, blockValue, true);   // Locked amount
// Missing: BeginValue, AveragePrice, UnrealizedPnL, RealizedPnL, Commission
```

#### 3. Level1ChangeMessage (commission rates)
```csharp
new Level1ChangeMessage
{
    SecurityId = pair.Key.ToStockSharp(),
    ServerTime = CurrentTime.ConvertToUtc(),
    Changes = { [Level1Fields.CommissionTaker] = feeRate }
}
```

### When Portfolio is Refreshed

The `PortfolioLookupAsync()` method is called in these scenarios:

1. **After Withdrawals** (line 35):
   ```csharp
   await PortfolioLookupAsync(null, cancellationToken);
   ```

2. **After Order Cancellation** (line 68):
   ```csharp
   await PortfolioLookupAsync(null, cancellationToken);
   ```

3. **After Cancel All Orders** (line 85):
   ```csharp
   await PortfolioLookupAsync(null, cancellationToken);
   ```

4. **After Order Status Changes** (line 308):
   ```csharp
   if (portfolioRefresh)
       await PortfolioLookupAsync(null, cancellationToken);
   ```

5. **User-Initiated Subscription**:
   ```csharp
   _connector.Subscribe(_connector.PortfolioLookup);
   ```

---

## Specification Compliance

### Portfolio Properties (from spec)

| Property | Spec | BitStamp | Gap |
|----------|------|----------|-----|
| Name | Required | ✅ `BitStamp_{KeyId}` | None |
| Board | Required | ✅ `BoardCodes.BitStamp` | None |
| State | Optional | ❌ Not set | Cannot detect account restrictions |
| BeginValue | Optional | ❌ Not set | Cannot calculate daily P&L |
| CurrentValue | Optional | ❌ Not set | No total portfolio equity |
| BlockedValue | Optional | ❌ Not set | No portfolio-level margin |
| Commission | Optional | ❌ Not set | Cannot track total fees |
| Currency | Optional | ❌ Not set | Unclear base currency |

### Position Properties (from spec)

| Property | Spec | BitStamp | Gap |
|----------|------|----------|-----|
| CurrentValue | Required | ✅ Provided | None |
| CurrentPrice | Required | ✅ Provided | None |
| BlockedValue | Optional | ✅ Provided | None |
| BeginValue | Optional | ❌ Not provided | Cannot track session start |
| AveragePrice | Optional | ❌ Not provided | Cannot calculate entry price |
| UnrealizedPnL | Optional | ❌ Not provided | No open P&L |
| RealizedPnL | Optional | ❌ Not provided | No closed P&L |
| Commission | Optional | ❌ Not provided | Per-position fees unknown |
| VariationMargin | Optional | ❌ N/A | Futures-only field |
| LiquidationPrice | Optional | ❌ N/A | Margin trading only |
| Leverage | Optional | ❌ N/A | Margin trading only |
| Side | Optional | ❌ Not provided | Net position only |
| StrategyId | Optional | ❌ Not provided | No multi-strategy |

---

## Usage Examples

### Getting Portfolio Information

```csharp
// Subscribe to portfolio updates
_connector.SubscriptionsOnConnect.Add(_connector.PortfolioLookup);

_connector.PortfolioReceived += (subscription, portfolio) =>
{
    Console.WriteLine($"Portfolio: {portfolio.Name}");
    Console.WriteLine($"Board: {portfolio.Board.Code}");

    // ⚠️ These will be null for BitStamp:
    // portfolio.State
    // portfolio.CurrentValue
    // portfolio.BlockedValue
    // portfolio.Currency
};

_connector.Connect();
```

### Getting Position Information

```csharp
_connector.PositionReceived += (subscription, position) =>
{
    Console.WriteLine($"\n{position.Security.Code} Position:");
    Console.WriteLine($"  Portfolio: {position.Portfolio.Name}");
    Console.WriteLine($"  Current: {position.CurrentValue ?? 0}");         // ✅ Available
    Console.WriteLine($"  Price: {position.CurrentPrice ?? 0:F2}");        // ✅ Available
    Console.WriteLine($"  Blocked: {position.BlockedValue ?? 0}");         // ✅ Available

    // ⚠️ These will be null for BitStamp:
    // Console.WriteLine($"  Avg Price: {position.AveragePrice}");        // ❌ Not available
    // Console.WriteLine($"  P&L: {position.UnrealizedPnL}");             // ❌ Not available
    // Console.WriteLine($"  Realized: {position.RealizedPnL}");          // ❌ Not available
};
```

### Calculating Available Balance

```csharp
var position = _connector.GetPosition(_portfolio, _security);

decimal available = position.CurrentValue ?? 0;
decimal blocked = position.BlockedValue ?? 0;
decimal free = available;  // Note: CurrentValue already excludes blocked in BitStamp

Console.WriteLine($"Total Available: {available}");
Console.WriteLine($"Blocked in Orders: {blocked}");
Console.WriteLine($"Free to Trade: {free}");
```

### Manual P&L Calculation (Workaround)

Since BitStamp doesn't provide P&L data, you must calculate it manually:

```csharp
public class BitStampPnLTracker
{
    private readonly Dictionary<string, TradeHistory> _tradeHistory = new();

    public void OnTrade(Trade trade)
    {
        var key = trade.Security.Code;

        if (!_tradeHistory.TryGetValue(key, out var history))
        {
            history = new TradeHistory();
            _tradeHistory[key] = history;
        }

        // Track trades
        history.AddTrade(trade);
    }

    public decimal CalculateUnrealizedPnL(Position position)
    {
        if (!_tradeHistory.TryGetValue(position.Security.Code, out var history))
            return 0;

        decimal avgEntryPrice = history.GetAveragePrice();
        decimal currentPrice = position.CurrentPrice ?? 0;
        decimal quantity = position.CurrentValue ?? 0;

        return (currentPrice - avgEntryPrice) * quantity;
    }
}
```

---

## Recommendations

### 1. Portfolio State Enhancement

**Current:** No state information provided
**Recommendation:** Add state detection based on API responses

```csharp
// Proposed enhancement
SendOutMessage(new PortfolioMessage
{
    PortfolioName = pfName,
    BoardCode = BoardCodes.BitStamp,
    State = PortfolioStates.Online,  // ← Add this
    OriginalTransactionId = transactionId,
});
```

### 2. Portfolio Aggregation

**Current:** No total portfolio equity
**Recommendation:** Calculate and send portfolio-level values

```csharp
// Proposed enhancement
decimal totalValue = 0;
decimal totalBlocked = 0;

foreach (var pair in tuple.Item1)
{
    totalValue += (pair.Value.First ?? 0) * (pair.Value.Second ?? 1);
    totalBlocked += (pair.Value.Third ?? 0) * (pair.Value.Second ?? 1);
}

SendOutMessage(new PortfolioMessage
{
    PortfolioName = pfName,
    BoardCode = BoardCodes.BitStamp,
    State = PortfolioStates.Online,
    CurrentValue = totalValue,      // ← Add this
    BlockedValue = totalBlocked,    // ← Add this
    Currency = CurrencyTypes.USD,   // ← Add this
    OriginalTransactionId = transactionId,
});
```

### 3. Position Average Price Tracking

**Current:** No average price calculation
**Recommendation:** Track average entry price from trades

```csharp
// Proposed: Add field to track trade history
private readonly Dictionary<string, decimal> _avgPrices = new();

// In ProcessTrade method, calculate average price:
private void UpdateAveragePrice(string securityCode, decimal tradePrice, decimal tradeVolume)
{
    // Implement VWAP calculation
    // Store in _avgPrices dictionary
}

// In PortfolioLookupAsync, include average price:
msg.TryAdd(PositionChangeTypes.AveragePrice, _avgPrices.GetValueOrDefault(currencyCode), true);
```

### 4. Position P&L Calculation

**Current:** No P&L reporting
**Recommendation:** Calculate and send P&L data

```csharp
// Proposed enhancement
decimal avgPrice = _avgPrices.GetValueOrDefault(currencyCode);
decimal currentPrice = pair.Value.Second ?? 0;
decimal quantity = pair.Value.First ?? 0;
decimal unrealizedPnL = (currentPrice - avgPrice) * quantity;

msg.TryAdd(PositionChangeTypes.UnrealizedPnL, unrealizedPnL, true);
msg.TryAdd(PositionChangeTypes.AveragePrice, avgPrice, true);
```

### 5. Commission Tracking

**Current:** Only taker fee rate provided
**Recommendation:** Track actual commission paid per position

```csharp
// Proposed: Track commission in ProcessTrade
private readonly Dictionary<string, decimal> _commissions = new();

// In ProcessTrade:
_commissions[pair] = (_commissions.GetValueOrDefault(pair) ?? 0) + (decimal)transaction.Fee;

// In PortfolioLookupAsync:
msg.TryAdd(PositionChangeTypes.Commission, _commissions.GetValueOrDefault(currencyCode), true);
```

### 6. Session Begin Values

**Current:** No beginning-of-session snapshot
**Recommendation:** Store initial state on connect

```csharp
// Proposed: Store begin values
private Dictionary<string, decimal> _beginValues = null;

// On first call after connect:
if (_beginValues == null)
{
    _beginValues = new Dictionary<string, decimal>();
    foreach (var pair in tuple.Item1)
    {
        _beginValues[pair.Key] = pair.Value.First ?? 0;
    }
}

// Then include in messages:
msg.TryAdd(PositionChangeTypes.BeginValue, _beginValues.GetValueOrDefault(currencyCode), true);
```

---

## Comparison with Other Connectors

### Full-Featured Reference: Interactive Brokers

Interactive Brokers provides comprehensive portfolio/position data:
- ✅ Portfolio state, equity, margin
- ✅ Position average price, P&L
- ✅ Real-time P&L updates
- ✅ Multi-currency support
- ✅ Strategy-level positions

### BitStamp Limitations

BitStamp's REST API limitations affect implementation:
- No streaming position updates (polling required)
- Limited balance endpoint data
- No historical cost basis tracking
- No built-in P&L calculation
- Spot trading only (no margin/futures)

---

## Quick Reference Cheat Sheet

### What BitStamp Provides

```csharp
Position position = _connector.GetPosition(portfolio, security);

// ✅ Available Data:
position.CurrentValue    // Available balance
position.CurrentPrice    // Market price
position.BlockedValue    // Locked in orders
position.Portfolio.Name  // "BitStamp_{KeyId}"
position.Security        // Security object

// ❌ Unavailable Data:
position.BeginValue      // null
position.AveragePrice    // null
position.UnrealizedPnL   // null
position.RealizedPnL     // null
position.Commission      // null
position.Side            // null
position.StrategyId      // ""

portfolio.State          // null
portfolio.CurrentValue   // null
portfolio.BlockedValue   // null
portfolio.Currency       // null
```

### Working with BitStamp Limitations

```csharp
// ✅ GOOD: Check what's available
decimal available = position.CurrentValue ?? 0;
decimal price = position.CurrentPrice ?? 0;
decimal blocked = position.BlockedValue ?? 0;

// ❌ BAD: Assume full spec compliance
decimal avgPrice = position.AveragePrice.Value;  // Will throw NullReferenceException
decimal pnl = position.UnrealizedPnL.Value;      // Will throw NullReferenceException

// ✅ GOOD: Calculate P&L manually
if (position.CurrentPrice.HasValue && _myAvgPrice.HasValue)
{
    decimal pnl = (position.CurrentPrice.Value - _myAvgPrice.Value) * (position.CurrentValue ?? 0);
}
```

---

## Testing Checklist

When testing BitStamp portfolio/position management:

- [ ] Portfolio name format is correct (`BitStamp_{KeyId}`)
- [ ] Board code is set to `BoardCodes.BitStamp`
- [ ] Position CurrentValue reflects available balance
- [ ] Position BlockedValue reflects orders
- [ ] Position CurrentPrice updates with market data
- [ ] Commission rates are provided via Level1ChangeMessage
- [ ] Portfolio refreshes after order changes
- [ ] Subscription/unsubscription works correctly
- [ ] Handle null values for missing properties
- [ ] Implement manual P&L tracking if needed
- [ ] Don't rely on Portfolio.State for account status

---

## Related Files

### Implementation
- `Connectors/BitStamp/BitStampMessageAdapter_Transaction.cs` - Main implementation
- `Connectors/BitStamp/BitStampMessageAdapter.cs` - Base adapter
- `Connectors/BitStamp/Native/BitStampClient.cs` - HTTP client

### Specifications
- `Customization/Documents/StockSharp/Specifications/04-Trading/portfolio-management.md`
- `Customization/Documents/StockSharp/Specifications/04-Trading/position-management.md`
- `BusinessEntities/Portfolio.cs` - Portfolio class definition
- `BusinessEntities/Position.cs` - Position class definition

### Reference Implementations
- `Connectors/InteractiveBrokers/` - Full-featured example
- `Connectors/Binance/` - Another crypto exchange
- `Samples/01_Basic/03_Orders/` - Usage examples

---

## Summary

### BitStamp's Portfolio Management Capabilities

**Strengths:**
- ✅ Basic balance tracking per currency
- ✅ Real-time price updates
- ✅ Blocked amount tracking
- ✅ Automatic refresh on order changes
- ✅ Commission rate information

**Limitations:**
- ❌ No portfolio-level aggregation
- ❌ No P&L calculation
- ❌ No average price tracking
- ❌ No account state information
- ❌ No multi-strategy support
- ❌ No session begin values

**Best Practices:**
1. Always null-check Position properties
2. Implement manual P&L tracking if needed
3. Calculate portfolio totals in USD equivalent
4. Store trade history for average price calculation
5. Don't rely on Portfolio.State for BitStamp
6. Use CurrentValue for available balance (already excludes blocked)

**Typical Use Case:**
BitStamp connector is suitable for:
- Simple spot trading strategies
- Single-account trading
- Applications that don't require detailed P&L reporting
- Systems that can calculate P&L externally

**Not Suitable For:**
- Multi-strategy portfolio management
- Detailed performance analytics
- Regulatory reporting requiring cost basis
- Applications requiring real-time P&L

---

**Document Version:** 1.0
**Last Updated:** 2025-10-24
**Applies to:** StockSharp BitStamp Connector
