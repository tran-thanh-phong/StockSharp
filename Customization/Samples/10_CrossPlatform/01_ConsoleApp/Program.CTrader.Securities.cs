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
	/// Sample: Get securities using GetSecuritiesAsync
	/// </summary>
	public static async Task CTraderGetSecuritiesAsync(AsyncConnector connector)
	{
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Getting BTC securities");

		var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest(""));

		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Got BTC securities: {securities.Count}");

		foreach (var item in securities)
		{
			Console.WriteLine(item);
		}
	}

	/// <summary>
	/// Sample: Get multiple securities in parallel
	/// </summary>
	public static async Task CTraderGetMultipleSecuritiesAsync(AsyncConnector connector)
	{
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Getting multiple securities in parallel");

		var task1 = connector.GetSecuritiesAsync(new GetSecuritiesRequest("BTC"));
		var task2 = connector.GetSecuritiesAsync(new GetSecuritiesRequest("ETH"));
		var task3 = connector.GetSecuritiesAsync(new GetSecuritiesRequest("GOLD"));
		var task4 = connector.GetSecuritiesAsync(new GetSecuritiesRequest("LTC"));

		Task.WaitAll(task1, task2, task3, task4);

		var securities = task1.Result.ToList();
		securities.AddRange(task2.Result);
		securities.AddRange(task3.Result);
		securities.AddRange(task4.Result);

		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Got {securities.Count} securities");

		foreach (var item in securities)
		{
			Console.WriteLine(item);
		}
	}

	/// <summary>
	/// Sample: Lookup securities with subscription
	/// </summary>
	public static async Task CTraderSecurityLookupAsync(AsyncConnector connector)
	{
		Console.WriteLine("Securities Lookup:");

		connector.LookupSecuritiesResult += (message, securities, arg3) =>
		{
			Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | LookupSecuritiesResult | Message: {message}, Securities: {securities?.Count() ?? 0}, arg3: {arg3}");

			foreach (var security1 in securities?.Take(10) ?? [])
			{
				Console.WriteLine(security1);
			}
		};

		// Lookup all securities
		connector.Subscribe(new(Extensions.LookupAllCriteriaMessage));
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | LookupAllCriteriaMessage | Subscribe | Securities: {connector.Subscriptions.Count()}");

		await Task.Delay(5000);

		// Lookup specific security by code
		connector.Subscribe(new(new SecurityLookupMessage { SecurityId = new() { SecurityCode = "BTC" } }));
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | SecurityLookupMessage | Subscribe | Securities: {connector.Subscriptions.Count()}");

		await Task.Delay(5000);
	}

	/// <summary>
	/// Main securities sample - combines all security operations
	/// </summary>
	public static async Task CTraderSecuritiesSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Securities Sample ===\n");

		// Get BTC securities
		await CTraderGetSecuritiesAsync(connector);
	}
}
