# Implementation Plan: cTrader Connector Integration

**Branch**: `features/002-ctrader-connector-integration` | **Date**: 2025-09-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `Docs/ctrader_connector_specification.md` and feature requirements

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → ✅ Loaded: cTrader Connector Integration specification
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → ✅ Context filled from cTrader specification document
   → Project Type: StockSharp connector class library
3. Fill the Constitution Check section based on the constitution document
   → ✅ StockSharp constitution principles applied
4. Evaluate Constitution Check section below
   → ✅ No violations - follows MessageAdapter patterns
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → ✅ Research cTrader API integration patterns
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, CLAUDE.md
   → ✅ Design message contracts and entity models
7. Re-evaluate Constitution Check section
   → ✅ Design complies with StockSharp architecture
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
   → ✅ TDD-based task planning described
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 8. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary

Create a comprehensive cTrader connector for StockSharp platform providing full market data and trading capabilities. The connector will integrate cTrader's OpenAPI.Net SDK with StockSharp's message-based architecture, supporting real-time market data (ticks, depth, candles), order management (market/limit/stop orders), portfolio tracking, and account management. Implementation follows StockSharp's established MessageAdapter pattern with performance targets of <100ms market data latency and support for 10 concurrent instrument subscriptions.

## Technical Context
**Language/Version**: C# 12.0 (.NET 6)
**Primary Dependencies**: cTrader.OpenAPI.Net (v1.4.4+), StockSharp.Messages, StockSharp.BusinessEntities, Ecng.* libraries
**Storage**: In-memory state management with message-based persistence
**Testing**: MSTest framework targeting net8.0 and net9.0 with limited unit test coverage
**Target Platform**: Windows/.NET 6 class library
**Project Type**: single (StockSharp connector library)
**Performance Goals**: <100ms market data latency, 10 concurrent subscriptions, 10k data points max per historical request
**Constraints**: OAuth2 authentication required, cTrader API rate limits, limited unit testing scope
**Scale/Scope**: Single connector supporting forex, CFDs, cryptocurrencies with basic happy path test coverage

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**KISS Principle Compliance**:
- [x] Solution follows simplest approach that meets requirements
- [x] Each component has single, clear purpose (MessageAdapter pattern)
- [x] Existing StockSharp patterns used (AsyncMessageAdapter, partial classes)
- [x] Complex design alternatives documented and rejected

**SOLID Principles Compliance**:
- [x] Single Responsibility: Adapter handles connection, partial classes handle specific concerns
- [x] Open/Closed: Extensions through MessageAdapter inheritance, not core modifications
- [x] Interface Segregation: IMessageAdapter for core functionality, specific message handlers
- [x] Dependency Inversion: Depends on StockSharp abstractions and cTrader SDK interfaces

**DRY Principle Compliance**:
- [x] No code duplication across similar functionality (reuses BitStamp patterns)
- [x] Common patterns abstracted into Native/ utility layer
- [x] Configuration centralized using common_connectors.props

**Architecture Compliance**:
- [x] Message-based communication used for data flow (ExecutionMessage, QuoteChangeMessage, etc.)
- [x] Extension patterns followed instead of core modifications (MessageAdapter inheritance)
- [x] Backward compatibility maintained for existing integrations

## Project Structure

### Documentation (this feature)
```
specs/002-ctrader-connector-integration/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# StockSharp Connector Structure
Customization/Connectors/CTrader/
├── CTrader.csproj                      # .NET 6 class library
├── CTraderMessageAdapter.cs            # Main adapter class
├── CTraderMessageAdapter_MarketData.cs # Market data handling
├── CTraderMessageAdapter_Transaction.cs # Trading operations
├── CTraderMessageAdapter_Settings.cs   # Configuration
├── CTraderOrderCondition.cs           # Custom order conditions
├── Native/
│   ├── OpenApiClient.cs               # cTrader SDK wrapper
│   ├── Extensions.cs                  # Format conversion utilities
│   └── Models/                        # cTrader-specific data models
└── Properties/
    ├── AssemblyInfo.cs
    └── usings.cs

Customization/Tests/Tests.Connectors.CTrader/
├── Tests.Connectors.CTrader.csproj    # Test project
├── ConnectionTests.cs                 # Connection/authentication tests
├── MarketDataTests.cs                 # Market data subscription tests
└── TradingTests.cs                    # Order execution tests
```

**Structure Decision**: StockSharp connector pattern - single library with test project

## Phase 0: Outline & Research

**Research Topics Identified**:
1. cTrader OpenAPI.Net SDK integration patterns and best practices
2. StockSharp MessageAdapter implementation requirements and message types
3. OAuth2 authentication flow for cTrader platform
4. Performance optimization techniques for <100ms latency requirements
5. Error handling and reconnection strategies for trading systems

**Research Tasks**:
- Research cTrader OpenAPI.Net SDK capabilities and integration patterns
- Analyze existing StockSharp connectors (BitStamp) for architectural patterns
- Document message type mappings between cTrader and StockSharp
- Research OAuth2 authentication implementation for trading APIs
- Define performance benchmarking and optimization strategies

**Output**: research.md with technical decisions and implementation approach

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

**Entity Extraction** → `data-model.md`:
- CTraderSecurityInfo: Symbol, pricing, trading specifications
- CTraderMarketData: Real-time quotes, trades, depth data
- CTraderOrder: Order lifecycle, types, status tracking
- CTraderTrade: Execution details, commission, timestamps
- CTraderPosition: Holdings, P&L, margin requirements
- CTraderPortfolio: Account balances, multi-currency support

**Message Contracts** → `/contracts/`:
- Connection: Authentication, heartbeat, error handling
- MarketData: Subscription management, data streaming
- Trading: Order lifecycle, execution reporting
- Portfolio: Balance updates, position tracking

**Contract Tests**:
- Connection establishment and authentication validation
- Market data subscription and data format validation
- Order placement and status tracking validation
- Portfolio synchronization and update validation

**Integration Scenarios** → `quickstart.md`:
- Basic connection setup and authentication
- Market data subscription workflow
- Order placement and execution workflow
- Portfolio monitoring workflow

**Agent Context Update**:
- Update CLAUDE.md with cTrader connector development context
- Add StockSharp message-based architecture patterns
- Include cTrader API integration specifics

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, CLAUDE.md

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `.specify/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs following TDD principles
- Each message contract → contract test task [P]
- Each entity model → model creation task [P]
- Each user story → integration test task
- Implementation tasks ordered by dependency requirements

**Ordering Strategy**:
- TDD order: Tests before implementation
- Dependency order:
  1. Project setup and configuration
  2. Native API wrapper layer
  3. Core MessageAdapter implementation
  4. Market data handling (partial class)
  5. Trading operations (partial class)
  6. Settings and configuration (partial class)
  7. Integration tests and validation

**Estimated Output**: 20-25 numbered, ordered tasks covering:
- Project setup (3-4 tasks)
- Native layer implementation (4-5 tasks)
- Core adapter implementation (6-8 tasks)
- Testing and validation (4-5 tasks)
- Documentation and integration (2-3 tasks)

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*No constitutional violations identified - design follows established StockSharp patterns*

No complexity deviations from constitutional principles. The design:
- Uses standard MessageAdapter inheritance pattern
- Follows message-based architecture for all data flow
- Implements existing StockSharp connector patterns
- Maintains single responsibility through partial classes
- Avoids core framework modifications

## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command) ✅ research.md created
- [x] Phase 1: Design complete (/plan command) ✅ data-model.md, contracts/, quickstart.md, CLAUDE.md updated
- [x] Phase 2: Task planning complete (/plan command - describe approach only) ✅ TDD strategy defined
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS ✅ No violations identified
- [x] Post-Design Constitution Check: PASS ✅ Design follows StockSharp patterns
- [x] All NEEDS CLARIFICATION resolved (from clarification session) ✅ 5 clarifications completed
- [x] Complexity deviations documented (none required) ✅ No constitutional violations

---
*Based on Constitution v1.1.0 - See `.specify/memory/constitution.md`*