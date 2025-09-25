using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;

namespace StockSharp.Binance.Spot;

/// <summary>
/// REST API integration for BinanceSpotMessageAdapter.
/// </summary>
public partial class BinanceSpotMessageAdapter
{
    #region REST API Integration (T034)

    /// <summary>
    /// Get exchange information from REST API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange information</returns>
    public async Task<BinanceExchangeInfo?> GetExchangeInfoAsync(CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetExchangeInfoAsync(cancellationToken),
                "Get exchange info");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get exchange info: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get account information from REST API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Account information</returns>
    public async Task<BinanceAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.Account.GetAccountInfoAsync(cancellationToken: cancellationToken),
                "Get account info");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get account info: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get open orders from REST API.
    /// </summary>
    /// <param name="symbol">Symbol to filter (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of open orders</returns>
    public async Task<IEnumerable<BinanceOrder>?> GetOpenOrdersAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.Trading.GetOpenOrdersAsync(symbol, cancellationToken: cancellationToken),
                $"Get open orders{(symbol != null ? $" for {symbol}" : "")}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get open orders: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get order history from REST API.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="orderId">Order ID (optional)</param>
    /// <param name="startTime">Start time filter (optional)</param>
    /// <param name="endTime">End time filter (optional)</param>
    /// <param name="limit">Result limit (default: 500, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orders</returns>
    public async Task<IEnumerable<BinanceOrder>?> GetOrderHistoryAsync(
        string symbol,
        long? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.Trading.GetOrdersAsync(
                    symbol,
                    orderId: orderId,
                    startTime: startTime,
                    endTime: endTime,
                    limit: limit,
                    ct: cancellationToken),
                $"Get order history for {symbol}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get order history for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get trade history from REST API.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="orderId">Order ID (optional)</param>
    /// <param name="startTime">Start time filter (optional)</param>
    /// <param name="endTime">End time filter (optional)</param>
    /// <param name="fromId">Trade ID to start from (optional)</param>
    /// <param name="limit">Result limit (default: 500, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of trades</returns>
    public async Task<IEnumerable<BinanceTrade>?> GetTradeHistoryAsync(
        string symbol,
        long? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        long? fromId = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.Trading.GetUserTradesAsync(
                    symbol,
                    orderId: orderId,
                    startTime: startTime,
                    endTime: endTime,
                    fromId: fromId,
                    limit: limit,
                    ct: cancellationToken),
                $"Get trade history for {symbol}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get trade history for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get 24-hour ticker price change statistics.
    /// </summary>
    /// <param name="symbol">Symbol (optional, if null returns all symbols)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ticker statistics</returns>
    public async Task<IEnumerable<Binance24HPrice>?> Get24HrTickerAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            if (symbol != null)
            {
                var singleResult = await ExecuteWithRetryAsync(
                    () => RestClient.SpotApi.ExchangeData.GetTickerAsync(symbol, cancellationToken),
                    $"Get 24hr ticker for {symbol}");

                return singleResult != null ? new[] { singleResult } : null;
            }
            else
            {
                var allResult = await ExecuteWithRetryAsync(
                    () => RestClient.SpotApi.ExchangeData.GetTickersAsync(cancellationToken),
                    "Get 24hr ticker for all symbols");

                return allResult;
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get 24hr ticker{0}: {1}",
                symbol != null ? $" for {symbol}" : "", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get current order book for a symbol.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="limit">Depth limit (5, 10, 20, 50, 100, 500, 1000, 5000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order book</returns>
    public async Task<BinanceOrderBook?> GetOrderBookAsync(string symbol, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, limit, cancellationToken),
                $"Get order book for {symbol}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get order book for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get recent trades for a symbol.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="limit">Trade limit (default: 500, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recent trades</returns>
    public async Task<IEnumerable<BinanceRecentTrade>?> GetRecentTradesAsync(string symbol, int? limit = null, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetRecentTradesAsync(symbol, limit, cancellationToken),
                $"Get recent trades for {symbol}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get recent trades for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get kline/candlestick data for a symbol.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="interval">Kline interval</param>
    /// <param name="startTime">Start time (optional)</param>
    /// <param name="endTime">End time (optional)</param>
    /// <param name="limit">Data limit (default: 500, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Kline data</returns>
    public async Task<IEnumerable<BinanceKline>?> GetKlinesAsync(
        string symbol,
        BinanceKlineInterval interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetKlinesAsync(
                    symbol,
                    interval,
                    startTime: startTime,
                    endTime: endTime,
                    limit: limit,
                    ct: cancellationToken),
                $"Get klines for {symbol} at {interval}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get klines for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get current average price for a symbol.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Average price</returns>
    public async Task<BinanceAveragePrice?> GetCurrentAvgPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetCurrentAvgPriceAsync(symbol, cancellationToken),
                $"Get average price for {symbol}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get average price for {0}: {1}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get all symbol price tickers.
    /// </summary>
    /// <param name="symbol">Symbol (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Price tickers</returns>
    public async Task<IEnumerable<BinancePrice>?> GetPricesAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            if (symbol != null)
            {
                var singleResult = await ExecuteWithRetryAsync(
                    () => RestClient.SpotApi.ExchangeData.GetPriceAsync(symbol, cancellationToken),
                    $"Get price for {symbol}");

                return singleResult != null ? new[] { singleResult } : null;
            }
            else
            {
                var allResult = await ExecuteWithRetryAsync(
                    () => RestClient.SpotApi.ExchangeData.GetPricesAsync(cancellationToken),
                    "Get prices for all symbols");

                return allResult;
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get prices{0}: {1}",
                symbol != null ? $" for {symbol}" : "", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Test connectivity to the REST API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection successful</returns>
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            return false;

        try
        {
            var result = await RestClient.SpotApi.ExchangeData.PingAsync(cancellationToken);

            if (result.Success)
            {
                this.AddInfoLog("REST API connectivity test successful");
                return true;
            }
            else
            {
                this.AddWarningLog("REST API connectivity test failed: {0}", result.Error?.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("REST API connectivity test error: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Get server time from REST API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server time</returns>
    public async Task<DateTime?> GetServerTimeAsync(CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            return null;

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.ExchangeData.GetServerTimeAsync(cancellationToken),
                "Get server time");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get server time: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Sync local time with server time to handle clock drift.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time difference in milliseconds</returns>
    public async Task<long> SyncTimeAsync(CancellationToken cancellationToken = default)
    {
        var localTime = DateTime.UtcNow;
        var serverTime = await GetServerTimeAsync(cancellationToken);

        if (serverTime.HasValue)
        {
            var timeDiff = (long)(serverTime.Value - localTime).TotalMilliseconds;
            this.AddInfoLog("Time sync: Server time difference = {0}ms", timeDiff);
            return timeDiff;
        }

        return 0;
    }

    /// <summary>
    /// Get trading fees for current account.
    /// </summary>
    /// <param name="symbol">Symbol (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Trading fees</returns>
    public async Task<IEnumerable<BinanceTradeFee>?> GetTradingFeesAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        if (RestClient == null)
            throw new InvalidOperationException("REST client not initialized");

        try
        {
            var result = await ExecuteWithRetryAsync(
                () => RestClient.SpotApi.Account.GetTradeFeeAsync(symbol, cancellationToken: cancellationToken),
                $"Get trading fees{(symbol != null ? $" for {symbol}" : "")}");

            return result;
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Failed to get trading fees: {0}", ex.Message);
            return null;
        }
    }

    #endregion
}