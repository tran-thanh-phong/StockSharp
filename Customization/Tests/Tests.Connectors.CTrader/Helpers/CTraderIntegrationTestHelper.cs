namespace StockSharp.Customization.CTrader.Tests.Helpers;

/// <summary>
/// Helper class for CTrader connector integration tests.
/// Provides utilities for message flow testing using MarketEmulator pattern.
/// </summary>
internal static class CTraderIntegrationTestHelper
{
	private static readonly IncrementalIdGenerator _idGenerator = new();

	/// <summary>
	/// Creates a CTrader adapter configured for testing with output message collection.
	/// </summary>
	/// <param name="messages">List to collect output messages</param>
	/// <returns>Configured adapter ready for testing</returns>
	public static CTraderMessageAdapter CreateAdapterForTesting(out List<Message> messages)
	{
		var adapter = new CTraderMessageAdapter(_idGenerator)
		{
			Key = "test_integration_app".Secure(),
			Secret = "test_integration_secret".Secure(),
			Token = "test_integration_token".Secure(),
			IsDemo = true,
			HeartbeatInterval = TimeSpan.FromSeconds(10),
		};

		var result = new List<Message>();
		adapter.NewOutMessage += result.Add;
		messages = result;
		
		return adapter;
	}

	/// <summary>
	/// Creates a test security for use in integration tests.
	/// </summary>
	public static Security CreateTestSecurity(string symbol = "EURUSD", decimal lastPrice = 1.1000m)
	{
		var security = new Security
		{
			Id = symbol,
			Code = symbol,
			Board = ExchangeBoard.Test,
			PriceStep = 0.00001m,
			VolumeStep = 1000m,
			Type = SecurityTypes.CryptoCurrency,
		};

		security.LastTick = new ExecutionMessage
		{
			DataTypeEx = DataType.Ticks,
			SecurityId = security.ToSecurityId(),
			TradePrice = lastPrice,
			ServerTime = DateTimeOffset.UtcNow,
		};

		return security;
	}

	/// <summary>
	/// Creates a test portfolio for integration tests.
	/// </summary>
	public static Portfolio CreateTestPortfolio(string name = "cTrader_integration_test")
	{
		var portfolio = new Portfolio
		{
			Name = name,
			BeginValue = 10000m,
			Currency = CurrencyTypes.USD,
		};

		return portfolio;
	}

	/// <summary>
	/// Creates an OrderRegisterMessage for testing order flow.
	/// </summary>
	public static OrderRegisterMessage CreateTestOrderMessage(
		SecurityId securityId,
		Sides side = Sides.Buy,
		decimal volume = 1000m,
		OrderTypes orderType = OrderTypes.Market,
		decimal price = 0,
		string portfolioName = "cTrader_test")
	{
		var msg = new OrderRegisterMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			SecurityId = securityId,
			Side = side,
			Volume = volume,
			OrderType = orderType,
			PortfolioName = portfolioName,
		};

		if (orderType == OrderTypes.Limit && price > 0)
		{
			msg.Price = price;
		}

		return msg;
	}

	/// <summary>
	/// Creates a MarketDataMessage for testing subscription flow.
	/// </summary>
	public static MarketDataMessage CreateTestSubscription(
		SecurityId securityId,
		DataType dataType,
		bool isSubscribe = true)
	{
		return new MarketDataMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			SecurityId = securityId,
			DataType2 = dataType,
			IsSubscribe = isSubscribe,
			LocalTime = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Creates a SecurityLookupMessage for testing security lookup flow.
	/// </summary>
	public static SecurityLookupMessage CreateTestSecurityLookup(string securityCode = null)
	{
		var msg = new SecurityLookupMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			LocalTime = DateTimeOffset.UtcNow,
		};

		if (!string.IsNullOrEmpty(securityCode))
		{
			msg.SecurityId = new SecurityId
			{
				SecurityCode = securityCode,
			};
		}

		return msg;
	}

	/// <summary>
	/// Creates a PortfolioLookupMessage for testing portfolio lookup flow.
	/// </summary>
	public static PortfolioLookupMessage CreateTestPortfolioLookup()
	{
		return new PortfolioLookupMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			LocalTime = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Creates a random tick message for testing.
	/// </summary>
	public static ExecutionMessage CreateRandomTick(SecurityId securityId, decimal price, decimal volume)
	{
		return new ExecutionMessage
		{
			DataTypeEx = DataType.Ticks,
			SecurityId = securityId,
			TradePrice = price,
			TradeVolume = volume,
			ServerTime = DateTimeOffset.UtcNow,
			LocalTime = DateTimeOffset.UtcNow,
			TradeId = _idGenerator.GetNextId(),
		};
	}

	/// <summary>
	/// Creates a market depth (order book) message for testing.
	/// </summary>
	public static QuoteChangeMessage CreateTestDepth(
		SecurityId securityId,
		decimal bidPrice = 1.0995m,
		decimal askPrice = 1.1005m,
		decimal volume = 10000m)
	{
		return new QuoteChangeMessage
		{
			SecurityId = securityId,
			ServerTime = DateTimeOffset.UtcNow,
			LocalTime = DateTimeOffset.UtcNow,
			Bids = [new QuoteChange(bidPrice, volume)],
			Asks = [new QuoteChange(askPrice, volume)],
		};
	}

	/// <summary>
	/// Finds first message of specific type in the output messages.
	/// </summary>
	public static T FindMessage<T>(this List<Message> messages) where T : Message
	{
		return messages.OfType<T>().FirstOrDefault();
	}

	/// <summary>
	/// Finds all messages of specific type in the output messages.
	/// </summary>
	public static IEnumerable<T> FindMessages<T>(this List<Message> messages) where T : Message
	{
		return messages.OfType<T>();
	}

	/// <summary>
	/// Asserts that at least one message of the specified type exists.
	/// </summary>
	public static void AssertHasMessage<T>(this List<Message> messages) where T : Message
	{
		messages.OfType<T>().Any().AssertTrue($"Expected at least one message of type {typeof(T).Name}");
	}

	/// <summary>
	/// Asserts that exactly N messages of the specified type exist.
	/// </summary>
	public static void AssertMessageCount<T>(this List<Message> messages, int expectedCount) where T : Message
	{
		var actualCount = messages.OfType<T>().Count();
		actualCount.AssertEqual(expectedCount, $"Expected {expectedCount} messages of type {typeof(T).Name}, but found {actualCount}");
	}
}
