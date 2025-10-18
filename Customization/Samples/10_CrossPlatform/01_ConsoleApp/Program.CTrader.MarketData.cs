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
	private static int depthCounter = 0;
	
	/// <summary>
	/// Sample: Subscribe to market depth and display best bid/ask
	/// </summary>
	public static async Task<IOrderBookMessage> CTraderMarketDepthAsync(AsyncConnector connector, Security security)
	{
		IOrderBookMessage lastDepth = null;

		connector.OrderBookReceived += (subscription, depth) =>
		{
			Console.WriteLine(depthCounter);

			Console.WriteLine("Bid: " + depth.GetBestBid());
			Console.WriteLine("Ask: " + depth.GetBestAsk());

			lastDepth = depth;

			if (depthCounter++ > 10)
			{
				connector.UnSubscribe(subscription);				
			}
		};

		connector.Subscribe(new(DataType.MarketDepth, security));

		// Wait for depth data
		await Task.Delay(3000);

		return lastDepth;
	}

	/// <summary>
	/// Main market data sample
	/// </summary>
	public static async Task CTraderMarketDataSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Market Data Sample ===\n");

		// Get a security first
		var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest("BTC"));
		var security = securities.First();

		Console.WriteLine($"Getting market depth for {security.Code}");

		var depth = await CTraderMarketDepthAsync(connector, security);

		if (depth != null)
		{
			Console.WriteLine("\nMarket depth received successfully");
			Console.WriteLine($"Best Bid: {depth.GetBestBid()?.Price}");
			Console.WriteLine($"Best Ask: {depth.GetBestAsk()?.Price}");
		}
	}
}
