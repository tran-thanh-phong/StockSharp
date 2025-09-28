using System;
using System.ComponentModel;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;
using StockSharp.Configuration;

namespace StockSharp.Tests.ConnectorManagement.ContractTests;

[TestClass]
public class DataTypesTests
{
    [TestMethod]
    public void ConnectorInfo_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var connectorInfo = new ConnectorInfo
        {
            Type = "Bitstamp",
            Name = "Bitstamp Exchange",
            Description = "Crypto trading platform",
            SupportedFeatures = ConnectorCapabilities.MarketData | ConnectorCapabilities.Trading,
            IconPath = "/images/bitstamp.png",
            IsAvailable = true
        };

        // Assert
        Assert.AreEqual("Bitstamp", connectorInfo.Type);
        Assert.AreEqual("Bitstamp Exchange", connectorInfo.Name);
        Assert.IsFalse(string.IsNullOrEmpty(connectorInfo.Description));
        Assert.IsTrue(connectorInfo.SupportedFeatures.HasFlag(ConnectorCapabilities.MarketData));
        Assert.IsTrue(connectorInfo.IsAvailable);
    }

    [TestMethod]
    public void ConnectorAccount_ShouldImplementPropertyChangeNotification()
    {
        // Arrange
        var account = new ConnectorAccount();
        var propertyChangedFired = false;
        string changedProperty = null;

        account.PropertyChanged += (sender, args) =>
        {
            propertyChangedFired = true;
            changedProperty = args.PropertyName;
        };

        // Act
        account.AccountName = "Test Account";

        // Assert
        Assert.IsTrue(propertyChangedFired);
        Assert.AreEqual(nameof(ConnectorAccount.AccountName), changedProperty);
    }

    [TestMethod]
    public void ConnectorConfiguration_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var config = new ConnectorConfiguration();

        // Assert
        Assert.IsNotNull(config.Settings);
        Assert.IsNotNull(config.SecureData);
        Assert.AreEqual(TimeSpan.FromSeconds(30), config.ConnectionTimeout); // Per FR-003 clarification
        Assert.IsFalse(config.AutoReconnect);
        Assert.AreEqual(3, config.MaxRetryAttempts);
    }

    [TestMethod]
    public void ConnectorStatus_ShouldUpdateStateAndNotifyChanges()
    {
        // Arrange
        var status = new ConnectorStatus();
        var propertyChangedCount = 0;
        var propertiesChanged = new List<string>();

        status.PropertyChanged += (sender, args) =>
        {
            propertyChangedCount++;
            propertiesChanged.Add(args.PropertyName);
        };

        // Act
        status.CurrentState = ConnectionState.Connecting;

        // Assert
        Assert.AreEqual(ConnectionState.Connecting, status.CurrentState);
        Assert.IsTrue(propertyChangedCount >= 1);
        Assert.IsTrue(propertiesChanged.Contains(nameof(ConnectorStatus.CurrentState)));
        Assert.IsTrue(propertiesChanged.Contains(nameof(ConnectorStatus.LastStateChange)));
        Assert.IsTrue(status.LastStateChange > DateTime.MinValue);
    }

    [TestMethod]
    public void ActivityEntry_ShouldInitializeWithTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var entry = new ActivityEntry(ActivityLevel.Info, "Test message", "Test details");
        var afterCreation = DateTime.Now;

        // Assert
        Assert.AreEqual(ActivityLevel.Info, entry.Level);
        Assert.AreEqual("Test message", entry.Message);
        Assert.AreEqual("Test details", entry.Details);
        Assert.IsTrue(entry.Timestamp >= beforeCreation);
        Assert.IsTrue(entry.Timestamp <= afterCreation);
    }

    [TestMethod]
    public void ConnectionTestResult_StaticFactories_ShouldCreateCorrectObjects()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var successResult = ConnectionTestResult.Success(duration, "Connection successful");
        var failureResult = ConnectionTestResult.Failure("Connection failed", null, duration);

        // Assert
        Assert.IsTrue(successResult.IsSuccess);
        Assert.AreEqual("Connection successful", successResult.Message);
        Assert.AreEqual(duration, successResult.TestDuration);
        Assert.IsNull(successResult.Exception);

        Assert.IsFalse(failureResult.IsSuccess);
        Assert.AreEqual("Connection failed", failureResult.Message);
        Assert.AreEqual(duration, failureResult.TestDuration);
    }

    [TestMethod]
    public void ConnectionState_Enum_ShouldHaveAllRequiredValues()
    {
        // Act & Assert - Verify all required connection states exist (per FR-004)
        Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Disconnected));
        Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Connecting));
        Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Connected));
        Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Error));
        Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Testing));
    }

    [TestMethod]
    public void ConnectorCapabilities_Flags_ShouldSupportCombinations()
    {
        // Arrange & Act
        var capabilities = ConnectorCapabilities.MarketData | ConnectorCapabilities.Trading | ConnectorCapabilities.Crypto;

        // Assert
        Assert.IsTrue(capabilities.HasFlag(ConnectorCapabilities.MarketData));
        Assert.IsTrue(capabilities.HasFlag(ConnectorCapabilities.Trading));
        Assert.IsTrue(capabilities.HasFlag(ConnectorCapabilities.Crypto));
        Assert.IsFalse(capabilities.HasFlag(ConnectorCapabilities.Options));
    }

    [TestMethod]
    public void AccountStatusChangedEventArgs_ShouldInitializeCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var oldState = ConnectionState.Disconnected;
        var newState = ConnectionState.Connected;
        var message = "Connection established";

        // Act
        var eventArgs = new AccountStatusChangedEventArgs(accountId, oldState, newState, message);

        // Assert
        Assert.AreEqual(accountId, eventArgs.AccountId);
        Assert.AreEqual(oldState, eventArgs.OldState);
        Assert.AreEqual(newState, eventArgs.NewState);
        Assert.AreEqual(message, eventArgs.Message);
    }
}