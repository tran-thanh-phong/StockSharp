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
	/// Main order management sample with choice between event-driven and async approaches
	/// </summary>
	public static async Task CTraderOrdersSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== CTrader Orders Sample ===\n");
		Console.WriteLine("Choose the approach:\n");
		Console.WriteLine("1. Event-Driven Approach (Traditional)");
		Console.WriteLine("2. Async/Await Approach (Modern)");
		Console.WriteLine("3. Compare Both Approaches");
		Console.Write("\nEnter your choice: ");

		var choice = Console.ReadLine();

		switch (choice)
		{
			case "1":
				await CTraderOrdersEventDrivenSampleAsync(connector);
				break;

			case "2":
				await CTraderOrdersAsyncSampleAsync(connector);
				break;

			case "3":
				await CTraderOrdersComparisonSampleAsync(connector);
				break;

			default:
				Console.WriteLine("Invalid choice. Returning to main menu.");
				break;
		}
	}

	/// <summary>
	/// Compare event-driven and async approaches side by side
	/// </summary>
	public static async Task CTraderOrdersComparisonSampleAsync(AsyncConnector connector)
	{
		Console.WriteLine("\n=== Comparison: Event-Driven vs Async Approaches ===\n");

		// Get a security first
		var securities = await connector.GetSecuritiesAsync(new GetSecuritiesRequest("EUR"));
		if (!securities.Any())
		{
			Console.WriteLine("No securities found for symbol 'EUR'");
			return;
		}

		var security = securities.First();
		Console.WriteLine($"Using security: {security.Code}");

		Console.WriteLine("\n=== Feature Comparison ===");
		Console.WriteLine("\nEvent-Driven Approach:");
		Console.WriteLine("  ✓ More control over event flow");
		Console.WriteLine("  ✓ Can handle multiple simultaneous operations");
		Console.WriteLine("  ✓ Good for long-running subscriptions");
		Console.WriteLine("  ✗ More verbose code");
		Console.WriteLine("  ✗ Manual timeout handling required");
		Console.WriteLine("  ✗ Manual cleanup of event handlers");

		Console.WriteLine("\nAsync/Await Approach:");
		Console.WriteLine("  ✓ Clean, readable code");
		Console.WriteLine("  ✓ Built-in timeout support");
		Console.WriteLine("  ✓ Automatic resource cleanup");
		Console.WriteLine("  ✓ Easy error handling with try-catch");
		Console.WriteLine("  ✓ Natural for request-response patterns");
		Console.WriteLine("  ✗ Less control over intermediate events");

		Console.WriteLine("\n\n=== Live Demo ===");
		Console.WriteLine("Retrieving order history using BOTH approaches...\n");

		// Event-Driven Demo
		Console.WriteLine("--- Running Event-Driven Approach ---");
		var startTime1 = DateTime.Now;
		await CTraderGetOrdersEventDrivenAsync(connector, security);
		var elapsed1 = DateTime.Now - startTime1;
		Console.WriteLine($"Event-Driven approach took: {elapsed1.TotalSeconds:F2} seconds");

		Console.WriteLine("\n\n");

		// Async Demo
		Console.WriteLine("--- Running Async Approach ---");
		var startTime2 = DateTime.Now;
		await CTraderGetOrdersAsyncAsync(connector, security);
		var elapsed2 = DateTime.Now - startTime2;
		Console.WriteLine($"Async approach took: {elapsed2.TotalSeconds:F2} seconds");

		Console.WriteLine("\n\n=== Recommendation ===");
		Console.WriteLine("Use Event-Driven when:");
		Console.WriteLine("  • You need real-time updates");
		Console.WriteLine("  • Multiple operations run simultaneously");
		Console.WriteLine("  • You need fine-grained control over events");

		Console.WriteLine("\nUse Async/Await when:");
		Console.WriteLine("  • You have simple request-response operations");
		Console.WriteLine("  • Code readability is a priority");
		Console.WriteLine("  • You want built-in timeout and cancellation");
		Console.WriteLine("  • You're building modern applications");
	}
}
