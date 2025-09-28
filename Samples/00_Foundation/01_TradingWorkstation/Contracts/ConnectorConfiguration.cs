using System;
using System.Collections.Generic;
using Ecng.Serialization;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Connector configuration containing all settings needed for connection
/// Uses SettingsStorage for StockSharp integration and persistence
/// </summary>
public class ConnectorConfiguration
{
    /// <summary>
    /// StockSharp settings storage containing connector-specific configuration
    /// </summary>
    public SettingsStorage Settings { get; set; }

    /// <summary>
    /// Secure credentials and sensitive data (API keys, passwords, etc.)
    /// Stored as plain text per MVP requirements (FR-009 clarification)
    /// </summary>
    public Dictionary<string, string> SecureData { get; set; }

    /// <summary>
    /// Connection timeout (default: 30 seconds per FR-003)
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to automatically reconnect on disconnection
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for connection
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    public ConnectorConfiguration()
    {
        Settings = new SettingsStorage();
        SecureData = new Dictionary<string, string>();
    }

    /// <summary>
    /// Check if configuration has all required settings for connection
    /// </summary>
    public bool IsValid()
    {
        // Basic validation - can be extended per connector type
        return Settings != null && Settings.Keys.Count > 0;
    }

    /// <summary>
    /// Get display summary of configuration (excluding sensitive data)
    /// </summary>
    public string GetSummary()
    {
        var settingCount = Settings?.Keys.Count ?? 0;
        var secureCount = SecureData?.Count ?? 0;

        return $"Settings: {settingCount}, Credentials: {secureCount}, Timeout: {ConnectionTimeout.TotalSeconds}s";
    }
}