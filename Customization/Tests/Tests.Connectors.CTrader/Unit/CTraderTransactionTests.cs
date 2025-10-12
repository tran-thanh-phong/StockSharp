using StockSharp.Customization.CTrader.Tests.Helpers;

namespace StockSharp.Customization.CTrader.Tests;

using CTrader.Tests.Helpers;

/// <summary>
/// Tests for cTrader transaction (order/trade) handling.
/// Tests use StockSharp IMessageAdapter interface pattern.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("Transaction")]
public class CTraderTransactionTests
{
	[TestMethod]
	public void CreateOrderRegisterMessage_Market_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateOrderRegisterMessage(
			symbol: "EURUSD",
			side: Sides.Buy,
			volume: 10000m,
			orderType: OrderTypes.Market);

		// Assert
		message.AssertNotNull();
		message.SecurityId.SecurityCode.AssertEqual("EURUSD");
		message.Side.AssertEqual(Sides.Buy);
		message.Volume.AssertEqual(10000m);
		message.OrderType.AssertEqual(OrderTypes.Market);
		message.TransactionId.AssertNotEqual(0L);
	}

	[TestMethod]
	public void CreateOrderRegisterMessage_Limit_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateOrderRegisterMessage(
			symbol: "GBPUSD",
			side: Sides.Sell,
			volume: 5000m,
			orderType: OrderTypes.Limit,
			price: 1.25m);

		// Assert
		message.AssertNotNull();
		message.SecurityId.SecurityCode.AssertEqual("GBPUSD");
		message.Side.AssertEqual(Sides.Sell);
		message.Volume.AssertEqual(5000m);
		message.OrderType.AssertEqual(OrderTypes.Limit);
		message.Price.AssertEqual(1.25m);
	}

	[TestMethod]
	public void CreateOrderRegisterMessage_Stop_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateOrderRegisterMessage(
			symbol: "USDJPY",
			side: Sides.Buy,
			volume: 7500m,
			orderType: OrderTypes.Conditional,
			price: 110.50m);

		// Assert
		message.AssertNotNull();
		message.OrderType.AssertEqual(OrderTypes.Conditional);
		message.Price.AssertEqual(110.50m);
	}

	[TestMethod]
	public void CreateOrderCancelMessage_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreateOrderCancelMessage(orderId: 12345, originalTransactionId: 100);

		// Assert
		message.AssertNotNull();
		message.OrderId.AssertEqual(12345L);
		message.OriginalTransactionId.AssertEqual(100L);
		message.TransactionId.AssertNotEqual(0L);
	}

	[TestMethod]
	public void Adapter_SupportsTransactions()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - Adapter should support transactional messages
		adapter.IsMessageSupported(MessageTypes.OrderRegister).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.OrderCancel).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.OrderStatus).AssertTrue();
	}

	[TestMethod]
	public void Adapter_DoesNotSupportOrderReplace()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert - cTrader doesn't support order modification
		adapter.IsMessageSupported(MessageTypes.OrderReplace).AssertFalse();
	}

	[TestMethod]
	public void CreatePortfolioLookupMessage_GeneratesCorrectStructure()
	{
		// Arrange & Act
		var message = CTraderTestHelper.CreatePortfolioLookupMessage();

		// Assert
		message.AssertNotNull();
		message.TransactionId.AssertNotEqual(0L);
		message.IsSubscribe.AssertTrue();
	}

	[TestMethod]
	public void Adapter_SupportsPortfolioLookup()
	{
		// Arrange
		var adapter = new CTraderMessageAdapter(new IncrementalIdGenerator());

		// Assert
		adapter.IsMessageSupported(MessageTypes.PortfolioLookup).AssertTrue();
	}
}
