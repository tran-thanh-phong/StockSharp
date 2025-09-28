# Tasks: cTrader Connector Integration

**Input**: Design documents from `/specs/features/002-ctrader-connector-integration/`
**Prerequisites**: plan.md (✅), research.md (✅), data-model.md (✅), contracts/ (✅), quickstart.md (✅)

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → ✅ Found: StockSharp connector class library (.NET 6)
   → Extract: C# 12.0, cTrader.OpenAPI.Net, MSTest framework
2. Load optional design documents:
   → data-model.md: 6 entities (CTraderSecurityInfo, CTraderMarketData, etc.)
   → contracts/: 4 contract files (connection, marketdata, trading, portfolio)
   → research.md: OAuth2, message mappings, performance requirements
3. Generate tasks by category:
   → Setup: project creation, dependencies, configuration
   → Tests: contract tests for all 4 contracts, integration scenarios
   → Core: entities, adapters, message handlers, native SDK wrapper
   → Integration: authentication, subscriptions, error handling
   → Polish: unit tests, performance validation, documentation
4. Apply task rules:
   → Different files = mark [P] for parallel execution
   → Tests before implementation (TDD compliance)
   → Models before adapters, core before integration
5. Number tasks sequentially (T001-T024)
6. StockSharp-specific structure: Customization/Connectors/CTrader/ + test project
7. Validation: All contracts tested, all entities modeled, TDD followed
8. Return: SUCCESS (24 tasks ready for StockSharp connector implementation)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths for StockSharp project structure

## Path Conventions (StockSharp Connector)
- **Connector Library**: `Customization/Connectors/CTrader/`
- **Test Project**: `Customization/Tests/Tests.Connectors.CTrader/`
- **Native Layer**: `Customization/Connectors/CTrader/Native/`
- **Properties**: `Customization/Connectors/CTrader/Properties/`

## Phase 3.1: Setup and Project Structure

- [x] **T001** Create CTrader connector project structure in `Customization/Connectors/CTrader/` with .NET 6 class library
- [x] **T002** Create test project structure in `Customization/Tests/Tests.Connectors.CTrader/` with MSTest framework targeting net8.0 and net9.0
- [x] **T003** [P] Configure project dependencies: cTrader.OpenAPI.Net (v1.4.4+), import common_connectors.props for StockSharp references
- [x] **T004** [P] Setup assembly info and usings in `Customization/Connectors/CTrader/Properties/`

## Phase 3.2: Contract Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [ ] **T005** [P] Connection contract test: OAuth2 authentication, heartbeat, error handling in `Customization/Tests/Tests.Connectors.CTrader/ConnectionTests.cs`
- [ ] **T006** [P] Market data contract test: subscription management, real-time updates, historical data in `Customization/Tests/Tests.Connectors.CTrader/MarketDataTests.cs`
- [ ] **T007** [P] Trading contract test: order lifecycle, execution handling, position tracking in `Customization/Tests/Tests.Connectors.CTrader/TradingTests.cs`
- [ ] **T008** [P] Portfolio contract test: balance updates, position monitoring, risk management in `Customization/Tests/Tests.Connectors.CTrader/PortfolioTests.cs`

## Phase 3.3: Native SDK Integration Layer (ONLY after tests are failing)

- [ ] **T009** [P] Create cTrader OpenAPI client wrapper in `Customization/Connectors/CTrader/Native/OpenApiClient.cs`
- [ ] **T010** [P] Create format conversion extensions for cTrader ↔ StockSharp data mapping in `Customization/Connectors/CTrader/Native/Extensions.cs`
- [ ] **T011** [P] Create cTrader native models for orders, trades, positions in `Customization/Connectors/CTrader/Native/Models/`
- [ ] **T012** [P] Create CTraderOrderCondition for custom order types in `Customization/Connectors/CTrader/CTraderOrderCondition.cs`

## Phase 3.4: Core MessageAdapter Implementation

- [ ] **T013** Create main CTraderMessageAdapter class inheriting from AsyncMessageAdapter in `Customization/Connectors/CTrader/CTraderMessageAdapter.cs`
- [ ] **T014** Implement connection management and authentication in CTraderMessageAdapter.cs (ConnectAsync, DisconnectAsync, OAuth2 flow)
- [ ] **T015** Create market data subscription handler in `Customization/Connectors/CTrader/CTraderMessageAdapter_MarketData.cs`
- [ ] **T016** Implement real-time data processing (ticks, depth, level1) in CTraderMessageAdapter_MarketData.cs
- [ ] **T017** Implement historical data retrieval (candles, time series) with 10k limit in CTraderMessageAdapter_MarketData.cs
- [ ] **T018** Create trading operations handler in `Customization/Connectors/CTrader/CTraderMessageAdapter_Transaction.cs`
- [ ] **T019** Implement order management (register, cancel, status) in CTraderMessageAdapter_Transaction.cs
- [ ] **T020** Implement portfolio tracking and position updates in CTraderMessageAdapter_Transaction.cs

## Phase 3.5: Configuration and Settings

- [ ] **T021** Create settings and configuration properties in `Customization/Connectors/CTrader/CTraderMessageAdapter_Settings.cs`
- [ ] **T022** Implement security lookup and instrument management in CTraderMessageAdapter_Settings.cs
- [ ] **T023** Add performance monitoring and latency tracking (<100ms requirement) in CTraderMessageAdapter.cs

## Phase 3.6: Polish and Validation

- [ ] **T024** [P] Run quickstart validation scenarios and update documentation based on test results

## Dependencies

**Phase Blocking:**
- Setup (T001-T004) before everything
- Contract tests (T005-T008) before any implementation
- Native layer (T009-T012) before MessageAdapter core
- Core implementation (T013-T020) before settings
- All implementation before polish (T024)

**File Dependencies:**
- T013 blocks T014 (same file: CTraderMessageAdapter.cs)
- T015 blocks T016, T017 (same file: CTraderMessageAdapter_MarketData.cs)
- T018 blocks T019, T020 (same file: CTraderMessageAdapter_Transaction.cs)
- T021 blocks T022 (same file: CTraderMessageAdapter_Settings.cs)

## Parallel Execution Examples

### Phase 3.1 - Setup (can run together)
```bash
# Launch T003-T004 in parallel:
Task: "Configure project dependencies: cTrader.OpenAPI.Net (v1.4.4+), import common_connectors.props"
Task: "Setup assembly info and usings in Customization/Connectors/CTrader/Properties/"
```

### Phase 3.2 - Contract Tests (can run together)
```bash
# Launch T005-T008 in parallel:
Task: "Connection contract test: OAuth2 authentication, heartbeat, error handling in ConnectionTests.cs"
Task: "Market data contract test: subscription management, real-time updates in MarketDataTests.cs"
Task: "Trading contract test: order lifecycle, execution handling in TradingTests.cs"
Task: "Portfolio contract test: balance updates, position monitoring in PortfolioTests.cs"
```

### Phase 3.3 - Native Layer (can run together)
```bash
# Launch T009-T012 in parallel:
Task: "Create cTrader OpenAPI client wrapper in Native/OpenApiClient.cs"
Task: "Create format conversion extensions in Native/Extensions.cs"
Task: "Create cTrader native models in Native/Models/"
Task: "Create CTraderOrderCondition for custom order types"
```

## Entity Implementation Mapping

**From data-model.md:**
- CTraderSecurityInfo → SecurityMessage mapping (T010, T022)
- CTraderMarketData → QuoteChangeMessage, ExecutionMessage (T010, T016)
- CTraderOrder → OrderRegisterMessage, ExecutionMessage (T010, T019)
- CTraderTrade → ExecutionMessage with trade details (T010, T019)
- CTraderPosition → PositionChangeMessage (T010, T020)
- CTraderPortfolio → PortfolioMessage (T010, T020)

## Contract Implementation Mapping

**From contracts/ directory:**
- connection-contract.md → T005, T013, T014 (OAuth2, heartbeat, error handling)
- marketdata-contract.md → T006, T015, T016, T017 (subscriptions, real-time, historical)
- trading-contract.md → T007, T018, T019 (orders, execution, cancellation)
- portfolio-contract.md → T008, T020 (balances, positions, risk monitoring)

## Performance Requirements Validation

**From clarifications and requirements:**
- Market data latency <100ms (T023)
- 10 concurrent instrument subscriptions (T016)
- 10k max data points per historical request (T017)
- OAuth2 authentication with secure token storage (T014)
- Automatic reconnection capabilities (T014)

## Notes

- [P] tasks target different files with no shared dependencies
- Follow TDD: tests MUST fail before implementation
- All message handling follows StockSharp message-based architecture
- Limited unit test scope per requirements (focus on contract and integration tests)
- Inherit from AsyncMessageAdapter following StockSharp connector patterns
- Use partial classes for logical separation (MarketData, Transaction, Settings)

## Validation Checklist
*GATE: Checked before task execution*

- [x] All 4 contracts have corresponding tests (T005-T008)
- [x] All 6 entities covered in native models and extensions (T010-T011)
- [x] All tests come before implementation (T005-T008 before T013+)
- [x] Parallel tasks truly independent (different files, no shared state)
- [x] Each task specifies exact file path in StockSharp structure
- [x] No task modifies same file as another [P] task
- [x] MessageAdapter inheritance pattern followed
- [x] Performance requirements addressed in implementation tasks