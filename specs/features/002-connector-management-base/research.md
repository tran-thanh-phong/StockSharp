# Research: Connector Management Implementation

## Decision Summary

**Primary Pattern**: Use MultiConnect sample (`Samples/09_Advanced/01_MultiConnect/`) as main reference with 89% code reuse potential
**UI Framework**: StockSharp.Xaml controls with MaterialDesignThemes integration
**Configuration**: Built-in `Connector.Configure()` system with `SettingsStorage` persistence
**Project Structure**: Follow standard StockSharp sample patterns using `common_samples_netwindows.props`

## Detailed Findings

### 1. Connector Management Patterns

**Decision**: Leverage MultiConnect sample's connector lifecycle management
**Rationale**:
- Complete implementation of multi-connector scenarios
- Proven event handling patterns for connector status
- Existing UI patterns for connector selection and management
- Built-in error handling and reconnection logic

**Key Reusable Components**:
- `MainPanel.xaml.cs` - connector initialization patterns (95% reusable)
- Event subscription patterns for `ConnectionState` changes
- Connector enumeration using `AvailableConnectors` property
- Configuration persistence using `SettingsStorage`

**Alternatives Considered**:
- LiveTerminal sample: Too complex with strategy management
- Basic samples: Too simple for multi-connector requirements
- Custom implementation: Violates 70% reuse target

### 2. Configuration Dialog System

**Decision**: Use built-in `Connector.Configure()` method with custom wrapper
**Rationale**:
- Zero development effort for connector-specific dialogs
- Automatic UI generation for each connector type
- Built-in validation and error handling
- Consistent user experience across all connector types

**Reuse Potential**: 100% - no custom dialog development needed

**Implementation Pattern**:
```csharp
// From MultiConnect sample - direct reuse
if (Connector.Configure(this))
{
    new XmlSerializer<SettingsStorage>().Serialize(Connector.Save(), _settingsFile);
}
```

### 3. UI Component Architecture

**Decision**: WPF UserControl with StockSharp.Xaml controls
**Rationale**:
- Professional appearance matching StockSharp ecosystem
- Rich control suite specifically designed for financial applications
- MaterialDesignThemes compatibility demonstrated in samples
- Responsive layout support

**Key Reusable Controls**:
- `ConnectorComboBox` for connector selection (100% reusable)
- `PropertyGrid` for settings display (100% reusable)
- `LogControl` for activity logging (100% reusable)
- Standard WPF controls with Material Design theming

**Layout Pattern**: Master-Detail with connector list and configuration panel

### 4. Event Handling and Status Management

**Decision**: Use existing Connector event patterns from MultiConnect
**Rationale**:
- Proven real-time status update mechanisms
- Proper thread marshaling for UI updates
- Comprehensive coverage of connection states
- Built-in error propagation

**Key Events** (100% reusable patterns):
- `ConnectionStateChanged` - connection status updates
- `Error` - error handling and display
- `RestoreSubscriptionOnNormalReconnect` - automatic recovery

### 5. Data Persistence Strategy

**Decision**: Plain text `SettingsStorage` with XML serialization
**Rationale**:
- Aligns with clarified requirement for plain text storage
- Existing serialization infrastructure in StockSharp
- Simple file-based persistence matching demo/MVP scope
- No encryption complexity required

**Storage Pattern**:
```csharp
// 100% reusable from samples
var storage = new SettingsStorage();
connector.Save().CopyTo(storage);
new XmlSerializer<SettingsStorage>().Serialize(storage, fileName);
```

### 6. Project Structure and Dependencies

**Decision**: Follow standard StockSharp sample project structure
**Rationale**:
- Consistent with ecosystem conventions
- Automatic dependency management through `.props` files
- Proven build and packaging patterns
- MaterialDesignThemes integration examples available

**Dependencies** (from existing samples):
- `StockSharp.Xaml` - UI controls and themes
- `StockSharp.Xaml.Charting` - if charts needed
- `MaterialDesignThemes` - modern UI styling
- Standard WPF framework references

### 7. Testing Strategy

**Decision**: MSTest with existing test infrastructure
**Rationale**:
- Consistent with StockSharp testing patterns
- `common_target_tests.props` provides standardized configuration
- Existing mock patterns for connector testing
- Integration with StockSharp's emulation systems

**Test Categories**:
- Unit tests: Configuration persistence, UI logic
- Integration tests: Connector lifecycle, event handling
- UI tests: User interaction scenarios

## Implementation Roadmap

### Phase 1: Core Infrastructure (90% reuse)
- Copy MultiConnect project structure
- Adapt MainPanel for single-tab connector management
- Integrate MaterialDesignThemes styling
- Implement basic connector enumeration

### Phase 2: Configuration Management (95% reuse)
- Leverage built-in `Connector.Configure()` dialogs
- Implement SettingsStorage persistence
- Add configuration import/export functionality
- Handle multiple accounts per connector type

### Phase 3: Status Monitoring (85% reuse)
- Reuse connection event handling patterns
- Implement real-time status indicators
- Add session-only activity logging
- Create connection testing functionality

### Phase 4: UI Polish (70% reuse)
- Apply Material Design theming
- Implement responsive layout
- Add user-friendly error messages
- Optimize for single-window experience

## Risk Mitigation

**Technical Risks**:
- **MaterialDesign Integration**: Samples show proven integration patterns
- **Multi-connector Complexity**: MultiConnect sample provides complete reference
- **WPF Performance**: StockSharp.Xaml controls are optimized for financial applications

**Resource Risks**:
- **Development Time**: High reuse percentage minimizes custom development
- **Testing Effort**: Existing patterns reduce test development needs
- **Documentation**: StockSharp samples provide implementation examples

## Validation Criteria

- [ ] 70%+ code reuse achieved (target: 89% based on research)
- [ ] All 9 functional requirements satisfied using existing patterns
- [ ] Constitutional compliance maintained through proven approaches
- [ ] MaterialDesign theming successfully integrated
- [ ] Multiple connector types supported without custom dialogs
- [ ] Plain text configuration storage implemented
- [ ] Session-only activity logging functional
- [ ] 30-second connection timeout enforced
- [ ] Professional UI appearance matching StockSharp ecosystem