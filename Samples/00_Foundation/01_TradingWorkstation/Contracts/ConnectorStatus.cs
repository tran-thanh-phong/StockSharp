using System;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Real-time connection status for a connector account
/// Tracks current state and provides status information for UI display
/// </summary>
public class ConnectorStatus
{
    /// <summary>
    /// Account ID this status relates to
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState CurrentState { get; set; }

    /// <summary>
    /// When the current state was entered
    /// </summary>
    public DateTime LastStateChange { get; set; }

    /// <summary>
    /// Error message if CurrentState is Error, otherwise null
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Additional status details or context
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// Duration in current state
    /// </summary>
    public TimeSpan Duration => DateTime.Now - LastStateChange;

    /// <summary>
    /// Whether the connector is in a connected state
    /// </summary>
    public bool IsConnected => CurrentState == ConnectionState.Connected;

    /// <summary>
    /// Whether the connector has an error
    /// </summary>
    public bool HasError => CurrentState == ConnectionState.Error;

    /// <summary>
    /// Whether the connector is currently trying to connect
    /// </summary>
    public bool IsConnecting => CurrentState == ConnectionState.Connecting || CurrentState == ConnectionState.Testing;

    /// <summary>
    /// Display-friendly status text
    /// </summary>
    public string StatusText => ErrorMessage ?? CurrentState.ToString();

    public override string ToString()
    {
        return $"{CurrentState} ({Duration.TotalSeconds:F0}s)";
    }
}

/// <summary>
/// Connection states for connector accounts
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Error,
    Testing
}