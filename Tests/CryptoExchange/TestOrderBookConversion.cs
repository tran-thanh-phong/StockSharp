using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestOrderBookConversion
{
    [TestMethod]
    public void BinanceOrderBook_ShouldConvert_ToQuoteChangeMessage()
    {
        // Arrange - This test MUST fail until QuoteChangeMessageConverter exists
        var binanceOrderBook = new MockBinanceOrderBook
        {
            Symbol = "BTCUSDT",
            Bids = new[]
            {
                new MockOrderBookEntry { Price = 49999m, Quantity = 1.5m },
                new MockOrderBookEntry { Price = 49998m, Quantity = 2.0m }
            },
            Asks = new[]
            {
                new MockOrderBookEntry { Price = 50001m, Quantity = 1.2m },
                new MockOrderBookEntry { Price = 50002m, Quantity = 1.8m }
            },
            LastUpdateId = 12345,
            UpdateTime = DateTimeOffset.UtcNow
        };

        // Act - Will fail because converter doesn't exist
        var converter = new QuoteChangeMessageConverter(); // Should not compile
        var result = converter.Convert(binanceOrderBook, new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" });

        // Assert - Should convert to QuoteChangeMessage
        Assert.IsInstanceOfType(result, typeof(QuoteChangeMessage));
        var quote = (QuoteChangeMessage)result;
        Assert.AreEqual("BTCUSDT", quote.SecurityId.SecurityCode);
        Assert.AreEqual("BINANCE", quote.SecurityId.BoardCode);

        // Bids should be sorted descending by price
        Assert.AreEqual(2, quote.Bids.Length);
        Assert.AreEqual(49999m, quote.Bids[0].Price);
        Assert.AreEqual(49998m, quote.Bids[1].Price);

        // Asks should be sorted ascending by price
        Assert.AreEqual(2, quote.Asks.Length);
        Assert.AreEqual(50001m, quote.Asks[0].Price);
        Assert.AreEqual(50002m, quote.Asks[1].Price);
    }

    [TestMethod]
    public void BinanceDepthUpdate_ShouldValidateOrderBookIntegrity()
    {
        // Arrange - Mock depth update with crossed market (should be invalid)
        var invalidOrderBook = new MockBinanceOrderBook
        {
            Symbol = "BTCUSDT",
            Bids = new[] { new MockOrderBookEntry { Price = 50002m, Quantity = 1.0m } }, // Bid higher than ask
            Asks = new[] { new MockOrderBookEntry { Price = 50001m, Quantity = 1.0m } }
        };

        // Act - Will fail until converter with validation exists
        var converter = new QuoteChangeMessageConverter(); // Should not compile

        // Assert - Should detect crossed market and handle appropriately
        Assert.ThrowsException<InvalidOperationException>(() =>
            converter.Convert(invalidOrderBook, new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" }));
    }

    // Mock classes for testing - will be replaced by actual Binance.Net types
    private class MockBinanceOrderBook
    {
        public string Symbol { get; set; } = "";
        public MockOrderBookEntry[] Bids { get; set; } = Array.Empty<MockOrderBookEntry>();
        public MockOrderBookEntry[] Asks { get; set; } = Array.Empty<MockOrderBookEntry>();
        public long LastUpdateId { get; set; }
        public DateTimeOffset UpdateTime { get; set; }
    }

    private class MockOrderBookEntry
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
}