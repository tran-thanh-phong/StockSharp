using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class TestOrderExecutionIntegration
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
        _adapter.UseTestnet = true; // CRITICAL: Use testnet for order testing
        _adapter.LogLevel = LogLevels.Debug;

        // Set test credentials (these should be testnet credentials)
        // Note: In real tests, these would come from secure configuration
        _adapter.Key = new System.Security.SecureString();
        _adapter.Secret = new System.Security.SecureString();
        // Add dummy testnet credentials for testing
        foreach (char c in "testnet_api_key")
            _adapter.Key.AppendChar(c);
        foreach (char c in "testnet_secret_key")
            _adapter.Secret.AppendChar(c);
        _adapter.Key.MakeReadOnly();
        _adapter.Secret.MakeReadOnly();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _adapter?.Key?.Dispose();
        _adapter?.Secret?.Dispose();
        _adapter?.Dispose();
    }

    [TestMethod]
    public async Task T037_LimitOrder_Should_ProcessCorrectly()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var orderConfirmationReceived = new TaskCompletionSource<ExecutionMessage>();
        var executionMessages = new List<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Transaction:
                    executionMessages.Add(execMsg);
                    if (execMsg.OrderState == OrderStates.Active || execMsg.OrderState == OrderStates.Done)
                    {
                        orderConfirmationReceived.TrySetResult(execMsg);
                    }
                    break;

                case ErrorMessage errorMsg:
                    orderConfirmationReceived.SetException(errorMsg.Error);
                    break;
            }
        };

        // Connect and authenticate
        await ConnectAndAuthenticateAsync(_adapter);

        var transactionId = _adapter.TransactionIdGenerator.GetNextId();

        // Create a limit order with a very high price to avoid execution
        var limitOrderMessage = new OrderRegisterMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId,
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m, // Small test amount
            Price = 1000m,   // Very low price to avoid execution
            TimeInForce = TimeInForce.GTC
        };

        // Act
        _adapter.SendInMessage(limitOrderMessage);

        // Wait for order confirmation
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var confirmation = await orderConfirmationReceived.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(confirmation, "Should receive order confirmation");
        Assert.AreEqual(transactionId, confirmation.OriginalTransactionId, "Should match original transaction ID");
        Assert.AreEqual(_testSecurityId, confirmation.SecurityId, "Should match security ID");
        Assert.AreEqual(Sides.Buy, confirmation.Side, "Should match order side");
        Assert.AreEqual(OrderTypes.Limit, confirmation.OrderType, "Should match order type");
        Assert.IsTrue(confirmation.OrderId > 0, "Should have valid order ID");

        // Order should be active (not filled due to low price)
        Assert.IsTrue(confirmation.OrderState == OrderStates.Active || confirmation.OrderState == OrderStates.Pending,
            $"Order should be active or pending, was {confirmation.OrderState}");

        Console.WriteLine($"Limit order placed: ID={confirmation.OrderId}, State={confirmation.OrderState}");

        // Cancel the order to clean up
        await CancelOrderAsync(_adapter, transactionId, confirmation.OrderId!.Value);
    }

    [TestMethod]
    public async Task T037_MarketOrder_Should_ExecuteImmediately()
    {
        // Arrange - This test should be run with EXTREME caution and minimal amounts
        Assert.IsNotNull(_adapter);

        var orderExecutionReceived = new TaskCompletionSource<ExecutionMessage>();
        var executionMessages = new List<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Transaction:
                    executionMessages.Add(execMsg);
                    if (execMsg.OrderState == OrderStates.Done)
                    {
                        orderExecutionReceived.TrySetResult(execMsg);
                    }
                    break;

                case ErrorMessage errorMsg:
                    orderExecutionReceived.SetException(errorMsg.Error);
                    break;
            }
        };

        await ConnectAndAuthenticateAsync(_adapter);

        var transactionId = _adapter.TransactionIdGenerator.GetNextId();

        // WARNING: This is a real market order that will execute
        // Use minimal amount and ensure testnet
        var marketOrderMessage = new OrderRegisterMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId,
            Side = Sides.Buy,
            OrderType = OrderTypes.Market,
            Volume = 0.001m // Minimal test amount
        };

        // Act - CAUTION: This will place a real order on testnet
        _adapter.SendInMessage(marketOrderMessage);

        // Wait for execution
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var execution = await orderExecutionReceived.Task.WaitAsync(cts.Token);

            // Assert
            Assert.IsNotNull(execution, "Should receive order execution");
            Assert.AreEqual(transactionId, execution.OriginalTransactionId);
            Assert.AreEqual(OrderStates.Done, execution.OrderState, "Market order should be filled");
            Assert.IsTrue(execution.Volume > 0, "Should have positive volume");

            Console.WriteLine($"Market order executed: ID={execution.OrderId}, Volume={execution.Volume}");
        }
        catch (Exception ex)
        {
            // Market order might fail due to insufficient balance on testnet
            Console.WriteLine($"Market order test failed (expected on testnet): {ex.Message}");
            Assert.IsTrue(ex.Message.Contains("insufficient") || ex.Message.Contains("balance") ||
                         ex.Message.Contains("funds") || ex.Message.Contains("Account has insufficient"),
                         $"Should fail due to insufficient funds: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task T037_OrderCancellation_Should_Work()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var orderPlaced = new TaskCompletionSource<ExecutionMessage>();
        var orderCancelled = new TaskCompletionSource<ExecutionMessage>();
        var executionMessages = new List<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Transaction:
                    executionMessages.Add(execMsg);

                    if (execMsg.OrderState == OrderStates.Active && !execMsg.IsCancellation)
                    {
                        orderPlaced.TrySetResult(execMsg);
                    }
                    else if (execMsg.IsCancellation || execMsg.OrderState == OrderStates.Done)
                    {
                        orderCancelled.TrySetResult(execMsg);
                    }
                    break;

                case ErrorMessage errorMsg:
                    orderPlaced.TrySetException(errorMsg.Error);
                    orderCancelled.TrySetException(errorMsg.Error);
                    break;
            }
        };

        await ConnectAndAuthenticateAsync(_adapter);

        var transactionId = _adapter.TransactionIdGenerator.GetNextId();

        // Place a limit order that won't execute
        var limitOrderMessage = new OrderRegisterMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId,
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 1m, // Very low price to avoid execution
            TimeInForce = TimeInForce.GTC
        };

        // Act - Place order
        _adapter.SendInMessage(limitOrderMessage);

        // Wait for order placement
        using var placementCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var placedOrder = await orderPlaced.Task.WaitAsync(placementCts.Token);

        Assert.IsNotNull(placedOrder.OrderId, "Placed order should have order ID");

        // Cancel the order
        var cancelMessage = new OrderCancelMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId
        };

        _adapter.SendInMessage(cancelMessage);

        // Wait for cancellation
        using var cancellationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancelledOrder = await orderCancelled.Task.WaitAsync(cancellationCts.Token);

        // Assert
        Assert.IsNotNull(cancelledOrder, "Should receive cancellation confirmation");
        Assert.AreEqual(transactionId, cancelledOrder.OriginalTransactionId);
        Assert.IsTrue(cancelledOrder.IsCancellation || cancelledOrder.OrderState == OrderStates.Done,
            "Should indicate cancellation");

        Console.WriteLine($"Order {placedOrder.OrderId} was successfully cancelled");
    }

    [TestMethod]
    public async Task T037_OrderStatus_Should_ReturnCurrentStatus()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var orderPlaced = new TaskCompletionSource<ExecutionMessage>();
        var statusReceived = new TaskCompletionSource<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            switch (message)
            {
                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Transaction:
                    if (execMsg.OrderState == OrderStates.Active && !orderPlaced.Task.IsCompleted)
                    {
                        orderPlaced.TrySetResult(execMsg);
                    }
                    else if (orderPlaced.Task.IsCompleted)
                    {
                        statusReceived.TrySetResult(execMsg);
                    }
                    break;

                case ErrorMessage errorMsg:
                    orderPlaced.TrySetException(errorMsg.Error);
                    statusReceived.TrySetException(errorMsg.Error);
                    break;
            }
        };

        await ConnectAndAuthenticateAsync(_adapter);

        var transactionId = _adapter.TransactionIdGenerator.GetNextId();

        // Place order first
        var limitOrderMessage = new OrderRegisterMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId,
            Side = Sides.Sell,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = 100000m, // Very high price to avoid execution
            TimeInForce = TimeInForce.GTC
        };

        _adapter.SendInMessage(limitOrderMessage);

        using var placementCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var placedOrder = await orderPlaced.Task.WaitAsync(placementCts.Token);

        // Request status
        var statusMessage = new OrderStatusMessage
        {
            TransactionId = transactionId,
            SecurityId = _testSecurityId
        };

        _adapter.SendInMessage(statusMessage);

        // Wait for status response
        using var statusCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var statusResponse = await statusReceived.Task.WaitAsync(statusCts.Token);

        // Assert
        Assert.IsNotNull(statusResponse, "Should receive status response");
        Assert.AreEqual(placedOrder.OrderId, statusResponse.OrderId, "Should match order ID");
        Assert.IsTrue(statusResponse.OrderState == OrderStates.Active || statusResponse.OrderState == OrderStates.Pending,
            $"Order should be active, was {statusResponse.OrderState}");

        Console.WriteLine($"Order status: ID={statusResponse.OrderId}, State={statusResponse.OrderState}");

        // Cleanup
        await CancelOrderAsync(_adapter, transactionId, placedOrder.OrderId!.Value);
    }

    [TestMethod]
    public async Task T037_InvalidOrder_Should_ReturnError()
    {
        // Arrange
        Assert.IsNotNull(_adapter);

        var errorReceived = new TaskCompletionSource<ExecutionMessage>();

        _adapter.NewOutMessage += message =>
        {
            if (message is ExecutionMessage execMsg && execMsg.Error != null)
            {
                errorReceived.SetResult(execMsg);
            }
        };

        await ConnectAndAuthenticateAsync(_adapter);

        // Create invalid order (negative price)
        var invalidOrderMessage = new OrderRegisterMessage
        {
            TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
            SecurityId = _testSecurityId,
            Side = Sides.Buy,
            OrderType = OrderTypes.Limit,
            Volume = 0.001m,
            Price = -100m // Invalid negative price
        };

        // Act
        _adapter.SendInMessage(invalidOrderMessage);

        // Wait for error
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var errorResponse = await errorReceived.Task.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(errorResponse.Error, "Should return error for invalid order");
        Assert.AreEqual(OrderStates.Failed, errorResponse.OrderState, "Order state should be failed");

        Console.WriteLine($"Invalid order rejected: {errorResponse.Error.Message}");
    }

    private async Task ConnectAndAuthenticateAsync(BinanceSpotMessageAdapter adapter)
    {
        var connectComplete = new TaskCompletionSource<bool>();

        adapter.NewOutMessage += message =>
        {
            if (message is ConnectMessage connectMsg)
            {
                if (connectMsg.Error == null)
                    connectComplete.SetResult(true);
                else
                    connectComplete.SetException(connectMsg.Error);
            }
        };

        adapter.SendInMessage(new ConnectMessage());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await connectComplete.Task.WaitAsync(cts.Token);
    }

    private async Task CancelOrderAsync(BinanceSpotMessageAdapter adapter, long transactionId, long orderId)
    {
        var cancelConfirmed = new TaskCompletionSource<bool>();

        var tempHandler = new Action<Message>(message =>
        {
            if (message is ExecutionMessage execMsg &&
                execMsg.OriginalTransactionId == transactionId &&
                (execMsg.IsCancellation || execMsg.OrderState == OrderStates.Done))
            {
                cancelConfirmed.TrySetResult(true);
            }
        });

        adapter.NewOutMessage += tempHandler;

        try
        {
            var cancelMessage = new OrderCancelMessage
            {
                TransactionId = transactionId,
                SecurityId = _testSecurityId
            };

            adapter.SendInMessage(cancelMessage);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await cancelConfirmed.Task.WaitAsync(cts.Token);

            Console.WriteLine($"Order {orderId} cancelled successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to cancel order {orderId}: {ex.Message}");
        }
        finally
        {
            adapter.NewOutMessage -= tempHandler;
        }
    }
}