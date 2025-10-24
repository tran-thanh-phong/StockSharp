using Ecng.Logging;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

public partial class AsyncConnector
{
    /// <summary>
    /// Asynchronously retrieves portfolios from the broker.
    /// Returns cached portfolios if already loaded, otherwise subscribes and waits for data.
    /// </summary>
    /// <param name="request">The portfolio lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of portfolios matching the criteria.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<Portfolio>> GetPortfoliosAsync(
        GetPortfoliosRequest? request = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        request ??= new GetPortfoliosRequest();
        timeout ??= TimeSpan.FromSeconds(30);

        // Fast path: Return from cache if portfolios already loaded
        if (this.Portfolios.Any())
        {
            this.AddDebugLog("Returning portfolios from cache, count={0}", this.Portfolios.Count());
            return ApplyFilters(this.Portfolios, request);
        }

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var message = new PortfolioLookupMessage
        {
            TransactionId = transactionId,
            IsSubscribe = true
        };

        // Apply filters to message
        if (!string.IsNullOrEmpty(request.PortfolioName))
            message.PortfolioName = request.PortfolioName;
        if (request.From.HasValue)
            message.From = request.From.Value;
        if (request.To.HasValue)
            message.To = request.To.Value;

        this.AddDebugLog("Starting portfolio lookup, TransactionId={0}", transactionId);

        var taskSource = new TaskCompletionSource<List<Portfolio>>();
        _portfolioLookupTaskSource.TryAdd(transactionId, taskSource);

        void SubscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId != transactionId && s.OriginalTransactionId != transactionId)
                return;

            if (!_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                return;

            if (ex == null)
            {
                var result = ApplyFilters(this.Portfolios, request);
                this.AddDebugLog("Portfolio lookup completed, TransactionId={0}, found {1} portfolio(s)", transactionId, result.Count);
                tcs.TrySetResult(result);
            }
            else
            {
                this.AddErrorLog("Portfolio lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                tcs.TrySetException(ex);
            }
        }

        void SubscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if ((s.TransactionId != transactionId && s.OriginalTransactionId != transactionId) || !isSubscribe)
                return;

            if (!_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
            {
                return;
            }
            
            this.AddErrorLog("Portfolio lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
            tcs.TrySetException(ex);
        }

        void SubscriptionOnlineHandler(Subscription subscription)
        {
            if (subscription.TransactionId != transactionId)
                return;

            // Subscription is online - portfolios loaded into Connector cache
            if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
            {
                var result = ApplyFilters(this.Portfolios, request);
                this.AddDebugLog("Portfolio lookup subscription online, TransactionId={0}, completing with {1} portfolio(s)",
                    transactionId, result.Count);
                tcs.TrySetResult(result);
            }

            UnSubscribe(subscription);
            this.AddDebugLog("Unsubscribed from portfolio lookup, TransactionId={0}, OriginalTransactionId={1}",
                subscription.TransactionId, subscription.OriginalTransactionId);
        }

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            SubscriptionStopped += SubscriptionStoppedHandler;
            SubscriptionFailed += SubscriptionFailedHandler;
            SubscriptionOnline += SubscriptionOnlineHandler;

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
                    $"Portfolio lookup timed out after {timeout.Value.TotalSeconds} seconds");
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                SubscriptionStopped -= SubscriptionStoppedHandler;
                SubscriptionFailed -= SubscriptionFailedHandler;
                SubscriptionOnline -= SubscriptionOnlineHandler;
            }
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Portfolio lookup cancelled, TransactionId={0}", transactionId);
            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Portfolio lookup failed, TransactionId={0}: {1}", transactionId, ex);
            throw;
        }
        finally
        {
            _portfolioLookupTaskSource.TryRemove(transactionId, out var _);
        }
    }

    private static List<Portfolio> ApplyFilters(IEnumerable<Portfolio> portfolios, GetPortfoliosRequest request)
    {
        var result = portfolios.AsEnumerable();

        // Apply portfolio name filter
        if (!string.IsNullOrEmpty(request.PortfolioName))
            result = result.Where(p => p.Name == request.PortfolioName);

        // Note: From/To date filters are typically for historical data
        // Current implementation returns live portfolios

        return result.ToList();
    }
}
