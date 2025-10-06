# cTrader Connector for StockSharp

Production-quality cTrader connector built from scratch following StockSharp best practices.

## Status

**Phase 1 Complete** ✅ - Project structure and skeleton implementation

## Project Structure

```
CTraderConnector/
├── CTrader Connector.csproj          # Project file
├── README.md                          # This file
├── Properties/
│   ├── AssemblyInfo.cs                # Assembly metadata
│   └── usings.cs                      # Global usings
├── CTraderMessageAdapter.cs           # Main adapter (Connect/Disconnect)
├── CTraderMessageAdapter_Settings.cs  # Configuration & persistence
├── CTraderMessageAdapter_MarketData.cs    # Market data subscriptions
├── CTraderMessageAdapter_Transaction.cs   # Order execution & portfolio
├── CTraderOrderCondition.cs           # Order conditions (SL/TP/Trailing)
└── Native/
    ├── CTraderClient.cs               # OpenAPI client wrapper
    └── Extensions.cs                  # Conversion helpers
```

## Features

### Implemented (Skeleton)
- ✅ Proper file organization (4 partial classes)
- ✅ Configuration properties with Save/Load
- ✅ Market data capability declarations
- ✅ Transaction capability declarations
- ✅ Order conditions (Stop Loss, Take Profit, Trailing Stop)
- ✅ Native client infrastructure
- ✅ BoardCodes integration (`BoardCodes.CTrader`)

### Pending Implementation (Phases 6-7)
- ⏳ OpenAPI SDK integration
- ⏳ Symbol lookup (ProtoOASymbolsListReq)
- ⏳ Market data subscriptions (Ticks, Depth, Level1, Candles)
- ⏳ Order execution (Register, Cancel, Modify)
- ⏳ Portfolio management
- ⏳ Position tracking
- ⏳ Error handling and reconnection

## Comparison with Reference

| Aspect | BitStamp (Reference) | CTraderConnector | Status |
|--------|---------------------|------------------|--------|
| File structure | 4 partials | 4 partials | ✅ Match |
| Native folder | Yes | Yes | ✅ Match |
| Settings persistence | Save/Load | Save/Load | ✅ Match |
| Order conditions | BitStampOrderCondition | CTraderOrderCondition | ✅ Match |
| Native client | HttpClient + PusherClient | CTraderClient (stub) | ⏳ Pending |
| Extensions | ToStockSharp/ToCurrency | ToStockSharp/ToCTraderVolume | ✅ Match |

## Configuration Properties

- **ApplicationId**: cTrader OAuth2 application ID
- **ApplicationSecret**: cTrader OAuth2 secret (SecureString)
- **AccountId**: Trading account ID
- **Host**: Server host (default: demo.ctraderapi.com)
- **Port**: Server port (default: 5035)
- **Environment**: Demo or Live

## Dependencies

- cTrader.OpenAPI.Net 1.4.4
- StockSharp core libraries (Messages, Algo, BusinessEntities)

## Build Status

✅ **Compiles successfully** with no errors, only warnings (unused fields in stubs)

## Next Steps

1. **Phase 6**: Implement CTraderClient with actual OpenAPI SDK integration
2. **Phase 7**: Implement market data subscriptions and order execution
3. **Phase 8**: Add unit tests
4. **Phase 9**: Integration testing with demo account
5. **Phase 10**: Move to `Connectors/CTrader` when production-ready

## Differences from Old CTrader Project

| Old Project | New CTraderConnector |
|-------------|----------------------|
| Monolithic single file (512 lines) | 4 organized partials |
| Stub native layer | Proper native client structure |
| Phase 3-4 TODO comments | Clean skeleton ready for implementation |
| No settings persistence | Full Save/Load support |
| Basic properties | Comprehensive configuration |
| Non-standard location | Following connector patterns |

## Notes

- Old `Customization/Connectors/CTrader` project kept for reference
- This is a clean rebuild following BitStamp patterns exactly
- BoardCodes.CTrader added to Messages/BoardCodes.cs
- Ready for OpenAPI SDK integration in next phase
