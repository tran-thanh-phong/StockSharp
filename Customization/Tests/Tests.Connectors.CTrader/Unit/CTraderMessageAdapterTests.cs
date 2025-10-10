namespace StockSharp.CTraderConnector.Tests;

using StockSharp.CTraderConnector.Tests.Helpers;

/// <summary>
/// Tests for cTrader adapter configuration and error handling.
/// Tests use StockSharp IMessageAdapter interface pattern.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CTraderMessageAdapterTests
{
	[TestMethod]
	public void Adapter_DefaultHeartbeatInterval()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		adapter.HeartbeatInterval.AssertEqual(TimeSpan.FromSeconds(30));
		adapter.HeartbeatInterval.AssertEqual(CTraderMessageAdapter.DefaultHeartbeatInterval);
	}

	[TestMethod]
	public void Adapter_Categories()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - Verify adapter is categorized correctly
		var categories = adapter.Categories;
		categories.HasFlag(MessageAdapterCategories.FX).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.RealTime).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.Ticks).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.MarketDepth).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.Level1).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.Transactions).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.Candles).AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportedInMessages_IsConfigured()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - Verify adapter has message support configured for key business messages
		adapter.IsMessageSupported(MessageTypes.SecurityLookup).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.MarketData).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.OrderRegister).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.OrderCancel).AssertTrue();
		// Note: Connect/Disconnect are infrastructure messages handled by base class
		// Note: OrderStatus, PortfolioLookup not yet implemented
	}

	[TestMethod]
	public void Adapter_DoesNotSupportOrderReplace()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - OrderReplace should be explicitly removed
		var supportedTypes = adapter.SupportedInMessages.ToList();
		supportedTypes.Contains(MessageTypes.OrderReplace).AssertFalse();
	}

	[TestMethod]
	public void Settings_SaveLoad_PreservesConfiguration()
	{
		// Arrange
		var adapter1 = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "test_app",
			ApplicationSecret = "test_secret".Secure(),
			AccessToken = "test_token".Secure(),
			RefreshToken = "test_refresh".Secure(),
			Environment = CTraderEnvironment.Live,
			AccountId = 999888,
			HeartbeatInterval = TimeSpan.FromSeconds(60),
		};

		var storage = new SettingsStorage();

		// Act
		adapter1.Save(storage);

		var adapter2 = new CTraderMessageAdapter(new IncrementalIdGenerator());
		adapter2.Load(storage);

		// Assert - Verify CTrader-specific settings are preserved
		adapter2.ApplicationId.AssertEqual("test_app");
		adapter2.ApplicationSecret.UnSecure().AssertEqual("test_secret");
		adapter2.AccessToken.UnSecure().AssertEqual("test_token");
		adapter2.RefreshToken.UnSecure().AssertEqual("test_refresh");
		adapter2.Environment.AssertEqual(CTraderEnvironment.Live);
		// Note: AccountId is not saved/loaded - it's populated from access token
		// Note: HeartbeatInterval is saved by base class
	}

	[TestMethod]
	public void Settings_Clone_CreatesIndependentCopy()
	{
		// Arrange
		var adapter1 = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "clone_test",
			Environment = CTraderEnvironment.Demo,
			AccountId = 111222,
		};

		// Act
		var adapter2 = (CTraderMessageAdapter)adapter1.Clone();

		// Assert - Verify values were copied
		adapter2.ApplicationId.AssertEqual("clone_test");
		adapter2.Environment.AssertEqual(CTraderEnvironment.Demo);
		// Note: AccountId may not be cloned as it's populated from access token

		// Verify it's a different instance
		(adapter1 != adapter2).AssertTrue();
	}

	[TestMethod]
	public void ToString_ContainsKeyInformation()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "my_app",
			Environment = CTraderEnvironment.Live,
			AccountId = 777888,
		};

		// Act
		var result = adapter.ToString();

		// Assert
		result.Contains("my_app").AssertTrue();
		result.Contains("Live").AssertTrue();
		result.Contains("777888").AssertTrue();
	}
}
