namespace StockSharp.CTraderConnector;

partial class CTraderMessageAdapter
{
	private readonly Dictionary<long, RefPair<long, decimal>> _orderInfo = new();
	private long _lastTradeId;

	private string PortfolioName => "cTrader_" + ApplicationId + "_" + AccountId;

	/// <inheritdoc />
	public override async ValueTask RegisterOrderAsync(OrderRegisterMessage regMsg, CancellationToken cancellationToken)
	{
		// TODO: Implement order registration via ProtoOANewOrderReq
		// Will be implemented in Phase 7

		this.AddInfoLog("Order registration for {0} {1} @ {2} - implementation pending",
			regMsg.SecurityId.SecurityCode, regMsg.Volume, regMsg.Price);

		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OriginalTransactionId = regMsg.TransactionId,
			OrderState = OrderStates.Failed,
			Error = new NotImplementedException("Order registration - Phase 7"),
			HasOrderInfo = true,
		});
	}

	/// <inheritdoc />
	public override async ValueTask CancelOrderAsync(OrderCancelMessage cancelMsg, CancellationToken cancellationToken)
	{
		if (cancelMsg.OrderId == null)
			throw new InvalidOperationException(LocalizedStrings.OrderNoExchangeId.Put(cancelMsg.OriginalTransactionId));

		// TODO: Implement order cancellation via ProtoOACancelOrderReq
		// Will be implemented in Phase 7

		this.AddInfoLog("Order cancellation for ID {0} - implementation pending", cancelMsg.OrderId);

		SendOutMessage(new ExecutionMessage
		{
			DataTypeEx = DataType.Transactions,
			OriginalTransactionId = cancelMsg.TransactionId,
			OrderId = cancelMsg.OrderId,
			OrderState = OrderStates.Failed,
			Error = new NotImplementedException("Order cancellation - Phase 7"),
			HasOrderInfo = true,
		});
	}

	/// <inheritdoc />
	public override async ValueTask OrderStatusAsync(OrderStatusMessage statusMsg, CancellationToken cancellationToken)
	{
		// TODO: Implement order status via ProtoOAReconcileReq
		// Will be implemented in Phase 7

		if (statusMsg != null)
		{
			SendSubscriptionReply(statusMsg.TransactionId);

			this.AddInfoLog("Order status subscription - implementation pending");

			SendSubscriptionResult(statusMsg);
		}
		else
		{
			// Internal refresh request
			this.AddDebugLog("Order status refresh");
		}
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

		var transactionId = lookupMsg?.TransactionId ?? 0;

		// TODO: Implement portfolio lookup via ProtoOAGetAccountsReq
		// Will be implemented in Phase 7

		this.AddInfoLog("Portfolio lookup - implementation pending");

		var pfName = PortfolioName;

		SendOutMessage(new PortfolioMessage
		{
			PortfolioName = pfName,
			BoardCode = BoardCodes.CTrader,
			OriginalTransactionId = transactionId,
		});

		if (lookupMsg != null)
			SendSubscriptionResult(lookupMsg);
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
