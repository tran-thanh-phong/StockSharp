namespace StockSharp.CTraderConnector;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// cTrader message adapter for StockSharp integration.
/// Supports real-time market data, order execution, and portfolio management.
/// </summary>
[OrderCondition(typeof(CTraderOrderCondition))]
[Display(
	Name = "cTrader",
	Description = "cTrader trading platform connector for Forex and CFD trading",
	GroupName = "FX")]
[MessageAdapterCategory(
	MessageAdapterCategories.FX |
	MessageAdapterCategories.RealTime |
	MessageAdapterCategories.Ticks |
	MessageAdapterCategories.MarketDepth |
	MessageAdapterCategories.Level1 |
	MessageAdapterCategories.Transactions |
	MessageAdapterCategories.Candles)]
public partial class CTraderMessageAdapter : AsyncMessageAdapter
{
	private CTraderClient _client;
	private readonly Dictionary<long, string> _symbolIdToCode = new();
	private readonly Dictionary<string, long> _symbolCodeToId = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="CTraderMessageAdapter"/>.
	/// </summary>
	/// <param name="transactionIdGenerator">Transaction id generator.</param>
	public CTraderMessageAdapter(IdGenerator transactionIdGenerator)
		: base(transactionIdGenerator)
	{
		this.AddMarketDataSupport();
		this.AddTransactionalSupport();

		HeartbeatInterval = DefaultHeartbeatInterval;

		this.AddSupportedMarketDataType(Messages.DataType.Ticks);
		this.AddSupportedMarketDataType(Messages.DataType.MarketDepth);
		this.AddSupportedMarketDataType(Messages.DataType.Level1);
		this.AddSupportedCandleTimeFrames(AllTimeFrames);

		this.RemoveSupportedMessage(MessageTypes.OrderReplace);
	}

	/// <inheritdoc />
	public override string[] AssociatedBoards { get; } = new[] { BoardCodes.CTrader };

	/// <summary>
	/// Possible time-frames.
	/// </summary>
	public static IEnumerable<TimeSpan> AllTimeFrames { get; } = new[]
	{
		TimeSpan.FromMinutes(1),
		TimeSpan.FromMinutes(5),
		TimeSpan.FromMinutes(15),
		TimeSpan.FromMinutes(30),
		TimeSpan.FromHours(1),
		TimeSpan.FromHours(4),
		TimeSpan.FromDays(1),
	};

	/// <inheritdoc />
	public override async ValueTask ConnectAsync(ConnectMessage connectMsg, CancellationToken cancellationToken)
	{
		if (this.IsTransactional())
		{
			if (ApplicationId.IsEmpty())
				throw new InvalidOperationException(LocalizedStrings.KeyNotSpecified);

			if (ApplicationSecret.IsEmpty())
				throw new InvalidOperationException(LocalizedStrings.SecretNotSpecified);
		}

		if (_client != null)
			throw new InvalidOperationException(LocalizedStrings.NotDisconnectPrevTime);

		var secret = ApplicationSecret?.UnSecure() ?? string.Empty;
		_client = new CTraderClient(ApplicationId, secret, Host, Port) { Parent = this };

		SubscribeClient();

		this.AddInfoLog("Connecting to cTrader {0} at {1}:{2}", Environment, Host, Port);
		await _client.Connect(cancellationToken);

		// Authenticate application
		if (!ApplicationId.IsEmpty() && !secret.IsEmpty())
		{
			await _client.AuthenticateAsync(cancellationToken);
		}
	}

	/// <inheritdoc />
	public override ValueTask DisconnectAsync(DisconnectMessage disconnectMsg, CancellationToken cancellationToken)
	{
		if (_client == null)
			throw new InvalidOperationException(LocalizedStrings.ConnectionNotOk);

		this.AddInfoLog("Disconnecting from cTrader");

		_client.Disconnect();

		return default;
	}

	/// <inheritdoc />
	public override ValueTask ResetAsync(ResetMessage resetMsg, CancellationToken cancellationToken)
	{
		if (_client != null)
		{
			try
			{
				UnsubscribeClient();
				_client.Disconnect();
			}
			catch (Exception ex)
			{
				SendOutError(ex);
			}

			_client = null;
		}

		SendOutMessage(new ResetMessage());

		return default;
	}

	/// <inheritdoc />
	public override async ValueTask TimeAsync(TimeMessage timeMsg, CancellationToken cancellationToken)
	{
		// Heartbeat and periodic checks will be implemented in Phase 7
		await Task.CompletedTask;
	}

	private void SubscribeClient()
	{
		_client.StateChanged += SendOutConnectionState;
		_client.Error += SendOutError;

		// Market data events
		_client.NewSpot += OnNewSpot;
		_client.NewDepth += OnNewDepth;
		_client.NewCandle += OnNewCandle;

		// Transaction events
		_client.ExecutionEvent += OnExecutionEvent;
		_client.OrderError += OnOrderError;

		// Account events
		_client.AccountsReceived += OnAccountsReceived;
		_client.SymbolsReceived += OnSymbolsReceived;
	}

	private void UnsubscribeClient()
	{
		_client.StateChanged -= SendOutConnectionState;
		_client.Error -= SendOutError;

		// Market data events
		_client.NewSpot -= OnNewSpot;
		_client.NewDepth -= OnNewDepth;
		_client.NewCandle -= OnNewCandle;

		// Transaction events
		_client.ExecutionEvent -= OnExecutionEvent;
		_client.OrderError -= OnOrderError;

		// Account events
		_client.AccountsReceived -= OnAccountsReceived;
		_client.SymbolsReceived -= OnSymbolsReceived;
	}

	// Event handlers
	private void OnNewSpot(ProtoOASpotEvent spot)
	{
		if (!_symbolIdToCode.TryGetValue(spot.SymbolId, out var symbolCode))
			return;

		var securityId = symbolCode.ToStockSharp();

		// Send bid/ask as Level1
		if (spot.HasBid && spot.HasAsk)
		{
			SendOutMessage(new Level1ChangeMessage
			{
				SecurityId = securityId,
				ServerTime = DateTimeOffset.FromUnixTimeMilliseconds((long)spot.Timestamp).UtcDateTime,
			}
			.TryAdd(Level1Fields.BestBidPrice, (decimal)spot.Bid)
			.TryAdd(Level1Fields.BestAskPrice, (decimal)spot.Ask));
		}

		// Send as tick if we have a last price
		if (spot.HasBid || spot.HasAsk)
		{
			var price = spot.HasBid ? spot.Bid : spot.Ask;
			var side = spot.HasBid ? Sides.Sell : Sides.Buy;

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = Messages.DataType.Ticks,
				SecurityId = securityId,
				TradePrice = (decimal)price,
				ServerTime = DateTimeOffset.FromUnixTimeMilliseconds((long)spot.Timestamp).UtcDateTime,
				OriginSide = side,
			});
		}
	}

	private void OnNewDepth(ProtoOADepthEvent depth)
	{
		if (!_symbolIdToCode.TryGetValue((long)depth.SymbolId, out var symbolCode))
			return;

		var bids = new List<QuoteChange>();
		var asks = new List<QuoteChange>();

		foreach (var quote in depth.NewQuotes)
		{
			if (quote.HasBid && quote.HasSize)
			{
				bids.Add(new QuoteChange((decimal)quote.Bid, quote.Size.ToStockSharpVolume()));
			}

			if (quote.HasAsk && quote.HasSize)
			{
				asks.Add(new QuoteChange((decimal)quote.Ask, quote.Size.ToStockSharpVolume()));
			}
		}

		SendOutMessage(new QuoteChangeMessage
		{
			SecurityId = symbolCode.ToStockSharp(),
			Bids = bids.ToArray(),
			Asks = asks.ToArray(),
			ServerTime = CurrentTime.ConvertToUtc(),
		});
	}

	private void OnNewCandle(ProtoOAGetTrendbarsRes candle)
	{
		// Candle handling will be implemented when needed
	}

	private void OnExecutionEvent(ProtoOAExecutionEvent execution)
	{
		// Will be implemented in transaction partial
	}

	private void OnOrderError(ProtoOAOrderErrorEvent error)
	{
		this.AddErrorLog("Order error: {0}", error.ErrorCode);
	}

	private void OnAccountsReceived(ProtoOAGetAccountListByAccessTokenRes accounts)
	{
		// Accounts handling if needed
	}

	private void OnSymbolsReceived(ProtoOASymbolsListRes symbols)
	{
		// Symbols are handled in SecurityLookupAsync
	}
}
