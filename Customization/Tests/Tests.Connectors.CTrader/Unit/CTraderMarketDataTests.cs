namespace StockSharp.CTraderConnector.Tests;

using StockSharp.CTraderConnector.Tests.Helpers;

/// <summary>
/// Tests for cTrader market data subscriptions and processing.
/// Tests use StockSharp IMessageAdapter interface pattern.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("MarketData")]
public class CTraderMarketDataTests
{
	[TestMethod]
	public void CreateMarketDataSubscription_Ticks_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateMarketDataSubscription("EURUSD", DataType.Ticks);

		// Assert
		message.AssertNotNull();
		message.SecurityId.SecurityCode.AssertEqual("EURUSD");
		message.DataType2.AssertEqual(DataType.Ticks);
		message.IsSubscribe.AssertTrue();
	}

	[TestMethod]
	public void CreateMarketDataSubscription_MarketDepth_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateMarketDataSubscription("GBPUSD", DataType.MarketDepth);

		// Assert
		message.AssertNotNull();
		message.SecurityId.SecurityCode.AssertEqual("GBPUSD");
		message.DataType2.AssertEqual(DataType.MarketDepth);
		message.IsSubscribe.AssertTrue();
	}

	[TestMethod]
	public void CreateMarketDataSubscription_Level1_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateMarketDataSubscription("USDJPY", DataType.Level1);

		// Assert
		message.AssertNotNull();
		message.SecurityId.SecurityCode.AssertEqual("USDJPY");
		message.DataType2.AssertEqual(DataType.Level1);
		message.IsSubscribe.AssertTrue();
	}

	[TestMethod]
	public void CreateMarketDataSubscription_Unsubscribe()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateMarketDataSubscription("EURUSD", DataType.Ticks, isSubscribe: false);

		// Assert
		message.IsSubscribe.AssertFalse();
	}

	[TestMethod]
	public void Adapter_SupportedDataTypes_IncludesTicks()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Act
		var supportedTypes = adapter.GetSupportedMarketDataTypes(default, null, null);

		// Assert
		supportedTypes.Contains(DataType.Ticks).AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportedDataTypes_IncludesMarketDepth()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Act
		var supportedTypes = adapter.GetSupportedMarketDataTypes(default, null, null);

		// Assert
		supportedTypes.Contains(DataType.MarketDepth).AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportedDataTypes_IncludesLevel1()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Act
		var supportedTypes = adapter.GetSupportedMarketDataTypes(default, null, null);

		// Assert
		supportedTypes.Contains(DataType.Level1).AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportedTimeFrames_ContainsStandardIntervals()
	{
		// Arrange & Act
		var timeFrames = CTraderMessageAdapter.AllTimeFrames.ToList();

		// Assert
		timeFrames.Count.AssertGreater(0);
		timeFrames.Contains(TimeSpan.FromMinutes(1)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromMinutes(5)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromMinutes(15)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromMinutes(30)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromHours(1)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromHours(4)).AssertTrue();
		timeFrames.Contains(TimeSpan.FromDays(1)).AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportsMarketDataMessages()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		adapter.IsMessageSupported(MessageTypes.MarketData).AssertTrue();
	}
}
