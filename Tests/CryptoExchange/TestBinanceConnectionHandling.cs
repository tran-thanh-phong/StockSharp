using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestBinanceConnectionHandling
{
    [TestMethod]
    public async Task T039_TestnetConnection_WithValidCredentials_Should_Connect()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        adapter.UseTestnet = true;
        adapter.LogLevel = LogLevels.Debug;

        // Set valid testnet credentials (these should be real testnet API keys for testing)
        adapter.Key = new System.Security.SecureString();
        adapter.Secret = new System.Security.SecureString();

        // Note: In real testing environment, these would be loaded from secure config
        var testApiKey = Environment.GetEnvironmentVariable("BINANCE_TESTNET_API_KEY") ?? "testnet_key";
        var testSecret = Environment.GetEnvironmentVariable("BINANCE_TESTNET_SECRET") ?? "testnet_secret";

        foreach (char c in testApiKey)
            adapter.Key.AppendChar(c);
        foreach (char c in testSecret)
            adapter.Secret.AppendChar(c);

        adapter.Key.MakeReadOnly();
        adapter.Secret.MakeReadOnly();

        var connectionComplete = new TaskCompletionSource<ConnectMessage>();

        adapter.NewOutMessage += message =>
        {
            if (message is ConnectMessage connectMsg)
                connectionComplete.SetResult(connectMsg);
        };

        try
        {
            // Act
            adapter.SendInMessage(new ConnectMessage());

            // Wait for connection result
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await connectionComplete.Task.WaitAsync(cts.Token);

            // Assert
            if (testApiKey != "testnet_key" && testSecret != "testnet_secret")
            {
                // Real credentials provided
                Assert.IsNull(result.Error, $"Connection should succeed with valid credentials: {result.Error?.Message}");
                Console.WriteLine("Successfully connected to Binance testnet");
            }
            else
            {
                // Dummy credentials - connection should fail gracefully
                Assert.IsNotNull(result.Error, "Connection should fail with dummy credentials");
                Assert.IsTrue(result.Error.Message.Contains("API") || result.Error.Message.Contains("authentication"),
                    $"Error should indicate authentication issue: {result.Error.Message}");
                Console.WriteLine($"Connection properly failed with dummy credentials: {result.Error.Message}");
            }
        }
        finally
        {
            adapter.Key?.Dispose();
            adapter.Secret?.Dispose();
            adapter.Dispose();
        }
    }

    [TestMethod]
    public async Task T039_TestnetConnection_WithInvalidCredentials_Should_FailGracefully()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        adapter.UseTestnet = true;

        adapter.Key = new System.Security.SecureString();
        adapter.Secret = new System.Security.SecureString();

        // Intentionally invalid credentials
        foreach (char c in "invalid_api_key_12345")
            adapter.Key.AppendChar(c);
        foreach (char c in "invalid_secret_67890")
            adapter.Secret.AppendChar(c);

        adapter.Key.MakeReadOnly();
        adapter.Secret.MakeReadOnly();

        var connectionComplete = new TaskCompletionSource<ConnectMessage>();

        adapter.NewOutMessage += message =>
        {
            if (message is ConnectMessage connectMsg)
                connectionComplete.SetResult(connectMsg);
        };

        try
        {
            // Act
            adapter.SendInMessage(new ConnectMessage());

            // Wait for connection result
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await connectionComplete.Task.WaitAsync(cts.Token);

            // Assert
            Assert.IsNotNull(result.Error, "Connection should fail with invalid credentials");
            Assert.IsTrue(
                result.Error.Message.Contains("API") ||
                result.Error.Message.Contains("authentication") ||
                result.Error.Message.Contains("Invalid") ||
                result.Error.Message.Contains("key"),
                $"Error should indicate authentication problem: {result.Error.Message}");

            Console.WriteLine($"Connection properly failed: {result.Error.Message}");
        }
        finally
        {
            adapter.Key?.Dispose();
            adapter.Secret?.Dispose();
            adapter.Dispose();
        }
    }

    [TestMethod]
    public async Task T040_RateLimit_Should_HandleBackoffStrategies()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        adapter.UseTestnet = true;

        // Test rate limit handling by checking static methods
        var binanceError = new Error { Code = -1003, Message = "Too many requests" };

        // Act & Assert
        Assert.IsTrue(BinanceSpotMessageAdapter.IsTransientError(binanceError),
            "Rate limit error should be identified as transient");

        var delay1 = BinanceSpotMessageAdapter.GetRetryDelay(binanceError, 1);
        var delay2 = BinanceSpotMessageAdapter.GetRetryDelay(binanceError, 2);
        var delay3 = BinanceSpotMessageAdapter.GetRetryDelay(binanceError, 3);

        Assert.IsTrue(delay1.TotalSeconds >= 60, $"First retry delay should be at least 60s, was {delay1.TotalSeconds}s");
        Assert.IsTrue(delay2.TotalSeconds > delay1.TotalSeconds, "Retry delays should increase");
        Assert.IsTrue(delay3.TotalSeconds > delay2.TotalSeconds, "Retry delays should continue increasing");

        Console.WriteLine($"Rate limit backoff strategy:");
        Console.WriteLine($"  Attempt 1: {delay1.TotalSeconds}s delay");
        Console.WriteLine($"  Attempt 2: {delay2.TotalSeconds}s delay");
        Console.WriteLine($"  Attempt 3: {delay3.TotalSeconds}s delay");

        // Test different error types
        var networkError = new Error { Code = -1000, Message = "Unknown error" };
        var serverError = new Error { Code = 500, Message = "Internal server error" };

        Assert.IsTrue(BinanceSpotMessageAdapter.IsTransientError(networkError),
            "Network errors should be transient");
        Assert.IsTrue(BinanceSpotMessageAdapter.IsTransientError(serverError),
            "Server errors should be transient");

        var networkDelay = BinanceSpotMessageAdapter.GetRetryDelay(networkError, 1);
        var serverDelay = BinanceSpotMessageAdapter.GetRetryDelay(serverError, 1);

        Assert.IsTrue(networkDelay.TotalSeconds <= 30, "Network errors should have shorter delays");
        Assert.IsTrue(serverDelay.TotalSeconds <= 30, "Server errors should have shorter delays");

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T041_WebSocketReconnection_Should_HandleConnectionLoss()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        adapter.UseTestnet = true;
        adapter.AutoReconnect = true;
        adapter.ReconnectInterval = TimeSpan.FromSeconds(5);

        var connectionStates = new List<(DateTime Time, ConnectionStates State)>();
        var connectionStateChanges = new TaskCompletionSource<bool>();
        int stateChangeCount = 0;

        adapter.NewOutMessage += message =>
        {
            if (message is BaseConnectionMessage connMsg)
            {
                connectionStates.Add((DateTime.UtcNow, connMsg.ConnectionState));
                stateChangeCount++;

                // Wait for a few state changes to observe reconnection behavior
                if (stateChangeCount >= 3)
                    connectionStateChanges.TrySetResult(true);
            }
        };

        try
        {
            // Act - Connect, then simulate disconnection
            adapter.SendInMessage(new ConnectMessage());
            await Task.Delay(TimeSpan.FromSeconds(2));

            adapter.SendInMessage(new DisconnectMessage());
            await Task.Delay(TimeSpan.FromSeconds(1));

            adapter.SendInMessage(new ConnectMessage()); // Trigger reconnection

            // Wait for state changes
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await connectionStateChanges.Task.WaitAsync(cts.Token);

            // Assert
            Assert.IsTrue(connectionStates.Count >= 2,
                $"Should have recorded multiple connection states, got {connectionStates.Count}");

            var hasConnecting = connectionStates.Any(s => s.State == ConnectionStates.Connecting);
            var hasDisconnected = connectionStates.Any(s => s.State == ConnectionStates.Disconnected);

            Assert.IsTrue(hasConnecting || hasDisconnected,
                "Should observe connection state transitions");

            Console.WriteLine("Connection state transitions:");
            foreach (var (time, state) in connectionStates)
            {
                Console.WriteLine($"  {time:HH:mm:ss.fff}: {state}");
            }

            // Verify reconnection settings are properly configured
            Assert.AreEqual(TimeSpan.FromSeconds(5), adapter.ReconnectInterval,
                "Reconnect interval should be configured");
            Assert.IsTrue(adapter.AutoReconnect, "Auto-reconnect should be enabled");
        }
        finally
        {
            adapter.Dispose();
        }
    }

    [TestMethod]
    public async Task T042_BinanceErrorMapping_Should_ConvertToStockSharpFormat()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        // Test various Binance error codes and their StockSharp mappings
        var testCases = new[]
        {
            // Authentication errors
            (new Error { Code = -2014, Message = "API-key format invalid" }, typeof(UnauthorizedAccessException)),
            (new Error { Code = -2015, Message = "Invalid API-key, IP, or permissions" }, typeof(UnauthorizedAccessException)),

            // Invalid request errors
            (new Error { Code = -1102, Message = "Mandatory parameter missing" }, typeof(ArgumentException)),
            (new Error { Code = -1100, Message = "Illegal characters found" }, typeof(ArgumentException)),

            // Order errors
            (new Error { Code = -2010, Message = "NEW_ORDER_REJECTED" }, typeof(InvalidOperationException)),
            (new Error { Code = -2011, Message = "CANCEL_REJECTED" }, typeof(InvalidOperationException)),

            // Rate limiting
            (new Error { Code = -1003, Message = "Too much request weight" }, typeof(InvalidOperationException)),
            (new Error { Code = 429, Message = "Too Many Requests" }, typeof(InvalidOperationException)),

            // Network/timeout errors
            (new Error { Code = -1007, Message = "Timeout waiting for response" }, typeof(TimeoutException)),
            (new Error { Code = -1001, Message = "Internal error; unable to process your request" }, typeof(TimeoutException)),

            // Server errors
            (new Error { Code = 500, Message = "Internal Server Error" }, typeof(InvalidOperationException)),
            (new Error { Code = 503, Message = "Service Unavailable" }, typeof(InvalidOperationException))
        };

        foreach (var (error, expectedExceptionType) in testCases)
        {
            // Act
            var errorMessage = adapter.HandleBinanceError(error, "Test Context");

            // Assert
            Assert.IsNotNull(errorMessage, $"Should create error message for code {error.Code}");
            Assert.IsNotNull(errorMessage.Error, $"Should create exception for code {error.Code}");
            Assert.IsInstanceOfType(errorMessage.Error, expectedExceptionType,
                $"Error code {error.Code} should map to {expectedExceptionType.Name}, got {errorMessage.Error.GetType().Name}");

            Assert.IsTrue(errorMessage.Error.Message.Contains(error.Code.ToString() ?? ""),
                $"Exception message should contain error code: {errorMessage.Error.Message}");
            Assert.IsTrue(errorMessage.Error.Message.Contains("Test Context"),
                $"Exception message should contain context: {errorMessage.Error.Message}");

            Console.WriteLine($"✓ Error {error.Code} → {errorMessage.Error.GetType().Name}: {errorMessage.Error.Message}");
        }

        // Test transient vs permanent error classification
        var transientErrors = new[] { -1003, -1015, 429, -1000, -1001, -1006, -1007, 500, 502, 503, 504 };
        var permanentErrors = new[] { -2014, -2015, -1102, -1100, -1101, -2010, -2011 };

        foreach (var code in transientErrors)
        {
            var error = new Error { Code = code, Message = "Test error" };
            Assert.IsTrue(BinanceSpotMessageAdapter.IsTransientError(error),
                $"Error code {code} should be classified as transient");
        }

        foreach (var code in permanentErrors)
        {
            var error = new Error { Code = code, Message = "Test error" };
            Assert.IsFalse(BinanceSpotMessageAdapter.IsTransientError(error),
                $"Error code {code} should be classified as permanent");
        }

        Console.WriteLine($"✓ Tested error classification: {transientErrors.Length} transient, {permanentErrors.Length} permanent");

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T042_RetryLogic_Should_HandleTransientErrors()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        int attemptCount = 0;
        var maxRetries = 2;

        // Simulate an operation that fails twice then succeeds
        Func<Task<WebCallResult<string>>> flakyOperation = async () =>
        {
            attemptCount++;
            await Task.Delay(100); // Simulate network delay

            if (attemptCount <= 2)
            {
                // Fail with transient error first two times
                var error = new Error { Code = -1003, Message = "Too many requests" };
                return new WebCallResult<string>(error);
            }

            // Succeed on third attempt
            return new WebCallResult<string>("Success");
        };

        var startTime = DateTime.UtcNow;

        try
        {
            // Act
            var result = await adapter.ExecuteWithRetryAsync(flakyOperation, "Test Operation", maxRetries);

            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.AreEqual("Success", result, "Should eventually succeed after retries");
            Assert.AreEqual(3, attemptCount, "Should make exactly 3 attempts (1 initial + 2 retries)");
            Assert.IsTrue(duration.TotalSeconds >= 2,
                $"Should have delays between retries, took {duration.TotalSeconds}s");

            Console.WriteLine($"✓ Retry logic succeeded after {attemptCount} attempts in {duration.TotalSeconds:F1}s");
        }
        finally
        {
            adapter.Dispose();
        }
    }

    [TestMethod]
    public async Task T042_RetryLogic_Should_FailPermanentErrors()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        int attemptCount = 0;

        // Simulate an operation that always fails with permanent error
        Func<Task<WebCallResult<string>>> permanentFailureOperation = async () =>
        {
            attemptCount++;
            await Task.Delay(50);

            var error = new Error { Code = -2014, Message = "API-key format invalid" };
            return new WebCallResult<string>(error);
        };

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
                async () => await adapter.ExecuteWithRetryAsync(permanentFailureOperation, "Test Operation", 3));

            // Should only attempt once for permanent errors
            Assert.AreEqual(1, attemptCount, "Should not retry permanent errors");
            Assert.IsTrue(exception.Message.Contains("-2014"),
                $"Exception should contain error code: {exception.Message}");

            Console.WriteLine($"✓ Permanent error properly failed immediately: {exception.Message}");
        }
        finally
        {
            adapter.Dispose();
        }
    }
}