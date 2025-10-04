namespace StockSharp.CTraderConnector.Native;

static class Extensions
{
	/// <summary>
	/// Convert cTrader symbol code to StockSharp SecurityId.
	/// </summary>
	public static SecurityId ToStockSharp(this string symbolCode)
	{
		return new SecurityId
		{
			SecurityCode = symbolCode,
			BoardCode = BoardCodes.CTrader,
		};
	}

	/// <summary>
	/// Convert StockSharp volume (decimal) to cTrader volume (lots in cents).
	/// </summary>
	public static long ToCTraderVolume(this decimal volume)
	{
		// cTrader uses cents (volume * 100)
		return (long)(volume * 100);
	}

	/// <summary>
	/// Convert cTrader volume (lots in cents) to StockSharp decimal.
	/// </summary>
	public static decimal ToStockSharpVolume(this long volume)
	{
		return (decimal)volume / 100m;
	}

	/// <summary>
	/// Convert cTrader volume ulong (lots in cents) to StockSharp decimal.
	/// </summary>
	public static decimal ToStockSharpVolume(this ulong volume)
	{
		return (decimal)volume / 100m;
	}

	/// <summary>
	/// Convert StockSharp price (decimal) to cTrader price (double).
	/// </summary>
	public static double ToCTraderPrice(this decimal price)
	{
		return (double)price;
	}

	/// <summary>
	/// Convert cTrader price (double) to StockSharp decimal.
	/// </summary>
	public static decimal ToStockSharpPrice(this double price)
	{
		return (decimal)price;
	}
}
