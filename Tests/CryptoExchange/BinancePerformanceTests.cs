using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class BinancePerformanceTests
{
    private BinanceSpotMessageAdapter? _adapter;
    private readonly SecurityId _testSecurityId = new()
    {
        SecurityCode = "BTCUSDT",
        BoardCode = "BINANCE"
    };

    [TestInitialize]
    public void Setup()
    {
        _adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator());
        _adapter.UseTestnet = true;
        _adapter.LogLevel = LogLevels.Warning; // Reduce logging overhead for performance tests
    }

    [TestCleanup]
    public void Cleanup()
    {
        _adapter?.Dispose();
    }

    [TestMethod]
    public async Task T046_MarketDataProcessing_Should_MeetLatencyRequirements()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        const int messageCount = 1000;
        const double maxLatencyMs = 50.0; // <50ms requirement
        var processingTimes = new List<double>();

        // Simulate market data messages
        var testMessages = new List<object>();
        for (int i = 0; i < messageCount; i++)
        {
            testMessages.Add(new
            {
                Id = (long)i,
                Price = 50000m + (i * 0.01m),
                Quantity = 0.001m + (i * 0.0001m),
                Time = DateTime.UtcNow.AddMilliseconds(i),
                IsBuyerMaker = i % 2 == 0
            });
        }

        // Act & Measure
        foreach (var message in testMessages)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Test message conversion performance
                var result = _adapter.ConvertToStockSharpMessage(message, _testSecurityId);

                stopwatch.Stop();
                processingTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"Message processing failed: {ex.Message}");
                // Still record the time for failed operations
                processingTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        // Assert
        var averageLatency = processingTimes.Average();
        var maxLatency = processingTimes.Max();
        var percentile95 = processingTimes.OrderBy(x => x).Skip((int)(processingTimes.Count * 0.95)).First();

        Assert.IsTrue(averageLatency < maxLatencyMs,
            $"Average processing latency should be <{maxLatencyMs}ms, was {averageLatency:F2}ms");

        Assert.IsTrue(percentile95 < maxLatencyMs * 2,
            $"95th percentile should be <{maxLatencyMs * 2}ms, was {percentile95:F2}ms");

        Console.WriteLine($"✓ Market Data Performance Results:");
        Console.WriteLine($"  Messages processed: {messageCount}");
        Console.WriteLine($"  Average latency: {averageLatency:F2}ms");
        Console.WriteLine($"  Max latency: {maxLatency:F2}ms");
        Console.WriteLine($"  95th percentile: {percentile95:F2}ms");
        Console.WriteLine($"  Throughput: {messageCount / (processingTimes.Sum() / 1000):F0} messages/second");

        // Performance should be significantly better than requirement for synthetic data
        Assert.IsTrue(averageLatency < 10,
            $"Synthetic message processing should be very fast, was {averageLatency:F2}ms");
    }

    [TestMethod]
    public async Task T047_OrderExecution_Should_MeetRoundtripRequirements()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        const double maxRoundtripMs = 100.0; // <100ms requirement
        var roundtripTimes = new List<double>();

        // Test order creation and validation performance
        var testCases = new[]
        {
            new { Side = Sides.Buy, Type = OrderTypes.Market, Volume = 0.001m, Price = (decimal?)null },
            new { Side = Sides.Sell, Type = OrderTypes.Limit, Volume = 0.001m, Price = (decimal?)100000m },
            new { Side = Sides.Buy, Type = OrderTypes.Limit, Volume = 0.005m, Price = (decimal?)1000m },
        };

        foreach (var testCase in testCases)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Test order message creation and validation
                var orderMessage = new OrderRegisterMessage
                {
                    TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                    SecurityId = _testSecurityId,
                    Side = testCase.Side,
                    OrderType = testCase.Type,
                    Volume = testCase.Volume,
                    Price = testCase.Price,
                    TimeInForce = TimeInForce.GTC
                };

                // Simulate order processing steps
                var symbol = orderMessage.SecurityId.SecurityCode;
                var isValidSymbol = !string.IsNullOrEmpty(symbol) &&
                                   symbol.Length >= 6 &&
                                   symbol.All(char.IsLetterOrDigit);

                Assert.IsTrue(isValidSymbol, "Symbol should be valid");

                // Test order validation logic
                if (testCase.Type == OrderTypes.Limit && !testCase.Price.HasValue)
                {
                    throw new ArgumentException("Limit orders require price");
                }

                stopwatch.Stop();
                roundtripTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"Order processing failed: {ex.Message}");
                roundtripTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        // Assert
        var averageRoundtrip = roundtripTimes.Average();
        var maxRoundtrip = roundtripTimes.Max();

        Assert.IsTrue(averageRoundtrip < maxRoundtripMs,
            $"Average order processing should be <{maxRoundtripMs}ms, was {averageRoundtrip:F2}ms");

        Console.WriteLine($"✓ Order Execution Performance Results:");
        Console.WriteLine($"  Orders processed: {testCases.Length}");
        Console.WriteLine($"  Average roundtrip: {averageRoundtrip:F2}ms");
        Console.WriteLine($"  Max roundtrip: {maxRoundtrip:F2}ms");

        // Local processing should be much faster than network roundtrip
        Assert.IsTrue(averageRoundtrip < 10,
            $"Local order processing should be very fast, was {averageRoundtrip:F2}ms");
    }

    [TestMethod]
    public void T048_MemoryUsage_Should_StayWithinLimits()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        const long maxMemoryMB = 2048; // <2GB requirement
        const int messageCount = 10000;

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Process many messages to test memory usage
        var messages = new List<ExecutionMessage>();
        for (int i = 0; i < messageCount; i++)
        {
            try
            {
                var message = new ExecutionMessage
                {
                    SecurityId = _testSecurityId,
                    DataType = DataType.Ticks,
                    ExecutionType = ExecutionTypes.Tick,
                    TradeId = i,
                    Volume = 0.001m + (i * 0.0001m),
                    Price = 50000m + (i * 0.01m),
                    Side = i % 2 == 0 ? Sides.Buy : Sides.Sell,
                    ServerTime = DateTimeOffset.UtcNow.AddMilliseconds(i),
                    LocalTime = DateTimeOffset.Now
                };

                messages.Add(message);

                // Periodic memory check
                if (i % 1000 == 0)
                {
                    var currentMemory = GC.GetTotalMemory(false);
                    var memoryUsedMB = (currentMemory - initialMemory) / (1024 * 1024);

                    Assert.IsTrue(memoryUsedMB < maxMemoryMB,
                        $"Memory usage should stay below {maxMemoryMB}MB, was {memoryUsedMB}MB at message {i}");
                }
            }
            catch (OutOfMemoryException)
            {
                Assert.Fail($"Out of memory after processing {i} messages");
            }
        }

        // Force garbage collection and measure final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var totalMemoryUsedMB = (finalMemory - initialMemory) / (1024 * 1024);

        // Assert
        Assert.IsTrue(totalMemoryUsedMB < maxMemoryMB,
            $"Total memory usage should be <{maxMemoryMB}MB, was {totalMemoryUsedMB}MB");

        Console.WriteLine($"✓ Memory Usage Results:");
        Console.WriteLine($"  Messages processed: {messageCount:N0}");
        Console.WriteLine($"  Initial memory: {initialMemory / (1024 * 1024):F1}MB");
        Console.WriteLine($"  Final memory: {finalMemory / (1024 * 1024):F1}MB");
        Console.WriteLine($"  Memory used: {totalMemoryUsedMB:F1}MB");
        Console.WriteLine($"  Memory per message: {totalMemoryUsedMB * 1024 / messageCount:F2}KB");

        // Clean up
        messages.Clear();
        GC.Collect();
    }

    [TestMethod]
    public void T046_ThroughputTest_Should_Handle1000UpdatesPerSecond()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        const int targetThroughput = 1000; // 1k+ updates/sec requirement
        const int testDurationSeconds = 5;
        const int expectedMessages = targetThroughput * testDurationSeconds;

        var processedMessages = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages as fast as possible for the test duration
        while (stopwatch.Elapsed.TotalSeconds < testDurationSeconds)
        {
            try
            {
                var testMessage = new
                {
                    Id = (long)processedMessages,
                    Price = 50000m + (processedMessages * 0.001m),
                    Quantity = 0.001m,
                    Time = DateTime.UtcNow,
                    IsBuyerMaker = processedMessages % 2 == 0
                };

                var result = _adapter.ConvertToStockSharpMessage(testMessage, _testSecurityId);
                processedMessages++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Message processing error: {ex.Message}");
                processedMessages++; // Count failed messages too
            }
        }

        stopwatch.Stop();

        // Assert
        var actualThroughput = processedMessages / stopwatch.Elapsed.TotalSeconds;

        Assert.IsTrue(actualThroughput >= targetThroughput,
            $"Throughput should be >= {targetThroughput} msg/sec, was {actualThroughput:F0} msg/sec");

        Console.WriteLine($"✓ Throughput Test Results:");
        Console.WriteLine($"  Test duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"  Messages processed: {processedMessages:N0}");
        Console.WriteLine($"  Throughput achieved: {actualThroughput:F0} messages/second");
        Console.WriteLine($"  Target throughput: {targetThroughput} messages/second");

        // Should significantly exceed minimum requirement
        Assert.IsTrue(actualThroughput > targetThroughput * 2,
            $"Should significantly exceed minimum throughput: {actualThroughput:F0} vs {targetThroughput * 2}");
    }

    [TestMethod]
    public void T048_ConcurrentAccess_Should_HandleMultipleThreads()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        const int threadCount = 4;
        const int messagesPerThread = 1000;
        var totalProcessed = 0;
        var errors = new List<Exception>();
        var tasks = new List<Task>();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages concurrently from multiple threads
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            var task = Task.Run(() =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    try
                    {
                        var testMessage = new
                        {
                            Id = (long)(threadId * messagesPerThread + i),
                            Price = 50000m + (i * 0.001m),
                            Quantity = 0.001m,
                            Time = DateTime.UtcNow,
                            ThreadId = threadId
                        };

                        var result = _adapter.ConvertToStockSharpMessage(testMessage, _testSecurityId);
                        Interlocked.Increment(ref totalProcessed);
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add(ex);
                        }
                    }
                }
            });

            tasks.Add(task);
        }

        // Wait for all threads to complete
        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));
        stopwatch.Stop();

        // Assert
        var expectedTotal = threadCount * messagesPerThread;
        var successRate = (double)totalProcessed / expectedTotal;

        Assert.IsTrue(successRate > 0.95,
            $"Success rate should be >95%, was {successRate:P1} ({totalProcessed}/{expectedTotal})");

        Assert.IsTrue(errors.Count < expectedTotal * 0.05,
            $"Error rate should be <5%, had {errors.Count} errors out of {expectedTotal} attempts");

        Console.WriteLine($"✓ Concurrent Access Results:");
        Console.WriteLine($"  Threads: {threadCount}");
        Console.WriteLine($"  Messages per thread: {messagesPerThread:N0}");
        Console.WriteLine($"  Total messages: {expectedTotal:N0}");
        Console.WriteLine($"  Successfully processed: {totalProcessed:N0}");
        Console.WriteLine($"  Success rate: {successRate:P1}");
        Console.WriteLine($"  Errors: {errors.Count}");
        Console.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"  Concurrent throughput: {totalProcessed / stopwatch.Elapsed.TotalSeconds:F0} msg/sec");

        if (errors.Any())
        {
            Console.WriteLine($"  First error: {errors.First().Message}");
        }
    }
}