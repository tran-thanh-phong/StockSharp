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
}