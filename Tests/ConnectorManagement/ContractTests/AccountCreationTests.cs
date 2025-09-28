using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Tests.ConnectorManagement.ContractTests;

[TestClass]
public class AccountCreationTests
{
    private IConnectorManagerService _connectorService;

    [TestInitialize]
    public void Setup()
    {
        // This will fail until we implement the service
        _connectorService = null; // TODO: Replace with actual service implementation
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void CreateAccount_WithValidParameters_ShouldReturnNewAccount()
    {
        // Arrange
        var connectorType = "Bitstamp";
        var accountName = "Test Bitstamp Account";

        // Act
        var account = _connectorService.CreateAccount(connectorType, accountName);

        // Assert - These assertions should fail initially
        Assert.IsNotNull(account);
        Assert.AreNotEqual(Guid.Empty, account.Id);
        Assert.AreEqual(connectorType, account.ConnectorType);
        Assert.AreEqual(accountName, account.AccountName);
        Assert.IsNotNull(account.Configuration);
        Assert.IsFalse(account.IsEnabled); // Should be disabled by default
        Assert.IsTrue(account.CreatedDate > DateTime.MinValue);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CreateAccount_WithInvalidConnectorType_ShouldThrowException()
    {
        // Arrange
        var invalidConnectorType = "NonExistentConnector";
        var accountName = "Test Account";

        // Act & Assert
        _connectorService.CreateAccount(invalidConnectorType, accountName);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CreateAccount_WithEmptyAccountName_ShouldThrowException()
    {
        // Arrange
        var connectorType = "Bitstamp";
        var emptyAccountName = "";

        // Act & Assert
        _connectorService.CreateAccount(connectorType, emptyAccountName);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void CreateAccount_WithNullParameters_ShouldThrowException()
    {
        // Act & Assert
        _connectorService.CreateAccount(null, null);
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void CreateAccount_ShouldCreateUniqueAccounts()
    {
        // Arrange
        var connectorType = "Bitstamp";
        var accountName1 = "Bitstamp Account 1";
        var accountName2 = "Bitstamp Account 2";

        // Act
        var account1 = _connectorService.CreateAccount(connectorType, accountName1);
        var account2 = _connectorService.CreateAccount(connectorType, accountName2);

        // Assert - Each account should be unique (per FR-002: multiple accounts per connector)
        Assert.AreNotEqual(account1.Id, account2.Id);
        Assert.AreEqual(accountName1, account1.AccountName);
        Assert.AreEqual(accountName2, account2.AccountName);
        Assert.AreEqual(connectorType, account1.ConnectorType);
        Assert.AreEqual(connectorType, account2.ConnectorType);
    }
}