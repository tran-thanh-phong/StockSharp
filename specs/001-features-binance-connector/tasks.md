# Tasks: Binance Exchange Connector (CryptoExchange.Net Framework)

**Input**: Design documents from `/specs/001-features-binance-connector/`
**Prerequisites**: plan.md (CryptoExchange.Net framework), data-model.md, contracts/, quickstart.md
**Focus**: First implementation of Binance Spot connector using CryptoExchangeAdapterBase framework

## Execution Flow Summary
1. **Setup Phase**: Create CryptoExchange framework and Binance Spot project structure
2. **Test Phase**: Contract tests and integration tests (TDD approach)
3. **Framework Phase**: Implement CryptoExchangeAdapterBase and message converters
4. **Binance Phase**: Implement BinanceSpotMessageAdapter extending the framework
5. **Integration Phase**: End-to-end testing and validation
6. **Polish Phase**: Unit tests, documentation, and optimization

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Paths are absolute from repository root: `E:\Sources\github\tran-thanh-phong\StockSharp\`

## Phase 3.1: Project Setup

- [x] T001 Create CryptoExchangeBase project structure at `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\`
- [x] T002 Create BinanceSpot project structure at `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\BinanceSpot\`
- [x] T003 Configure CryptoExchangeBase.csproj with CryptoExchange.Net and StockSharp dependencies
- [x] T004 Configure BinanceSpot.csproj with Binance.Net and CryptoExchangeBase project reference
- [x] T005 [P] Configure common_connectors_websocket.props integration for both projects
- [x] T006 [P] Add global using statements (usings.cs) for CryptoExchangeBase project

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests [P]
- [x] T007 [P] Contract test for SecurityLookupMessage → SecurityMessage conversion in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestSecurityLookup.cs`
- [x] T008 [P] Contract test for MarketDataMessage → WebSocket subscription in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestMarketDataSubscription.cs`
- [x] T009 [P] Contract test for OrderRegisterMessage → Binance order placement in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestOrderRegistration.cs`
- [x] T010 [P] Contract test for Binance trade events → ExecutionMessage (Ticks) in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestTradeConversion.cs`
- [x] T011 [P] Contract test for Binance order book → QuoteChangeMessage in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestOrderBookConversion.cs`

### Integration Tests [P]
- [x] T012 [P] Integration test for BinanceSpot connection and authentication in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestBinanceSpotConnection.cs`
- [x] T013 [P] Integration test for real-time market data flow (BTCUSDT) in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestMarketDataFlow.cs`
- [x] T014 [P] Integration test for order lifecycle (place → fill → update) in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestOrderLifecycle.cs`
- [x] T015 [P] Integration test for account balance updates in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\TestAccountUpdates.cs`

## Phase 3.3: CryptoExchange Framework Implementation (ONLY after tests are failing)

### Core Framework Components [P]
- [x] T016 [P] Implement IMessageConverter interface in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\IMessageConverter.cs`
- [x] T017 [P] Implement MessageConverterRegistry in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\MessageConverterRegistry.cs`
- [x] T018 [P] Implement ExecutionMessageConverter in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\ExecutionMessageConverter.cs`
- [x] T019 [P] Implement QuoteChangeMessageConverter in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\QuoteChangeMessageConverter.cs`
- [x] T020 [P] Implement SecurityMessageConverter in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\SecurityMessageConverter.cs`
- [x] T021 [P] Implement PortfolioMessageConverter in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\MessageConverters\PortfolioMessageConverter.cs`

### Base Adapter Implementation
- [x] T022 Implement CryptoExchangeAdapterBase<TRestClient, TSocketClient> in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\CryptoExchangeAdapterBase.cs`
- [x] T023 Implement base connection management (ConnectAsync, DisconnectAsync) in CryptoExchangeAdapterBase
- [x] T024 Implement base message handling infrastructure in CryptoExchangeAdapterBase
- [x] T025 Implement base subscription management in CryptoExchangeAdapterBase
- [x] T026 [P] Implement CryptoExchangeExtensions utility methods in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\Extensions\CryptoExchangeExtensions.cs`

## Phase 3.4: Binance Spot Implementation

### Binance Spot Adapter Core
- [x] T027 Implement BinanceSpotMessageAdapter class extending CryptoExchangeAdapterBase in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\BinanceSpot\BinanceSpotMessageAdapter.cs`
- [x] T028 Implement BinanceSpot settings and authentication in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\BinanceSpot\BinanceSpotMessageAdapter_Settings.cs`
- [x] T029 Implement BinanceSpot market data subscriptions in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\BinanceSpot\BinanceSpotMessageAdapter_MarketData.cs`
- [x] T030 Implement BinanceSpot transaction handling (orders) in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\BinanceSpot\BinanceSpotMessageAdapter_Transaction.cs`

### Binance-Specific Components
- [x] T031 Implement Binance symbol mapping and validation logic in BinanceSpotMessageAdapter
- [x] T032 Implement Binance error handling and rate limit management in BinanceSpotMessageAdapter
- [x] T033 Implement Binance WebSocket event handling (trades, order book, user data) in BinanceSpotMessageAdapter
- [x] T034 Implement Binance REST API integration (exchange info, account, orders) in BinanceSpotMessageAdapter

## Phase 3.5: Integration & Testing

### End-to-End Validation
- [x] T035 Validate SecurityLookupMessage → BinanceSpot exchange info → SecurityMessage flow
- [x] T036 Validate MarketDataMessage → BinanceSpot WebSocket → StockSharp messages flow
- [x] T037 Validate OrderRegisterMessage → BinanceSpot REST API → ExecutionMessage flow
- [x] T038 Validate real-time account updates via BinanceSpot user data stream

### Connection & Error Handling
- [ ] T039 Test BinanceSpot testnet connection and authentication
- [ ] T040 Test BinanceSpot rate limit handling and backoff strategies
- [ ] T041 Test BinanceSpot WebSocket reconnection logic
- [ ] T042 Test BinanceSpot error mapping from Binance.Net to StockSharp ErrorMessage

## Phase 3.6: Polish & Documentation

### Unit Tests [P]
- [ ] T043 [P] Unit tests for message converters in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\MessageConverterTests.cs`
- [ ] T044 [P] Unit tests for symbol mapping and validation in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\BinanceSymbolTests.cs`
- [ ] T045 [P] Unit tests for error handling and rate limiting in `E:\Sources\github\tran-thanh-phong\StockSharp\Tests\CryptoExchange\BinanceErrorHandlingTests.cs`

### Performance & Optimization
- [ ] T046 Performance test: Market data processing latency (<50ms requirement)
- [ ] T047 Performance test: Order execution roundtrip time (<100ms requirement)
- [ ] T048 Memory usage validation (<2GB requirement under load)

### Documentation Updates [P]
- [ ] T049 [P] Update `E:\Sources\github\tran-thanh-phong\StockSharp\Samples\Connectors\BinanceSpotSample.cs` with working example
- [ ] T050 [P] Update quickstart guide with actual usage examples in `E:\Sources\github\tran-thanh-phong\StockSharp\specs\001-features-binance-connector\quickstart.md`
- [ ] T051 [P] Create framework extension guide for adding new exchanges in `E:\Sources\github\tran-thanh-phong\StockSharp\Connectors\CryptoExchangeBase\README.md`

## Dependencies

### Setup Dependencies
- T001, T002 → T003, T004 → T005, T006

### Test Dependencies
- T007-T015: All tests can run in parallel (different files)
- All tests MUST be failing before starting T016

### Implementation Dependencies
- **Framework**: T016 → T017 → T018-T021 (converters parallel) → T022 → T023-T025 → T026
- **Binance**: T022-T026 complete → T027 → T028-T030 (parallel partial classes) → T031-T034
- **Integration**: T027-T034 complete → T035-T038 → T039-T042
- **Polish**: T035-T042 complete → T043-T045 (parallel) → T046-T048 → T049-T051 (parallel)

## Parallel Execution Examples

### Phase 3.2 - Contract Tests (Launch Together)
```
Task: "Contract test SecurityLookupMessage conversion in Tests/CryptoExchange/TestSecurityLookup.cs"
Task: "Contract test MarketDataMessage subscription in Tests/CryptoExchange/TestMarketDataSubscription.cs"
Task: "Contract test OrderRegisterMessage placement in Tests/CryptoExchange/TestOrderRegistration.cs"
Task: "Contract test trade event conversion in Tests/CryptoExchange/TestTradeConversion.cs"
Task: "Contract test order book conversion in Tests/CryptoExchange/TestOrderBookConversion.cs"
```

### Phase 3.3 - Message Converters (Launch Together)
```
Task: "Implement ExecutionMessageConverter in MessageConverters/ExecutionMessageConverter.cs"
Task: "Implement QuoteChangeMessageConverter in MessageConverters/QuoteChangeMessageConverter.cs"
Task: "Implement SecurityMessageConverter in MessageConverters/SecurityMessageConverter.cs"
Task: "Implement PortfolioMessageConverter in MessageConverters/PortfolioMessageConverter.cs"
```

### Phase 3.4 - Binance Partials (Launch Together)
```
Task: "Implement BinanceSpot settings in BinanceSpotMessageAdapter_Settings.cs"
Task: "Implement BinanceSpot market data in BinanceSpotMessageAdapter_MarketData.cs"
Task: "Implement BinanceSpot transactions in BinanceSpotMessageAdapter_Transaction.cs"
```

## Notes
- **Focus**: First Binance Spot connector implementation only (BinanceFutures in separate feature)
- **Architecture**: CryptoExchange.Net wrapper approach for rapid extensibility
- **Testing**: TDD approach - all tests must fail before implementation begins
- **Performance**: Target <50ms market data, <100ms orders (MVP acceptable performance)
- **Future**: Framework designed for 2-3 day additional exchange implementations

## Task Generation Rules Applied
1. **From Contracts**: Each API endpoint → contract test + implementation
2. **From Data Model**: Each message converter → separate implementation task
3. **From Quickstart**: Connection scenarios → integration tests
4. **TDD Ordering**: All tests before any implementation
5. **Parallel**: Different files marked [P], same file sequential

## Validation Checklist ✅
- [x] All contracts have corresponding tests (T007-T011)
- [x] All message converters have implementation tasks (T018-T021)
- [x] All tests come before implementation (T007-T015 → T016+)
- [x] Parallel tasks truly independent (different files, no shared state)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] Dependencies clearly documented
- [x] Integration tests cover user scenarios from quickstart
- [x] Framework extensibility built for future exchanges