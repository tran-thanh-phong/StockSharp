using StockSharp.Configuration;

namespace StockSharp.Customization.CTrader;

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
	private long _accountId;

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
	public override bool IsAllDownloadingSupported(StockSharp.Messages.DataType dataType)
		=> dataType == StockSharp.Messages.DataType.Securities || base.IsAllDownloadingSupported(dataType);
	
	/// <inheritdoc />
	public override string[] AssociatedBoards { get; } = new[] { Native.Extensions.BoardCode };

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
		this.AddInfoLog("Connecting to cTrader");

		// Required App Key & Secret to connect to get public data
		if (Key.IsEmpty())
			throw new InvalidOperationException(LocalizedStrings.KeyNotSpecified);

		if (Secret.IsEmpty())
			throw new InvalidOperationException(LocalizedStrings.SecretNotSpecified);

		if (this.IsTransactional())
		{
			if (Token.IsEmpty())
				throw new InvalidOperationException(LocalizedStrings.AccessToken);
		}

		if (_client != null)
			throw new InvalidOperationException(LocalizedStrings.NotDisconnectPrevTime);

		// Get host and port from environment using ApiInfo helper
		var mode = IsDemo ? OpenAPI.Net.Helpers.Mode.Demo : OpenAPI.Net.Helpers.Mode.Live;
		var host = OpenAPI.Net.Helpers.ApiInfo.GetHost(mode);
		var port = OpenAPI.Net.Helpers.ApiInfo.Port;

		var secret = Secret?.UnSecure() ?? string.Empty;
		var keyId = Key?.UnSecure() ?? string.Empty;
		_client = new CTraderClient(keyId, secret, host, port) { Parent = this };

		SubscribeClient();

		this.AddInfoLog("Connecting to cTrader {0} at {1}:{2}", IsDemo ? "Demo" : "Live", host, port);
		await _client.Connect(cancellationToken);

		// Authenticate application
		if (!Key.IsEmpty() && !secret.IsEmpty())
		{
			await _client.AuthenticateAsync(cancellationToken);
		}

		// Get account list and authorize account
		if (!Token.IsEmpty())
		{
			var accessToken = Token.UnSecure();

			// Fetch account list and use the first one (AccountId will be stored locally)
			this.AddInfoLog("Fetching account list...");
			var accountsRes = await _client.GetAccountListAsync(accessToken, cancellationToken);

			if (accountsRes.CtidTraderAccount.Count == 0)
				throw new InvalidOperationException("No trading accounts found for the provided access token");

			_accountId = (long)accountsRes.CtidTraderAccount[0].CtidTraderAccountId;
			this.AddInfoLog("Using first account from list: AccountId={0}", _accountId);

			// Authorize the account
			await _client.AuthorizeAccountAsync(_accountId, accessToken, cancellationToken);
			this.AddInfoLog("Account {0} authorized with access token", _accountId);
		}

		this.AddInfoLog("Connected to cTrader");
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
			_accountId = 0;
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

		// Token refresh event
		_client.TokenRefreshed += OnTokenRefreshed;
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

		// Token refresh event
		_client.TokenRefreshed -= OnTokenRefreshed;
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
		try
		{
			this.AddDebugLog("Execution event: Type={0}, HasOrder={1}, HasDeal={2}, HasPosition={3}",
				execution.ExecutionType, execution.Order != null, execution.Deal != null, execution.Position != null);

			// Handle order-related events
			if (execution.Order != null)
			{
				ProcessOrderExecution(execution);
			}

			// Handle trade/fill events
			if (execution.Deal != null)
			{
				ProcessDealExecution(execution);
			}
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Error processing execution event: {0}", ex);
		}
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

	private void OnTokenRefreshed(ProtoOARefreshTokenRes response)
	{
		// Update stored tokens when refresh occurs
		Token = response.AccessToken.Secure();
		if (!string.IsNullOrEmpty(response.RefreshToken))
			RefreshToken = response.RefreshToken.Secure();

		this.AddInfoLog("Access token refreshed. New token expires at: {0}",
			DateTimeOffset.FromUnixTimeMilliseconds(response.ExpiresIn));
		this.AddWarningLog("Token refreshed - you may need to re-authorize trading accounts");
	}
}
