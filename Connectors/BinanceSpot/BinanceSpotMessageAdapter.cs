using Binance.Net.Clients;
using StockSharp.CryptoExchange;

namespace StockSharp.Binance.Spot;

/// <summary>
/// Message adapter for Binance Spot trading.
/// </summary>
public partial class BinanceSpotMessageAdapter : CryptoExchangeAdapterBase<BinanceRestClient, BinanceSocketClient>
{
    /// <summary>
    /// Initialize Binance Spot message adapter.
    /// </summary>
    /// <param name="transactionIdGenerator">Transaction ID generator</param>
    public BinanceSpotMessageAdapter(IdGenerator transactionIdGenerator)
        : base(transactionIdGenerator)
    {
        DisplayName = "Binance Spot";
        Description = "Binance cryptocurrency exchange spot trading adapter";
    }

    /// <inheritdoc />
    protected override string GetBoardCode() => "BINANCE";

    /// <inheritdoc />
    protected override BinanceRestClient CreateRestClient()
    {
        var options = new BinanceRestOptions();
        ConfigureRestClientOptions(options);
        return new BinanceRestClient(options);
    }

    /// <inheritdoc />
    protected override BinanceSocketClient CreateSocketClient()
    {
        var options = new BinanceSocketOptions();
        ConfigureSocketClientOptions(options);
        return new BinanceSocketClient(options);
    }

    /// <inheritdoc />
    protected override void ConfigureAuthentication(BinanceRestClient restClient, BinanceSocketClient socketClient, ApiCredentials credentials)
    {
        // Configure authentication for both clients
        restClient.SetApiCredentials(credentials);
        socketClient.SetApiCredentials(credentials);
    }

    /// <inheritdoc />
    protected override async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        // Test connection by getting server time
        var result = await RestClient.SpotApi.ExchangeData.GetServerTimeAsync(cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Connection test failed: {result.Error?.Message}");
        }

        this.AddInfoLog("Server time: {0}", result.Data);
    }

    /// <summary>
    /// Configure REST client options.
    /// </summary>
    /// <param name="options">REST options</param>
    protected virtual void ConfigureRestClientOptions(BinanceRestOptions options)
    {
        options.Environment = UseTestnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
        options.RequestTimeout = TimeSpan.FromSeconds(30);
        options.ReceiveWindow = TimeSpan.FromSeconds(5);

        // Configure rate limiting
        options.RateLimitingBehaviour = RateLimitingBehaviour.Wait;
    }

    /// <summary>
    /// Configure WebSocket client options.
    /// </summary>
    /// <param name="options">Socket options</param>
    protected virtual void ConfigureSocketClientOptions(BinanceSocketOptions options)
    {
        options.Environment = UseTestnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
        options.ReconnectInterval = ReconnectInterval;
        options.AutoReconnect = AutoReconnect;
        options.MaxReconnectAttempts = 10;
    }

    #region Symbol Mapping and Validation (T031)

    private readonly ConcurrentDictionary<string, BinanceSymbol> _symbolCache = new();
    private DateTimeOffset _lastSymbolCacheUpdate = DateTimeOffset.MinValue;
    private readonly TimeSpan _symbolCacheExpiry = TimeSpan.FromHours(1);

    /// <summary>
    /// Validate and normalize symbol for Binance trading.
    /// </summary>
    /// <param name="symbol">Symbol to validate</param>
    /// <returns>Normalized symbol or null if invalid</returns>
    public string? ValidateAndNormalizeSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return null;

        // Normalize to uppercase
        var normalized = symbol.ToUpperInvariant().Trim();

        // Basic format validation
        if (!IsValidBinanceSymbol(normalized))
            return null;

        // Check against cached exchange info if available
        if (ValidateSymbols && _symbolCache.ContainsKey(normalized))
        {
            var symbolInfo = _symbolCache[normalized];
            return symbolInfo.Status == SymbolStatus.Trading ? normalized : null;
        }

        return normalized;
    }

    /// <summary>
    /// Get symbol information from cache or exchange.
    /// </summary>
    /// <param name="symbol">Symbol to lookup</param>
    /// <returns>Symbol information or null</returns>
    public async Task<BinanceSymbol?> GetSymbolInfoAsync(string symbol)
    {
        var normalizedSymbol = ValidateAndNormalizeSymbol(symbol);
        if (normalizedSymbol == null)
            return null;

        // Check cache first
        if (_symbolCache.TryGetValue(normalizedSymbol, out var cachedSymbol) &&
            DateTimeOffset.UtcNow - _lastSymbolCacheUpdate < _symbolCacheExpiry)
        {
            return cachedSymbol;
        }

        // Refresh cache if needed
        await RefreshSymbolCacheAsync();

        return _symbolCache.TryGetValue(normalizedSymbol, out var symbolInfo) ? symbolInfo : null;
    }

    /// <summary>
    /// Refresh symbol cache from exchange.
    /// </summary>
    /// <returns>Task</returns>
    public async Task RefreshSymbolCacheAsync()
    {
        if (RestClient == null)
            return;

        try
        {
            var result = await RestClient.SpotApi.ExchangeData.GetExchangeInfoAsync();

            if (result.Success)
            {
                _symbolCache.Clear();

                foreach (var symbol in result.Data.Symbols)
                {
                    _symbolCache.TryAdd(symbol.Name, symbol);
                }

                _lastSymbolCacheUpdate = DateTimeOffset.UtcNow;

                this.AddInfoLog("Symbol cache updated with {0} symbols", result.Data.Symbols.Count());
            }
            else
            {
                this.AddWarningLog("Failed to refresh symbol cache: {0}", result.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error refreshing symbol cache: {0}", ex.Message);
        }
    }

    #endregion

    #region Error Handling and Rate Limit Management (T032)

    /// <summary>
    /// Handle Binance API errors and convert to StockSharp format.
    /// </summary>
    /// <param name="error">Binance error</param>
    /// <param name="context">Context information</param>
    /// <returns>StockSharp error message</returns>
    public ErrorMessage HandleBinanceError(Error error, string context = "")
    {
        var errorMessage = new ErrorMessage
        {
            Error = CreateStockSharpException(error, context),
            LocalTime = DateTimeOffset.Now
        };

        // Log based on error severity
        if (IsTransientError(error))
        {
            this.AddWarningLog("Transient error in {0}: [{1}] {2}", context, error.Code, error.Message);
        }
        else
        {
            this.AddErrorLog("Permanent error in {0}: [{1}] {2}", context, error.Code, error.Message);
        }

        return errorMessage;
    }

    /// <summary>
    /// Determine if Binance error is transient (can be retried).
    /// </summary>
    /// <param name="error">Binance error</param>
    /// <returns>True if transient</returns>
    public static bool IsTransientError(Error error)
    {
        if (error.Code == null) return true; // Network errors are usually transient

        return error.Code.Value switch
        {
            // Rate limiting errors
            -1003 => true, // Too many requests
            -1015 => true, // Too many orders
            429 => true,   // Rate limit exceeded

            // Network/server errors
            -1000 => true, // Unknown error (network issues)
            -1001 => true, // Disconnected
            -1006 => true, // Unexpected response
            -1007 => true, // Timeout
            500 => true,   // Internal server error
            502 => true,   // Bad gateway
            503 => true,   // Service unavailable
            504 => true,   // Gateway timeout

            // Maintenance
            -1013 => true, // Invalid quantity (could be due to market conditions)

            // All others are permanent
            _ => false
        };
    }

    /// <summary>
    /// Get retry delay for transient errors.
    /// </summary>
    /// <param name="error">Binance error</param>
    /// <param name="attemptNumber">Current attempt number</param>
    /// <returns>Delay before retry</returns>
    public static TimeSpan GetRetryDelay(Error error, int attemptNumber)
    {
        if (error.Code == null)
            return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attemptNumber))); // Exponential backoff

        return error.Code.Value switch
        {
            // Rate limiting - longer delays
            -1003 => TimeSpan.FromSeconds(60 + attemptNumber * 30),
            -1015 => TimeSpan.FromSeconds(60 + attemptNumber * 30),
            429 => TimeSpan.FromSeconds(60 + attemptNumber * 30),

            // Server errors - shorter delays
            -1000 or -1001 or -1006 or -1007 => TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attemptNumber))),
            500 or 502 or 503 or 504 => TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attemptNumber))),

            // Default exponential backoff
            _ => TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attemptNumber)))
        };
    }

    /// <summary>
    /// Create StockSharp exception from Binance error.
    /// </summary>
    /// <param name="error">Binance error</param>
    /// <param name="context">Context information</param>
    /// <returns>StockSharp exception</returns>
    private static Exception CreateStockSharpException(Error error, string context)
    {
        var message = string.IsNullOrEmpty(context)
            ? $"[{error.Code}] {error.Message}"
            : $"{context}: [{error.Code}] {error.Message}";

        return error.Code switch
        {
            // Authentication errors
            -2014 or -2015 => new UnauthorizedAccessException(message),

            // Invalid request errors
            -1102 or -1100 or -1101 => new ArgumentException(message),

            // Order errors
            -2010 or -2011 or -2013 => new InvalidOperationException(message),

            // Rate limiting
            -1003 or -1015 or 429 => new InvalidOperationException(message),

            // Network/timeout errors
            -1000 or -1001 or -1006 or -1007 => new TimeoutException(message),

            // Server errors
            500 or 502 or 503 or 504 => new InvalidOperationException(message),

            // Default
            _ => new InvalidOperationException(message)
        };
    }

    /// <summary>
    /// Execute operation with retry logic for transient errors.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="context">Context for logging</param>
    /// <param name="maxRetries">Maximum retry attempts</param>
    /// <returns>Operation result</returns>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<WebCallResult<T>>> operation,
        string context,
        int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                var result = await operation();

                if (result.Success)
                {
                    if (attempt > 1)
                    {
                        this.AddInfoLog("{0} succeeded on attempt {1}", context, attempt);
                    }
                    return result.Data;
                }

                // Check if error is transient
                if (result.Error != null && IsTransientError(result.Error) && attempt <= maxRetries)
                {
                    var delay = GetRetryDelay(result.Error, attempt);
                    this.AddWarningLog("{0} failed (attempt {1}/{2}), retrying in {3}s: [{4}] {5}",
                        context, attempt, maxRetries + 1, delay.TotalSeconds, result.Error.Code, result.Error.Message);

                    await Task.Delay(delay);
                    continue;
                }

                // Non-transient error or max retries exceeded
                throw CreateStockSharpException(result.Error!, context);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException) && attempt <= maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempt)));
                this.AddWarningLog("{0} threw exception (attempt {1}/{2}), retrying in {3}s: {4}",
                    context, attempt, maxRetries + 1, delay.TotalSeconds, ex.Message);

                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException($"{context} failed after {maxRetries + 1} attempts");
    }

    #endregion

    #region WebSocket Event Handling (T033)

    private readonly ConcurrentDictionary<string, UpdateSubscription> _userDataSubscriptions = new();
    private UpdateSubscription? _userDataStreamSubscription;

    /// <summary>
    /// Initialize WebSocket subscriptions for user data stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    protected override async Task InitializeUserDataStreamAsync(CancellationToken cancellationToken)
    {
        if (SocketClient == null || !IsAuthenticated)
            return;

        try
        {
            // Start user data stream for account and order updates
            var listenKeyResult = await ExecuteWithRetryAsync(
                () => RestClient!.SpotApi.Account.StartUserStreamAsync(cancellationToken),
                "Start user data stream");

            if (listenKeyResult != null)
            {
                var subscription = await SocketClient.SpotApi.Account.SubscribeToUserDataUpdatesAsync(
                    listenKeyResult,
                    OnAccountUpdate,
                    OnOrderUpdate,
                    OnOcoOrderUpdate,
                    OnAccountPositionUpdate,
                    OnBalanceUpdate,
                    cancellationToken);

                if (subscription.Success)
                {
                    _userDataStreamSubscription = subscription.Data;
                    this.AddInfoLog("User data stream subscribed with listen key: {0}", listenKeyResult);

                    // Keep alive the listen key every 30 minutes
                    _ = Task.Run(async () => await KeepAliveUserDataStreamAsync(listenKeyResult, cancellationToken), cancellationToken);
                }
                else
                {
                    this.AddErrorLog("Failed to subscribe to user data stream: {0}", subscription.Error?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error initializing user data stream: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Keep alive user data stream listen key.
    /// </summary>
    /// <param name="listenKey">Listen key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task KeepAliveUserDataStreamAsync(string listenKey, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _userDataStreamSubscription != null)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);

                if (RestClient != null)
                {
                    await ExecuteWithRetryAsync(
                        () => RestClient.SpotApi.Account.KeepAliveUserStreamAsync(listenKey, cancellationToken),
                        "Keep alive user data stream");

                    this.AddDebugLog("User data stream keep-alive sent");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                this.AddWarningLog("Error keeping user data stream alive: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Handle account update events from WebSocket.
    /// </summary>
    /// <param name="data">Account update data</param>
    private void OnAccountUpdate(BinanceStreamAccountInfo data)
    {
        try
        {
            // Convert to StockSharp portfolio message
            var portfolioMessage = new PositionChangeMessage
            {
                SecurityId = SecurityId.Money,
                ServerTime = data.UpdateTime,
                LocalTime = DateTimeOffset.Now
            };

            // Add balance changes for each asset
            foreach (var balance in data.Balances.Where(b => b.Available > 0 || b.Locked > 0))
            {
                var securityId = new SecurityId { SecurityCode = balance.Asset, BoardCode = GetBoardCode() };

                var positionMessage = new PositionChangeMessage
                {
                    SecurityId = securityId,
                    ServerTime = data.UpdateTime,
                    LocalTime = DateTimeOffset.Now
                };

                positionMessage
                    .Add(PositionChangeTypes.RealizedPnL, (decimal?)balance.Available)
                    .Add(PositionChangeTypes.UnrealizedPnL, (decimal?)balance.Locked);

                SendOutMessage(positionMessage);
            }

            this.AddDebugLog("Account update processed: {0} balances", data.Balances.Count());
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing account update: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Handle order update events from WebSocket.
    /// </summary>
    /// <param name="data">Order update data</param>
    private void OnOrderUpdate(BinanceStreamOrderUpdate data)
    {
        try
        {
            // Find StockSharp transaction ID
            if (!_reverseOrderMapping.TryGetValue(data.Id, out var transactionId))
            {
                // Try to parse from client order ID
                if (long.TryParse(data.ClientOrderId, out var clientId))
                {
                    transactionId = clientId;
                }
                else
                {
                    this.AddWarningLog("Order update for unknown order: {0}", data.Id);
                    return;
                }
            }

            var securityId = CreateSecurityId(data.Symbol);
            var executionMessage = new ExecutionMessage
            {
                SecurityId = securityId,
                DataType = DataType.Transactions,
                ExecutionType = ExecutionTypes.Transaction,
                OriginalTransactionId = transactionId,
                OrderId = data.Id,
                OrderState = MapBinanceOrderStatus(data.Status),
                Side = data.Side == OrderSide.Buy ? Sides.Buy : Sides.Sell,
                Volume = data.Quantity,
                Price = data.Price,
                Balance = data.Quantity - data.QuantityFilled,
                ServerTime = data.UpdateTime,
                LocalTime = DateTimeOffset.Now
            };

            // Handle fills
            if (data.LastQuantityFilled > 0)
            {
                var tradeMessage = new ExecutionMessage
                {
                    SecurityId = securityId,
                    DataType = DataType.Transactions,
                    ExecutionType = ExecutionTypes.Trade,
                    OriginalTransactionId = transactionId,
                    OrderId = data.Id,
                    TradeId = data.TradeId,
                    Volume = data.LastQuantityFilled,
                    Price = data.LastPriceFilled,
                    Commission = data.Fee,
                    ServerTime = data.UpdateTime,
                    LocalTime = DateTimeOffset.Now
                };

                SendOutMessage(tradeMessage);
            }

            SendOutMessage(executionMessage);

            this.AddDebugLog("Order update: {0} {1} {2} @ {3}", data.Status, data.Side, data.Quantity, data.Price);
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing order update: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Handle OCO order update events from WebSocket.
    /// </summary>
    /// <param name="data">OCO order update data</param>
    private void OnOcoOrderUpdate(BinanceStreamOrderList data)
    {
        try
        {
            // Process each order in the OCO list
            foreach (var order in data.Orders)
            {
                if (_reverseOrderMapping.TryGetValue(order.Id, out var transactionId))
                {
                    var securityId = CreateSecurityId(order.Symbol);
                    var executionMessage = new ExecutionMessage
                    {
                        SecurityId = securityId,
                        DataType = DataType.Transactions,
                        ExecutionType = ExecutionTypes.Transaction,
                        OriginalTransactionId = transactionId,
                        OrderId = order.Id,
                        OrderState = MapBinanceOrderStatus(order.Status),
                        ServerTime = DateTimeOffset.UtcNow,
                        LocalTime = DateTimeOffset.Now
                    };

                    SendOutMessage(executionMessage);
                }
            }

            this.AddDebugLog("OCO order update: {0} orders", data.Orders.Count());
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing OCO order update: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Handle account position update events from WebSocket.
    /// </summary>
    /// <param name="data">Position update data</param>
    private void OnAccountPositionUpdate(BinanceStreamPositionsUpdate data)
    {
        try
        {
            foreach (var position in data.Positions)
            {
                var securityId = new SecurityId { SecurityCode = position.Asset, BoardCode = GetBoardCode() };

                var positionMessage = new PositionChangeMessage
                {
                    SecurityId = securityId,
                    ServerTime = data.UpdateTime,
                    LocalTime = DateTimeOffset.Now
                };

                positionMessage.Add(PositionChangeTypes.CurrentValue, (decimal?)position.Free + position.Locked);

                SendOutMessage(positionMessage);
            }

            this.AddDebugLog("Position update: {0} positions", data.Positions.Count());
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing position update: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Handle balance update events from WebSocket.
    /// </summary>
    /// <param name="data">Balance update data</param>
    private void OnBalanceUpdate(BinanceStreamBalanceUpdate data)
    {
        try
        {
            var securityId = new SecurityId { SecurityCode = data.Asset, BoardCode = GetBoardCode() };

            var positionMessage = new PositionChangeMessage
            {
                SecurityId = securityId,
                ServerTime = data.UpdateTime,
                LocalTime = DateTimeOffset.Now
            };

            positionMessage
                .Add(PositionChangeTypes.RealizedPnL, (decimal?)data.Available)
                .Add(PositionChangeTypes.UnrealizedPnL, (decimal?)data.Locked);

            SendOutMessage(positionMessage);

            this.AddDebugLog("Balance update: {0} = {1} available, {2} locked", data.Asset, data.Available, data.Locked);
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing balance update: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Convert Binance trade data to StockSharp ExecutionMessage.
    /// </summary>
    /// <param name="data">Binance trade data</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>StockSharp execution message</returns>
    protected override ExecutionMessage ConvertToStockSharpMessage(BinanceStreamTrade data, SecurityId securityId)
    {
        return new ExecutionMessage
        {
            SecurityId = securityId,
            DataType = DataType.Ticks,
            ExecutionType = ExecutionTypes.Tick,
            TradeId = data.Id,
            Volume = data.Quantity,
            Price = data.Price,
            Side = data.BuyerIsMaker ? Sides.Sell : Sides.Buy,
            ServerTime = data.TradeTime,
            LocalTime = DateTimeOffset.Now
        };
    }

    /// <summary>
    /// Convert Binance order book data to StockSharp QuoteChangeMessage.
    /// </summary>
    /// <param name="data">Binance order book data</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>StockSharp quote change message</returns>
    protected override QuoteChangeMessage ConvertToStockSharpMessage(BinanceOrderBook data, SecurityId securityId)
    {
        var message = new QuoteChangeMessage
        {
            SecurityId = securityId,
            ServerTime = DateTimeOffset.UtcNow,
            LocalTime = DateTimeOffset.Now,
            IsByLevel = true
        };

        var quotes = new List<QuoteChange>();

        // Add bids
        foreach (var bid in data.Bids.Take(OrderBookDepth))
        {
            quotes.Add(new QuoteChange
            {
                Side = Sides.Buy,
                Price = bid.Price,
                Volume = bid.Quantity
            });
        }

        // Add asks
        foreach (var ask in data.Asks.Take(OrderBookDepth))
        {
            quotes.Add(new QuoteChange
            {
                Side = Sides.Sell,
                Price = ask.Price,
                Volume = ask.Quantity
            });
        }

        message.Quotes = quotes.ToArray();
        return message;
    }

    /// <summary>
    /// Cleanup WebSocket subscriptions.
    /// </summary>
    /// <returns>Task</returns>
    protected override async Task CleanupWebSocketSubscriptionsAsync()
    {
        try
        {
            // Close user data stream subscription
            if (_userDataStreamSubscription != null)
            {
                await _userDataStreamSubscription.CloseAsync();
                _userDataStreamSubscription = null;
                this.AddInfoLog("User data stream subscription closed");
            }

            // Close market data subscriptions
            var subscriptionsToClose = _marketDataSubscriptions.Values.ToArray();
            _marketDataSubscriptions.Clear();

            foreach (var subscription in subscriptionsToClose)
            {
                try
                {
                    await subscription.CloseAsync();
                }
                catch (Exception ex)
                {
                    this.AddWarningLog("Error closing subscription: {0}", ex.Message);
                }
            }

            // Close user data subscriptions
            var userDataToClose = _userDataSubscriptions.Values.ToArray();
            _userDataSubscriptions.Clear();

            foreach (var subscription in userDataToClose)
            {
                try
                {
                    await subscription.CloseAsync();
                }
                catch (Exception ex)
                {
                    this.AddWarningLog("Error closing user data subscription: {0}", ex.Message);
                }
            }

            this.AddInfoLog("WebSocket subscriptions cleanup completed");
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error during WebSocket cleanup: {0}", ex.Message);
        }
    }

    #endregion
}