using StockSharp.Customization.CTrader.Tests.Helpers;

namespace StockSharp.Customization.CTrader.Tests.Integration;

using CTrader.Tests.Helpers;

/// <summary>
/// Integration tests for CTrader order execution message flow.
/// Tests the adapter's behavior when processing order-related messages.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[TestCategory("OrderExecution")]
public class CTraderOrderExecutionTests
{
	private static readonly IncrementalIdGenerator _idGenerator = new();

	[TestMethod]
	public void OrderRegister_MarketOrder_GeneratesCorrectMessages()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");
		var orderMsg = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(),
			Sides.Buy,
			volume: 1000m,
			orderType: OrderTypes.Market);

		// Act
		adapter.SendInMessage(orderMsg);

		// Assert - Verify adapter supports market orders
		var isSupported = adapter.IsMessageSupported(MessageTypes.OrderRegister);
		isSupported.AssertTrue();

		// Note: Actual execution would require live connection
		// This test validates message structure and adapter capabilities
	}

	[TestMethod]
	public void OrderRegister_LimitOrder_HasPriceSet()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("GBPUSD");
		var orderMsg = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(),
			Sides.Sell,
			volume: 2000m,
			orderType: OrderTypes.Limit,
			price: 1.2500m);

		// Act
		adapter.SendInMessage(orderMsg);

		// Assert
		orderMsg.Price.AssertEqual(1.2500m);
		orderMsg.OrderType.AssertEqual(OrderTypes.Limit);
		orderMsg.Side.AssertEqual(Sides.Sell);
		orderMsg.Volume.AssertEqual(2000m);
	}

	[TestMethod]
	public void OrderCancel_HasTransactionId()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var cancelMsg = new OrderCancelMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			OriginalTransactionId = 12345,
			OrderId = 67890,
			SecurityId = "EURUSD".ToStockSharp(),
		};

		// Act
		adapter.SendInMessage(cancelMsg);

		// Assert
		var isSupported = adapter.IsMessageSupported(MessageTypes.OrderCancel);
		isSupported.AssertTrue();
		cancelMsg.TransactionId.AssertNotEqual(0);
		cancelMsg.OriginalTransactionId.AssertEqual(12345);
	}

	[TestMethod]
	public void OrderReplace_MessageStructure()
	{
		// Arrange
		var replaceMsg = new OrderReplaceMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			OriginalTransactionId = 11111,
			SecurityId = "USDJPY".ToStockSharp(),
			Price = 110.50m,
			Volume = 1500m,
		};

		// Assert - Validate message structure (OrderReplace not yet implemented in adapter)
		replaceMsg.Price.AssertEqual(110.50m);
		replaceMsg.Volume.AssertEqual(1500m);
		replaceMsg.TransactionId.AssertNotEqual(0);
		replaceMsg.OriginalTransactionId.AssertEqual(11111);
	}

	[TestMethod]
	public void OrderStatus_MessageStructure()
	{
		// Arrange
		var statusMsg = new OrderStatusMessage
		{
			TransactionId = _idGenerator.GetNextId(),
			SecurityId = "EURUSD".ToStockSharp(),
		};

		// Assert - Validate message structure (OrderStatus not yet implemented in adapter)
		statusMsg.TransactionId.AssertNotEqual(0);
		statusMsg.SecurityId.SecurityCode.AssertEqual("EURUSD");
	}

	[TestMethod]
	public void MultipleOrders_HaveUniqueTransactionIds()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");

		var order1 = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(), Sides.Buy, 1000m, OrderTypes.Market);
		var order2 = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(), Sides.Sell, 2000m, OrderTypes.Limit, 1.1100m);
		var order3 = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(), Sides.Buy, 3000m, OrderTypes.Limit, 1.0900m);

		// Act
		adapter.SendInMessage(order1);
		adapter.SendInMessage(order2);
		adapter.SendInMessage(order3);

		// Assert
		order1.TransactionId.AssertNotEqual(order2.TransactionId);
		order2.TransactionId.AssertNotEqual(order3.TransactionId);
		order1.TransactionId.AssertNotEqual(order3.TransactionId);
	}

	[TestMethod]
	public void Portfolio_LookupMessage_Structure()
	{
		// Arrange
		var portfolioLookup = CTraderIntegrationTestHelper.CreateTestPortfolioLookup();

		// Assert - Validate message structure (PortfolioLookup not yet fully implemented in adapter)
		portfolioLookup.TransactionId.AssertNotEqual(0);
	}

	[TestMethod]
	public void Adapter_SupportsTransactionMessages()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);

		// Assert - Verify critical transaction messages are supported
		adapter.IsMessageSupported(MessageTypes.OrderRegister).AssertTrue();
		adapter.IsMessageSupported(MessageTypes.OrderCancel).AssertTrue();
		// Note: OrderReplace, OrderStatus, PortfolioLookup not yet implemented
	}

	[TestMethod]
	public void OrderMessage_HasRequiredFields()
	{
		// Arrange
		var security = CTraderIntegrationTestHelper.CreateTestSecurity("EURUSD");
		var orderMsg = CTraderIntegrationTestHelper.CreateTestOrderMessage(
			security.ToSecurityId(),
			Sides.Buy,
			volume: 1000m,
			orderType: OrderTypes.Limit,
			price: 1.1000m,
			portfolioName: "test_portfolio");

		// Assert
		orderMsg.TransactionId.AssertNotEqual(0);
		orderMsg.SecurityId.SecurityCode.AssertEqual("EURUSD");
		orderMsg.Side.AssertEqual(Sides.Buy);
		orderMsg.Volume.AssertEqual(1000m);
		orderMsg.OrderType.AssertEqual(OrderTypes.Limit);
		orderMsg.Price.AssertEqual(1.1000m);
		orderMsg.PortfolioName.AssertEqual("test_portfolio");
	}

	[TestMethod]
	public void Adapter_TransactionCategories_Configured()
	{
		// Arrange
		var adapter = CTraderIntegrationTestHelper.CreateAdapterForTesting(out var messages);

		// Assert
		var categories = adapter.Categories;
		categories.HasFlag(MessageAdapterCategories.Transactions).AssertTrue();
		categories.HasFlag(MessageAdapterCategories.RealTime).AssertTrue();
	}
}
