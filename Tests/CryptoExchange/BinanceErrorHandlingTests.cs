using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class BinanceErrorHandlingTests
{
    [TestMethod]
    public void T045_IsTransientError_Should_ClassifyErrorsCorrectly()
    {
        // Arrange - Test various error codes and their expected classification
        var transientErrorCodes = new[]
        {
            -1003, // Too many requests
            -1015, // Too many orders
            429,   // Rate limit exceeded
            -1000, // Unknown error (network issues)
            -1001, // Disconnected
            -1006, // Unexpected response
            -1007, // Timeout
            500,   // Internal server error
            502,   // Bad gateway
            503,   // Service unavailable
            504,   // Gateway timeout
            -1013  // Invalid quantity (could be due to market conditions)
        };

        var permanentErrorCodes = new[]
        {
            -2014, // API-key format invalid
            -2015, // Invalid API-key, IP, or permissions
            -1102, // Mandatory parameter missing
            -1100, // Illegal characters found
            -1101, // Too many parameters
            -2010, // NEW_ORDER_REJECTED
            -2011, // CANCEL_REJECTED
            -2013, // Order does not exist
            -1121, // Invalid symbol
            -1125  // Timestamp outside recv window
        };

        // Test transient errors
        foreach (var code in transientErrorCodes)
        {
            var error = new Error { Code = code, Message = $"Test error {code}" };

            // Act
            var isTransient = BinanceSpotMessageAdapter.IsTransientError(error);

            // Assert
            Assert.IsTrue(isTransient, $"Error code {code} should be classified as transient");

            Console.WriteLine($"✓ Error {code}: Transient (can retry)");
        }

        // Test permanent errors
        foreach (var code in permanentErrorCodes)
        {
            var error = new Error { Code = code, Message = $"Test error {code}" };

            // Act
            var isTransient = BinanceSpotMessageAdapter.IsTransientError(error);

            // Assert
            Assert.IsFalse(isTransient, $"Error code {code} should be classified as permanent");

            Console.WriteLine($"✓ Error {code}: Permanent (don't retry)");
        }

        // Test null code (network errors)
        var nullCodeError = new Error { Code = null, Message = "Network error" };
        Assert.IsTrue(BinanceSpotMessageAdapter.IsTransientError(nullCodeError),
            "Errors with null code should be considered transient");

        Console.WriteLine($"✓ Null code error: Transient (network issue)");
    }

    [TestMethod]
    public void T045_GetRetryDelay_Should_CalculateAppropriateDelays()
    {
        // Test rate limiting errors - should have longer delays
        var rateLimitError = new Error { Code = -1003, Message = "Too many requests" };

        var delay1 = BinanceSpotMessageAdapter.GetRetryDelay(rateLimitError, 1);
        var delay2 = BinanceSpotMessageAdapter.GetRetryDelay(rateLimitError, 2);
        var delay3 = BinanceSpotMessageAdapter.GetRetryDelay(rateLimitError, 3);

        Assert.IsTrue(delay1.TotalSeconds >= 60, $"Rate limit retry 1 should be at least 60s, got {delay1.TotalSeconds}s");
        Assert.IsTrue(delay2.TotalSeconds > delay1.TotalSeconds, "Rate limit delays should increase");
        Assert.IsTrue(delay3.TotalSeconds > delay2.TotalSeconds, "Rate limit delays should continue increasing");

        Console.WriteLine($"✓ Rate limit delays: {delay1.TotalSeconds}s → {delay2.TotalSeconds}s → {delay3.TotalSeconds}s");

        // Test server errors - should have shorter delays
        var serverError = new Error { Code = 500, Message = "Internal server error" };

        var serverDelay1 = BinanceSpotMessageAdapter.GetRetryDelay(serverError, 1);
        var serverDelay2 = BinanceSpotMessageAdapter.GetRetryDelay(serverError, 2);

        Assert.IsTrue(serverDelay1.TotalSeconds <= 30, $"Server error delay should be reasonable, got {serverDelay1.TotalSeconds}s");
        Assert.IsTrue(serverDelay2.TotalSeconds > serverDelay1.TotalSeconds, "Server error delays should increase");

        Console.WriteLine($"✓ Server error delays: {serverDelay1.TotalSeconds}s → {serverDelay2.TotalSeconds}s");

        // Test network errors (null code)
        var networkError = new Error { Code = null, Message = "Network timeout" };

        var networkDelay1 = BinanceSpotMessageAdapter.GetRetryDelay(networkError, 1);
        var networkDelay2 = BinanceSpotMessageAdapter.GetRetryDelay(networkError, 2);

        Assert.IsTrue(networkDelay1.TotalSeconds <= 30, $"Network error delay should be reasonable, got {networkDelay1.TotalSeconds}s");
        Assert.IsTrue(networkDelay2.TotalSeconds > networkDelay1.TotalSeconds, "Network error delays should increase");

        Console.WriteLine($"✓ Network error delays: {networkDelay1.TotalSeconds}s → {networkDelay2.TotalSeconds}s");

        // Test exponential backoff behavior
        Assert.IsTrue(Math.Abs(networkDelay1.TotalSeconds - 2) < 1, "First network retry should be ~2s (2^1)");
        Assert.IsTrue(Math.Abs(networkDelay2.TotalSeconds - 4) < 1, "Second network retry should be ~4s (2^2)");
    }

    [TestMethod]
    public void T045_HandleBinanceError_Should_CreateCorrectErrorMessages()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        var testCases = new[]
        {
            (new Error { Code = -2014, Message = "API-key format invalid" }, "authentication"),
            (new Error { Code = -1102, Message = "Mandatory parameter missing" }, "parameter"),
            (new Error { Code = -2010, Message = "NEW_ORDER_REJECTED" }, "order"),
            (new Error { Code = -1003, Message = "Too many requests" }, "rate limit"),
            (new Error { Code = -1007, Message = "Timeout" }, "timeout"),
            (new Error { Code = 500, Message = "Internal server error" }, "server")
        };

        foreach (var (error, context) in testCases)
        {
            // Act
            var errorMessage = adapter.HandleBinanceError(error, context);

            // Assert
            Assert.IsNotNull(errorMessage, $"Should create error message for {context}");
            Assert.IsNotNull(errorMessage.Error, $"Should create exception for {context}");
            Assert.IsTrue(errorMessage.Error.Message.Contains(error.Code.ToString()!),
                $"Error message should contain code: {errorMessage.Error.Message}");
            Assert.IsTrue(errorMessage.Error.Message.Contains(context),
                $"Error message should contain context: {errorMessage.Error.Message}");
            Assert.IsTrue(errorMessage.LocalTime > DateTimeOffset.Now.AddMinutes(-1),
                "Error should have recent timestamp");

            Console.WriteLine($"✓ {context} error: {errorMessage.Error.GetType().Name} - {errorMessage.Error.Message}");
        }

        adapter.Dispose();
    }

    [TestMethod]
    public void T045_CreateStockSharpException_Should_MapToCorrectExceptionTypes()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        var exceptionMappings = new[]
        {
            // Authentication errors → UnauthorizedAccessException
            (new Error { Code = -2014, Message = "API-key format invalid" }, typeof(UnauthorizedAccessException)),
            (new Error { Code = -2015, Message = "Invalid API-key" }, typeof(UnauthorizedAccessException)),

            // Invalid request errors → ArgumentException
            (new Error { Code = -1102, Message = "Mandatory parameter missing" }, typeof(ArgumentException)),
            (new Error { Code = -1100, Message = "Illegal characters" }, typeof(ArgumentException)),

            // Order errors → InvalidOperationException
            (new Error { Code = -2010, Message = "NEW_ORDER_REJECTED" }, typeof(InvalidOperationException)),
            (new Error { Code = -2011, Message = "CANCEL_REJECTED" }, typeof(InvalidOperationException)),

            // Rate limiting → InvalidOperationException
            (new Error { Code = -1003, Message = "Too many requests" }, typeof(InvalidOperationException)),
            (new Error { Code = 429, Message = "Rate limited" }, typeof(InvalidOperationException)),

            // Network/timeout errors → TimeoutException
            (new Error { Code = -1007, Message = "Timeout" }, typeof(TimeoutException)),
            (new Error { Code = -1001, Message = "Internal error" }, typeof(TimeoutException)),

            // Server errors → InvalidOperationException
            (new Error { Code = 500, Message = "Internal server error" }, typeof(InvalidOperationException)),
            (new Error { Code = 503, Message = "Service unavailable" }, typeof(InvalidOperationException)),

            // Unknown errors → InvalidOperationException (default)
            (new Error { Code = -9999, Message = "Unknown error" }, typeof(InvalidOperationException))
        };

        foreach (var (error, expectedType) in exceptionMappings)
        {
            // Act
            var errorMessage = adapter.HandleBinanceError(error, "Test Context");

            // Assert
            Assert.IsInstanceOfType(errorMessage.Error, expectedType,
                $"Error {error.Code} should map to {expectedType.Name}, got {errorMessage.Error.GetType().Name}");

            Console.WriteLine($"✓ Error {error.Code} → {errorMessage.Error.GetType().Name}");
        }

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T045_ExecuteWithRetryAsync_Should_RetryTransientErrors()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        int attemptCount = 0;
        var maxRetries = 2;

        // Create a function that fails twice with transient error, then succeeds
        Func<Task<WebCallResult<string>>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10); // Simulate network delay

            if (attemptCount <= 2)
            {
                var error = new Error { Code = -1003, Message = "Too many requests" };
                return new WebCallResult<string>(error);
            }

            return new WebCallResult<string>("Success");
        };

        // Act
        var result = await adapter.ExecuteWithRetryAsync(operation, "Test Operation", maxRetries);

        // Assert
        Assert.AreEqual("Success", result, "Should eventually succeed");
        Assert.AreEqual(3, attemptCount, "Should make 3 attempts (initial + 2 retries)");

        Console.WriteLine($"✓ Retry succeeded after {attemptCount} attempts");

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T045_ExecuteWithRetryAsync_Should_FailPermanentErrors()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        int attemptCount = 0;

        // Create a function that always fails with permanent error
        Func<Task<WebCallResult<string>>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);

            var error = new Error { Code = -2014, Message = "API-key format invalid" };
            return new WebCallResult<string>(error);
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            async () => await adapter.ExecuteWithRetryAsync(operation, "Test Operation", 3));

        Assert.AreEqual(1, attemptCount, "Should not retry permanent errors");

        Console.WriteLine($"✓ Permanent error failed immediately after {attemptCount} attempt");

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T045_ExecuteWithRetryAsync_Should_HandleExceptionsDuringRetry()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        int attemptCount = 0;

        // Create a function that throws exceptions
        Func<Task<WebCallResult<string>>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);

            if (attemptCount <= 2)
            {
                throw new HttpRequestException("Network error");
            }

            return new WebCallResult<string>("Success after exceptions");
        };

        // Act
        var result = await adapter.ExecuteWithRetryAsync(operation, "Exception Test", 3);

        // Assert
        Assert.AreEqual("Success after exceptions", result, "Should succeed after handling exceptions");
        Assert.AreEqual(3, attemptCount, "Should retry exceptions");

        Console.WriteLine($"✓ Exception handling succeeded after {attemptCount} attempts");

        adapter.Dispose();
    }

    [TestMethod]
    public async Task T045_ExecuteWithRetryAsync_Should_RespectMaxRetries()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        int attemptCount = 0;
        var maxRetries = 2;

        // Create a function that always fails with transient error
        Func<Task<WebCallResult<string>>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);

            var error = new Error { Code = -1003, Message = "Too many requests" };
            return new WebCallResult<string>(error);
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await adapter.ExecuteWithRetryAsync(operation, "Max Retries Test", maxRetries));

        Assert.AreEqual(maxRetries + 1, attemptCount, $"Should make {maxRetries + 1} attempts total");

        Console.WriteLine($"✓ Max retries respected: {attemptCount} attempts made");

        adapter.Dispose();
    }

    [TestMethod]
    public void T045_ErrorHandling_Should_LogAppropriately()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        var logMessages = new List<string>();

        // Note: Since we can't easily capture log messages in unit tests,
        // we'll test that the HandleBinanceError method creates appropriate error structures

        var transientError = new Error { Code = -1003, Message = "Too many requests" };
        var permanentError = new Error { Code = -2014, Message = "API-key format invalid" };

        // Act
        var transientResult = adapter.HandleBinanceError(transientError, "Transient Test");
        var permanentResult = adapter.HandleBinanceError(permanentError, "Permanent Test");

        // Assert
        Assert.IsNotNull(transientResult, "Should handle transient error");
        Assert.IsNotNull(permanentResult, "Should handle permanent error");

        // Verify error structures contain appropriate information for logging
        Assert.IsTrue(transientResult.Error.Message.Contains("Transient Test"),
            "Transient error should include context");
        Assert.IsTrue(permanentResult.Error.Message.Contains("Permanent Test"),
            "Permanent error should include context");

        Console.WriteLine($"✓ Transient error structure: {transientResult.Error.Message}");
        Console.WriteLine($"✓ Permanent error structure: {permanentResult.Error.Message}");

        adapter.Dispose();
    }

    [TestMethod]
    public void T045_ErrorHandling_Should_PreserveOriginalErrorInformation()
    {
        // Arrange
        var adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());

        var originalError = new Error
        {
            Code = -2010,
            Message = "Account has insufficient balance for requested action.",
            Data = "Additional error data"
        };

        // Act
        var errorMessage = adapter.HandleBinanceError(originalError, "Balance Check");

        // Assert
        var exceptionMessage = errorMessage.Error.Message;

        Assert.IsTrue(exceptionMessage.Contains("-2010"), "Should preserve error code");
        Assert.IsTrue(exceptionMessage.Contains("insufficient balance"), "Should preserve error message");
        Assert.IsTrue(exceptionMessage.Contains("Balance Check"), "Should include context");

        Console.WriteLine($"✓ Original error preserved: {exceptionMessage}");

        adapter.Dispose();
    }
}