namespace StockSharp.Customization.CTrader.Tests.Helpers;

/// <summary>
/// Test helper utilities for cTrader connector tests using StockSharp interface pattern.
/// </summary>
static class CTraderTestHelper
{
	private static readonly IdGenerator _idGenerator = new IncrementalIdGenerator();

	/// <summary>
	/// Create test adapter with message capture.
	/// </summary>
	public static CTraderMessageAdapter CreateAdapterWithEvents(out List<Message> messages)
	{
		var adapter = new CTraderMessageAdapter(_idGenerator)
		{
			Key = "test_app_id".Secure(),
			Secret = "test_secret".Secure(),
			Token = "test_access_token".Secure(),
			IsDemo = true,
		};

		var result = new List<Message>();
		adapter.NewOutMessage += result.Add;
		messages = result;

		return adapter;
	}

	/// <summary>
	/// Create security ID from symbol name.
	/// </summary>
	public static SecurityId ToStockSharp(this string symbolName)
	{
		return new SecurityId
		{
			SecurityCode = symbolName,
			BoardCode = "CTRADER",
		};
	}

	/// <summary>
	/// Create order register message for testing.
	/// </summary>
	public static OrderRegisterMessage CreateOrderRegisterMessage(
		string symbol = "EURUSD",
		Sides side = Sides.Buy,
		decimal volume = 1000m,
		OrderTypes orderType = OrderTypes.Market,
		decimal price = 0,
		string portfolioName = "cTrader_test")
	{
		var msg = new OrderRegisterMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			SecurityId = symbol.ToStockSharp(),
			Side = side,
			Volume = volume,
			OrderType = orderType,
			PortfolioName = portfolioName,
		};

		if (price > 0)
			msg.Price = price;

		return msg;
	}

	/// <summary>
	/// Create order cancel message for testing.
	/// </summary>
	public static OrderCancelMessage CreateOrderCancelMessage(long orderId, long originalTransactionId = 0)
	{
		return new OrderCancelMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			OrderId = orderId,
			OriginalTransactionId = originalTransactionId,
		};
	}

	/// <summary>
	/// Create security lookup message for testing.
	/// </summary>
	public static SecurityLookupMessage CreateSecurityLookupMessage()
	{
		return new SecurityLookupMessage
		{
			TransactionId = _idGenerator.GetNextId(),
		};
	}

	/// <summary>
	/// Create market data subscription message for testing.
	/// </summary>
	public static MarketDataMessage CreateMarketDataSubscription(
		string symbol = "EURUSD",
		DataType dataType = null,
		bool isSubscribe = true)
	{
		return new MarketDataMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			SecurityId = symbol.ToStockSharp(),
			DataType2 = dataType ?? DataType.Ticks,
			IsSubscribe = isSubscribe,
		};
	}

	/// <summary>
	/// Create portfolio lookup message for testing.
	/// </summary>
	public static PortfolioLookupMessage CreatePortfolioLookupMessage()
	{
		return new PortfolioLookupMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			IsSubscribe = true,
		};
	}

	/// <summary>
	/// Wait for specific message type in message list with timeout.
	/// </summary>
	public static async Task<T> WaitForMessage<T>(List<Message> messages, int timeoutMs = 1000, Func<T, bool> predicate = null)
		where T : Message
	{
		var startTime = DateTime.UtcNow;

		while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
		{
			var msg = predicate == null
				? messages.OfType<T>().FirstOrDefault()
				: messages.OfType<T>().FirstOrDefault(predicate);

			if (msg != null)
				return msg;

			await Task.Delay(10);
		}

		return null;
	}

	/// <summary>
	/// Wait for message count with timeout.
	/// </summary>
	public static async Task<bool> WaitForMessageCount(List<Message> messages, int expectedCount, int timeoutMs = 1000)
	{
		var startTime = DateTime.UtcNow;

		while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
		{
			if (messages.Count >= expectedCount)
				return true;

			await Task.Delay(10);
		}

		return false;
	}

	/// <summary>
	/// Find message by predicate.
	/// </summary>
	public static T FindMessage<T>(this List<Message> messages, Func<T, bool> predicate = null)
		where T : Message
	{
		return predicate == null
			? messages.OfType<T>().FirstOrDefault()
			: messages.OfType<T>().FirstOrDefault(predicate);
	}

	/// <summary>
	/// Find all messages of type.
	/// </summary>
	public static List<T> FindMessages<T>(this List<Message> messages)
		where T : Message
	{
		return messages.OfType<T>().ToList();
	}
}
