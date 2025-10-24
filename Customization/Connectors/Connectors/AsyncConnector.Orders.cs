using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

public partial class AsyncConnector
{
    private void OnOrderReceived(Subscription subscription, Order order)
    {
        // Handle order registration completion
        if (order.TransactionId != 0 && _orderRegistrationTaskSource.TryGetValue(order.TransactionId, out var regTaskSource))
        {
            // Check if order reached a final or active state
            if (order.State == OrderStates.Active || order.State == OrderStates.Done || order.State == OrderStates.Failed)
            {
                if (_orderRegistrationTaskSource.TryRemove(order.TransactionId, out var tcs))
                {
                    this.AddDebugLog("Order registration completed for TransactionId={0}, State={1}", order.TransactionId, order.State);
                    tcs.TrySetResult(order);
                }
            }
        }

        // Handle order cancellation completion
        foreach (var kvp in _orderCancellationTaskSource)
        {
            var cancelTransactionId = kvp.Key;
            var taskSource = kvp.Value;

            // Check if this order matches a pending cancellation
            if (order.State == OrderStates.Done && order.Id != 0)
            {
                // We need to track the original order being cancelled
                // The cancellation transaction ID is stored separately
                // For now, we'll match by order state transition to Done
                // A more robust implementation would track order IDs
            }
        }
    }

    private void OnOrderRegisterFailReceived(Subscription subscription, OrderFail fail)
    {
        if (fail.Order?.TransactionId > 0 && _orderRegistrationTaskSource.TryRemove(fail.Order.TransactionId, out var taskSource))
        {
            var exception = new InvalidOperationException($"Order registration failed: {fail.Error}");
            this.AddErrorLog("Order registration failed for TransactionId={0}: {1}", fail.Order.TransactionId, fail.Error);
            taskSource.TrySetException(exception);
        }
    }

    private void OnOwnTradeReceived(Subscription subscription, MyTrade trade)
    {
        // Collect trades for lookup operations
        foreach (var kvp in _tradeLookupTaskSource.ToArray())
        {
            // Trades are collected via subscription completion, handled elsewhere
        }
    }

    /// <summary>
    /// Asynchronously registers an order in the trading system.
    /// </summary>
    /// <param name="request">The order registration request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the registration operation. Defaults to 30 seconds.</param>
    /// <returns>The registered order with updated state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request or order is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when order registration fails.</exception>
    public async Task<Order> RegisterOrderAsync(
        RegisterOrderRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Order, nameof(request.Order));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();
        request.Order.TransactionId = transactionId;

        this.AddDebugLog("Starting order registration: {0} {1} {2}@{3}, TransactionId={4}",
            request.Order.Side, request.Order.Security?.Code, request.Order.Volume, request.Order.Price, transactionId);

        var taskSource = new TaskCompletionSource<Order>();
        _orderRegistrationTaskSource.TryAdd(transactionId, taskSource);

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_orderRegistrationTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            RegisterOrder(request.Order);

            // Wait for result with timeout
            var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

            if (completedTask == taskSource.Task)
            {
                // Cancel the timeout delay task
                cts.Cancel();
                return await taskSource.Task;
            }

            // Timeout occurred
            var timeoutException = new TimeoutException(
                $"Order registration timed out after {timeout.Value.TotalSeconds} seconds for order {request.Order}");
            if (_orderRegistrationTaskSource.TryRemove(transactionId, out var tcs))
            {
                tcs.TrySetException(timeoutException);
            }

            throw timeoutException;
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Order registration cancelled, TransactionId={0}", transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Order registration failed, TransactionId={0}: {1}", transactionId, ex);
            throw;
        }
        finally
        {
            _orderRegistrationTaskSource.TryRemove(transactionId, out var _);
        }
    }

    /// <summary>
    /// Asynchronously cancels an order.
    /// </summary>
    /// <param name="request">The order cancellation request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the cancellation operation. Defaults to 30 seconds.</param>
    /// <returns>The cancelled order with updated state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request or order is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<Order> CancelOrderAsync(
        CancelOrderRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Order, nameof(request.Order));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var orderId = request.Order.Id ?? throw new InvalidOperationException("Order.Id is required for cancellation");

        this.AddDebugLog("Starting order cancellation for Order {0}, TransactionId={1}",
            orderId, transactionId);

        var taskSource = new TaskCompletionSource<Order>();
        _orderCancellationTaskSource.TryAdd(orderId, taskSource);

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_orderCancellationTaskSource.TryRemove(orderId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            // Track order state changes
            void orderStateHandler(Subscription s, Order o)
            {
                if (o.Id == orderId && o.State == OrderStates.Done)
                {
                    if (_orderCancellationTaskSource.TryRemove(orderId, out var tcs))
                    {
                        this.AddDebugLog("Order cancelled successfully, OrderId={0}", o.Id);
                        tcs.TrySetResult(o);
                    }
                }
            }

            OrderReceived += orderStateHandler;

            try
            {
                CancelOrder(request.Order);

                // Wait for result with timeout
                var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

                if (completedTask == taskSource.Task)
                {
                    // Cancel the timeout delay task
                    cts.Cancel();
                    return await taskSource.Task;
                }

                // Timeout occurred
                var timeoutException = new TimeoutException(
                    $"Order cancellation timed out after {timeout.Value.TotalSeconds} seconds for order {orderId}");
                if (_orderCancellationTaskSource.TryRemove(orderId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                OrderReceived -= orderStateHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Order cancellation cancelled for OrderId={0}", orderId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Order cancellation failed for OrderId={0}: {1}", orderId, ex);
            throw;
        }
        finally
        {
            _orderCancellationTaskSource.TryRemove(orderId, out var _);
        }
    }
}
