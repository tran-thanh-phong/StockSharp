# StockSharp WPF Trading Workstation - Product Requirements Document (PRD)

**Version**: 1.0
**Date**: September 26 2025
**Status**: Planning Phase

## Executive Summary

This PRD outlines the development of a comprehensive WPF Trading Workstation that demonstrates the full capabilities of the StockSharp trading platform. The application will serve as a flagship sample showcasing live trading, backtesting, strategy management, and multi-connector support in a single, professional interface.

**Key Strategy**: Hybrid development approach leveraging existing StockSharp samples to maximize code reuse (70%) while creating a modern, unified user experience.

---

## 1. Project Overview

### 1.1 Project Location
```
📁 Samples/00_Foundation/01_TradingWorkstation/
├── 📄 TradingWorkstation.csproj
├── 📄 App.xaml / App.xaml.cs
├── 📄 MainWindow.xaml / MainWindow.xaml.cs
├── 📁 Views/
├── 📁 Models/
├── 📁 Strategies/
├── 📁 Properties/
└── 📄 README.md
```

### 1.2 Business Logic Sources
The application will reuse proven business logic from existing samples:

| Component | Source Location | Usage |
|-----------|----------------|-------|
| **Live Trading Engine** | `Samples/06_Strategies/10_LiveTerminal/` | Connector management, real-time data, order execution |
| **Backtesting Engine** | `Samples/07_Testing/01_History/` | Historical testing, performance metrics, charting |
| **Strategy Framework** | `Samples/07_Testing/01_History/SmaStrategy.cs` | Strategy base implementation |
| **Basic Connectivity** | `Samples/01_Basic/01_ConnectAndDownloadInstruments/` | Connector configuration patterns |

---

## 2. Functional Requirements

### 2.1 Core Features

#### F1: Connector Management
**Priority**: HIGH
**Source**: `Samples/06_Strategies/10_LiveTerminal/MainWindow.xaml.cs:101-172`

**Requirements**:
- [R1.1] Display list of supported connectors (60+ including Binance, IB, MT4/MT5)
- [R1.2] Support multiple accounts per connector type
- [R1.3] Connection testing with visual status indicators
- [R1.4] Encrypted credential storage
- [R1.5] Connection configuration UI via `Connector.Configure()`

**Code Reuse Map**:
```csharp
// From LiveTerminal/MainWindow.xaml.cs
private void InitConnector() { ... }          // Lines 101-172
private void SettingsClick() { ... }          // Lines 195-199
private void ConnectClick() { ... }           // Lines 201-211
private void ChangeConnectStatus() { ... }    // Lines 213-217
```

#### F2: Strategy Management
**Priority**: HIGH
**Source**: `Samples/07_Testing/01_History/SmaStrategy.cs`

**Requirements**:
- [R2.1] Create/edit/delete strategies
- [R2.2] Parameter configuration with validation
- [R2.3] Strategy templates (SMA, Bollinger, MACD, etc.)
- [R2.4] Enable/disable individual strategies
- [R2.5] Strategy performance monitoring

**Code Reuse Map**:
```csharp
// From SmaStrategy.cs - Complete strategy implementation
class SmaStrategy : Strategy { ... }          // Lines 15-202

// Strategy parameter system
private readonly StrategyParam<int> _longSma  // Lines 39-45
private readonly StrategyParam<int> _shortSma // Lines 47-53
protected override void OnStarted() { ... }  // Lines 102-165
```

#### F3: Live Trading
**Priority**: HIGH
**Source**: `Samples/06_Strategies/10_LiveTerminal/`

**Requirements**:
- [R3.1] Strategy + Symbol + Account selection interface
- [R3.2] Risk control settings (Stop Loss, Take Profit, Position Size)
- [R3.3] Real-time P&L monitoring
- [R3.4] Order and position tracking
- [R3.5] Trade history with filtering
- [R3.6] Start/Stop controls per strategy instance

**Code Reuse Map**:
```csharp
// From LiveTerminal/MainWindow.xaml.cs
Connector.OrderReceived += (s, order) => { ... }     // Lines 132-136
Connector.OwnTradeReceived += (s, t) => { ... }      // Lines 141
Connector.PositionReceived += (sub, p) => { ... }    // Lines 143

// Window components to integrate
private readonly SecuritiesWindow _securitiesWindow;  // Line 31
private readonly OrdersWindow _ordersWindow;          // Line 32
private readonly PortfoliosWindow _portfoliosWindow;  // Line 33
private readonly MyTradesWindow _myTradesWindow;      // Line 34
private readonly StrategiesWindow _strategiesWindow;  // Line 35
```

#### F4: Backtesting
**Priority**: HIGH
**Source**: `Samples/07_Testing/01_History/MainWindow.xaml.cs`

**Requirements**:
- [R4.1] Historical data source selection
- [R4.2] Date range and timeframe configuration
- [R4.3] Multiple data types (Ticks, Candles, OrderBook, Level1)
- [R4.4] Execution speed controls (1x, 5x, 10x, Instant)
- [R4.5] Performance metrics (Sharpe, Drawdown, Win Rate, P&L)
- [R4.6] Visual chart output with trade overlays
- [R4.7] Results export (CSV, JSON)

**Code Reuse Map**:
```csharp
// From History/MainWindow.xaml.cs - Complete backtesting engine
private void StartBtnClick() { ... }                    // Lines 236-594
var connector = new HistoryEmulationConnector() { ... } // Lines 331-372
var strategy = new SmaStrategy { ... }                  // Lines 379-392

// Performance tracking
strategy.PnLReceived2 += (s, pf, t, r, u, c) => { ... } // Lines 492-504
strategy.PositionReceived += (s, p) => { ... }          // Lines 508-517

// Progress and state management
connector.ProgressChanged += steps => { ... }           // Line 519
connector.StateChanged2 += state => { ... }             // Lines 521-560
```

#### F5: Settings & Configuration
**Priority**: MEDIUM
**Source**: Custom implementation

**Requirements**:
- [R5.1] Global application settings
- [R5.2] Notification/alert configuration
- [R5.3] Logging level and destination settings
- [R5.4] Import/export configurations
- [R5.5] Theme selection (Light/Dark)

---

## 3. User Interface Design

### 3.1 Main Window Layout
**Layout Type**: Tabbed Interface with Ribbon Menu

```
┌─────────────────────────────────────────────────────────┐
│ [File] [Edit] [View] [Tools] [Help]               [🔌●] │ ← Ribbon Menu
├─────────────────────────────────────────────────────────┤
│ [Connectors] [Strategies] [Live Trading] [Backtesting] [Settings] │ ← Tabs
├─────────────────────────────────────────────────────────┤
│                                                         │
│                   Tab Content Area                      │
│                                                         │
│                                                         │
├─────────────────────────────────────────────────────────┤
│ Status: Connected | Strategies: 2/3 Active | 📊 | 🔔 │ ← Status Bar
└─────────────────────────────────────────────────────────┘
```

### 3.2 Tab Specifications

#### Tab 1: Connectors
**Layout**: Master-Detail with Connection Status

```
┌─────────────────┬───────────────────────────────────────┐
│ Available       │ Configuration & Status                │
│ Connectors      │                                       │
│ ─────────────── │ Connector: Binance                    │
│ ☐ Binance       │ Status: ● Connected                   │
│ ☐ Interactive.. │ Account: user@email.com               │
│ ☐ MT4           │ Balance: $10,000.00                   │
│ ☐ MT5           │ [Test Connection] [Configure]         │
│ ☐ BitFinex      │                                       │
│ ... (60+)       │ Recent Activity:                      │
│                 │ 14:32:15 - Connection established     │
│ [Add Account]   │ 14:30:22 - Authentication successful │
│                 │                                       │
└─────────────────┴───────────────────────────────────────┘
```

**Code Integration**:
- Reuse connector initialization from `LiveTerminal/MainWindow.xaml.cs:50-82`
- Adapt configuration dialog from `LiveTerminal/MainWindow.xaml.cs:195-199`

#### Tab 2: Strategies
**Layout**: Strategy Library with Parameter Editor

```
┌─────────────────┬───────────────────────────────────────┐
│ Strategy        │ Strategy Configuration                │
│ Templates       │                                       │
│ ─────────────── │ Name: SMA Crossover #1                │
│ ► SMA Crossover │ Template: Simple Moving Average       │
│ ► Bollinger     │                                       │
│ ► MACD Signal   │ Parameters:                           │
│ ► RSI Overbought│ ├─ Long SMA: [20] periods            │
│ ► Breakout      │ ├─ Short SMA: [5] periods             │
│                 │ ├─ Volume: [100] shares               │
│ [New Strategy]  │ ├─ Stop Loss: [2%]                   │
│ [Import]        │ └─ Take Profit: [5%]                 │
│                 │                                       │
│                 │ [Save] [Test Parameters] [Delete]     │
└─────────────────┴───────────────────────────────────────┘
```

**Code Integration**:
- Adapt strategy base from `SmaStrategy.cs:15-202`
- Use parameter system from `SmaStrategy.cs:21-29, 39-93`

#### Tab 3: Live Trading
**Layout**: Multi-panel Trading Dashboard

```
┌─────────────────────────────────────────────────────────┐
│ Active Strategies               │ Market Data & Charts  │
│ ─────────────────────────────   │                       │
│ ● SMA Cross #1  EURUSD  $1,230 │   [Chart Area]        │
│ ● Bollinger    GBPUSD    $-45  │                       │
│ ○ RSI Signal   USDJPY   $0     │                       │
│ [▶] [⏸] [⏹] [⚙]               │                       │
├─────────────────────────────────┼───────────────────────┤
│ Open Positions                  │ Recent Trades         │
│ EURUSD  Long  100  +$45.20     │ 14:25 SELL EURUSD -45 │
│ GBPUSD  Short 50   -$12.30     │ 14:20 BUY EURUSD +120 │
│                                 │ 14:15 BUY GBPUSD +67  │
└─────────────────────────────────┴───────────────────────┘
```

**Code Integration**:
- Reuse window components from `LiveTerminal/MainWindow.xaml.cs:31-35`
- Adapt real-time data handling from `LiveTerminal/MainWindow.xaml.cs:130-143`

#### Tab 4: Backtesting
**Layout**: Setup Panel + Results Dashboard

```
┌─────────────────────────────────────────────────────────┐
│ Backtest Configuration                                  │
│ Strategy: [SMA Crossover ▼] Symbol: [EURUSD ▼]        │
│ From: [2024-01-01] To: [2024-12-01] TF: [1H ▼]        │
│ Data: [☑ Ticks] [☑ Candles] [☐ OrderBook]             │
│ [▶ Start Backtest] Speed: [10x ▼] [⏸ Pause] [⏹ Stop] │
├─────────────────────────────────────────────────────────┤
│ Results & Performance                                   │
│ ┌─ Equity Curve ────┬─ Performance Stats ─────────────┐ │
│ │  [Chart]          │ Net P&L: $2,450.30             │ │
│ │                   │ Win Rate: 65.4%                │ │
│ │                   │ Sharpe Ratio: 1.23             │ │
│ │                   │ Max Drawdown: -8.2%            │ │
│ └───────────────────┴─────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Code Integration**:
- Complete backtesting engine from `History/MainWindow.xaml.cs:236-594`
- Chart integration from `History/MainWindow.xaml.cs:487-517`
- Performance metrics from existing `StatisticParameterGrid` usage

#### Tab 5: Settings
**Layout**: Categorized Settings Panel

```
┌─────────────────┬───────────────────────────────────────┐
│ Categories      │ Settings                              │
│ ─────────────── │                                       │
│ ► General       │ Application Settings                  │
│ ► Connections   │ ├─ Theme: [Light ▼]                  │
│ ► Trading       │ ├─ Auto-save: [☑] Every 5 minutes    │
│ ► Notifications │ ├─ Data Path: [C:\StockSharp\Data]   │
│ ► Logging       │ └─ Language: [English ▼]             │
│ ► Advanced      │                                       │
│                 │ Risk Management                       │
│                 │ ├─ Daily Loss Limit: [$500]          │
│                 │ ├─ Max Positions: [5]                │
│                 │ └─ Position Size Limit: [10%]        │
│ [Export Config] │                                       │
│ [Import Config] │ [Apply] [Reset to Defaults]          │
└─────────────────┴───────────────────────────────────────┘
```

---

## 4. Technical Architecture

### 4.1 Project Structure
```
📁 Samples/00_Foundation/01_TradingWorkstation/
├── 📄 TradingWorkstation.csproj
├── 📄 App.xaml
├── 📄 App.xaml.cs
├── 📄 MainWindow.xaml
├── 📄 MainWindow.xaml.cs
├── 📁 Views/
│   ├── 📄 ConnectorsView.xaml/.cs
│   ├── 📄 StrategiesView.xaml/.cs
│   ├── 📄 LiveTradingView.xaml/.cs
│   ├── 📄 BacktestingView.xaml/.cs
│   └── 📄 SettingsView.xaml/.cs
├── 📁 Models/
│   ├── 📄 StrategyTemplate.cs
│   ├── 📄 TradingSession.cs
│   ├── 📄 BacktestResult.cs
│   └── 📄 AppSettings.cs
├── 📁 Strategies/
│   ├── 📄 SmaStrategy.cs (copied)
│   ├── 📄 BollingerBandsStrategy.cs
│   └── 📄 StrategyBase.cs
├── 📁 Services/
│   ├── 📄 ConnectorManager.cs
│   ├── 📄 StrategyManager.cs
│   └── 📄 BacktestingService.cs
├── 📁 Properties/
│   ├── 📄 AssemblyInfo.cs
│   └── 📄 Resources.resx
└── 📄 README.md
```

### 4.2 Project Configuration (.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\..\common_samples_netwindows.props" />
  <Import Project="..\..\common_connectors.props" />

  <PropertyGroup>
    <AssemblyName>TradingWorkstation</AssemblyName>
    <RootNamespace>StockSharp.Samples.Foundation.TradingWorkstation</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core StockSharp packages -->
    <PackageReference Include="StockSharp.Xaml" Version="$(StockSharpVer)" />
    <PackageReference Include="StockSharp.Xaml.Charting" Version="$(StockSharpVer)" />
    <PackageReference Include="StockSharp.Samples.HistoryData" Version="$(StockSharpVer)" />

    <!-- Additional UI libraries -->
    <PackageReference Include="MaterialDesignThemes" Version="4.9.0" />
    <PackageReference Include="MaterialDesignColors" Version="2.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Algo\Algo.csproj" />
  </ItemGroup>
</Project>
```

### 4.3 Dependencies and References
**Base Dependencies** (from existing samples):
- `StockSharp.Xaml` - Core UI components
- `StockSharp.Xaml.Charting` - Chart controls
- `StockSharp.Samples.HistoryData` - Sample historical data

**Additional Dependencies**:
- `MaterialDesignThemes` - Modern UI styling
- `MaterialDesignColors` - Color themes

---

## 5. Implementation Plan

### 5.1 Development Phases

#### Phase 1: Foundation Setup (Week 1)
**Effort**: 40 hours

**Tasks**:
1. **[T1.1]** Create project structure at `Samples/00_Foundation/01_TradingWorkstation/`
2. **[T1.2]** Configure .csproj with correct dependencies and references
3. **[T1.3]** Copy and adapt MainWindow base from LiveTerminal
4. **[T1.4]** Implement tabbed interface navigation
5. **[T1.5]** Setup basic MVVM structure with ViewModels

**Code Migration**:
```csharp
// Copy from LiveTerminal/MainWindow.xaml.cs
public readonly Connector Connector;              // Line 28
public readonly LogManager LogManager;             // Line 29
private void InitConnector() { ... }              // Lines 101-172

// Adapt to tabbed interface
private void ConnectorsTab_Loaded() { ... }       // New
private void StrategiesTab_Loaded() { ... }       // New
private void LiveTradingTab_Loaded() { ... }      // New
private void BacktestingTab_Loaded() { ... }      // New
```

**Deliverables**:
- ✅ Working project with compilation
- ✅ Basic UI with 5 tabs
- ✅ Connector initialization working
- ✅ Navigation between tabs functional

#### Phase 2: Core Trading Features (Week 2-3)
**Effort**: 60 hours

**Tasks**:
1. **[T2.1]** Implement ConnectorsView with multi-connector support
2. **[T2.2]** Copy and adapt strategy framework from SmaStrategy.cs
3. **[T2.3]** Implement StrategiesView with parameter editing
4. **[T2.4]** Copy LiveTradingView components from LiveTerminal windows
5. **[T2.5]** Integrate real-time data feeds and order management

**Code Migration Map**:
```csharp
// From LiveTerminal - Window Integration
private readonly SecuritiesWindow → SecuritiesPanel      // Line 31 → New Panel
private readonly OrdersWindow → OrdersGrid              // Line 32 → New Grid
private readonly PortfoliosWindow → PortfoliosGrid      // Line 33 → New Grid
private readonly MyTradesWindow → TradesGrid            // Line 34 → New Grid
private readonly StrategiesWindow → StrategiesPanel     // Line 35 → New Panel

// From SmaStrategy.cs - Complete Copy
class SmaStrategy : Strategy { ... }                    // Lines 15-202 → Strategies/SmaStrategy.cs
```

**Deliverables**:
- ✅ Multi-connector configuration working
- ✅ Strategy creation and editing functional
- ✅ Live trading dashboard with real-time data
- ✅ Order placement and monitoring working

#### Phase 3: Backtesting Integration (Week 4)
**Effort**: 40 hours

**Tasks**:
1. **[T3.1]** Copy backtesting engine from History sample
2. **[T3.2]** Implement BacktestingView with configuration UI
3. **[T3.3]** Integrate charting and performance metrics
4. **[T3.4]** Add result export functionality
5. **[T3.5]** Connect strategy parameters between Live and Backtest

**Code Migration Map**:
```csharp
// From History/MainWindow.xaml.cs - Complete Backtesting Engine
private void StartBtnClick() { ... }                    // Lines 236-594 → BacktestingService.cs
private sealed class EmulationInfo { ... }              // Lines 33-45 → Models/BacktestConfiguration.cs
var connector = new HistoryEmulationConnector() { ... } // Lines 331-372 → BacktestingService.cs

// Performance Tracking
strategy.PnLReceived2 += (s, pf, t, r, u, c) => { ... } // Lines 492-504 → BacktestResult.cs
strategy.PositionReceived += (s, p) => { ... }          // Lines 508-517 → BacktestResult.cs
```

**Deliverables**:
- ✅ Historical backtesting functional
- ✅ Performance metrics and charting working
- ✅ Strategy parameter consistency between Live/Backtest
- ✅ Results export (CSV/JSON) implemented

#### Phase 4: Polish & Advanced Features (Week 5)
**Effort**: 20 hours

**Tasks**:
1. **[T4.1]** Implement SettingsView with configuration management
2. **[T4.2]** Add risk management controls
3. **[T4.3]** Implement notifications and alerts system
4. **[T4.4]** Add import/export for strategies and configurations
5. **[T4.5]** Apply Material Design theming and polish UI

**Deliverables**:
- ✅ Complete settings management
- ✅ Risk controls and position limits
- ✅ Professional UI with theming
- ✅ Configuration import/export working

### 5.2 Code Reuse Summary

| Component | Source File | Target Location | Lines | Effort |
|-----------|-------------|-----------------|-------|--------|
| **Connector Management** | `LiveTerminal/MainWindow.xaml.cs` | `Services/ConnectorManager.cs` | 101-172 | Copy + Adapt |
| **Real-time Trading** | `LiveTerminal/MainWindow.xaml.cs` | `Views/LiveTradingView.xaml.cs` | 130-143 | Copy + Adapt |
| **Strategy Framework** | `History/SmaStrategy.cs` | `Strategies/SmaStrategy.cs` | 15-202 | Direct Copy |
| **Backtesting Engine** | `History/MainWindow.xaml.cs` | `Services/BacktestingService.cs` | 236-594 | Copy + Adapt |
| **Window Components** | `LiveTerminal/SecurityWindow.xaml` etc | `Views/Panels/` | All files | Convert to UserControls |

**Total Code Reuse**: ~70% of business logic
**New Development**: ~30% for UI integration and enhancements

---

## 6. Acceptance Criteria

### 6.1 Functional Acceptance Criteria

#### AC1: Connector Management
- [✓] User can view list of 60+ supported connectors
- [✓] User can configure multiple accounts per connector type
- [✓] Connection status is visually indicated (Connected/Disconnected/Error)
- [✓] User can test connections before activating
- [✓] Credentials are securely stored and encrypted

#### AC2: Strategy Management
- [✓] User can create strategies from templates (SMA, Bollinger, etc.)
- [✓] User can edit strategy parameters with validation
- [✓] User can enable/disable strategies independently
- [✓] Strategy configurations can be saved and loaded
- [✓] Strategy performance metrics are tracked

#### AC3: Live Trading
- [✓] User can select Strategy + Symbol + Account combinations
- [✓] Risk controls (SL/TP/Position Size) are configurable
- [✓] Real-time P&L is displayed and updates live
- [✓] Orders and positions are monitored in real-time
- [✓] Trade history is maintained with filtering options
- [✓] User can start/stop strategies individually

#### AC4: Backtesting
- [✓] User can select historical date ranges and timeframes
- [✓] Multiple data types are supported (Ticks, Candles, OrderBook)
- [✓] Backtests can be run at various speeds (1x, 5x, 10x, Instant)
- [✓] Performance metrics are calculated (Sharpe, Drawdown, Win Rate)
- [✓] Results are visualized with equity curves and trade overlays
- [✓] Results can be exported to CSV/JSON formats

#### AC5: Settings & Configuration
- [✓] Application settings are persistent between sessions
- [✓] User can configure notifications and alerts
- [✓] Logging levels and destinations are configurable
- [✓] Configurations can be imported/exported
- [✓] UI themes can be switched (Light/Dark)

### 6.2 Technical Acceptance Criteria

#### TAC1: Performance
- Application startup time < 5 seconds
- Real-time data updates < 100ms latency
- Backtest execution handles 1M+ data points efficiently
- Memory usage stable under extended operation

#### TAC2: Reliability
- Application handles connection failures gracefully
- All user data persists between sessions
- No memory leaks during extended operation
- Proper error handling and user feedback

#### TAC3: Usability
- Intuitive navigation between functional areas
- Consistent UI patterns throughout application
- Responsive design adapts to different screen sizes
- Professional appearance suitable for production use

---

## 7. Risk Assessment

### 7.1 Technical Risks

| Risk | Impact | Probability | Mitigation Strategy |
|------|---------|-------------|-------------------|
| **Complex UI Integration** | High | Medium | Incremental development with frequent testing |
| **Performance with Real-time Data** | High | Low | Reuse proven patterns from LiveTerminal |
| **Strategy Parameter Synchronization** | Medium | Medium | Shared configuration models between Live/Backtest |
| **Multi-threading Issues** | High | Low | Follow StockSharp threading patterns |

### 7.2 Schedule Risks

| Risk | Impact | Probability | Mitigation Strategy |
|------|---------|-------------|-------------------|
| **UI Complexity Underestimated** | Medium | Medium | Prioritize core functionality over polish |
| **Code Migration Issues** | Low | Low | Thorough testing of copied components |
| **Feature Scope Creep** | High | Medium | Strict adherence to MVP requirements |

---

## 8. Success Metrics

### 8.1 Development Metrics
- **Code Reuse Target**: 70% (measured by LOC)
- **Development Time**: 160 hours total (5 weeks)
- **Bug Density**: < 1 bug per 100 LOC in final release
- **Test Coverage**: > 80% for business logic components

### 8.2 User Experience Metrics
- **Time to First Trade**: < 5 minutes from application start
- **Learning Curve**: New users productive within 30 minutes
- **Feature Discovery**: All major features accessible within 3 clicks
- **Error Recovery**: Clear error messages with actionable guidance

---

## 9. Appendices

### Appendix A: Existing Sample Analysis

#### A.1 LiveTerminal Sample (`Samples/06_Strategies/10_LiveTerminal/`)
**Strengths**:
- Complete connector management implementation
- Multi-window architecture with Securities, Orders, Portfolios, etc.
- Real-time data subscription handling
- Proper event-driven architecture following StockSharp patterns

**Reusable Components**:
- `MainWindow.xaml.cs:101-172` - Connector initialization and event handling
- `MainWindow.xaml.cs:195-199` - Connector configuration dialog integration
- `MainWindow.xaml.cs:130-143` - Real-time data event subscribers
- Individual window classes (SecuritiesWindow, OrdersWindow, etc.)

**Adaptation Required**:
- Convert multi-window to single-window tabbed interface
- Integrate Material Design theming
- Add enhanced risk management controls

#### A.2 History Testing Sample (`Samples/07_Testing/01_History/`)
**Strengths**:
- Complete backtesting framework with multiple data source support
- Performance metrics calculation and visualization
- Progress tracking and state management
- Comprehensive strategy testing infrastructure

**Reusable Components**:
- `MainWindow.xaml.cs:236-594` - Complete backtesting execution engine
- `SmaStrategy.cs:15-202` - Strategy implementation template
- `MainWindow.xaml.cs:487-517` - Performance tracking and charting
- EmulationInfo class for backtest configuration

**Adaptation Required**:
- Integrate with unified strategy management system
- Adapt UI for tabbed interface
- Add result export functionality

### Appendix B: Material Design Integration

#### B.1 Theme Configuration
```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <materialDesign:BundledTheme BaseTheme="Light"
                                       PrimaryColor="Blue"
                                       SecondaryColor="Orange" />
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

#### B.2 Common UI Patterns
```xml
<!-- Material Design Tab Header -->
<TabItem Header="Live Trading">
    <TabItem.Header>
        <StackPanel Orientation="Horizontal">
            <materialDesign:PackIcon Kind="TrendingUp" Margin="0,0,5,0" />
            <TextBlock Text="Live Trading" />
        </StackPanel>
    </TabItem.Header>
</TabItem>

<!-- Material Design Cards for sections -->
<materialDesign:Card Margin="10" Padding="15">
    <StackPanel>
        <TextBlock Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                   Text="Strategy Configuration" />
        <!-- Content -->
    </StackPanel>
</materialDesign:Card>
```

### Appendix C: Testing Strategy

#### C.1 Unit Testing Approach
- Test strategy parameter validation
- Test connector configuration persistence
- Test backtesting calculations
- Mock external dependencies (market data, brokers)

#### C.2 Integration Testing
- Test real-time data flow end-to-end
- Test strategy execution in both Live and Backtest modes
- Test UI responsiveness under load
- Test configuration import/export round-trip

#### C.3 User Acceptance Testing
- New user onboarding workflow
- Strategy creation and deployment workflow
- Backtesting and result analysis workflow
- Multi-connector configuration workflow

---

**Document Status**: ✅ Complete
**Next Action**: Begin Phase 1 implementation
**Approver**: Development Team Lead
**Review Date**: Weekly during development phases