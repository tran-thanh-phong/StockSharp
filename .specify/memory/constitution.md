<!--
Sync Impact Report:
- Version change: new → v1.0.0
- Modified principles: N/A (new constitution)
- Added sections: All core principles, Testing Standards, Performance Requirements, Development Workflow, Governance
- Removed sections: N/A
- Templates requiring updates: ✅ Updated (plan-template.md, spec-template.md, tasks-template.md examined and aligned)
- Follow-up TODOs: None
-->

# StockSharp Constitution

## Core Principles

### I. KISS - Keep It Simple, Stupid (NON-NEGOTIABLE)
Simplicity MUST be the first priority in all design decisions. Every component, class, and method MUST have a single, clear purpose. Complex solutions MUST be justified by demonstrating that simpler alternatives were evaluated and found inadequate. When extending the platform, follow existing patterns rather than introducing new complexity. Connectors MUST inherit from MessageAdapter and implement only required message types. Strategies MUST inherit from Strategy base class and use event-driven patterns.

**Rationale**: StockSharp's message-based architecture succeeds because each component has clear responsibilities. Complexity leads to bugs in financial systems where reliability is critical.

### II. SOLID Principles
Code MUST adhere to SOLID principles to maintain the platform's extensibility and reliability:
- **Single Responsibility**: Each class serves one purpose (Connector handles communication, Strategy handles logic)
- **Open/Closed**: Extensions through inheritance (MessageAdapter, Strategy base classes) without modifying core
- **Liskov Substitution**: All connectors interchangeable through IMessageAdapter interface
- **Interface Segregation**: Specific interfaces for market data, trading, historical data access
- **Dependency Inversion**: High-level modules depend on abstractions (IConnector, IMarketDataProvider)

**Rationale**: SOLID principles enable the platform's 60+ connector ecosystem and ensure new extensions don't break existing functionality.

### III. DRY - Don't Repeat Yourself
Code duplication MUST be eliminated through proper abstraction. Common functionality MUST be centralized in base classes, utility methods, or shared libraries. When building connectors, reuse existing patterns from similar implementations. Sample applications MUST demonstrate best practices without duplicating boilerplate code. Configuration and dependency management MUST be centralized (common_versions.props, common_target_tests.props).

**Rationale**: Financial platforms require consistent behavior across components. Duplication leads to maintenance burden and inconsistent bug fixes.

### IV. Message-Based Architecture Compliance
All data flow MUST use the established message-based communication system. New features MUST communicate through typed messages (ExecutionMessage, QuoteChangeMessage, SecurityMessage, etc.). Direct object manipulation MUST be avoided in favor of message passing. Extensions MUST handle message types appropriately and maintain backward compatibility.

**Rationale**: Message-based architecture ensures loose coupling, testability, and enables distributed processing capabilities essential for high-frequency trading.

### V. Extension Pattern Adherence
New development MUST follow established extension patterns rather than modifying core components. Connectors MUST extend MessageAdapter with appropriate message handling. Strategies MUST extend Strategy base class using MarketRuleHelper for event-driven logic. Samples MUST demonstrate single concepts clearly without mixing concerns. Follow naming conventions and project structure from existing implementations.

**Rationale**: Extension patterns preserve platform stability while enabling infinite customization. Core modifications risk breaking the entire ecosystem.

## Testing Standards

### Comprehensive Test Coverage
All code MUST have corresponding tests using MSTest framework targeting net8.0 and net9.0. Test projects MUST use common_target_tests.props for consistent configuration. Unit tests MUST validate individual components in isolation. Integration tests MUST verify message flow and connector behavior. Contract tests MUST ensure API compatibility across versions.

### Test-Driven Development
Tests MUST be written before implementation code. Red-Green-Refactor cycle MUST be strictly followed. Tests MUST fail initially, then pass after correct implementation. Test failures MUST block deployment. No code changes without corresponding test coverage.

### Testing Categories
- **Unit Tests**: Individual class behavior, mathematical calculations, business logic validation
- **Integration Tests**: Market emulation, strategy execution, data storage systems
- **Contract Tests**: Message format validation, API compatibility, connector interfaces
- **Performance Tests**: Latency requirements, throughput validation, memory usage limits

## Performance Requirements

### Latency Standards
Market data processing MUST complete within 1ms for critical path operations. Order execution latency MUST not exceed 5ms from signal to submission. Strategy calculations MUST complete within allocated time slots to avoid missing market opportunities. Memory allocation in hot paths MUST be minimized to reduce garbage collection pressure.

### Throughput Requirements
System MUST handle minimum 10,000 market data updates per second per connector. Historical data processing MUST achieve minimum 1GB/minute throughput. Strategy backtesting MUST process minimum 1 million bars per minute. Concurrent connector support MUST scale to 50+ simultaneous connections.

### Resource Constraints
Memory usage MUST remain under 2GB for standard configurations. CPU usage MUST not exceed 80% average under normal load. Disk I/O MUST be optimized for SSD storage patterns. Network connections MUST implement proper connection pooling and retry logic.

## Development Workflow

### Code Quality Gates
All code MUST pass static analysis before merge. Language version MUST remain at C# 12.0 for consistency. Target frameworks MUST support net8.0 and net9.0. Package versions MUST be managed through common_versions.props. Code style MUST follow established patterns from existing codebase.

### Review Process
All changes MUST be reviewed by maintainers familiar with affected components. Financial calculations MUST receive additional mathematical verification. Security-related changes MUST undergo security review. Performance-critical changes MUST include benchmark comparisons.

### Documentation Requirements
Public APIs MUST have complete XML documentation. Sample applications MUST include clear README files explaining usage. Complex algorithms MUST include implementation rationale. Breaking changes MUST be documented with migration guides.

## Governance

Constitution violations MUST be justified with technical necessity and simpler alternatives documented. All development decisions MUST prioritize platform stability over individual convenience. Changes affecting message formats or base classes require architectural review. Performance regressions are not acceptable without compelling functionality gains.

Amendment process requires consensus from core maintainers and impact analysis on existing extensions. Complexity increases must demonstrate proportional value addition. Use CLAUDE.md for runtime development guidance and established patterns.

**Version**: 1.0.0 | **Ratified**: 2025-09-24 | **Last Amended**: 2025-09-24