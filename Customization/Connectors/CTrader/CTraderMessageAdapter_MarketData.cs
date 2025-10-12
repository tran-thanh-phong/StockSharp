namespace StockSharp.Customization.CTrader;

partial class CTraderMessageAdapter
{
	/// <inheritdoc />
	public override async ValueTask SecurityLookupAsync(SecurityLookupMessage lookupMsg, CancellationToken cancellationToken)
	{
		var secTypes = lookupMsg.GetSecurityTypes();
		var left = lookupMsg.Count ?? long.MaxValue;

		if (_client == null)
		{
			this.AddErrorLog("SecurityLookup requires connection. Please connect first.");
			SendSubscriptionFinished(lookupMsg.TransactionId);
			return;
		}

		try
		{
			this.AddInfoLog("Security lookup for account {0}", _accountId);

			var symbolsRes = await _client.GetSymbolsAsync(_accountId, cancellationToken);

			foreach (var symbol in symbolsRes.Symbol)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// Cache symbol mapping
				_symbolIdToCode[symbol.SymbolId] = symbol.SymbolName;
				_symbolCodeToId[symbol.SymbolName] = symbol.SymbolId;

				var secMsg = new SecurityMessage
				{
					SecurityId = symbol.SymbolName.ToStockSharp(),
					SecurityType = SecurityTypes.CryptoCurrency, // cTrader is primarily FX/CFD
					MinVolume = 0.01m, // Default - ProtoOALightSymbol doesn't include this
					VolumeStep = 0.01m, // Default - ProtoOALightSymbol doesn't include this
					Decimals = 5, // Default - ProtoOALightSymbol doesn't include this
					Name = symbol.HasDescription ? symbol.Description : symbol.SymbolName,
					OriginalTransactionId = lookupMsg.TransactionId,
				};

				if (!secMsg.IsMatch(lookupMsg, secTypes))
					continue;

				SendOutMessage(secMsg);

				if (--left <= 0)
					break;
			}

			this.AddInfoLog("Loaded {0} symbols", symbolsRes.Symbol.Count);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Security lookup failed: {0}", ex);
		}

		SendSubscriptionFinished(lookupMsg.TransactionId);
	}

	/// <inheritdoc />
	protected override async ValueTask OnTFCandlesSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		SendSubscriptionReply(mdMsg.TransactionId);

		var symbolCode = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			// TODO: Implement candle subscription
			// Historical: ProtoOAGetTrendbarsReq
			// Real-time: Will need to build from ticks
			this.AddInfoLog("Candle subscription for {0} - implementation pending", symbolCode);

			SendSubscriptionResult(mdMsg);
		}
		else
		{
			// Unsubscribe from candles
			this.AddInfoLog("Candle unsubscription for {0}", symbolCode);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnMarketDepthSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		SendSubscriptionReply(mdMsg.TransactionId);

		var symbolCode = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			if (!mdMsg.IsHistoryOnly())
			{
				if (_symbolCodeToId.TryGetValue(symbolCode, out var symbolId))
				{
					await _client.SubscribeDepth(_accountId, symbolId, cancellationToken);
					this.AddInfoLog("Subscribed to market depth for {0} (ID: {1})", symbolCode, symbolId);
				}
				else
				{
					this.AddWarningLog("Symbol {0} not found in cache", symbolCode);
				}
			}

			SendSubscriptionResult(mdMsg);
		}
		else
		{
			if (_symbolCodeToId.TryGetValue(symbolCode, out var symbolId))
			{
				await _client.UnsubscribeDepth(_accountId, symbolId, cancellationToken);
				this.AddInfoLog("Unsubscribed from market depth for {0}", symbolCode);
			}
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnTicksSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		SendSubscriptionReply(mdMsg.TransactionId);

		var symbolCode = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			if (!mdMsg.IsHistoryOnly())
			{
				if (_symbolCodeToId.TryGetValue(symbolCode, out var symbolId))
				{
					await _client.SubscribeSpots(_accountId, new[] { symbolId }, cancellationToken);
					this.AddInfoLog("Subscribed to ticks for {0} (ID: {1})", symbolCode, symbolId);
				}
				else
				{
					this.AddWarningLog("Symbol {0} not found in cache", symbolCode);
				}
			}

			SendSubscriptionResult(mdMsg);
		}
		else
		{
			if (_symbolCodeToId.TryGetValue(symbolCode, out var symbolId))
			{
				await _client.UnsubscribeSpots(_accountId, new[] { symbolId }, cancellationToken);
				this.AddInfoLog("Unsubscribed from ticks for {0}", symbolCode);
			}
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnLevel1SubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		SendSubscriptionReply(mdMsg.TransactionId);

		var symbolCode = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			if (!mdMsg.IsHistoryOnly())
			{
				// TODO: Level1 will be aggregated from ticks and depth
				this.AddInfoLog("Level1 subscription for {0} - implementation pending", symbolCode);
			}

			SendSubscriptionResult(mdMsg);
		}
		else
		{
			this.AddInfoLog("Level1 unsubscription for {0}", symbolCode);
		}
	}
}
