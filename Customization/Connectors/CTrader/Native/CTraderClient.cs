namespace StockSharp.Customization.CTrader.Native;

using System.Reactive.Linq;
using OpenAPI.Net;
using OpenAPI.Net.Helpers;
using Google.Protobuf;

/// <summary>
/// cTrader OpenAPI client wrapper for StockSharp integration.
/// </summary>
class CTraderClient : BaseLogReceiver
{
	private readonly string _applicationId;
	private readonly string _applicationSecret;
	private readonly string _host;
	private readonly int _port;

	private OpenClient _client;
	private bool _isAuthenticated;
	private TaskCompletionSource<ProtoOAApplicationAuthRes> _appAuthTaskSource;
	private TaskCompletionSource<ProtoOAAccountAuthRes> _accountAuthTaskSource;
	private TaskCompletionSource<ProtoOASymbolsListRes> _symbolsTaskSource;
	private TaskCompletionSource<ProtoOATraderRes> _traderTaskSource;
	private TaskCompletionSource<ProtoOAReconcileRes> _reconcileTaskSource;
	private TaskCompletionSource<ProtoOAGetAccountListByAccessTokenRes> _accountListTaskSource;
	private TaskCompletionSource<ProtoOARefreshTokenRes> _refreshTokenTaskSource;

	public event Action<ConnectionStates> StateChanged;
	public event Action<Exception> Error;

	// Market data events
	public event Action<ProtoOASpotEvent> NewSpot;
	public event Action<ProtoOADepthEvent> NewDepth;
	public event Action<ProtoOAGetTrendbarsRes> NewCandle;

	// Transaction events
	public event Action<ProtoOAExecutionEvent> ExecutionEvent;
	public event Action<ProtoOAOrderErrorEvent> OrderError;

	// Account events
	public event Action<ProtoOAGetAccountListByAccessTokenRes> AccountsReceived;
	public event Action<ProtoOASymbolsListRes> SymbolsReceived;

	// Token refresh event
	public event Action<ProtoOARefreshTokenRes> TokenRefreshed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CTraderClient"/>.
	/// </summary>
	public CTraderClient(string applicationId, string applicationSecret, string host, int port)
	{
		_applicationId = applicationId ?? throw new ArgumentNullException(nameof(applicationId));
		_applicationSecret = applicationSecret ?? throw new ArgumentNullException(nameof(applicationSecret));
		_host = host ?? throw new ArgumentNullException(nameof(host));
		_port = port;
	}

	/// <summary>
	/// Connects to cTrader server.
	/// </summary>
	public async ValueTask Connect(CancellationToken cancellationToken, bool useWebSocket = false)
	{
		try
		{
			this.AddInfoLog("Connecting to cTrader OpenAPI at {0}:{1}", _host, _port);

			_client = new OpenClient(_host, _port, TimeSpan.FromSeconds(30), useWebSocket: useWebSocket);

			// Subscribe to all messages with unified error handler (exclude heartbeat for cleaner logging)
			_client.Where(iMessage => iMessage is not ProtoHeartbeatEvent).Subscribe(OnMessageReceived, OnClientError);

			// Subscribe to specific message types for routing
			_client.OfType<ProtoOAApplicationAuthRes>().Subscribe(OnAppAuthResponse);
			_client.OfType<ProtoOAAccountAuthRes>().Subscribe(OnAccountAuthResponse);
			_client.OfType<ProtoOASpotEvent>().Subscribe(OnSpotEvent);
			_client.OfType<ProtoOADepthEvent>().Subscribe(OnDepthQuotes);
			_client.OfType<ProtoOAGetTrendbarsRes>().Subscribe(OnTrendbar);
			_client.OfType<ProtoOAExecutionEvent>().Subscribe(OnExecution);
			_client.OfType<ProtoOAOrderErrorEvent>().Subscribe(OnOrderErrorEvent);
			_client.OfType<ProtoOAGetAccountListByAccessTokenRes>().Subscribe(OnAccounts);
			_client.OfType<ProtoOASymbolsListRes>().Subscribe(OnSymbolsList);
			_client.OfType<ProtoOATraderRes>().Subscribe(OnTrader);
			_client.OfType<ProtoOAReconcileRes>().Subscribe(OnReconcile);
			_client.OfType<ProtoOARefreshTokenRes>().Subscribe(OnRefreshTokenResponse);

			await _client.Connect();

			this.AddInfoLog("[CTraderClient.Connect] Connected to cTrader OpenAPI.");
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Failed to connect: {0}", ex);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Authenticates the application.
	/// </summary>
	public async ValueTask AuthenticateAsync(CancellationToken cancellationToken)
	{
		try
		{
			this.AddInfoLog("Authenticating application {0}", _applicationId);

			_appAuthTaskSource = new TaskCompletionSource<ProtoOAApplicationAuthRes>();

			var authReq = new ProtoOAApplicationAuthReq
			{
				ClientId = _applicationId,
				ClientSecret = _applicationSecret
			};

			await _client.SendMessage(authReq);
			this.AddInfoLog("Application authentication request sent");

			// Wait for response
			var result = await _appAuthTaskSource.Task;
			_isAuthenticated = true;
			this.AddInfoLog($"Application authenticated: {result.ToJson()}");
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Authentication failed: {0}", ex);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Authorizes a trading account using OAuth2 access token.
	/// </summary>
	public async ValueTask AuthorizeAccountAsync(long accountId, string accessToken, CancellationToken cancellationToken)
	{
		try
		{
			this.AddInfoLog("Authorizing account {0}", accountId);

			_accountAuthTaskSource = new TaskCompletionSource<ProtoOAAccountAuthRes>();

			var authReq = new ProtoOAAccountAuthReq
			{
				CtidTraderAccountId = accountId,
				AccessToken = accessToken
			};

			await _client.SendMessage(authReq);
			this.AddInfoLog("Account authorization request sent for account {0}", accountId);

			// Wait for response
			await _accountAuthTaskSource.Task;
			this.AddInfoLog("Account {0} authorized. Change status to Connected.", accountId);
			
			StateChanged?.Invoke(ConnectionStates.Connected);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Account authorization failed: {0}", ex);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Gets list of symbols for an account.
	/// </summary>
	public async ValueTask<ProtoOASymbolsListRes> GetSymbolsAsync(long accountId, CancellationToken cancellationToken)
	{
		_symbolsTaskSource = new TaskCompletionSource<ProtoOASymbolsListRes>();

		var req = new ProtoOASymbolsListReq
		{
			CtidTraderAccountId = accountId
		};
		await _client.SendMessage(req);
		this.AddDebugLog("Symbols list request sent for account {0}", accountId);

		// Wait for response via event handler
		return await _symbolsTaskSource.Task;
	}

	/// <summary>
	/// Gets trader/account data including balance, equity, margin.
	/// </summary>
	public async ValueTask<ProtoOATraderRes> GetTraderAsync(long accountId, CancellationToken cancellationToken)
	{
		_traderTaskSource = new TaskCompletionSource<ProtoOATraderRes>();

		var req = new ProtoOATraderReq
		{
			CtidTraderAccountId = accountId
		};
		await _client.SendMessage(req);
		this.AddDebugLog("Trader data request sent for account {0}", accountId);

		// Wait for response via event handler
		return await _traderTaskSource.Task;
	}

	/// <summary>
	/// Reconciles account state - gets all active orders and positions.
	/// </summary>
	public async ValueTask<ProtoOAReconcileRes> ReconcileAsync(long accountId, CancellationToken cancellationToken)
	{
		_reconcileTaskSource = new TaskCompletionSource<ProtoOAReconcileRes>();

		var req = new ProtoOAReconcileReq
		{
			CtidTraderAccountId = accountId
		};
		await _client.SendMessage(req);
		this.AddDebugLog("Reconcile request sent for account {0}", accountId);

		// Wait for response via event handler
		return await _reconcileTaskSource.Task;
	}

	/// <summary>
	/// Gets list of trading accounts associated with the access token.
	/// </summary>
	public async ValueTask<ProtoOAGetAccountListByAccessTokenRes> GetAccountListAsync(string accessToken, CancellationToken cancellationToken)
	{
		_accountListTaskSource = new TaskCompletionSource<ProtoOAGetAccountListByAccessTokenRes>();

		var req = new ProtoOAGetAccountListByAccessTokenReq
		{
			AccessToken = accessToken
		};
		await _client.SendMessage(req);
		this.AddDebugLog("Account list request sent");

		// Wait for response via event handler
		return await _accountListTaskSource.Task;
	}

	/// <summary>
	/// Refreshes the OAuth2 access token using refresh token.
	/// </summary>
	public async ValueTask<ProtoOARefreshTokenRes> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
	{
		_refreshTokenTaskSource = new TaskCompletionSource<ProtoOARefreshTokenRes>();

		var req = new ProtoOARefreshTokenReq
		{
			RefreshToken = refreshToken
		};
		await _client.SendMessage(req);
		this.AddInfoLog("Refresh token request sent");

		// Wait for response via event handler
		return await _refreshTokenTaskSource.Task;
	}

	/// <summary>
	/// Subscribes to spot events (ticks) for a symbol.
	/// </summary>
	public async ValueTask SubscribeSpots(long accountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken)
	{
		var req = new ProtoOASubscribeSpotsReq
		{
			CtidTraderAccountId = accountId
		};
		req.SymbolId.AddRange(symbolIds);

		await _client.SendMessage(req);
		this.AddDebugLog("Subscribed to spots for {0} symbols", symbolIds.Count());
	}

	/// <summary>
	/// Unsubscribes from spot events.
	/// </summary>
	public async ValueTask UnsubscribeSpots(long accountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken)
	{
		var req = new ProtoOAUnsubscribeSpotsReq
		{
			CtidTraderAccountId = accountId
		};
		req.SymbolId.AddRange(symbolIds);

		await _client.SendMessage(req);
		this.AddDebugLog("Unsubscribed from spots for {0} symbols", symbolIds.Count());
	}

	/// <summary>
	/// Subscribes to market depth.
	/// </summary>
	public async ValueTask SubscribeDepth(long accountId, long symbolId, CancellationToken cancellationToken)
	{
		var req = new ProtoOASubscribeDepthQuotesReq
		{
			CtidTraderAccountId = accountId
		};
		req.SymbolId.Add(symbolId);

		await _client.SendMessage(req);
		this.AddDebugLog("Subscribed to depth for symbol {0}", symbolId);
	}

	/// <summary>
	/// Unsubscribes from market depth.
	/// </summary>
	public async ValueTask UnsubscribeDepth(long accountId, long symbolId, CancellationToken cancellationToken)
	{
		var req = new ProtoOAUnsubscribeDepthQuotesReq
		{
			CtidTraderAccountId = accountId
		};
		req.SymbolId.Add(symbolId);

		await _client.SendMessage(req);
		this.AddDebugLog("Unsubscribed from depth for symbol {0}", symbolId);
	}

	/// <summary>
	/// Registers a new order.
	/// </summary>
	public async ValueTask NewOrderAsync(
		long accountId,
		long symbolId,
		ProtoOATradeSide tradeSide,
		long volume,
		ProtoOAOrderType orderType,
		string clientOrderId = null,
		double? limitPrice = null,
		double? stopPrice = null,
		double? stopLoss = null,
		double? takeProfit = null,
		bool? trailingStopLoss = null,
		CancellationToken cancellationToken = default)
	{
		var req = new ProtoOANewOrderReq
		{
			CtidTraderAccountId = accountId,
			SymbolId = symbolId,
			OrderType = orderType,
			TradeSide = tradeSide,
			Volume = volume
		};

		if (!string.IsNullOrEmpty(clientOrderId))
			req.ClientOrderId = clientOrderId;

		if (limitPrice.HasValue)
			req.LimitPrice = limitPrice.Value;

		if (stopPrice.HasValue)
			req.StopPrice = stopPrice.Value;

		if (stopLoss.HasValue)
			req.StopLoss = stopLoss.Value;

		if (takeProfit.HasValue)
			req.TakeProfit = takeProfit.Value;

		if (trailingStopLoss.HasValue)
			req.TrailingStopLoss = trailingStopLoss.Value;

		await _client.SendMessage(req);
		this.AddDebugLog("New order request sent for symbol {0}, ClientOrderId={1}", symbolId, clientOrderId);
	}

	/// <summary>
	/// Cancels an existing order.
	/// </summary>
	public async ValueTask CancelOrderAsync(
		long accountId,
		long orderId,
		CancellationToken cancellationToken)
	{
		var req = new ProtoOACancelOrderReq
		{
			CtidTraderAccountId = accountId,
			OrderId = orderId
		};

		await _client.SendMessage(req);
		this.AddDebugLog("Cancel order request sent for order {0}", orderId);
	}

	/// <summary>
	/// Disconnects from cTrader server.
	/// </summary>
	public void Disconnect()
	{
		this.AddInfoLog("Disconnecting from cTrader");

		if (_client != null)
		{
			_client.Dispose();
			_client = null;
		}

		_isAuthenticated = false;
		StateChanged?.Invoke(ConnectionStates.Disconnected);
	}

	// Event handlers
	private void OnMessageReceived(IMessage message)
	{
		this.AddDebugLog("Message received: {0}", message.GetType().Name);
	}

	private void OnClientError(Exception ex)
	{
		this.AddErrorLog("OpenAPI client error: {0}", ex);
		Error?.Invoke(ex);
	}

	private void OnSpotEvent(ProtoOASpotEvent spotEvent)
	{
		NewSpot?.Invoke(spotEvent);
	}

	private void OnDepthQuotes(ProtoOADepthEvent depthQuotes)
	{
		NewDepth?.Invoke(depthQuotes);
	}

	private void OnTrendbar(ProtoOAGetTrendbarsRes trendbar)
	{
		NewCandle?.Invoke(trendbar);
	}

	private void OnExecution(ProtoOAExecutionEvent executionEvent)
	{
		ExecutionEvent?.Invoke(executionEvent);
	}

	private void OnOrderErrorEvent(ProtoOAOrderErrorEvent orderError)
	{
		OrderError?.Invoke(orderError);
	}

	private void OnAccounts(ProtoOAGetAccountListByAccessTokenRes accounts)
	{
		this.AddInfoLog("Received account list with {0} accounts", accounts.CtidTraderAccount.Count);
		_accountListTaskSource?.TrySetResult(accounts);
		AccountsReceived?.Invoke(accounts);
	}

	private void OnAppAuthResponse(ProtoOAApplicationAuthRes response)
	{
		this.AddInfoLog("Application authenticated successfully");
		_appAuthTaskSource?.TrySetResult(response);
	}

	private void OnAccountAuthResponse(ProtoOAAccountAuthRes response)
	{
		this.AddInfoLog("Account {0} authorized successfully", response.CtidTraderAccountId);
		_accountAuthTaskSource?.TrySetResult(response);
	}

	private void OnSymbolsList(ProtoOASymbolsListRes symbols)
	{
		LogInfo($"OnSymbolsList: {symbols.Symbol.Count.ToString()}. TaskSource: {_symbolsTaskSource != null}");
		_symbolsTaskSource?.TrySetResult(symbols);
		SymbolsReceived?.Invoke(symbols);
	}

	private void OnTrader(ProtoOATraderRes trader)
	{
		_traderTaskSource?.TrySetResult(trader);
	}

	private void OnReconcile(ProtoOAReconcileRes reconcile)
	{
		_reconcileTaskSource?.TrySetResult(reconcile);
	}

	private void OnRefreshTokenResponse(ProtoOARefreshTokenRes response)
	{
		this.AddInfoLog("Token refreshed successfully. New AccessToken received, ExpiresIn: {0}", response.ExpiresIn);
		_refreshTokenTaskSource?.TrySetResult(response);
		TokenRefreshed?.Invoke(response);
	}

	protected override void DisposeManaged()
	{
		Disconnect();
		base.DisposeManaged();
	}

	// to get readable name after obfuscation
	public override string Name => nameof(CTrader) + "_" + nameof(CTraderClient);
}
