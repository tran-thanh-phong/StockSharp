using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot.Socket;

namespace StockSharp.Binance.Spot;

/// <summary>
/// Market data handling for BinanceSpotMessageAdapter.
/// </summary>
public partial class BinanceSpotMessageAdapter
{
    private readonly ConcurrentDictionary<string, UpdateSubscription> _marketDataSubscriptions = new();

    /// <inheritdoc />
    protected override void ProcessSecurityLookupMessage(SecurityLookupMessage message)
    {
        Task.Run(async () =>
        {
            try
            {
                if (RestClient == null)
                {
                    SendOutMessage(new SecurityLookupResultMessage
                    {
                        OriginalTransactionId = message.TransactionId,
                        Error = new InvalidOperationException("Not connected")
                    });
                    return;
                }

                var result = await RestClient.SpotApi.ExchangeData.GetExchangeInfoAsync();

                if (!result.Success)
                {
                    SendOutMessage(new SecurityLookupResultMessage
                    {
                        OriginalTransactionId = message.TransactionId,
                        Error = new InvalidOperationException(result.Error?.Message ?? "Failed to get exchange info")
                    });
                    return;
                }

                foreach (var symbol in result.Data.Symbols)
                {
                    if (symbol.Status != SymbolStatus.Trading)
                        continue;

                    // Filter by security type if specified
                    if (message.SecurityType.HasValue && message.SecurityType != SecurityTypes.Stock)
                        continue;

                    var securityId = CreateSecurityId(symbol.Name);

                    // Filter by symbol if specified
                    if (!message.SecurityId.IsDefault() && message.SecurityId != securityId)
                        continue;

                    var securityMessage = ConvertToSecurityMessage(symbol, securityId);
                    SendOutMessage(securityMessage);
                }

                SendOutMessage(new SecurityLookupResultMessage
                {
                    OriginalTransactionId = message.TransactionId
                });
            }
            catch (Exception ex)
            {
                SendOutMessage(new SecurityLookupResultMessage
                {
                    OriginalTransactionId = message.TransactionId,
                    Error = ex
                });
            }
        });
    }

    /// <inheritdoc />
    protected override void ProcessMarketDataMessage(MarketDataMessage message)
    {
        var symbol = message.SecurityId.SecurityCode;

        if (string.IsNullOrEmpty(symbol) || !IsValidBinanceSymbol(symbol))
        {
            SendOutMessage(new MarketDataMessage
            {
                OriginalTransactionId = message.TransactionId,
                Error = new ArgumentException($"Invalid symbol: {symbol}")
            });
            return;
        }

        if (message.IsSubscribe)
        {
            SubscribeToMarketData(message, symbol);
        }
        else
        {
            UnsubscribeFromMarketData(message, symbol);
        }
    }

    /// <summary>
    /// Subscribe to market data for a symbol.
    /// </summary>
    /// <param name="message">Market data message</param>
    /// <param name="symbol">Symbol to subscribe</param>
    private void SubscribeToMarketData(MarketDataMessage message, string symbol)
    {
        Task.Run(async () =>
        {
            try
            {
                if (SocketClient == null)
                {
                    SendOutMessage(new MarketDataMessage
                    {
                        OriginalTransactionId = message.TransactionId,
                        Error = new InvalidOperationException("WebSocket client not connected")
                    });
                    return;
                }

                UpdateSubscription? subscription = null;

                if (message.DataType == DataType.Ticks)
                {
                    var result = await SocketClient.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(
                        symbol,
                        data => OnTradeUpdate(data.Data, message.SecurityId),
                        CancellationToken.None);

                    if (result.Success)
                    {
                        subscription = result.Data;
                        this.AddInfoLog("Subscribed to trades for {0}", symbol);
                    }
                    else
                    {
                        this.AddErrorLog("Failed to subscribe to trades for {0}: {1}", symbol, result.Error?.Message);
                    }
                }
                else if (message.DataType == DataType.MarketDepth)
                {
                    var result = await SocketClient.SpotApi.ExchangeData.SubscribeToOrderBookUpdatesAsync(
                        symbol,
                        OrderBookDepth,
                        TimeSpan.FromMilliseconds(100), // 100ms updates
                        data => OnOrderBookUpdate(data.Data, message.SecurityId),
                        CancellationToken.None);

                    if (result.Success)
                    {
                        subscription = result.Data;
                        this.AddInfoLog("Subscribed to order book for {0}", symbol);
                    }
                    else
                    {
                        this.AddErrorLog("Failed to subscribe to order book for {0}: {1}", symbol, result.Error?.Message);
                    }
                }
                else if (message.DataType.IsCandle)
                {
                    var timeFrame = (TimeSpan)message.DataType.Arg;
                    var interval = ToBinanceInterval(timeFrame);

                    var result = await SocketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                        symbol,
                        interval,
                        data => OnKlineUpdate(data.Data, message.SecurityId, timeFrame),
                        CancellationToken.None);

                    if (result.Success)
                    {
                        subscription = result.Data;
                        this.AddInfoLog("Subscribed to klines for {0} at {1}", symbol, interval);
                    }
                    else
                    {
                        this.AddErrorLog("Failed to subscribe to klines for {0}: {1}", symbol, result.Error?.Message);
                    }
                }

                if (subscription != null)
                {
                    var subscriptionKey = $"{symbol}_{message.DataType}";
                    _marketDataSubscriptions.TryAdd(subscriptionKey, subscription);

                    TrackSubscription(message.SecurityId, message.DataType, subscription);

                    SendOutMessage(new MarketDataMessage
                    {
                        OriginalTransactionId = message.TransactionId,
                        IsSubscribe = true
                    });
                }
                else
                {
                    SendOutMessage(new MarketDataMessage
                    {
                        OriginalTransactionId = message.TransactionId,
                        Error = new InvalidOperationException($"Unsupported data type: {message.DataType}")
                    });
                }
            }
            catch (Exception ex)
            {
                SendOutMessage(new MarketDataMessage
                {
                    OriginalTransactionId = message.TransactionId,
                    Error = ex
                });
            }
        });
    }

    /// <summary>
    /// Unsubscribe from market data.
    /// </summary>
    /// <param name="message">Market data message</param>
    /// <param name="symbol">Symbol to unsubscribe</param>
    private void UnsubscribeFromMarketData(MarketDataMessage message, string symbol)
    {
        try
        {
            var subscriptionKey = $"{symbol}_{message.DataType}";

            if (_marketDataSubscriptions.TryRemove(subscriptionKey, out var subscription))
            {
                Task.Run(async () =>
                {
                    await subscription.CloseAsync();
                    this.AddInfoLog("Unsubscribed from {0} for {1}", message.DataType, symbol);
                });

                RemoveSubscriptionTracking(message.SecurityId);
            }

            SendOutMessage(new MarketDataMessage
            {
                OriginalTransactionId = message.TransactionId,
                IsSubscribe = false
            });
        }
        catch (Exception ex)
        {
            SendOutMessage(new MarketDataMessage
            {
                OriginalTransactionId = message.TransactionId,
                Error = ex
            });
        }
    }

    /// <inheritdoc />
    protected override void UnsubscribeInternal(SecurityId securityId)
    {
        var symbol = securityId.SecurityCode;
        var subscriptionsToRemove = _marketDataSubscriptions
            .Where(kvp => kvp.Key.StartsWith($"{symbol}_"))
            .ToArray();

        foreach (var kvp in subscriptionsToRemove)
        {
            Task.Run(async () =>
            {
                try
                {
                    await kvp.Value.CloseAsync();
                    _marketDataSubscriptions.TryRemove(kvp.Key, out _);
                    this.AddInfoLog("Closed subscription {0}", kvp.Key);
                }
                catch (Exception ex)
                {
                    this.AddErrorLog("Error closing subscription {0}: {1}", kvp.Key, ex.Message);
                }
            });
        }
    }

    /// <summary>
    /// Handle trade update from WebSocket.
    /// </summary>
    /// <param name="data">Trade data</param>
    /// <param name="securityId">Security identifier</param>
    private void OnTradeUpdate(BinanceStreamTrade data, SecurityId securityId)
    {
        try
        {
            var message = ConvertToStockSharpMessage(data, securityId);
            SendOutMessage(message);
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing trade update for {0}: {1}", securityId.SecurityCode, ex.Message);
        }
    }

    /// <summary>
    /// Handle order book update from WebSocket.
    /// </summary>
    /// <param name="data">Order book data</param>
    /// <param name="securityId">Security identifier</param>
    private void OnOrderBookUpdate(BinanceOrderBook data, SecurityId securityId)
    {
        try
        {
            var message = ConvertToStockSharpMessage(data, securityId);
            SendOutMessage(message);
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing order book update for {0}: {1}", securityId.SecurityCode, ex.Message);
        }
    }

    /// <summary>
    /// Handle kline update from WebSocket.
    /// </summary>
    /// <param name="data">Kline data</param>
    /// <param name="securityId">Security identifier</param>
    /// <param name="timeFrame">Time frame</param>
    private void OnKlineUpdate(BinanceStreamKlineData data, SecurityId securityId, TimeSpan timeFrame)
    {
        try
        {
            if (data.Final) // Only process completed candles
            {
                var candleMessage = new CandleMessage
                {
                    SecurityId = securityId,
                    OpenTime = data.OpenTime,
                    CloseTime = data.CloseTime,
                    OpenPrice = data.OpenPrice,
                    HighPrice = data.HighPrice,
                    LowPrice = data.LowPrice,
                    ClosePrice = data.ClosePrice,
                    TotalVolume = data.Volume,
                    TimeFrame = timeFrame,
                    ServerTime = data.CloseTime,
                    LocalTime = DateTimeOffset.Now
                };

                SendOutMessage(candleMessage);
            }
        }
        catch (Exception ex)
        {
            this.AddErrorLog("Error processing kline update for {0}: {1}", securityId.SecurityCode, ex.Message);
        }
    }

    /// <summary>
    /// Convert Binance symbol to StockSharp SecurityMessage.
    /// </summary>
    /// <param name="symbol">Binance symbol</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>Security message</returns>
    private SecurityMessage ConvertToSecurityMessage(BinanceSymbol symbol, SecurityId securityId)
    {
        // Extract price and lot size filters
        var priceFilter = symbol.PriceFilter;
        var lotSizeFilter = symbol.LotSizeFilter;
        var minNotionalFilter = symbol.MinNotionalFilter;

        return new SecurityMessage
        {
            SecurityId = securityId,
            Name = $"{symbol.BaseAsset}/{symbol.QuoteAsset}",
            ShortName = symbol.Name,
            SecurityType = SecurityTypes.Stock,
            PriceStep = priceFilter?.TickSize ?? 0.00000001m,
            VolumeStep = lotSizeFilter?.StepSize ?? 0.00000001m,
            MinVolume = lotSizeFilter?.MinQuantity ?? 0.00000001m,
            MaxVolume = lotSizeFilter?.MaxQuantity ?? 100000000m,
            Multiplier = 1m,
            Currency = DetermineCurrency(symbol.QuoteAsset),
            State = symbol.Status == SymbolStatus.Trading ? SecurityStates.Trading : SecurityStates.Stoped,
            LocalTime = DateTimeOffset.Now
        };
    }

    /// <summary>
    /// Determine currency type from quote asset.
    /// </summary>
    /// <param name="quoteAsset">Quote asset symbol</param>
    /// <returns>Currency type</returns>
    private static CurrencyTypes DetermineCurrency(string quoteAsset)
    {
        return quoteAsset?.ToUpperInvariant() switch
        {
            "USDT" or "USDC" or "BUSD" or "TUSD" => CurrencyTypes.USD,
            "BTC" => CurrencyTypes.BTC,
            "ETH" => CurrencyTypes.ETH,
            "EUR" or "EURS" => CurrencyTypes.EUR,
            "GBP" => CurrencyTypes.GBP,
            _ => CurrencyTypes.USD
        };
    }
}