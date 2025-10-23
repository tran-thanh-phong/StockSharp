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
	/// Sample: Test GetPortfoliosAsync method
	/// </summary>
	public static async Task CTraderGetPortfoliosAsyncSample(AsyncConnector connector)
	{
		Console.WriteLine("\n--- Testing GetPortfoliosAsync() ---\n");

		try
		{
			// Test 1: Get all portfolios
			Console.WriteLine("Test 1: Getting all portfolios...");
			var portfolios = await connector.GetPortfoliosAsync();

			Console.WriteLine($"Found {portfolios.Count} portfolio(s):");
			foreach (var portfolio in portfolios)
			{
				Console.WriteLine($"  - {portfolio.Name}");
				Console.WriteLine($"    Current Value: {portfolio.CurrentValue}");
				Console.WriteLine($"    Begin Value: {portfolio.BeginValue}");
				Console.WriteLine($"    Leverage: {portfolio.Leverage}");
			}

			// Test 2: Test with filter if portfolios exist
			if (portfolios.Any())
			{
				var firstPortfolio = portfolios.First();
				Console.WriteLine($"\nTest 2: Getting portfolio with filter (Name='{firstPortfolio.Name}')...");

				var filtered = await connector.GetPortfoliosAsync(
					new GetPortfoliosRequest { PortfolioName = firstPortfolio.Name });

				Console.WriteLine($"Filtered result: {filtered.Count} portfolio(s)");
				foreach (var portfolio in filtered)
				{
					Console.WriteLine($"  - {portfolio.Name}: {portfolio.CurrentValue}");
				}
			}
			else
			{
				Console.WriteLine("\nNo portfolios found, skipping filter test.");
			}

			// Test 3: Test with non-existent portfolio name
			Console.WriteLine("\nTest 3: Testing with non-existent portfolio name...");
			var empty = await connector.GetPortfoliosAsync(
				new GetPortfoliosRequest { PortfolioName = "NonExistentPortfolio123" });
			Console.WriteLine($"Result: {empty.Count} portfolio(s) (expected 0)");

			Console.WriteLine("\n✓ GetPortfoliosAsync() tests completed successfully!");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"\n✗ Error during GetPortfoliosAsync() test: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
		}
	}

	/// <summary>
	/// Main portfolio sample
	/// </summary>
	public static async Task CTraderPortfolioSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Portfolio Sample ===\n");

		// Show portfolios using direct access
		Console.WriteLine("--- Direct Portfolio Access (cached) ---");
		CTraderShowPortfolios(connector);

		// Test async method
		await CTraderGetPortfoliosAsyncSample(connector);

		// Original position changes demo
		if (!connector.Portfolios.Any())
		{
			Console.WriteLine("\n--- Position Changes Demo ---");
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
