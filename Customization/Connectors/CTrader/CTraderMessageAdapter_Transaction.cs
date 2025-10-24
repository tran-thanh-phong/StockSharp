namespace StockSharp.Customization.CTrader;

using OpenAPI.Net;

partial class CTraderMessageAdapter
{
	private readonly Dictionary<long, RefPair<long, decimal>> _orderInfo = new();
	private long _lastTradeId;

	private string PortfolioName => "cTrader_" + Key?.ToId() + "_" + _accountId;

	/// <inheritdoc />
	public override async ValueTask RegisterOrderAsync(OrderRegisterMessage regMsg, CancellationToken cancellationToken)
	{
		try
		{
			var symbolCode = regMsg.SecurityId.SecurityCode;

			if (!_symbolCodeToId.TryGetValue(symbolCode, out var symbolId))
			{
				SendOutMessage(new ExecutionMessage
				{
					DataTypeEx = DataType.Transactions,
					OriginalTransactionId = regMsg.TransactionId,
					OrderState = OrderStates.Failed,
					Error = new InvalidOperationException($"Unknown symbol: {symbolCode}"),
					HasOrderInfo = true,
				});
				return;
			}

			// Map order type
			// StockSharp uses Conditional for stop orders; determine specific type from price and condition
			var orderType = regMsg.OrderType switch
			{
				StockSharp.Messages.OrderTypes.Market => ProtoOAOrderType.Market,
				StockSharp.Messages.OrderTypes.Limit => ProtoOAOrderType.Limit,
				StockSharp.Messages.OrderTypes.Conditional when regMsg.Price > 0 => ProtoOAOrderType.StopLimit, // Stop-limit if has price
				StockSharp.Messages.OrderTypes.Conditional => ProtoOAOrderType.Stop, // Pure stop order
				_ => throw new NotSupportedException($"Order type {regMsg.OrderType} not supported")
			};

			// Map side
			var tradeSide = regMsg.Side switch
			{
				Sides.Buy => ProtoOATradeSide.Buy,
				Sides.Sell => ProtoOATradeSide.Sell,
				_ => throw new InvalidOperationException($"Invalid order side: {regMsg.Side}")
			};

			// Convert volume
			var volume = regMsg.Volume.ToCTraderVolume();

			this.AddInfoLog("Registering order: {0} {1} {2} @ {3}, Volume={4}",
				regMsg.Side, regMsg.Type, symbolCode, regMsg.Price, volume);

			// Get order conditions if available
			var condition = regMsg.Condition as CTraderOrderCondition;

			// Use transaction ID as client order ID for correlation
			var clientOrderId = regMsg.TransactionId.ToString();

			// Send order to cTrader
			await _client.NewOrderAsync(
				_accountId,
				symbolId,
				tradeSide,
				volume,
				orderType,
				clientOrderId: clientOrderId,
				limitPrice: regMsg.Price > 0 ? (double)regMsg.Price : null,
				stopPrice: condition?.StopPrice > 0 ? (double)condition.StopPrice : null,
				stopLoss: condition?.StopLoss > 0 ? (double)condition.StopLoss : null,
				takeProfit: condition?.TakeProfit > 0 ? (double)condition.TakeProfit : null,
				trailingStopLoss: condition?.TrailingStop > 0 ? (bool?)true : null,
				cancellationToken);

			// Note: Response will come via ProtoOAExecutionEvent which is handled by ProcessOrderExecution
			// We don't send immediate confirmation here - wait for execution event

			this.AddDebugLog("Order request sent, TransactionId={0}", regMsg.TransactionId);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Order registration failed: {0}", ex);

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OriginalTransactionId = regMsg.TransactionId,
				OrderState = OrderStates.Failed,
				Error = ex,
				HasOrderInfo = true,
			});
		}
	}

	/// <inheritdoc />
	public override async ValueTask CancelOrderAsync(OrderCancelMessage cancelMsg, CancellationToken cancellationToken)
	{
		if (cancelMsg.OrderId == null)
			throw new InvalidOperationException(LocalizedStrings.OrderNoExchangeId.Put(cancelMsg.OriginalTransactionId));

		try
		{
			this.AddInfoLog("Cancelling order ID {0}", cancelMsg.OrderId);

			await _client.CancelOrderAsync(_accountId, cancelMsg.OrderId.Value, cancellationToken);

			// Note: Confirmation will come via ProtoOAExecutionEvent with OrderCancelled execution type
			// We don't send immediate confirmation here - wait for execution event

			this.AddDebugLog("Order cancellation request sent, OrderId={0}, TransactionId={1}",
				cancelMsg.OrderId, cancelMsg.TransactionId);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Order cancellation failed: {0}", ex);

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OriginalTransactionId = cancelMsg.TransactionId,
				OrderId = cancelMsg.OrderId,
				OrderState = OrderStates.Failed,
				Error = ex,
				HasOrderInfo = true,
			});
		}
	}

	/// <inheritdoc />
	public override async ValueTask OrderStatusAsync(OrderStatusMessage statusMsg, CancellationToken cancellationToken)
	{
		if (statusMsg != null)
		{
			SendSubscriptionReply(statusMsg.TransactionId);

			if (!statusMsg.IsSubscribe)
				return;
		}

		try
		{
			this.AddInfoLog("Order status reconciliation for account {0}", _accountId);

			// Get all active orders and positions
			var reconcileRes = await _client.ReconcileAsync(_accountId, cancellationToken);

			// Process all active orders
			foreach (var order in reconcileRes.Order)
			{
				if (!_symbolIdToCode.TryGetValue(order.TradeData.SymbolId, out var symbolCode))
				{
					this.AddWarningLog("Unknown symbol ID {0} in reconcile", order.TradeData.SymbolId);
					continue;
				}

				// Extract transaction ID from ClientOrderId if available
				long transactionId = 0;
				if (order.HasClientOrderId && long.TryParse(order.ClientOrderId, out var parsedTransId))
				{
					transactionId = parsedTransId;
				}

				var side = order.TradeData.TradeSide == ProtoOATradeSide.Buy ? Sides.Buy : Sides.Sell;
				var volume = order.TradeData.Volume.ToStockSharpVolume();
				var filledVolume = order.ExecutedVolume.ToStockSharpVolume();
				var remainingVolume = volume - filledVolume;

				var orderState = order.OrderStatus switch
				{
					ProtoOAOrderStatus.OrderStatusAccepted => OrderStates.Active,
					ProtoOAOrderStatus.OrderStatusFilled => OrderStates.Done,
					ProtoOAOrderStatus.OrderStatusCancelled => OrderStates.Done,
					ProtoOAOrderStatus.OrderStatusExpired => OrderStates.Done,
					ProtoOAOrderStatus.OrderStatusRejected => OrderStates.Failed,
					_ => OrderStates.Active
				};

				var msg = new ExecutionMessage
				{
					DataTypeEx = DataType.Transactions,
					OrderId = order.OrderId,
					OriginalTransactionId = transactionId,
					OrderVolume = volume,
					Balance = remainingVolume,
					Side = side,
					SecurityId = symbolCode.ToStockSharp(),
					ServerTime = DateTimeOffset.FromUnixTimeMilliseconds((long)order.UtcLastUpdateTimestamp).UtcDateTime,
					PortfolioName = PortfolioName,
					OrderState = orderState,
					HasOrderInfo = true,
				};

				if (order.HasLimitPrice)
					msg.OrderPrice = (decimal)order.LimitPrice;

				SendOutMessage(msg);

				// Track active orders
				if (orderState == OrderStates.Active && remainingVolume > 0)
				{
					_orderInfo[order.OrderId] = RefTuple.Create(transactionId, remainingVolume);
				}
			}

			this.AddInfoLog("Reconciled {0} orders", reconcileRes.Order.Count);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Order status reconciliation failed: {0}", ex);
		}

		if (statusMsg != null)
			SendSubscriptionResult(statusMsg);
	}

	/// <inheritdoc />
	public override async ValueTask PortfolioLookupAsync(PortfolioLookupMessage lookupMsg, CancellationToken cancellationToken)
	{
		if (lookupMsg != null)
		{
			SendSubscriptionReply(lookupMsg.TransactionId);

			if (!lookupMsg.IsSubscribe)
				return;
		}

		if (_client == null)
		{
			this.AddErrorLog("PortfolioLookup requires connection. Please connect first.");
			if (lookupMsg != null)
				SendSubscriptionResult(lookupMsg);
			return;
		}

		var transactionId = lookupMsg?.TransactionId ?? 0;

		try
		{
			this.AddInfoLog("Portfolio lookup for account {0}", _accountId);

			// Get trader/account data
			var traderRes = await _client.GetTraderAsync(_accountId, cancellationToken);
			var trader = traderRes.Trader;

			var pfName = PortfolioName;

			// Send portfolio message with real balance data
			SendOutMessage(new PortfolioMessage
			{
				PortfolioName = pfName,
				BoardCode = Native.Extensions.BoardCode,
				OriginalTransactionId = transactionId
			});

			// Calculate equity and available margin
			// Balance is in cents, convert to decimal
			var moneyDigits = trader.MoneyDigits;
			var divisor = (decimal)Math.Pow(10, moneyDigits);
			var balance = trader.Balance / divisor;

			// Send position change message with balance/equity/margin
			SendOutMessage(new PositionChangeMessage
			{
				PortfolioName = pfName,
				SecurityId = SecurityId.Money,
				ServerTime = CurrentTime.ConvertToUtc(),
				OriginalTransactionId = transactionId
			}
			.TryAdd(PositionChangeTypes.BeginValue, balance)
			.TryAdd(PositionChangeTypes.CurrentValue, balance)
			.TryAdd(PositionChangeTypes.BlockedValue, 0m)); // TODO: Calculate used margin from positions

			this.AddInfoLog("Portfolio loaded: Balance={0}", balance);
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Portfolio lookup failed: {0}", ex);
		}

		if (lookupMsg != null)
		{
			SendSubscriptionResult(lookupMsg);
		}
	}

	private void ProcessOrderExecution(ProtoOAExecutionEvent execution)
	{
		var order = execution.Order;

		if (!_symbolIdToCode.TryGetValue(order.TradeData.SymbolId, out var symbolCode))
		{
			this.AddWarningLog("Unknown symbol ID {0} in execution event", order.TradeData.SymbolId);
			return;
		}

		// Extract transaction ID from ClientOrderId if available
		long transactionId = 0;
		if (order.HasClientOrderId && long.TryParse(order.ClientOrderId, out var parsedTransId))
		{
			transactionId = parsedTransId;
		}

		var orderState = execution.ExecutionType switch
		{
			ProtoOAExecutionType.OrderAccepted => OrderStates.Active,
			ProtoOAExecutionType.OrderFilled => OrderStates.Done,
			ProtoOAExecutionType.OrderCancelled => OrderStates.Done,
			ProtoOAExecutionType.OrderExpired => OrderStates.Done,
			ProtoOAExecutionType.OrderRejected => OrderStates.Failed,
			ProtoOAExecutionType.OrderCancelRejected => OrderStates.Active, // Cancel failed, order still active
			ProtoOAExecutionType.OrderReplaced => OrderStates.Done, // Old order done, new one will come
			_ => OrderStates.None
		};

		var side = order.TradeData.TradeSide == ProtoOATradeSide.Buy ? Sides.Buy : Sides.Sell;
		var volume = order.TradeData.Volume.ToStockSharpVolume();
		var filledVolume = order.ExecutedVolume.ToStockSharpVolume();
		var remainingVolume = volume - filledVolume;

		var msg = new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OrderId = order.OrderId,
			OriginalTransactionId = transactionId,
			OrderVolume = volume,
			Balance = remainingVolume,
			Side = side,
			SecurityId = symbolCode.ToStockSharp(),
			ServerTime = DateTimeOffset.FromUnixTimeMilliseconds((long)order.UtcLastUpdateTimestamp).UtcDateTime,
			PortfolioName = PortfolioName,
			OrderState = orderState,
			HasOrderInfo = true,
		};

		if (order.HasLimitPrice)
			msg.OrderPrice = (decimal)order.LimitPrice;

		SendOutMessage(msg);

		// Track order for trade processing
		if (orderState == OrderStates.Active && remainingVolume > 0)
		{
			_orderInfo[order.OrderId] = RefTuple.Create(transactionId, remainingVolume);
		}
		else if (orderState == OrderStates.Done || orderState == OrderStates.Failed)
		{
			_orderInfo.Remove(order.OrderId);
		}
	}

	private void ProcessOrder(long orderId, string symbolCode, decimal volume, decimal price, Sides side, long transId, long origTransId)
	{
		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OrderId = orderId,
			TransactionId = transId,
			OriginalTransactionId = origTransId,
			OrderPrice = price,
			Balance = volume,
			OrderVolume = volume,
			Side = side,
			SecurityId = symbolCode.ToStockSharp(),
			ServerTime = CurrentTime.ConvertToUtc(),
			PortfolioName = PortfolioName,
			OrderState = OrderStates.Active,
			HasOrderInfo = true,
		});
	}

	private void ProcessDealExecution(ProtoOAExecutionEvent execution)
	{
		var deal = execution.Deal;

		if (!_symbolIdToCode.TryGetValue(deal.SymbolId, out var symbolCode))
		{
			this.AddWarningLog("Unknown symbol ID {0} in deal execution", deal.SymbolId);
			return;
		}

		var info = _orderInfo.TryGetValue(deal.OrderId);
		var time = DateTimeOffset.FromUnixTimeMilliseconds((long)deal.CreateTimestamp).UtcDateTime;
		var volume = deal.Volume.ToStockSharpVolume();
		var price = (decimal)deal.ExecutionPrice;

		// Send trade message
		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OrderId = deal.OrderId,
			TradeId = deal.DealId,
			TradePrice = price,
			TradeVolume = volume,
			ServerTime = time,
			PortfolioName = PortfolioName,
			SecurityId = symbolCode.ToStockSharp(),
			OriginalTransactionId = info?.First ?? 0,
		});

		// Update order balance if we're tracking it
		if (info != null)
		{
			info.Second -= volume;

			if (info.Second < 0)
			{
				this.AddWarningLog("Order {0} balance negative: {1}", deal.OrderId, info.Second);
				info.Second = 0;
			}

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OrderId = deal.OrderId,
				Balance = info.Second,
				OrderState = info.Second > 0 ? OrderStates.Active : OrderStates.Done,
				HasOrderInfo = true,
				ServerTime = time,
				PortfolioName = PortfolioName,
				SecurityId = symbolCode.ToStockSharp(),
				OriginalTransactionId = info.First,
			});

			if (info.Second == 0)
				_orderInfo.Remove(deal.OrderId);
		}
	}

	private void ProcessTrade(long orderId, long tradeId, decimal price, decimal volume, DateTimeOffset time)
	{
		var info = _orderInfo.TryGetValue(orderId);

		if (info == null)
			return;

		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OrderId = orderId,
			TradeId = tradeId,
			TradePrice = price,
			TradeVolume = volume,
			ServerTime = time,
			PortfolioName = PortfolioName,
			OriginalTransactionId = info.First,
		});

		info.Second -= volume;

		if (info.Second < 0)
			throw new InvalidOperationException(LocalizedStrings.OrderBalanceNotEnough.Put(orderId, info.Second));

		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OrderId = orderId,
			Balance = info.Second,
			OrderState = info.Second > 0 ? OrderStates.Active : OrderStates.Done,
			HasOrderInfo = true,
			ServerTime = time,
			PortfolioName = PortfolioName,
			OriginalTransactionId = info.First,
		});

		if (info.Second == 0)
			_orderInfo.Remove(orderId);
	}
}
