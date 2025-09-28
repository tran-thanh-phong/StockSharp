# Market Data Contract

## Subscription Management

### Market Data Subscription Request
```csharp
// StockSharp Request
MarketDataMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    DataType: DataType.Ticks | DataType.MarketDepth | DataType.Level1,
    IsSubscribe: true,
    TransactionId: 12345,
    From: DateTime? (for historical data),
    To: DateTime? (for historical data),
    Count: long? (max 10000 per clarification)
}

// Subscription Limits (from clarification)
MaxConcurrentSubscriptions: 10 instruments simultaneously
FutureTarget: 100 instruments simultaneously
```

### Real-time Market Data Streams

#### Tick Data (Trade Events)
```csharp
// cTrader Event
ProtoOASpotEvent
{
    SymbolId: 1001,
    Bid: 1.08451,
    Ask: 1.08454,
    TrendbarPeriod: M1,
    UtcTimestamp: 1640995200000
}

// StockSharp Output
ExecutionMessage
{
    DataTypeEx: DataType.Ticks,
    SecurityId: SecurityId("EURUSD", "CTrader"),
    TradePrice: 1.08452,  // Mid price or last trade
    TradeVolume: 1000000, // Standard lot
    ServerTime: DateTimeOffset.FromUnixTimeMilliseconds(1640995200000),
    OriginSide: Sides.Buy | Sides.Sell
}
```

#### Market Depth (Order Book)
```csharp
// cTrader Event
ProtoOADepthEvent
{
    SymbolId: 1001,
    NewQuotes: [
        {Side: BID, Price: 1.08450, Volume: 1000000},
        {Side: BID, Price: 1.08449, Volume: 2000000},
        {Side: ASK, Price: 1.08454, Volume: 1500000},
        {Side: ASK, Price: 1.08455, Volume: 2500000}
    ]
}

// StockSharp Output
QuoteChangeMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    Bids: [
        new QuoteChange(1.08450m, 1000000m),
        new QuoteChange(1.08449m, 2000000m)
    ],
    Asks: [
        new QuoteChange(1.08454m, 1500000m),
        new QuoteChange(1.08455m, 2500000m)
    ],
    ServerTime: DateTimeOffset.UtcNow,
    IsByLevel: true
}
```

#### Level 1 Data (Best Bid/Ask)
```csharp
// Derived from ProtoOASpotEvent
Level1ChangeMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    Changes: {
        [Level1Fields.BestBidPrice] = 1.08451m,
        [Level1Fields.BestAskPrice] = 1.08454m,
        [Level1Fields.BestBidVolume] = 1000000m,
        [Level1Fields.BestAskVolume] = 1000000m,
        [Level1Fields.LastTradePrice] = 1.08452m,
        [Level1Fields.LastTradeVolume] = 500000m
    },
    ServerTime: DateTimeOffset.UtcNow
}
```

## Historical Data Retrieval

### Candle Data Request
```csharp
// StockSharp Request
MarketDataMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    DataType: DataType.TimeFrame(TimeSpan.FromMinutes(1)),
    IsSubscribe: true,
    From: DateTime.Today.AddDays(-7),
    To: DateTime.Today,
    Count: null // Will be limited to 10000 by connector
}

// cTrader Request
ProtoOATrendbarsReq
{
    SymbolId: 1001,
    Period: M1,
    FromTimestamp: UnixTimestamp(From),
    ToTimestamp: UnixTimestamp(To),
    Count: Math.Min(requestedCount ?? 10000, 10000)
}
```

### Supported Timeframes
```csharp
// Available Timeframes (from clarification)
public static readonly TimeSpan[] SupportedTimeFrames = {
    TimeSpan.FromMinutes(1),   // M1
    TimeSpan.FromMinutes(5),   // M5
    TimeSpan.FromMinutes(15),  // M15
    TimeSpan.FromMinutes(30),  // M30
    TimeSpan.FromHours(1),     // H1
    TimeSpan.FromHours(4),     // H4
    TimeSpan.FromDays(1),      // D1
    TimeSpan.FromDays(7),      // W1
    TimeSpan.FromDays(30)      // MN1
};

// Data Point Limit (from clarification)
MaxDataPointsPerRequest: 10000
```

### Historical Candle Response
```csharp
// cTrader Response
ProtoOATrendbarsRes
{
    TrendBars: [
        {
            UtcTimestamp: 1640995200000,
            Open: 108451,      // Price in 1/100000 format
            High: 108465,
            Low: 108440,
            Close: 108459,
            Volume: 15000000   // Volume in units
        }
    ]
}

// StockSharp Output
TimeFrameCandleMessage
{
    SecurityId: SecurityId("EURUSD", "CTrader"),
    OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(1640995200000),
    OpenPrice: 1.08451m,
    HighPrice: 1.08465m,
    LowPrice: 1.08440m,
    ClosePrice: 1.08459m,
    TotalVolume: 15000000m,
    State: CandleStates.Finished,
    OriginalTransactionId: 12345
}
```

## Performance Contract

### Latency Requirements (from clarification)
```csharp
// Real-time Data Latency
Target: < 100ms from cTrader event to StockSharp delivery
Measurement: cTrader UtcTimestamp to StockSharp ServerTime

// Historical Data Response
Target: < 2 seconds for 1000 candles
Target: < 10 seconds for 10000 candles (max request)

// Subscription Response
Target: < 1 second to confirm subscription
Target: < 3 seconds to receive first market data
```

### Throughput Requirements
```csharp
// Market Data Processing
Target: 1000+ market updates per second per symbol
Target: 10+ symbols concurrent processing
Peak: Handle market open/close surge (5x normal volume)

// Memory Usage
Target: < 100MB for 10 concurrent subscriptions
Target: < 1GB for maximum subscription load (future: 100 symbols)
```

## Error Handling Contract

### Subscription Errors
```csharp
// Symbol Not Found
ProtoOAErrorRes
{
    ErrorCode: "SYMBOL_NOT_FOUND",
    Description: "Symbol INVALID does not exist"
}

// StockSharp Error Response
SubscriptionResponseMessage
{
    OriginalTransactionId: 12345,
    Error: new ArgumentException("Symbol not found: INVALID"),
    IsOk: false
}

// Subscription Limit Exceeded
When: More than 10 concurrent subscriptions requested
Response: Reject new subscription with error message
Action: Queue subscription for retry when slot available
```

### Market Data Quality Issues
```csharp
// Stale Data Detection
MaxDataAge: 5 seconds for active markets
StaleDataAction: Mark as suspicious, continue delivery
OldDataAction: Discard data older than 1 minute

// Data Validation
PriceValidation: Reject prices <= 0 or > 1000x previous price
VolumeValidation: Reject negative volumes
TimestampValidation: Reject future timestamps or very old data
```

### Network and Connection Issues
```csharp
// Connection Loss During Subscription
Event: TCP/WebSocket connection lost
Action:
1. Mark all subscriptions as "reconnecting"
2. Attempt automatic reconnection
3. Re-establish subscriptions on reconnection
4. Notify user of data gap

// Market Data Stream Interruption
Event: No market data received for 30+ seconds
Action:
1. Send heartbeat to test connection
2. Re-subscribe to affected symbols
3. Log warning for investigation
```

## Subscription State Management

### Subscription Lifecycle
```csharp
enum SubscriptionState
{
    Pending,        // Subscription requested
    Active,         // Receiving data
    Reconnecting,   // Connection lost, attempting recovery
    Paused,         // Temporarily stopped (market closed)
    Error,          // Subscription failed
    Cancelled       // User cancelled or unsubscribed
}

// State Transitions
Pending → Active (first data received)
Active → Reconnecting (connection lost)
Reconnecting → Active (connection restored)
Active → Paused (market closed)
Paused → Active (market opened)
Any → Cancelled (unsubscribe request)
Any → Error (unrecoverable failure)
```

### Subscription Recovery
```csharp
// Automatic Recovery Strategy
ConnectionLoss:
1. Detect connection loss within 30 seconds
2. Mark all subscriptions as "reconnecting"
3. Attempt reconnection with exponential backoff
4. Re-establish subscriptions in priority order
5. Resume normal data flow

ResubscriptionOrder:
1. Portfolio/account data (highest priority)
2. Active trading instrument subscriptions
3. Analysis/monitoring subscriptions
4. Historical data requests (lowest priority)
```

## Data Format Specifications

### Price Format Conversion
```csharp
// cTrader Price Encoding
CTraderPrice: int64 (price * 100000 for 5-digit currencies)
Examples:
- EUR/USD 1.08451 → 108451
- USD/JPY 110.123 → 11012300 (3-digit currency)
- BTC/USD 45123.45 → 4512345000 (2-digit precision)

// StockSharp Conversion
decimal ConvertPrice(long cTraderPrice, int digits)
{
    return cTraderPrice / Math.Pow(10, digits);
}
```

### Volume Format Conversion
```csharp
// cTrader Volume Encoding
Unit: Volume in base currency units
Examples:
- Forex: 1000000 = 1 standard lot (100,000 base currency)
- CFD: 1 = 1 contract/share
- Crypto: 1 = 1 coin/token

// StockSharp Volume
Always use decimal representation
Maintain original precision from cTrader
```

### Timestamp Handling
```csharp
// cTrader Timestamps
Format: Unix timestamp in milliseconds (UTC)
Example: 1640995200000 = 2022-01-01 00:00:00 UTC

// StockSharp Conversion
DateTimeOffset serverTime = DateTimeOffset.FromUnixTimeMilliseconds(cTraderTimestamp);

// Local Time Adjustment
Use DateTimeOffset to preserve timezone information
Convert to local time only for display purposes
Always use UTC for calculations and storage
```