# Data Model: cTrader Connector Integration

## Entity Definitions

### CTraderSecurityInfo
**Purpose**: Represents tradeable financial instruments with cTrader-specific metadata

**Fields**:
- `SymbolId` (long): Unique cTrader symbol identifier
- `SymbolName` (string): Display name (e.g., "EUR/USD", "Bitcoin CFD")
- `AssetClass` (string): Type classification (FOREX, CFD_INDEX, CFD_COMMODITY, CFD_CRYPTO)
- `BaseAsset` (string): Base currency/asset code
- `QuoteAsset` (string): Quote currency/asset code
- `PipValue` (decimal): Value of one pip movement
- `MinVolume` (decimal): Minimum order volume
- `VolumeStep` (decimal): Volume increment step
- `MaxVolume` (decimal): Maximum order volume
- `MarginRate` (decimal): Margin requirement percentage
- `SwapLong` (decimal): Overnight swap for long positions
- `SwapShort` (decimal): Overnight swap for short positions
- `TradingSchedule` (object): Market hours and session information

**Relationships**:
- Maps to StockSharp `SecurityMessage`
- Contains cTrader-specific trading specifications

**Validation Rules**:
- SymbolId must be positive
- Volume constraints: MinVolume ≤ VolumeStep ≤ MaxVolume
- Margin rate must be between 0 and 1

### CTraderMarketData
**Purpose**: Real-time and historical price information with high-frequency updates

**Fields**:
- `SymbolId` (long): Reference to CTraderSecurityInfo
- `Timestamp` (DateTimeOffset): Server timestamp with timezone
- `Bid` (decimal): Best bid price
- `Ask` (decimal): Best ask price
- `LastPrice` (decimal): Last trade price
- `Volume` (decimal): Trade volume
- `TickDirection` (enum): Price movement direction (Up, Down, Unchanged)
- `Depth` (List<QuoteLevel>): Order book levels
- `DayHigh` (decimal): Daily high price
- `DayLow` (decimal): Daily low price
- `DayVolume` (decimal): Daily total volume

**Relationships**:
- References CTraderSecurityInfo by SymbolId
- Maps to StockSharp `QuoteChangeMessage` and `ExecutionMessage`

**State Transitions**:
- Inactive → Active (first market data received)
- Active → Stale (no updates within timeout period)
- Stale → Active (fresh data received)
- Active → Inactive (market closed or unsubscribed)

### CTraderOrder
**Purpose**: Trading instruction with complete lifecycle tracking

**Fields**:
- `OrderId` (long): Unique cTrader order identifier
- `ClientOrderId` (string): StockSharp transaction ID reference
- `SymbolId` (long): Reference to trading instrument
- `OrderType` (enum): MARKET, LIMIT, STOP, STOP_LIMIT
- `TradeSide` (enum): BUY, SELL
- `RequestedVolume` (decimal): Original order quantity
- `ExecutedVolume` (decimal): Filled quantity
- `RemainingVolume` (decimal): Unfilled quantity
- `OrderPrice` (decimal?): Limit price (null for market orders)
- `StopPrice` (decimal?): Stop trigger price
- `TimeInForce` (enum): IOC, FOK, GTC, DAY
- `OrderStatus` (enum): PENDING, FILLED, PARTIALLY_FILLED, CANCELLED, REJECTED
- `CreatedTime` (DateTimeOffset): Order creation timestamp
- `ModifiedTime` (DateTimeOffset?): Last modification timestamp
- `ExecutedTime` (DateTimeOffset?): Execution completion timestamp
- `RejectReason` (string?): Rejection explanation if applicable

**Relationships**:
- References CTraderSecurityInfo by SymbolId
- Has many CTraderTrade executions
- Maps to StockSharp `ExecutionMessage` with DataType.Transactions

**State Transitions**:
```
PENDING → FILLED (full execution)
PENDING → PARTIALLY_FILLED → FILLED (partial then complete)
PENDING → CANCELLED (user cancellation)
PENDING → REJECTED (broker rejection)
PARTIALLY_FILLED → CANCELLED (partial fill then cancel)
```

**Validation Rules**:
- RequestedVolume must be positive
- ExecutedVolume ≤ RequestedVolume
- RemainingVolume = RequestedVolume - ExecutedVolume
- OrderPrice required for LIMIT and STOP_LIMIT orders
- StopPrice required for STOP and STOP_LIMIT orders

### CTraderTrade
**Purpose**: Individual trade execution records with commission details

**Fields**:
- `TradeId` (long): Unique trade execution identifier
- `OrderId` (long): Reference to parent order
- `SymbolId` (long): Trading instrument
- `TradeSide` (enum): BUY, SELL
- `ExecutedVolume` (decimal): Trade quantity
- `ExecutedPrice` (decimal): Execution price
- `Commission` (decimal): Trading commission charged
- `Swap` (decimal): Overnight interest charge/credit
- `Profit` (decimal): Realized profit/loss
- `ExecutedTime` (DateTimeOffset): Trade execution timestamp
- `Comment` (string?): Additional trade notes

**Relationships**:
- References CTraderOrder by OrderId
- References CTraderSecurityInfo by SymbolId
- Maps to StockSharp `ExecutionMessage` with trade details

**Validation Rules**:
- ExecutedVolume must be positive
- ExecutedPrice must be positive
- Trade timestamp must be within order lifecycle

### CTraderPosition
**Purpose**: Current holdings in specific instruments with P&L tracking

**Fields**:
- `PositionId` (long): Unique position identifier
- `SymbolId` (long): Trading instrument reference
- `TradeSide` (enum): LONG, SHORT
- `Volume` (decimal): Position size (absolute value)
- `AveragePrice` (decimal): Volume-weighted average entry price
- `CurrentPrice` (decimal): Current market price
- `UnrealizedPnL` (decimal): Mark-to-market profit/loss
- `RealizedPnL` (decimal): Closed profit/loss
- `Swap` (decimal): Accumulated overnight charges/credits
- `Commission` (decimal): Total commission paid
- `MarginUsed` (decimal): Margin requirement for position
- `OpenTime` (DateTimeOffset): Position opening timestamp
- `ModifiedTime` (DateTimeOffset): Last update timestamp

**Relationships**:
- References CTraderSecurityInfo by SymbolId
- Maps to StockSharp `PositionChangeMessage`

**State Transitions**:
- Closed → Open (first trade execution)
- Open → Modified (additional trades or market price changes)
- Open → Closed (position fully closed)

**Validation Rules**:
- Volume must be non-negative (zero for closed positions)
- AveragePrice must be positive for open positions
- UnrealizedPnL calculation: (CurrentPrice - AveragePrice) × Volume × (LONG ? 1 : -1)

### CTraderPortfolio
**Purpose**: Account-level information with multi-currency balance support

**Fields**:
- `AccountId` (long): cTrader account identifier
- `AccountName` (string): Display name for account
- `BaseCurrency` (string): Account base currency (USD, EUR, etc.)
- `Balance` (decimal): Account cash balance
- `Equity` (decimal): Balance + unrealized P&L
- `FreeMargin` (decimal): Available margin for new trades
- `UsedMargin` (decimal): Margin locked in open positions
- `MarginLevel` (decimal): Equity / Used Margin percentage
- `UnrealizedPnL` (decimal): Total unrealized profit/loss
- `DayPnL` (decimal): Today's realized profit/loss
- `LastUpdateTime` (DateTimeOffset): Last balance update timestamp
- `IsDemo` (bool): Demo account flag

**Relationships**:
- Has many CTraderPosition records
- Maps to StockSharp `PortfolioMessage`

**Validation Rules**:
- Balance must not be negative for live accounts
- Equity = Balance + UnrealizedPnL
- FreeMargin = Equity - UsedMargin
- MarginLevel = (UsedMargin > 0) ? (Equity / UsedMargin) × 100 : 0

## Message Flow Mappings

### Market Data Flow
```
cTrader ProtoOASpotEvent
├── Bid/Ask → QuoteChangeMessage.Bids[0]/Asks[0]
├── LastPrice → ExecutionMessage.TradePrice
└── Volume → ExecutionMessage.TradeVolume

cTrader ProtoOADepthEvent
├── BidDepth[] → QuoteChangeMessage.Bids[]
└── AskDepth[] → QuoteChangeMessage.Asks[]

cTrader ProtoOATrendBar
├── OHLC → TimeFrameCandleMessage.OHLC
├── Volume → TimeFrameCandleMessage.TotalVolume
└── Timestamp → TimeFrameCandleMessage.OpenTime
```

### Trading Flow
```
StockSharp OrderRegisterMessage
├── SecurityId → cTrader SymbolId lookup
├── Side → TradeSide
├── Volume → RequestedVolume
├── Price → OrderPrice
└── OrderType → cTrader order type

cTrader ProtoOAExecutionEvent
├── Order details → ExecutionMessage (Transactions)
├── Trade details → ExecutionMessage (Trades)
└── Position updates → PositionChangeMessage
```

### Portfolio Flow
```
cTrader ProtoOATrader
├── Balance → PortfolioMessage.BeginValue
├── Equity → PortfolioMessage.CurrentValue
└── Margin info → Custom portfolio attributes

cTrader ProtoOAPosition
├── Position data → PositionChangeMessage
├── PnL calculations → Position attributes
└── Margin usage → Position.BlockedValue
```

## Data Consistency Rules

### Cross-Entity Consistency
1. **Order-Trade Consistency**: Sum of CTraderTrade.ExecutedVolume must equal CTraderOrder.ExecutedVolume
2. **Position-Trade Consistency**: CTraderPosition volume must reflect net CTraderTrade executions
3. **Portfolio-Position Consistency**: Portfolio UnrealizedPnL must equal sum of all position UnrealizedPnL
4. **Symbol Reference Integrity**: All SymbolId references must exist in CTraderSecurityInfo

### Temporal Consistency
1. **Order Lifecycle**: Order.CreatedTime ≤ Order.ModifiedTime ≤ Order.ExecutedTime
2. **Trade Timing**: Trade.ExecutedTime must be within parent Order lifecycle
3. **Position Updates**: Position.ModifiedTime must reflect latest trade execution time
4. **Market Data Freshness**: Market data timestamps must be within acceptable latency window

### Business Logic Constraints
1. **Margin Requirements**: Portfolio.UsedMargin ≥ sum of all Position.MarginUsed
2. **Balance Validation**: Portfolio.Equity = Portfolio.Balance + Portfolio.UnrealizedPnL
3. **Volume Constraints**: All volumes must respect security-specific MinVolume/MaxVolume/VolumeStep
4. **Price Validation**: All prices must be positive and within reasonable market ranges

## Concurrency and Thread Safety

### Read-Write Patterns
- **Market Data**: High-frequency writes, concurrent reads (lock-free where possible)
- **Orders**: Moderate-frequency writes with immediate read consistency required
- **Positions**: Low-frequency writes with strong consistency requirements
- **Portfolio**: Periodic writes with eventual consistency acceptable

### Synchronization Strategy
- Use concurrent collections for market data caching
- Lock-based synchronization for order state changes
- Message-based updates for position and portfolio consistency
- Immutable objects for historical data records