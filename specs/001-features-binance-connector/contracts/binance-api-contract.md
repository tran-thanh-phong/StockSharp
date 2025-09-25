# Binance API Contract: Integration Requirements

## REST API Contracts

### Authentication
**Endpoint**: All authenticated endpoints
**Headers Required**:
- X-MBX-APIKEY: string (API key)
- signature: string (HMAC SHA256 signature)
- timestamp: long (request timestamp)

**Contract**:
- MUST include timestamp within 5000ms of server time
- MUST generate signature using API secret and query parameters
- MUST handle API key permissions (spot trading, futures, margin)
- Invalid authentication MUST return HTTP 401 with specific error code

### Account Information
**Endpoint**: GET /api/v3/account
**Response Schema**:
```json
{
  "makerCommission": 15,
  "takerCommission": 15,
  "buyerCommission": 0,
  "sellerCommission": 0,
  "canTrade": true,
  "canWithdraw": true,
  "canDeposit": true,
  "balances": [
    {
      "asset": "BTC",
      "free": "4723846.89208129",
      "locked": "0.00000000"
    }
  ]
}
```

**Contract**:
- MUST return all non-zero balances
- free + locked = total balance for each asset
- MUST respect rate limit: 10 requests/minute
- MUST handle insufficient permissions error

### Order Placement
**Endpoint**: POST /api/v3/order
**Request Parameters**:
- symbol: string (e.g., "BTCUSDT")
- side: "BUY" | "SELL"
- type: "MARKET" | "LIMIT" | "STOP_LOSS" | "STOP_LOSS_LIMIT"
- quantity: decimal
- price: decimal (required for LIMIT orders)
- stopPrice: decimal (required for STOP orders)
- timeInForce: "GTC" | "IOC" | "FOK"

**Response Schema**:
```json
{
  "symbol": "BTCUSDT",
  "orderId": 28,
  "orderListId": -1,
  "clientOrderId": "6gCrw2kRUAF9CvJDGP16IP",
  "transactTime": 1507725176595,
  "price": "0.00000000",
  "origQty": "10.00000000",
  "executedQty": "0.00000000",
  "cummulativeQuoteQty": "0.00000000",
  "status": "NEW",
  "timeInForce": "GTC",
  "type": "MARKET",
  "side": "SELL"
}
```

**Contract**:
- MUST validate quantity against symbol LOT_SIZE filter
- MUST validate price against PRICE_FILTER
- MUST return unique orderId for tracking
- Rate limit: 1200 requests/minute
- MUST handle insufficient balance errors

### Order Cancellation
**Endpoint**: DELETE /api/v3/order
**Request Parameters**:
- symbol: string
- orderId: long

**Response Schema**:
```json
{
  "symbol": "LTCBTC",
  "origClientOrderId": "myOrder1",
  "orderId": 4,
  "orderListId": -1,
  "clientOrderId": "cancelMyOrder1",
  "price": "2.00000000",
  "origQty": "1.00000000",
  "executedQty": "0.00000000",
  "cummulativeQuoteQty": "0.00000000",
  "status": "CANCELED",
  "timeInForce": "GTC",
  "type": "LIMIT",
  "side": "BUY"
}
```

**Contract**:
- MUST cancel order if status allows cancellation
- MUST return error for already filled orders
- MUST handle order not found scenarios
- Rate limit: 1200 requests/minute

## WebSocket Stream Contracts

### Trade Stream
**Stream**: <symbol>@trade
**Message Schema**:
```json
{
  "e": "trade",
  "E": 123456789,
  "s": "BNBBTC",
  "t": 12345,
  "p": "0.001",
  "q": "100",
  "b": 88,
  "a": 50,
  "T": 123456785,
  "m": true,
  "M": true
}
```

**Field Mapping**:
- e: event type ("trade")
- E: event time (timestamp)
- s: symbol
- t: trade ID
- p: price
- q: quantity
- b: buyer order ID
- a: seller order ID
- T: trade time
- m: is buyer maker (true = sell order filled)

**Contract**:
- MUST emit trade message for each execution
- MUST maintain trade ID sequence
- MUST provide buyer/seller determination via 'm' field
- Connection MUST auto-reconnect on disconnect

### Order Book Depth Stream
**Stream**: <symbol>@depth20@100ms
**Message Schema**:
```json
{
  "lastUpdateId": 160,
  "bids": [
    ["0.0024", "10"]
  ],
  "asks": [
    ["0.0026", "100"]
  ]
}
```

**Contract**:
- MUST provide 20-level depth snapshot
- MUST update every 100ms or on change
- Bids MUST be sorted price descending
- Asks MUST be sorted price ascending
- MUST handle missed updates via REST API resync

### User Data Stream
**Stream**: User-specific stream with listenKey
**Order Update Message**:
```json
{
  "e": "executionReport",
  "E": 1499405658658,
  "s": "ETHBTC",
  "c": "mUvoqJxFIILMdfAW5iGSOW",
  "S": "BUY",
  "o": "LIMIT",
  "f": "GTC",
  "q": "1.00000000",
  "p": "0.10264410",
  "P": "0.00000000",
  "F": "0.00000000",
  "g": -1,
  "C": "null",
  "x": "NEW",
  "X": "NEW",
  "r": "NONE",
  "i": 4293153,
  "l": "0.00000000",
  "z": "0.00000000",
  "L": "0.00000000",
  "n": "0",
  "N": null,
  "T": 1499405658657,
  "t": -1,
  "I": 8641984,
  "w": true,
  "m": false,
  "M": false,
  "O": 1499405658657,
  "Z": "0.00000000",
  "Y": "0.00000000"
}
```

**Key Fields**:
- e: "executionReport"
- s: symbol
- i: order ID
- x: current execution type
- X: current order status
- l: last executed quantity
- L: last executed price
- z: cumulative filled quantity

**Contract**:
- MUST provide real-time order status updates
- MUST emit execution reports for all order state changes
- MUST handle partial fills correctly
- Listen key MUST be renewed every 30 minutes

### Account Update Stream
**Stream**: User data stream
**Balance Update Message**:
```json
{
  "e": "balanceUpdate",
  "E": 1573200697110,
  "a": "BTC",
  "d": "100.00000000",
  "T": 1573200697068
}
```

**Fields**:
- e: "balanceUpdate"
- a: asset
- d: balance delta
- T: transaction time

**Contract**:
- MUST emit balance updates for all asset changes
- MUST provide delta (not absolute) balance changes
- MUST handle multiple asset updates in sequence
- Updates MUST be chronologically ordered

## Rate Limiting Contracts

### REST API Limits
- **Request Rate**: 1200 requests per minute per IP
- **Order Rate**: 100 orders per 10 seconds per account
- **Weight Limits**: Different endpoints have different weights
- **Violation Response**: HTTP 429 with retry-after header

**Contract**:
- MUST implement request rate limiting locally
- MUST respect weight-based limiting
- MUST handle 429 responses with exponential backoff
- MUST track rate limit headers in responses

### WebSocket Limits
- **Connection Limit**: 5 connections per IP for spot
- **Stream Limit**: 1024 streams per connection
- **Message Rate**: 10 messages per second per connection

**Contract**:
- MUST not exceed stream limits per connection
- MUST handle connection drops with reconnection
- MUST manage multiple symbols within stream limits
- MUST implement connection pooling if needed

## Error Handling Contracts

### REST API Errors
**Standard Error Response**:
```json
{
  "code": -1121,
  "msg": "Invalid symbol."
}
```

**Common Error Codes**:
- -1121: Invalid symbol
- -1102: Mandatory parameter was not sent
- -2010: NEW_ORDER_REJECTED
- -2011: CANCEL_REJECTED
- -1013: Filter failure (LOT_SIZE, PRICE_FILTER, etc.)

**Contract**:
- MUST categorize errors by type (client error vs server error)
- MUST provide actionable error messages
- MUST handle temporary vs permanent errors differently
- Rate limit errors (429) MUST trigger backoff, not failure

### WebSocket Errors
**Connection Error Handling**:
- Network disconnection MUST trigger immediate reconnection
- Invalid stream subscriptions MUST be logged and removed
- Authentication failures MUST stop reconnection attempts

**Contract**:
- MUST implement exponential backoff for reconnection
- MUST maintain subscription state across reconnections
- MUST handle partial message corruption gracefully
- MUST provide connection health monitoring