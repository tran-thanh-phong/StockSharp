# Auto Trading Basic Flow - System Architecture

## 1. Architecture Overview

### 1.1 Layered Architecture

The system follows a layered architecture pattern with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Presentation Layer                          │
│                              (WPF UI)                               │
├─────────────────────────────────────────────────────────────────────┤
│                        Application Layer                            │
│              (Signal Processing, Strategy Logic)                    │
├─────────────────────────────────────────────────────────────────────┤
│                     StockSharp Framework Layer                      │
│           (Connector, Strategy Base, Risk Management)               │
├─────────────────────────────────────────────────────────────────────┤
│                        Connector Layer                              │
│                  (CTrader Message Adapter)                          │
├─────────────────────────────────────────────────────────────────────┤
│                      External Services                              │
│              (CTrader API, TradingView Webhooks)                    │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 Key Architectural Patterns

1. **Message-Based Communication**: All data flows through typed messages (ExecutionMessage, QuoteChangeMessage, etc.)
2. **Event-Driven Processing**: Components communicate via events and event handlers
3. **Strategy Pattern**: Different signal sources implement common ISignalSource interface
4. **Observer Pattern**: UI components observe connector events for real-time updates
5. **Factory Pattern**: ProtectiveBehaviourFactory creates TP/SL strategies based on configuration

## 2. Component Architecture

### 2.1 Component Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│                            Main Window                               │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐   │
│  │  Settings  │  │  Connect   │  │  Strategy  │  │  Monitor   │   │
│  │   Panel    │  │   Panel    │  │  Control   │  │   Panel    │   │
│  └────────────┘  └────────────┘  └────────────┘  └────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     Application Coordinator                          │
│  - Manages application lifecycle                                     │
│  - Coordinates components                                            │
│  - Handles configuration persistence                                 │
└──────────────────────────────────────────────────────────────────────┘
           │                    │                    │
           ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Signal Manager  │  │ Trading Strategy│  │  Risk Manager   │
│                 │  │                 │  │                 │
│ - Webhook API   │  │ - Entry Logic   │  │ - Position Size │
│ - Market Data   │  │ - TP/SL Logic   │  │ - Risk Rules    │
│ - Normalization │  │ - Order Mgmt    │  │ - Validation    │
└─────────────────┘  └─────────────────┘  └─────────────────┘
           │                    │                    │
           └────────────────────┼────────────────────┘
                                ▼
                    ┌───────────────────────┐
                    │   Connector (Core)    │
                    │                       │
                    │ - Connection Mgmt     │
                    │ - Order Registration  │
                    │ - Event Distribution  │
                    │ - Market Data Sub     │
                    └───────────────────────┘
                                │
                                ▼
                ┌───────────────────────────────┐
                │  CTrader Message Adapter      │
                │                               │
                │ - Protocol Translation        │
                │ - Market Data Handling        │
                │ - Transaction Processing      │
                └───────────────────────────────┘
                                │
                                ▼
                        ┌───────────────┐
                        │  CTrader API  │
                        │ (OpenAPI.Net) │
                        └───────────────┘
```

### 2.2 Component Responsibilities

#### Main Window
- **Purpose**: Application entry point and UI coordination
- **Responsibilities**:
  - Initialize all components
  - Display connection status
  - Show/hide child windows
  - Coordinate logging
- **Dependencies**: Connector, LogManager, child windows

#### Signal Manager
- **Purpose**: Centralized signal processing hub
- **Responsibilities**:
  - Receive signals from multiple sources
  - Validate signal format and data
  - Normalize signals to common format
  - Queue signals for processing
  - Emit normalized trading signals
- **Dependencies**: None (standalone component)
- **Interfaces**:
  - `ISignalSource` - Implemented by webhook receiver and market data processor
  - `ITradingSignal` - Signal output format

#### Trading Strategy (SignalDrivenStrategy)
- **Purpose**: Execute trading logic based on signals
- **Responsibilities**:
  - Subscribe to SignalManager events
  - Validate trading conditions
  - Calculate position sizes
  - Create and register orders
  - Manage protective orders
  - Track positions
- **Dependencies**: Connector, SignalManager, ProtectiveController, RiskManager
- **Base Class**: `StockSharp.Algo.Strategies.Strategy`

#### Risk Manager
- **Purpose**: Enforce trading risk limits
- **Responsibilities**:
  - Validate position sizes
  - Check account balance
  - Enforce max position limits
  - Check risk rules before order registration
- **Dependencies**: Connector (for portfolio info)
- **Uses**: `StockSharp.Algo.Risk.RiskManager`

#### Protective Controller
- **Purpose**: Manage TP/SL protective orders
- **Responsibilities**:
  - Create TP/SL orders when position opens
  - Monitor protective order triggers
  - Handle protective order execution
  - Support both server and local modes
- **Dependencies**: Connector
- **Uses**: `StockSharp.Algo.Strategies.Protective.ProtectiveController`

#### Connector
- **Purpose**: Core trading engine
- **Responsibilities**:
  - Manage connection to broker
  - Subscribe to market data
  - Register and cancel orders
  - Emit events for orders, trades, portfolio updates
  - Persist state to storage
- **StockSharp Component**: `StockSharp.Algo.Connector`

#### CTrader Message Adapter
- **Purpose**: Translate between StockSharp and CTrader protocols
- **Responsibilities**:
  - Connect to CTrader API
  - Transform StockSharp messages to CTrader protocol
  - Transform CTrader events to StockSharp messages
  - Handle authentication and token refresh
- **Custom Component**: `Customization/Connectors/CTrader/CTraderMessageAdapter.cs`

## 3. Data Flow Architecture

### 3.1 Signal Processing Flow

```
External Signal Sources
│
├─── TradingView Webhook
│    │
│    └──> POST /api/tradingview/webhook
│         │
│         ├─ Validate Payload
│         ├─ Parse JSON
│         └─> SignalManager.ProcessWebhookSignal()
│
└─── Exchange Market Data
     │
     └──> Connector.Subscribe(Candles)
          │
          └──> OnCandleReceived Event
               │
               ├─ Update Indicators
               ├─ Check Entry Conditions
               └─> SignalManager.ProcessMarketSignal()

SignalManager
│
├─ Validate Signal
├─ Normalize to TradingSignal
├─ Apply Filters
└─> Emit SignalReceived Event
     │
     └──> Strategy.OnSignalReceived()
          │
          ├─ Validate Trading Conditions
          ├─ Check Risk Rules
          ├─ Calculate Position Size
          ├─ Create Order
          └─> Connector.RegisterOrder()
```

### 3.2 Order Execution Flow

```
Order Registration
│
└──> Connector.RegisterOrder(order)
     │
     └──> CTraderMessageAdapter.RegisterOrderAsync()
          │
          ├─ Convert to OrderRegisterMessage
          ├─ Map to CTrader protocol
          └─> CTraderClient.NewOrderAsync()
               │
               └──> CTrader API
                    │
                    ├─ Order Submitted
                    └─> ProtoOAExecutionEvent (OrderAccepted)

Order Accepted Event
│
└──> CTraderClient.ExecutionEvent
     │
     └──> CTraderMessageAdapter.OnExecutionEvent()
          │
          ├─ Parse execution details
          ├─ Create ExecutionMessage
          └─> SendOutMessage()
               │
               └──> Connector.OrderReceived Event
                    │
                    ├──> UI: OrderGrid.Orders.Add()
                    └──> Strategy.OnOrderReceived()
                         │
                         └─ Track order in _activeOrders

Order Fill Event
│
└──> ProtoOAExecutionEvent (OrderFilled)
     │
     └──> CTraderMessageAdapter.ProcessDealExecution()
          │
          ├─ Create ExecutionMessage (Trade)
          └─> Connector.OwnTradeReceived Event
               │
               ├──> UI: MyTradeGrid.Trades.Add()
               └──> Strategy.OnTradeReceived()
                    │
                    ├─ Update position tracking
                    └─ Create TP/SL if enabled
                         │
                         └──> ProtectiveController.Update()
                              │
                              └──> Connector.RegisterOrder(TP/SL)
```

### 3.3 Portfolio Update Flow

```
Account Update Request
│
└──> Connector.PortfolioLookup()
     │
     └──> CTraderMessageAdapter.PortfolioLookupAsync()
          │
          └─> CTraderClient.GetTraderAsync()
              │
              └──> CTrader API: ProtoOATraderRes
                   │
                   ├─ Parse account balance
                   ├─ Create PortfolioMessage
                   ├─ Create PositionChangeMessage
                   └─> SendOutMessage()
                        │
                        └──> Connector.PositionReceived Event
                             │
                             └──> UI: PortfolioGrid.Update()
```

## 4. Threading Architecture

### 4.1 Thread Model

```
┌─────────────────────────────────────────────────────────────┐
│                      Main UI Thread                         │
│  - WPF rendering                                            │
│  - User input handling                                      │
│  - UI control updates                                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Dispatcher.Invoke()
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Connector Message Pump                     │
│  - Message processing                                       │
│  - Event raising                                            │
│  - State management                                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Task.Run() / async
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Background Worker Threads                  │
│  - Network I/O (CTrader API)                                │
│  - Signal processing                                        │
│  - Indicator calculations                                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Thread Pool
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     HTTP Server Thread                      │
│  - Webhook endpoint listener                                │
│  - Request processing                                       │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Thread Safety Patterns

1. **UI Updates**: Always use `GuiAsync()` extension method
   ```csharp
   this.GuiAsync(() => OrderGrid.Orders.Add(order));
   ```

2. **Connector Operations**: Already thread-safe (message-based)
   ```csharp
   await Connector.RegisterOrderAsync(order); // Safe from any thread
   ```

3. **Shared State**: Use concurrent collections
   ```csharp
   private readonly ConcurrentDictionary<long, Order> _activeOrders = new();
   ```

4. **Event Handlers**: Minimize work, delegate to background
   ```csharp
   connector.OrderReceived += (sub, order) =>
   {
       Task.Run(() => ProcessOrder(order)); // Offload to thread pool
   };
   ```

## 5. Message Flow Architecture

### 5.1 StockSharp Message Types

```
Market Data Messages
├── SecurityMessage           - Security/Instrument definition
├── QuoteChangeMessage        - Market depth (order book)
├── Level1ChangeMessage       - Best bid/ask, last price
├── ExecutionMessage          - Tick trades
└── CandleMessage             - OHLCV candles

Transaction Messages
├── OrderRegisterMessage      - Register new order
├── OrderCancelMessage        - Cancel order
├── OrderReplaceMessage       - Modify order
├── ExecutionMessage          - Order state change / Trade fill
└── OrderStatusMessage        - Query order status

Portfolio Messages
├── PortfolioMessage          - Portfolio information
├── PositionMessage           - Position information
└── PositionChangeMessage     - Position updates

System Messages
├── ConnectMessage            - Connect to broker
├── DisconnectMessage         - Disconnect from broker
└── ResetMessage              - Reset state
```

### 5.2 Message Transformation Pipeline

```
CTrader Protocol → StockSharp Message → Application Event

Example: Order Fill
ProtoOAExecutionEvent            ExecutionMessage            TradeReceived Event
{                                {                           {
  OrderId: 123                     OrderId: 123                Order: {...}
  ExecutionType: OrderFilled       DataType: Transactions      Trade: {...}
  Deal: {                          TradeId: 456                Volume: 0.01
    DealId: 456                    TradePrice: 1.0850          Price: 1.0850
    Volume: 1000                   TradeVolume: 0.01           Time: ...
    ExecutionPrice: 1.0850         ServerTime: ...           }
  }                                ...
}                                }
```

## 6. State Management

### 6.1 Application State

```
Application States:
│
├── Initializing
│   └─> Loading configuration, creating components
│
├── Disconnected
│   └─> Ready to connect, UI controls disabled
│
├── Connecting
│   └─> Connection in progress
│
├── Connected
│   └─> Ready for trading, UI controls enabled
│
├── Trading
│   └─> Strategy running, processing signals
│
└── Stopping
    └─> Cleaning up, closing positions
```

### 6.2 Strategy State

```
Strategy States (ProcessStates):
│
├── Stopped
│   └─> Not running, can be configured
│
├── Stopping
│   └─> Cancelling orders, cleaning up
│
├── Starting
│   └─> Initializing, subscribing to data
│
└── Started
    └─> Running, processing signals
```

### 6.3 Order State

```
Order Lifecycle States:
│
├── None
│   └─> Order created locally, not yet submitted
│
├── Pending
│   └─> Order submitted to broker, awaiting acceptance
│
├── Active
│   └─> Order accepted, active in market
│
├── Done
│   └─> Order fully filled or cancelled
│
└── Failed
    └─> Order rejected by broker
```

## 7. Configuration Architecture

### 7.1 Configuration Hierarchy

```
Application Configuration
│
├── Connector Settings (connection.json)
│   ├── Application ID
│   ├── Application Secret
│   ├── Access Token
│   ├── Account ID
│   └── Environment (Demo/Live)
│
├── Strategy Settings (strategies/{id}.json)
│   ├── Strategy ID
│   ├── Strategy Type
│   ├── Parameters
│   │   ├── Security
│   │   ├── Portfolio
│   │   ├── Risk %
│   │   ├── Enable TP/SL
│   │   ├── Use Server Orders
│   │   └── Max Position Size
│   └── Signal Configuration
│       ├── Signal Source
│       ├── Webhook Endpoint
│       └── Candle Timeframe
│
└── UI Settings (ui.json)
    ├── Window Positions
    ├── Column Widths
    └── Theme Preferences
```

### 7.2 Persistence Strategy

**Connector Settings**:
- Format: JSON via `SettingsStorage` serialization
- Location: `Data/connection.json`
- When: On successful configuration, before connection

**Strategy Settings**:
- Format: JSON via `Strategy.SaveEntire()`
- Location: `Data/Strategies/{StrategyId}.json`
- When: On strategy creation, parameter change, shutdown

**Logs**:
- Format: Text files
- Location: `Data/Logs/{date}.log`
- When: Real-time as events occur

**Market Data** (optional):
- Format: Binary via `StorageRegistry`
- Location: `Data/Storage/{security}/{date}/`
- When: Real-time as market data received

## 8. Error Handling Architecture

### 8.1 Error Handling Layers

```
Layer 1: UI Layer
├── User-friendly error messages (MessageBox)
├── Visual indicators (red background, icons)
└── Error log display (Monitor control)

Layer 2: Application Layer
├── Try-catch around critical operations
├── Validation before operations
├── Graceful degradation
└── State recovery

Layer 3: StockSharp Framework
├── Message-based error reporting
├── Error events (ConnectionError, SubscriptionFailed)
└── Exception wrapping

Layer 4: Connector Layer
├── Protocol-level error handling
├── Reconnection logic
└── Token refresh on expiry
```

### 8.2 Error Flow

```
Error Occurrence
│
├──> CTrader API Error
│    │
│    └──> CTraderClient.Error Event
│         │
│         └──> Adapter.OnError()
│              │
│              └──> Connector.Error Event
│                   │
│                   ├──> Strategy.OnError()
│                   │    └─> Log and potentially stop strategy
│                   │
│                   └──> MainWindow.OnError()
│                        └─> Display error to user
│
└──> Application Error
     │
     └──> Try-Catch Block
          │
          ├──> Log error
          ├──> Update UI status
          ├──> Send notification (future)
          └──> Attempt recovery or shutdown
```

## 9. Scalability Considerations

### 9.1 Current Architecture (Single Strategy)

```
Performance Characteristics:
- Orders/sec: ~10 (limited by broker API)
- Signals/sec: ~100 (webhook + market data)
- Latency: ~100-500ms (network + processing)
- Memory: ~100MB (baseline + market data cache)
```

### 9.2 Future Scalability (Multi-Strategy)

```
Proposed Architecture:
┌─────────────────────────────────────────┐
│      Strategy Coordinator               │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐  │
│  │Strategy1│ │Strategy2│ │Strategy3│  │
│  └─────────┘ └─────────┘ └─────────┘  │
└─────────────────────────────────────────┘
                 │
                 ▼
        ┌────────────────┐
        │  Order Manager │  ← Prevent conflicts
        └────────────────┘
                 │
                 ▼
            Connector
```

Considerations:
- **Order Conflicts**: Multiple strategies trading same symbol
- **Resource Sharing**: Shared market data subscriptions
- **Risk Aggregation**: Combined position limits
- **Performance**: Parallel signal processing

## 10. Security Architecture

### 10.1 Security Layers

```
Layer 1: Credential Storage
├── SecureString for passwords/tokens
├── Windows Data Protection API (future)
└── No credentials in logs

Layer 2: Network Security
├── HTTPS for webhook endpoint
├── TLS for CTrader API connection
└── IP whitelist for webhooks (future)

Layer 3: Application Security
├── Input validation (signals, user input)
├── Order validation (prevent fat-finger)
└── Rate limiting (prevent abuse)
```

### 10.2 Webhook Security

```
Webhook Request Flow:
│
├─ HTTPS Only (production)
├─ Validate Content-Type: application/json
├─ Validate Payload Schema
├─ Check IP Whitelist (optional)
├─ Verify Signature (future enhancement)
└─ Rate Limiting: Max 10/second
```

## 11. Monitoring and Observability

### 11.1 Logging Architecture

```
Log Levels:
├── Debug   - Detailed flow information
├── Info    - Important events (connections, orders)
├── Warning - Recoverable issues
└── Error   - Critical failures

Log Destinations:
├── File    - FileLogListener → Data/Logs/
├── GUI     - GuiLogListener → Monitor control
└── Console - ConsoleLogListener (debug builds)
```

### 11.2 Metrics (Future Enhancement)

```
Application Metrics:
├── Performance
│   ├── Signal processing latency
│   ├── Order execution latency
│   └── Memory usage
│
├── Trading
│   ├── Orders per minute
│   ├── Fill rate
│   ├── Win rate
│   └── PnL
│
└── System
    ├── Connection uptime
    ├── Error rate
    └── Message throughput
```

## 12. Deployment Architecture

### 12.1 Development Environment

```
Development Setup:
├── Visual Studio 2022
├── .NET 8.0 SDK
├── CTrader Demo Account
├── ngrok (for TradingView webhook testing)
└── Postman (for webhook testing)
```

### 12.2 Production Environment (Future)

```
Production Setup:
├── Windows Server or Desktop
├── .NET Runtime
├── CTrader Live Account
├── SSL Certificate (for HTTPS webhook)
├── Reverse Proxy (nginx/IIS) for webhook
└── Monitoring/Alerting Service
```

## 13. References

### 13.1 Pattern References
- **Message-Based Architecture**: Enterprise Integration Patterns
- **Event-Driven**: Observer Pattern (Gang of Four)
- **Strategy Pattern**: Strategy Pattern (Gang of Four)
- **Layered Architecture**: Domain-Driven Design

### 13.2 StockSharp Architecture References
- [StockSharp Documentation - Architecture](https://doc.stocksharp.com)
- `Algo/Connector.cs` - Core connector implementation
- `Algo/Strategies/Strategy.cs` - Strategy base class
- `Messages/` - Message type definitions
