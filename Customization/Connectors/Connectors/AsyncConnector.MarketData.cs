using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

public partial class AsyncConnector
{
    /// <summary>
    /// Asynchronously retrieves a single market depth snapshot.
    /// </summary>
    /// <param name="request">The market depth request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
    /// <returns>A market depth snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<IOrderBookMessage> GetMarketDepthAsync(
        GetMarketDepthRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Security, nameof(request.Security));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();

        this.AddDebugLog("Starting market depth request for {0}, TransactionId={1}", request.Security.Code, transactionId);

        var taskSource = new TaskCompletionSource<IOrderBookMessage>();
        _marketDepthTaskSource.TryAdd(transactionId, taskSource);

        void depthHandler(Subscription s, IOrderBookMessage depth)
        {
            if (s.TransactionId == transactionId)
            {
                if (_marketDepthTaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddDebugLog("Market depth received, TransactionId={0}", transactionId);
                    tcs.TrySetResult(depth);
                }
            }
        }

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_marketDepthTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            OrderBookReceived += depthHandler;

            try
            {
                var subscription = new Subscription(DataType.MarketDepth, request.Security)
                {
                    TransactionId = transactionId
                };

                // TODO: Fix MaxDepth support - Subscription doesn't support 'with' syntax or DepthBuilder
                // if (request.MaxDepth.HasValue)
                // {
                //     subscription = subscription with
                //     {
                //         DepthBuilder = new MarketDepthBuilder(request.Security.ToSecurityId())
                //         {
                //             MaxDepth = request.MaxDepth.Value
                //         }
                //     };
                // }

                Subscribe(subscription);

                // Wait for result with timeout
                var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

                if (completedTask == taskSource.Task)
                {
                    // Cancel the timeout delay task and unsubscribe
                    cts.Cancel();
                    UnSubscribe(subscription);
                    return await taskSource.Task;
                }

                // Timeout occurred
                UnSubscribe(subscription);
                var timeoutException = new TimeoutException(
                    $"Market depth request timed out after {timeout.Value.TotalSeconds} seconds for {request.Security.Code}");
                if (_marketDepthTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                OrderBookReceived -= depthHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Market depth request cancelled for {0}, TransactionId={1}", request.Security.Code, transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Market depth request failed for {0}, TransactionId={1}: {2}", request.Security.Code, transactionId, ex);
            throw;
        }
        finally
        {
            _marketDepthTaskSource.TryRemove(transactionId, out var _);
        }
    }

    /// <summary>
    /// Asynchronously retrieves historical tick trades.
    /// </summary>
    /// <param name="request">The ticks lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of tick trades.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<ITickTradeMessage>> GetTicksAsync(
        GetTicksRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Security, nameof(request.Security));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();

        this.AddDebugLog("Starting ticks lookup for {0}, TransactionId={1}", request.Security.Code, transactionId);

        var ticks = new List<ITickTradeMessage>();
        var taskSource = new TaskCompletionSource<List<ITickTradeMessage>>();
        _ticksTaskSource.TryAdd(transactionId, taskSource);

        void tickHandler(Subscription s, ITickTradeMessage tick)
        {
            if (s.TransactionId == transactionId)
            {
                ticks.Add(tick);
                // TODO: Fix property names - find correct ITickTradeMessage properties for price/volume
                // this.AddDebugLog("Tick received for lookup TransactionId={0}: Price={1}, Volume={2}", transactionId, tick.TradePrice, tick.TradeVolume);
                this.AddDebugLog("Tick received for lookup TransactionId={0}", transactionId);
            }
        }

        void subscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId == transactionId)
            {
                if (_ticksTaskSource.TryRemove(transactionId, out var tcs))
                {
                    if (ex == null)
                    {
                        this.AddDebugLog("Ticks lookup completed, TransactionId={0}, found {1} ticks", transactionId, ticks.Count);
                        tcs.TrySetResult(ticks);
                    }
                    else
                    {
                        this.AddErrorLog("Ticks lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                        tcs.TrySetException(ex);
                    }
                }
            }
        }

        void subscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if (s.TransactionId == transactionId && isSubscribe)
            {
                if (_ticksTaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddErrorLog("Ticks lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
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
                if (_ticksTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            TickTradeReceived += tickHandler;
            SubscriptionStopped += subscriptionStoppedHandler;
            SubscriptionFailed += subscriptionFailedHandler;

            try
            {
                var subscription = new Subscription(DataType.Ticks, request.Security)
                {
                    TransactionId = transactionId
                };

                // Apply date filters if present
                if (request.From.HasValue || request.To.HasValue || request.Skip.HasValue || request.Count.HasValue)
                {
                    var mdMsg = new MarketDataMessage
                    {
                        TransactionId = transactionId,
                        SecurityId = request.Security.ToSecurityId(),
                        DataType2 = DataType.Ticks,
                        IsSubscribe = true,
                        From = request.From,
                        To = request.To,
                        Skip = request.Skip,
                        Count = request.Count
                    };
                    subscription = new Subscription(mdMsg);
                }

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
                    $"Ticks lookup timed out after {timeout.Value.TotalSeconds} seconds for {request.Security.Code}");
                if (_ticksTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                TickTradeReceived -= tickHandler;
                SubscriptionStopped -= subscriptionStoppedHandler;
                SubscriptionFailed -= subscriptionFailedHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Ticks lookup cancelled for {0}, TransactionId={1}", request.Security.Code, transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Ticks lookup failed for {0}, TransactionId={1}: {1}", request.Security.Code, transactionId, ex);
            throw;
        }
        finally
        {
            _ticksTaskSource.TryRemove(transactionId, out var _);
        }
    }

    /// <summary>
    /// Asynchronously retrieves Level1 market data.
    /// </summary>
    /// <param name="request">The Level1 lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of Level1 changes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<Level1ChangeMessage>> GetLevel1Async(
        GetLevel1Request request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Security, nameof(request.Security));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();

        this.AddDebugLog("Starting Level1 lookup for {0}, TransactionId={1}", request.Security.Code, transactionId);

        var level1Data = new List<Level1ChangeMessage>();
        var taskSource = new TaskCompletionSource<List<Level1ChangeMessage>>();
        _level1TaskSource.TryAdd(transactionId, taskSource);

        void level1Handler(Subscription s, Level1ChangeMessage msg)
        {
            if (s.TransactionId == transactionId)
            {
                level1Data.Add(msg);
                this.AddDebugLog("Level1 received for lookup TransactionId={0}", transactionId);
            }
        }

        void subscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId == transactionId)
            {
                if (_level1TaskSource.TryRemove(transactionId, out var tcs))
                {
                    if (ex == null)
                    {
                        this.AddDebugLog("Level1 lookup completed, TransactionId={0}, received {1} messages", transactionId, level1Data.Count);
                        tcs.TrySetResult(level1Data);
                    }
                    else
                    {
                        this.AddErrorLog("Level1 lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                        tcs.TrySetException(ex);
                    }
                }
            }
        }

        void subscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if (s.TransactionId == transactionId && isSubscribe)
            {
                if (_level1TaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddErrorLog("Level1 lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
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
                if (_level1TaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            Level1Received += level1Handler;
            SubscriptionStopped += subscriptionStoppedHandler;
            SubscriptionFailed += subscriptionFailedHandler;

            try
            {
                var subscription = new Subscription(DataType.Level1, request.Security)
                {
                    TransactionId = transactionId
                };

                // Apply date filters if present
                if (request.From.HasValue || request.To.HasValue)
                {
                    var mdMsg = new MarketDataMessage
                    {
                        TransactionId = transactionId,
                        SecurityId = request.Security.ToSecurityId(),
                        DataType2 = DataType.Level1,
                        IsSubscribe = true,
                        From = request.From,
                        To = request.To
                    };
                    subscription = new Subscription(mdMsg);
                }

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
                    $"Level1 lookup timed out after {timeout.Value.TotalSeconds} seconds for {request.Security.Code}");
                if (_level1TaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                Level1Received -= level1Handler;
                SubscriptionStopped -= subscriptionStoppedHandler;
                SubscriptionFailed -= subscriptionFailedHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Level1 lookup cancelled for {0}, TransactionId={1}", request.Security.Code, transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Level1 lookup failed for {0}, TransactionId={1}: {2}", request.Security.Code, transactionId, ex);
            throw;
        }
        finally
        {
            _level1TaskSource.TryRemove(transactionId, out var _);
        }
    }
}
