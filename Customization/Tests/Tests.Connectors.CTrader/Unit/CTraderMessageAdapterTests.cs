namespace StockSharp.Customization.CTrader.Tests;

using CTrader.Tests.Helpers;

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
			Key = "test_app".Secure(),
			Secret = "test_secret".Secure(),
			Token = "test_token".Secure(),
			RefreshToken = "test_refresh".Secure(),
			IsDemo = false,
			HeartbeatInterval = TimeSpan.FromSeconds(60),
		};

		var storage = new SettingsStorage();

		// Act
		adapter1.Save(storage);

		var adapter2 = new CTraderMessageAdapter(new IncrementalIdGenerator());
		adapter2.Load(storage);

		// Assert - Verify CTrader-specific settings are preserved
		adapter2.Key.UnSecure().AssertEqual("test_app");
		adapter2.Secret.UnSecure().AssertEqual("test_secret");
		adapter2.Token.UnSecure().AssertEqual("test_token");
		adapter2.RefreshToken.UnSecure().AssertEqual("test_refresh");
		adapter2.IsDemo.AssertFalse();
		// Note: HeartbeatInterval is saved by base class
	}

	[TestMethod]
	public void Settings_Clone_CreatesIndependentCopy()
	{
		// Arrange
		var adapter1 = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Key = "clone_test".Secure(),
			IsDemo = true,
		};

		// Act
		var adapter2 = (CTraderMessageAdapter)adapter1.Clone();

		// Assert - Verify values were copied
		adapter2.Key.UnSecure().AssertEqual("clone_test");
		adapter2.IsDemo.AssertTrue();

		// Verify it's a different instance
		(adapter1 != adapter2).AssertTrue();
	}

	[TestMethod]
	public void ToString_ContainsKeyInformation()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Key = "my_app".Secure(),
			IsDemo = false,
		};

		// Act
		var result = adapter.ToString();

		// Assert
		result.Contains("my_app").AssertTrue();
		result.Contains("Live").AssertTrue();
	}
}
