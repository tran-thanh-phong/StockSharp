using System;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Result of connection test operation (FR-003)
/// Contains success/failure information and performance metrics
/// </summary>
public class ConnectionTestResult
{
    /// <summary>
    /// Whether the connection test was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Result message describing success or failure reason
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Exception that caused failure (if any)
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// Time taken for the connection test
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// When the test was performed
    /// </summary>
    public DateTime TestedAt { get; set; }

    /// <summary>
    /// Additional test metadata or performance metrics
    /// </summary>
    public string Details { get; set; }

    private ConnectionTestResult(bool isSuccess, string message, Exception exception, TimeSpan duration, string details = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        Exception = exception;
        Duration = duration;
        Details = details;
        TestedAt = DateTime.Now;
    }

    /// <summary>
    /// Create successful test result
    /// </summary>
    public static ConnectionTestResult Success(TimeSpan duration, string message = "Connection test successful")
    {
        return new ConnectionTestResult(true, message, null, duration);
    }

    /// <summary>
    /// Create failed test result
    /// </summary>
    public static ConnectionTestResult Failure(string message, Exception exception = null, TimeSpan? duration = null)
    {
        return new ConnectionTestResult(false, message, exception, duration ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Create timeout result
    /// </summary>
    public static ConnectionTestResult Timeout(TimeSpan duration, string message = "Connection test timed out")
    {
        return new ConnectionTestResult(false, message, null, duration);
    }

    /// <summary>
    /// Get display-friendly summary of the test result
    /// </summary>
    public string GetSummary()
    {
        var status = IsSuccess ? "SUCCESS" : "FAILED";
        var durationText = Duration.TotalSeconds > 0 ? $" ({Duration.TotalSeconds:F1}s)" : "";

        return $"{status}{durationText}: {Message}";
    }

    public override string ToString()
    {
        return GetSummary();
    }
}