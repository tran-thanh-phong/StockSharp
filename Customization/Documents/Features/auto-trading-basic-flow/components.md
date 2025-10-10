# Auto Trading Basic Flow - Component Breakdown

## 1. Component Overview

This document provides detailed implementation specifications for each component in the auto trading system.

## 2. Signal Manager Component

### 2.1 Purpose
Centralized hub for receiving, validating, and normalizing trading signals from multiple sources.

### 2.2 Class Structure

```csharp
namespace AutoTrading.Core.Signals
{
    /// <summary>
    /// Manages trading signals from multiple sources
    /// </summary>
    public class SignalManager : BaseLogReceiver
    {
        // Events
        public event Action<TradingSignal> SignalReceived;
        public event Action<SignalError> SignalError;

        // Signal sources
        private readonly List<ISignalSource> _signalSources = new();
        private readonly ConcurrentQueue<TradingSignal> _signalQueue = new();

        // Configuration
        public bool EnableSignalQueue { get; set; } = true;
        public int MaxQueueSize { get; set; } = 100;

        // Methods
        public void RegisterSignalSource(ISignalSource source);
        public void ProcessWebhookSignal(TradingViewWebhook webhook);
        public void ProcessMarketSignal(Security security, ICandleMessage candle,
                                       Dictionary<string, decimal> indicators);

        private void ValidateSignal(TradingSignal signal);
        private void NormalizeSignal(TradingSignal signal);
        private void EmitSignal(TradingSignal signal);
    }

    /// <summary>
    /// Interface for signal sources
    /// </summary>
    public interface ISignalSource
    {
        string Name { get; }
        event Action<TradingSignal> SignalGenerated;
        void Start();
        void Stop();
    }

    /// <summary>
    /// Normalized trading signal
    /// </summary>
    public class TradingSignal
    {
        public Guid SignalId { get; set; } = Guid.NewGuid();
        public string Source { get; set; }  // "TradingView", "Exchange", etc.
        public DateTimeOffset Timestamp { get; set; }

        // Security
        public SecurityId SecurityId { get; set; }
        public string Symbol { get; set; }

        // Trading decision
        public SignalAction Action { get; set; }  // Buy, Sell, Close
        public Sides Side { get; set; }

        // Price levels
        public decimal? EntryPrice { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? StopLoss { get; set; }

        // Optional metadata
        public Dictionary<string, object> Metadata { get; set; } = new();

        // Validation
        public bool IsValid => SecurityId != default &&
                               Action != SignalAction.None &&
                               (Action == SignalAction.Close || Side != default);
    }

    public enum SignalAction
    {
        None,
        Buy,
        Sell,
        Close
    }

    public class SignalError
    {
        public string Source { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public object RawData { get; set; }
    }
}
```

### 2.3 Implementation Details

#### 2.3.1 Webhook Signal Processing

```csharp
public void ProcessWebhookSignal(TradingViewWebhook webhook)
{
    try
    {
        this.AddInfoLog("Processing webhook signal: {0}", webhook.Symbol);

        var signal = new TradingSignal
        {
            Source = "TradingView",
            Timestamp = webhook.Timestamp ?? DateTimeOffset.UtcNow,
            Symbol = webhook.Symbol,
            Action = ParseAction(webhook.Action),
            Side = ParseSide(webhook.Action),
            EntryPrice = webhook.Price,
            TakeProfit = webhook.TakeProfit,
            StopLoss = webhook.StopLoss
        };

        // Map symbol to SecurityId (requires SecurityProvider)
        var security = _securityProvider.LookupById(signal.Symbol.ToStockSharp());
        if (security == null)
        {
            throw new InvalidOperationException($"Unknown symbol: {signal.Symbol}");
        }
        signal.SecurityId = security.ToSecurityId();

        ValidateSignal(signal);
        EmitSignal(signal);
    }
    catch (Exception ex)
    {
        this.AddErrorLog("Webhook signal processing failed: {0}", ex);
        SignalError?.Invoke(new SignalError
        {
            Source = "TradingView",
            Message = ex.Message,
            Exception = ex,
            RawData = webhook
        });
    }
}

private SignalAction ParseAction(string action)
{
    return action?.ToLowerInvariant() switch
    {
        "buy" => SignalAction.Buy,
        "sell" => SignalAction.Sell,
        "close" => SignalAction.Close,
        _ => SignalAction.None
    };
}

private Sides ParseSide(string action)
{
    return action?.ToLowerInvariant() switch
    {
        "buy" => Sides.Buy,
        "sell" => Sides.Sell,
        _ => default
    };
}
```

#### 2.3.2 Market Signal Processing

```csharp
public void ProcessMarketSignal(Security security, ICandleMessage candle,
                                Dictionary<string, decimal> indicators)
{
    try
    {
        this.AddDebugLog("Processing market signal: {0}, Close={1}",
                        security.Code, candle.ClosePrice);

        // Example: SMA crossover logic
        if (indicators.TryGetValue("FastSMA", out var fastSma) &&
            indicators.TryGetValue("SlowSMA", out var slowSma))
        {
            var signal = DetectCrossover(security, fastSma, slowSma, candle);
            if (signal != null)
            {
                EmitSignal(signal);
            }
        }
    }
    catch (Exception ex)
    {
        this.AddErrorLog("Market signal processing failed: {0}", ex);
        SignalError?.Invoke(new SignalError
        {
            Source = "Exchange",
            Message = ex.Message,
            Exception = ex,
            RawData = candle
        });
    }
}

private TradingSignal DetectCrossover(Security security, decimal fastSma,
                                     decimal slowSma, ICandleMessage candle)
{
    // Crossover detection logic here
    // Return signal if crossover detected, null otherwise
    return null;
}
```

### 2.4 Dependencies
- `ISecurityProvider` - For symbol lookup
- `BaseLogReceiver` - For logging

## 3. Trading Strategy Component

### 3.1 Purpose
Execute trading logic based on signals, manage orders, and coordinate protective orders.

### 3.2 Class Structure

```csharp
namespace AutoTrading.Strategies
{
    /// <summary>
    /// Signal-driven trading strategy
    /// </summary>
    public class SignalDrivenStrategy : Strategy
    {
        // Parameters
        private readonly StrategyParam<decimal> _riskPercentage;
        private readonly StrategyParam<bool> _enableTPSL;
        private readonly StrategyParam<bool> _useServerOrders;
        private readonly StrategyParam<decimal> _maxPositionSize;
        private readonly StrategyParam<int> _maxOpenPositions;

        // Dependencies
        public SignalManager SignalManager { get; set; }
        public RiskCalculator RiskCalculator { get; set; }

        // State
        private readonly Dictionary<long, Order> _activeOrders = new();
        private readonly Dictionary<long, Position> _openPositions = new();
        private ProtectiveController _protectiveController;

        // Properties
        public decimal RiskPercentage
        {
            get => _riskPercentage.Value;
            set => _riskPercentage.Value = value;
        }

        public bool EnableTPSL
        {
            get => _enableTPSL.Value;
            set => _enableTPSL.Value = value;
        }

        // Constructor
        public SignalDrivenStrategy();

        // Lifecycle methods
        protected override void OnStarted(DateTimeOffset time);
        protected override void OnStopped();

        // Signal handling
        private void OnSignalReceived(TradingSignal signal);
        private bool ValidateTradingConditions(TradingSignal signal);

        // Order management
        private void CreateAndRegisterOrder(TradingSignal signal);
        private void OnOrderReceived(Subscription sub, Order order);
        private void OnTradeReceived(Subscription sub, MyTrade trade);

        // Protective orders
        private void CreateProtectiveOrders(MyTrade trade, TradingSignal signal);
        private void OnProtectiveTriggered(Order order);
    }

    /// <summary>
    /// Position tracking
    /// </summary>
    public class Position
    {
        public Guid PositionId { get; set; }
        public Security Security { get; set; }
        public Sides Side { get; set; }
        public decimal Volume { get; set; }
        public decimal AveragePrice { get; set; }
        public DateTimeOffset OpenTime { get; set; }
        public TradingSignal OriginSignal { get; set; }

        // Protective orders
        public Order TakeProfitOrder { get; set; }
        public Order StopLossOrder { get; set; }

        // PnL tracking
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
    }
}
```

### 3.3 Implementation Details

#### 3.3.1 Strategy Initialization

```csharp
public SignalDrivenStrategy()
{
    _riskPercentage = Param(nameof(RiskPercentage), 2m)
        .SetGreaterThan(0)
        .SetLessThanOrEqual(10)
        .SetDisplay("Risk %", "Percentage of account to risk per trade", "Risk");

    _enableTPSL = Param(nameof(EnableTPSL), true)
        .SetDisplay("Enable TP/SL", "Enable protective take profit and stop loss orders", "Risk");

    _useServerOrders = Param(nameof(UseServerOrders), true)
        .SetDisplay("Use Server Orders", "Use broker's native TP/SL orders", "Risk");

    _maxPositionSize = Param(nameof(MaxPositionSize), 1.0m)
        .SetGreaterThan(0)
        .SetDisplay("Max Position", "Maximum position size in lots", "Risk");

    _maxOpenPositions = Param(nameof(MaxOpenPositions), 3)
        .SetGreaterThan(0)
        .SetDisplay("Max Positions", "Maximum number of concurrent positions", "Risk");
}

protected override void OnStarted(DateTimeOffset time)
{
    base.OnStarted(time);

    this.AddInfoLog("Strategy started at {0}", time);

    // Initialize protective controller
    var factory = _useServerOrders.Value
        ? new ServerProtectiveBehaviourFactory(Connector.Adapter)
        : (IProtectiveBehaviourFactory)new LocalProtectiveBehaviourFactory(
            Security.PriceStep, Security.Decimals);

    _protectiveController = new ProtectiveController();
    _protectiveController.Parent = this;

    // Subscribe to signals
    if (SignalManager != null)
    {
        SignalManager.SignalReceived += OnSignalReceived;
    }

    // Subscribe to connector events
    Connector.OrderReceived += OnOrderReceived;
    Connector.OwnTradeReceived += OnTradeReceived;

    this.AddInfoLog("Strategy initialization complete");
}

protected override void OnStopped()
{
    // Unsubscribe from events
    if (SignalManager != null)
    {
        SignalManager.SignalReceived -= OnSignalReceived;
    }

    Connector.OrderReceived -= OnOrderReceived;
    Connector.OwnTradeReceived -= OnTradeReceived;

    // Cancel all active orders
    foreach (var order in _activeOrders.Values.ToArray())
    {
        if (order.State == OrderStates.Active)
        {
            this.AddInfoLog("Cancelling order {0}", order.TransactionId);
            Connector.CancelOrder(order);
        }
    }

    base.OnStopped();
    this.AddInfoLog("Strategy stopped");
}
```

#### 3.3.2 Signal Processing

```csharp
private void OnSignalReceived(TradingSignal signal)
{
    this.AddInfoLog("Signal received: {0} {1} {2}", signal.Action, signal.Side, signal.Symbol);

    try
    {
        // Validate trading conditions
        if (!ValidateTradingConditions(signal))
        {
            this.AddWarningLog("Signal validation failed, skipping");
            return;
        }

        // Check if we should close position
        if (signal.Action == SignalAction.Close)
        {
            ClosePosition(signal.SecurityId);
            return;
        }

        // Create and register new order
        CreateAndRegisterOrder(signal);
    }
    catch (Exception ex)
    {
        this.AddErrorLog("Signal processing error: {0}", ex);
    }
}

private bool ValidateTradingConditions(TradingSignal signal)
{
    // Check strategy state
    if (ProcessState != ProcessStates.Started)
    {
        this.AddWarningLog("Strategy not started");
        return false;
    }

    // Check connector state
    if (Connector?.ConnectionState != ConnectionStates.Connected)
    {
        this.AddWarningLog("Not connected to broker");
        return false;
    }

    // Check signal validity
    if (!signal.IsValid)
    {
        this.AddWarningLog("Invalid signal");
        return false;
    }

    // Check max open positions
    if (_openPositions.Count >= _maxOpenPositions.Value)
    {
        this.AddWarningLog("Max open positions reached");
        return false;
    }

    // Check if already have position in this security
    if (_openPositions.Values.Any(p => p.Security.Id == signal.SecurityId.SecurityCode))
    {
        this.AddWarningLog("Already have position in {0}", signal.Symbol);
        return false;
    }

    return true;
}

private void CreateAndRegisterOrder(TradingSignal signal)
{
    var security = Connector.GetSecurity(signal.SecurityId);
    if (security == null)
    {
        this.AddErrorLog("Security not found: {0}", signal.SecurityId);
        return;
    }

    // Calculate position size
    var portfolio = Portfolio;
    var accountBalance = portfolio.CurrentValue ?? 0;

    var positionSize = RiskCalculator.CalculatePositionSize(
        accountBalance,
        _riskPercentage.Value,
        signal.EntryPrice ?? security.LastTrade?.Price ?? 0,
        signal.StopLoss ?? 0
    );

    // Apply max position limit
    positionSize = Math.Min(positionSize, _maxPositionSize.Value);

    this.AddInfoLog("Creating order: {0} {1} {2} @ {3}, SL={4}, TP={5}",
        signal.Side, positionSize, signal.Symbol, signal.EntryPrice,
        signal.StopLoss, signal.TakeProfit);

    // Create order
    var order = new Order
    {
        Security = security,
        Portfolio = portfolio,
        Side = signal.Side,
        Volume = positionSize,
        Price = signal.EntryPrice ?? security.BestBid?.Price ?? security.BestAsk?.Price ?? 0,
        Type = signal.EntryPrice.HasValue ? OrderTypes.Limit : OrderTypes.Market,
        TimeInForce = TimeInForce.GoodTillCancel
    };

    // Store signal with order for later use
    order.UserOrderId = signal.SignalId.ToString();

    // Register order
    RegisterOrder(order);

    this.AddInfoLog("Order registered: TransactionId={0}", order.TransactionId);
}
```

#### 3.3.3 Order and Trade Handling

```csharp
private void OnOrderReceived(Subscription sub, Order order)
{
    // Only process our own orders
    if (order.Security.Id != Security.Id)
        return;

    this.AddInfoLog("Order update: {0}, State={1}, Volume={2}/{3}",
        order.TransactionId, order.State, order.Volume - order.Balance, order.Volume);

    // Track active orders
    if (order.State == OrderStates.Active)
    {
        _activeOrders[order.TransactionId] = order;
    }
    else if (order.State == OrderStates.Done || order.State == OrderStates.Failed)
    {
        _activeOrders.Remove(order.TransactionId);

        if (order.State == OrderStates.Failed)
        {
            this.AddErrorLog("Order failed: {0}, Error={1}",
                order.TransactionId, order.Error?.Message);
        }
    }
}

private void OnTradeReceived(Subscription sub, MyTrade trade)
{
    // Only process our own trades
    if (trade.Order.Security.Id != Security.Id)
        return;

    this.AddInfoLog("Trade received: {0}, Price={1}, Volume={2}",
        trade.Trade.Id, trade.Trade.Price, trade.Trade.Volume);

    // Find or create position
    var existingPosition = _openPositions.Values
        .FirstOrDefault(p => p.Security.Id == trade.Order.Security.Id &&
                            p.Side == trade.Order.Side);

    if (existingPosition == null)
    {
        // Create new position
        var position = new Position
        {
            PositionId = Guid.NewGuid(),
            Security = trade.Order.Security,
            Side = trade.Order.Side,
            Volume = trade.Trade.Volume,
            AveragePrice = trade.Trade.Price,
            OpenTime = trade.Trade.Time
        };

        _openPositions[trade.Trade.Id] = position;

        this.AddInfoLog("Position opened: {0} {1} {2} @ {3}",
            position.Side, position.Volume, position.Security.Code, position.AveragePrice);

        // Create TP/SL if enabled
        if (_enableTPSL.Value)
        {
            // Get original signal
            TradingSignal signal = null;
            if (Guid.TryParse(trade.Order.UserOrderId, out var signalId))
            {
                // In practice, store signals in dictionary for lookup
                // signal = _signals[signalId];
            }

            CreateProtectiveOrders(trade, signal);
        }
    }
    else
    {
        // Update existing position (averaging)
        var totalVolume = existingPosition.Volume + trade.Trade.Volume;
        existingPosition.AveragePrice =
            (existingPosition.AveragePrice * existingPosition.Volume +
             trade.Trade.Price * trade.Trade.Volume) / totalVolume;
        existingPosition.Volume = totalVolume;

        this.AddInfoLog("Position updated: {0} {1} {2} @ {3}",
            existingPosition.Side, existingPosition.Volume,
            existingPosition.Security.Code, existingPosition.AveragePrice);
    }
}
```

#### 3.3.4 Protective Orders

```csharp
private void CreateProtectiveOrders(MyTrade trade, TradingSignal signal)
{
    try
    {
        var security = trade.Order.Security;
        var position = _openPositions.Values.First(p => p.Security.Id == security.Id);

        decimal? takeProfitPrice = signal?.TakeProfit;
        decimal? stopLossPrice = signal?.StopLoss;

        // If not provided in signal, calculate based on defaults
        if (takeProfitPrice == null || stopLossPrice == null)
        {
            var riskReward = 2.0m; // 1:2 risk/reward
            var riskPips = 50; // Default 50 pips risk

            if (stopLossPrice == null)
            {
                stopLossPrice = trade.Order.Side == Sides.Buy
                    ? trade.Trade.Price - riskPips * security.PriceStep
                    : trade.Trade.Price + riskPips * security.PriceStep;
            }

            if (takeProfitPrice == null)
            {
                var risk = Math.Abs(trade.Trade.Price - stopLossPrice.Value);
                takeProfitPrice = trade.Order.Side == Sides.Buy
                    ? trade.Trade.Price + risk * riskReward
                    : trade.Trade.Price - risk * riskReward;
            }
        }

        this.AddInfoLog("Creating protective orders: TP={0}, SL={1}",
            takeProfitPrice, stopLossPrice);

        // Get protective controller
        var factory = _useServerOrders.Value
            ? new ServerProtectiveBehaviourFactory(Connector.Adapter)
            : (IProtectiveBehaviourFactory)new LocalProtectiveBehaviourFactory(
                security.PriceStep, security.Decimals);

        var controller = _protectiveController.GetController(
            security.ToSecurityId(),
            Portfolio.Name,
            factory,
            takeValue: new Unit(Math.Abs(takeProfitPrice.Value - trade.Trade.Price),
                               UnitTypes.Absolute),
            stopValue: new Unit(Math.Abs(stopLossPrice.Value - trade.Trade.Price),
                               UnitTypes.Absolute),
            isStopTrailing: false,
            takeTimeout: TimeSpan.Zero,
            stopTimeout: TimeSpan.Zero,
            useMarketOrders: true
        );

        // Update controller with trade
        controller.Update(trade.Trade.Price, trade.Trade.Volume, trade.Trade.Time);

        // Try to activate protective orders
        var activations = _protectiveController.TryActivate(
            security.ToSecurityId(),
            trade.Trade.Price,
            trade.Trade.Time
        );

        foreach (var activation in activations)
        {
            var protectiveOrder = new Order
            {
                Security = security,
                Portfolio = Portfolio,
                Side = activation.side,
                Volume = activation.volume,
                Price = activation.price,
                Type = activation.condition != null ? OrderTypes.Conditional : OrderTypes.Limit,
                Condition = activation.condition
            };

            RegisterOrder(protectiveOrder);

            // Track protective order
            if (activation.isTake)
                position.TakeProfitOrder = protectiveOrder;
            else
                position.StopLossOrder = protectiveOrder;

            this.AddInfoLog("Registered {0} order: {1} @ {2}",
                activation.isTake ? "TP" : "SL",
                activation.volume, activation.price);
        }
    }
    catch (Exception ex)
    {
        this.AddErrorLog("Failed to create protective orders: {0}", ex);
    }
}

private void ClosePosition(SecurityId securityId)
{
    var position = _openPositions.Values
        .FirstOrDefault(p => p.Security.ToSecurityId() == securityId);

    if (position == null)
    {
        this.AddWarningLog("No position to close for {0}", securityId);
        return;
    }

    this.AddInfoLog("Closing position: {0} {1} {2}",
        position.Side, position.Volume, position.Security.Code);

    // Cancel protective orders if exist
    if (position.TakeProfitOrder != null && position.TakeProfitOrder.State == OrderStates.Active)
        Connector.CancelOrder(position.TakeProfitOrder);

    if (position.StopLossOrder != null && position.StopLossOrder.State == OrderStates.Active)
        Connector.CancelOrder(position.StopLossOrder);

    // Create market order to close
    var closeOrder = new Order
    {
        Security = position.Security,
        Portfolio = Portfolio,
        Side = position.Side.Invert(),
        Volume = position.Volume,
        Type = OrderTypes.Market
    };

    RegisterOrder(closeOrder);

    // Remove position tracking
    _openPositions.Remove(_openPositions.First(kv => kv.Value == position).Key);
}
```

### 3.4 Dependencies
- `Connector` - Trading operations
- `SignalManager` - Signal source
- `RiskCalculator` - Position sizing
- `ProtectiveController` - TP/SL management

## 4. Risk Calculator Component

### 4.1 Purpose
Calculate position sizes and validate risk parameters.

### 4.2 Class Structure

```csharp
namespace AutoTrading.Core.Risk
{
    public class RiskCalculator
    {
        /// <summary>
        /// Calculate position size based on risk parameters
        /// </summary>
        public decimal CalculatePositionSize(
            decimal accountBalance,
            decimal riskPercentage,
            decimal entryPrice,
            decimal stopLoss)
        {
            if (accountBalance <= 0)
                throw new ArgumentException("Account balance must be positive");

            if (riskPercentage <= 0 || riskPercentage > 100)
                throw new ArgumentException("Risk percentage must be between 0 and 100");

            if (entryPrice <= 0)
                throw new ArgumentException("Entry price must be positive");

            if (stopLoss <= 0)
                throw new ArgumentException("Stop loss must be positive");

            // Calculate risk amount in account currency
            var riskAmount = accountBalance * (riskPercentage / 100m);

            // Calculate price risk per unit
            var priceRisk = Math.Abs(entryPrice - stopLoss);

            if (priceRisk == 0)
                return 0;

            // Position size = Risk Amount / Price Risk
            var positionSize = riskAmount / priceRisk;

            return positionSize;
        }

        /// <summary>
        /// Calculate required margin for position
        /// </summary>
        public decimal CalculateRequiredMargin(
            decimal positionSize,
            decimal price,
            decimal leverage)
        {
            return (positionSize * price) / leverage;
        }

        /// <summary>
        /// Validate if position can be opened
        /// </summary>
        public bool CanOpenPosition(
            decimal accountBalance,
            decimal requiredMargin,
            decimal usedMargin,
            decimal marginCallLevel = 0.5m)
        {
            var availableMargin = accountBalance - usedMargin;
            var marginAfterTrade = availableMargin - requiredMargin;

            // Check if margin level stays above margin call level
            return (marginAfterTrade / accountBalance) >= marginCallLevel;
        }
    }
}
```

## 5. Webhook Server Component

### 5.1 Purpose
Receive and process TradingView webhook requests.

### 5.2 Implementation

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoTrading.WebhookServer
{
    public class WebhookServer
    {
        private WebApplication _app;
        private readonly SignalManager _signalManager;
        private readonly int _port;

        public WebhookServer(SignalManager signalManager, int port = 5000)
        {
            _signalManager = signalManager;
            _port = port;
        }

        public void Start()
        {
            var builder = WebApplication.CreateBuilder();

            // Configure services
            builder.Services.AddSingleton(_signalManager);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            _app = builder.Build();

            // Configure middleware
            _app.UseHttpsRedirection();

            // Webhook endpoint
            _app.MapPost("/api/tradingview/webhook", async (
                HttpContext context,
                SignalManager signalManager,
                ILogger<WebhookServer> logger) =>
            {
                try
                {
                    // Read request body
                    var webhook = await context.Request.ReadFromJsonAsync<TradingViewWebhook>();

                    if (webhook == null)
                    {
                        logger.LogWarning("Invalid webhook payload");
                        return Results.BadRequest(new { error = "Invalid payload" });
                    }

                    logger.LogInformation("Webhook received: {Symbol} {Action}",
                        webhook.Symbol, webhook.Action);

                    // Process signal
                    signalManager.ProcessWebhookSignal(webhook);

                    return Results.Ok(new
                    {
                        status = "received",
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Webhook processing error");
                    return Results.StatusCode(500);
                }
            });

            // Health check endpoint
            _app.MapGet("/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow
            }));

            // Start server
            _app.RunAsync($"http://localhost:{_port}");
        }

        public void Stop()
        {
            _app?.StopAsync().Wait();
            _app?.DisposeAsync().AsTask().Wait();
        }
    }

    /// <summary>
    /// TradingView webhook payload
    /// </summary>
    public class TradingViewWebhook
    {
        public string Symbol { get; set; }
        public string Action { get; set; }  // "buy", "sell", "close"
        public decimal? Price { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? StopLoss { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public Dictionary<string, object> Extra { get; set; }
    }
}
```

## 6. UI Components

### 6.1 Main Window

```csharp
namespace AutoTrading.UI
{
    public partial class MainWindow
    {
        public Connector Connector { get; private set; }
        public LogManager LogManager { get; private set; }
        public SignalManager SignalManager { get; private set; }
        public SignalDrivenStrategy Strategy { get; private set; }
        public WebhookServer WebhookServer { get; private set; }

        private readonly OrdersWindow _ordersWindow;
        private readonly TradesWindow _tradesWindow;
        private readonly PortfolioWindow _portfolioWindow;
        private readonly SignalConfigWindow _signalConfigWindow;

        private bool _isConnected;
        private readonly string _settingsFile = "Data/connection.json";

        public MainWindow()
        {
            InitializeComponent();

            // Initialize logging
            LogManager = new LogManager();
            LogManager.Listeners.Add(new FileLogListener
            {
                LogDirectory = "Data/Logs"
            });
            LogManager.Listeners.Add(new GuiLogListener(Monitor));

            // Initialize connector
            var entityRegistry = new CsvEntityRegistry("Data");
            var exchangeInfo = new StorageExchangeInfoProvider(entityRegistry);
            var storageRegistry = new StorageRegistry(exchangeInfo);
            var snapshotRegistry = new SnapshotRegistry("Data/Snapshots");

            Connector = new Connector(
                entityRegistry.Securities,
                entityRegistry.PositionStorage,
                exchangeInfo,
                storageRegistry,
                snapshotRegistry,
                new StorageBuffer());

            LogManager.Sources.Add(Connector);

            // Initialize components
            SignalManager = new SignalManager();
            LogManager.Sources.Add(SignalManager);

            // Initialize strategy
            Strategy = new SignalDrivenStrategy
            {
                Connector = Connector,
                SignalManager = SignalManager,
                Security = null,  // Set when connected
                Portfolio = null, // Set when connected
                RiskCalculator = new RiskCalculator()
            };
            LogManager.Sources.Add(Strategy);

            // Initialize webhook server (optional)
            if (Settings.EnableTradingViewWebhook)
            {
                WebhookServer = new WebhookServer(SignalManager, 5000);
            }

            // Initialize child windows
            _ordersWindow = new OrdersWindow();
            _tradesWindow = new TradesWindow();
            _portfolioWindow = new PortfolioWindow();
            _signalConfigWindow = new SignalConfigWindow();

            // Setup connector events
            InitializeConnectorEvents();

            // Load settings
            LoadSettings();
        }

        private void InitializeConnectorEvents()
        {
            Connector.Connected += () =>
            {
                this.GuiAsync(() =>
                {
                    _isConnected = true;
                    ConnectBtn.Content = "Disconnect";
                    ConnectBtn.Background = System.Windows.Media.Brushes.LightGreen;
                    EnableTradingControls(true);
                });
            };

            Connector.Disconnected += () =>
            {
                this.GuiAsync(() =>
                {
                    _isConnected = false;
                    ConnectBtn.Content = "Connect";
                    ConnectBtn.Background = System.Windows.Media.Brushes.LightPink;
                    EnableTradingControls(false);
                });
            };

            Connector.ConnectionError += error =>
            {
                this.GuiAsync(() =>
                {
                    MessageBox.Show(this, error.ToString(), "Connection Error");
                });
            };

            Connector.OrderReceived += (sub, order) =>
            {
                _ordersWindow.OrderGrid.Orders.TryAdd(order);
            };

            Connector.OwnTradeReceived += (sub, trade) =>
            {
                _tradesWindow.TradeGrid.Trades.TryAdd(trade);
            };

            Connector.PositionReceived += (sub, position) =>
            {
                _portfolioWindow.PortfolioGrid.Positions.TryAdd(position);
            };
        }

        // Event handlers...
    }
}
```

## 7. Component Integration

### 7.1 Initialization Sequence

```
1. Application Start
   └─> MainWindow.Constructor
       ├─> Create LogManager
       ├─> Create Connector
       ├─> Create SignalManager
       ├─> Create Strategy
       ├─> Create WebhookServer (if enabled)
       ├─> Create UI Windows
       └─> Load Settings

2. User Clicks "Connect"
   └─> Connector.Connect()
       ├─> CTraderAdapter.ConnectAsync()
       └─> Connected Event
           ├─> Enable UI Controls
           └─> Start WebhookServer

3. User Clicks "Start Strategy"
   └─> Strategy.Start()
       ├─> Subscribe to SignalManager
       ├─> Subscribe to Connector events
       └─> Initialize ProtectiveController

4. Signal Arrives
   └─> SignalManager.SignalReceived Event
       └─> Strategy.OnSignalReceived()
           ├─> Validate Conditions
           ├─> Calculate Position Size
           ├─> Create Order
           └─> Connector.RegisterOrder()

5. Order Filled
   └─> Connector.OwnTradeReceived Event
       └─> Strategy.OnTradeReceived()
           ├─> Update Position
           └─> Create TP/SL Orders
```

This component breakdown provides detailed implementation guidance for each part of the auto trading system.
