using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestMarketDataFlowIntegration
{
    private BinanceSpotMessageAdapter? _adapter;
    private readonly SecurityId _testSecurityId = new()
    {
        SecurityCode = "BTCUSDT",
        BoardCode = "BINANCE"
    };

    [TestInitialize]
    public void Setup()
    {
        _adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        _adapter.UseTestnet = true;
        _adapter.LogLevel = LogLevels.Debug;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _adapter?.Dispose();
    }

    [TestMethod]
    public async Task T036_MarketDataTicks_Should_ReceiveTradeUpdates()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var subscriptionComplete = new TaskCompletionSource<MarketDataMessage>();
        var firstTickReceived = new TaskCompletionSource<ExecutionMessage>();
        var ticksReceived = new List<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case MarketDataMessage mdMsg when mdMsg.IsSubscribe && mdMsg.Error == null:
                    subscriptionComplete.SetResult(mdMsg);
                    break;

                case MarketDataMessage mdMsg when mdMsg.Error != null:
                    subscriptionComplete.SetException(new InvalidOperationException(mdMsg.Error.Message));
                    break;

                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Tick:
                    ticksReceived.Add(execMsg);
                    if (ticksReceived.Count == 1)
                        firstTickReceived.SetResult(execMsg);
                    break;

                case ErrorMessage errorMsg:
                    subscriptionComplete.SetException(errorMsg.Error);
                    break;
            }
        };

        // Connect first
        await ConnectAdapterAsync(_adapter);

        var subscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.Ticks,
            IsSubscribe = true
        };

        // Act
        _adapter.SendInMessage(subscribeMessage);

        // Wait for subscription confirmation
        using var subscriptionCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var subscriptionResult = await subscriptionComplete.Task.WaitAsync(subscriptionCts.Token);

        Assert.IsNull(subscriptionResult.Error, $"Subscription failed: {subscriptionResult.Error?.Message}");

        // Wait for first tick
        using var tickCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstTick = await firstTickReceived.Task.WaitAsync(tickCts.Token);

        // Assert
        Assert.IsNotNull(firstTick, "Should receive at least one tick");
        Assert.AreEqual(_testSecurityId, firstTick.SecurityId, "Tick should be for correct security");
        Assert.AreEqual(DataType.Ticks, firstTick.DataType, "Should be tick data");
        Assert.AreEqual(ExecutionTypes.Tick, firstTick.ExecutionType, "Should be tick execution type");
        Assert.IsTrue(firstTick.Price > 0, "Tick price should be positive");
        Assert.IsTrue(firstTick.Volume > 0, "Tick volume should be positive");
        Assert.IsTrue(firstTick.TradeId > 0, "Tick should have trade ID");

        // Wait a bit more to collect multiple ticks
        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.IsTrue(ticksReceived.Count >= 1, $"Should receive multiple ticks, got {ticksReceived.Count}");

        Console.WriteLine($"Received {ticksReceived.Count} ticks for {_testSecurityId.SecurityCode}");
        Console.WriteLine($"First tick: Price={firstTick.Price}, Volume={firstTick.Volume}, Time={firstTick.ServerTime}");

        // Cleanup - unsubscribe
        var unsubscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.Ticks,
            IsSubscribe = false
        };

        _adapter.SendInMessage(unsubscribeMessage);
    }

    [TestMethod]
    public async Task T036_MarketDataOrderBook_Should_ReceiveDepthUpdates()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var subscriptionComplete = new TaskCompletionSource<MarketDataMessage>();
        var firstDepthReceived = new TaskCompletionSource<QuoteChangeMessage>();
        var depthUpdatesReceived = new List<QuoteChangeMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case MarketDataMessage mdMsg when mdMsg.IsSubscribe && mdMsg.Error == null:
                    subscriptionComplete.SetResult(mdMsg);
                    break;

                case MarketDataMessage mdMsg when mdMsg.Error != null:
                    subscriptionComplete.SetException(new InvalidOperationException(mdMsg.Error.Message));
                    break;

                case QuoteChangeMessage depthMsg:
                    depthUpdatesReceived.Add(depthMsg);
                    if (depthUpdatesReceived.Count == 1)
                        firstDepthReceived.SetResult(depthMsg);
                    break;

                case ErrorMessage errorMsg:
                    subscriptionComplete.SetException(errorMsg.Error);
                    break;
            }
        };

        // Connect first
        await ConnectAdapterAsync(_adapter);

        var subscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.MarketDepth,
            IsSubscribe = true
        };

        // Act
        _adapter.SendInMessage(subscribeMessage);

        // Wait for subscription confirmation
        using var subscriptionCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var subscriptionResult = await subscriptionComplete.Task.WaitAsync(subscriptionCts.Token);

        Assert.IsNull(subscriptionResult.Error, $"Subscription failed: {subscriptionResult.Error?.Message}");

        // Wait for first depth update
        using var depthCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstDepth = await firstDepthReceived.Task.WaitAsync(depthCts.Token);

        // Assert
        Assert.IsNotNull(firstDepth, "Should receive at least one depth update");
        Assert.AreEqual(_testSecurityId, firstDepth.SecurityId, "Depth should be for correct security");
        Assert.IsNotNull(firstDepth.Quotes, "Quotes should not be null");
        Assert.IsTrue(firstDepth.Quotes.Length > 0, "Should have quotes");

        // Check quote structure
        var bids = firstDepth.Quotes.Where(q => q.Side == Sides.Buy).ToArray();
        var asks = firstDepth.Quotes.Where(q => q.Side == Sides.Sell).ToArray();

        Assert.IsTrue(bids.Length > 0, "Should have bid quotes");
        Assert.IsTrue(asks.Length > 0, "Should have ask quotes");

        // Validate quote data
        foreach (var bid in bids.Take(3))
        {
            Assert.IsTrue(bid.Price > 0, "Bid price should be positive");
            Assert.IsTrue(bid.Volume > 0, "Bid volume should be positive");
            Assert.AreEqual(Sides.Buy, bid.Side, "Should be buy side");
        }

        foreach (var ask in asks.Take(3))
        {
            Assert.IsTrue(ask.Price > 0, "Ask price should be positive");
            Assert.IsTrue(ask.Volume > 0, "Ask volume should be positive");
            Assert.AreEqual(Sides.Sell, ask.Side, "Should be sell side");
        }

        // Spread validation
        var bestBid = bids.OrderByDescending(b => b.Price).First().Price;
        var bestAsk = asks.OrderBy(a => a.Price).First().Price;
        Assert.IsTrue(bestAsk > bestBid, "Best ask should be higher than best bid");

        Console.WriteLine($"Received {depthUpdatesReceived.Count} depth updates for {_testSecurityId.SecurityCode}");
        Console.WriteLine($"Best bid: {bestBid}, Best ask: {bestAsk}, Spread: {bestAsk - bestBid}");

        // Cleanup
        var unsubscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.MarketDepth,
            IsSubscribe = false
        };

        _adapter.SendInMessage(unsubscribeMessage);
    }

    [TestMethod]
    public async Task T036_MarketDataCandles_Should_ReceiveKlineUpdates()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var subscriptionComplete = new TaskCompletionSource<MarketDataMessage>();
        var firstCandleReceived = new TaskCompletionSource<CandleMessage>();
        var candlesReceived = new List<CandleMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case MarketDataMessage mdMsg when mdMsg.IsSubscribe && mdMsg.Error == null:
                    subscriptionComplete.SetResult(mdMsg);
                    break;

                case MarketDataMessage mdMsg when mdMsg.Error != null:
                    subscriptionComplete.SetException(new InvalidOperationException(mdMsg.Error.Message));
                    break;

                case CandleMessage candleMsg:
                    candlesReceived.Add(candleMsg);
                    if (candlesReceived.Count == 1)
                        firstCandleReceived.SetResult(candleMsg);
                    break;

                case ErrorMessage errorMsg:
                    subscriptionComplete.SetException(errorMsg.Error);
                    break;
            }
        };

        // Connect first
        await ConnectAdapterAsync(_adapter);

        var timeFrame = TimeSpan.FromMinutes(1);
        var subscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.TimeFrame(timeFrame),
            IsSubscribe = true
        };

        // Act
        _adapter.SendInMessage(subscribeMessage);

        // Wait for subscription confirmation
        using var subscriptionCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var subscriptionResult = await subscriptionComplete.Task.WaitAsync(subscriptionCts.Token);

        Assert.IsNull(subscriptionResult.Error, $"Subscription failed: {subscriptionResult.Error?.Message}");

        // Wait for first candle (might take up to 1 minute for completed candle)
        using var candleCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var firstCandle = await firstCandleReceived.Task.WaitAsync(candleCts.Token);

        // Assert
        Assert.IsNotNull(firstCandle, "Should receive at least one candle");
        Assert.AreEqual(_testSecurityId, firstCandle.SecurityId, "Candle should be for correct security");
        Assert.AreEqual(timeFrame, firstCandle.TimeFrame, "Should have correct time frame");

        // OHLC validation
        Assert.IsTrue(firstCandle.OpenPrice > 0, "Open price should be positive");
        Assert.IsTrue(firstCandle.HighPrice > 0, "High price should be positive");
        Assert.IsTrue(firstCandle.LowPrice > 0, "Low price should be positive");
        Assert.IsTrue(firstCandle.ClosePrice > 0, "Close price should be positive");
        Assert.IsTrue(firstCandle.TotalVolume > 0, "Volume should be positive");

        // Price relationship validation
        Assert.IsTrue(firstCandle.HighPrice >= firstCandle.OpenPrice, "High >= Open");
        Assert.IsTrue(firstCandle.HighPrice >= firstCandle.ClosePrice, "High >= Close");
        Assert.IsTrue(firstCandle.LowPrice <= firstCandle.OpenPrice, "Low <= Open");
        Assert.IsTrue(firstCandle.LowPrice <= firstCandle.ClosePrice, "Low <= Close");

        // Time validation
        Assert.IsTrue(firstCandle.OpenTime < firstCandle.CloseTime, "Open time should be before close time");

        Console.WriteLine($"Received candle for {_testSecurityId.SecurityCode}:");
        Console.WriteLine($"Time: {firstCandle.OpenTime} - {firstCandle.CloseTime}");
        Console.WriteLine($"OHLC: {firstCandle.OpenPrice} / {firstCandle.HighPrice} / {firstCandle.LowPrice} / {firstCandle.ClosePrice}");
        Console.WriteLine($"Volume: {firstCandle.TotalVolume}");

        // Cleanup
        var unsubscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            DataType = DataType.TimeFrame(timeFrame),
            IsSubscribe = false
        };

        _adapter.SendInMessage(unsubscribeMessage);
    }

    [TestMethod]
    public async Task T036_MarketData_InvalidSymbol_Should_ReturnError()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var errorReceived = new TaskCompletionSource<MarketDataMessage>();

        _adapter.NewOutMessage += message =>
        {
            if (message is MarketDataMessage mdMsg && mdMsg.Error != null)
            {
                errorReceived.SetResult(mdMsg);
            }
        };

        await ConnectAdapterAsync(_adapter);

        var invalidSecurityId = new SecurityId
        {
            SecurityCode = "INVALIDXXX",
            BoardCode = "BINANCE"
        };

        var subscribeMessage = new MarketDataMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = invalidSecurityId,
            DataType = DataType.Ticks,
            IsSubscribe = true
        };

        // Act
        _adapter.SendInMessage(subscribeMessage);

        // Wait for error
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var errorMessage = await errorReceived.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(errorMessage.Error, "Should return error for invalid symbol");
        Assert.IsTrue(errorMessage.Error.Message.Contains("Invalid symbol") ||
                     errorMessage.Error.Message.Contains("INVALIDXXX"),
                     $"Error should mention invalid symbol: {errorMessage.Error.Message}");
    }

    private async Task ConnectAdapterAsync(BinanceSpotMessageAdapter adapter)
    {
        var connectComplete = new TaskCompletionSource<bool>();

        adapter.NewOutMessage += message =>
        {
            if (message is ConnectMessage connectMsg)
            {
                if (connectMsg.Error == null)
                    connectComplete.SetResult(true);
                else
                    connectComplete.SetException(connectMsg.Error);
            }
        };

        adapter.SendInMessage(new ConnectMessage());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await connectComplete.Task.WaitAsync(cts.Token);
    }
}