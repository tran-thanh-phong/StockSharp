using System;
using System.Threading.Tasks;

using Ecng.Common;

using StockSharp.Binance;
using StockSharp.Customization.Connectors;

namespace StockSharp.Samples.CrossPlatform.ConsoleApp;

static partial class Program
{
	/// <summary>
	/// Initialize Binance connector with demo credentials
	/// NOTE: Replace with your own credentials
	/// </summary>
	private static void InitBinanceAdapter(AsyncConnector connector)
	{
		connector.Adapter.InnerAdapters.Add(new BinanceMessageAdapter(connector.TransactionIdGenerator)
		{
			Key = "c0QDqcdJQGZT5YOl95oB8dhftMgJJx3aaf2ODDBclVgugxwU1q8fbU0UeWGuTsVa".Secure(),
			Secret = "JyxJyk56j3VeBBhju3WC3VUQRqfuw4sAIW7sFQLhiJgSAShbE78e1AYTBaqEIbTE".Secure(),
			IsDemo = true,
			// Available sections: BinanceSections.Futures, BinanceSections.FuturesCoin, BinanceSections.Margin, BinanceSections.Spot
			Sections = [BinanceSections.Futures]
		});
	}

	/// <summary>
	/// Run all Binance samples
	/// </summary>
	public static async Task RunBinanceSamplesAsync()
	{
		var connector = CreateConnector();
		InitBinanceAdapter(connector);

		connector.Connect();
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | Binance Connected");

		await Task.Delay(1000);

		// Add Binance-specific samples here
		// Similar structure to CTrader samples can be implemented

		Console.WriteLine("Binance samples not yet implemented.");
		Console.WriteLine("You can create Program.Binance.Securities.cs, Program.Binance.MarketData.cs, etc.");
	}
}
