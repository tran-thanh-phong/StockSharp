using System;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Activity log entry for session-only logging (FR-007)
/// Tracks connector operations and events with timestamps
/// </summary>
public class ActivityEntry
{
    /// <summary>
    /// When the activity occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Severity level of the activity
    /// </summary>
    public ActivityLevel Level { get; set; }

    /// <summary>
    /// Activity message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Additional details or context
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// Source that generated the activity (e.g., connector type, service name)
    /// </summary>
    public string Source { get; set; }

    public ActivityEntry(ActivityLevel level, string message, string details = null, string source = null)
    {
        Timestamp = DateTime.Now;
        Level = level;
        Message = message;
        Details = details;
        Source = source;
    }

    /// <summary>
    /// Format for display in activity logs
    /// </summary>
    public string GetDisplayText()
    {
        var timestamp = Timestamp.ToString("HH:mm:ss.fff");
        var levelPrefix = Level.ToString().ToUpper().PadRight(5);

        if (!string.IsNullOrEmpty(Source))
        {
            return $"{timestamp} [{levelPrefix}] [{Source}] {Message}";
        }

        return $"{timestamp} [{levelPrefix}] {Message}";
    }

    public override string ToString()
    {
        return GetDisplayText();
    }
}

/// <summary>
/// Activity log levels for filtering and display
/// </summary>
public enum ActivityLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}