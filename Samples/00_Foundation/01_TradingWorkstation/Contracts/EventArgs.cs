using System;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Event args for account status changes
/// Used by ConnectorManagerService to notify about connection state transitions
/// </summary>
public class AccountStatusChangedEventArgs : EventArgs
{
    public Guid AccountId { get; }
    public ConnectionState OldState { get; }
    public ConnectionState NewState { get; }
    public string Message { get; }

    public AccountStatusChangedEventArgs(Guid accountId, ConnectionState oldState, ConnectionState newState, string message = null)
    {
        AccountId = accountId;
        OldState = oldState;
        NewState = newState;
        Message = message;
    }

    public override string ToString()
    {
        return $"Account {AccountId}: {OldState} → {NewState}";
    }
}

/// <summary>
/// Event args for activity entries
/// Used by ConnectorManagerService to notify about new activity log entries
/// </summary>
public class ActivityEntryEventArgs : EventArgs
{
    public Guid AccountId { get; }
    public ActivityEntry Entry { get; }

    public ActivityEntryEventArgs(Guid accountId, ActivityEntry entry)
    {
        AccountId = accountId;
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public override string ToString()
    {
        return $"Activity for {AccountId}: [{Entry.Level}] {Entry.Message}";
    }
}

/// <summary>
/// Event args for account configuration changes
/// Used by ConnectorManagerService to notify about configuration updates
/// </summary>
public class AccountConfigurationChangedEventArgs : EventArgs
{
    public Guid AccountId { get; }
    public ConnectorConfiguration Configuration { get; }
    public DateTime ChangedAt { get; }

    public AccountConfigurationChangedEventArgs(Guid accountId, ConnectorConfiguration configuration)
    {
        AccountId = accountId;
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ChangedAt = DateTime.Now;
    }

    public override string ToString()
    {
        return $"Configuration changed for {AccountId}";
    }
}