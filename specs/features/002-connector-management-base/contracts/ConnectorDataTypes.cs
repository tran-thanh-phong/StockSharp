using System;
using System.Collections.Generic;
using System.ComponentModel;
using StockSharp.Configuration;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts
{
    /// <summary>
    /// Information about an available connector type
    /// Maps to Connector entity in data model
    /// Examples: Bitstamp (tested), Interactive Brokers, MT4/MT5
    /// </summary>
    public class ConnectorInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ConnectorCapabilities SupportedFeatures { get; set; }
        public string IconPath { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Configured connector account instance
    /// Maps to Account entity in data model
    /// </summary>
    public class ConnectorAccount : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _accountName;

        public Guid Id { get; set; }
        public string ConnectorType { get; set; }

        public string AccountName
        {
            get => _accountName;
            set
            {
                _accountName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccountName)));
            }
        }

        public ConnectorConfiguration Configuration { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public DateTime CreatedDate { get; set; }
        public DateTime LastUsed { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Connector configuration settings
    /// Maps to Configuration entity in data model
    /// </summary>
    public class ConnectorConfiguration
    {
        public SettingsStorage Settings { get; set; }
        public Dictionary<string, string> SecureData { get; set; }
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool AutoReconnect { get; set; }
        public int MaxRetryAttempts { get; set; } = 3;

        public ConnectorConfiguration()
        {
            Settings = new SettingsStorage();
            SecureData = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Current connection status for an account
    /// Maps to ConnectionStatus entity in data model
    /// </summary>
    public class ConnectorStatus : INotifyPropertyChanged
    {
        private ConnectionState _currentState;
        private string _errorMessage;
        private DateTime _lastStateChange;

        public Guid AccountId { get; set; }

        public ConnectionState CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                LastStateChange = DateTime.Now;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentState)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastStateChange)));
            }
        }

        public DateTime LastStateChange
        {
            get => _lastStateChange;
            private set
            {
                _lastStateChange = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastStateChange)));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
            }
        }

        public DateTime? ConnectionStartTime { get; set; }
        public DateTime? LastHeartbeat { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Activity log entry for troubleshooting
    /// Maps to ActivityEntry entity in data model
    /// </summary>
    public class ActivityEntry
    {
        public DateTime Timestamp { get; set; }
        public ActivityLevel Level { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }

        public ActivityEntry(ActivityLevel level, string message, string details = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Details = details;
        }
    }

    /// <summary>
    /// Connection test result
    /// Used by FR-003 connection testing
    /// </summary>
    public class ConnectionTestResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public TimeSpan TestDuration { get; set; }
        public Exception Exception { get; set; }

        public static ConnectionTestResult Success(TimeSpan duration, string message = "Connection successful")
        {
            return new ConnectionTestResult
            {
                IsSuccess = true,
                Message = message,
                TestDuration = duration
            };
        }

        public static ConnectionTestResult Failure(string message, Exception exception = null, TimeSpan duration = default)
        {
            return new ConnectionTestResult
            {
                IsSuccess = false,
                Message = message,
                Exception = exception,
                TestDuration = duration
            };
        }
    }

    // Enums

    /// <summary>
    /// Connection states for status monitoring
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error,
        Testing
    }

    /// <summary>
    /// Activity log levels
    /// </summary>
    public enum ActivityLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Supported connector capabilities
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
        Crypto = 32
    }

    // Event Args

    /// <summary>
    /// Event arguments for account status changes
    /// </summary>
    public class AccountStatusChangedEventArgs : EventArgs
    {
        public Guid AccountId { get; set; }
        public ConnectionState OldState { get; set; }
        public ConnectionState NewState { get; set; }
        public string Message { get; set; }

        public AccountStatusChangedEventArgs(Guid accountId, ConnectionState oldState, ConnectionState newState, string message = null)
        {
            AccountId = accountId;
            OldState = oldState;
            NewState = newState;
            Message = message;
        }
    }

    /// <summary>
    /// Event arguments for activity entries
    /// </summary>
    public class ActivityEntryEventArgs : EventArgs
    {
        public Guid AccountId { get; set; }
        public ActivityEntry Entry { get; set; }

        public ActivityEntryEventArgs(Guid accountId, ActivityEntry entry)
        {
            AccountId = accountId;
            Entry = entry;
        }
    }

    /// <summary>
    /// Event arguments for configuration changes
    /// </summary>
    public class AccountConfigurationChangedEventArgs : EventArgs
    {
        public Guid AccountId { get; set; }
        public ConnectorConfiguration Configuration { get; set; }

        public AccountConfigurationChangedEventArgs(Guid accountId, ConnectorConfiguration configuration)
        {
            AccountId = accountId;
            Configuration = configuration;
        }
    }
}