using System.Collections.Concurrent;
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.Customization.Connectors;

/// <summary>
/// An extended Connector that provides async/await methods for common operations.
/// Wraps event-driven StockSharp APIs into modern async patterns with timeout and cancellation support.
/// </summary>
public partial class AsyncConnector : Connector
{
    // Task completion sources for tracking async operations
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<Security>>> _securityLookupTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<Order>> _orderRegistrationTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<Order>> _orderCancellationTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<Order>>> _orderLookupTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<MyTrade>>> _tradeLookupTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<Portfolio>>> _portfolioLookupTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<IOrderBookMessage>> _marketDepthTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<ITickTradeMessage>>> _ticksTaskSource = new();
    internal readonly ConcurrentDictionary<long, TaskCompletionSource<List<Level1ChangeMessage>>> _level1TaskSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncConnector"/> class.
    /// </summary>
    public AsyncConnector() : base()
    {
        LookupSecuritiesResult += OnLookupSecuritiesResult;
        OrderReceived += OnOrderReceived;
        OrderRegisterFailReceived += OnOrderRegisterFailReceived;
        OwnTradeReceived += OnOwnTradeReceived;
    }

    public string? OnPortfolioLookupResult { get; set; }
}
