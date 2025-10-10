# Auto Trading Basic Flow - Feature Specification

## 1. Overview

### 1.1 Purpose
This feature implements a complete auto trading application that demonstrates the full trading lifecycle using StockSharp as the trading engine with CTrader connector. The application serves as a comprehensive learning tool to understand how StockSharp components work together in a real trading system.

### 1.2 Goals
- Understand StockSharp's message-based architecture
- Learn how connectors interact with trading strategies
- Master order lifecycle management (register, monitor, execute, cancel)
- Implement risk management with TP/SL protective orders
- Build production-ready UI using StockSharp's WPF controls
- Handle external signals from TradingView webhooks
- Process exchange-based signals from market data

### 1.3 Scope
**In Scope**:
- Signal reception (Exchange data + TradingView webhooks)
- Signal processing and trading decision logic
- Order management (register, monitor, cancel)
- Risk management with TP/SL
- Real-time UI displaying orders, trades, portfolio
- Configuration persistence
- Logging system

**Out of Scope** (Future Enhancements):
- Multi-strategy portfolio management
- Advanced risk rules (max drawdown, profit targets)
- Backtesting integration
- Chart visualization
- Performance analytics dashboard

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer (WPF)                        │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │
│  │  Main    │ │  Signal  │ │  Order   │ │   Portfolio  │  │
│  │  Window  │ │  Config  │ │  Monitor │ │   Monitor    │  │
│  └──────────┘ └──────────┘ └──────────┘ └──────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────▼─────────────────────────────────┐
│                    Application Layer                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Signal     │  │   Trading    │  │     Risk     │       │
│  │   Manager    │  │   Strategy   │  │   Manager    │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────▼─────────────────────────────────┐
│                   StockSharp Framework                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  Connector   │  │  Strategy    │  │  Protective  │       │
│  │              │  │   Base       │  │  Controller  │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────▼─────────────────────────────────┐
│                      Connector Layer                          │
│  ┌──────────────────────────────────────────────────────┐    │
│  │      CTrader Message Adapter                         │    │
│  │  (Market Data + Transaction Processing)              │    │
│  └──────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────▼─────────┐
                    │   CTrader API     │
                    │   (OpenAPI.Net)   │
                    └───────────────────┘
```

### 2.2 Signal Flow

```
External Signal Sources
├── TradingView Webhook → HTTP Endpoint → Signal Manager
└── Exchange Market Data → Connector → Strategy → Signal Manager

Signal Manager
├── Validate Signal
├── Apply Risk Rules
└── Generate Trading Decision

Trading Decision
├── Calculate Position Size (Risk Manager)
├── Create Order with TP/SL (Protective Controller)
└── Register Order (Connector)

Order Lifecycle
├── Order Registered → UI Update
├── Order Accepted → UI Update
├── Order Filled → Trade Created → UI Update
├── TP/SL Triggered → Position Closed → UI Update
└── Order Failed/Cancelled → UI Update
```

## 3. Component Specifications

### 3.1 Signal Manager

**Responsibilities**:
- Receive signals from multiple sources
- Validate and normalize signal data
- Queue signals for processing
- Emit trading decisions

**Inputs**:
- TradingView webhook (JSON payload)
- Exchange market data (candles, ticks, indicators)

**Outputs**:
- `TradingSignal` object with action, symbol, direction, entry price, TP, SL

**Key Classes**:
```csharp
public class SignalManager
{
    public event Action<TradingSignal> SignalReceived;

    public void ProcessWebhookSignal(TradingViewWebhook webhook);
    public void ProcessMarketSignal(ICandleMessage candle, IndicatorValues indicators);
}

public class TradingSignal
{
    public SecurityId SecurityId { get; set; }
    public Sides Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal? TakeProfit { get; set; }
    public decimal? StopLoss { get; set; }
    public string SignalSource { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
```

### 3.2 Trading Strategy

**Base**: Extends `StockSharp.Algo.Strategies.Strategy`

**Responsibilities**:
- Subscribe to signals from SignalManager
- Validate trading conditions (connected, sufficient balance, etc.)
- Execute trading decisions
- Manage protective orders (TP/SL)
- Track open positions

**Parameters**:
```csharp
public class SignalDrivenStrategy : Strategy
{
    // Strategy Parameters
    StrategyParam<decimal> _riskPercentage;        // % of account to risk per trade
    StrategyParam<bool> _enableTPSL;               // Enable TP/SL protective orders
    StrategyParam<bool> _useServerOrders;          // Use server-side or local TP/SL
    StrategyParam<decimal> _maxPositionSize;       // Maximum position size
    StrategyParam<string> _webhookEndpoint;        // TradingView webhook URL

    // References
    public SignalManager SignalManager { get; set; }
    public IProtectiveController ProtectiveController { get; set; }
}
```

**Event Handlers**:
- `OnSignalReceived()` - Process incoming signals
- `OnOrderReceived()` - Track order state changes
- `OnTradeReceived()` - Update position tracking
- `OnProtectiveTriggered()` - Handle TP/SL execution

### 3.3 Risk Manager Integration

**Uses**: `StockSharp.Algo.Risk.RiskManager`

**Risk Rules**:
- Maximum position size per trade
- Maximum open positions
- Daily loss limit
- Maximum leverage

**Position Sizing**:
```csharp
public decimal CalculatePositionSize(decimal accountBalance, decimal riskPercent,
                                     decimal entryPrice, decimal stopLoss)
{
    var riskAmount = accountBalance * (riskPercent / 100);
    var priceRisk = Math.Abs(entryPrice - stopLoss);
    var positionSize = riskAmount / priceRisk;
    return positionSize;
}
```

### 3.4 Protective Order Controller

**Uses**: `StockSharp.Algo.Strategies.Protective.ProtectiveController`

**Modes**:
- **Server Mode**: Use broker's native TP/SL orders (via `ServerProtectiveBehaviourFactory`)
- **Local Mode**: Emulated TP/SL managed by StockSharp (via `LocalProtectiveBehaviourFactory`)

**Configuration**:
```csharp
var factory = useServerOrders
    ? new ServerProtectiveBehaviourFactory(connector.Adapter)
    : new LocalProtectiveBehaviourFactory(security.PriceStep, security.Decimals);

var controller = new ProtectiveController();
var posController = controller.GetController(
    securityId, portfolioName, factory,
    takeValue: new Unit(takeProfitPips, UnitTypes.Point),
    stopValue: new Unit(stopLossPips, UnitTypes.Point),
    isStopTrailing: false,
    takeTimeout: TimeSpan.Zero,
    stopTimeout: TimeSpan.Zero,
    useMarketOrders: false
);
```

### 3.5 Order Management

**Order Registration**:
```csharp
var order = new Order
{
    Security = security,
    Portfolio = portfolio,
    Side = signal.Side,
    Price = signal.EntryPrice,
    Volume = positionSize,
    Type = OrderTypes.Limit,
    TimeInForce = TimeInForce.GoodTillCancel
};

connector.RegisterOrder(order);
```

**Order Monitoring**:
```csharp
connector.OrderReceived += (subscription, order) =>
{
    // Update UI
    ordersGrid.Orders.TryAdd(order);

    // Track for strategy
    if (order.State == OrderStates.Active)
        _activeOrders[order.TransactionId] = order;
    else if (order.State == OrderStates.Done || order.State == OrderStates.Failed)
        _activeOrders.Remove(order.TransactionId);
};

connector.OwnTradeReceived += (subscription, trade) =>
{
    // Update UI
    tradesGrid.Trades.TryAdd(trade);

    // Trigger TP/SL creation if not already set
    if (enableTPSL && !_protectiveOrdersSet.Contains(trade.Order.TransactionId))
    {
        CreateProtectiveOrders(trade);
    }
};
```

### 3.6 TradingView Webhook Receiver

**Technology**: ASP.NET Core Minimal API

**Endpoint**: `POST /api/tradingview/webhook`

**Request Format**:
```json
{
  "symbol": "EURUSD",
  "action": "buy",
  "price": 1.0850,
  "takeProfit": 1.0900,
  "stopLoss": 1.0800,
  "timestamp": "2025-10-06T10:30:00Z"
}
```

**Implementation**:
```csharp
app.MapPost("/api/tradingview/webhook", async (TradingViewWebhook webhook,
                                                SignalManager signalManager) =>
{
    try
    {
        signalManager.ProcessWebhookSignal(webhook);
        return Results.Ok(new { status = "received" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
```

## 4. User Interface Specifications

### 4.1 Main Window

**Components**:
- **Connection Panel**: Settings button, Connect/Disconnect button
- **Strategy Control**: Start/Stop strategy, Strategy parameters
- **Quick Access Buttons**: Securities, Portfolios, Orders, Trades, Strategies
- **Log Monitor**: Real-time application logging

**Layout**: Based on `Samples/06_Strategies/10_LiveTerminal/MainWindow.xaml`

### 4.2 Signal Configuration Window

**Fields**:
- Signal Source: [Dropdown: TradingView, Exchange Data, Both]
- TradingView Webhook URL: [TextBox - Read-only endpoint URL]
- Enable Exchange Signals: [Checkbox]
- Candle Timeframe: [Dropdown: 1m, 5m, 15m, 30m, 1h, 4h, 1d]
- Signal Indicators: [Multi-select: SMA Cross, MACD, RSI, Custom]

### 4.3 Orders Window

**Component**: `xaml:OrderGrid`

**Features**:
- Display all orders (pending, active, filled, cancelled)
- Real-time updates
- Cancel order button
- Order details (symbol, side, volume, price, state, time)

### 4.4 Trades Window

**Component**: `xaml:MyTradeGrid`

**Features**:
- Display all executed trades
- Trade details (symbol, side, volume, price, commission, PnL, time)
- Filter by date/symbol

### 4.5 Portfolio Window

**Component**: `xaml:PortfolioGrid`

**Features**:
- Account balance
- Equity
- Available margin
- Used margin
- Unrealized PnL
- Realized PnL

## 5. Data Models

### 5.1 Configuration Data

**Connector Settings**: Saved to `Data/connection.json`
```csharp
public class ConnectorSettings
{
    public string ApplicationId { get; set; }
    public SecureString ApplicationSecret { get; set; }
    public SecureString AccessToken { get; set; }
    public long AccountId { get; set; }
    public CTraderEnvironment Environment { get; set; }
}
```

**Strategy Settings**: Saved to `Data/Strategies/{StrategyId}.json`
```csharp
public class StrategySettings
{
    public Guid StrategyId { get; set; }
    public string StrategyName { get; set; }
    public SecurityId SecurityId { get; set; }
    public string PortfolioName { get; set; }
    public decimal RiskPercentage { get; set; }
    public bool EnableTPSL { get; set; }
    public bool UseServerOrders { get; set; }
    public decimal MaxPositionSize { get; set; }
    public string SignalSource { get; set; }
}
```

## 6. Implementation Flow

### 6.1 Application Startup

1. Initialize `LogManager` with file and GUI listeners
2. Create `Connector` instance
3. Load connector settings from file
4. Initialize `SignalManager`
5. Initialize `ProtectiveController`
6. Create and configure `SignalDrivenStrategy`
7. Setup UI windows and bind to connector events
8. Start webhook server (if TradingView enabled)

### 6.2 Connection Flow

1. User clicks "Settings" → Configure connector (credentials, account)
2. User clicks "Connect" → `Connector.Connect()`
3. On connected → Subscribe to securities, load portfolio
4. Enable trading controls

### 6.3 Trading Flow

**From TradingView Signal**:
1. TradingView sends webhook → Webhook endpoint receives JSON
2. Parse webhook → Create `TradingSignal`
3. `SignalManager.ProcessWebhookSignal()` → Emit `SignalReceived` event
4. `Strategy.OnSignalReceived()` → Validate signal
5. Calculate position size → Create order
6. Register order → `Connector.RegisterOrder()`
7. Order accepted → Create TP/SL protective orders
8. Monitor order until filled/cancelled

**From Exchange Signal**:
1. Subscribe to candles → `Connector.Subscribe(candleSubscription)`
2. Candle received → Process through indicators
3. Indicator signals crossover → Create `TradingSignal`
4. Follow same flow as webhook (steps 4-8 above)

### 6.4 Order Lifecycle Flow

1. **Register**: `Connector.RegisterOrder()` → Order sent to broker
2. **Pending**: Order submitted, waiting for acceptance
3. **Active**: Order accepted by broker, active in market
4. **Partial Fill**: Order partially executed → Trade created → Update balance
5. **Filled**: Order fully executed → Create TP/SL if not exists
6. **Cancelled**: Order cancelled by user or broker → Remove from tracking
7. **Failed**: Order rejected → Log error → Notify user

### 6.5 Protective Order Flow

1. Main order filled → Trade received event
2. Check if TP/SL already created for this trade
3. Calculate TP/SL prices based on signal or strategy parameters
4. Create protective controller if not exists
5. Update protective controller with trade details
6. Protective controller creates TP/SL orders
7. Monitor protective orders until triggered
8. When triggered → Position closed → Calculate PnL

## 7. Error Handling

### 7.1 Connection Errors
- Display error message to user
- Log to file
- Attempt reconnection with exponential backoff
- Disable trading controls until reconnected

### 7.2 Order Errors
- Log order registration failure
- Notify user via MessageBox
- Update order status in grid to "Failed"
- Do not create protective orders

### 7.3 Signal Processing Errors
- Log invalid signal
- Skip signal processing
- Continue monitoring for next signal

### 7.4 Risk Rule Violations
- Prevent order registration
- Log violation
- Notify user with specific rule violated

## 8. Testing Scenarios

### 8.1 Unit Tests
- Signal parsing and validation
- Position size calculation
- Risk rule validation
- Order state transitions

### 8.2 Integration Tests
- Connector connection/disconnection
- Order registration and cancellation
- Trade execution and TP/SL triggering
- Webhook endpoint receiving signals

### 8.3 Manual Testing
- Connect to CTrader demo account
- Send test webhook from TradingView
- Place market and limit orders
- Test TP/SL execution
- Test order cancellation
- Test reconnection after disconnect
- Test with insufficient balance
- Test with invalid symbols

## 9. Performance Considerations

### 9.1 Threading
- UI updates on GUI thread via `GuiAsync()`
- Signal processing on background thread
- Order operations are async (`ValueTask`)

### 9.2 Memory Management
- Dispose connector on shutdown
- Clear order/trade collections periodically
- Use weak references for event handlers where appropriate

### 9.3 Latency
- Minimize processing time in signal handler
- Use async/await for I/O operations
- Batch UI updates where possible

## 10. Security Considerations

### 10.1 Credentials Storage
- Store credentials using `SecureString`
- Encrypt settings file (future enhancement)
- Never log credentials

### 10.2 Webhook Security
- Validate webhook source (IP whitelist or signature)
- Rate limiting on webhook endpoint
- HTTPS only in production

## 11. Future Enhancements

1. **Multi-Strategy Support**: Run multiple strategies simultaneously
2. **Backtesting Integration**: Test strategies on historical data
3. **Chart Integration**: Visualize trades on charts
4. **Performance Analytics**: Win rate, profit factor, Sharpe ratio
5. **Alert System**: Email/SMS notifications for trades
6. **Strategy Optimization**: Genetic algorithm parameter optimization
7. **Database Storage**: Persist trades and performance to database
8. **Web Dashboard**: Remote monitoring via web interface

## 12. References

### 12.1 StockSharp Documentation
- Strategy Development Guide
- Connector API Reference
- Risk Management Framework
- Protective Orders Documentation

### 12.2 Code References
- `Samples/01_Basic/03_Orders/MainWindow.xaml.cs` - Basic order management
- `Samples/06_Strategies/10_LiveTerminal/` - Complete trading terminal
- `Customization/Strategies/BasicStrategies/SmaCrossStrategy.cs` - Strategy example
- `Customization/Connectors/CTrader/` - CTrader connector implementation
- `Algo/Strategies/Protective/ProtectiveController.cs` - TP/SL management

## 13. Glossary

- **TP**: Take Profit - Order to close position at profit target
- **SL**: Stop Loss - Order to close position to limit loss
- **Signal**: Trading indication (buy/sell) from external source or analysis
- **Protective Order**: TP or SL order attached to position
- **Position Sizing**: Calculating order volume based on risk parameters
- **Risk Management**: Rules to control trading risk and exposure
