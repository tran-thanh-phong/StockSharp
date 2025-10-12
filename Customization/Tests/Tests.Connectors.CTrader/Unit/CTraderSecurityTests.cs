using StockSharp.Customization.CTrader.Tests.Helpers;

namespace StockSharp.Customization.CTrader.Tests;

using CTrader.Tests.Helpers;

/// <summary>
/// Tests for cTrader security lookup and symbol management.
/// Tests use StockSharp IMessageAdapter interface pattern.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CTraderSecurityTests
{
	[TestMethod]
	public void CreateSecurityLookupMessage_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateSecurityLookupMessage();

		// Assert
		message.AssertNotNull();
		message.TransactionId.AssertNotEqual(0L);
	}

	[TestMethod]
	public void SecurityId_FromSymbolName_CreatesCorrectStructure()
	{
		// Arrange & Act
		var securityId = "EURUSD".ToStockSharp();

		// Assert
		securityId.SecurityCode.AssertEqual("EURUSD");
		securityId.BoardCode.AssertEqual("CTRADER");
	}

	[TestMethod]
	public void Adapter_SupportedBoards_ContainsCTrader()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		adapter.AssociatedBoards.AssertNotNull();
		adapter.AssociatedBoards.Length.AssertGreater(0);
		adapter.AssociatedBoards.Contains("CTRADER").AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportsSecurityLookup()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - Adapter should support security lookup messages
		var supportsSecurityLookup = adapter.IsMessageSupported(MessageTypes.SecurityLookup);
		supportsSecurityLookup.AssertTrue();
	}

	[TestMethod]
	public void Adapter_IsAllDownloadingSupported_ForSecurities()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Act
		var isSupported = adapter.IsAllDownloadingSupported(DataType.Securities);

		// Assert
		isSupported.AssertTrue();
	}
}
