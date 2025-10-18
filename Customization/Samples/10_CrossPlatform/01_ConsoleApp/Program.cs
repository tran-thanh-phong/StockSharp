using System;
using System.Threading.Tasks;

using Ecng.Logging;

using StockSharp.Customization.Connectors;

namespace StockSharp.Samples.CrossPlatform.ConsoleApp;

static partial class Program
{
	private static async Task Main()
	{
		while (true)
		{
			Console.WriteLine("\n=== StockSharp Cross-Platform Console App ===\n");
			Console.WriteLine("Select a connector to test:\n");
			Console.WriteLine("1. CTrader - All Samples");
			Console.WriteLine("2. CTrader - Securities");
			Console.WriteLine("3. CTrader - Portfolio");
			Console.WriteLine("4. CTrader - Market Data");
			Console.WriteLine("5. CTrader - Orders");
			Console.WriteLine("6. Binance - All Samples");
			Console.WriteLine("0. Exit\n");
			Console.Write("Enter your choice: ");

			var choice = Console.ReadLine();

			try
			{
				switch (choice)
				{
					case "1":
						await RunCTraderSamplesAsync();
						break;

					case "2":
						await RunCTraderSecuritiesSampleAsync();
						break;

					case "3":
						await RunCTraderPortfolioSampleAsync();
						break;

					case "4":
						await RunCTraderMarketDataSampleAsync();
						break;

					case "5":
						await RunCTraderOrdersSampleAsync();
						break;

					case "6":
						await RunBinanceSamplesAsync();
						break;

					case "0":
						Console.WriteLine("Exiting...");
						return;

					default:
						Console.WriteLine("Invalid choice. Please try again.");
						continue;
				}

				Console.WriteLine("\n\nPress Enter to return to main menu...");
				Console.ReadLine();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"\nError: {ex.Message}");
				Console.WriteLine($"StackTrace: {ex.StackTrace}");
				Console.WriteLine("\nPress Enter to return to main menu...");
				Console.ReadLine();
			}
		}
	}

	/// <summary>
	/// Create and configure a connector with common settings
	/// </summary>
	private static AsyncConnector CreateConnector()
	{
		var logger = new LogManager();
		logger.Listeners.Add(new ConsoleLogListener());

		var connector = new AsyncConnector();
		logger.Sources.Add(connector);

		connector.ConnectionError += Console.WriteLine;
		connector.Error += Console.WriteLine;

		// Clear all auto-subscriptions on connect
		// and send necessary subscriptions manually from code
		connector.SubscriptionsOnConnect.Clear();

		connector.SubscriptionStarted += subscription
			=> Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription Started");

		connector.SubscriptionStopped += (subscription, ex)
			=> Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Subscription Stopped, Error: {ex?.Message}");

		return connector;
	}

	// Individual sample runners

	private static async Task RunCTraderSecuritiesSampleAsync()
	{
		var connector = CreateConnector();
		InitCTraderAdapter(connector);
		connector.Connect();
		await Task.Delay(1000);
		await CTraderSecuritiesSampleAsync(connector);
	}

	private static async Task RunCTraderPortfolioSampleAsync()
	{
		var connector = CreateConnector();
		InitCTraderAdapter(connector);
		connector.Connect();
		await Task.Delay(1000);
		await CTraderPortfolioSampleAsync(connector);
	}

	private static async Task RunCTraderMarketDataSampleAsync()
	{
		var connector = CreateConnector();
		InitCTraderAdapter(connector);
		connector.Connect();
		await Task.Delay(1000);
		await CTraderMarketDataSampleAsync(connector);
	}

	private static async Task RunCTraderOrdersSampleAsync()
	{
		var connector = CreateConnector();
		InitCTraderAdapter(connector);
		connector.Connect();
		await Task.Delay(1000);
		await CTraderOrdersSampleAsync(connector);
	}
}
