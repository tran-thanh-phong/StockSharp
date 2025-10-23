using System.Collections.Concurrent;
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

/// <summary>
/// An extended Connector that provides async/await methods for common operations.
/// Wraps event-driven StockSharp APIs into modern async patterns with timeout and cancellation support.
/// </summary>
public class AsyncConnector : Connector
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<Security>>> _securityLookupTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Order>> _orderRegistrationTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Order>> _orderCancellationTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<Order>>> _orderLookupTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<MyTrade>>> _tradeLookupTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<Portfolio>>> _portfolioLookupTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<IOrderBookMessage>> _marketDepthTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<ITickTradeMessage>>> _ticksTaskSource = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<List<Level1ChangeMessage>>> _level1TaskSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncConnector"/> class.
    /// </summary>
    public AsyncConnector() : base()
    {
        LookupSecuritiesResult += OnLookupSecuritiesResult;
        OrderReceived += OnOrderReceived;
        OrderRegisterFailReceived += OnOrderRegisterFailReceived;
        OwnTradeReceived += OnOwnTradeReceived;
        
    }

    public string OnPortfolioLookupResult { get; set; }

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

    /// <summary>
    /// Asynchronously retrieves portfolios and their positions.
    /// Uses a hybrid approach: returns cached portfolios if available, otherwise subscribes to load them.
    /// </summary>
    /// <param name="request">The portfolio lookup request with optional filters.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">Optional timeout for the lookup operation. Defaults to 30 seconds.</param>
    /// <returns>A list of portfolios with their positions.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<List<Portfolio>> GetPortfoliosAsync(
        GetPortfoliosRequest? request = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        request ??= new GetPortfoliosRequest();
        timeout ??= TimeSpan.FromSeconds(30);

        // Helper method to apply filters to portfolio list
        List<Portfolio> ApplyFilters(IEnumerable<Portfolio> portfolios)
        {
            var filtered = portfolios;

            if (!string.IsNullOrEmpty(request.PortfolioName))
                filtered = filtered.Where(p => p.Name == request.PortfolioName);

            return filtered.ToList();
        }

        // Check if portfolios are already cached
        if (this.Portfolios.Any())
        {
            this.AddDebugLog("Returning cached portfolios, count={0}", this.Portfolios.Count());
            return ApplyFilters(this.Portfolios);
        }

        // Cache is empty, subscribe to load portfolios
        this.AddDebugLog("Portfolio cache is empty, subscribing to load portfolios");

        var transactionId = this.TransactionIdGenerator.GetNextId();
        var message = new PortfolioLookupMessage
        {
            TransactionId = transactionId,
            IsSubscribe = true
        };

        // Apply filters to the message
        if (!string.IsNullOrEmpty(request.PortfolioName))
            message.PortfolioName = request.PortfolioName;
        if (request.From.HasValue)
            message.From = request.From.Value;
        if (request.To.HasValue)
            message.To = request.To.Value;

        var taskSource = new TaskCompletionSource<List<Portfolio>>();
        _portfolioLookupTaskSource.TryAdd(transactionId, taskSource);

        void portfolioHandler(Subscription s, Portfolio p)
        {
            if (s.TransactionId == transactionId)
            {
                // Portfolios are automatically added to this.Portfolios cache by Connector
                this.AddDebugLog("Portfolio received for lookup TransactionId={0}: {1}", transactionId, p.Name);
            }
        }

        void positionHandler(Subscription s, Position pos)
        {
            if (s.TransactionId == transactionId)
            {
                this.AddDebugLog("Position received for lookup TransactionId={0}: {1}", transactionId, pos);
            }
        }

        void subscriptionStoppedHandler(Subscription s, Exception ex)
        {
            if (s.TransactionId == transactionId)
            {
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    if (ex == null)
                    {
                        // Return portfolios from the cache (they were auto-populated during subscription)
                        var result = ApplyFilters(this.Portfolios);
                        this.AddDebugLog("Portfolio lookup completed, TransactionId={0}, returning {1} portfolios from cache", transactionId, result.Count);
                        tcs.TrySetResult(result);
                    }
                    else
                    {
                        this.AddErrorLog("Portfolio lookup stopped with error, TransactionId={0}: {1}", transactionId, ex);
                        tcs.TrySetException(ex);
                    }
                }
            }
        }

        void subscriptionFailedHandler(Subscription s, Exception ex, bool isSubscribe)
        {
            if (s.TransactionId == transactionId && isSubscribe)
            {
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    this.AddErrorLog("Portfolio lookup subscription failed, TransactionId={0}: {1}", transactionId, ex);
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
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });

            PortfolioReceived += portfolioHandler;
            PositionReceived += positionHandler;
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
                    $"Portfolio lookup timed out after {timeout.Value.TotalSeconds} seconds");
                if (_portfolioLookupTaskSource.TryRemove(transactionId, out var tcs))
                {
                    tcs.TrySetException(timeoutException);
                }

                throw timeoutException;
            }
            finally
            {
                PortfolioReceived -= portfolioHandler;
                PositionReceived -= positionHandler;
                SubscriptionStopped -= subscriptionStoppedHandler;
                SubscriptionFailed -= subscriptionFailedHandler;
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

/// <summary>
/// Request object for registering an order.
/// </summary>
/// <param name="Order">The order to register.</param>
public record RegisterOrderRequest(Order Order);

/// <summary>
/// Request object for cancelling an order.
/// </summary>
/// <param name="Order">The order to cancel.</param>
public record CancelOrderRequest(Order Order);

/// <summary>
/// Request object for retrieving orders.
/// </summary>
public record GetOrdersRequest
{
    /// <summary>
    /// Filter by security.
    /// </summary>
    public Security? Security { get; init; }

    /// <summary>
    /// Filter by portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; init; }

    /// <summary>
    /// Start date for historical orders.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical orders.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Filter by order states.
    /// </summary>
    public OrderStates[]? States { get; init; }
}

/// <summary>
/// Request object for retrieving trades.
/// </summary>
public record GetTradesRequest
{
    /// <summary>
    /// Filter by security.
    /// </summary>
    public Security? Security { get; init; }

    /// <summary>
    /// Filter by portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; init; }

    /// <summary>
    /// Start date for historical trades.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical trades.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// Request object for retrieving securities information.
/// </summary>
/// <param name="Symbol">The symbol code to search for (e.g., "EURUSD", "AAPL").</param>
public record GetSecuritiesRequest(string Symbol);

/// <summary>
/// Request object for retrieving portfolios.
/// </summary>
public record GetPortfoliosRequest
{
    /// <summary>
    /// Filter by portfolio name.
    /// </summary>
    public string? PortfolioName { get; init; }

    /// <summary>
    /// Start date for historical portfolio data.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical portfolio data.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// Request object for retrieving market depth snapshot.
/// </summary>
/// <param name="Security">The security to get market depth for.</param>
public record GetMarketDepthRequest(Security Security)
{
    /// <summary>
    /// Maximum depth level (number of price levels to return).
    /// </summary>
    public int? MaxDepth { get; init; }
}

/// <summary>
/// Request object for retrieving historical ticks.
/// </summary>
/// <param name="Security">The security to get ticks for.</param>
public record GetTicksRequest(Security Security)
{
    /// <summary>
    /// Start date for historical ticks.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical ticks.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Number of records to skip.
    /// </summary>
    public long? Skip { get; init; }

    /// <summary>
    /// Maximum number of records to return.
    /// </summary>
    public long? Count { get; init; }
}

/// <summary>
/// Request object for retrieving Level1 market data.
/// </summary>
/// <param name="Security">The security to get Level1 data for.</param>
public record GetLevel1Request(Security Security)
{
    /// <summary>
    /// Start date for historical Level1 data.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical Level1 data.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}