using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

/// <summary>
/// Request object for registering an order.
/// </summary>
/// <param name="Order">The order to register.</param>
public record RegisterOrderRequest(Order Order);

/// <summary>
/// Request object for cancelling an order.
/// </summary>
/// <param name="Order">The order to cancel.</param>
public record CancelOrderRequest(Order Order);

/// <summary>
/// Request object for retrieving orders.
/// </summary>
public record GetOrdersRequest
{
    /// <summary>
    /// Filter by security.
    /// </summary>
    public Security? Security { get; init; }

    /// <summary>
    /// Filter by portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; init; }

    /// <summary>
    /// Start date for historical orders.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical orders.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Filter by order states.
    /// </summary>
    public OrderStates[]? States { get; init; }
}

/// <summary>
/// Request object for retrieving trades.
/// </summary>
public record GetTradesRequest
{
    /// <summary>
    /// Filter by security.
    /// </summary>
    public Security? Security { get; init; }

    /// <summary>
    /// Filter by portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; init; }

    /// <summary>
    /// Start date for historical trades.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical trades.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// Request object for retrieving securities information.
/// </summary>
/// <param name="Symbol">The symbol code to search for (e.g., "EURUSD", "AAPL").</param>
public record GetSecuritiesRequest(string Symbol);

/// <summary>
/// Request object for retrieving portfolios.
/// </summary>
public record GetPortfoliosRequest
{
    /// <summary>
    /// Filter by portfolio name.
    /// </summary>
    public string? PortfolioName { get; init; }

    /// <summary>
    /// Start date for historical portfolio data.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical portfolio data.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// Request object for retrieving market depth snapshot.
/// </summary>
/// <param name="Security">The security to get market depth for.</param>
public record GetMarketDepthRequest(Security Security)
{
    /// <summary>
    /// Maximum depth level (number of price levels to return).
    /// </summary>
    public int? MaxDepth { get; init; }
}

/// <summary>
/// Request object for retrieving historical ticks.
/// </summary>
/// <param name="Security">The security to get ticks for.</param>
public record GetTicksRequest(Security Security)
{
    /// <summary>
    /// Start date for historical ticks.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical ticks.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Number of records to skip.
    /// </summary>
    public long? Skip { get; init; }

    /// <summary>
    /// Maximum number of records to return.
    /// </summary>
    public long? Count { get; init; }
}

/// <summary>
/// Request object for retrieving Level1 market data.
/// </summary>
/// <param name="Security">The security to get Level1 data for.</param>
public record GetLevel1Request(Security Security)
{
    /// <summary>
    /// Start date for historical Level1 data.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// End date for historical Level1 data.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}
