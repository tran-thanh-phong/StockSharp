# Data Model: Binance Connector

## Core Entities

### CryptoExchangeAdapterBase<TRestClient, TSocketClient>
**Purpose**: Generic base class for all cryptocurrency exchange connectors
**Base Class**: AsyncMessageAdapter with IKeySecretAdapter implementation
**Generic Constraints**:
- TRestClient : BaseRestClient (CryptoExchange.Net REST client)
- TSocketClient : BaseSocketClient (CryptoExchange.Net WebSocket client)

**Attributes**:
- ApiKey: string (credential storage via IKeySecretAdapter)
- ApiSecret: string (credential storage via IKeySecretAdapter)
- UseTestnet: bool (environment selection)
- RestClient: TRestClient (CryptoExchange.Net REST client)
- SocketClient: TSocketClient (CryptoExchange.Net WebSocket client)
- MessageConverters: Dictionary<Type, IMessageConverter> (conversion registry)
- ActiveSubscriptions: ConcurrentDictionary<SecurityId, SubscriptionInfo> (thread-safe tracking)

### BinanceSpotMessageAdapter / BinanceFuturesMessageAdapter
**Purpose**: Binance-specific implementations extending CryptoExchangeAdapterBase
**Base Class**: CryptoExchangeAdapterBase<BinanceRestClient, BinanceSocketClient>
**Relationships**:
- Extends CryptoExchangeAdapterBase with Binance-specific type parameters
- Uses Binance.Net clients (BinanceRestClient, BinanceSocketClient) via CryptoExchange.Net
- Leverages shared MessageConverters from base class
- Implements Binance-specific message mapping and configuration

**State Transitions**:
Disconnected → Connecting → Connected → Disconnected
- Connecting: CryptoExchange.Net client initialization and authentication
- Connected: Active CryptoExchange.Net connections, message processing and conversion
- Disconnecting: CryptoExchange.Net graceful disconnection and cleanup

### Security (StockSharp Entity)
**Purpose**: Represents tradeable instruments on Binance
**Key Attributes**:
- SecurityId.SecurityCode: string (Binance symbol, e.g., "BTCUSDT")
- SecurityId.BoardCode: string ("BNB" for spot, "BNBFT" for futures)
- SecurityType: SecurityTypes (Stock for spot, Future for futures contracts)
- PriceStep: decimal (minimum price increment from Binance symbol info)
- VolumeStep: decimal (minimum quantity increment)
- MinVolume: decimal (minimum order quantity)
- MaxVolume: decimal (maximum order quantity)
- Multiplier: decimal (contract multiplier for futures)

**Validation Rules**:
- Symbol format must match Binance naming convention (base + quote currency)
- PriceStep and VolumeStep must be positive values
- MinVolume ≤ MaxVolume
- All decimal precision must match Binance's symbol configuration

### Order (StockSharp Entity via Messages)
**Purpose**: Represents trading orders through ExecutionMessage
**Key Attributes**:
- OrderId: long (Binance order ID)
- TransactionId: long (StockSharp transaction ID for tracking)
- SecurityId: SecurityId (trading pair identifier)
- Side: Sides (Buy/Sell)
- OrderType: OrderTypes (Market, Limit, StopLoss, StopLimit)
- Volume: decimal (order quantity)
- Price: decimal (limit price, null for market orders)
- StopPrice: decimal (stop price for stop orders)
- OrderState: OrderStates (Pending, Active, Done, Failed)
- Balance: decimal (remaining quantity to fill)

**State Transitions**:
Pending → Active → Done/Failed
- Pending: Order submitted but not yet acknowledged
- Active: Order confirmed and working in market
- Done: Order fully executed
- Failed: Order rejected or cancelled

**Validation Rules**:
- Volume must be >= Security.MinVolume and <= Security.MaxVolume
- Volume must be multiple of Security.VolumeStep
- Price (if specified) must be multiple of Security.PriceStep
- StopPrice validation for stop orders

### Trade (StockSharp Entity via Messages)
**Purpose**: Represents executed transactions through ExecutionMessage
**Key Attributes**:
- TradeId: long (Binance trade ID)
- OrderId: long (associated order ID)
- SecurityId: SecurityId (trading pair)
- TradePrice: decimal (execution price)
- TradeVolume: decimal (executed quantity)
- OriginSide: Sides (aggressor side - buy/sell)
- ServerTime: DateTimeOffset (Binance execution timestamp)
- Commission: decimal (trading fee amount)
- CommissionCurrency: string (fee currency)

**Relationships**:
- Belongs to specific Order (via OrderId)
- Associated with Security (via SecurityId)
- May have multiple trades per order (partial fills)

### Portfolio (StockSharp Entity via Messages)
**Purpose**: Represents account balances through PortfolioChangeMessage
**Key Attributes**:
- PortfolioName: string ("Binance" or "BinanceTestnet")
- BeginValue: decimal (starting balance)
- CurrentValue: decimal (current total value)
- BlockedValue: decimal (locked/reserved funds)
- VariationMargin: decimal (unrealized P&L)
- Commission: decimal (total fees paid)
- Currency: string (account base currency, typically "USDT")

**Validation Rules**:
- CurrentValue = BeginValue + VariationMargin - Commission
- BlockedValue ≤ CurrentValue (cannot block more than available)
- All values must be non-negative

### Position (StockSharp Entity via Messages)
**Purpose**: Represents trading positions through PositionChangeMessage
**Key Attributes**:
- SecurityId: SecurityId (instrument identifier)
- CurrentValue: decimal (current position size, positive = long, negative = short)
- BlockedValue: decimal (position locked in active orders)
- AveragePrice: decimal (average entry price)
- UnrealizedPnL: decimal (mark-to-market P&L)
- Commission: decimal (position-related fees)
- Currency: string (position currency)

**Relationships**:
- Associated with Security (via SecurityId)
- Belongs to Portfolio (account-level aggregation)
- Updated by Trade executions

**State Transitions**:
Flat (0) ↔ Long (>0) ↔ Short (<0)
- Position changes based on trade executions
- BlockedValue increases with pending orders, decreases on execution/cancellation

### MarketDepth (StockSharp Entity via Messages)
**Purpose**: Represents order book data through QuoteChangeMessage
**Key Attributes**:
- SecurityId: SecurityId (trading pair)
- Bids: Quote[] (buy side price/volume pairs, sorted by price descending)
- Asks: Quote[] (sell side price/volume pairs, sorted by price ascending)
- ServerTime: DateTimeOffset (Binance update timestamp)
- IsByLevel: bool (true - level data, false - order-by-order)
- BuildFrom: DataType (source data type)

**Validation Rules**:
- Bids must be sorted by price descending (highest first)
- Asks must be sorted by price ascending (lowest first)
- All prices and volumes must be positive
- No bid price should equal or exceed any ask price (no crossed market)

### Candle (StockSharp Entity via Messages)
**Purpose**: Represents OHLCV candlestick data through CandleMessage
**Key Attributes**:
- SecurityId: SecurityId (trading pair)
- OpenTime: DateTimeOffset (candle start time)
- CloseTime: DateTimeOffset (candle end time)
- OpenPrice: decimal (opening price)
- HighPrice: decimal (highest price)
- LowPrice: decimal (lowest price)
- ClosePrice: decimal (closing price)
- TotalVolume: decimal (total volume traded)
- TimeFrame: TimeSpan (candle duration: 1m, 5m, 1h, 1d, etc.)

**Validation Rules**:
- HighPrice ≥ max(OpenPrice, ClosePrice, LowPrice)
- LowPrice ≤ min(OpenPrice, ClosePrice, HighPrice)
- TotalVolume ≥ 0
- OpenTime < CloseTime
- TimeFrame must match standard Binance intervals

## Message Flow Architecture

### Market Data Flow
1. **Subscription Request**: MarketDataMessage → CryptoExchangeAdapterBase
2. **CryptoExchange.Net Subscription**: Adapter uses Binance.Net to subscribe to streams
3. **Real-time Updates**: Binance.Net events → MessageConverter → StockSharp messages
4. **Message Types**: ExecutionMessage (ticks), QuoteChangeMessage (depth), CandleMessage (OHLCV)

### Order Management Flow
1. **Order Registration**: OrderRegisterMessage → CryptoExchangeAdapterBase
2. **CryptoExchange.Net API Call**: Adapter uses Binance.Net REST client for order placement
3. **Acknowledgment**: Binance.Net response → MessageConverter → ExecutionMessage (order confirmation)
4. **Execution Updates**: Binance.Net WebSocket events → MessageConverter → ExecutionMessage (fills)

### Account Data Flow
1. **Portfolio Request**: PortfolioMessage → CryptoExchangeAdapterBase
2. **Account Query**: Binance.Net REST client call for account information
3. **Balance Updates**: Binance.Net WebSocket events → MessageConverter → PortfolioChangeMessage/PositionChangeMessage
4. **Real-time Sync**: CryptoExchange.Net handles connection management and updates

## Message Converter Framework

### IMessageConverter Interface
```csharp
public interface IMessageConverter<TSource, TTarget>
{
    TTarget Convert(TSource source, SecurityId securityId);
    bool CanConvert(Type sourceType, Type targetType);
}
```

### Core Message Converters

#### ExecutionMessageConverter
**Purpose**: Convert CryptoExchange.Net trade/order data to StockSharp ExecutionMessage
**Source Types**:
- Binance.Net: BinanceStreamTrade, BinanceOrder, BinanceOrderUpdate
- Generic: Any CryptoExchange.Net trade/order event
**Target**: ExecutionMessage (DataType.Ticks or DataType.Transactions)

#### QuoteChangeMessageConverter
**Purpose**: Convert order book updates to StockSharp QuoteChangeMessage
**Source Types**:
- Binance.Net: BinanceStreamOrderBookDepth, BinanceOrderBook
- Generic: Any CryptoExchange.Net order book event
**Target**: QuoteChangeMessage

#### SecurityMessageConverter
**Purpose**: Convert exchange symbol info to StockSharp SecurityMessage
**Source Types**:
- Binance.Net: BinanceSymbol, BinanceExchangeInfo
- Generic: Any CryptoExchange.Net symbol info
**Target**: SecurityMessage

#### PortfolioMessageConverter
**Purpose**: Convert account balance info to StockSharp PortfolioChangeMessage
**Source Types**:
- Binance.Net: BinanceAccountInfo, BinanceStreamBalanceUpdate
- Generic: Any CryptoExchange.Net balance event
**Target**: PortfolioChangeMessage

### Conversion Registry
```csharp
public class MessageConverterRegistry
{
    private readonly Dictionary<(Type, Type), IMessageConverter> _converters;

    public void RegisterConverter<TSource, TTarget>(IMessageConverter<TSource, TTarget> converter);
    public TTarget Convert<TSource, TTarget>(TSource source, SecurityId securityId);
    public Message ConvertToStockSharp(object cryptoExchangeObject, SecurityId securityId);
}
```

## Data Validation and Conversion

### Symbol Mapping via CryptoExchange.Net
- Binance.Net symbol objects → StockSharp SecurityId conversion
- Board code mapping: "BINANCE" (spot), "BINANCE_FUTURES" (futures)
- Validation through CryptoExchange.Net exchange info endpoints
- Automatic symbol filter and precision extraction

### Precision Handling via Message Converters
- CryptoExchange.Net decimal handling → StockSharp decimal conversion
- Symbol-specific precision rules from exchange metadata
- Automatic rounding and validation through converter framework

### Time Synchronization
- CryptoExchange.Net DateTime/DateTimeOffset → StockSharp DateTimeOffset
- Automatic timezone handling by CryptoExchange.Net infrastructure
- Message ordering preserved through CryptoExchange.Net event sequencing