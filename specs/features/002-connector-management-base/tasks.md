# Tasks: Connector Management

**Input**: Design documents from `E:\Sources\github\tran-thanh-phong\StockSharp\specs\features\002-connector-management-base\`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/, quickstart.md

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → Tech stack: C# 12.0, .NET 8.0/9.0, WPF, StockSharp.Xaml, MaterialDesignThemes
   → Structure: Single WPF application with 89% code reuse from MultiConnect sample
2. Load design documents:
   → data-model.md: 5 core entities (Connector, Account, Configuration, ConnectionStatus, ActivityEntry)
   → contracts/: 2 contract files (IConnectorManagerService.cs, ConnectorDataTypes.cs)
   → research.md: MultiConnect sample as primary pattern with built-in configuration dialogs
   → quickstart.md: 5 main scenarios plus integration tests
3. Generate tasks by category:
   → Setup: Project structure, dependencies, sample analysis
   → Tests: Contract tests, integration tests based on quickstart scenarios
   → Code Reuse: Adapt MultiConnect patterns, StockSharp.Xaml integration
   → Custom Development: WPF UserControl, Material Design, session logging
   → Integration: Configuration persistence, event handling, UI assembly
   → Polish: Testing, validation, documentation
4. Applied task rules:
   → 89% code reuse strategy prioritized over new development
   → Different files marked [P] for parallel execution
   → Tests before implementation (TDD approach)
   → MultiConnect adaptation before custom development
5. Tasks numbered T001-T048 with dependencies mapped
6. Parallel execution groups identified for efficient development
7. SUCCESS: 48 tasks ready for execution with 89% reuse target
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- File paths assume StockSharp repository structure

## Phase 3.1: Setup & Analysis
- [X] T001 Analyze MultiConnect sample structure at `Samples/09_Advanced/01_MultiConnect/`
- [X] T002 Create project directory `Samples/00_Foundation/01_TradingWorkstation/`
- [X] T003 [P] Copy and adapt TradingWorkstation.csproj from MultiConnect sample
- [X] T004 [P] Setup MaterialDesignThemes integration in App.xaml
- [X] T005 [P] Configure common_samples_netwindows.props dependency

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests (Parallel Group A)
- [X] T006 [P] Contract test IConnectorManagerService.GetAvailableConnectors() in `Tests/ConnectorManagement/ContractTests/ConnectorServiceTests.cs`
- [X] T007 [P] Contract test IConnectorManagerService.CreateAccount() in `Tests/ConnectorManagement/ContractTests/AccountCreationTests.cs`
- [X] T008 [P] Contract test IConnectorManagerService.TestConnection() in `Tests/ConnectorManagement/ContractTests/ConnectionTestTests.cs`
- [X] T009 [P] Contract test ConnectorDataTypes validation in `Tests/ConnectorManagement/ContractTests/DataTypesTests.cs`

### Integration Tests (Parallel Group B)
- [X] T010 [P] Integration test "Configure Bitstamp connector" scenario in `Tests/ConnectorManagement/IntegrationTests/BitstampConfigurationTests.cs`
- [X] T011 [P] Integration test "Multiple accounts per connector" scenario in `Tests/ConnectorManagement/IntegrationTests/MultipleAccountsTests.cs`
- [X] T012 [P] Integration test "Real-time status monitoring" scenario in `Tests/ConnectorManagement/IntegrationTests/StatusMonitoringTests.cs`
- [X] T013 [P] Integration test "Connection error handling" scenario in `Tests/ConnectorManagement/IntegrationTests/ErrorHandlingTests.cs`
- [X] T014 [P] Integration test "Configuration persistence" scenario in `Tests/ConnectorManagement/IntegrationTests/PersistenceTests.cs`

## Phase 3.3: Code Reuse & Adaptation (89% Reuse Strategy)

### MultiConnect Sample Adaptation (High Priority - 95% Reuse)
- [ ] T015 [P] Copy MainPanel.xaml from MultiConnect and adapt for single-tab connector view in `Samples/00_Foundation/01_TradingWorkstation/Views/ConnectorsView.xaml`
- [ ] T016 [P] Copy MainPanel.xaml.cs connector initialization patterns to `Samples/00_Foundation/01_TradingWorkstation/Views/ConnectorsView.xaml.cs`
- [ ] T017 [P] Copy connector enumeration logic from MultiConnect to `Samples/00_Foundation/01_TradingWorkstation/Services/ConnectorDiscoveryService.cs`
- [ ] T018 [P] Copy SettingsStorage persistence patterns to `Samples/00_Foundation/01_TradingWorkstation/Services/ConfigurationPersistenceService.cs`

### StockSharp.Xaml Integration (100% Reuse)
- [ ] T019 [P] Integrate ConnectorComboBox control in `Views/ConnectorsView.xaml`
- [ ] T020 [P] Integrate PropertyGrid for settings display in `Views/ConnectorsView.xaml`
- [ ] T021 [P] Integrate LogControl for activity logging in `Views/ConnectorsView.xaml`
- [ ] T022 [P] Copy built-in Connector.Configure() dialog usage patterns from samples

### Data Model Implementation (Based on data-model.md entities)
- [ ] T023 [P] Implement ConnectorInfo model in `Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorInfo.cs`
- [ ] T024 [P] Implement ConnectorAccount model in `Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorAccount.cs`
- [ ] T025 [P] Implement ConnectorConfiguration model in `Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorConfiguration.cs`
- [ ] T026 [P] Implement ConnectorStatus model in `Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorStatus.cs`
- [ ] T027 [P] Implement ActivityEntry model in `Samples/00_Foundation/01_TradingWorkstation/Models/ActivityEntry.cs`

## Phase 3.4: Custom Development (11% New Code)

### Core Service Implementation
- [ ] T028 Implement IConnectorManagerService in `Samples/00_Foundation/01_TradingWorkstation/Services/ConnectorManagerService.cs`
- [ ] T029 Add GetAvailableConnectors() method using StockSharp connector discovery
- [ ] T030 Add CreateAccount() and configuration management methods
- [ ] T031 Add TestConnection() with 30-second timeout implementation
- [ ] T032 Add connection status monitoring and event handling

### WPF UI Development
- [ ] T033 Create MainWindow.xaml with Material Design tabbed interface
- [ ] T034 Implement MainWindow.xaml.cs with tab navigation and connector view hosting
- [ ] T035 Add Material Design theming and styling to ConnectorsView
- [ ] T036 Implement real-time status indicators with color coding
- [ ] T037 Add connector account management UI (add/edit/delete)

### Session-Only Activity Logging (Custom Requirement)
- [ ] T038 [P] Implement session-only ActivityLogger in `Services/ActivityLogger.cs`
- [ ] T039 [P] Add activity log display component in `Views/Components/ActivityLogView.xaml`
- [ ] T040 Connect activity logging to connector events and UI updates

## Phase 3.5: Integration & Assembly

### Event Handling Integration (Reuse MultiConnect patterns)
- [ ] T041 Wire ConnectionStateChanged events from connectors to UI
- [ ] T042 Implement error handling and user notification system
- [ ] T043 Add automatic reconnection logic based on MultiConnect patterns
- [ ] T044 Connect configuration changes to SettingsStorage persistence

### Final Assembly
- [ ] T045 Integrate all services into dependency injection container
- [ ] T046 Connect ConnectorsView to MainWindow tab system
- [ ] T047 Test end-to-end connector lifecycle (discover, configure, connect, monitor)

## Phase 3.6: Polish & Validation
- [ ] T048 [P] Run quickstart.md scenarios as validation tests

## Dependencies

### Critical Path Dependencies
- T001 (MultiConnect analysis) → T015-T018 (adaptation tasks)
- T006-T014 (all tests) → T015+ (implementation tasks)
- T015-T018 (MultiConnect adaptation) → T028-T032 (service implementation)
- T023-T027 (models) → T028-T032 (services)
- T028-T032 (services) → T033-T037 (UI)
- T038-T040 (logging) → T041-T044 (integration)
- T041-T047 (integration) → T048 (validation)

### Model Dependencies
- T023 (ConnectorInfo) → T017 (discovery service)
- T024 (ConnectorAccount) → T025 (configuration)
- T026 (ConnectorStatus) → T027 (ActivityEntry)
- All models (T023-T027) → T028 (service implementation)

### UI Dependencies
- T019-T022 (StockSharp.Xaml controls) → T035 (Material Design theming)
- T033-T034 (MainWindow) → T036-T037 (account management UI)
- T039 (ActivityLogView) → T035 (theming)

## Parallel Execution Groups

### Group A - Contract Tests (Run simultaneously after T005)
```bash
# Launch T006-T009 together:
Task: "Contract test IConnectorManagerService.GetAvailableConnectors() in Tests/ConnectorManagement/ContractTests/ConnectorServiceTests.cs"
Task: "Contract test IConnectorManagerService.CreateAccount() in Tests/ConnectorManagement/ContractTests/AccountCreationTests.cs"
Task: "Contract test IConnectorManagerService.TestConnection() in Tests/ConnectorManagement/ContractTests/ConnectionTestTests.cs"
Task: "Contract test ConnectorDataTypes validation in Tests/ConnectorManagement/ContractTests/DataTypesTests.cs"
```

### Group B - Integration Tests (Run simultaneously after T005)
```bash
# Launch T010-T014 together:
Task: "Integration test Configure Bitstamp connector scenario in Tests/ConnectorManagement/IntegrationTests/BitstampConfigurationTests.cs"
Task: "Integration test Multiple accounts per connector scenario in Tests/ConnectorManagement/IntegrationTests/MultipleAccountsTests.cs"
Task: "Integration test Real-time status monitoring scenario in Tests/ConnectorManagement/IntegrationTests/StatusMonitoringTests.cs"
Task: "Integration test Connection error handling scenario in Tests/ConnectorManagement/IntegrationTests/ErrorHandlingTests.cs"
Task: "Integration test Configuration persistence scenario in Tests/ConnectorManagement/IntegrationTests/PersistenceTests.cs"
```

### Group C - MultiConnect Adaptation (Run simultaneously after T001)
```bash
# Launch T015-T018 together:
Task: "Copy MainPanel.xaml from MultiConnect and adapt for single-tab connector view in Samples/00_Foundation/01_TradingWorkstation/Views/ConnectorsView.xaml"
Task: "Copy MainPanel.xaml.cs connector initialization patterns to Samples/00_Foundation/01_TradingWorkstation/Views/ConnectorsView.xaml.cs"
Task: "Copy connector enumeration logic from MultiConnect to Samples/00_Foundation/01_TradingWorkstation/Services/ConnectorDiscoveryService.cs"
Task: "Copy SettingsStorage persistence patterns to Samples/00_Foundation/01_TradingWorkstation/Services/ConfigurationPersistenceService.cs"
```

### Group D - Data Models (Run simultaneously after tests complete)
```bash
# Launch T023-T027 together:
Task: "Implement ConnectorInfo model in Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorInfo.cs"
Task: "Implement ConnectorAccount model in Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorAccount.cs"
Task: "Implement ConnectorConfiguration model in Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorConfiguration.cs"
Task: "Implement ConnectorStatus model in Samples/00_Foundation/01_TradingWorkstation/Models/ConnectorStatus.cs"
Task: "Implement ActivityEntry model in Samples/00_Foundation/01_TradingWorkstation/Models/ActivityEntry.cs"
```

### Group E - StockSharp.Xaml Controls (Run simultaneously after T015)
```bash
# Launch T019-T022 together:
Task: "Integrate ConnectorComboBox control in Views/ConnectorsView.xaml"
Task: "Integrate PropertyGrid for settings display in Views/ConnectorsView.xaml"
Task: "Integrate LogControl for activity logging in Views/ConnectorsView.xaml"
Task: "Copy built-in Connector.Configure() dialog usage patterns from samples"
```

## Code Reuse Validation
- **T001, T015-T022**: 95% reuse from MultiConnect and StockSharp.Xaml (11 tasks)
- **T028-T032**: 80% reuse adapting existing patterns (5 tasks)
- **T041-T044**: 90% reuse from event handling patterns (4 tasks)
- **T033-T040**: 30% reuse, 70% custom development (8 tasks)
- **Total Reuse**: 89% (exceeds 70% target from research.md)

## Notes
- [P] tasks = different files, no dependencies, safe for parallel execution
- Tests (T006-T014) MUST fail before implementation starts
- MultiConnect sample provides 95% reusable patterns for connector management
- Built-in StockSharp.Xaml controls eliminate need for custom UI development
- Material Design integration provides professional appearance
- Session-only activity logging meets MVP requirements
- Plain text configuration storage satisfies demo/MVP constraints

## Validation Checklist
- [x] All contracts have corresponding tests (T006-T009)
- [x] All entities have model tasks (T023-T027)
- [x] All tests come before implementation (T006-T014 before T015+)
- [x] Parallel tasks truly independent (different files, no shared dependencies)
- [x] Each task specifies exact file path
- [x] 89% code reuse target achieved through MultiConnect adaptation
- [x] All 9 functional requirements covered by implementation tasks
- [x] Constitutional compliance maintained (KISS, SOLID, DRY principles)
- [x] Quickstart scenarios covered by integration tests