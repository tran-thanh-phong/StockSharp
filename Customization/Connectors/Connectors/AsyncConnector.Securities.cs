using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

public partial class AsyncConnector
{
    private void OnLookupSecuritiesResult(SecurityLookupMessage message, IEnumerable<Security> securities, Exception ex)
    {
        var transactionId = message.TransactionId;

        if (!_securityLookupTaskSource.TryRemove(transactionId, out var taskSource))
            return;

        if (ex != null)
        {
            this.AddErrorLog("Security lookup failed for transaction {0}: {1}", transactionId, ex);
            taskSource.TrySetException(ex);
        }
        else
        {
            this.AddDebugLog("Security lookup completed for transaction {0}, found {1} securities", transactionId, securities?.Count() ?? 0);
            taskSource.TrySetResult(securities?.ToList() ?? new List<Security>());
        }
    }

    /// <summary>
    /// Asynchronously retrieves securities matching the specified symbol.
    /// </summary>
    /// <param name="request">The request containing the symbol to search for.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of securities matching the symbol.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request or symbol is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<Security>> GetSecuritiesAsync(
        GetSecuritiesRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        timeout ??= TimeSpan.FromSeconds(30);

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var subscription = new Subscription(new SecurityLookupMessage
        {
            SecurityId = new() { SecurityCode = request.Symbol },
            TransactionId = transactionId
        });

        this.AddDebugLog("Starting security lookup for symbol '{0}', TransactionId={1}", request.Symbol, transactionId);

        var taskSource = new TaskCompletionSource<List<Security>>();
        _securityLookupTaskSource.TryAdd(transactionId, taskSource);

        try
        {
            // Register cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                if (_securityLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

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
            var timeoutException =
                new TimeoutException(
                    $"Security lookup timed out after {timeout.Value.TotalSeconds} seconds for symbol '{request.Symbol}'");
            if (_securityLookupTaskSource.TryRemove(transactionId, out var tcs))
            {
                tcs.TrySetException(timeoutException);
            }

            throw timeoutException;
        }
        catch (OperationCanceledException)
        {
            this.AddWarningLog("Security lookup cancelled for symbol '{0}', TransactionId={1}", request.Symbol,
                transactionId);

            throw;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Security lookup failed for symbol '{0}', TransactionId={1}: {2}", request.Symbol,
                transactionId, ex);

            throw;
        }
        finally
        {
            _securityLookupTaskSource.TryRemove(transactionId, out var _);
        }
    }
}
