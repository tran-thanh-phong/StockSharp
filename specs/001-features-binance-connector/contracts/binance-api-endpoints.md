# Binance API Endpoints Specification

## Base URLs
- **Spot Testnet**: `https://testnet.binance.vision`
- **Spot Production**: `https://api.binance.com`
- **Futures Testnet**: `https://testnet.binancefuture.com`
- **Futures Production**: `https://fapi.binance.com`

## Authentication
- **Method**: HMAC-SHA256 signature
- **Headers**:
  - `X-MBX-APIKEY`: API key
  - `signature`: HMAC-SHA256 signature of query string + request body
  - `timestamp`: Request timestamp in milliseconds
  - `recvWindow`: Request validity window (default: 5000ms)

## Rate Limits
- **REST API**: 1200 requests per minute (weight-based)
- **WebSocket**: 5 connections per IP, 1024 subscriptions per connection
- **Order Rate**: 10 orders per second, 100,000 orders per 24 hours

## Market Data Endpoints (Public)

### GET /api/v3/exchangeInfo (Spot) / /fapi/v1/exchangeInfo (Futures)
**Purpose**: Get exchange trading rules and symbol information
**Rate Limit**: 10 weight
**Response**: Symbol metadata including price/quantity filters

### GET /api/v3/depth (Spot) / /fapi/v1/depth (Futures)
**Purpose**: Get order book snapshot
**Parameters**:
- `symbol`: Trading pair (required)
- `limit`: Order book depth (5, 10, 20, 50, 100, 500, 1000, 5000)
**Rate Limit**: 1-50 weight based on limit

### GET /api/v3/trades (Spot) / /fapi/v1/aggTrades (Futures)
**Purpose**: Get recent trades
**Parameters**:
- `symbol`: Trading pair (required)
- `limit`: Number of trades (max 1000)
**Rate Limit**: 1 weight

### GET /api/v3/klines (Spot) / /fapi/v1/klines (Futures)
**Purpose**: Get candlestick/kline data
**Parameters**:
- `symbol`: Trading pair (required)
- `interval`: Kline interval (1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M)
- `startTime`: Start timestamp (optional)
- `endTime`: End timestamp (optional)
- `limit`: Number of klines (max 1000)
**Rate Limit**: 1 weight

### GET /api/v3/ticker/24hr (Spot) / /fapi/v1/ticker/24hr (Futures)
**Purpose**: Get 24hr ticker statistics
**Parameters**:
- `symbol`: Trading pair (optional, returns all if omitted)
**Rate Limit**: 1 weight per symbol, 40 weight for all symbols

## Account/Trading Endpoints (Private)

### GET /api/v3/account (Spot) / /fapi/v2/account (Futures)
**Purpose**: Get current account information
**Authentication**: Required
**Rate Limit**: 10 weight
**Response**: Account balances and trading permissions

### GET /api/v3/openOrders (Spot) / /fapi/v1/openOrders (Futures)
**Purpose**: Get all open orders
**Authentication**: Required
**Parameters**:
- `symbol`: Trading pair (optional)
**Rate Limit**: 3 weight per symbol, 40 weight for all symbols

### POST /api/v3/order (Spot) / /fapi/v1/order (Futures)
**Purpose**: Place new order
**Authentication**: Required
**Parameters**:
- `symbol`: Trading pair (required)
- `side`: BUY or SELL (required)
- `type`: Order type (LIMIT, MARKET, STOP_LOSS, TAKE_PROFIT, etc.)
- `quantity`: Order quantity (required)
- `price`: Order price (required for LIMIT orders)
- `stopPrice`: Stop price (required for stop orders)
- `timeInForce`: GTC, IOC, FOK (required for LIMIT orders)
- `newClientOrderId`: Client order ID (optional)
**Rate Limit**: 1 weight

### DELETE /api/v3/order (Spot) / /fapi/v1/order (Futures)
**Purpose**: Cancel active order
**Authentication**: Required
**Parameters**:
- `symbol`: Trading pair (required)
- `orderId`: Order ID (required if newClientOrderId not provided)
- `origClientOrderId`: Original client order ID (required if orderId not provided)
**Rate Limit**: 1 weight

### GET /api/v3/order (Spot) / /fapi/v1/order (Futures)
**Purpose**: Check order status
**Authentication**: Required
**Parameters**:
- `symbol`: Trading pair (required)
- `orderId`: Order ID (required if origClientOrderId not provided)
- `origClientOrderId`: Original client order ID (required if orderId not provided)
**Rate Limit**: 2 weight

### GET /api/v3/myTrades (Spot) / /fapi/v1/userTrades (Futures)
**Purpose**: Get trade history
**Authentication**: Required
**Parameters**:
- `symbol`: Trading pair (required)
- `startTime`: Start timestamp (optional)
- `endTime`: End timestamp (optional)
- `fromId`: Trade ID to start from (optional)
- `limit`: Number of trades (max 1000)
**Rate Limit**: 10 weight

## WebSocket Streams

### Market Data Streams (Public)
**Base URL**:
- Spot: `wss://stream.binance.com:9443/ws/`
- Futures: `wss://fstream.binance.com/ws/`

#### Individual Symbol Ticker
**Stream**: `<symbol>@ticker`
**Data**: 24hr ticker statistics updates

#### Trade Streams
**Stream**: `<symbol>@trade`
**Data**: Real-time trade executions

#### Order Book Streams
**Stream**: `<symbol>@depth<levels>` or `<symbol>@depth`
**Levels**: 5, 10, 20 (partial updates) or full depth
**Data**: Order book updates

#### Kline/Candlestick Streams
**Stream**: `<symbol>@kline_<interval>`
**Intervals**: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M
**Data**: Real-time kline updates

### User Data Streams (Private)
**Authentication**: Listen key required from POST /api/v3/userDataStream

#### Spot User Data Stream
**Stream**: User data stream with listen key
**Data Types**:
- `executionReport`: Order updates (new, filled, cancelled)
- `outboundAccountPosition`: Balance updates
- `balanceUpdate`: Individual balance changes

#### Futures User Data Stream
**Stream**: User data stream with listen key
**Data Types**:
- `ORDER_TRADE_UPDATE`: Order and trade updates
- `ACCOUNT_UPDATE`: Account balance and position updates
- `MARGIN_CALL`: Margin call warnings
- `ACCOUNT_CONFIG_UPDATE`: Leverage and margin type updates

## Error Codes

### General Errors
- `-1000`: Unknown error
- `-1001`: Disconnected
- `-1002`: Unauthorized
- `-1003`: Too many requests
- `-1006`: Unexpected response
- `-1007`: Timeout
- `-1014`: Unsupported order combination
- `-1015`: Too many orders
- `-1016`: Service shutting down
- `-1020`: Unsupported operation
- `-1021`: Invalid timestamp
- `-1022`: Invalid signature

### Order Errors
- `-2010`: New order rejected (REJECT)
- `-2011`: Order cancel rejected (CANCEL_REJECTED)
- `-2013`: Order does not exist (NO_SUCH_ORDER)
- `-2014`: API key format invalid
- `-2015`: Invalid API key, IP, or permissions
- `-2016`: No trading window could be found
- `-2018`: Balance insufficient
- `-2019`: Margin insufficient
- `-2021`: Order would immediately match and take
- `-2022`: Reduce-only order rejected

### Filter Errors
- `-1013`: Filter failure (INVALID_QUANTITY, MIN_NOTIONAL, PRICE_FILTER, etc.)
- `-1111`: Precision over maximum
- `-1112`: No orders on book for symbol
- `-1113`: Withdraw amount exceeds balance
- `-1114`: Time in force parameter invalid
- `-1115`: Invalid order type
- `-1116`: Invalid side
- `-1117`: Empty new client order ID
- `-1118`: Original client order ID is empty
- `-1119`: Bad interval
- `-1120`: Bad symbol
- `-1121`: Invalid listen key
- `-1125`: Invalid data sent for parameter
- `-1130`: Invalid data sent in parameter

## Message Formats

### REST Response Format
```json
{
  // Success response varies by endpoint
  // Error response:
  "code": -1021,
  "msg": "Timestamp for this request is outside of the recvWindow."
}
```

### WebSocket Message Format
```json
{
  "e": "executionReport",    // Event type
  "E": 1499405658658,        // Event time
  "s": "ETHBTC",            // Symbol
  "c": "mUvoqJxFIILMdfAW5iGSOW", // Client order ID
  "S": "BUY",               // Side
  "o": "LIMIT",             // Order type
  "f": "GTC",               // Time in force
  "q": "1.00000000",        // Order quantity
  "p": "0.10264410",        // Order price
  "P": "0.00000000",        // Stop price
  "F": "0.00000000",        // Iceberg quantity
  "g": -1,                  // OrderListId
  "C": "",                  // Original client order ID
  "x": "NEW",               // Current execution type
  "X": "NEW",               // Current order status
  "r": "NONE",              // Order reject reason
  "i": 4293153,             // Order ID
  "l": "0.00000000",        // Last executed quantity
  "z": "0.00000000",        // Cumulative filled quantity
  "L": "0.00000000",        // Last executed price
  "n": "0",                 // Commission amount
  "N": null,                // Commission asset
  "T": 1499405658657,       // Transaction time
  "t": -1,                  // Trade ID
  "I": 8641984,             // Ignore
  "w": true,                // Is the order on the book?
  "m": false,               // Is this trade the maker side?
  "M": false,               // Ignore
  "O": 1499405658657,       // Order creation time
  "Z": "0.00000000",        // Cumulative quote asset transacted quantity
  "Y": "0.00000000",        // Last quote asset transacted quantity
  "Q": "0.00000000"         // Quote Order Qty
}
```

## Implementation Notes

### Request Signing
1. Create query string from parameters (sorted alphabetically)
2. Create string to sign: timestamp + method + requestPath + queryString + body
3. Generate HMAC-SHA256 signature using API secret
4. Include signature in query parameters or request headers

### Timestamp Synchronization
- Server time must be synchronized within recvWindow (default 5000ms)
- Use GET /api/v3/time to get server time for synchronization
- Account for network latency when calculating timestamp

### WebSocket Connection Management
- Maintain heartbeat to keep connections alive
- Handle reconnection automatically on disconnect
- Subscribe to streams after connection establishment
- Use listen keys for private streams (expires every 24 hours, extend with PUT request)

### Rate Limit Handling
- Track request weights and timing
- Implement exponential backoff for 429 responses
- Respect X-MBX-USED-WEIGHT response headers
- Prioritize trading requests over market data requests