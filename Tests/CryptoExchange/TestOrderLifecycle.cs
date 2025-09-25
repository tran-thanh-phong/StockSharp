using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestOrderLifecycle
{
    [TestMethod]
    public void OrderLifecycle_MarketOrder_ShouldExecuteImmediately()
    {
        // Arrange - This test MUST fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var orderUpdates = new List<ExecutionMessage>();
        var orderCompleteEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execution && execution.DataType == DataType.Transactions)
            {
                orderUpdates.Add(execution);
                if (execution.OrderState == OrderStates.Done)
                    orderCompleteEvent.Set();
            }
        };

        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12345,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Market,
            Volume = 0.001m // Small amount for testnet
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(orderMessage);

        var completed = orderCompleteEvent.Wait(TimeSpan.FromSeconds(10));

        // Assert - Market order should execute quickly
        Assert.IsTrue(completed, "Market order did not complete within timeout");
        Assert.IsTrue(orderUpdates.Count >= 2); // At least confirmation + fill

        // Verify order progression: Pending -> Active -> Done
        var confirmation = orderUpdates.FirstOrDefault(u => u.OrderState == OrderStates.Active);
        var completion = orderUpdates.FirstOrDefault(u => u.OrderState == OrderStates.Done);

        Assert.IsNotNull(confirmation, "Missing order confirmation");
        Assert.IsNotNull(completion, "Missing order completion");
        Assert.IsTrue(completion.TradePrice > 0, "Invalid execution price");
        Assert.AreEqual(0.001m, completion.TradeVolume, "Volume mismatch");
    }

    [TestMethod]
    public void OrderLifecycle_LimitOrder_ShouldWorkInMarket()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var orderConfirmedEvent = new ManualResetEventSlim();
        var orderUpdates = new List<ExecutionMessage>();

        adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execution && execution.DataType == DataType.Transactions)
            {
                orderUpdates.Add(execution);
                if (execution.OrderState == OrderStates.Active)
                    orderConfirmedEvent.Set();
            }
        };

        // Place limit order below current market price
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12346,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 1000m // Very low price to avoid execution
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(orderMessage);

        var confirmed = orderConfirmedEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert - Limit order should be confirmed but not filled
        Assert.IsTrue(confirmed, "Limit order not confirmed");

        var confirmation = orderUpdates.First(u => u.OrderState == OrderStates.Active);
        Assert.AreEqual(1000m, confirmation.Price);
        Assert.AreEqual(0.001m, confirmation.Volume);
        Assert.AreEqual(0.001m, confirmation.Balance); // No fills yet
    }

    [TestMethod]
    public void OrderLifecycle_CancelOrder_ShouldUpdateState()
    {
        // Arrange - Place and then cancel order
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var orderCancelledEvent = new ManualResetEventSlim();
        var orderUpdates = new List<ExecutionMessage>();

        adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execution && execution.DataType == DataType.Transactions)
            {
                orderUpdates.Add(execution);
                if (execution.OrderState == OrderStates.Done && execution.IsCancellation)
                    orderCancelledEvent.Set();
            }
        };

        // Place limit order
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12347,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 1000m
        };

        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(orderMessage);
        Thread.Sleep(1000); // Wait for order to be active

        // Cancel the order
        var cancelMessage = new OrderCancelMessage
        {
            TransactionId = 12347,
            OrderId = orderUpdates.First().OrderId
        };

        // Act - Will fail until implementation
        adapter.SendInMessage(cancelMessage);

        var cancelled = orderCancelledEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert
        Assert.IsTrue(cancelled, "Order cancellation not received");

        var cancellation = orderUpdates.First(u => u.IsCancellation);
        Assert.AreEqual(OrderStates.Done, cancellation.OrderState);
        Assert.IsTrue(cancellation.IsCancellation);
    }
}