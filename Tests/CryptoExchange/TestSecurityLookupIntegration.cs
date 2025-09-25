using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestSecurityLookupIntegration
{
    private BinanceSpotMessageAdapter? _adapter;

    [TestInitialize]
    public void Setup()
    {
        _adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        _adapter.UseTestnet = true; // Use testnet for testing
        _adapter.LogLevel = LogLevels.Debug;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _adapter?.Dispose();
    }

    [TestMethod]
    public async Task T035_SecurityLookupMessage_Should_ReturnBinanceSecurities()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var completionSource = new TaskCompletionSource<SecurityLookupResultMessage>();
        var receivedSecurities = new List<SecurityMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case SecurityMessage securityMessage:
                    receivedSecurities.Add(securityMessage);
                    break;

                case SecurityLookupResultMessage resultMessage:
                    completionSource.SetResult(resultMessage);
                    break;

                case ErrorMessage errorMessage:
                    completionSource.SetException(errorMessage.Error);
                    break;
            }
        };

        var lookupMessage = new SecurityLookupMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityType = SecurityTypes.Stock // Request all crypto pairs
        };

        // Act
        _adapter.SendInMessage(lookupMessage);

        // Wait for completion with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var resultMessage = await completionSource.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNull(resultMessage.Error, $"Security lookup failed: {resultMessage.Error?.Message}");
        Assert.IsTrue(receivedSecurities.Count > 0, "Should receive at least some securities");

        // Validate received securities
        foreach (var security in receivedSecurities.Take(5)) // Check first 5 securities
        {
            Assert.IsNotNull(security.SecurityId.SecurityCode, "Security code should not be null");
            Assert.AreEqual("BINANCE", security.SecurityId.BoardCode, "Board code should be BINANCE");
            Assert.AreEqual(SecurityTypes.Stock, security.SecurityType, "Security type should be Stock (crypto)");
            Assert.IsTrue(security.PriceStep > 0, "Price step should be positive");
            Assert.IsTrue(security.VolumeStep > 0, "Volume step should be positive");
        }

        // Check for common symbols
        var btcusdt = receivedSecurities.FirstOrDefault(s => s.SecurityId.SecurityCode == "BTCUSDT");
        Assert.IsNotNull(btcusdt, "BTCUSDT should be available");
        Assert.AreEqual(CurrencyTypes.USD, btcusdt.Currency, "BTCUSDT currency should be USD");

        Console.WriteLine($"Retrieved {receivedSecurities.Count} securities from Binance");
        Console.WriteLine($"Sample securities: {string.Join(", ", receivedSecurities.Take(10).Select(s => s.SecurityId.SecurityCode))}");
    }

    [TestMethod]
    public async Task T035_SecurityLookup_SpecificSymbol_Should_ReturnOnlyThatSymbol()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var completionSource = new TaskCompletionSource<SecurityLookupResultMessage>();
        var receivedSecurities = new List<SecurityMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case SecurityMessage securityMessage:
                    receivedSecurities.Add(securityMessage);
                    break;

                case SecurityLookupResultMessage resultMessage:
                    completionSource.SetResult(resultMessage);
                    break;

                case ErrorMessage errorMessage:
                    completionSource.SetException(errorMessage.Error);
                    break;
            }
        };

        var targetSecurityId = new SecurityId
        {
            SecurityCode = "BTCUSDT",
            BoardCode = "BINANCE"
        };

        var lookupMessage = new SecurityLookupMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = targetSecurityId
        };

        // Act
        _adapter.SendInMessage(lookupMessage);

        // Wait for completion
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var resultMessage = await completionSource.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNull(resultMessage.Error, $"Security lookup failed: {resultMessage.Error?.Message}");

        // Should receive only BTCUSDT or none if not found
        var btcusdtSecurities = receivedSecurities.Where(s => s.SecurityId.SecurityCode == "BTCUSDT").ToList();

        if (btcusdtSecurities.Count > 0)
        {
            Assert.AreEqual(1, btcusdtSecurities.Count, "Should receive exactly one BTCUSDT security");
            var btcusdt = btcusdtSecurities[0];

            Assert.AreEqual("BTCUSDT", btcusdt.SecurityId.SecurityCode);
            Assert.AreEqual("BINANCE", btcusdt.SecurityId.BoardCode);
            Assert.IsTrue(btcusdt.Name?.Contains("BTC"), "Name should contain BTC");
            Assert.IsTrue(btcusdt.PriceStep > 0, "Price step should be positive");
        }

        Console.WriteLine($"Lookup for BTCUSDT returned {receivedSecurities.Count} securities");
    }

    [TestMethod]
    public async Task T035_SecurityLookup_InvalidConnection_Should_ReturnError()
    {
        // Arrange - Create adapter without proper initialization
        var invalidAdapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        // Don't connect or authenticate

        var completionSource = new TaskCompletionSource<SecurityLookupResultMessage>();

        invalidAdapter.NewOutMessage += message =>
        {
            if (message is SecurityLookupResultMessage resultMessage)
            {
                completionSource.SetResult(resultMessage);
            }
        };

        var lookupMessage = new SecurityLookupMessage
        {
            TransactionId = invalidAdapter.TransactionIdGenerator.GetNextId()
        };

        // Act
        invalidAdapter.SendInMessage(lookupMessage);

        // Wait for result
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var resultMessage = await completionSource.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(resultMessage.Error, "Should return error when not connected");
        Assert.IsTrue(resultMessage.Error.Message.Contains("Not connected"),
            $"Error should indicate connection issue: {resultMessage.Error.Message}");

        invalidAdapter.Dispose();
    }

    [TestMethod]
    public async Task T035_SecurityLookup_Performance_Should_CompleteWithinTimeLimit()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var completionSource = new TaskCompletionSource<SecurityLookupResultMessage>();
        var receivedCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case SecurityMessage:
                    Interlocked.Increment(ref receivedCount);
                    break;

                case SecurityLookupResultMessage resultMessage:
                    stopwatch.Stop();
                    completionSource.SetResult(resultMessage);
                    break;
            }
        };

        var lookupMessage = new SecurityLookupMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId()
        };

        // Act
        _adapter.SendInMessage(lookupMessage);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)); // Allow more time for full lookup
        var resultMessage = await completionSource.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNull(resultMessage.Error, $"Security lookup should succeed: {resultMessage.Error?.Message}");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000,
            $"Security lookup should complete within 30 seconds, took {stopwatch.ElapsedMilliseconds}ms");
        Assert.IsTrue(receivedCount > 100,
            $"Should receive substantial number of securities, got {receivedCount}");

        Console.WriteLine($"Security lookup completed in {stopwatch.ElapsedMilliseconds}ms with {receivedCount} securities");
    }
}