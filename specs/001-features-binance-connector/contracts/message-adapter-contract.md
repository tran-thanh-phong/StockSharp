# MessageAdapter Contract: Binance Connector

## Interface Contract: IMessageAdapter

### Connection Management

#### ConnectAsync
**Input**: ConnectMessage
- TransactionId: long
- Error: Exception (optional, null for connect requests)

**Output**: ConnectMessage
- TransactionId: long (matches input)
- Error: Exception (null on success, populated on failure)

**Contract**:
- MUST authenticate with Binance using provided API credentials
- MUST establish both REST and WebSocket connections
- MUST validate API permissions (spot trading, futures if enabled)
- MUST emit ConnectMessage with Error=null on successful connection
- MUST emit ConnectMessage with Error populated on connection failure
- Connection timeout MUST NOT exceed 30 seconds

#### DisconnectAsync
**Input**: DisconnectMessage
- TransactionId: long

**Output**: DisconnectMessage
- TransactionId: long (matches input)

**Contract**:
- MUST gracefully close all WebSocket subscriptions
- MUST dispose REST and WebSocket clients
- MUST cancel all pending operations
- MUST emit DisconnectMessage when complete
- Disconnection MUST complete within 10 seconds

### Market Data Subscription

#### Subscribe to Ticks
**Input**: MarketDataMessage
- DataType: DataType.Ticks
- SecurityId: SecurityId (Binance symbol)
- IsSubscribe: true

**Output Stream**: ExecutionMessage
- DataTypeEx: DataType.Ticks
- SecurityId: SecurityId (matches subscription)
- TradeId: long (Binance trade ID)
- TradePrice: decimal (execution price)
- TradeVolume: decimal (trade quantity)
- OriginSide: Sides (Buy/Sell based on aggressor)
- ServerTime: DateTimeOffset (Binance timestamp)

**Contract**:
- MUST subscribe to Binance trade stream for specified symbol
- MUST emit ExecutionMessage for each trade update
- MUST maintain chronological order of trades
- MUST handle reconnection and re-subscription automatically
- Subscription latency MUST be <100ms
- Trade message latency MUST be <50ms from Binance timestamp

#### Subscribe to Market Depth
**Input**: MarketDataMessage
- DataType: DataType.MarketDepth
- SecurityId: SecurityId (Binance symbol)
- IsSubscribe: true

**Output Stream**: QuoteChangeMessage
- SecurityId: SecurityId (matches subscription)
- Bids: Quote[] (price descending order)
- Asks: Quote[] (price ascending order)
- ServerTime: DateTimeOffset (Binance timestamp)
- IsByLevel: true

**Contract**:
- MUST subscribe to Binance order book stream
- MUST maintain full depth snapshot (20+ levels minimum)
- MUST apply incremental updates correctly
- MUST emit QuoteChangeMessage on each depth update
- Depth updates MUST maintain price/time priority
- MUST handle order book synchronization errors

### Order Management

#### Register Order
**Input**: OrderRegisterMessage
- TransactionId: long (unique transaction identifier)
- SecurityId: SecurityId (Binance symbol)
- Side: Sides (Buy/Sell)
- OrderType: OrderTypes (Market, Limit, StopLoss, StopLimit)
- Volume: decimal (order quantity)
- Price: decimal (limit price, null for market orders)
- StopPrice: decimal (stop trigger price for stop orders)
- TimeInForce: TimeInForce (GTC, IOC, FOK)

**Output**: ExecutionMessage
- DataTypeEx: DataType.Transactions
- OriginalTransactionId: long (matches OrderRegisterMessage.TransactionId)
- OrderId: long (Binance order ID)
- OrderState: OrderStates (Pending → Active/Failed)
- ServerTime: DateTimeOffset (order acknowledgment time)
- HasOrderInfo: true

**Contract**:
- MUST validate order parameters against Security constraints
- MUST submit order to appropriate Binance API (spot/futures)
- MUST emit ExecutionMessage with order acknowledgment
- MUST handle order rejection with appropriate error details
- Order registration latency MUST be <200ms
- MUST assign unique OrderId from Binance response

#### Cancel Order
**Input**: OrderCancelMessage
- TransactionId: long
- OrderId: long (Binance order ID to cancel)

**Output**: ExecutionMessage
- DataTypeEx: DataType.Transactions
- OriginalTransactionId: long (matches OrderCancelMessage.TransactionId)
- OrderId: long (Binance order ID)
- OrderState: OrderStates.Done
- ServerTime: DateTimeOffset
- HasOrderInfo: true

**Contract**:
- MUST cancel specified order via Binance REST API
- MUST emit ExecutionMessage confirming cancellation
- MUST handle cancellation of already filled orders gracefully
- Cancellation latency MUST be <100ms
- MUST return appropriate error if order not found

### Account Information

#### Portfolio Request
**Input**: PortfolioMessage
- TransactionId: long
- PortfolioName: string ("Binance" or "BinanceTestnet")
- IsSubscribe: true

**Output**: PortfolioChangeMessage
- PortfolioName: string (matches request)
- BeginValue: decimal (account starting value)
- CurrentValue: decimal (current total value)
- BlockedValue: decimal (locked funds)
- Commission: decimal (total fees paid)
- Currency: string (base currency, typically USDT)

**Contract**:
- MUST query Binance account information
- MUST emit PortfolioChangeMessage with current balances
- MUST subscribe to account update stream for real-time changes
- MUST aggregate multi-asset portfolio into single currency value
- Portfolio update latency MUST be <500ms

#### Position Request
**Input**: PositionMessage
- TransactionId: long
- SecurityId: SecurityId (optional, null for all positions)

**Output Stream**: PositionChangeMessage
- SecurityId: SecurityId (instrument identifier)
- CurrentValue: decimal (position size, signed)
- AveragePrice: decimal (average entry price)
- UnrealizedPnL: decimal (mark-to-market P&L)
- BlockedValue: decimal (locked in orders)

**Contract**:
- MUST query Binance position information
- MUST emit PositionChangeMessage for each non-zero position
- MUST calculate unrealized P&L using current market prices
- MUST update positions in real-time via WebSocket streams
- Position update latency MUST be <200ms

### Error Handling

#### Error Message Format
**Output**: ErrorMessage
- Error: Exception
- OriginalTransactionId: long (if applicable)

**Contract**:
- MUST classify errors by type (Network, API, Authentication, RateLimit)
- MUST provide actionable error descriptions
- MUST implement exponential backoff for rate limit errors
- MUST trigger reconnection for network errors
- Authentication errors MUST stop adapter operation
- MUST log all errors for debugging purposes

### Performance Requirements

#### Latency Targets
- Connection establishment: <30 seconds
- Market data subscription: <100ms
- Order registration: <200ms
- Order cancellation: <100ms
- Portfolio/position queries: <500ms
- Market data updates: <50ms from exchange

#### Throughput Targets
- Market data processing: >10,000 messages/second
- Order operations: >100 orders/second
- WebSocket message processing: >1,000 updates/second
- Concurrent symbol subscriptions: >500 symbols

#### Resource Constraints
- Memory usage: <100MB per connector instance
- CPU usage: <10% single core average
- Network connections: 2 (REST + WebSocket)
- WebSocket streams: <1024 (Binance limit)