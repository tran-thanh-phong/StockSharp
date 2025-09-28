# Data Model: Connector Management

## Core Entities

### 1. Connector
**Purpose**: Represents a trading platform integration type
**Source**: StockSharp core `IConnector` interface with extensions

**Attributes**:
- `Type` (string): Connector type identifier (e.g., "Binance", "InteractiveBrokers")
- `Name` (string): Display name for UI
- `Description` (string): Human-readable description
- `SupportedFeatures` (enum flags): Market data, trading, historical data capabilities
- `ConfigurationSchema` (Type): Type information for connector-specific settings
- `Icon` (ImageSource): Visual identifier for UI
- `IsAvailable` (bool): Whether connector is available for configuration

**Relationships**:
- One Connector type can have multiple Account instances
- Connected to ConfigurationTemplate for default settings

**Validation Rules**:
- Type must be unique across all connectors
- Name must be non-empty and user-friendly
- SupportedFeatures must have at least one capability

**State Transitions**: Static entity - no state changes

### 2. Account
**Purpose**: A specific configured instance of a connector with credentials
**Source**: Custom wrapper around StockSharp `Connector` instances

**Attributes**:
- `Id` (Guid): Unique identifier for this account instance
- `ConnectorType` (string): References Connector.Type
- `AccountName` (string): User-defined name for this account
- `Configuration` (SettingsStorage): Connector-specific settings and credentials
- `IsEnabled` (bool): Whether this account should auto-connect
- `CreatedDate` (DateTime): When account was configured
- `LastUsed` (DateTime): Last successful connection time

**Relationships**:
- Belongs to one Connector type
- Has one Configuration instance
- Can have one active ConnectionStatus

**Validation Rules**:
- AccountName must be unique per connector type
- Configuration must be valid for the connector type
- ConnectorType must reference an available Connector

**State Transitions**:
- Created → Configured → Enabled/Disabled
- Can be deleted at any time

### 3. Configuration
**Purpose**: Settings and parameters required for a connector to function
**Source**: StockSharp `SettingsStorage` with extensions

**Attributes**:
- `Settings` (SettingsStorage): StockSharp standard configuration storage
- `SecureData` (Dictionary<string, string>): Plain text credentials (MVP approach)
- `ConnectionTimeout` (TimeSpan): Maximum time for connection attempts (30 seconds)
- `AutoReconnect` (bool): Whether to automatically reconnect on disconnect
- `MaxRetryAttempts` (int): Number of reconnection attempts

**Relationships**:
- Belongs to one Account
- Used by ConnectionStatus for connection attempts

**Validation Rules**:
- ConnectionTimeout must be > 0 and ≤ 30 seconds (per clarification)
- Required fields must be populated based on connector type
- SecureData values must be non-empty for required credential fields

**State Transitions**:
- Draft → Validated → Applied
- Can be modified when account is disconnected

### 4. ConnectionStatus
**Purpose**: Current state and activity information for a connector account
**Source**: Custom aggregation of StockSharp connection events

**Attributes**:
- `AccountId` (Guid): References Account.Id
- `CurrentState` (enum): Connected, Disconnected, Connecting, Error, Testing
- `LastStateChange` (DateTime): When current state was reached
- `ErrorMessage` (string): Details of last error (if any)
- `ActivityLog` (List<ActivityEntry>): Session-only activity history
- `ConnectionStartTime` (DateTime?): When current connection began
- `LastHeartbeat` (DateTime?): Most recent successful communication

**Relationships**:
- Belongs to one Account
- Contains multiple ActivityEntry records (composition)

**Validation Rules**:
- CurrentState must be valid enum value
- ErrorMessage required when CurrentState is Error
- ActivityLog limited to current application session only

**State Transitions**:
- Disconnected → Connecting → Connected
- Any state → Error (on failure)
- Connected → Disconnected (on user action or network failure)
- Any state → Testing (during connection test)

## Supporting Entities

### 5. ActivityEntry
**Purpose**: Individual log entry for connector activity
**Lifetime**: Session-only (per clarification)

**Attributes**:
- `Timestamp` (DateTime): When event occurred
- `Level` (enum): Info, Warning, Error
- `Message` (string): Human-readable event description
- `Details` (string): Technical details for troubleshooting

**Validation Rules**:
- Timestamp must be ≤ current time
- Message must be non-empty
- Level must be valid enum value

### 6. ConnectorCapability
**Purpose**: Enumeration of supported connector features

**Values**:
- `MarketData`: Real-time price feeds
- `Trading`: Order execution
- `HistoricalData`: Historical price data
- `Level2Data`: Market depth information
- `Options`: Options trading support
- `Crypto`: Cryptocurrency support

## Data Relationships

```
Connector (1) ──── (N) Account
    │                   │
    │                   │
    └── ConfigurationTemplate  Configuration (1)
                               │
                               │
                        ConnectionStatus (1)
                               │
                               │
                        ActivityEntry (N)
```

## Storage Implementation

### File-Based Storage (MVP Approach)
**Location**: `%AppData%\StockSharp\TradingWorkstation\Connectors\`

**Structure**:
```
Connectors/
├── accounts.xml          # Account definitions
├── configurations/       # Individual account configurations
│   ├── {AccountId}.xml
│   └── {AccountId}.xml
└── activity/            # Session logs (cleared on restart)
    ├── {AccountId}.log
    └── {AccountId}.log
```

### Serialization Strategy
- **Accounts**: XML serialization using StockSharp `SettingsStorage`
- **Configurations**: StockSharp native configuration format
- **Activity Logs**: Simple text files with timestamp prefix
- **Security**: Plain text storage (per clarification)

### Data Access Patterns
- **Load on Startup**: All account configurations loaded into memory
- **Save on Change**: Immediate persistence when configuration modified
- **Session Cleanup**: Activity logs cleared on application restart
- **Backup**: No automatic backup (MVP scope)

## Integration Points

### StockSharp Integration
- Leverage existing `Connector.Save()` and `Connector.Load()` methods
- Use `AvailableConnectors` for connector discovery
- Integrate with `Connector.Configure()` for UI dialogs

### WPF Data Binding
- All entities implement `INotifyPropertyChanged` for UI binding
- Observable collections for dynamic lists (accounts, activity entries)
- Validation attributes for data binding validation

### Configuration Migration
- Support for importing/exporting account configurations
- Backward compatibility with existing StockSharp configuration formats
- Conflict resolution for duplicate account names

## Performance Considerations

### Memory Usage
- Activity logs limited to 1000 entries per session per account
- Configuration caching to avoid repeated file I/O
- Lazy loading of connector icons and descriptions

### Concurrency
- Thread-safe collections for activity logs
- UI thread marshaling for status updates
- Lock-free reads for configuration data

### MVP Constraints
- No database requirements - file-based storage only
- No encryption overhead - plain text approach
- Session-only retention - minimal disk usage
- Unlimited connections - no artificial limits