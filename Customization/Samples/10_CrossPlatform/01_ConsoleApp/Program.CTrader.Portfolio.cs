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
	/// Sample: Display all portfolios
	/// </summary>
	public static void CTraderShowPortfolios(AsyncConnector connector)
	{
		Console.WriteLine("Portfolios: " + connector.Portfolios.Count());

		foreach (var portfolio in connector.Portfolios)
		{
			Console.WriteLine(portfolio);
		}
	}

	/// <summary>
	/// Sample: Subscribe to position changes for a security
	/// </summary>
	public static async Task CTraderPositionChangesAsync(AsyncConnector connector, Security security)
	{
		Console.WriteLine($"Subscribing to position changes for {security.Code}");

		connector.PositionReceived += (subscription, position) =>
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Position: {position}");
			connector.UnSubscribe(subscription);
		};

		connector.Subscribe(new(DataType.PositionChanges, security));

		await Task.Delay(2000);
	}

	/// <summary>
	/// Main portfolio sample
	/// </summary>
	public static async Task CTraderPortfolioSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Portfolio Sample ===\n");

		CTraderShowPortfolios(connector);

		if (!connector.Portfolios.Any())
		{
			Console.WriteLine("No portfolios available. Getting securities to subscribe for positions...");
			var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest("BTC"));
			var security = securities.FirstOrDefault();

			if (security != null)
			{
				await CTraderPositionChangesAsync(connector, security);
			}
		}
	}
}
