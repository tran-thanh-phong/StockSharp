using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestBinanceSpotConnection
{
    [TestMethod]
    public void BinanceSpotAdapter_ShouldConnectToTestnet()
    {
        // Arrange - This test MUST fail until BinanceSpotMessageAdapter exists
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;
        adapter.Key = "test-api-key";
        adapter.Secret = "test-api-secret";

        // Act - Will fail until implementation
        var connectTask = adapter.ConnectAsync();
        var result = connectTask.Wait(TimeSpan.FromSeconds(10));

        // Assert - Should establish connection to testnet
        Assert.IsTrue(result, "Connection timeout");
        Assert.AreEqual(ConnectionStates.Connected, adapter.ConnectionState);
    }

    [TestMethod]
    public void BinanceSpotAdapter_ShouldAuthenticate_WithValidCredentials()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;
        adapter.Key = Environment.GetEnvironmentVariable("BINANCE_TESTNET_API_KEY") ?? "test-key";
        adapter.Secret = Environment.GetEnvironmentVariable("BINANCE_TESTNET_API_SECRET") ?? "test-secret";

        // Act - Will fail until implementation
        var authResult = adapter.TestAuthentication();

        // Assert - Should authenticate successfully
        Assert.IsTrue(authResult.IsSuccess);
        Assert.IsNull(authResult.Error);
    }

    [TestMethod]
    public void BinanceSpotAdapter_ShouldHandleInvalidCredentials()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;
        adapter.Key = "invalid-key";
        adapter.Secret = "invalid-secret";

        // Act & Assert - Will fail until implementation
        Assert.ThrowsException<UnauthorizedAccessException>(() => adapter.ConnectAsync().Wait());
    }

    [TestMethod]
    public void BinanceSpotAdapter_ShouldReconnect_AfterConnectionLoss()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(); // Should not compile
        adapter.UseTestnet = true;
        adapter.AutoReconnect = true;
        adapter.ReconnectInterval = TimeSpan.FromSeconds(1);

        var reconnectedEvent = new ManualResetEventSlim();
        adapter.ConnectionStateChanged += state =>
        {
            if (state == ConnectionStates.Connected)
                reconnectedEvent.Set();
        };

        // Act - Connect, simulate disconnect, wait for reconnection
        adapter.ConnectAsync().Wait();
        adapter.SimulateDisconnect(); // Test method
        var reconnected = reconnectedEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert
        Assert.IsTrue(reconnected, "Auto-reconnection failed");
        Assert.AreEqual(ConnectionStates.Connected, adapter.ConnectionState);
    }
}