// TODO: Windows Forms not available in net6.0 class library - move to separate WinForms project in future phases
/*
using System.ComponentModel;
using System.Security;
using System.Windows.Forms;

namespace StockSharp.CTrader;

/// <summary>
/// Configuration dialog for cTrader connector settings.
/// Phase 3.5 implementation - T022: Add GUI configuration forms/dialogs.
/// </summary>
public partial class CTraderConfigurationDialog : Form
{
    private readonly CTraderConfiguration _configuration;
    private readonly CTraderConfiguration _originalConfiguration;

    private TableLayoutPanel _mainLayout;
    private GroupBox _authenticationGroup;
    private GroupBox _connectionGroup;
    private GroupBox _tradingGroup;
    private GroupBox _advancedGroup;
    private GroupBox _loggingGroup;

    private TextBox _applicationIdTextBox;
    private TextBox _applicationSecretTextBox;
    private ComboBox _environmentComboBox;
    private TextBox _hostTextBox;
    private NumericUpDown _portNumericUpDown;
    private CheckBox _useSslCheckBox;
    private NumericUpDown _accountIdNumericUpDown;
    private NumericUpDown _heartbeatIntervalNumericUpDown;
    private NumericUpDown _connectionTimeoutNumericUpDown;
    private NumericUpDown _requestTimeoutNumericUpDown;
    private NumericUpDown _maxRetryAttemptsNumericUpDown;
    private CheckBox _enableLoggingCheckBox;
    private CheckBox _enableDebugLoggingCheckBox;

    private Button _okButton;
    private Button _cancelButton;
    private Button _testConnectionButton;
    private Button _resetToDefaultsButton;

    private Label _validationLabel;

    /// <summary>
    /// Initializes a new instance of the <see cref="CTraderConfigurationDialog"/> class.
    /// </summary>
    /// <param name="configuration">The configuration to edit.</param>
    public CTraderConfigurationDialog(CTraderConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _originalConfiguration = configuration.Clone();

        InitializeComponent();
        InitializeControls();
        LoadConfiguration();
        ValidateConfiguration();

        // Wire up events
        _configuration.PropertyChanged += Configuration_PropertyChanged;
    }

    /// <summary>
    /// Gets the configuration being edited.
    /// </summary>
    public CTraderConfiguration Configuration => _configuration;

    /// <summary>
    /// Initializes the dialog components.
    /// </summary>
    private void InitializeComponent()
    {
        Text = "cTrader Configuration";
        Size = new Size(500, 700);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;

        // Create main layout
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(10)
        };

        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Authentication
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Connection
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Trading
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Advanced
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Logging
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Validation
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

        Controls.Add(_mainLayout);
    }

    /// <summary>
    /// Initializes the dialog controls.
    /// </summary>
    private void InitializeControls()
    {
        CreateAuthenticationGroup();
        CreateConnectionGroup();
        CreateTradingGroup();
        CreateAdvancedGroup();
        CreateLoggingGroup();
        CreateValidationArea();
        CreateButtons();

        _mainLayout.Controls.Add(_authenticationGroup, 0, 0);
        _mainLayout.Controls.Add(_connectionGroup, 0, 1);
        _mainLayout.Controls.Add(_tradingGroup, 0, 2);
        _mainLayout.Controls.Add(_advancedGroup, 0, 3);
        _mainLayout.Controls.Add(_loggingGroup, 0, 4);
        _mainLayout.Controls.Add(_validationLabel, 0, 5);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(_testConnectionButton);
        buttonPanel.Controls.Add(_resetToDefaultsButton);

        _mainLayout.Controls.Add(buttonPanel, 0, 6);
    }

    /// <summary>
    /// Creates the authentication group controls.
    /// </summary>
    private void CreateAuthenticationGroup()
    {
        _authenticationGroup = new GroupBox
        {
            Text = "Authentication",
            Dock = DockStyle.Fill,
            Height = 80
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Application ID
        layout.Controls.Add(new Label { Text = "Application ID:", Anchor = AnchorStyles.Left }, 0, 0);
        _applicationIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _applicationIdTextBox.TextChanged += (s, e) => _configuration.ApplicationId = _applicationIdTextBox.Text;
        layout.Controls.Add(_applicationIdTextBox, 1, 0);

        // Application Secret
        layout.Controls.Add(new Label { Text = "Application Secret:", Anchor = AnchorStyles.Left }, 0, 1);
        _applicationSecretTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _applicationSecretTextBox.TextChanged += ApplicationSecretTextBox_TextChanged;
        layout.Controls.Add(_applicationSecretTextBox, 1, 1);

        _authenticationGroup.Controls.Add(layout);
    }

    /// <summary>
    /// Creates the connection group controls.
    /// </summary>
    private void CreateConnectionGroup()
    {
        _connectionGroup = new GroupBox
        {
            Text = "Connection",
            Dock = DockStyle.Fill,
            Height = 120
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Environment
        layout.Controls.Add(new Label { Text = "Environment:", Anchor = AnchorStyles.Left }, 0, 0);
        _environmentComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _environmentComboBox.Items.AddRange(Enum.GetValues(typeof(CTraderEnvironment)).Cast<object>().ToArray());
        _environmentComboBox.SelectedIndexChanged += (s, e) =>
            _configuration.Environment = (CTraderEnvironment)_environmentComboBox.SelectedItem;
        layout.Controls.Add(_environmentComboBox, 1, 0);

        // Host
        layout.Controls.Add(new Label { Text = "Host:", Anchor = AnchorStyles.Left }, 0, 1);
        _hostTextBox = new TextBox { Dock = DockStyle.Fill };
        _hostTextBox.TextChanged += (s, e) => _configuration.Host = _hostTextBox.Text;
        layout.Controls.Add(_hostTextBox, 1, 1);

        // Port
        layout.Controls.Add(new Label { Text = "Port:", Anchor = AnchorStyles.Left }, 0, 2);
        _portNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 65535
        };
        _portNumericUpDown.ValueChanged += (s, e) => _configuration.Port = (int)_portNumericUpDown.Value;
        layout.Controls.Add(_portNumericUpDown, 1, 2);

        // Use SSL
        _useSslCheckBox = new CheckBox { Text = "Use SSL", Dock = DockStyle.Fill };
        _useSslCheckBox.CheckedChanged += (s, e) => _configuration.UseSSL = _useSslCheckBox.Checked;
        layout.Controls.Add(_useSslCheckBox, 1, 3);

        _connectionGroup.Controls.Add(layout);
    }

    /// <summary>
    /// Creates the trading group controls.
    /// </summary>
    private void CreateTradingGroup()
    {
        _tradingGroup = new GroupBox
        {
            Text = "Trading",
            Dock = DockStyle.Fill,
            Height = 60
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Account ID
        layout.Controls.Add(new Label { Text = "Account ID:", Anchor = AnchorStyles.Left }, 0, 0);
        _accountIdNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = long.MaxValue
        };
        _accountIdNumericUpDown.ValueChanged += (s, e) => _configuration.AccountId = (long)_accountIdNumericUpDown.Value;
        layout.Controls.Add(_accountIdNumericUpDown, 1, 0);

        _tradingGroup.Controls.Add(layout);
    }

    /// <summary>
    /// Creates the advanced group controls.
    /// </summary>
    private void CreateAdvancedGroup()
    {
        _advancedGroup = new GroupBox
        {
            Text = "Advanced",
            Dock = DockStyle.Fill,
            Height = 140
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Heartbeat Interval
        layout.Controls.Add(new Label { Text = "Heartbeat (sec):", Anchor = AnchorStyles.Left }, 0, 0);
        _heartbeatIntervalNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 5,
            Maximum = 300
        };
        _heartbeatIntervalNumericUpDown.ValueChanged += (s, e) =>
            _configuration.HeartbeatInterval = TimeSpan.FromSeconds((double)_heartbeatIntervalNumericUpDown.Value);
        layout.Controls.Add(_heartbeatIntervalNumericUpDown, 1, 0);

        // Connection Timeout
        layout.Controls.Add(new Label { Text = "Connection Timeout (sec):", Anchor = AnchorStyles.Left }, 0, 1);
        _connectionTimeoutNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 5,
            Maximum = 300
        };
        _connectionTimeoutNumericUpDown.ValueChanged += (s, e) =>
            _configuration.ConnectionTimeout = TimeSpan.FromSeconds((double)_connectionTimeoutNumericUpDown.Value);
        layout.Controls.Add(_connectionTimeoutNumericUpDown, 1, 1);

        // Request Timeout
        layout.Controls.Add(new Label { Text = "Request Timeout (sec):", Anchor = AnchorStyles.Left }, 0, 2);
        _requestTimeoutNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 60
        };
        _requestTimeoutNumericUpDown.ValueChanged += (s, e) =>
            _configuration.RequestTimeout = TimeSpan.FromSeconds((double)_requestTimeoutNumericUpDown.Value);
        layout.Controls.Add(_requestTimeoutNumericUpDown, 1, 2);

        // Max Retry Attempts
        layout.Controls.Add(new Label { Text = "Max Retry Attempts:", Anchor = AnchorStyles.Left }, 0, 3);
        _maxRetryAttemptsNumericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 10
        };
        _maxRetryAttemptsNumericUpDown.ValueChanged += (s, e) =>
            _configuration.MaxRetryAttempts = (int)_maxRetryAttemptsNumericUpDown.Value;
        layout.Controls.Add(_maxRetryAttemptsNumericUpDown, 1, 3);

        _advancedGroup.Controls.Add(layout);
    }

    /// <summary>
    /// Creates the logging group controls.
    /// </summary>
    private void CreateLoggingGroup()
    {
        _loggingGroup = new GroupBox
        {
            Text = "Logging",
            Dock = DockStyle.Fill,
            Height = 80
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };

        // Enable Logging
        _enableLoggingCheckBox = new CheckBox { Text = "Enable Logging", Dock = DockStyle.Fill };
        _enableLoggingCheckBox.CheckedChanged += (s, e) => _configuration.EnableLogging = _enableLoggingCheckBox.Checked;
        layout.Controls.Add(_enableLoggingCheckBox, 0, 0);

        // Enable Debug Logging
        _enableDebugLoggingCheckBox = new CheckBox { Text = "Enable Debug Logging", Dock = DockStyle.Fill };
        _enableDebugLoggingCheckBox.CheckedChanged += (s, e) => _configuration.EnableDebugLogging = _enableDebugLoggingCheckBox.Checked;
        layout.Controls.Add(_enableDebugLoggingCheckBox, 0, 1);

        _loggingGroup.Controls.Add(layout);
    }

    /// <summary>
    /// Creates the validation area.
    /// </summary>
    private void CreateValidationArea()
    {
        _validationLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Red,
            Font = new Font(Font, FontStyle.Bold),
            Text = string.Empty,
            AutoSize = false
        };
    }

    /// <summary>
    /// Creates the dialog buttons.
    /// </summary>
    private void CreateButtons()
    {
        _okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 23)
        };
        _okButton.Click += OkButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(75, 23)
        };
        _cancelButton.Click += CancelButton_Click;

        _testConnectionButton = new Button
        {
            Text = "Test Connection",
            Size = new Size(100, 23)
        };
        _testConnectionButton.Click += TestConnectionButton_Click;

        _resetToDefaultsButton = new Button
        {
            Text = "Reset to Defaults",
            Size = new Size(110, 23)
        };
        _resetToDefaultsButton.Click += ResetToDefaultsButton_Click;

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    /// <summary>
    /// Loads the configuration into the dialog controls.
    /// </summary>
    private void LoadConfiguration()
    {
        _applicationIdTextBox.Text = _configuration.ApplicationId;
        _applicationSecretTextBox.Text = _configuration.ApplicationSecret?.ToInsecureString() ?? string.Empty;
        _environmentComboBox.SelectedItem = _configuration.Environment;
        _hostTextBox.Text = _configuration.Host;
        _portNumericUpDown.Value = _configuration.Port;
        _useSslCheckBox.Checked = _configuration.UseSSL;
        _accountIdNumericUpDown.Value = _configuration.AccountId;
        _heartbeatIntervalNumericUpDown.Value = (decimal)_configuration.HeartbeatInterval.TotalSeconds;
        _connectionTimeoutNumericUpDown.Value = (decimal)_configuration.ConnectionTimeout.TotalSeconds;
        _requestTimeoutNumericUpDown.Value = (decimal)_configuration.RequestTimeout.TotalSeconds;
        _maxRetryAttemptsNumericUpDown.Value = _configuration.MaxRetryAttempts;
        _enableLoggingCheckBox.Checked = _configuration.EnableLogging;
        _enableDebugLoggingCheckBox.Checked = _configuration.EnableDebugLogging;
    }

    /// <summary>
    /// Validates the configuration and updates the validation label.
    /// </summary>
    private void ValidateConfiguration()
    {
        var validationResult = _configuration.ValidateConfiguration();

        if (validationResult.IsValid)
        {
            _validationLabel.Text = "Configuration is valid.";
            _validationLabel.ForeColor = Color.Green;
            _okButton.Enabled = true;
            _testConnectionButton.Enabled = true;
        }
        else
        {
            _validationLabel.Text = "Validation errors:\n" + string.Join("\n", validationResult.Errors);
            _validationLabel.ForeColor = Color.Red;
            _okButton.Enabled = false;
            _testConnectionButton.Enabled = false;
        }
    }

    /// <summary>
    /// Handles the application secret text changed event.
    /// </summary>
    private void ApplicationSecretTextBox_TextChanged(object sender, EventArgs e)
    {
        var secureString = new SecureString();
        foreach (char c in _applicationSecretTextBox.Text)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        _configuration.ApplicationSecret = secureString;
    }

    /// <summary>
    /// Handles the configuration property changed event.
    /// </summary>
    private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        ValidateConfiguration();
    }

    /// <summary>
    /// Handles the OK button click event.
    /// </summary>
    private void OkButton_Click(object sender, EventArgs e)
    {
        if (_configuration.IsValid)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>
    /// Handles the Cancel button click event.
    /// </summary>
    private void CancelButton_Click(object sender, EventArgs e)
    {
        // Restore original configuration
        _originalConfiguration.ApplyTo(_configuration as CTraderMessageAdapter);
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>
    /// Handles the Test Connection button click event.
    /// </summary>
    private void TestConnectionButton_Click(object sender, EventArgs e)
    {
        // TODO: Implement connection testing in future phases
        MessageBox.Show("Connection testing will be implemented in a future phase.",
            "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Handles the Reset to Defaults button click event.
    /// </summary>
    private void ResetToDefaultsButton_Click(object sender, EventArgs e)
    {
        var defaultConfig = new CTraderConfiguration();
        defaultConfig.ApplyTo(_configuration as CTraderMessageAdapter);
        LoadConfiguration();
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_configuration != null)
                _configuration.PropertyChanged -= Configuration_PropertyChanged;
        }

        base.Dispose(disposing);
    }
}
*/