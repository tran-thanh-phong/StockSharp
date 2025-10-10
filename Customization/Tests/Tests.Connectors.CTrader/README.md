# CTrader Connector Tests

Comprehensive test suite for the cTrader connector using **StockSharp IMessageAdapter interface pattern**.

## Testing Approach: Interface-Based Pattern

✅ **Tests Real User API** - Validates what developers actually use  
✅ **No Protobuf Complexity** - Avoids OpenAPI.Net internal structures  
✅ **StockSharp Conventions** - Follows patterns from existing tests  
✅ **Portable & Maintainable** - Same pattern works for all connectors  

## Test Statistics

- **Total Tests**: 39
- **Test Files**: 5
- **Build Status**: ✅ Zero errors
- **Pattern**: Interface-based testing (IMessageAdapter)

## Running Tests

```bash
# Build
dotnet build Customization/Tests/Tests.Connectors.CTrader/

# Run all tests
dotnet test Customization/Tests/Tests.Connectors.CTrader/

# Run specific category
dotnet test --filter "TestCategory=Unit"
dotnet test --filter "TestCategory=MarketData"
```

## Test Categories

1. **Authentication Tests** (9 tests) - Settings & configuration
2. **Security Tests** (5 tests) - Symbol lookup & mapping
3. **Market Data Tests** (10 tests) - Subscriptions (Ticks, Depth, Level1)
4. **Transaction Tests** (8 tests) - Order messages (Market, Limit, Stop)
5. **Adapter Tests** (7 tests) - Capabilities & persistence

See individual test files for details.
