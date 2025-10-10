# Position Tracking in Strategies

## Overview

StockSharp strategies provide comprehensive position tracking capabilities, allowing you to monitor and manage positions at the strategy, security, and portfolio levels. Position information is automatically calculated based on order executions and trades.

**Location**: `StockSharp.Algo.Strategies.Strategy`, `StockSharp.Algo.Positions.PositionManager`

## Strategy.Position Property

### Basic Usage

The `Position` property returns the current net position for the strategy's primary security and portfolio:

```csharp
// Access current position
var currentPosition = Position;

if (currentPosition > 0)
    LogInfo("Long position: {0}", currentPosition);
else if (currentPosition < 0)
    LogInfo("Short position: {0}", Math.Abs(currentPosition));
else
    LogInfo("Flat (no position)");
```

### Position in Trading Logic

```csharp
private void ProcessCandle(ICandleMessage candle, decimal smaValue)
{
    if (!IsFormedAndOnlineAndAllowTrading())
        return;

    var price = candle.ClosePrice;

    // Check current position before trading
    if (price > smaValue && Position <= 0)
    {
        // No long position - enter long
        if (Position < 0)
            LogInfo("Reversing from short to long");

        BuyMarket(Volume);
    }
    else if (price < smaValue && Position >= 0)
    {
        // No short position - enter short
        if (Position > 0)
            LogInfo("Reversing from long to short");

        SellMarket(Volume);
    }
}
```

## GetPositionValue Method

### Signature

```csharp
public decimal? GetPositionValue(Security security = null, Portfolio portfolio = null)
```

### Purpose

Gets the position for a specific security and portfolio combination:

```csharp
// Get position for strategy's default security/portfolio
var position = GetPositionValue();

// Get position for specific security
var position = GetPositionValue(specificSecurity);

// Get position for specific security and portfolio
var position = GetPositionValue(specificSecurity, specificPortfolio);
```

### Return Value

- Returns `decimal?` (nullable decimal)
- `null` if no position information available
- `0` for flat position
- Positive value for long position
- Negative value for short position

## Multi-Security Position Tracking

### Tracking Multiple Securities

When trading multiple securities in a strategy:

```csharp
public class MultiSecurityStrategy : Strategy
{
    private Security _security1;
    private Security _security2;

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        _security1 = this.LookupById("AAPL@NASDAQ");
        _security2 = this.LookupById("MSFT@NASDAQ");

        // Subscribe to both
        SubscribeCandles(_security1, TimeSpan.FromMinutes(5).TimeFrame());
        SubscribeCandles(_security2, TimeSpan.FromMinutes(5).TimeFrame());
    }

    private void ProcessSignal(Security security, decimal signal)
    {
        // Get position for specific security
        var position = GetPositionValue(security);

        LogInfo("{0} position: {1}", security.Id, position ?? 0);

        if (signal > 0 && position <= 0)
        {
            // Buy this specific security
            var order = this.BuyMarket(security, Volume);
            order.Portfolio = Portfolio;
            RegisterOrder(order);
        }
        else if (signal < 0 && position >= 0)
        {
            // Sell this specific security
            var order = this.SellMarket(security, Volume);
            order.Portfolio = Portfolio;
            RegisterOrder(order);
        }
    }
}
```

## Position Manager

### Internal Position Tracking

The Strategy class uses an internal `PositionManager` to track positions:

```csharp
private readonly PositionManager _posManager;
```

### Calculation Methods

Position can be calculated by:
1. **Orders** - Based on filled order volume (default)
2. **Trades** - Based on executed trades

```csharp
public class PositionManager
{
    // Calculate by orders (default)
    public PositionManager(bool byOrders = true)

    // Process messages to update positions
    public PositionChangeMessage ProcessMessage(Message message)
}
```

## Position Change Events

### Monitoring Position Changes

The Strategy class tracks position changes through the `PositionManager`:

```csharp
// Internal position change handler
_posManager.ProcessOrder(order);  // Called when order updates
```

### Custom Position Tracking

You can implement custom position tracking:

```csharp
public class MyStrategy : Strategy
{
    private decimal _trackedPosition;

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);

        // Update custom position tracking
        var delta = trade.Trade.Volume;
        if (trade.Trade.Side == Sides.Sell)
            delta = -delta;

        _trackedPosition += delta;

        LogInfo("Trade: {0} {1} at {2}. Position: {3}",
            trade.Trade.Side,
            trade.Trade.Volume,
            trade.Trade.Price,
            _trackedPosition);
    }
}
```

## Position in Orders and Trades

### Trade.Position Property

Each `MyTrade` object includes the position after the trade:

```csharp
order.WhenNewTrade(this)
    .Do((MyTrade trade) =>
    {
        // Position is automatically set by strategy
        var positionAfterTrade = trade.Position;

        LogInfo("Trade executed: {0} at {1}. New position: {2}",
            trade.Trade.Volume,
            trade.Trade.Price,
            positionAfterTrade ?? 0);
    })
    .Apply(this);
```

### Setting Position in Custom Logic

```csharp
public override bool TryAddMyTrade(MyTrade trade)
{
    if (!base.TryAddMyTrade(trade))
        return false;

    // Get position for this security/portfolio
    trade.Position = GetPositionValue(
        trade.Order.Security,
        trade.Order.Portfolio
    );

    return true;
}
```

## Complete Position Tracking Example

```csharp
public class PositionAwareStrategy : Strategy
{
    private readonly StrategyParam<decimal> _maxPosition;
    private readonly StrategyParam<decimal> _targetPosition;

    public PositionAwareStrategy()
    {
        _maxPosition = Param(nameof(MaxPosition), 10m)
            .SetGreaterThanZero()
            .SetDisplay("Max Position", "Maximum allowed position size", "Risk");

        _targetPosition = Param(nameof(TargetPosition), 5m)
            .SetGreaterThanZero()
            .SetDisplay("Target Position", "Target position size per signal", "Trading");
    }

    public decimal MaxPosition
    {
        get => _maxPosition.Value;
        set => _maxPosition.Value = value;
    }

    public decimal TargetPosition
    {
        get => _targetPosition.Value;
        set => _targetPosition.Value = value;
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        LogInfo("Starting with position: {0}", Position);

        var sma = new SimpleMovingAverage { Length = 20 };
        Indicators.Add(sma);

        var subscription = SubscribeCandles(TimeSpan.FromMinutes(5).TimeFrame());
        subscription.Bind(sma, ProcessCandle).Start();
    }

    private void ProcessCandle(ICandleMessage candle, decimal smaValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        if (!IsFormedAndOnlineAndAllowTrading())
            return;

        var price = candle.ClosePrice;
        var currentPosition = Position;

        LogInfo("Current position: {0}", currentPosition);

        // Bullish signal
        if (price > smaValue)
        {
            ProcessBullishSignal(currentPosition);
        }
        // Bearish signal
        else if (price < smaValue)
        {
            ProcessBearishSignal(currentPosition);
        }
    }

    private void ProcessBullishSignal(decimal currentPosition)
    {
        // Check if we're already at max long position
        if (currentPosition >= MaxPosition)
        {
            LogInfo("At max long position ({0}), no action", currentPosition);
            return;
        }

        // Calculate how much we can buy
        var maxBuy = MaxPosition - currentPosition;
        var desiredBuy = TargetPosition;

        // Adjust buy volume based on current position and limits
        decimal buyVolume;
        if (currentPosition < 0)
        {
            // Currently short - close short first, then go long
            buyVolume = Math.Abs(currentPosition) + desiredBuy;
            LogInfo("Reversing from short to long. Buying: {0}", buyVolume);
        }
        else
        {
            // Adding to long position
            buyVolume = Math.Min(desiredBuy, maxBuy);
            LogInfo("Adding to long. Buying: {0}", buyVolume);
        }

        if (buyVolume > 0)
        {
            var order = BuyMarket(buyVolume);

            order.WhenMatched(this)
                .Do(() => LogInfo("Buy filled. New position: {0}", Position))
                .Apply(this);
        }
    }

    private void ProcessBearishSignal(decimal currentPosition)
    {
        // Check if we're already at max short position
        if (currentPosition <= -MaxPosition)
        {
            LogInfo("At max short position ({0}), no action", currentPosition);
            return;
        }

        // Calculate how much we can sell
        var maxSell = MaxPosition + currentPosition;  // currentPosition is negative
        var desiredSell = TargetPosition;

        // Adjust sell volume based on current position and limits
        decimal sellVolume;
        if (currentPosition > 0)
        {
            // Currently long - close long first, then go short
            sellVolume = currentPosition + desiredSell;
            LogInfo("Reversing from long to short. Selling: {0}", sellVolume);
        }
        else
        {
            // Adding to short position
            sellVolume = Math.Min(desiredSell, maxSell);
            LogInfo("Adding to short. Selling: {0}", sellVolume);
        }

        if (sellVolume > 0)
        {
            var order = SellMarket(sellVolume);

            order.WhenMatched(this)
                .Do(() => LogInfo("Sell filled. New position: {0}", Position))
                .Apply(this);
        }
    }

    protected override void OnStopped()
    {
        LogInfo("Stopped with final position: {0}", Position);
        base.OnStopped();
    }
}
```

## Position-Based Risk Management

### Position Size Limits

```csharp
private void EnterLong()
{
    var currentPosition = Position;

    // Check position limits
    if (currentPosition >= MaxPosition)
    {
        LogWarning("Cannot enter long - at max position: {0}", currentPosition);
        return;
    }

    // Calculate safe order size
    var orderSize = Math.Min(TargetVolume, MaxPosition - currentPosition);

    if (orderSize > 0)
    {
        BuyMarket(orderSize);
    }
}
```

### Position Flattening

```csharp
private void FlattenPosition()
{
    var currentPosition = Position;

    if (currentPosition == 0)
    {
        LogInfo("Already flat");
        return;
    }

    LogInfo("Flattening position: {0}", currentPosition);

    if (currentPosition > 0)
    {
        // Close long
        SellMarket(Math.Abs(currentPosition));
    }
    else
    {
        // Close short
        BuyMarket(Math.Abs(currentPosition));
    }
}
```

### Position Reversal

```csharp
private void ReversePosition()
{
    var currentPosition = Position;

    if (currentPosition == 0)
    {
        LogWarning("No position to reverse");
        return;
    }

    LogInfo("Reversing position: {0}", currentPosition);

    // Reverse position (close and open opposite)
    var reversalSize = Math.Abs(currentPosition) * 2;

    if (currentPosition > 0)
    {
        // Was long, go short
        SellMarket(reversalSize);
    }
    else
    {
        // Was short, go long
        BuyMarket(reversalSize);
    }
}
```

## Position Tracking by Portfolio

### Multiple Portfolios

```csharp
public class MultiPortfolioStrategy : Strategy
{
    private Portfolio _portfolio1;
    private Portfolio _portfolio2;

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        _portfolio1 = Connector.LookupByPortfolioName("Portfolio1");
        _portfolio2 = Connector.LookupByPortfolioName("Portfolio2");
    }

    private void CheckPositions()
    {
        // Get position for each portfolio
        var pos1 = GetPositionValue(Security, _portfolio1);
        var pos2 = GetPositionValue(Security, _portfolio2);

        LogInfo("Portfolio1 position: {0}", pos1 ?? 0);
        LogInfo("Portfolio2 position: {0}", pos2 ?? 0);
        LogInfo("Total position: {0}", (pos1 ?? 0) + (pos2 ?? 0));
    }

    private void TradeInPortfolio(Portfolio portfolio, Sides side, decimal volume)
    {
        var currentPos = GetPositionValue(Security, portfolio);
        LogInfo("{0} current position: {1}", portfolio.Name, currentPos ?? 0);

        var order = side == Sides.Buy
            ? this.BuyMarket(Security, volume)
            : this.SellMarket(Security, volume);

        order.Portfolio = portfolio;
        RegisterOrder(order);
    }
}
```

## Position Change Notification

### Listening for Position Changes

```csharp
public class MyStrategy : Strategy
{
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to position changes
        PositionChanged += OnPositionChanged;
    }

    private void OnPositionChanged(DateTimeOffset time)
    {
        LogInfo("Position changed to: {0} at {1}", Position, time);

        // React to position changes
        if (Position == 0)
        {
            LogInfo("Position is now flat");
            OnPositionFlat();
        }
    }

    protected override void OnStopped()
    {
        PositionChanged -= OnPositionChanged;
        base.OnStopped();
    }
}
```

## Best Practices

### 1. Always Check Position Before Trading

```csharp
// Good
var currentPosition = Position;
if (currentPosition <= 0)
{
    // Safe to enter long
    BuyMarket(Volume);
}

// Bad - may accumulate unwanted positions
BuyMarket(Volume);  // No position check!
```

### 2. Handle Null Position Values

```csharp
// Good
var position = GetPositionValue(security);
var posValue = position ?? 0;  // Handle null

if (posValue > 0)
    LogInfo("Long position: {0}", posValue);

// Bad
var position = GetPositionValue(security);
if (position > 0)  // NullReferenceException if position is null!
    LogInfo("Long position: {0}", position);
```

### 3. Log Position Changes

```csharp
// Good - track position changes
order.WhenMatched(this)
    .Do(() =>
    {
        LogInfo("Order filled. Position: {0} -> {1}",
            previousPosition, Position);
    })
    .Apply(this);
```

### 4. Implement Position Limits

```csharp
// Good - respects limits
if (Math.Abs(Position) >= MaxPosition)
{
    LogWarning("At max position, not trading");
    return;
}

// Bad - no position limits
BuyMarket(Volume);  // Could exceed risk limits!
```

### 5. Consider Position in P&L

```csharp
// Position affects P&L calculations
var unrealizedPnL = Position * (currentPrice - avgEntryPrice);
LogInfo("Unrealized P&L: {0}", unrealizedPnL);
```

## Common Patterns

### Flat Position Only Trading

```csharp
if (Position == 0)
{
    // Only trade when flat
    if (signal > 0)
        BuyMarket(Volume);
    else if (signal < 0)
        SellMarket(Volume);
}
```

### Scale In/Out

```csharp
if (signal > 0)
{
    // Scale into long position
    if (Position < TargetPosition)
    {
        var orderSize = Math.Min(ScaleSize, TargetPosition - Position);
        BuyMarket(orderSize);
    }
}
```

### One-Direction Only

```csharp
// Long only strategy
if (signal > 0 && Position == 0)
    BuyMarket(Volume);
else if (signal < 0 && Position > 0)
    SellMarket(Position);  // Only flatten, never short
```

## See Also

- [strategy-basics.md](strategy-basics.md) - Strategy fundamentals
- [risk-management.md](risk-management.md) - Position-based risk controls
- [statistics.md](statistics.md) - Position tracking in statistics
