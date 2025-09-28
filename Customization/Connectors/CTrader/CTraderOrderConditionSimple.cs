namespace StockSharp.CTrader;

/// <summary>
/// Simplified cTrader order condition for Phase 3.3 structure.
/// Full implementation will be completed in Phase 3.4.
/// </summary>
[Serializable]
public class CTraderOrderConditionSimple : OrderCondition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CTraderOrderConditionSimple"/>.
    /// </summary>
    public CTraderOrderConditionSimple()
    {
    }

    /// <summary>
    /// Stop loss price for the order.
    /// </summary>
    public decimal? StopLoss { get; set; }

    /// <summary>
    /// Take profit price for the order.
    /// </summary>
    public decimal? TakeProfit { get; set; }

    /// <summary>
    /// Order label for identification.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Whether this is a hedging order.
    /// </summary>
    public bool IsHedging { get; set; }

    /// <summary>
    /// Position ID to modify (for modifying existing positions).
    /// </summary>
    public long? PositionId { get; set; }

    /// <summary>
    /// Determines if this condition has risk management parameters.
    /// </summary>
    public bool HasRiskManagement => StopLoss.HasValue || TakeProfit.HasValue;

    /// <summary>
    /// Creates a clone of this order condition.
    /// </summary>
    /// <returns>Cloned order condition.</returns>
    public override OrderCondition Clone()
    {
        return new CTraderOrderConditionSimple
        {
            StopLoss = StopLoss,
            TakeProfit = TakeProfit,
            Label = Label,
            IsHedging = IsHedging,
            PositionId = PositionId
        };
    }

    /// <summary>
    /// Gets a summary of the order condition.
    /// </summary>
    /// <returns>Summary string.</returns>
    public override string ToString()
    {
        var parts = new List<string>();

        if (StopLoss.HasValue)
            parts.Add($"SL: {StopLoss:F5}");

        if (TakeProfit.HasValue)
            parts.Add($"TP: {TakeProfit:F5}");

        if (!string.IsNullOrEmpty(Label))
            parts.Add($"Label: {Label}");

        if (IsHedging)
            parts.Add("Hedging");

        return parts.Count > 0 ? string.Join(", ", parts) : "Standard Order";
    }
}