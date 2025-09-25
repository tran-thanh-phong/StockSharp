using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestTradeConversion
{
    [TestMethod]
    public void BinanceTradeEvent_ShouldConvert_ToExecutionMessage()
    {
        // Arrange - This test MUST fail until ExecutionMessageConverter exists
        var binanceTrade = new MockBinanceTrade
        {
            Symbol = "BTCUSDT",
            TradeId = 123456789,
            Price = 50000.00m,
            Quantity = 0.001m,
            IsBuyerMaker = true,
            TradeTime = DateTimeOffset.UtcNow
        };

        // Act - Will fail because converter doesn't exist
        var converter = new ExecutionMessageConverter(); // Should not compile
        var result = converter.Convert(binanceTrade, new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" });

        // Assert - Should convert to ExecutionMessage with Ticks data type
        Assert.IsInstanceOfType(result, typeof(ExecutionMessage));
        var execution = (ExecutionMessage)result;
        Assert.AreEqual(DataType.Ticks, execution.DataType);
        Assert.AreEqual(123456789, execution.TradeId);
        Assert.AreEqual(50000.00m, execution.TradePrice);
        Assert.AreEqual(0.001m, execution.TradeVolume);
        Assert.AreEqual(Sides.Sell, execution.OriginSide); // IsBuyerMaker = true means taker was seller
    }

    [TestMethod]
    public void BinanceTradeStream_ShouldHandleMultipleTrades()
    {
        // Arrange - Mock multiple trades
        var trades = new[]
        {
            new MockBinanceTrade { TradeId = 1, Price = 50000m, Quantity = 0.001m },
            new MockBinanceTrade { TradeId = 2, Price = 50001m, Quantity = 0.002m },
            new MockBinanceTrade { TradeId = 3, Price = 49999m, Quantity = 0.0015m }
        };

        // Act - Will fail until converter exists
        var converter = new ExecutionMessageConverter(); // Should not compile
        var results = trades.Select(t => converter.Convert(t, new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" }));

        // Assert - All trades should be converted
        Assert.AreEqual(3, results.Count());
        Assert.IsTrue(results.All(r => r.DataType == DataType.Ticks));
    }

    // Mock class for testing - will be replaced by actual Binance.Net types
    private class MockBinanceTrade
    {
        public string Symbol { get; set; } = "";
        public long TradeId { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public bool IsBuyerMaker { get; set; }
        public DateTimeOffset TradeTime { get; set; }
    }
}