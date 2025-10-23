using System;
using System.Linq;
using System.Threading.Tasks;

using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Customization.Connectors;
using StockSharp.Messages;

namespace StockSharp.Samples.CrossPlatform.ConsoleApp;

static partial class Program
{
	/// <summary>
	/// Sample: Register order using async/await approach
	/// </summary>
	public static async Task CTraderRegisterOrderAsyncAsync(AsyncConnector connector, Security security, decimal price)
	{
		Console.WriteLine("\n=== Async Order Registration ===\n");

		var order = new Order
		{
			Security = security,
			Portfolio = connector.Portfolios.First(),
			Price = price,
			Type = OrderTypes.Limit,
			Volume = 0.01m, // Small volume for testing
			Side = Sides.Buy,
		};

		try
		{
			Console.WriteLine($"Registering order: {order.Type} {order.Side} {order.Volume} @ {order.Price}");

			var request = new RegisterOrderRequest(order);
			var registeredOrder = await connector.RegisterOrderAsync(request, timeout: TimeSpan.FromSeconds(30));

			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order registered successfully!");
			Console.WriteLine($"  Order ID: {registeredOrder.Id}");
			Console.WriteLine($"  State: {registeredOrder.State}");
			Console.WriteLine($"  Price: {registeredOrder.Price}");
			Console.WriteLine($"  Volume: {registeredOrder.Volume}");
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order registration timed out: {ex.Message}");
		}
		catch (InvalidOperationException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order registration failed: {ex.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Unexpected error: {ex.Message}");
		}
	}

	/// <summary>
	/// Sample: Cancel order using async/await approach
	/// </summary>
	public static async Task CTraderCancelOrderAsyncAsync(AsyncConnector connector, Order order)
	{
		Console.WriteLine("\n=== Async Order Cancellation ===\n");

		try
		{
			Console.WriteLine($"Cancelling order {order.Id}...");

			var request = new CancelOrderRequest(order);
			var cancelledOrder = await connector.CancelOrderAsync(request, timeout: TimeSpan.FromSeconds(30));

			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order cancelled successfully!");
			Console.WriteLine($"  Order ID: {cancelledOrder.Id}");
			Console.WriteLine($"  State: {cancelledOrder.State}");
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order cancellation timed out: {ex.Message}");
		}
		catch (InvalidOperationException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order cancellation failed: {ex.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Unexpected error: {ex.Message}");
		}
	}

	/// <summary>
	/// Sample: Get order history using async/await approach
	/// </summary>
	public static async Task CTraderGetOrdersAsyncAsync(AsyncConnector connector, Security security)
	{
		Console.WriteLine("\n=== Async Order History Retrieval ===\n");

		try
		{
			Console.WriteLine($"Retrieving order history for {security.Code}...");

			// Example 1: Get all orders for the security
			var request1 = new GetOrdersRequest
			{
				Security = security
			};

			var orders = await connector.GetOrdersAsync(request1, timeout: TimeSpan.FromSeconds(30));

			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Orders retrieved successfully!");
			Console.WriteLine($"Total orders: {orders.Count}");

			foreach (var order in orders.Take(10))
			{
				Console.WriteLine($"  Order {order.Id}: {order.State} | {order.Side} | {order.Volume}@{order.Price}");
			}

			if (orders.Count > 10)
			{
				Console.WriteLine($"  ... and {orders.Count - 10} more orders");
			}

			// Example 2: Get only active orders
			Console.WriteLine("\n--- Active Orders Only ---");
			var request2 = new GetOrdersRequest
			{
				Security = security,
				States = new[] { OrderStates.Active }
			};

			var activeOrders = await connector.GetOrdersAsync(request2, timeout: TimeSpan.FromSeconds(30));
			Console.WriteLine($"Active orders: {activeOrders.Count}");

			// Example 3: Get orders within date range
			Console.WriteLine("\n--- Orders from Last 7 Days ---");
			var request3 = new GetOrdersRequest
			{
				Security = security,
				From = DateTimeOffset.Now.AddDays(-7),
				To = DateTimeOffset.Now
			};

			var recentOrders = await connector.GetOrdersAsync(request3, timeout: TimeSpan.FromSeconds(30));
			Console.WriteLine($"Recent orders: {recentOrders.Count}");

			// Example 4: Get orders for specific portfolio
			if (connector.Portfolios.Any())
			{
				Console.WriteLine("\n--- Orders for Specific Portfolio ---");
				var request4 = new GetOrdersRequest
				{
					Security = security,
					Portfolio = connector.Portfolios.First()
				};

				var portfolioOrders = await connector.GetOrdersAsync(request4, timeout: TimeSpan.FromSeconds(30));
				Console.WriteLine($"Portfolio orders: {portfolioOrders.Count}");
			}
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order history retrieval timed out: {ex.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Unexpected error: {ex.Message}");
		}
	}

	/// <summary>
	/// Sample: Get trade history using async/await approach
	/// </summary>
	public static async Task CTraderGetTradesAsyncAsync(AsyncConnector connector, Security security)
	{
		Console.WriteLine("\n=== Async Trade History Retrieval ===\n");

		try
		{
			Console.WriteLine($"Retrieving trade history for {security.Code}...");

			// Example 1: Get all trades for the security
			var request1 = new GetTradesRequest
			{
				Security = security
			};

			var trades = await connector.GetTradesAsync(request1, timeout: TimeSpan.FromSeconds(30));

			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Trades retrieved successfully!");
			Console.WriteLine($"Total trades: {trades.Count}");

			foreach (var trade in trades.Take(10))
			{
				Console.WriteLine($"  Trade {trade.Trade.Id}: {trade.Trade.Price} | Volume: {trade.Trade.Volume} | Time: {trade.Trade.ServerTime}");
			}

			if (trades.Count > 10)
			{
				Console.WriteLine($"  ... and {trades.Count - 10} more trades");
			}

			// Example 2: Get trades within date range
			Console.WriteLine("\n--- Trades from Last 7 Days ---");
			var request2 = new GetTradesRequest
			{
				Security = security,
				From = DateTimeOffset.Now.AddDays(-7),
				To = DateTimeOffset.Now
			};

			var recentTrades = await connector.GetTradesAsync(request2, timeout: TimeSpan.FromSeconds(30));
			Console.WriteLine($"Recent trades: {recentTrades.Count}");

			// Example 3: Get trades for specific portfolio
			if (connector.Portfolios.Any())
			{
				Console.WriteLine("\n--- Trades for Specific Portfolio ---");
				var request3 = new GetTradesRequest
				{
					Security = security,
					Portfolio = connector.Portfolios.First()
				};

				var portfolioTrades = await connector.GetTradesAsync(request3, timeout: TimeSpan.FromSeconds(30));
				Console.WriteLine($"Portfolio trades: {portfolioTrades.Count}");
			}

			// Calculate statistics
			if (trades.Any())
			{
				Console.WriteLine("\n--- Trade Statistics ---");
				var totalVolume = trades.Sum(t => t.Trade.Volume);
				var avgPrice = trades.Average(t => t.Trade.Price);
				Console.WriteLine($"Total Volume: {totalVolume}");
				Console.WriteLine($"Average Price: {avgPrice:F2}");
			}
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Trade history retrieval timed out: {ex.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Unexpected error: {ex.Message}");
		}
	}

	/// <summary>
	/// Comprehensive async order workflow demo
	/// </summary>
	public static async Task CTraderAsyncOrderWorkflowAsync(AsyncConnector connector, Security security, decimal price)
	{
		Console.WriteLine("\n=== Complete Async Order Workflow ===\n");

		try
		{
			// Step 1: Register order
			Console.WriteLine("Step 1: Registering order...");
			var order = new Order
			{
				Security = security,
				Portfolio = connector.Portfolios.First(),
				Price = price,
				Type = OrderTypes.Limit,
				Volume = 0.01m,
				Side = Sides.Buy,
			};

			var registeredOrder = await connector.RegisterOrderAsync(
				new RegisterOrderRequest(order),
				timeout: TimeSpan.FromSeconds(30));

			Console.WriteLine($"Order registered: {registeredOrder.Id} | State: {registeredOrder.State}");

			// Step 2: Wait a bit
			await Task.Delay(2000);

			// Step 3: Cancel order (if still active)
			if (registeredOrder.State == OrderStates.Active && registeredOrder.Id.HasValue)
			{
				Console.WriteLine("\nStep 2: Cancelling order...");
				var cancelledOrder = await connector.CancelOrderAsync(
					new CancelOrderRequest(registeredOrder),
					timeout: TimeSpan.FromSeconds(30));

				Console.WriteLine($"Order cancelled: {cancelledOrder.Id} | State: {cancelledOrder.State}");
			}

			// Step 4: Verify in order history
			Console.WriteLine("\nStep 3: Verifying in order history...");
			var orders = await connector.GetOrdersAsync(
				new GetOrdersRequest { Security = security },
				timeout: TimeSpan.FromSeconds(30));

			var foundOrder = orders.FirstOrDefault(o => o.Id == registeredOrder.Id);
			if (foundOrder != null)
			{
				Console.WriteLine($"Order found in history: {foundOrder.Id} | Final State: {foundOrder.State}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in workflow: {ex.Message}");
		}
	}

	/// <summary>
	/// Main async orders sample runner
	/// </summary>
	public static async Task CTraderOrdersAsyncSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Orders (Async) Sample ===\n");

		// Get a security first
		var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest("EUR"));
		if (!securities.Any())
		{
			Console.WriteLine("No securities found for symbol 'EUR'");
			return;
		}

		var security = securities.First();
		Console.WriteLine($"Using security: {security.Code}");

		// Get market depth to determine order price
		var depth = await CTraderMarketDepthAsync(connector, security);
		if (depth == null)
		{
			Console.WriteLine("No market depth available");
			return;
		}

		var bestBid = depth.GetBestBid()?.Price ?? 0;
		var bestAsk = depth.GetBestAsk()?.Price ?? 0;
		Console.WriteLine($"Best Bid: {bestBid}, Best Ask: {bestAsk}");

		// Demo order operations
		Console.WriteLine("\n--- Async Order Operations Menu ---");
		Console.WriteLine("1. Register Order (Async)");
		Console.WriteLine("2. Get Order History (Async with Filters)");
		Console.WriteLine("3. Get Trade History (Async with Filters)");
		Console.WriteLine("4. Complete Workflow (Register -> Cancel -> Verify)");
		Console.Write("\nEnter choice (or press Enter to skip): ");

		var choice = Console.ReadLine();

		switch (choice)
		{
			case "1":
				await CTraderRegisterOrderAsyncAsync(connector, security, bestBid);
				break;

			case "2":
				await CTraderGetOrdersAsyncAsync(connector, security);
				break;

			case "3":
				await CTraderGetTradesAsyncAsync(connector, security);
				break;

			case "4":
				await CTraderAsyncOrderWorkflowAsync(connector, security, bestBid);
				break;

			default:
				Console.WriteLine("Skipping async order operations demo");
				break;
		}
	}
}
