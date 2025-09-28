using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Samples.Foundation.TradingWorkstation.Contracts;

namespace StockSharp.Tests.ConnectorManagement.ContractTests;

[TestClass]
public class ConnectorServiceTests
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
    public void GetAvailableConnectors_ShouldReturnConnectorList()
    {
        // Arrange
        // Service not implemented yet - test should fail

        // Act
        var connectors = _connectorService.GetAvailableConnectors();

        // Assert - These assertions should fail initially
        Assert.IsNotNull(connectors);
        Assert.IsTrue(connectors.Any());
        Assert.IsTrue(connectors.Count() >= 60); // Per requirement FR-001: 60+ connectors

        // Verify Bitstamp is in the list (our primary example)
        var bitstampConnector = connectors.FirstOrDefault(c => c.Type == "Bitstamp");
        Assert.IsNotNull(bitstampConnector);
        Assert.AreEqual("Bitstamp", bitstampConnector.Name);
        Assert.IsTrue(bitstampConnector.IsAvailable);
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void GetAvailableConnectors_ShouldReturnConnectorWithRequiredProperties()
    {
        // Arrange
        // Service not implemented yet - test should fail

        // Act
        var connectors = _connectorService.GetAvailableConnectors();

        // Assert - Check that each connector has required properties
        foreach (var connector in connectors)
        {
            Assert.IsFalse(string.IsNullOrEmpty(connector.Type));
            Assert.IsFalse(string.IsNullOrEmpty(connector.Name));
            Assert.IsNotNull(connector.Description);
            // SupportedFeatures should have at least one capability
            Assert.AreNotEqual(ConnectorCapabilities.None, connector.SupportedFeatures);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void GetAvailableConnectors_ShouldIncludeRequiredConnectorTypes()
    {
        // Arrange
        var requiredConnectors = new[] { "Bitstamp", "InteractiveBrokers", "MT4", "MT5", "BitFinex" };

        // Act
        var connectors = _connectorService.GetAvailableConnectors();
        var connectorTypes = connectors.Select(c => c.Type).ToList();

        // Assert - Verify required connectors are present (per FR-001)
        foreach (var required in requiredConnectors)
        {
            Assert.IsTrue(connectorTypes.Contains(required),
                $"Required connector type '{required}' not found in available connectors");
        }
    }
}