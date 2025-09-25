using StockSharp.CryptoExchange.MessageConverters;

namespace StockSharp.CryptoExchange;

/// <summary>
/// Abstract base class for cryptocurrency exchange message adapters using CryptoExchange.Net framework.
/// </summary>
/// <typeparam name="TRestClient">REST client type from CryptoExchange.Net</typeparam>
/// <typeparam name="TSocketClient">WebSocket client type from CryptoExchange.Net</typeparam>
public abstract class CryptoExchangeAdapterBase<TRestClient, TSocketClient> : AsyncMessageAdapter, IKeySecretAdapter
    where TRestClient : BaseRestClient
    where TSocketClient : BaseSocketClient
{
    private readonly MessageConverterRegistry _messageConverters;
    private readonly ConcurrentDictionary<SecurityId, SubscriptionInfo> _activeSubscriptions = new();

    /// <summary>
    /// REST client instance.
    /// </summary>
    protected TRestClient? RestClient { get; private set; }

    /// <summary>
    /// WebSocket client instance.
    /// </summary>
    protected TSocketClient? SocketClient { get; private set; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public SecureString Key { get; set; } = new();

    /// <summary>
    /// API secret for authentication.
    /// </summary>
    public SecureString Secret { get; set; } = new();

    /// <summary>
    /// Use testnet environment.
    /// </summary>
    public bool UseTestnet { get; set; }

    /// <summary>
    /// Auto-reconnect on connection loss.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Reconnection interval.
    /// </summary>
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initialize crypto exchange adapter.
    /// </summary>
    /// <param name="transactionIdGenerator">Transaction ID generator</param>
    protected CryptoExchangeAdapterBase(IdGenerator transactionIdGenerator)
        : base(transactionIdGenerator)
    {
        _messageConverters = new MessageConverterRegistry();
        InitializeMessageConverters();
        ConfigureAdapter();
    }

    /// <summary>
    /// Configure adapter capabilities and settings.
    /// </summary>
    protected virtual void ConfigureAdapter()
    {
        this.AddMarketDataSupport();
        this.AddTransactionalSupport();

        this.AddSupportedMarketDataType(DataType.Ticks);
        this.AddSupportedMarketDataType(DataType.MarketDepth);
        this.AddSupportedMarketDataType(DataType.Level1);

        // Add supported candle timeframes
        this.AddSupportedCandleTimeFrames(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(12),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(7)
        );

        HeartbeatInterval = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Initialize message converters for the exchange.
    /// </summary>
    protected virtual void InitializeMessageConverters()
    {
        _messageConverters.RegisterConverter(new ExecutionMessageConverter());
        _messageConverters.RegisterConverter(new QuoteChangeMessageConverter());
        _messageConverters.RegisterConverter(new SecurityMessageConverter());
        _messageConverters.RegisterConverter(new PortfolioMessageConverter(GetPortfolioName()));
    }

    /// <summary>
    /// Create REST client instance.
    /// </summary>
    /// <returns>Configured REST client</returns>
    protected abstract TRestClient CreateRestClient();

    /// <summary>
    /// Create WebSocket client instance.
    /// </summary>
    /// <returns>Configured WebSocket client</returns>
    protected abstract TSocketClient CreateSocketClient();

    /// <summary>
    /// Get board code for this exchange.
    /// </summary>
    /// <returns>Board identifier</returns>
    protected abstract string GetBoardCode();

    /// <summary>
    /// Get portfolio name for this exchange.
    /// </summary>
    /// <returns>Portfolio name</returns>
    protected virtual string GetPortfolioName()
    {
        var boardCode = GetBoardCode();
        return UseTestnet ? $"{boardCode}_TESTNET" : boardCode;
    }

    /// <inheritdoc />
    public override async ValueTask ConnectAsync(ConnectMessage connectMsg, CancellationToken cancellationToken)
    {
        if (RestClient != null || SocketClient != null)
            return;

        try
        {
            // Create and configure clients
            RestClient = CreateRestClient();
            SocketClient = CreateSocketClient();

            // Configure authentication
            if (Key.Length > 0 && Secret.Length > 0)
            {
                var keyString = Key.ToInsecureString();
                var secretString = Secret.ToInsecureString();
                var credentials = new ApiCredentials(keyString, secretString);
                ConfigureAuthentication(RestClient, SocketClient, credentials);
            }

            // Test connection
            await TestConnectionAsync(cancellationToken);

            SendOutMessage(new ConnectMessage());
            this.AddInfoLog("Connected to {0}{1}", GetBoardCode(), UseTestnet ? " (Testnet)" : "");
        }
        catch (Exception ex)
        {
            SendOutMessage(new ConnectMessage { Error = new InvalidOperationException($"Connection failed: {ex.Message}", ex) });
            throw;
        }
    }

    /// <inheritdoc />
    public override ValueTask DisconnectAsync(DisconnectMessage disconnectMsg, CancellationToken cancellationToken)
    {
        try
        {
            // Unsubscribe from all active subscriptions
            foreach (var subscription in _activeSubscriptions.Keys.ToArray())
            {
                try
                {
                    UnsubscribeInternal(subscription);
                }
                catch (Exception ex)
                {
                    this.AddWarningLog("Error unsubscribing from {0}: {1}", subscription, ex.Message);
                }
            }

            _activeSubscriptions.Clear();

            // Dispose clients
            RestClient?.Dispose();
            SocketClient?.Dispose();

            RestClient = null;
            SocketClient = null;

            SendOutMessage(new DisconnectMessage());
            this.AddInfoLog("Disconnected from {0}", GetBoardCode());
        }
        catch (Exception ex)
        {
            SendOutMessage(new DisconnectMessage { Error = ex });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override bool OnSendInMessage(Message message)
    {
        switch (message.Type)
        {
            case MessageTypes.SecurityLookup:
                ProcessSecurityLookupMessage((SecurityLookupMessage)message);
                return true;

            case MessageTypes.MarketData:
                ProcessMarketDataMessage((MarketDataMessage)message);
                return true;

            case MessageTypes.OrderRegister:
                ProcessOrderRegisterMessage((OrderRegisterMessage)message);
                return true;

            case MessageTypes.OrderCancel:
                ProcessOrderCancelMessage((OrderCancelMessage)message);
                return true;

            case MessageTypes.OrderStatus:
                ProcessOrderStatusMessage((OrderStatusMessage)message);
                return true;

            case MessageTypes.Portfolio:
                ProcessPortfolioMessage((PortfolioMessage)message);
                return true;

            default:
                return base.OnSendInMessage(message);
        }
    }

    /// <summary>
    /// Configure authentication for REST and WebSocket clients.
    /// </summary>
    /// <param name="restClient">REST client</param>
    /// <param name="socketClient">WebSocket client</param>
    /// <param name="credentials">API credentials</param>
    protected abstract void ConfigureAuthentication(TRestClient restClient, TSocketClient socketClient, ApiCredentials credentials);

    /// <summary>
    /// Test connection to exchange.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    protected abstract Task TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Process security lookup message.
    /// </summary>
    /// <param name="message">Security lookup message</param>
    protected abstract void ProcessSecurityLookupMessage(SecurityLookupMessage message);

    /// <summary>
    /// Process market data message.
    /// </summary>
    /// <param name="message">Market data message</param>
    protected abstract void ProcessMarketDataMessage(MarketDataMessage message);

    /// <summary>
    /// Process order register message.
    /// </summary>
    /// <param name="message">Order register message</param>
    protected abstract void ProcessOrderRegisterMessage(OrderRegisterMessage message);

    /// <summary>
    /// Process order cancel message.
    /// </summary>
    /// <param name="message">Order cancel message</param>
    protected abstract void ProcessOrderCancelMessage(OrderCancelMessage message);

    /// <summary>
    /// Process order status message.
    /// </summary>
    /// <param name="message">Order status message</param>
    protected abstract void ProcessOrderStatusMessage(OrderStatusMessage message);

    /// <summary>
    /// Process portfolio message.
    /// </summary>
    /// <param name="message">Portfolio message</param>
    protected abstract void ProcessPortfolioMessage(PortfolioMessage message);

    /// <summary>
    /// Convert CryptoExchange.Net object to StockSharp message.
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>Converted message</returns>
    protected Message ConvertToStockSharpMessage(object source, SecurityId securityId)
    {
        try
        {
            return _messageConverters.ConvertToStockSharp(source, securityId);
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to convert message from {0}: {1}", source?.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Create security ID for this exchange.
    /// </summary>
    /// <param name="symbol">Symbol name</param>
    /// <returns>Security identifier</returns>
    protected SecurityId CreateSecurityId(string symbol)
    {
        return new SecurityId
        {
            SecurityCode = symbol,
            BoardCode = GetBoardCode()
        };
    }

    /// <summary>
    /// Unsubscribe from market data internally.
    /// </summary>
    /// <param name="securityId">Security identifier</param>
    protected abstract void UnsubscribeInternal(SecurityId securityId);

    /// <summary>
    /// Track active subscription.
    /// </summary>
    /// <param name="securityId">Security identifier</param>
    /// <param name="dataType">Data type</param>
    /// <param name="subscriptionId">Subscription ID from exchange</param>
    protected void TrackSubscription(SecurityId securityId, DataType dataType, object? subscriptionId = null)
    {
        var info = new SubscriptionInfo(dataType, subscriptionId);
        _activeSubscriptions.TryAdd(securityId, info);
    }

    /// <summary>
    /// Remove subscription tracking.
    /// </summary>
    /// <param name="securityId">Security identifier</param>
    protected void RemoveSubscriptionTracking(SecurityId securityId)
    {
        _activeSubscriptions.TryRemove(securityId, out _);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        RestClient?.Dispose();
        SocketClient?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Subscription tracking information.
    /// </summary>
    protected record SubscriptionInfo(DataType DataType, object? SubscriptionId);
}