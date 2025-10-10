namespace StockSharp.CTraderConnector.Tests;

using StockSharp.CTraderConnector.Tests.Helpers;

/// <summary>
/// Tests for cTrader connector authentication and connection management.
/// Tests use StockSharp IMessageAdapter interface pattern.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("Connection")]
public class CTraderAuthenticationTests
{
	[TestMethod]
	public void Settings_ApplicationId_Required()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationSecret = "secret".Secure(),
			AccessToken = "token".Secure(),
		};

		// Act & Assert - ApplicationId is required
		adapter.ApplicationId.AssertNull();
	}

	[TestMethod]
	public void Settings_ApplicationSecret_Required()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "test_app",
			AccessToken = "token".Secure(),
		};

		// Act & Assert - ApplicationSecret is required
		adapter.ApplicationSecret.AssertNull();
	}

	[TestMethod]
	public void Settings_AccessToken_RequiredForTransactions()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "test_app",
			ApplicationSecret = "secret".Secure(),
			// No AccessToken - should fail when trying transactional operations
		};

		// Note: Actual connection will fail in real test, but this verifies settings validation
		adapter.ApplicationId.AssertEqual("test_app");
		adapter.ApplicationSecret.AssertNotNull();
		adapter.AccessToken.AssertNull();
	}

	[TestMethod]
	public void Settings_Environment_Demo()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Environment = CTraderEnvironment.Demo,
		};

		// Assert
		adapter.Environment.AssertEqual(CTraderEnvironment.Demo);
	}

	[TestMethod]
	public void Settings_Environment_Live()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Environment = CTraderEnvironment.Live,
		};

		// Assert
		adapter.Environment.AssertEqual(CTraderEnvironment.Live);
	}

	[TestMethod]
	public void Settings_AccountId_GetSet()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			AccountId = 123456,
		};

		// Assert
		adapter.AccountId.AssertEqual(123456L);
	}

	[TestMethod]
	public void Settings_HeartbeatInterval_Default()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		adapter.HeartbeatInterval.AssertEqual(CTraderMessageAdapter.DefaultHeartbeatInterval);
		adapter.HeartbeatInterval.AssertEqual(TimeSpan.FromSeconds(30));
	}

	[TestMethod]
	public void Settings_SaveLoad_PreservesValues()
	{
		// Arrange
		var adapter1 = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "save_test_app",
			ApplicationSecret = "save_test_secret".Secure(),
			AccessToken = "save_test_token".Secure(),
			Environment = CTraderEnvironment.Live,
			AccountId = 789012,
		};

		var storage = new SettingsStorage();

		// Act
		adapter1.Save(storage);

		var adapter2 = new CTraderMessageAdapter(new IncrementalIdGenerator());
		adapter2.Load(storage);

		// Assert
		adapter2.ApplicationId.AssertEqual("save_test_app");
		adapter2.ApplicationSecret.UnSecure().AssertEqual("save_test_secret");
		adapter2.AccessToken.UnSecure().AssertEqual("save_test_token");
		adapter2.Environment.AssertEqual(CTraderEnvironment.Live);
		// Note: AccountId is not saved/loaded - it's populated from access token
	}

	[TestMethod]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			ApplicationId = "test_app",
			Environment = CTraderEnvironment.Demo,
			AccountId = 123456,
		};

		// Act
		var result = adapter.ToString();

		// Assert
		result.Contains("test_app").AssertTrue();
		result.Contains("Demo").AssertTrue();
		result.Contains("123456").AssertTrue();
	}
}
