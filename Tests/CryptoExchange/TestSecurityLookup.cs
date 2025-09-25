using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestSecurityLookup
{
    [TestMethod]
    public void SecurityLookupMessage_ShouldConvert_ToSecurityMessage()
    {
        // Arrange - This test MUST fail until SecurityMessageConverter is implemented
        var lookupMessage = new SecurityLookupMessage
        {
            SecurityId = new SecurityId
            {
                SecurityCode = "BTCUSDT",
                BoardCode = "BINANCE"
            },
            SecurityType = SecurityTypes.Stock
        };

        // Act - This will fail because SecurityMessageConverter doesn't exist yet
        var converter = new SecurityMessageConverter(); // Should not compile
        var result = converter.Convert(lookupMessage);

        // Assert - Should convert to proper SecurityMessage
        Assert.IsNotNull(result);
        Assert.AreEqual("BTCUSDT", result.SecurityId.SecurityCode);
        Assert.AreEqual("BINANCE", result.SecurityId.BoardCode);
        Assert.AreEqual(SecurityTypes.Stock, result.SecurityType);
    }

    [TestMethod]
    public void SecurityLookup_WithFuturesType_ShouldFilterCorrectly()
    {
        // Arrange
        var lookupMessage = new SecurityLookupMessage
        {
            SecurityType = SecurityTypes.Future
        };

        // Act - This will fail until framework is implemented
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessSecurityLookup(lookupMessage);

        // Assert
        Assert.IsTrue(result.All(s => s.SecurityType == SecurityTypes.Future));
    }
}