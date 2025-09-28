using System;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Information about an available connector type
/// Contains metadata for displaying connector options to users
/// </summary>
public class ConnectorInfo
{
    /// <summary>
    /// Technical type name (e.g., "Bitstamp", "InteractiveBrokers")
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// User-friendly display name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the connector and its capabilities
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Supported features and capabilities
    /// </summary>
    public ConnectorCapabilities SupportedFeatures { get; set; }

    /// <summary>
    /// Path to connector icon for UI display
    /// </summary>
    public string IconPath { get; set; }

    /// <summary>
    /// Whether the connector is currently available/enabled
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Category for grouping connectors (e.g., "Crypto", "Stock", "Forex")
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Relative priority for display ordering (lower = higher priority)
    /// </summary>
    public int DisplayPriority { get; set; } = 100;

    public override string ToString()
    {
        return Name ?? Type ?? "Unknown Connector";
    }
}

/// <summary>
/// Capabilities supported by connectors
/// </summary>
[Flags]
public enum ConnectorCapabilities
{
    None = 0,
    MarketData = 1,
    Trading = 2,
    HistoricalData = 4,
    Level2Data = 8,
    Options = 16,
    Crypto = 32,
    Forex = 64,
    Futures = 128
}