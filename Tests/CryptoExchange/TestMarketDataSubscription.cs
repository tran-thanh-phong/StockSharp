using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestMarketDataSubscription
{
    [TestMethod]
    public void MarketDataMessage_Ticks_ShouldSubscribeToTradeStream()
    {
        // Arrange - This test MUST fail until implementation
        var subscribeMessage = new MarketDataMessage
        {
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            DataType = DataType.Ticks,
            IsSubscribe = true
        };

        // Act - Will fail because adapter doesn't exist
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessMarketDataMessage(subscribeMessage);

        // Assert - Should establish WebSocket subscription
        Assert.IsTrue(adapter.IsSubscribedToTrades("BTCUSDT"));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void MarketDataMessage_MarketDepth_ShouldSubscribeToDepthStream()
    {
        // Arrange
        var subscribeMessage = new MarketDataMessage
        {
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            DataType = DataType.MarketDepth,
            IsSubscribe = true
        };

        // Act - Will fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessMarketDataMessage(subscribeMessage);

        // Assert
        Assert.IsTrue(adapter.IsSubscribedToDepth("BTCUSDT"));
    }

    [TestMethod]
    public void MarketDataMessage_CandleTimeFrame_ShouldSubscribeToKlineStream()
    {
        // Arrange
        var subscribeMessage = new MarketDataMessage
        {
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            DataType = DataType.Create<CandleMessage>(TimeSpan.FromMinutes(1)),
            IsSubscribe = true
        };

        // Act - Will fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessMarketDataMessage(subscribeMessage);

        // Assert
        Assert.IsTrue(adapter.IsSubscribedToCandles("BTCUSDT", "1m"));
    }
}