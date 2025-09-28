
# Implementation Plan: Connector Management

**Branch**: `features/002-connector-management-base` | **Date**: 2025-09-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `E:\Sources\github\tran-thanh-phong\StockSharp\specs\features\002-connector-management-base\spec.md`

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
Implement WPF Connector Management UI for StockSharp Trading Workstation that allows traders to configure and manage connections to 60+ trading platforms (Bitstamp, Interactive Brokers, MT4/MT5, etc.). The implementation will maximize code reuse from existing samples (70%+ reuse target) as specified in the PRD, particularly leveraging LiveTerminal sample for connector management patterns and UI components.

## Technical Context
**Language/Version**: C# 12.0, .NET 8.0/9.0  
**Primary Dependencies**: StockSharp.Xaml, StockSharp.Xaml.Charting, WPF, MaterialDesignThemes  
**Storage**: Plain text configuration files (demo/MVP approach)  
**Testing**: MSTest framework, common_target_tests.props  
**Target Platform**: Windows desktop (net6.0-windows)
**Project Type**: Single WPF application - determines source structure  
**Performance Goals**: MVP performance (30s connection timeout, session-only activity logs)  
**Constraints**: Demo/MVP constraints (plain text storage, unlimited connections, session-only retention)  
**Scale/Scope**: 60+ connector types, unlimited simultaneous connections, single-user workstation
**Code Reuse Strategy**: Implement full features with 70%+ code reuse from existing samples as specified in PRD `Docs\wpf_trading_sample_prd.md`

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**KISS Principle Compliance**:
- [x] Solution follows simplest approach that meets requirements (70% code reuse from existing samples)
- [x] Each component has single, clear purpose (ConnectorView, ConnectorManager, ConnectorConfiguration)
- [x] Existing StockSharp patterns used (Connector class, configuration dialogs from LiveTerminal)
- [x] Complex design alternatives documented and rejected (leveraging proven LiveTerminal patterns)

**SOLID Principles Compliance**:
- [x] Single Responsibility: ConnectorView (UI), ConnectorManager (business logic), Configuration (data)
- [x] Open/Closed: Extending WPF UserControl pattern, not modifying StockSharp core
- [x] Interface Segregation: Separate interfaces for connector listing vs configuration vs status
- [x] Dependency Inversion: Depend on IConnector abstraction, not concrete implementations

**DRY Principle Compliance**:
- [x] No code duplication (reuse LiveTerminal connector initialization patterns)
- [x] Common patterns abstracted (shared connector configuration utilities)
- [x] Configuration centralized using project standards (common_versions.props, common_target_tests.props)

**Architecture Compliance**:
- [x] Message-based communication used for data flow (Connector events for status updates)
- [x] Extension patterns followed instead of core modifications (UserControl extension, not core UI changes)
- [x] Backward compatibility maintained for existing integrations (no changes to StockSharp core APIs)

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
# Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure]
```

**Structure Decision**: [DEFAULT to Option 1 unless Technical Context indicates web/mobile app]

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
- Generate tasks based on 89% code reuse strategy from research findings
- Prioritize code adaptation tasks over new development
- Focus on WPF integration and Material Design theming
- Leverage MultiConnect sample as primary reference

**Code Reuse Tasks** (High Priority):
- Copy and adapt MultiConnect sample structure (95% reuse)
- Integrate StockSharp.Xaml controls for professional UI
- Implement built-in `Connector.Configure()` dialogs (100% reuse)
- Adapt SettingsStorage persistence patterns (100% reuse)
- Reuse connection event handling from existing samples

**Custom Development Tasks** (Lower Priority):
- Create WPF UserControl for connector management view
- Implement Material Design theming integration
- Build session-only activity logging system
- Add plain text configuration storage (MVP approach)
- Create connector status monitoring UI components

**Test Generation Strategy**:
- Contract tests for `IConnectorManagerService` interface
- Integration tests based on quickstart scenarios
- UI tests for Material Design components
- Configuration persistence tests
- Connection timeout and error handling tests

**Ordering Strategy**:
- TDD order: Tests before implementation
- Reuse-first: Adapt existing code before new development
- Dependency order: Data model → Services → UI → Integration
- Mark [P] for parallel execution (independent components)
- Phase validation: Ensure 70%+ code reuse target met

**Estimated Task Breakdown**:
- **Code Reuse Tasks**: 15-18 tasks (adaptation and integration)
- **Custom Development**: 8-10 tasks (new UI components)
- **Testing Tasks**: 12-15 tasks (comprehensive test coverage)
- **Integration Tasks**: 5-7 tasks (assembly and validation)
- **Total Estimated**: 40-50 numbered, ordered tasks in tasks.md

**Key Dependencies**:
1. MultiConnect sample analysis and adaptation
2. StockSharp.Xaml control integration
3. MaterialDesignThemes configuration
4. SettingsStorage implementation
5. WPF UserControl development patterns

**Success Criteria**:
- 89% code reuse achieved (target exceeded from research)
- All 9 functional requirements covered by tasks
- Constitutional compliance maintained
- Material Design theming properly integrated
- Plain text storage and session-only logging implemented

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
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
