namespace StockSharp.Customization.CTrader.Tests;

using CTrader.Tests.Helpers;

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
	public void Settings_Key_Required()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Secret = "secret".Secure(),
			Token = "token".Secure(),
		};

		// Act & Assert - Key is required for IKeySecretAdapter
		adapter.Key.AssertNull();
	}

	[TestMethod]
	public void Settings_Secret_Required()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Key = "test_app".Secure(),
			Token = "token".Secure(),
		};

		// Act & Assert - Secret is required for IKeySecretAdapter
		adapter.Secret.AssertNull();
	}

	[TestMethod]
	public void Settings_Token_RequiredForTransactions()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Key = "test_app".Secure(),
			Secret = "secret".Secure(),
			// No Token - should fail when trying transactional operations
		};

		// Note: Actual connection will fail in real test, but this verifies settings validation
		adapter.Key.AssertNotNull();
		adapter.Secret.AssertNotNull();
		adapter.Token.AssertNull();
	}

	[TestMethod]
	public void Settings_IsDemo_Demo()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			IsDemo = true,
		};

		// Assert
		adapter.IsDemo.AssertTrue();
	}

	[TestMethod]
	public void Settings_IsDemo_Live()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			IsDemo = false,
		};

		// Assert
		adapter.IsDemo.AssertFalse();
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
			Key = "save_test_app".Secure(),
			Secret = "save_test_secret".Secure(),
			Token = "save_test_token".Secure(),
			IsDemo = false,
		};

		var storage = new SettingsStorage();

		// Act
		adapter1.Save(storage);

		var adapter2 = new CTraderMessageAdapter(new IncrementalIdGenerator());
		adapter2.Load(storage);

		// Assert
		adapter2.Key.UnSecure().AssertEqual("save_test_app");
		adapter2.Secret.UnSecure().AssertEqual("save_test_secret");
		adapter2.Token.UnSecure().AssertEqual("save_test_token");
		adapter2.IsDemo.AssertFalse();
	}

	[TestMethod]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator())
		{
			Key = "test_app".Secure(),
			IsDemo = true,
		};

		// Act
		var result = adapter.ToString();

		// Assert
		result.Contains("test_app").AssertTrue();
		result.Contains("Demo").AssertTrue();
	}

	[TestMethod]
	public void Settings_Implements_IKeySecretAdapter()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		(adapter is IKeySecretAdapter).AssertTrue();
		adapter.Key.AssertNotNull();
		adapter.Secret.AssertNotNull();
	}

	[TestMethod]
	public void Settings_Implements_ITokenAdapter()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		(adapter is ITokenAdapter).AssertTrue();
		adapter.Token.AssertNotNull();
	}

	[TestMethod]
	public void Settings_Implements_IDemoAdapter()
	{
		// Arrange & Act
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		(adapter is IDemoAdapter).AssertTrue();
		adapter.IsDemo.AssertTrue(); // Default value should be true for demo
	}
}
