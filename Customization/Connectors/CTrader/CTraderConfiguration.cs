using System.ComponentModel;
using System.Security;

namespace StockSharp.CTrader;

/// <summary>
/// Configuration settings for cTrader connector.
/// Phase 3.5 implementation - T021: Create configuration class.
/// </summary>
[Serializable]
[DisplayName("cTrader Configuration")]
[Description("Configuration settings for cTrader connector integration.")]
public class CTraderConfiguration : INotifyPropertyChanged
{
    private string _applicationId = string.Empty;
    private SecureString _applicationSecret;
    private CTraderEnvironment _environment = CTraderEnvironment.Demo;
    private string _host = "demo.ctraderapi.com";
    private int _port = 5035;
    private bool _useSSL = true;
    private long _accountId;
    private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private TimeSpan _connectionTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _requestTimeout = TimeSpan.FromSeconds(15);
    private int _maxRetryAttempts = 3;
    private bool _enableLogging = true;
    private bool _enableDebugLogging = false;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Application ID for cTrader OAuth2 authentication.
    /// </summary>
    [Category("Authentication")]
    [DisplayName("Application ID")]
    [Description("The application ID provided by cTrader for OAuth2 authentication.")]
    public string ApplicationId
    {
        get => _applicationId;
        set
        {
            if (_applicationId != value)
            {
                _applicationId = value;
                OnPropertyChanged(nameof(ApplicationId));
            }
        }
    }

    /// <summary>
    /// Application secret for cTrader OAuth2 authentication.
    /// </summary>
    [Category("Authentication")]
    [DisplayName("Application Secret")]
    [Description("The application secret provided by cTrader for OAuth2 authentication.")]
    [PasswordPropertyText(true)]
    public SecureString ApplicationSecret
    {
        get => _applicationSecret;
        set
        {
            if (_applicationSecret != value)
            {
                _applicationSecret = value;
                OnPropertyChanged(nameof(ApplicationSecret));
            }
        }
    }

    /// <summary>
    /// cTrader environment (Demo/Live).
    /// </summary>
    [Category("Connection")]
    [DisplayName("Environment")]
    [Description("The cTrader environment to connect to (Demo or Live).")]
    [DefaultValue(CTraderEnvironment.Demo)]
    public CTraderEnvironment Environment
    {
        get => _environment;
        set
        {
            if (_environment != value)
            {
                _environment = value;

                // Auto-update host when environment changes
                Host = value == CTraderEnvironment.Demo ? "demo.ctraderapi.com" : "live.ctraderapi.com";

                OnPropertyChanged(nameof(Environment));
            }
        }
    }

    /// <summary>
    /// Server host for connection.
    /// </summary>
    [Category("Connection")]
    [DisplayName("Host")]
    [Description("The cTrader server hostname to connect to.")]
    public string Host
    {
        get => _host;
        set
        {
            if (_host != value)
            {
                _host = value;
                OnPropertyChanged(nameof(Host));
            }
        }
    }

    /// <summary>
    /// Server port for connection.
    /// </summary>
    [Category("Connection")]
    [DisplayName("Port")]
    [Description("The cTrader server port to connect to.")]
    [DefaultValue(5035)]
    public int Port
    {
        get => _port;
        set
        {
            if (_port != value)
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }
    }

    /// <summary>
    /// Whether to use SSL for connection.
    /// </summary>
    [Category("Connection")]
    [DisplayName("Use SSL")]
    [Description("Whether to use SSL encryption for the connection.")]
    [DefaultValue(true)]
    public bool UseSSL
    {
        get => _useSSL;
        set
        {
            if (_useSSL != value)
            {
                _useSSL = value;
                OnPropertyChanged(nameof(UseSSL));
            }
        }
    }

    /// <summary>
    /// Account ID for trading operations.
    /// </summary>
    [Category("Trading")]
    [DisplayName("Account ID")]
    [Description("The cTrader account ID to use for trading operations.")]
    public long AccountId
    {
        get => _accountId;
        set
        {
            if (_accountId != value)
            {
                _accountId = value;
                OnPropertyChanged(nameof(AccountId));
            }
        }
    }

    /// <summary>
    /// Heartbeat interval for connection keep-alive.
    /// </summary>
    [Category("Advanced")]
    [DisplayName("Heartbeat Interval")]
    [Description("The interval for sending heartbeat messages to keep the connection alive.")]
    public TimeSpan HeartbeatInterval
    {
        get => _heartbeatInterval;
        set
        {
            if (_heartbeatInterval != value)
            {
                _heartbeatInterval = value;
                OnPropertyChanged(nameof(HeartbeatInterval));
            }
        }
    }

    /// <summary>
    /// Connection timeout duration.
    /// </summary>
    [Category("Advanced")]
    [DisplayName("Connection Timeout")]
    [Description("The maximum time to wait for a connection to be established.")]
    public TimeSpan ConnectionTimeout
    {
        get => _connectionTimeout;
        set
        {
            if (_connectionTimeout != value)
            {
                _connectionTimeout = value;
                OnPropertyChanged(nameof(ConnectionTimeout));
            }
        }
    }

    /// <summary>
    /// Request timeout duration.
    /// </summary>
    [Category("Advanced")]
    [DisplayName("Request Timeout")]
    [Description("The maximum time to wait for a request response.")]
    public TimeSpan RequestTimeout
    {
        get => _requestTimeout;
        set
        {
            if (_requestTimeout != value)
            {
                _requestTimeout = value;
                OnPropertyChanged(nameof(RequestTimeout));
            }
        }
    }

    /// <summary>
    /// Maximum retry attempts for failed operations.
    /// </summary>
    [Category("Advanced")]
    [DisplayName("Max Retry Attempts")]
    [Description("The maximum number of retry attempts for failed operations.")]
    [DefaultValue(3)]
    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        set
        {
            if (_maxRetryAttempts != value)
            {
                _maxRetryAttempts = value;
                OnPropertyChanged(nameof(MaxRetryAttempts));
            }
        }
    }

    /// <summary>
    /// Whether to enable logging.
    /// </summary>
    [Category("Logging")]
    [DisplayName("Enable Logging")]
    [Description("Whether to enable logging for the connector.")]
    [DefaultValue(true)]
    public bool EnableLogging
    {
        get => _enableLogging;
        set
        {
            if (_enableLogging != value)
            {
                _enableLogging = value;
                OnPropertyChanged(nameof(EnableLogging));
            }
        }
    }

    /// <summary>
    /// Whether to enable debug-level logging.
    /// </summary>
    [Category("Logging")]
    [DisplayName("Enable Debug Logging")]
    [Description("Whether to enable debug-level logging for detailed diagnostics.")]
    [DefaultValue(false)]
    public bool EnableDebugLogging
    {
        get => _enableDebugLogging;
        set
        {
            if (_enableDebugLogging != value)
            {
                _enableDebugLogging = value;
                OnPropertyChanged(nameof(EnableDebugLogging));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the configuration is valid for connection.
    /// </summary>
    [Browsable(false)]
    public bool IsValid => ValidateConfiguration().IsValid;

    /// <summary>
    /// Gets the validation errors, if any.
    /// </summary>
    [Browsable(false)]
    public string[] ValidationErrors => ValidateConfiguration().Errors.ToArray();

    /// <summary>
    /// Validates the configuration settings.
    /// T023: Configuration validation implementation.
    /// </summary>
    /// <returns>Validation result.</returns>
    public ConfigurationValidationResult ValidateConfiguration()
    {
        var errors = new List<string>();

        // Authentication validation
        if (string.IsNullOrWhiteSpace(ApplicationId))
            errors.Add("Application ID is required.");

        if (ApplicationSecret == null || ApplicationSecret.Length == 0)
            errors.Add("Application Secret is required.");

        // Connection validation
        if (string.IsNullOrWhiteSpace(Host))
            errors.Add("Host is required.");

        if (Port <= 0 || Port > 65535)
            errors.Add("Port must be between 1 and 65535.");

        // Trading validation
        if (AccountId <= 0)
            errors.Add("Account ID must be greater than 0.");

        // Advanced validation
        if (HeartbeatInterval.TotalSeconds < 5)
            errors.Add("Heartbeat interval must be at least 5 seconds.");

        if (ConnectionTimeout.TotalSeconds < 5)
            errors.Add("Connection timeout must be at least 5 seconds.");

        if (RequestTimeout.TotalSeconds < 1)
            errors.Add("Request timeout must be at least 1 second.");

        if (MaxRetryAttempts < 0)
            errors.Add("Max retry attempts cannot be negative.");

        return new ConfigurationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a copy of the configuration.
    /// </summary>
    /// <returns>Configuration copy.</returns>
    public CTraderConfiguration Clone()
    {
        return new CTraderConfiguration
        {
            ApplicationId = ApplicationId,
            ApplicationSecret = ApplicationSecret?.Copy(),
            Environment = Environment,
            Host = Host,
            Port = Port,
            UseSSL = UseSSL,
            AccountId = AccountId,
            HeartbeatInterval = HeartbeatInterval,
            ConnectionTimeout = ConnectionTimeout,
            RequestTimeout = RequestTimeout,
            MaxRetryAttempts = MaxRetryAttempts,
            EnableLogging = EnableLogging,
            EnableDebugLogging = EnableDebugLogging
        };
    }

    /// <summary>
    /// Applies configuration to a message adapter.
    /// </summary>
    /// <param name="adapter">The message adapter to configure.</param>
    public void ApplyTo(CTraderMessageAdapter adapter)
    {
        if (adapter == null)
            throw new ArgumentNullException(nameof(adapter));

        adapter.ApplicationId = ApplicationId;
        adapter.ApplicationSecret = ApplicationSecret;
        adapter.Environment = Environment;
        adapter.Host = Host;
        adapter.Port = Port;
        adapter.AccountId = AccountId;
        adapter.HeartbeatInterval = HeartbeatInterval;
    }

    /// <summary>
    /// Loads configuration from a message adapter.
    /// </summary>
    /// <param name="adapter">The message adapter to load from.</param>
    public void LoadFrom(CTraderMessageAdapter adapter)
    {
        if (adapter == null)
            throw new ArgumentNullException(nameof(adapter));

        ApplicationId = adapter.ApplicationId;
        ApplicationSecret = adapter.ApplicationSecret;
        Environment = adapter.Environment;
        Host = adapter.Host;
        Port = adapter.Port;
        AccountId = adapter.AccountId;
        HeartbeatInterval = adapter.HeartbeatInterval;
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Configuration validation result.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
}