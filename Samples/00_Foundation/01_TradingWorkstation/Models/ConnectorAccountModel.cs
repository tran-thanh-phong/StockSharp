using System;
using System.ComponentModel;
using StockSharp.Algo;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Models;

/// <summary>
/// UI Model for ConnectorAccount - implements INotifyPropertyChanged for data binding
/// Wraps the contract ConnectorAccount with additional UI-specific functionality
/// </summary>
public class ConnectorAccountModel : INotifyPropertyChanged
{
    private readonly ConnectorAccount _account;
    private Connector _stockSharpConnector;
    private bool _isSelected;

    public ConnectorAccountModel(ConnectorAccount account)
    {
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _account.PropertyChanged += (s, e) => PropertyChanged?.Invoke(this, e);

        // Create associated StockSharp Connector for this account
        InitializeStockSharpConnector();
    }

    // Wrapped properties from ConnectorAccount
    public Guid Id => _account.Id;

    public string ConnectorType => _account.ConnectorType;

    public string AccountName
    {
        get => _account.AccountName;
        set
        {
            _account.AccountName = value;
            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsEnabled
    {
        get => _account.IsEnabled;
        set
        {
            _account.IsEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public DateTime CreatedDate => _account.CreatedDate;
    public DateTime LastUsed => _account.LastUsed;

    public ConnectorConfiguration Configuration => _account.Configuration;

    // UI-specific properties
    public string DisplayName => $"{AccountName} ({ConnectorType})";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    /// <summary>
    /// Associated StockSharp Connector instance for this account
    /// </summary>
    public Connector StockSharpConnector
    {
        get => _stockSharpConnector;
        private set
        {
            _stockSharpConnector = value;
            OnPropertyChanged(nameof(StockSharpConnector));
        }
    }

    /// <summary>
    /// Connection status display text
    /// </summary>
    public string StatusText { get; private set; } = "Disconnected";

    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

    private void InitializeStockSharpConnector()
    {
        try
        {
            // Create StockSharp Connector instance based on connector type
            StockSharpConnector = new Connector();

            // Load configuration if available
            if (Configuration?.Settings != null)
            {
                StockSharpConnector.Load(Configuration.Settings);
            }

            // Wire up events for status updates
            StockSharpConnector.Connected += () =>
            {
                CurrentState = ConnectionState.Connected;
                StatusText = "Connected";
                _account.LastUsed = DateTime.Now;
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CurrentState));
            };

            StockSharpConnector.Disconnected += () =>
            {
                CurrentState = ConnectionState.Disconnected;
                StatusText = "Disconnected";
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CurrentState));
            };

            StockSharpConnector.ConnectionError += error =>
            {
                CurrentState = ConnectionState.Error;
                StatusText = $"Error: {error.Message}";
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CurrentState));
            };
        }
        catch (Exception ex)
        {
            StatusText = $"Initialization error: {ex.Message}";
            CurrentState = ConnectionState.Error;
        }
    }

    /// <summary>
    /// Update connector configuration
    /// </summary>
    public void UpdateConfiguration(ConnectorConfiguration newConfig)
    {
        if (newConfig != null)
        {
            _account.Configuration = newConfig;

            // Apply to StockSharp connector
            if (StockSharpConnector != null && newConfig.Settings != null)
            {
                StockSharpConnector.Load(newConfig.Settings);
            }

            OnPropertyChanged(nameof(Configuration));
        }
    }

    /// <summary>
    /// Get current connector settings for persistence
    /// </summary>
    public void SaveCurrentSettings()
    {
        if (StockSharpConnector != null && Configuration != null)
        {
            var storage = new Ecng.Serialization.SettingsStorage();
            StockSharpConnector.Save(storage);
            Configuration.Settings = storage;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return DisplayName;
    }
}