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
	/// Sample: Register a buy order
	/// </summary>
	public static async Task CTraderRegisterOrderAsync(AsyncConnector connector, Security security, IOrderBookMessage lastDepth)
	{
		Console.WriteLine("Order:");
		Console.Write("Do you want to buy 1? (Y/N): ");

		var str = Console.ReadLine();
		if (str == null || str.ToUpper() != "Y")
		{
			Console.WriteLine("Order cancelled by user.");
			return;
		}

		var bestBidPrice = lastDepth?.GetBestBid()?.Price;

		var order = new Order
		{
			Security = security,
			Portfolio = connector.Portfolios.First(),
			Price = bestBidPrice ?? 0,
			Type = bestBidPrice == null ? OrderTypes.Market : OrderTypes.Limit,
			Volume = 1m,
			Side = Sides.Buy,
		};

		connector.OrderReceived += (s, o) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Order: {o}");

		Console.WriteLine($"Registering order: {order.Type} {order.Side} {order.Volume} @ {order.Price}");
		connector.RegisterOrder(order);

		await Task.Delay(2000);
	}

	/// <summary>
	/// Main order management sample
	/// </summary>
	public static async Task CTraderOrdersSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Orders Sample ===\n");

		// Get a security first
		var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest("BTC"));
		var security = securities.First();

		// Get market depth to determine order price
		var depth = await CTraderMarketDepthAsync(connector, security);

		if (depth == null)
		{
			Console.WriteLine("No market depth available. Cannot place order.");
			return;
		}

		// Register order
		await CTraderRegisterOrderAsync(connector, security, depth);
	}
}
