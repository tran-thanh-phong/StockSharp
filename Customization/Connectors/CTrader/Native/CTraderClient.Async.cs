using System.Collections.Concurrent;

namespace StockSharp.Customization.CTrader.Native;

using OpenAPI.Net;
using OpenAPI.Net.Helpers;

/// <summary>
/// Async wrapper methods for CTraderClient using TaskCompletionSource pattern.
/// Converts event-driven OpenAPI responses to async/await pattern.
/// </summary>
partial class CTraderClient
{
	// Concurrent dictionaries for tracking async operations by clientMsgId
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOAApplicationAuthRes>> _appAuthTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOAAccountAuthRes>> _accountAuthTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOASymbolsListRes>> _symbolsTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOATraderRes>> _traderTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOAReconcileRes>> _reconcileTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOAGetAccountListByAccessTokenRes>> _accountListTasks = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoOARefreshTokenRes>> _refreshTokenTasks = new();

	#region Event Handlers

	private void OnProtoMessageReceived(ProtoMessage protoMessage)
	{
		var clientMsgId = protoMessage.ClientMsgId;
		var message = MessageFactory.GetMessage(protoMessage);

		this.AddDebugLog("Message received: {0} with clientMsgId: {1}", message.GetType().Name, clientMsgId);

		// Route message to appropriate TaskCompletionSource based on clientMsgId
		switch (message)
		{
			case ProtoOAApplicationAuthRes appAuthRes:
				if (_appAuthTasks.TryRemove(clientMsgId, out var appAuthTcs))
				{
					this.AddInfoLog("Application authenticated successfully");
					appAuthTcs.TrySetResult(appAuthRes);
				}
				break;

			case ProtoOAAccountAuthRes accountAuthRes:
				if (_accountAuthTasks.TryRemove(clientMsgId, out var accountAuthTcs))
				{
					this.AddInfoLog("Account {0} authorized successfully", accountAuthRes.CtidTraderAccountId);
					accountAuthTcs.TrySetResult(accountAuthRes);
				}
				break;

			case ProtoOASymbolsListRes symbolsRes:
				if (_symbolsTasks.TryRemove(clientMsgId, out var symbolsTcs))
				{
					this.AddDebugLog("Received symbols list: {0} symbols", symbolsRes.Symbol.Count);
					symbolsTcs.TrySetResult(symbolsRes);
				}
				SymbolsReceived?.Invoke(symbolsRes);
				break;

			case ProtoOATraderRes traderRes:
				if (_traderTasks.TryRemove(clientMsgId, out var traderTcs))
				{
					this.AddDebugLog("Received trader data");
					traderTcs.TrySetResult(traderRes);
				}
				break;

			case ProtoOAReconcileRes reconcileRes:
				if (_reconcileTasks.TryRemove(clientMsgId, out var reconcileTcs))
				{
					this.AddDebugLog("Received reconcile data");
					reconcileTcs.TrySetResult(reconcileRes);
				}
				break;

			case ProtoOAGetAccountListByAccessTokenRes accountListRes:
				if (_accountListTasks.TryRemove(clientMsgId, out var accountListTcs))
				{
					this.AddInfoLog("Received account list with {0} accounts", accountListRes.CtidTraderAccount.Count);
					accountListTcs.TrySetResult(accountListRes);
				}

				AccountsReceived?.Invoke(accountListRes);
				break;

			case ProtoOARefreshTokenRes refreshTokenRes:
				if (_refreshTokenTasks.TryRemove(clientMsgId, out var refreshTokenTcs))
				{
					this.AddInfoLog("Token refreshed successfully, ExpiresIn: {0}", refreshTokenRes.ExpiresIn);
					refreshTokenTcs.TrySetResult(refreshTokenRes);
				}
				TokenRefreshed?.Invoke(refreshTokenRes);
				break;
		}
	}

	#endregion
	
	/// <summary>
	/// Authenticates the application.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the authentication operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask AuthenticateAsync(CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		this.AddInfoLog("Authenticating application {0}", _applicationId);

		var authReq = new ProtoOAApplicationAuthReq
		{
			ClientId = _applicationId,
			ClientSecret = _applicationSecret
		};

		var taskSource = new TaskCompletionSource<ProtoOAApplicationAuthRes>();
		_appAuthTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			// Register cancellation
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_appAuthTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(authReq, clientMsgId);
			this.AddDebugLog("Application authentication request sent with clientMsgId: {0}", clientMsgId);

			// Wait for response with timeout
			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				var result = await taskSource.Task;
				_isAuthenticated = true;
				this.AddDebugLog("Application authenticated successfully");
				return;
			}
			else
			{
				// Timeout occurred
				if (_appAuthTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Application authentication timed out after {timeout.Value.TotalSeconds} seconds"));
				}
				throw new TimeoutException($"Application authentication timed out after {timeout.Value.TotalSeconds} seconds");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Application authentication cancelled");
			_appAuthTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Authentication failed: {0}", ex);
			_appAuthTasks.TryRemove(clientMsgId, out var _);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Authorizes a trading account using OAuth2 access token.
	/// </summary>
	/// <param name="accountId">The trading account ID to authorize.</param>
	/// <param name="accessToken">OAuth2 access token.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the authorization operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask AuthorizeAccountAsync(long accountId, string accessToken, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		this.AddInfoLog("Authorizing account {0}", accountId);

		var authReq = new ProtoOAAccountAuthReq
		{
			CtidTraderAccountId = accountId,
			AccessToken = accessToken
		};

		var taskSource = new TaskCompletionSource<ProtoOAAccountAuthRes>();
		_accountAuthTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			// Register cancellation
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_accountAuthTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(authReq, clientMsgId);
			this.AddDebugLog("Account authorization request sent for account {0} with clientMsgId: {1}", accountId, clientMsgId);

			// Wait for response with timeout
			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				await taskSource.Task;
				this.AddDebugLog("Account {0} authorized successfully", accountId);
				StateChanged?.Invoke(ConnectionStates.Connected);
				return;
			}
			else
			{
				// Timeout occurred
				if (_accountAuthTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Account authorization timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}"));
				}
				throw new TimeoutException($"Account authorization timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Account authorization cancelled for account {0}", accountId);
			_accountAuthTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Account authorization failed for account {0}: {1}", accountId, ex);
			_accountAuthTasks.TryRemove(clientMsgId, out var _);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Gets list of symbols for an account.
	/// </summary>
	/// <param name="accountId">The trading account ID.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask<ProtoOASymbolsListRes> GetSymbolsAsync(long accountId, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		var req = new ProtoOASymbolsListReq
		{
			CtidTraderAccountId = accountId
		};

		var taskSource = new TaskCompletionSource<ProtoOASymbolsListRes>();
		_symbolsTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_symbolsTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(req, clientMsgId);
			this.AddDebugLog("Symbols list request sent for account {0} with clientMsgId: {1}", accountId, clientMsgId);

			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				return await taskSource.Task;
			}
			else
			{
				if (_symbolsTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Get symbols timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}"));
				}
				throw new TimeoutException($"Get symbols timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Get symbols cancelled for account {0}", accountId);
			_symbolsTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Get symbols failed for account {0}: {1}", accountId, ex);
			_symbolsTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
	}

	/// <summary>
	/// Gets trader/account data including balance, equity, margin.
	/// </summary>
	/// <param name="accountId">The trading account ID.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask<ProtoOATraderRes> GetTraderAsync(long accountId, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		var req = new ProtoOATraderReq
		{
			CtidTraderAccountId = accountId
		};

		var taskSource = new TaskCompletionSource<ProtoOATraderRes>();
		_traderTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_traderTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(req, clientMsgId);
			this.AddDebugLog("Trader data request sent for account {0} with clientMsgId: {1}", accountId, clientMsgId);

			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				return await taskSource.Task;
			}
			else
			{
				if (_traderTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Get trader data timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}"));
				}
				throw new TimeoutException($"Get trader data timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Get trader data cancelled for account {0}", accountId);
			_traderTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Get trader data failed for account {0}: {1}", accountId, ex);
			_traderTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
	}

	/// <summary>
	/// Reconciles account state - gets all active orders and positions.
	/// </summary>
	/// <param name="accountId">The trading account ID.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask<ProtoOAReconcileRes> ReconcileAsync(long accountId, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		var req = new ProtoOAReconcileReq
		{
			CtidTraderAccountId = accountId
		};

		var taskSource = new TaskCompletionSource<ProtoOAReconcileRes>();
		_reconcileTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_reconcileTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(req, clientMsgId);
			this.AddDebugLog("Reconcile request sent for account {0} with clientMsgId: {1}", accountId, clientMsgId);

			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				return await taskSource.Task;
			}
			else
			{
				if (_reconcileTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Reconcile timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}"));
				}
				throw new TimeoutException($"Reconcile timed out after {timeout.Value.TotalSeconds} seconds for account {accountId}");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Reconcile cancelled for account {0}", accountId);
			_reconcileTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Reconcile failed for account {0}: {1}", accountId, ex);
			_reconcileTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
	}

	/// <summary>
	/// Gets list of trading accounts associated with the access token.
	/// </summary>
	/// <param name="accessToken">OAuth2 access token.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask<ProtoOAGetAccountListByAccessTokenRes> GetAccountListAsync(string accessToken, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		var req = new ProtoOAGetAccountListByAccessTokenReq
		{
			AccessToken = accessToken
		};

		var taskSource = new TaskCompletionSource<ProtoOAGetAccountListByAccessTokenRes>();
		_accountListTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_accountListTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(req, clientMsgId);
			this.AddDebugLog("Account list request sent with clientMsgId: {0}", clientMsgId);

			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				return await taskSource.Task;
			}
			else
			{
				if (_accountListTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Get account list timed out after {timeout.Value.TotalSeconds} seconds"));
				}
				throw new TimeoutException($"Get account list timed out after {timeout.Value.TotalSeconds} seconds");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Get account list cancelled");
			_accountListTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Get account list failed: {0}", ex);
			_accountListTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
	}

	/// <summary>
	/// Refreshes the OAuth2 access token using refresh token.
	/// </summary>
	/// <param name="refreshToken">OAuth2 refresh token.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
	/// <param name="timeout">Optional timeout for the operation. Defaults to 30 seconds.</param>
	/// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public async ValueTask<ProtoOARefreshTokenRes> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromSeconds(30);
		var clientMsgId = GenerateClientMsgId();

		var req = new ProtoOARefreshTokenReq
		{
			RefreshToken = refreshToken
		};

		var taskSource = new TaskCompletionSource<ProtoOARefreshTokenRes>();
		_refreshTokenTasks.TryAdd(clientMsgId, taskSource);

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.Token.Register(() =>
			{
				if (_refreshTokenTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetCanceled(cancellationToken);
				}
			});

			await _client.SendMessage(req, clientMsgId);
			this.AddDebugLog("Refresh token request sent with clientMsgId: {0}", clientMsgId);

			var completedTask = await Task.WhenAny(taskSource.Task, Task.Delay(timeout.Value, cts.Token));

			if (completedTask == taskSource.Task)
			{
				cts.Cancel();
				return await taskSource.Task;
			}
			else
			{
				if (_refreshTokenTasks.TryRemove(clientMsgId, out var tcs))
				{
					tcs.TrySetException(new TimeoutException($"Refresh token timed out after {timeout.Value.TotalSeconds} seconds"));
				}
				throw new TimeoutException($"Refresh token timed out after {timeout.Value.TotalSeconds} seconds");
			}
		}
		catch (OperationCanceledException)
		{
			this.AddWarningLog("Refresh token cancelled");
			_refreshTokenTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Refresh token failed: {0}", ex);
			_refreshTokenTasks.TryRemove(clientMsgId, out var _);
			throw;
		}
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
}
