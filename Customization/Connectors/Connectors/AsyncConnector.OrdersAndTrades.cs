using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

public partial class AsyncConnector
{
    /// <summary>
    /// Asynchronously retrieves orders matching the specified criteria.
    /// </summary>
    /// <param name="request">The order lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of orders matching the criteria.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<Order>> GetOrdersAsync(
        GetOrdersRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var message = new OrderStatusMessage
        {
            TransactionId = transactionId,
            IsSubscribe = true
        };

        // Apply filters
        if (request.Security != null)
            message.SecurityId = request.Security.ToSecurityId();
        if (request.Portfolio != null)
            message.PortfolioName = request.Portfolio.Name;
        if (request.From.HasValue)
            message.From = request.From.Value;
        if (request.To.HasValue)
            message.To = request.To.Value;
        if (request.States != null && request.States.Length > 0)
            message.States = request.States;

        this.AddDebugLog("Starting order lookup, TransactionId={0}", transactionId);

        var orders = new List<Order>();
        var taskSource = new TaskCompletionSource<List<Order>>();
        _orderLookupTaskSource.TryAdd(transactionId, taskSource);

        void orderHandler(Subscription s, Order o)
        {
            if (s.TransactionId == transactionId)
            {
                orders.Add(o);
                this.AddDebugLog("Order received for lookup TransactionId={0}: {1}", transactionId, o);
            }
        }

        void subscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId == transactionId)
            {
                if (_orderLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    if (ex == null)
                    {
                        this.AddDebugLog("Order lookup completed, TransactionId={0}, found {1} orders", transactionId, orders.Count);
                        tcs.TrySetResult(orders);
                    }
                    else
                    {
                        this.AddErrorLog("Order lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                        tcs.TrySetException(ex);
                    }
                }
            }
        }

        void subscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if (s.TransactionId == transactionId && isSubscribe)
            {
                if (_orderLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddErrorLog("Order lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
                    tcs.TrySetException(ex);
                }
            }
        }

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_orderLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            OrderReceived += orderHandler;
            SubscriptionStopped += subscriptionStoppedHandler;
            SubscriptionFailed += subscriptionFailedHandler;

            try
            {
                var subscription = new Subscription(message);
                Subscribe(subscription);

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
                    $"Order lookup timed out after {timeout.Value.TotalSeconds} seconds");
                if (_orderLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                OrderReceived -= orderHandler;
                SubscriptionStopped -= subscriptionStoppedHandler;
                SubscriptionFailed -= subscriptionFailedHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Order lookup cancelled, TransactionId={0}", transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Order lookup failed, TransactionId={0}: {1}", transactionId, ex);
            throw;
        }
        finally
        {
            _orderLookupTaskSource.TryRemove(transactionId, out var _);
        }
    }

    /// <summary>
    /// Asynchronously retrieves own trades matching the specified criteria.
    /// </summary>
    /// <param name="request">The trade lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of trades matching the criteria.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<MyTrade>> GetTradesAsync(
        GetTradesRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var message = new OrderStatusMessage
        {
            TransactionId = transactionId,
            IsSubscribe = true
        };

        // Apply filters
        if (request.Security != null)
            message.SecurityId = request.Security.ToSecurityId();
        if (request.Portfolio != null)
            message.PortfolioName = request.Portfolio.Name;
        if (request.From.HasValue)
            message.From = request.From.Value;
        if (request.To.HasValue)
            message.To = request.To.Value;

        this.AddDebugLog("Starting trade lookup, TransactionId={0}", transactionId);

        var trades = new List<MyTrade>();
        var taskSource = new TaskCompletionSource<List<MyTrade>>();
        _tradeLookupTaskSource.TryAdd(transactionId, taskSource);

        void tradeHandler(Subscription s, MyTrade t)
        {
            if (s.TransactionId == transactionId)
            {
                trades.Add(t);
                this.AddDebugLog("Trade received for lookup TransactionId={0}: {1}", transactionId, t);
            }
        }

        void subscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId == transactionId)
            {
                if (_tradeLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    if (ex == null)
                    {
                        this.AddDebugLog("Trade lookup completed, TransactionId={0}, found {1} trades", transactionId, trades.Count);
                        tcs.TrySetResult(trades);
                    }
                    else
                    {
                        this.AddErrorLog("Trade lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                        tcs.TrySetException(ex);
                    }
                }
            }
        }

        void subscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if (s.TransactionId == transactionId && isSubscribe)
            {
                if (_tradeLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddErrorLog("Trade lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
                    tcs.TrySetException(ex);
                }
            }
        }

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_tradeLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            OwnTradeReceived += tradeHandler;
            SubscriptionStopped += subscriptionStoppedHandler;
            SubscriptionFailed += subscriptionFailedHandler;

            try
            {
                var subscription = new Subscription(message);
                Subscribe(subscription);

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
                    $"Trade lookup timed out after {timeout.Value.TotalSeconds} seconds");
                if (_tradeLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                OwnTradeReceived -= tradeHandler;
                SubscriptionStopped -= subscriptionStoppedHandler;
                SubscriptionFailed -= subscriptionFailedHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Trade lookup cancelled, TransactionId={0}", transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Trade lookup failed, TransactionId={0}: {1}", transactionId, ex);
            throw;
        }
        finally
        {
            _tradeLookupTaskSource.TryRemove(transactionId, out var _);
        }
    }
}
