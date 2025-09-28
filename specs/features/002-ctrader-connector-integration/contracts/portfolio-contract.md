# Portfolio Contract

## Portfolio Lookup and Monitoring

### Portfolio Lookup Request
```csharp
// StockSharp Request
PortfolioLookupMessage
{
    TransactionId: 78901,
    IsSubscribe: true,  // For real-time updates
    PortfolioName: null // Lookup all portfolios
}

// cTrader Account Query
ProtoOAGetAccountsReq
{
    // Query available trading accounts
}

// Response Processing
ProtoOAGetAccountsRes
{
    Accounts: [
        {
            AccountId: 12345,
            AccountType: HEDGED,
            CurrencyCode: "USD",
            Balance: 10000.00
        }
    ]
}
```

### Portfolio Information
```csharp
// StockSharp Portfolio Message
PortfolioMessage
{
    PortfolioName: "CTrader_12345",
    BoardCode: BoardCodes.CTrader,
    Currency: CurrencyTypes.USD,
    BeginValue: 10000.00m,      // Initial balance
    CurrentValue: 10127.50m,    // Current equity
    BlockedValue: 360.15m,      // Used margin
    VariationMargin: 127.50m,   // Unrealized P&L
    Commission: 25.75m,         // Total commission paid
    OriginalTransactionId: 78901
}
```

## Real-time Balance Updates

### Account Balance Changes
```csharp
// cTrader Balance Update
ProtoOATraderUpdatedEvent
{
    AccountId: 12345,
    Balance: 10145.25,          // New balance after trade
    BonusBalance: 0.00,
    Equity: 10272.75,           // Balance + Unrealized P&L
    FreeMargin: 9912.60,        // Available for new trades
    MarginLevel: 2847.32        // Equity / Used Margin * 100
}

// StockSharp Portfolio Update
PortfolioChangeMessage
{
    PortfolioName: "CTrader_12345",
    Changes: {
        [PortfolioChangeTypes.BeginValue] = 10145.25m,
        [PortfolioChangeTypes.CurrentValue] = 10272.75m,
        [PortfolioChangeTypes.BlockedValue] = 360.15m,
        [PortfolioChangeTypes.VariationMargin] = 127.50m
    },
    ServerTime: DateTimeOffset.UtcNow
}
```

### Multi-Currency Support
```csharp
// Account with Multiple Currencies
ProtoOAAssetListRes
{
    Assets: [
        {AssetId: 1, Name: "USD", DisplayName: "US Dollar"},
        {AssetId: 2, Name: "EUR", DisplayName: "Euro"},
        {AssetId: 3, Name: "GBP", DisplayName: "British Pound"}
    ]
}

// Currency-Specific Balances
ProtoOATraderRes
{
    Trader: {
        AccountId: 12345,
        Balance: 10000.00,        // Base currency (USD)
        DepositAssetId: 1,        // USD
        CurrencyBalances: [
            {AssetId: 1, Balance: 10000.00},  // USD
            {AssetId: 2, Balance: 500.00},    // EUR
            {AssetId: 3, Balance: 200.00}     // GBP
        ]
    }
}
```

## Position Tracking

### Position Change Events
```csharp
// cTrader Position Update
ProtoOAPositionEvent
{
    PositionStatus: POSITION_STATUS_OPEN,
    Position: {
        PositionId: 555666777,
        SymbolId: 1001,
        TradeSide: BUY,
        Volume: 100000,           // Current position size
        EntryPrice: 108455,       // Average entry price
        CurrentPrice: 108582,     // Current market price
        UnrealizedPnL: 127.50,   // Mark-to-market P&L
        UsedMargin: 360.15,      // Margin locked for position
        Swap: -1.23,             // Overnight interest
        Commission: -8.50,       // Commission paid
        Label: "StockSharp"      // Position label
    }
}

// StockSharp Position Update
PositionChangeMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    PortfolioName: "CTrader_12345",
    Changes: {
        [PositionChangeTypes.CurrentValue] = 100000m,
        [PositionChangeTypes.AveragePrice] = 1.08455m,
        [PositionChangeTypes.CurrentPrice] = 1.08582m,
        [PositionChangeTypes.UnrealizedPnL] = 127.50m,
        [PositionChangeTypes.RealizedPnL] = 0m,
        [PositionChangeTypes.Commission] = 8.50m,
        [PositionChangeTypes.BlockedValue] = 360.15m
    },
    ServerTime: DateTimeOffset.UtcNow
}
```

### Position Lifecycle
```csharp
// Position States
POSITION_STATUS_OPEN:     // Active position
POSITION_STATUS_CLOSED:   // Position fully closed
POSITION_STATUS_CREATED:  // Position just opened

// Position Events
PositionOpened:
  Trigger: First trade execution
  Data: Initial position details

PositionModified:
  Trigger: Additional trades, market price changes
  Data: Updated volume, P&L, margin usage

PositionClosed:
  Trigger: Position fully closed
  Data: Final P&L, commission summary
```

## Margin and Risk Monitoring

### Margin Calculations
```csharp
// Margin Components
UsedMargin = Sum(Position.UsedMargin) for all open positions
FreeMargin = Equity - UsedMargin
MarginLevel = (Equity / UsedMargin) * 100

// Position Margin Calculation
PositionMargin = Volume * ConvertPrice(CurrentPrice) * MarginRate
where:
- Volume: Position size in base currency
- CurrentPrice: Current market price
- MarginRate: Instrument-specific margin requirement
```

### Risk Level Monitoring
```csharp
// Risk Thresholds
MarginCall: MarginLevel < 100%
  Action: Warning notification to user
  Effect: No new positions allowed

StopOut: MarginLevel < 50% (configurable)
  Action: Automatic position closure
  Priority: Close most losing positions first

// Risk Notifications
RiskLevelChangeMessage
{
    PortfolioName: "CTrader_12345",
    RiskLevel: RiskLevels.Warning,  // Normal, Warning, Critical
    MarginLevel: 95.5m,
    Message: "Margin level below 100% - no new positions allowed"
}
```

## Account Information

### Account Details
```csharp
// cTrader Account Info
ProtoOATraderRes
{
    Trader: {
        AccountId: 12345,
        IsLive: false,            // Demo account flag
        AccountType: HEDGED,      // Hedging vs Netting
        LeverageInCents: 10000,   // 100:1 leverage
        Balance: 10000.00,
        Equity: 10127.50,
        FreeMargin: 9767.35,
        MarginLevel: 2847.32,
        UnrealizedPnL: 127.50,
        DayPnL: 45.25            // Today's realized P&L
    }
}

// Account Metadata
AccountInformation
{
    AccountId: 12345,
    AccountName: "Demo Account",
    AccountType: "Hedged",
    BaseCurrency: "USD",
    Leverage: 100,
    IsDemo: true,
    ServerTime: "UTC",
    Company: "cTrader Demo",
    AccountBalance: 10000.00m
}
```

### Account History
```csharp
// Historical Balance Request
ProtoOADealListReq
{
    AccountId: 12345,
    FromTimestamp: StartOfDay,
    ToTimestamp: EndOfDay,
    MaxRows: 1000
}

// Balance History Response
ProtoOADealListRes
{
    Deals: [
        {
            DealId: 987654321,
            Volume: 100000,
            Price: 108455,
            Commission: -8.50,
            Swap: 0.00,
            Profit: 127.50,
            SymbolId: 1001,
            CreateTimestamp: 1640995200000,
            ExecutionTimestamp: 1640995201500
        }
    ]
}
```

## Performance Monitoring

### Portfolio Metrics
```csharp
// Performance Calculations
DailyPnL = Sum(RealizedPnL) for today's closed trades
WeeklyPnL = Sum(RealizedPnL) for this week
MonthlyPnL = Sum(RealizedPnL) for this month
TotalReturn = (CurrentEquity - InitialBalance) / InitialBalance

// Risk Metrics
MaxDrawdown = Max historic equity decline from peak
Sharpe Ratio = (Return - RiskFreeRate) / Volatility
Win Rate = WinningTrades / TotalTrades
Average Win = Sum(WinningTrades) / Count(WinningTrades)
Average Loss = Sum(LosingTrades) / Count(LosingTrades)
```

### Portfolio Statistics
```csharp
// Trading Statistics
PortfolioStatisticsMessage
{
    PortfolioName: "CTrader_12345",
    Statistics: {
        ["TotalTrades"] = 156,
        ["WinningTrades"] = 89,
        ["LosingTrades"] = 67,
        ["WinRate"] = 57.05,
        ["TotalPnL"] = 1275.50,
        ["DailyPnL"] = 45.25,
        ["MaxDrawdown"] = -234.75,
        ["LargestWin"] = 156.80,
        ["LargestLoss"] = -89.45
    },
    ServerTime: DateTimeOffset.UtcNow
}
```

## Error Handling

### Portfolio Data Synchronization
```csharp
// Data Inconsistency Detection
LocalEquity = Portfolio.CurrentValue
ServerEquity = ProtoOATrader.Equity

If (Math.Abs(LocalEquity - ServerEquity) > 0.01):
  Action: Request full portfolio reconciliation
  Log: "Portfolio equity mismatch detected"

// Reconciliation Process
1. Request fresh account data from server
2. Recalculate all position values
3. Update portfolio balances
4. Verify margin calculations
5. Notify user if discrepancies remain
```

### Missing Position Updates
```csharp
// Position Data Validation
ExpectedPositions = Local position cache
ServerPositions = ProtoOAGetPositionsRes

MissingPositions = ExpectedPositions - ServerPositions
ExtraPositions = ServerPositions - ExpectedPositions

If (MissingPositions.Any() || ExtraPositions.Any()):
  Action: Full position synchronization
  Update: Local cache to match server state
  Notify: User of position reconciliation
```

### Account Access Issues
```csharp
// Account Authorization Errors
ProtoOAErrorRes
{
    ErrorCode: "ACCOUNT_NOT_AUTHORIZED",
    Description: "Account 12345 not accessible"
}

// Error Response
ErrorMessage
{
    Error: new UnauthorizedAccessException("Account not accessible"),
    Type: MessageTypes.PortfolioLookup,
    OriginalTransactionId: 78901
}

// Recovery Actions
1. Verify account credentials
2. Check account status (active/blocked)
3. Request account list to confirm access
4. Notify user of access restrictions
```

## Data Consistency Rules

### Portfolio Balance Validation
```csharp
// Balance Equation Validation
Equity = Balance + UnrealizedPnL
FreeMargin = Equity - UsedMargin
MarginLevel = (UsedMargin > 0) ? (Equity / UsedMargin * 100) : 0

// Validation Rules
Equity >= 0 (for live accounts)
FreeMargin can be negative (over-margined)
UsedMargin >= Sum(Position.UsedMargin)
UnrealizedPnL = Sum(Position.UnrealizedPnL)
```

### Position Aggregation
```csharp
// Position Summary Validation
For each Symbol:
  NetVolume = Sum(Position.Volume * Side)
  where Side = +1 for BUY, -1 for SELL

  WeightedAvgPrice = Sum(Position.Volume * Position.EntryPrice) / Sum(Position.Volume)
  TotalMargin = Sum(Position.UsedMargin)
  TotalPnL = Sum(Position.UnrealizedPnL)

// Cross-validation
Portfolio.UnrealizedPnL = Sum(AllPositions.UnrealizedPnL)
Portfolio.UsedMargin = Sum(AllPositions.UsedMargin)
Portfolio.Commission = Sum(AllTrades.Commission)
```