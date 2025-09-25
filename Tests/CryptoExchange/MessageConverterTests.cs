using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.CryptoExchange.MessageConverters;

namespace StockSharp.Tests.CryptoExchange;

[TestClass]
public class MessageConverterTests
{
    [TestMethod]
    public void T043_ExecutionMessageConverter_Should_ConvertTradeData()
    {
        // Arrange
        var converter = new ExecutionMessageConverter();
        var securityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };

        // Mock trade data object
        var tradeData = new
        {
            Id = 12345L,
            Price = 50000.50m,
            Quantity = 0.001m,
            Time = DateTime.UtcNow,
            IsBuyerMaker = false
        };

        // Act
        var result = converter.Convert(tradeData, securityId) as ExecutionMessage;

        // Assert
        Assert.IsNotNull(result, "Should convert to ExecutionMessage");
        Assert.AreEqual(securityId, result.SecurityId, "Should preserve security ID");
        Assert.AreEqual(DataType.Ticks, result.DataType, "Should be tick data");
        Assert.AreEqual(ExecutionTypes.Tick, result.ExecutionType, "Should be tick execution type");
        Assert.AreEqual(12345L, result.TradeId, "Should preserve trade ID");
        Assert.AreEqual(50000.50m, result.Price, "Should preserve price");
        Assert.AreEqual(0.001m, result.Volume, "Should preserve volume");

        Console.WriteLine($"✓ Trade conversion: ID={result.TradeId}, Price={result.Price}, Volume={result.Volume}");
    }

    [TestMethod]
    public void T043_ExecutionMessageConverter_Should_HandleNullValues()
    {
        // Arrange
        var converter = new ExecutionMessageConverter();
        var securityId = new SecurityId { SecurityCode = "ETHUSDT", BoardCode = "BINANCE" };

        var tradeDataWithNulls = new
        {
            Id = (long?)null,
            Price = (decimal?)null,
            Quantity = 0.5m,
            Time = (DateTime?)null
        };

        // Act
        var result = converter.Convert(tradeDataWithNulls, securityId) as ExecutionMessage;

        // Assert
        Assert.IsNotNull(result, "Should handle null values gracefully");
        Assert.AreEqual(securityId, result.SecurityId, "Should preserve security ID");
        Assert.AreEqual(0.5m, result.Volume, "Should preserve non-null volume");

        // Null values should be handled with defaults
        Assert.IsTrue(result.TradeId == null || result.TradeId == 0, "Should handle null trade ID");
        Assert.IsTrue(result.Price == null || result.Price == 0, "Should handle null price");

        Console.WriteLine($"✓ Null handling: Volume={result.Volume}, TradeId={result.TradeId}, Price={result.Price}");
    }

    [TestMethod]
    public void T043_QuoteChangeMessageConverter_Should_ConvertOrderBook()
    {
        // Arrange
        var converter = new QuoteChangeMessageConverter();
        var securityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };

        // Mock order book data
        var orderBookData = new
        {
            Bids = new[]
            {
                new { Price = 49999.99m, Quantity = 0.1m },
                new { Price = 49999.50m, Quantity = 0.2m }
            },
            Asks = new[]
            {
                new { Price = 50000.01m, Quantity = 0.15m },
                new { Price = 50000.50m, Quantity = 0.25m }
            },
            LastUpdateId = 123456789L
        };

        // Act
        var result = converter.Convert(orderBookData, securityId) as QuoteChangeMessage;

        // Assert
        Assert.IsNotNull(result, "Should convert to QuoteChangeMessage");
        Assert.AreEqual(securityId, result.SecurityId, "Should preserve security ID");
        Assert.IsNotNull(result.Quotes, "Should have quotes array");
        Assert.IsTrue(result.Quotes.Length > 0, "Should have quote entries");

        var bids = result.Quotes.Where(q => q.Side == Sides.Buy).ToArray();
        var asks = result.Quotes.Where(q => q.Side == Sides.Sell).ToArray();

        Assert.IsTrue(bids.Length >= 1, "Should have bid quotes");
        Assert.IsTrue(asks.Length >= 1, "Should have ask quotes");

        // Verify bid/ask prices are in correct order
        if (bids.Length > 1)
        {
            Assert.IsTrue(bids[0].Price >= bids[1].Price, "Bids should be in descending price order");
        }

        if (asks.Length > 1)
        {
            Assert.IsTrue(asks[0].Price <= asks[1].Price, "Asks should be in ascending price order");
        }

        Console.WriteLine($"✓ Order book conversion: {bids.Length} bids, {asks.Length} asks");
    }

    [TestMethod]
    public void T043_SecurityMessageConverter_Should_ConvertSymbolData()
    {
        // Arrange
        var converter = new SecurityMessageConverter();
        var securityId = new SecurityId { SecurityCode = "ETHUSDT", BoardCode = "BINANCE" };

        // Mock symbol data from exchange
        var symbolData = new
        {
            Symbol = "ETHUSDT",
            BaseAsset = "ETH",
            QuoteAsset = "USDT",
            Status = "TRADING",
            Filters = new[]
            {
                new { FilterType = "PRICE_FILTER", TickSize = 0.01m },
                new { FilterType = "LOT_SIZE", StepSize = 0.001m, MinQty = 0.001m, MaxQty = 10000m }
            }
        };

        // Act
        var result = converter.Convert(symbolData, securityId) as SecurityMessage;

        // Assert
        Assert.IsNotNull(result, "Should convert to SecurityMessage");
        Assert.AreEqual(securityId, result.SecurityId, "Should preserve security ID");
        Assert.AreEqual("ETHUSDT", result.ShortName, "Should set short name");
        Assert.IsTrue(result.Name?.Contains("ETH"), "Should contain base asset in name");
        Assert.AreEqual(SecurityTypes.Stock, result.SecurityType, "Should default to Stock type for crypto");
        Assert.IsTrue(result.PriceStep > 0, "Should have positive price step");
        Assert.IsTrue(result.VolumeStep > 0, "Should have positive volume step");
        Assert.AreEqual(CurrencyTypes.USD, result.Currency, "Should detect USD currency from USDT");

        Console.WriteLine($"✓ Security conversion: {result.Name}, PriceStep={result.PriceStep}, VolumeStep={result.VolumeStep}");
    }

    [TestMethod]
    public void T043_MessageConverterRegistry_Should_RegisterAndRetrieveConverters()
    {
        // Arrange
        var registry = new MessageConverterRegistry();
        var executionConverter = new ExecutionMessageConverter();
        var quoteConverter = new QuoteChangeMessageConverter();

        // Act
        registry.RegisterConverter(executionConverter);
        registry.RegisterConverter(quoteConverter);

        // Test conversion
        var securityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };
        var tradeData = new { Id = 123L, Price = 50000m, Quantity = 0.001m };

        var result = registry.ConvertToStockSharp(tradeData, securityId);

        // Assert
        Assert.IsNotNull(result, "Should find and use appropriate converter");

        // Test converter existence
        Assert.IsTrue(registry.HasConverter<object, ExecutionMessage>(),
            "Should report execution converter exists");
        Assert.IsTrue(registry.HasConverter<object, QuoteChangeMessage>(),
            "Should report quote converter exists");
        Assert.IsFalse(registry.HasConverter<object, CandleMessage>(),
            "Should report candle converter doesn't exist");

        var converterTypes = registry.GetRegisteredConverters().ToArray();
        Assert.IsTrue(converterTypes.Length >= 2, "Should have registered converters");

        Console.WriteLine($"✓ Registry: {converterTypes.Length} converters registered");
        Console.WriteLine($"✓ Conversion result type: {result?.GetType().Name}");
    }

    [TestMethod]
    public void T043_MessageConverters_Should_HandleInvalidData()
    {
        // Arrange
        var executionConverter = new ExecutionMessageConverter();
        var securityId = new SecurityId { SecurityCode = "INVALID", BoardCode = "TEST" };

        // Test with null input
        var nullResult = executionConverter.Convert(null, securityId);
        Assert.IsNull(nullResult, "Should handle null input gracefully");

        // Test with invalid object type
        var stringResult = executionConverter.Convert("invalid string data", securityId);
        Assert.IsNull(stringResult, "Should handle invalid data type gracefully");

        // Test with empty object
        var emptyResult = executionConverter.Convert(new { }, securityId);
        Assert.IsNotNull(emptyResult, "Should handle empty object and provide defaults");

        Console.WriteLine("✓ Invalid data handling: null, string, empty object handled gracefully");
    }

    [TestMethod]
    public void T043_MessageConverters_Should_PreserveTimestamps()
    {
        // Arrange
        var converter = new ExecutionMessageConverter();
        var securityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };
        var testTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        var tradeDataWithTime = new
        {
            Id = 999L,
            Price = 45000m,
            Quantity = 0.05m,
            Time = testTime,
            Timestamp = testTime.Ticks
        };

        // Act
        var result = converter.Convert(tradeDataWithTime, securityId) as ExecutionMessage;

        // Assert
        Assert.IsNotNull(result, "Should convert successfully");
        Assert.IsTrue(Math.Abs((result.ServerTime - testTime).TotalSeconds) < 1,
            $"Should preserve server time: expected {testTime}, got {result.ServerTime}");
        Assert.IsTrue(result.LocalTime.Year >= DateTime.Now.Year,
            "Local time should be set to current time");

        Console.WriteLine($"✓ Timestamp preservation: Server={result.ServerTime}, Local={result.LocalTime}");
    }

    [TestMethod]
    public void T043_SecurityMessageConverter_Should_DetectCurrencyTypes()
    {
        // Arrange
        var converter = new SecurityMessageConverter();
        var testCases = new[]
        {
            ("BTCUSDT", CurrencyTypes.USD),
            ("ETHUSDC", CurrencyTypes.USD),
            ("ADABUSD", CurrencyTypes.USD),
            ("ETHBTC", CurrencyTypes.BTC),
            ("LINKETH", CurrencyTypes.ETH),
            ("EURBUSD", CurrencyTypes.EUR),
            ("GBPUSDT", CurrencyTypes.GBP),
            ("DOTEUR", CurrencyTypes.EUR),
            ("XRPJPY", CurrencyTypes.USD) // Default to USD for unknown
        };

        foreach (var (symbol, expectedCurrency) in testCases)
        {
            var securityId = new SecurityId { SecurityCode = symbol, BoardCode = "BINANCE" };
            var symbolData = new
            {
                Symbol = symbol,
                QuoteAsset = symbol.Substring(symbol.Length - Math.Min(4, symbol.Length)),
                Status = "TRADING"
            };

            // Act
            var result = converter.Convert(symbolData, securityId) as SecurityMessage;

            // Assert
            Assert.IsNotNull(result, $"Should convert {symbol}");
            Assert.AreEqual(expectedCurrency, result.Currency,
                $"Symbol {symbol} should have currency {expectedCurrency}, got {result.Currency}");

            Console.WriteLine($"✓ {symbol} → {result.Currency}");
        }
    }
}