using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestOrderRegistration
{
    [TestMethod]
    public void OrderRegisterMessage_MarketBuy_ShouldPlaceOrderOnBinance()
    {
        // Arrange - This test MUST fail until implementation
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12345,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Market,
            Volume = 0.001m
        };

        // Act - Will fail because adapter doesn't exist
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessOrderRegisterMessage(orderMessage);

        // Assert - Should return ExecutionMessage with order confirmation
        Assert.IsInstanceOfType(result, typeof(ExecutionMessage));
        var execution = (ExecutionMessage)result;
        Assert.AreEqual(12345, execution.TransactionId);
        Assert.AreEqual(ExecutionTypes.Transaction, execution.ExecutionType);
        Assert.IsNotNull(execution.OrderId);
    }

    [TestMethod]
    public void OrderRegisterMessage_LimitOrder_ShouldValidatePriceStep()
    {
        // Arrange
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12346,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 50000.123m // Invalid precision
        };

        // Act - Will fail until validation is implemented
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile

        // Assert - Should throw validation error for price precision
        Assert.ThrowsException<InvalidOperationException>(() =>
            adapter.ProcessOrderRegisterMessage(orderMessage));
    }

    [TestMethod]
    public void OrderRegisterMessage_StopLoss_ShouldIncludeStopPrice()
    {
        // Arrange
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12347,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Sell,
            OrderType = OrderTypes.StopLimit,
            Volume = 0.001m,
            Price = 49000m,
            StopPrice = 49500m
        };

        // Act - Will fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        var result = adapter.ProcessOrderRegisterMessage(orderMessage);

        // Assert
        Assert.IsInstanceOfType(result, typeof(ExecutionMessage));
        var execution = (ExecutionMessage)result;
        Assert.AreEqual(49500m, execution.StopPrice);
    }
}