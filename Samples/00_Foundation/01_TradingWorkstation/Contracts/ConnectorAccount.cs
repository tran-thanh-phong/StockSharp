using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Represents a configured connector account with connection settings and metadata
/// Implements INotifyPropertyChanged for UI data binding support
/// </summary>
public class ConnectorAccount : INotifyPropertyChanged
{
    private string _accountName;
    private bool _isEnabled;
    private DateTime _lastUsed;

    /// <summary>
    /// Unique identifier for the account
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of connector (e.g., "Bitstamp", "InteractiveBrokers")
    /// </summary>
    public string ConnectorType { get; set; }

    /// <summary>
    /// User-friendly name for the account
    /// </summary>
    public string AccountName
    {
        get => _accountName;
        set
        {
            _accountName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the account is enabled for connection
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// When the account was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// When the account was last used for connection
    /// </summary>
    public DateTime LastUsed
    {
        get => _lastUsed;
        set
        {
            _lastUsed = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Connector-specific configuration settings
    /// </summary>
    public ConnectorConfiguration Configuration { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return $"{AccountName} ({ConnectorType})";
    }
}