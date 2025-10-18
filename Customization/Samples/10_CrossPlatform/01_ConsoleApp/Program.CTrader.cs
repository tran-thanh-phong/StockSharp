using System;
using System.Linq;
using System.Threading.Tasks;

using Ecng.Common;

using StockSharp.Customization.Connectors;
using StockSharp.Customization.CTrader;

namespace StockSharp.Samples.CrossPlatform.ConsoleApp;

static partial class Program
{
	/// <summary>
	/// Initialize CTrader connector with demo credentials
	/// </summary>
	private static void InitCTraderAdapter(AsyncConnector connector)
	{
		connector.Adapter.InnerAdapters.Add(new CTraderMessageAdapter(connector.TransactionIdGenerator)
		{
			Key = "17050_H08Li6FweA3QSFv1ez91X1QAtXnCf1alYn0uYgc2VFvjf1GB2r".Secure(),
			Secret = "3s3cGZPxW8HdYv5WRbezkO4MyvXdHvTCztJ7W7yW7UjonnxZ59".Secure(),
			Token = "Uq_4iBJfI5KU3ORBaJcT_41dFvdH65sqpa7238HATjc".Secure(),
			RefreshToken = "iLnYzm36O5q6eAK0EYia9ocFTGbg7vZ3Ig229Us7HUg".Secure(),
			IsDemo = true
		});
	}

	/// <summary>
	/// Run all CTrader samples
	/// </summary>
	public static async Task RunCTraderSamplesAsync()
	{
		var connector = CreateConnector();
		InitCTraderAdapter(connector);

		connector.Connect();
		Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | CTrader Connected");

		await Task.Delay(1000);

		// Run samples
		await CTraderSecuritiesSampleAsync(connector);
		await CTraderMarketDataSampleAsync(connector);
	}
}
