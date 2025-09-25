using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestAccountUpdates
{
    [TestMethod]
    public void BinanceSpot_ShouldReceive_AccountBalances()
    {
        // Arrange - This test MUST fail until implementation
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var balanceUpdates = new List<PortfolioChangeMessage>();
        var balanceReceivedEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is PortfolioChangeMessage portfolio)
            {
                balanceUpdates.Add(portfolio);
                balanceReceivedEvent.Set();
            }
        };

        var portfolioMessage = new PortfolioMessage
        {
            PortfolioName = "Binance",
            IsSubscribe = true
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(portfolioMessage);

        var balancesReceived = balanceReceivedEvent.Wait(TimeSpan.FromSeconds(10));

        // Assert - Should receive account balances
        Assert.IsTrue(balancesReceived, "No balance updates received");
        Assert.IsTrue(balanceUpdates.Count > 0);

        // Verify testnet has some balances (usually fake USDT, BTC)
        var usdtBalance = balanceUpdates.FirstOrDefault(b => b.ClientCode == "USDT");
        Assert.IsNotNull(usdtBalance, "Missing USDT balance in testnet");
        Assert.IsTrue(usdtBalance.CurrentValue >= 0, "Invalid USDT balance");
    }

    [TestMethod]
    public void BinanceSpot_ShouldUpdate_BalancesAfterTrade()
    {
        // Arrange - Monitor balance changes during trading
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var initialBalances = new Dictionary<string, decimal>();
        var finalBalances = new Dictionary<string, decimal>();
        var tradeCompleteEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is PortfolioChangeMessage portfolio)
            {
                finalBalances[portfolio.ClientCode] = portfolio.CurrentValue;
            }
            else if (message is ExecutionMessage execution &&
                     execution.DataType == DataType.Transactions &&
                     execution.OrderState == OrderStates.Done)
            {
                tradeCompleteEvent.Set();
            }
        };

        // Get initial balances
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(new PortfolioMessage { PortfolioName = "Binance", IsSubscribe = true });
        Thread.Sleep(2000); // Wait for initial balances

        foreach (var balance in finalBalances)
            initialBalances[balance.Key] = balance.Value;

        // Execute a small trade
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12348,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Market,
            Volume = 0.001m
        };

        // Act - Will fail until implementation
        adapter.SendInMessage(orderMessage);

        var tradeCompleted = tradeCompleteEvent.Wait(TimeSpan.FromSeconds(10));

        // Assert - Balances should update after trade
        Assert.IsTrue(tradeCompleted, "Trade did not complete");

        // USDT balance should decrease (bought BTC)
        var initialUsdt = initialBalances.GetValueOrDefault("USDT", 0);
        var finalUsdt = finalBalances.GetValueOrDefault("USDT", 0);
        Assert.IsTrue(finalUsdt < initialUsdt, "USDT balance did not decrease after buy order");

        // BTC balance should increase
        var initialBtc = initialBalances.GetValueOrDefault("BTC", 0);
        var finalBtc = finalBalances.GetValueOrDefault("BTC", 0);
        Assert.IsTrue(finalBtc > initialBtc, "BTC balance did not increase after buy order");
    }

    [TestMethod]
    public void BinanceSpot_ShouldTrack_LockedBalances()
    {
        // Arrange - Place order that locks funds
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;

        var balanceUpdates = new List<PortfolioChangeMessage>();
        var orderActiveEvent = new ManualResetEventSlim();

        adapter.NewOutMessage += message =>
        {
            if (message is PortfolioChangeMessage portfolio)
            {
                balanceUpdates.Add(portfolio);
            }
            else if (message is ExecutionMessage execution && execution.OrderState == OrderStates.Active)
            {
                orderActiveEvent.Set();
            }
        };

        // Place limit order that won't execute immediately (locks funds)
        var orderMessage = new OrderRegisterMessage
        {
            TransactionId = 12349,
            SecurityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" },
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 1000m // Very low price
        };

        // Act - Will fail until implementation
        adapter.ConnectAsync().Wait();
        adapter.SendInMessage(new PortfolioMessage { PortfolioName = "Binance", IsSubscribe = true });
        Thread.Sleep(1000); // Get initial balances

        adapter.SendInMessage(orderMessage);
        var orderActive = orderActiveEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert - Should show locked balance
        Assert.IsTrue(orderActive, "Order not confirmed as active");

        var usdtUpdate = balanceUpdates.LastOrDefault(b => b.ClientCode == "USDT");
        Assert.IsNotNull(usdtUpdate, "No USDT balance update");
        Assert.IsTrue(usdtUpdate.BlockedValue > 0, "No locked balance shown for active order");

        // Locked amount should be approximately order value (price * volume)
        var expectedLocked = 1000m * 0.001m; // 1 USDT
        Assert.IsTrue(Math.Abs(usdtUpdate.BlockedValue - expectedLocked) < 0.1m, "Incorrect locked amount");
    }
}