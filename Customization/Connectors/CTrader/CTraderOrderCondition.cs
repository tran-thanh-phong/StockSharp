namespace StockSharp.CTraderConnector;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

/// <summary>
/// cTrader order condition.
/// </summary>
[DataContract]
[Serializable]
[DisplayName("cTrader")]
public class CTraderOrderCondition : OrderCondition
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CTraderOrderCondition"/>.
	/// </summary>
	public CTraderOrderCondition()
	{
	}

	/// <summary>
	/// Stop loss price.
	/// </summary>
	[DataMember]
	[Display(
		Name = "Stop Loss",
		Description = "Stop loss price level",
		GroupName = "General",
		Order = 0)]
	public decimal? StopLoss { get; set; }

	/// <summary>
	/// Take profit price.
	/// </summary>
	[DataMember]
	[Display(
		Name = "Take Profit",
		Description = "Take profit price level",
		GroupName = "General",
		Order = 1)]
	public decimal? TakeProfit { get; set; }

	/// <summary>
	/// Trailing stop distance in pips.
	/// </summary>
	[DataMember]
	[Display(
		Name = "Trailing Stop",
		Description = "Trailing stop distance in pips",
		GroupName = "General",
		Order = 2)]
	public decimal? TrailingStop { get; set; }

	/// <summary>
	/// Stop price for stop orders.
	/// </summary>
	[DataMember]
	[Display(
		Name = "Stop Price",
		Description = "Stop price for stop/limit orders",
		GroupName = "General",
		Order = 3)]
	public decimal? StopPrice { get; set; }
}
