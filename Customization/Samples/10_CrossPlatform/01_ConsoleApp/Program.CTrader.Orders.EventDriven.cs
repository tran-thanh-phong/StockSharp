using System;
using System.Collections.Generic;
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
	/// Sample: Register order using event-driven approach
	/// </summary>
	public static async Task CTraderRegisterOrderEventDrivenAsync(AsyncConnector connector, Security security, decimal price)
	{
		Console.WriteLine("\n=== Event-Driven Order Registration ===\n");

		var order = new Order
		{
			Security = security,
			Portfolio = connector.Portfolios.First(),
			Price = price,
			Type = OrderTypes.Limit,
			Volume = 0.01m, // Small volume for testing
			Side = Sides.Buy,
		};

		var orderCompleted = false;

		// Subscribe to order events
		void orderHandler(Subscription s, Order o)
		{
			if (o.TransactionId == order.TransactionId)
			{
				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order Event: {o.State} | Price: {o.Price} | Volume: {o.Volume}");

				if (o.State == OrderStates.Active || o.State == OrderStates.Done || o.State == OrderStates.Failed)
				{
					orderCompleted = true;
				}
			}
		}

		void orderFailHandler(Subscription s, OrderFail fail)
		{
			if (fail.Order.TransactionId == order.TransactionId)
			{
				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order Failed: {fail.Error}");
				orderCompleted = true;
			}
		}

		connector.OrderReceived += orderHandler;
		connector.OrderRegisterFailReceived += orderFailHandler;

		try
		{
			Console.WriteLine($"Registering order: {order.Type} {order.Side} {order.Volume} @ {order.Price}");
			connector.RegisterOrder(order);

			// Wait for order to complete
			var timeout = DateTime.Now.AddSeconds(30);
			while (!orderCompleted && DateTime.Now < timeout)
			{
				await Task.Delay(100);
			}

			if (!orderCompleted)
			{
				Console.WriteLine("Order registration timed out");
			}
		}
		finally
		{
			connector.OrderReceived -= orderHandler;
			connector.OrderRegisterFailReceived -= orderFailHandler;
		}
	}

	/// <summary>
	/// Sample: Cancel order using event-driven approach
	/// </summary>
	public static async Task CTraderCancelOrderEventDrivenAsync(AsyncConnector connector, Order order)
	{
		Console.WriteLine("\n=== Event-Driven Order Cancellation ===\n");

		if (order.Id == null)
		{
			Console.WriteLine("Cannot cancel order: Order.Id is null");
			return;
		}

		var orderCancelled = false;

		void orderHandler(Subscription s, Order o)
		{
			if (o.Id == order.Id)
			{
				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order State: {o.State}");

				if (o.State == OrderStates.Done)
				{
					Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order cancelled successfully");
					orderCancelled = true;
				}
			}
		}

		connector.OrderReceived += orderHandler;

		try
		{
			Console.WriteLine($"Cancelling order {order.Id}...");
			connector.CancelOrder(order);

			// Wait for cancellation to complete
			var timeout = DateTime.Now.AddSeconds(30);
			while (!orderCancelled && DateTime.Now < timeout)
			{
				await Task.Delay(100);
			}

			if (!orderCancelled)
			{
				Console.WriteLine("Order cancellation timed out");
			}
		}
		finally
		{
			connector.OrderReceived -= orderHandler;
		}
	}

	/// <summary>
	/// Sample: Get order history using event-driven approach with subscription
	/// </summary>
	public static async Task CTraderGetOrdersEventDrivenAsync(AsyncConnector connector, Security security)
	{
		Console.WriteLine("\n=== Event-Driven Order History Retrieval ===\n");

		var orders = new List<Order>();
		var subscriptionCompleted = false;
		Subscription targetSubscription = null;

		void orderHandler(Subscription s, Order o)
		{
			if (s == targetSubscription)
			{
				orders.Add(o);
				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order Received: {o.Id} | {o.State} | {o.Side} | {o.Volume}@{o.Price}");
			}
		}

		void subscriptionStoppedHandler(Subscription s, Exception ex)
		{
			if (s == targetSubscription)
			{
				if (ex == null)
				{
					Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription completed successfully");
					subscriptionCompleted = true;
				}
				else
				{
					Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription stopped with error: {ex.Message}");
					subscriptionCompleted = true;
				}
			}
		}

		connector.OrderReceived += orderHandler;
		connector.SubscriptionStopped += subscriptionStoppedHandler;

		try
		{
			// Create order lookup message
			var message = new OrderStatusMessage
			{
				SecurityId = security.ToSecurityId(),
				IsSubscribe = true
			};

			targetSubscription = new Subscription(message);

			Console.WriteLine($"Subscribing to order history for {security.Code}...");
			connector.Subscribe(targetSubscription);

			// Wait for subscription to complete
			var timeout = DateTime.Now.AddSeconds(30);
			while (!subscriptionCompleted && DateTime.Now < timeout)
			{
				await Task.Delay(100);
			}

			if (!subscriptionCompleted)
			{
				Console.WriteLine("Order history retrieval timed out");
			}

			Console.WriteLine($"\nTotal orders received: {orders.Count}");
		}
		finally
		{
			connector.OrderReceived -= orderHandler;
			connector.SubscriptionStopped -= subscriptionStoppedHandler;
		}
	}

	/// <summary>
	/// Sample: Get trade history using event-driven approach with subscription
	/// </summary>
	public static async Task CTraderGetTradesEventDrivenAsync(AsyncConnector connector, Security security)
	{
		Console.WriteLine("\n=== Event-Driven Trade History Retrieval ===\n");

		var trades = new List<MyTrade>();
		var subscriptionCompleted = false;
		Subscription targetSubscription = null;

		void tradeHandler(Subscription s, MyTrade t)
		{
			if (s == targetSubscription)
			{
				trades.Add(t);
				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Trade Received: {t.Trade.Id} | {t.Trade.Price} | {t.Trade.Volume}");
			}
		}

		void subscriptionStoppedHandler(Subscription s, Exception ex)
		{
			if (s == targetSubscription)
			{
				if (ex == null)
				{
					Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription completed successfully");
					subscriptionCompleted = true;
				}
				else
				{
					Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription stopped with error: {ex.Message}");
					subscriptionCompleted = true;
				}
			}
		}

		connector.OwnTradeReceived += tradeHandler;
		connector.SubscriptionStopped += subscriptionStoppedHandler;

		try
		{
			// Create trade lookup message (using OrderStatusMessage)
			var message = new OrderStatusMessage
			{
				SecurityId = security.ToSecurityId(),
				IsSubscribe = true
			};

			targetSubscription = new Subscription(message);

			Console.WriteLine($"Subscribing to trade history for {security.Code}...");
			connector.Subscribe(targetSubscription);

			// Wait for subscription to complete
			var timeout = DateTime.Now.AddSeconds(30);
			while (!subscriptionCompleted && DateTime.Now < timeout)
			{
				await Task.Delay(100);
			}

			if (!subscriptionCompleted)
			{
				Console.WriteLine("Trade history retrieval timed out");
			}

			Console.WriteLine($"\nTotal trades received: {trades.Count}");
		}
		finally
		{
			connector.OwnTradeReceived -= tradeHandler;
			connector.SubscriptionStopped -= subscriptionStoppedHandler;
		}
	}

	/// <summary>
	/// Main event-driven orders sample runner
	/// </summary>
	public static async Task CTraderOrdersEventDrivenSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Orders (Event-Driven) Sample ===\n");

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
		Console.WriteLine("\n--- Order Operations Menu ---");
		Console.WriteLine("1. Register Order (Event-Driven)");
		Console.WriteLine("2. Get Order History (Event-Driven)");
		Console.WriteLine("3. Get Trade History (Event-Driven)");
		Console.Write("\nEnter choice (or press Enter to skip): ");

		var choice = Console.ReadLine();

		switch (choice)
		{
			case "1":
				await CTraderRegisterOrderEventDrivenAsync(connector, security, bestBid);
				break;

			case "2":
				await CTraderGetOrdersEventDrivenAsync(connector, security);
				break;

			case "3":
				await CTraderGetTradesEventDrivenAsync(connector, security);
				break;

			default:
				Console.WriteLine("Skipping order operations demo");
				break;
		}
	}
}
