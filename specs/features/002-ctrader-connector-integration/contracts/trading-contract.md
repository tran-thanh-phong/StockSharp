# Trading Contract

## Order Management

### Order Registration
```csharp
// StockSharp Request
OrderRegisterMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    Side: Sides.Buy,
    Volume: 100000m,  // 0.1 lot
    Price: 1.08450m,  // Limit price (null for market orders)
    OrderType: OrderTypes.Limit,
    TimeInForce: TimeInForce.GoodTillCancel,
    TransactionId: 67890,
    TillDate: DateTimeOffset.MaxValue
}

// cTrader Translation
ProtoOANewOrderReq
{
    SymbolId: ResolveSymbolId("EURUSD"),
    OrderType: LIMIT,
    TradeSide: BUY,
    Volume: 100000,
    LimitPrice: 108450,  // Price * 100000
    TimeInForce: GTC,
    Comment: "StockSharp Order #67890"
}
```

### Supported Order Types
```csharp
// Order Type Mapping
StockSharp.OrderTypes → cTrader.OrderType
{
    Market → MARKET,
    Limit → LIMIT,
    Stop → STOP,
    StopLimit → STOP_LIMIT
}

// Time In Force Mapping
StockSharp.TimeInForce → cTrader.TimeInForce
{
    PutInQueue → GTC,           // Good Till Cancel
    FillOrKill → FOK,           // Fill Or Kill
    ImmediateOrCancel → IOC,    // Immediate Or Cancel
    CancelBalance → DAY         // Day order
}
```

### Order Response
```csharp
// cTrader Acceptance
ProtoOAExecutionEvent
{
    ExecutionType: ORDER_ACCEPTED,
    Order: {
        OrderId: 123456789,
        OrderStatus: ORDER_STATUS_ACCEPTED,
        RequestedVolume: 100000,
        FilledVolume: 0,
        UtcTimestamp: 1640995200000
    }
}

// StockSharp Confirmation
ExecutionMessage
{
    DataTypeEx: DataType.Transactions,
    OrderId: 123456789,
    OriginalTransactionId: 67890,
    OrderState: OrderStates.Active,
    OrderVolume: 100000m,
    Balance: 100000m,  // Remaining volume
    ServerTime: DateTimeOffset.FromUnixTimeMilliseconds(1640995200000),
    HasOrderInfo: true
}
```

## Order Execution

### Partial Fill Handling
```csharp
// cTrader Partial Fill
ProtoOAExecutionEvent
{
    ExecutionType: ORDER_PARTIALLY_FILLED,
    Order: {
        OrderId: 123456789,
        OrderStatus: ORDER_STATUS_PARTIALLY_FILLED,
        RequestedVolume: 100000,
        FilledVolume: 30000
    },
    Deal: {
        DealId: 987654321,
        Volume: 30000,
        FilledPrice: 108455
    }
}

// StockSharp Trade Notification
ExecutionMessage
{
    DataTypeEx: DataType.Transactions,
    OrderId: 123456789,
    TradeId: 987654321,
    TradePrice: 1.08455m,
    TradeVolume: 30000m,
    Balance: 70000m,  // Remaining: 100000 - 30000
    OrderState: OrderStates.Active,
    OriginalTransactionId: 67890
}
```

### Complete Fill
```csharp
// cTrader Complete Fill
ProtoOAExecutionEvent
{
    ExecutionType: ORDER_FILLED,
    Order: {
        OrderId: 123456789,
        OrderStatus: ORDER_STATUS_FILLED,
        RequestedVolume: 100000,
        FilledVolume: 100000
    },
    Deal: {
        DealId: 987654322,
        Volume: 70000,  // Remaining volume
        FilledPrice: 108460
    }
}

// StockSharp Final Trade + Order Done
ExecutionMessage[] = {
    // Trade execution
    new ExecutionMessage {
        DataTypeEx: DataType.Transactions,
        OrderId: 123456789,
        TradeId: 987654322,
        TradePrice: 1.08460m,
        TradeVolume: 70000m,
        OriginalTransactionId: 67890
    },
    // Order completion
    new ExecutionMessage {
        DataTypeEx: DataType.Transactions,
        OrderId: 123456789,
        Balance: 0m,
        OrderState: OrderStates.Done,
        HasOrderInfo: true,
        OriginalTransactionId: 67890
    }
}
```

## Order Cancellation

### User-Initiated Cancellation
```csharp
// StockSharp Cancel Request
OrderCancelMessage
{
    OrderId: 123456789,
    SecurityId: SecurityId("EURUSD", "CTrader"),
    TransactionId: 67891
}

// cTrader Cancel Request
ProtoOACancelOrderReq
{
    OrderId: 123456789
}

// cTrader Cancel Confirmation
ProtoOAExecutionEvent
{
    ExecutionType: ORDER_CANCELLED,
    Order: {
        OrderId: 123456789,
        OrderStatus: ORDER_STATUS_CANCELLED,
        RequestedVolume: 100000,
        FilledVolume: 30000  // Partial fill before cancel
    }
}

// StockSharp Cancel Confirmation
ExecutionMessage
{
    DataTypeEx: DataType.Transactions,
    OrderId: 123456789,
    Balance: 0m,  // No remaining volume
    OrderState: OrderStates.Failed,  // Cancelled orders map to Failed
    HasOrderInfo: true,
    OriginalTransactionId: 67891
}
```

## Order Rejection

### Validation Rejections
```csharp
// cTrader Rejection
ProtoOAErrorRes
{
    ErrorCode: "INVALID_VOLUME",
    Description: "Order volume 50 is below minimum volume 1000"
}

// StockSharp Rejection
ExecutionMessage
{
    DataTypeEx: DataType.Transactions,
    OriginalTransactionId: 67890,
    OrderState: OrderStates.Failed,
    Error: new ArgumentException("Order volume 50 is below minimum volume 1000"),
    HasOrderInfo: true
}
```

### Common Rejection Reasons
```csharp
// Volume Validation
MinVolumeViolation: Volume < Symbol.MinVolume
MaxVolumeViolation: Volume > Symbol.MaxVolume
VolumeStepViolation: Volume % Symbol.VolumeStep != 0

// Price Validation
InvalidPrice: Price <= 0 for limit orders
PriceOutOfRange: Price too far from current market
StopPriceInvalid: Stop price in wrong direction

// Account Validation
InsufficientFunds: Not enough margin for position
AccountBlocked: Trading disabled for account
MarketClosed: Trading outside market hours
RiskLimitExceeded: Position size exceeds risk limits
```

## Position Management

### Position Updates
```csharp
// cTrader Position Event
ProtoOAPositionEvent
{
    Position: {
        PositionId: 555666777,
        SymbolId: 1001,
        TradeSide: BUY,
        Volume: 100000,
        Price: 108455,        // Average entry price
        Swap: -1.23,         // Overnight interest
        Commission: -8.50,   // Total commission
        UnrealizedPnL: 127.50,
        UsedMargin: 360.15
    }
}

// StockSharp Position Update
PositionChangeMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    Changes: {
        [PositionChangeTypes.CurrentValue] = 100000m,
        [PositionChangeTypes.AveragePrice] = 1.08455m,
        [PositionChangeTypes.UnrealizedPnL] = 127.50m,
        [PositionChangeTypes.Commission] = 8.50m,
        [PositionChangeTypes.BlockedValue] = 360.15m  // Used margin
    },
    ServerTime: DateTimeOffset.UtcNow
}
```

### Position Closing
```csharp
// Close Position Order
OrderRegisterMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    Side: Sides.Sell,  // Opposite of position side
    Volume: 100000m,   // Full position size
    OrderType: OrderTypes.Market,
    TransactionId: 67892
}

// Position Closed Event
ProtoOAPositionEvent
{
    Position: {
        PositionId: 555666777,
        Volume: 0,           // Position closed
        RealizedPnL: 145.27  // Final P&L
    }
}
```

## Portfolio Tracking

### Portfolio Updates
```csharp
// cTrader Trader Event
ProtoOATraderEvent
{
    Trader: {
        TraderId: 12345,
        Balance: 10000.00,
        NonWithdrawableBonus: 0.00
    }
}

// StockSharp Portfolio Message
PortfolioMessage
{
    PortfolioName: "CTrader_12345",
    BeginValue: 10000.00m,
    CurrentValue: 10127.50m,  // Balance + Unrealized P&L
    BoardCode: BoardCodes.CTrader,
    Currency: CurrencyTypes.USD
}
```

### Account Balance Components
```csharp
// Portfolio Calculation
Balance: Cash available in account
UnrealizedPnL: Sum of all position unrealized P&L
RealizedPnL: Today's realized profit/loss
UsedMargin: Total margin locked in positions
FreeMargin: Available margin for new trades
Equity: Balance + UnrealizedPnL

// Margin Level Calculation
MarginLevel = (Equity / UsedMargin) * 100
MarginCall: MarginLevel < 100%
StopOut: MarginLevel < 20% (positions auto-closed)
```

## Risk Management

### Order Validation Rules
```csharp
// Pre-trade Risk Checks
MarginCheck:
  RequiredMargin = Volume * Price * MarginRate
  Available = Portfolio.FreeMargin
  Validate: RequiredMargin <= Available

VolumeCheck:
  Validate: MinVolume <= Volume <= MaxVolume
  Validate: Volume % VolumeStep == 0

ExposureCheck:
  CurrentExposure = Sum(Position.Volume) for Symbol
  NewExposure = CurrentExposure + Order.Volume
  Validate: NewExposure <= MaxExposurePerSymbol
```

### Position Limits
```csharp
// Account-Level Limits
MaxPositionsPerAccount: 100
MaxExposurePerSymbol: Configurable per symbol
MaxTotalExposure: Account balance * MaxLeverage

// Order Limits
MaxOrdersPerSecond: 10 (rate limiting)
MaxPendingOrders: 50 per account
MaxOrderVolume: Symbol-specific maximum
```

## Error Recovery

### Order State Synchronization
```csharp
// On Reconnection
OrderStatusMessage
{
    TransactionId: 0,  // System-initiated
    IsSubscribe: true
}

// Response: All Active Orders
ProtoOAReconcileRes
{
    Orders: [
        // All pending and partially filled orders
    ],
    Positions: [
        // All open positions
    ]
}

// Synchronization Process
1. Compare local order cache with server state
2. Update missing or changed orders
3. Cancel orders that no longer exist on server
4. Reconcile position data
5. Update portfolio balances
```

### Trade Confirmation Validation
```csharp
// Trade Validation Rules
TradeIdUniqueness: Each trade ID must be unique
VolumeConsistency: Trade volume <= remaining order volume
PriceReasonableness: Trade price within market range
TimestampValidation: Trade time within order lifetime

// Reconciliation on Mismatch
If (LocalOrderState != ServerOrderState):
1. Log discrepancy for investigation
2. Update local state to match server
3. Notify user of unexpected state change
4. Request full order history if needed
```

### Failed Order Recovery
```csharp
// Order Timeout Handling
OrderTimeout: 30 seconds without response
Action:
1. Query order status from server
2. If not found: Mark as failed locally
3. If found: Update to current server state
4. Notify user of timeout and resolution

// Duplicate Order Prevention
TransactionIdTracking: Maintain map of TransactionId → OrderId
DuplicateDetection: Reject orders with same TransactionId
OrderIdempotency: Same order details = same result
```