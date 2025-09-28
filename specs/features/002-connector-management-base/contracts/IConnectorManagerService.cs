using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Messages;

namespace StockSharp.Samples.Foundation.TradingWorkstation.Contracts
{
    /// <summary>
    /// Service contract for connector management operations
    /// Implements functional requirements FR-001 through FR-009
    /// </summary>
    public interface IConnectorManagerService
    {
        /// <summary>
        /// FR-001: Get comprehensive list of all 60+ supported connector types
        /// </summary>
        /// <returns>Available connector types with metadata</returns>
        IEnumerable<ConnectorInfo> GetAvailableConnectors();

        /// <summary>
        /// FR-002: Get all configured accounts for a specific connector type
        /// </summary>
        /// <param name="connectorType">The connector type identifier</param>
        /// <returns>List of configured accounts</returns>
        IEnumerable<ConnectorAccount> GetAccountsByType(string connectorType);

        /// <summary>
        /// FR-002: Create new account configuration for a connector type
        /// </summary>
        /// <param name="connectorType">The connector type</param>
        /// <param name="accountName">User-defined name for the account</param>
        /// <returns>Created account with default configuration</returns>
        ConnectorAccount CreateAccount(string connectorType, string accountName);

        /// <summary>
        /// FR-006: Update account configuration with connector-specific settings
        /// </summary>
        /// <param name="accountId">Account identifier</param>
        /// <param name="configuration">Updated configuration settings</param>
        /// <returns>Success indicator</returns>
        Task<bool> UpdateAccountConfiguration(Guid accountId, ConnectorConfiguration configuration);

        /// <summary>
        /// FR-003: Test connection with 30-second timeout
        /// </summary>
        /// <param name="accountId">Account to test</param>
        /// <returns>Test result with success/failure details</returns>
        Task<ConnectionTestResult> TestConnection(Guid accountId);

        /// <summary>
        /// FR-008: Enable/connect individual connector account
        /// </summary>
        /// <param name="accountId">Account to connect</param>
        /// <returns>Success indicator</returns>
        Task<bool> ConnectAccount(Guid accountId);

        /// <summary>
        /// FR-008: Disable/disconnect individual connector account
        /// </summary>
        /// <param name="accountId">Account to disconnect</param>
        /// <returns>Success indicator</returns>
        Task<bool> DisconnectAccount(Guid accountId);

        /// <summary>
        /// FR-004: Get real-time connection status for account
        /// </summary>
        /// <param name="accountId">Account identifier</param>
        /// <returns>Current connection status</returns>
        ConnectorStatus GetAccountStatus(Guid accountId);

        /// <summary>
        /// FR-007: Get session-only activity log for account
        /// </summary>
        /// <param name="accountId">Account identifier</param>
        /// <returns>Activity entries from current session</returns>
        IEnumerable<ActivityEntry> GetAccountActivity(Guid accountId);

        /// <summary>
        /// FR-009: Save account configuration to persistent storage
        /// </summary>
        /// <param name="accountId">Account to save</param>
        /// <returns>Success indicator</returns>
        Task<bool> SaveAccountConfiguration(Guid accountId);

        /// <summary>
        /// FR-009: Load all saved account configurations on startup
        /// </summary>
        /// <returns>All persisted accounts</returns>
        Task<IEnumerable<ConnectorAccount>> LoadSavedAccounts();

        /// <summary>
        /// Delete account and its configuration
        /// </summary>
        /// <param name="accountId">Account to delete</param>
        /// <returns>Success indicator</returns>
        Task<bool> DeleteAccount(Guid accountId);

        // Events for UI binding and real-time updates

        /// <summary>
        /// Fired when account connection status changes
        /// </summary>
        event EventHandler<AccountStatusChangedEventArgs> AccountStatusChanged;

        /// <summary>
        /// Fired when new activity entry is added
        /// </summary>
        event EventHandler<ActivityEntryEventArgs> ActivityAdded;

        /// <summary>
        /// Fired when account configuration is modified
        /// </summary>
        event EventHandler<AccountConfigurationChangedEventArgs> AccountConfigurationChanged;
    }
}