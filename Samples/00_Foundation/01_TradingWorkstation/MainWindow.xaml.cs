using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;

using Ecng.Common;
using StockSharp.Samples.Foundation.TradingWorkstation.Services;
using StockSharp.Samples.Foundation.TradingWorkstation.Views;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Samples.Foundation.TradingWorkstation;

/// <summary>
/// Main window for Trading Workstation - provides tabbed interface and status management
/// Integrates with ConnectorManagerService for overall application coordination
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConnectorManagerService _connectorManager;
    private int _activeConnections = 0;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize connector manager service
        _connectorManager = new ConnectorManagerService();

        // Wire up status tracking events
        _connectorManager.AccountStatusChanged += OnAccountStatusChanged;
        _connectorManager.ActivityAdded += OnActivityAdded;

        // Set initial status
        UpdateStatus("StockSharp Trading Workstation ready");
        UpdateConnectionCount();

        // Set up window closing behavior
        Closing += OnWindowClosing;

        // Load ConnectorsView programmatically to bypass XAML namespace issues
        LoadConnectorsView();
    }

    /// <summary>
    /// Load ConnectorsView programmatically to bypass XAML namespace issues
    /// </summary>
    private void LoadConnectorsView()
    {
        try
        {
            // Create SimpleConnectorsView instance (avoids StockSharp entity initialization issues)
            var connectorsView = new Views.SimpleConnectorsView();

            // Replace the placeholder content with the actual view
            ConnectorsTabContent.Children.Clear();
            ConnectorsTabContent.Children.Add(connectorsView);

            UpdateStatus("Connectors view loaded successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to load connectors view: {ex.Message}");

            // Keep placeholder with error message
            ConnectorsTabContent.Children.Clear();
            var errorText = new TextBlock
            {
                Text = $"Failed to load Connectors view: {ex.Message}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20)
            };
            ConnectorsTabContent.Children.Add(errorText);
        }
    }

    /// <summary>
    /// Handle account status changes for global status tracking
    /// </summary>
    private void OnAccountStatusChanged(object sender, AccountStatusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateConnectionCount();

            // Update status indicator based on overall connection state
            var hasConnected = GetConnectedAccountsCount() > 0;
            var hasErrors = GetErrorAccountsCount() > 0;

            if (hasErrors)
            {
                UpdateStatusIndicator(Brushes.Red, "Connection errors detected");
            }
            else if (hasConnected)
            {
                UpdateStatusIndicator(Brushes.Green, "Connected");
            }
            else
            {
                UpdateStatusIndicator(Brushes.Gray, "Disconnected");
            }
        });
    }

    /// <summary>
    /// Handle activity entries for status updates
    /// </summary>
    private void OnActivityAdded(object sender, ActivityEntryEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Update status text with latest activity (if it's an info level or higher)
            if (e.Entry.Level >= ActivityLevel.Info)
            {
                var timestamp = e.Entry.Timestamp.ToString("HH:mm:ss");
                UpdateStatus($"{timestamp} - {e.Entry.Message}");
            }
        });
    }

    /// <summary>
    /// Update main status text
    /// </summary>
    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Update status indicator color and tooltip
    /// </summary>
    private void UpdateStatusIndicator(Brush color, string tooltip)
    {
        StatusIndicator.Fill = color;
        StatusIndicator.ToolTip = tooltip;
    }

    /// <summary>
    /// Update connection count display
    /// </summary>
    private void UpdateConnectionCount()
    {
        var connectedCount = GetConnectedAccountsCount();
        var totalCount = GetTotalAccountsCount();

        ConnectionCountText.Text = $"Connections: {connectedCount}/{totalCount}";
        _activeConnections = connectedCount;
    }

    /// <summary>
    /// Get count of connected accounts from connector manager
    /// </summary>
    private int GetConnectedAccountsCount()
    {
        // This would need to be implemented with proper service integration
        // For now, return a placeholder
        return 0; // TODO: Implement actual connection counting
    }

    /// <summary>
    /// Get count of accounts with errors
    /// </summary>
    private int GetErrorAccountsCount()
    {
        // This would need to be implemented with proper service integration
        return 0; // TODO: Implement actual error counting
    }

    /// <summary>
    /// Get total configured accounts count
    /// </summary>
    private int GetTotalAccountsCount()
    {
        // This would need to be implemented with proper service integration
        return 0; // TODO: Implement actual account counting
    }

    /// <summary>
    /// Settings button click handler
    /// </summary>
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        // Placeholder for application settings dialog
        MessageBox.Show(
            "Application settings will be available in a future version.\n\n" +
            "Currently available:\n" +
            "• Connector configuration in Connectors tab\n" +
            "• Account management and testing\n" +
            "• Session activity logging",
            "Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Help button click handler
    /// </summary>
    private void HelpClick(object sender, RoutedEventArgs e)
    {
        var helpText =
            "StockSharp Trading Workstation\n\n" +
            "Getting Started:\n" +
            "1. Go to Connectors tab\n" +
            "2. Select a connector type (e.g., Bitstamp)\n" +
            "3. Add a new account configuration\n" +
            "4. Configure connection settings\n" +
            "5. Test the connection\n" +
            "6. Connect when ready\n\n" +
            "Features:\n" +
            "• 60+ supported connector types\n" +
            "• Real-time connection status\n" +
            "• Session activity logging\n" +
            "• Configuration persistence\n" +
            "• Material Design UI\n\n" +
            "For more information, visit:\n" +
            "https://stocksharp.com/";

        MessageBox.Show(helpText, "Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Handle window closing - disconnect all accounts
    /// </summary>
    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        try
        {
            // Show confirmation if there are active connections
            if (_activeConnections > 0)
            {
                var result = MessageBox.Show(
                    $"There are {_activeConnections} active connections.\n\n" +
                    "Do you want to disconnect all connections and exit?",
                    "Active Connections",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Disconnect all accounts gracefully
            UpdateStatus("Shutting down - disconnecting all accounts...");

            // TODO: Implement graceful shutdown of all connections
            // This would iterate through all accounts and disconnect them

            UpdateStatus("Shutdown complete");
        }
        catch (Exception ex)
        {
            // Log error but don't prevent shutdown
            UpdateStatus($"Shutdown error: {ex.Message}");
        }
    }

    /// <summary>
    /// Public method to access the ConnectorManager service
    /// This allows other components to interact with the service if needed
    /// </summary>
    public ConnectorManagerService GetConnectorManager()
    {
        return _connectorManager;
    }

    /// <summary>
    /// Public method to programmatically update status from child controls
    /// </summary>
    public void SetStatus(string message)
    {
        UpdateStatus(message);
    }

    /// <summary>
    /// Public method to programmatically update status indicator from child controls
    /// </summary>
    public void SetStatusIndicator(Brush color, string tooltip)
    {
        UpdateStatusIndicator(color, tooltip);
    }
}

