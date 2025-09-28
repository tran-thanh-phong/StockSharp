using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Tests.ConnectorManagement.IntegrationTests;

[TestClass]
public class PersistenceTests
{
    private IConnectorManagerService _connectorService;

    [TestInitialize]
    public void Setup()
    {
        _connectorService = null; // TODO: Replace with actual service implementation
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public async Task Scenario5_ConfigurationPersistsBetweenSessions()
    {
        // Create multiple accounts with different settings
        var account1 = _connectorService.CreateAccount("Bitstamp", "Persistent Account 1");
        var account2 = _connectorService.CreateAccount("InteractiveBrokers", "Persistent Account 2");

        account1.Configuration.SecureData["ApiKey"] = "persistent_key_1";
        account1.IsEnabled = true;

        account2.Configuration.SecureData["Username"] = "persistent_user_2";
        account2.IsEnabled = false;

        // Save configurations
        await _connectorService.SaveAccountConfiguration(account1.Id);
        await _connectorService.SaveAccountConfiguration(account2.Id);

        // Simulate application restart by reloading
        var savedAccounts = await _connectorService.LoadSavedAccounts();

        // Assert: ✅ All configured accounts appear after restart
        Assert.IsTrue(savedAccounts.Count() >= 2);

        var reloadedAccount1 = savedAccounts.FirstOrDefault(a => a.AccountName == "Persistent Account 1");
        var reloadedAccount2 = savedAccounts.FirstOrDefault(a => a.AccountName == "Persistent Account 2");

        // Assert: ✅ Account names and settings preserved
        Assert.IsNotNull(reloadedAccount1);
        Assert.IsNotNull(reloadedAccount2);
        Assert.AreEqual("Bitstamp", reloadedAccount1.ConnectorType);
        Assert.AreEqual("InteractiveBrokers", reloadedAccount2.ConnectorType);

        // Assert: ✅ Enabled/disabled states maintained
        Assert.IsTrue(reloadedAccount1.IsEnabled);
        Assert.IsFalse(reloadedAccount2.IsEnabled);

        // Assert: ✅ Credentials available (plain text storage per clarification)
        Assert.AreEqual("persistent_key_1", reloadedAccount1.Configuration.SecureData["ApiKey"]);
        Assert.AreEqual("persistent_user_2", reloadedAccount2.Configuration.SecureData["Username"]);
    }
}