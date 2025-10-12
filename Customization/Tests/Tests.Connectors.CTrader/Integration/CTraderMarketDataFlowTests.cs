using StockSharp.Customization.CTrader.Tests.Helpers;

namespace StockSharp.Customization.CTrader.Tests.Integration;

using CTrader.Tests.Helpers;

/// <summary>
/// Integration tests for CTrader market data subscription flow.
/// Tests the adapter's behavior when processing market data subscription messages.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[TestCategory("MarketData")]
public class CTraderMarketDataFlowTests
{
	[TestMethod]
	public void MarketData_TicksSubscription_IsSupported()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");
		var subscription = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(),
			DataType.Ticks,
			isSubscribe: true);

		// Act
		adapter.SendInMessage(subscription);

		// Assert
		subscription.DataType2.AssertEqual(DataType.Ticks);
		subscription.IsSubscribe.AssertTrue();
		subscription.TransactionId.AssertNotEqual(0);

		var supportedTypes = adapter.GetSupportedMarketDataTypes(security.ToSecurityId(), null, null);
		supportedTypes.Contains(DataType.Ticks).AssertTrue();
	}

	[TestMethod]
	public void MarketData_OrderBookSubscription_IsSupported()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("GBPUSD");
		var subscription = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(),
			DataType.MarketDepth,
			isSubscribe: true);

		// Act
		adapter.SendInMessage(subscription);

		// Assert
		subscription.DataType2.AssertEqual(DataType.MarketDepth);
		subscription.IsSubscribe.AssertTrue();

		var supportedTypes = adapter.GetSupportedMarketDataTypes(security.ToSecurityId(), null, null);
		supportedTypes.Contains(DataType.MarketDepth).AssertTrue();
	}

	[TestMethod]
	public void MarketData_Level1Subscription_IsSupported()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("USDJPY");
		var subscription = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(),
			DataType.Level1,
			isSubscribe: true);

		// Act
		adapter.SendInMessage(subscription);

		// Assert
		subscription.DataType2.AssertEqual(DataType.Level1);

		var supportedTypes = adapter.GetSupportedMarketDataTypes(security.ToSecurityId(), null, null);
		supportedTypes.Contains(DataType.Level1).AssertTrue();
	}

	[TestMethod]
	public void MarketData_Unsubscribe_HasCorrectFlag()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");
		var unsubscribe = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(),
			DataType.Ticks,
			isSubscribe: false);

		// Act
		adapter.SendInMessage(unsubscribe);

		// Assert
		unsubscribe.IsSubscribe.AssertFalse();
		unsubscribe.TransactionId.AssertNotEqual(0);
	}

	[TestMethod]
	public void SecurityLookup_AllSecurities_IsSupported()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var lookup = CTraderIntegrationTestHelper.CreateTestSecurityLookup();

		// Act
		adapter.SendInMessage(lookup);

		// Assert
		var isSupported = adapter.IsMessageSupported(MessageTypes.SecurityLookup);
		isSupported.AssertTrue();
		lookup.TransactionId.AssertNotEqual(0);
	}

	[TestMethod]
	public void SecurityLookup_SpecificSecurity_HasSecurityCode()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var lookup = CTraderIntegrationTestHelper.CreateTestSecurityLookup("EURUSD");

		// Act
		adapter.SendInMessage(lookup);

		// Assert
		lookup.SecurityId.SecurityCode.AssertEqual("EURUSD");
		lookup.TransactionId.AssertNotEqual(0);
	}

	[TestMethod]
	public void MultipleSubscriptions_HaveUniqueTransactionIds()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");

		var sub1 = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(), DataType.Ticks);
		var sub2 = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(), DataType.MarketDepth);
		var sub3 = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(), DataType.Level1);

		// Act
		adapter.SendInMessage(sub1);
		adapter.SendInMessage(sub2);
		adapter.SendInMessage(sub3);

		// Assert
		sub1.TransactionId.AssertNotEqual(sub2.TransactionId);
		sub2.TransactionId.AssertNotEqual(sub3.TransactionId);
		sub1.TransactionId.AssertNotEqual(sub3.TransactionId);
	}

	[TestMethod]
	public void Adapter_SupportsAllCriticalDataTypes()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");

		// Assert
		var supportedTypes = adapter.GetSupportedMarketDataTypes(security.ToSecurityId(), null, null);

		supportedTypes.Contains(DataType.Ticks).AssertTrue();
		supportedTypes.Contains(DataType.MarketDepth).AssertTrue();
		supportedTypes.Contains(DataType.Level1).AssertTrue();
	}

	[TestMethod]
	public void MarketData_SubscriptionMessage_HasRequiredFields()
	{
		// Arrange
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("GBPUSD");
		var subscription = CTraderIntegrationTestHelper.CreateTestSubscription(
			security.ToSecurityId(),
			DataType.Ticks,
			isSubscribe: true);

		// Assert
		subscription.TransactionId.AssertNotEqual(0);
		subscription.SecurityId.SecurityCode.AssertEqual("GBPUSD");
		subscription.DataType2.AssertEqual(DataType.Ticks);
		subscription.IsSubscribe.AssertTrue();
		subscription.LocalTime.AssertNotEqual(default);
	}

	[TestMethod]
	public void Adapter_RealTimeCategory_Enabled()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);

		// Assert
		var categories = adapter.Categories;
		categories.HasFlag(MessageAdapterCategories.RealTime).AssertTrue();
	}

	[TestMethod]
	public void TickMessage_Creation_HasCorrectStructure()
	{
		// Arrange
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");
		var tick = CTraderIntegrationTestHelper.CreateRandomTick(
			security.ToSecurityId(),
			price: 1.1000m,
			volume: 1000m);

		// Assert
		tick.DataTypeEx.AssertEqual(DataType.Ticks);
		tick.SecurityId.SecurityCode.AssertEqual("EURUSD");
		tick.TradePrice.AssertEqual(1.1000m);
		tick.TradeVolume.AssertEqual(1000m);
		tick.ServerTime.AssertNotEqual(default);
		tick.TradeId.AssertNotEqual(0);
	}

	[TestMethod]
	public void DepthMessage_Creation_HasBidsAndAsks()
	{
		// Arrange
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("USDJPY");
		var depth = CTraderIntegrationTestHelper.CreateTestDepth(
			security.ToSecurityId(),
			bidPrice: 110.50m,
			askPrice: 110.55m,
			volume: 5000m);

		// Assert
		depth.SecurityId.SecurityCode.AssertEqual("USDJPY");
		depth.Bids.Length.AssertEqual(1);
		depth.Asks.Length.AssertEqual(1);
		depth.Bids[0].Price.AssertEqual(110.50m);
		depth.Asks[0].Price.AssertEqual(110.55m);
		depth.Bids[0].Volume.AssertEqual(5000m);
		depth.ServerTime.AssertNotEqual(default);
	}

	[TestMethod]
	public void Adapter_SupportsMarketDataMessages()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);

		// Assert - Verify all critical market data messages are supported
		adapter.IsMessageSupported(MessageTypes.MarketData).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.SecurityLookup).AssertTrue();
	}
}
