# StockSharp Message Contracts for CryptoExchange Framework

## Message Flow Architecture

### Inbound Messages (StockSharp → CryptoExchange Adapter)

#### SecurityLookupMessage
**Purpose**: Request security information from exchange via CryptoExchange.Net
**Key Fields**:
- `SecurityId`: Optional, specific symbol lookup
- `SecurityType`: SecurityTypes.Stock (spot) or SecurityTypes.Future (futures)

**Processing**:
- Use Binance.Net GetExchangeInfoAsync() method
- MessageConverter transforms BinanceSymbol objects to SecurityMessage
- Filter by SecurityType if specified
- Leverage CryptoExchange.Net automatic caching and rate limiting

#### MarketDataMessage
**Purpose**: Subscribe/unsubscribe to market data streams
**Key Fields**:
- `SecurityId`: Target trading pair
- `DataType`: Type of data (Ticks, MarketDepth, CandleTimeFrame)
- `IsSubscribe`: true for subscribe, false for unsubscribe
- `From/To`: Historical data range (if applicable)

**Processing**:
- DataType.Ticks → Subscribe to `<symbol>@trade` WebSocket stream
- DataType.MarketDepth → Subscribe to `<symbol>@depth` WebSocket stream
- CandleTimeFrame → Subscribe to `<symbol>@kline_<interval>` WebSocket stream
- Historical data → Call appropriate REST endpoint

#### OrderRegisterMessage
**Purpose**: Place new trading order
**Key Fields**:
- `TransactionId`: Unique transaction ID for tracking
- `SecurityId`: Trading pair
- `Side`: Sides.Buy or Sides.Sell
- `OrderType`: Market, Limit, Stop, StopLimit
- `Volume`: Order quantity
- `Price`: Limit price (for limit orders)
- `StopPrice`: Stop trigger price (for stop orders)
- `TimeInForce`: GTC, IOC, FOK
- `ClientCode`: Optional client order ID

**Processing**:
- Validate order parameters against symbol filters
- Convert to Binance order format
- Call POST /api/v3/order (spot) or POST /fapi/v1/order (futures)
- Return ExecutionMessage with order confirmation

#### OrderCancelMessage
**Purpose**: Cancel existing order
**Key Fields**:
- `TransactionId`: Original transaction ID
- `OrderId`: StockSharp order ID
- `SecurityId`: Trading pair

**Processing**:
- Map StockSharp OrderId to Binance orderId
- Call DELETE /api/v3/order (spot) or DELETE /fapi/v1/order (futures)
- Return ExecutionMessage with cancellation result

#### OrderStatusMessage
**Purpose**: Query order status
**Key Fields**:
- `TransactionId`: Transaction ID
- `OrderId`: Order ID to check

**Processing**:
- Call GET /api/v3/order (spot) or GET /fapi/v1/order (futures)
- Return ExecutionMessage with current status

#### PortfolioLookupMessage
**Purpose**: Request account balance information
**Key Fields**:
- `PortfolioName`: Portfolio identifier
- `Currency`: Optional currency filter

**Processing**:
- Call GET /api/v3/account (spot) or GET /fapi/v2/account (futures)
- Convert balances to PortfolioChangeMessage responses

### Outbound Messages (Binance Adapter → StockSharp)

#### SecurityMessage
**Purpose**: Security definition from Binance exchange info
**Key Fields**:
- `SecurityId`: SecurityId with symbol and board code
- `Name`: Trading pair display name
- `SecurityType`: Stock (spot) or Future (futures)
- `PriceStep`: Minimum price increment (tickSize)
- `VolumeStep`: Minimum quantity increment (stepSize)
- `MinVolume`: Minimum order quantity
- `MaxVolume`: Maximum order quantity
- `State`: SecurityStates.Trading (active symbols only)

**Binance Source**: /api/v3/exchangeInfo symbols array
**Conversion Rules**:
- Symbol "BTCUSDT" → SecurityId{SecurityCode="BTCUSDT", BoardCode="BINANCE"}
- Extract price/quantity filters from symbol filters
- Only include TRADING status symbols

#### ExecutionMessage (Ticks)
**Purpose**: Real-time trade executions
**Key Fields**:
- `SecurityId`: Trading pair
- `TradeId`: Binance trade ID
- `TradePrice`: Execution price
- `TradeVolume`: Executed quantity
- `OriginSide`: Aggressor side (buy/sell)
- `ServerTime`: Trade timestamp
- `DataType`: DataType.Ticks

**Binance Source**: WebSocket `<symbol>@trade` stream
**Conversion Rules**:
- Map `m` field to OriginSide (true=sell, false=buy)
- Convert `T` timestamp to DateTimeOffset
- Use `t` as TradeId, `p` as TradePrice, `q` as TradeVolume

#### ExecutionMessage (Transactions)
**Purpose**: Order status updates and confirmations
**Key Fields**:
- `TransactionId`: StockSharp transaction ID
- `OrderId`: Binance order ID
- `SecurityId`: Trading pair
- `Side`: Buy/Sell
- `OrderType`: Order type
- `Volume`: Order quantity
- `Price`: Order price
- `Balance`: Remaining quantity
- `OrderState`: Order status
- `ServerTime`: Update timestamp
- `DataType`: DataType.Transactions

**Binance Sources**:
- REST API responses (order placement, cancellation, status)
- WebSocket `executionReport` events
**Conversion Rules**:
- Map Binance order status to OrderStates enum
- Use `i` as OrderId, `c` as ClientOrderId
- Calculate Balance as Volume - ExecutedQuantity

#### QuoteChangeMessage
**Purpose**: Order book depth updates
**Key Fields**:
- `SecurityId`: Trading pair
- `Bids`: Bid price/volume levels
- `Asks`: Ask price/volume levels
- `ServerTime`: Update timestamp
- `IsByLevel`: true (aggregated levels)

**Binance Source**: WebSocket `<symbol>@depth` streams
**Conversion Rules**:
- Convert `b` array to Bids, `a` array to Asks
- Sort bids descending by price, asks ascending by price
- Use `E` event time as ServerTime

#### PortfolioChangeMessage
**Purpose**: Account balance updates
**Key Fields**:
- `PortfolioName`: "BINANCE" or "BINANCE_TESTNET"
- `ClientCode`: Asset symbol (BTC, USDT, etc.)
- `CurrentValue`: Available balance
- `BlockedValue`: Locked balance
- `ServerTime`: Update timestamp

**Binance Sources**:
- GET /api/v3/account response
- WebSocket `outboundAccountPosition` events
**Conversion Rules**:
- Each balance becomes separate PortfolioChangeMessage
- Use `f` as CurrentValue, `l` as BlockedValue
- Set PortfolioName based on testnet/production mode

#### PositionChangeMessage (Futures Only)
**Purpose**: Position updates for futures contracts
**Key Fields**:
- `SecurityId`: Futures contract symbol
- `CurrentValue`: Position size (signed)
- `AveragePrice`: Entry price
- `UnrealizedPnL`: Mark-to-market P&L
- `ServerTime`: Update timestamp

**Binance Source**: WebSocket `ACCOUNT_UPDATE` events (futures)
**Conversion Rules**:
- Use `pa` as CurrentValue (position amount)
- Convert `ep` to AveragePrice, `up` to UnrealizedPnL
- Handle LONG/SHORT position sides appropriately

#### CandleMessage
**Purpose**: OHLCV candlestick data
**Key Fields**:
- `SecurityId`: Trading pair
- `OpenTime`: Candle start time
- `CloseTime`: Candle end time
- `OpenPrice`: Opening price
- `HighPrice`: High price
- `LowPrice`: Low price
- `ClosePrice`: Closing price
- `TotalVolume`: Volume traded
- `TimeFrame`: Candle duration

**Binance Sources**:
- GET /api/v3/klines (historical)
- WebSocket `<symbol>@kline_<interval>` (real-time)
**Conversion Rules**:
- Array format: [openTime, open, high, low, close, volume, closeTime, ...]
- Convert timestamps from milliseconds to DateTimeOffset
- Map interval string to TimeSpan

#### Level1ChangeMessage
**Purpose**: Best bid/offer updates
**Key Fields**:
- `SecurityId`: Trading pair
- `BestBidPrice`: Best bid price
- `BestBidVolume`: Best bid volume
- `BestAskPrice`: Best ask price
- `BestAskVolume`: Best ask volume
- `ServerTime`: Update timestamp

**Binance Source**: WebSocket `<symbol>@ticker` stream
**Conversion Rules**:
- Use `b` as BestBidPrice, `B` as BestBidVolume
- Use `a` as BestAskPrice, `A` as BestAskVolume
- Extract from 24hr ticker statistics

## Message Validation Rules

### Inbound Validation
1. **SecurityId Validation**:
   - SecurityCode must be valid Binance symbol format
   - BoardCode must match connector type (BINANCE/BINANCE_FUTURES)

2. **Order Validation**:
   - Volume must be within symbol min/max limits
   - Price must be multiple of tickSize
   - Volume must be multiple of stepSize
   - Notional value must meet minimum requirements

3. **Market Data Validation**:
   - DataType must be supported by connector
   - TimeFrame must match Binance intervals for candles
   - Historical ranges must be within API limits

### Outbound Validation
1. **Price/Volume Precision**:
   - Round to appropriate decimal places per symbol
   - Ensure no precision loss in conversions

2. **Timestamp Validation**:
   - All timestamps in UTC
   - Chronological ordering maintained
   - No time travel (future timestamps)

3. **Message Completeness**:
   - All required fields populated
   - Valid enum values used
   - Proper message type correlation

## Error Handling

### Error Message Mapping
- Use ErrorMessage for API errors
- Include Binance error code and description
- Set appropriate IsTransient flag for retry logic

### Connection State Messages
- ConnectMessage on successful authentication
- DisconnectMessage on connection loss or deliberate disconnect
- ResetMessage for reconnection scenarios

### Subscription Management
- Track active subscriptions per SecurityId
- Handle duplicate subscription requests gracefully
- Clean up subscriptions on disconnect

## Performance Optimizations

### Message Pooling
- Reuse message objects where possible
- Pre-allocate common message types
- Use object pooling for high-frequency messages (ticks, quotes)

### Batch Processing
- Group related updates into single messages when appropriate
- Minimize message fragmentation for order book updates
- Buffer small updates and send in batches

### Memory Efficiency
- Use appropriate data types for fields
- Minimize string allocations in hot paths
- Cache frequently used objects (SecurityId, symbols)