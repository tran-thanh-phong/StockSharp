using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestMarketDataFlow
{
    [TestMethod]
    public void BinanceSpot_ShouldReceive_RealTimeTradeData()
    {
        // Arrange - This test MUST fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var tradesReceived = new List<ExecutionMessage>();
        var tradeReceivedEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execution && execution.DataType == DataType.Ticks)
            {
                tradesReceived.Add(execution);
                tradeReceivedEvent.Set();
            }
        };

        var subscribeMessage = new MarketDataMessage
        {
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            DataType = DataType.Ticks,
            IsSubscribe = true
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(subscribeMessage);

        var tradesReceived = tradeReceivedEvent.Wait(TimeSpan.FromSeconds(30));

        // Assert - Should receive trade data within 30 seconds
        Assert.IsTrue(tradesReceived, "No trade data received within timeout");
        Assert.IsTrue(tradesReceived.Count > 0);
        Assert.IsTrue(tradesReceived.All(t => t.SecurityId.SecurityCode == "BTCUSDT"));
        Assert.IsTrue(tradesReceived.All(t => t.TradePrice > 0));
        Assert.IsTrue(tradesReceived.All(t => t.TradeVolume > 0));
    }

    [TestMethod]
    public void BinanceSpot_ShouldReceive_OrderBookUpdates()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var depthUpdates = new List<QuoteChangeMessage>();
        var depthReceivedEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is QuoteChangeMessage depth)
            {
                depthUpdates.Add(depth);
                depthReceivedEvent.Set();
            }
        };

        var subscribeMessage = new MarketDataMessage
        {
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            DataType = DataType.MarketDepth,
            IsSubscribe = true
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(subscribeMessage);

        var depthReceived = depthReceivedEvent.Wait(TimeSpan.FromSeconds(10));

        // Assert
        Assert.IsTrue(depthReceived, "No order book data received");
        Assert.IsTrue(depthUpdates.Count > 0);

        var firstUpdate = depthUpdates.First();
        Assert.IsNotNull(firstUpdate.Bids);
        Assert.IsNotNull(firstUpdate.Asks);
        Assert.IsTrue(firstUpdate.Bids.Length > 0);
        Assert.IsTrue(firstUpdate.Asks.Length > 0);

        // Verify order book integrity
        Assert.IsTrue(firstUpdate.Bids[0].Price < firstUpdate.Asks[0].Price, "Order book is crossed");
    }

    [TestMethod]
    public void BinanceSpot_ShouldHandle_MultipleSymbolSubscriptions()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "ADAUSDT" };
        var receivedSymbols = new HashSet<string>();
        var allSymbolsEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execution && execution.DataType == DataType.Ticks)
            {
                receivedSymbols.Add(execution.SecurityId.SecurityCode);
                if (receivedSymbols.Count >= symbols.Length)
                    allSymbolsEvent.Set();
            }
        };

        // Act - Subscribe to multiple symbols
        adapter.ConnectAsync().Wait();

        foreach (var symbol in symbols)
        {
            var subscribeMessage = new MarketDataMessage
            {
                SecurityId = new SecurityId { SecurityCode = symbol, BoardCode = "BINANCE" },
                DataType = DataType.Ticks,
                IsSubscribe = true
            };
            adapter.SendInMessage(subscribeMessage);
        }

        var allReceived = allSymbolsEvent.Wait(TimeSpan.FromMinutes(1));

        // Assert
        Assert.IsTrue(allReceived, "Not all symbols received data");
        Assert.AreEqual(symbols.Length, receivedSymbols.Count);
        foreach (var symbol in symbols)
        {
            Assert.IsTrue(receivedSymbols.Contains(symbol), $"Missing data for {symbol}");
        }
    }
}