using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

/// <summary>
/// Contract for Connector Management Service - defines all connector lifecycle operations
/// Implements functional requirements FR-001 through FR-009 from specification
/// </summary>
public interface IConnectorManagerService
{
    #region Core Operations (FR-001, FR-002)

    /// <summary>
    /// FR-001: Get comprehensive list of 60+ supported connector types
    /// </summary>
    IEnumerable<ConnectorInfo> GetAvailableConnectors();

    /// <summary>
    /// FR-002: Get all configured accounts for specific connector type
    /// </summary>
    IEnumerable<ConnectorAccount> GetAccountsByType(string connectorType);

    /// <summary>
    /// FR-002: Create new account configuration for connector type
    /// </summary>
    ConnectorAccount CreateAccount(string connectorType, string accountName);

    #endregion

    #region Connection Management (FR-003, FR-008)

    /// <summary>
    /// FR-003: Test connection with 30-second timeout
    /// </summary>
    Task<ConnectionTestResult> TestConnection(Guid accountId);

    /// <summary>
    /// FR-008: Enable/connect individual connector account
    /// </summary>
    Task<bool> ConnectAccount(Guid accountId);

    /// <summary>
    /// FR-008: Disable/disconnect individual connector account
    /// </summary>
    Task<bool> DisconnectAccount(Guid accountId);

    #endregion

    #region Status and Monitoring (FR-004, FR-007)

    /// <summary>
    /// FR-004: Get real-time connection status for account
    /// </summary>
    ConnectorStatus GetAccountStatus(Guid accountId);

    /// <summary>
    /// FR-007: Get session-only activity log for account
    /// </summary>
    IEnumerable<ActivityEntry> GetAccountActivity(Guid accountId);

    #endregion

    #region Configuration Management (FR-006, FR-009)

    /// <summary>
    /// FR-006: Update account configuration with connector-specific settings
    /// </summary>
    Task<bool> UpdateAccountConfiguration(Guid accountId, ConnectorConfiguration configuration);

    /// <summary>
    /// FR-009: Save account configuration to persistent storage
    /// </summary>
    Task<bool> SaveAccountConfiguration(Guid accountId);

    /// <summary>
    /// FR-009: Load all saved account configurations on startup
    /// </summary>
    Task<IEnumerable<ConnectorAccount>> LoadSavedAccounts();

    /// <summary>
    /// Delete account and its configuration
    /// </summary>
    Task<bool> DeleteAccount(Guid accountId);

    #endregion

    #region Events

    /// <summary>
    /// Fired when account connection status changes
    /// </summary>
    event EventHandler<AccountStatusChangedEventArgs> AccountStatusChanged;

    /// <summary>
    /// Fired when new activity entry is added
    /// </summary>
    event EventHandler<ActivityEntryEventArgs> ActivityAdded;

    /// <summary>
    /// Fired when account configuration is changed
    /// </summary>
    event EventHandler<AccountConfigurationChangedEventArgs> AccountConfigurationChanged;

    #endregion
}