
# Implementation Plan: Binance Exchange Connector

**Branch**: `001-features-binance-connector` | **Date**: 2025-09-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-features-binance-connector/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code or `AGENTS.md` for opencode).
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Implement extensible cryptocurrency exchange connector framework using CryptoExchange.Net library. Create BinanceSpot and BinanceFutures connectors as first implementations of shared CryptoExchangeAdapterBase, enabling rapid expansion to 20+ additional exchanges. The framework follows StockSharp's MessageAdapter pattern while leveraging proven CryptoExchange.Net infrastructure for optimal development velocity and multi-exchange support. Location: `/Connectors/Binance*` with shared base at `/Connectors/CryptoExchangeBase/`.

## Technical Context
**Language/Version**: C# 12.0 with .NET 8.0/9.0 target frameworks
**Primary Dependencies**: CryptoExchange.Net, Binance.Net, Ecng.Common, existing StockSharp MessageAdapter framework
**Storage**: Local message storage, no direct database dependencies (follows StockSharp patterns)
**Testing**: MSTest framework targeting net8.0 and net9.0 using common_target_tests.props
**Target Platform**: Multi-platform (.NET Standard 2.0/2.1 + net6.0-windows compatibility)
**Project Type**: Extensible cryptocurrency exchange connector framework (MVP for rapid multi-exchange support)
**Performance Goals**: <50ms market data processing (acceptable for MVP), <100ms order execution latency, 1k+ updates/sec throughput
**Constraints**: <2GB memory usage, message-based architecture compliance, rate limit respect via CryptoExchange.Net
**Scale/Scope**: 20+ cryptocurrency exchanges, rapid development velocity (2-3 days per new exchange)
**User Requirements**: Extensible framework at /Connectors/CryptoExchangeBase/ with Binance implementation at /Connectors/Binance*

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**KISS Principle Compliance**:
- [x] Solution follows simplest approach that meets requirements (CryptoExchange.Net wrapper vs custom implementations for 20+ exchanges)
- [x] Each component has single, clear purpose (CryptoExchangeAdapterBase, BinanceSpot, BinanceFutures)
- [x] Leverages existing proven libraries (CryptoExchange.Net) rather than reinventing complex functionality
- [x] MVP approach prioritizes simplicity and extensibility over micro-optimizations

**SOLID Principles Compliance**:
- [x] Single Responsibility: Base class handles crypto exchange patterns, specific adapters handle exchange details
- [x] Open/Closed: New exchanges added by extending base class, no modifications to existing code
- [x] Interface Segregation: StockSharp interfaces maintained, CryptoExchange.Net abstracted away
- [x] Dependency Inversion: Depends on CryptoExchange.Net abstractions, not concrete implementations

**DRY Principle Compliance**:
- [x] Authentication, rate limiting, WebSocket management written once in base class
- [x] Message conversion patterns reused across all cryptocurrency exchanges
- [x] CryptoExchange.Net eliminates need to rewrite HTTP/WebSocket infrastructure per exchange

**Architecture Compliance**:
- [x] Message-based communication preserved (CryptoExchange.Net → StockSharp message conversion)
- [x] Extension patterns followed (inherits from AsyncMessageAdapter)
- [x] Backward compatibility maintained (standard StockSharp MessageAdapter interface)

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# Cryptocurrency Exchange Connector Framework
Connectors/
├── CryptoExchangeBase/                    # Shared framework
│   ├── CryptoExchangeAdapterBase.cs      # Abstract base adapter
│   ├── MessageConverters/                # Common conversion logic
│   │   ├── ExecutionMessageConverter.cs
│   │   ├── QuoteChangeMessageConverter.cs
│   │   └── SecurityMessageConverter.cs
│   ├── Extensions/                       # Utility extensions
│   └── CryptoExchangeBase.csproj
├── BinanceSpot/                          # First implementation
│   ├── BinanceSpotMessageAdapter.cs     # Extends CryptoExchangeAdapterBase
│   ├── BinanceSpotMessageAdapter_Settings.cs
│   ├── BinanceSpotMessageAdapter_MarketData.cs
│   ├── BinanceSpotMessageAdapter_Transaction.cs
│   └── BinanceSpot.csproj
├── BinanceFutures/                       # Second implementation
│   ├── BinanceFuturesMessageAdapter.cs  # Extends CryptoExchangeAdapterBase
│   ├── [similar structure to Spot]
│   └── BinanceFutures.csproj
└── [Future exchanges: Bybit/, KuCoin/, OKX/, etc.]

Tests/
├── CryptoExchangeBase.Tests/            # Framework tests
├── Binance.Integration.Tests/           # Integration tests
└── Common/                              # Test utilities
```

**Structure Decision**: Cryptocurrency exchange framework with extensible base class pattern

## Phase 0: Outline & Research
1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:
   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude`
     **IMPORTANT**: Execute it exactly as specified above. Do not add or remove any arguments.
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `.specify/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- CryptoExchangeBase framework → foundation tasks [P]
- BinanceSpot/BinanceFutures adapters → implementation tasks extending base
- Message conversion layer → transformation tasks reusable across exchanges
- CryptoExchange.Net integration → wrapper implementation tasks

**Ordering Strategy**:
1. **Foundation**: CryptoExchangeAdapterBase and shared message converters
2. **Dependencies**: NuGet package integration (CryptoExchange.Net, Binance.Net)
3. **Core Framework**: Abstract adapter implementation with common patterns
4. **Binance Implementation**: Spot and Futures adapters extending base
5. **Integration**: End-to-end testing with real Binance testnet
6. **Documentation**: Framework usage guide and samples

**Dependency Mapping**:
- CryptoExchangeBase → Foundation for all crypto exchange connectors
- Binance.Net → CryptoExchange.Net → CryptoExchangeAdapterBase → BinanceSpot/Futures
- MessageConverters → Reusable across all future exchanges
- Common authentication/error handling → Shared patterns via base class

**Estimated Output**: 18-22 numbered, ordered tasks in tasks.md

**Key Implementation Areas**:
1. CryptoExchangeAdapterBase abstract class design
2. NuGet package dependencies and configuration
3. Message conversion framework (CryptoExchange.Net ↔ StockSharp)
4. Binance-specific adapter implementations
5. Authentication and connection management
6. Error handling and rate limiting via CryptoExchange.Net
7. Testing framework for multiple exchanges
8. Documentation and extensibility guides

**Future Extensibility**:
- Each new exchange: 2-3 days implementation (extend CryptoExchangeAdapterBase)
- Common patterns solved once, reused across all exchanges
- CryptoExchange.Net handles API-specific complexity

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |


## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS (no violations after design)
- [x] All NEEDS CLARIFICATION resolved (research.md complete)
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
