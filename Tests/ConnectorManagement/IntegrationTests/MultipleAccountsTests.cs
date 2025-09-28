using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Tests.ConnectorManagement.IntegrationTests;

/// <summary>
/// Integration test for Scenario 2: Manage Multiple Accounts
/// Based on quickstart.md acceptance criteria
/// </summary>
[TestClass]
public class MultipleAccountsTests
{
    private IConnectorManagerService _connectorService;

    [TestInitialize]
    public void Setup()
    {
        _connectorService = null; // TODO: Replace with actual service implementation
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Scenario2_MultipleAccountsPerConnector_ShouldBeDistinguishable()
    {
        // Create two Bitstamp accounts with different names
        var account1 = _connectorService.CreateAccount("Bitstamp", "Bitstamp Testing");
        var account2 = _connectorService.CreateAccount("Bitstamp", "Bitstamp Production");

        // Configure differently
        account1.Configuration.SecureData["ApiKey"] = "test_key_1";
        account2.Configuration.SecureData["ApiKey"] = "prod_key_2";

        // Get accounts for connector type
        var bitstampAccounts = _connectorService.GetAccountsByType("Bitstamp");

        // Assert: ✅ Two Bitstamp accounts appear separately
        Assert.AreEqual(2, bitstampAccounts.Count());
        Assert.IsTrue(bitstampAccounts.Any(a => a.AccountName == "Bitstamp Testing"));
        Assert.IsTrue(bitstampAccounts.Any(a => a.AccountName == "Bitstamp Production"));

        // Assert: ✅ Each account has distinct configuration
        var testAccount = bitstampAccounts.First(a => a.AccountName == "Bitstamp Testing");
        var prodAccount = bitstampAccounts.First(a => a.AccountName == "Bitstamp Production");

        Assert.AreNotEqual(testAccount.Id, prodAccount.Id);
        Assert.AreEqual("test_key_1", testAccount.Configuration.SecureData["ApiKey"]);
        Assert.AreEqual("prod_key_2", prodAccount.Configuration.SecureData["ApiKey"]);
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public async Task Scenario2_IndependentAccountManagement()
    {
        var account1 = _connectorService.CreateAccount("Bitstamp", "Account 1");
        var account2 = _connectorService.CreateAccount("Bitstamp", "Account 2");

        // Enable first account
        account1.IsEnabled = true;
        await _connectorService.ConnectAccount(account1.Id);

        // Keep second account disabled
        account2.IsEnabled = false;

        // Verify independent status
        var status1 = _connectorService.GetAccountStatus(account1.Id);
        var status2 = _connectorService.GetAccountStatus(account2.Id);

        // Assert: ✅ Accounts can be enabled/disabled independently
        Assert.AreEqual(ConnectionState.Connected, status1.CurrentState);
        Assert.AreEqual(ConnectionState.Disconnected, status2.CurrentState);
    }
}