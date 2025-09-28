# Connection Contract

## Authentication Flow

### Connect Request
```csharp
// StockSharp Message
ConnectMessage
{
    // Handled internally using configured OAuth2 credentials
}

// Translates to cTrader OAuth2 Flow
1. ProtoOAApplicationAuthReq
   - ClientId: ApplicationId from settings
   - ClientSecret: ApplicationSecret from settings

2. ProtoOAApplicationAuthRes
   - Response validation and token storage

3. ProtoOAAccountAuthReq
   - AccessToken: From OAuth2 response
   - AccountId: Target trading account

4. ProtoOAAccountAuthRes
   - Account validation and connection establishment
```

### Connection State Management
```csharp
// Connection States
enum ConnectionState
{
    Disconnected,     // Initial state
    Connecting,       // OAuth2 in progress
    Authenticating,   // Account auth in progress
    Connected,        // Ready for trading
    Reconnecting,     // Automatic reconnection
    Disconnecting,    // Graceful shutdown
    Error            // Authentication or connection failure
}

// State Transitions
Disconnected → Connecting (ConnectMessage received)
Connecting → Authenticating (OAuth2 successful)
Authenticating → Connected (Account auth successful)
Connected → Reconnecting (Connection lost)
Reconnecting → Connected (Reconnection successful)
Connected → Disconnecting (DisconnectMessage received)
Any State → Error (Authentication failure)
Error → Disconnected (Reset or retry)
```

## Heartbeat and Keep-Alive

### Heartbeat Request
```csharp
// StockSharp Internal
TimeMessage
{
    LocalTime: CurrentTime
}

// Translates to cTrader
ProtoOAHeartbeatEvent
{
    UtcTimestamp: Current UTC time
}
```

### Connection Monitoring
```csharp
// Connection Health Contract
interface IConnectionHealth
{
    TimeSpan LastHeartbeat { get; }
    bool IsConnected { get; }
    ConnectionState State { get; }
    string? LastError { get; }
    int ReconnectionAttempts { get; }
}

// Heartbeat Response Validation
ProtoOAHeartbeatEvent Response:
- Must be received within 30 seconds of request
- Timestamp must be within 5 seconds of server time
- Missing heartbeat triggers reconnection attempt
```

## Error Handling Contract

### Authentication Errors
```csharp
// OAuth2 Authentication Failure
ProtoOAApplicationAuthRes
{
    ErrorCode: "INVALID_CLIENT" | "ACCESS_DENIED" | "EXPIRED_TOKEN"
    Description: Human-readable error message
}

// StockSharp Error Response
ErrorMessage
{
    Error: InvalidOperationException("Authentication failed: {Description}")
    Type: MessageTypes.Connect
}
```

### Connection Errors
```csharp
// Network/Connection Failures
ConnectionState.Error Reasons:
- TCP connection timeout
- SSL certificate validation failure
- WebSocket handshake failure
- cTrader server unavailable
- Rate limiting (HTTP 429)

// StockSharp Error Handling
ErrorMessage
{
    Error: ConnectionException("Connection failed: {Reason}")
    Type: MessageTypes.Connect
}

// Automatic Reconnection Logic
ReconnectionAttempts: 1, 2, 3, 4, 5... (max 10)
BackoffDelay: 1s, 2s, 4s, 8s, 16s, 30s (exponential backoff)
```

## Disconnect Contract

### Graceful Disconnect
```csharp
// StockSharp Request
DisconnectMessage
{
    // Clean shutdown requested
}

// cTrader Cleanup Sequence
1. Cancel all active subscriptions
2. Close market data streams
3. Send final heartbeat
4. Close TCP/WebSocket connection
5. Clear authentication tokens

// StockSharp Confirmation
DisconnectMessage
{
    // Disconnection completed
}
```

### Forced Disconnect
```csharp
// Credential Expiration (from clarification)
When: Authentication credentials expire or become invalid
Action: Disconnect completely and alert user

ErrorMessage
{
    Error: SecurityException("Credentials expired - please reconnect")
    Type: MessageTypes.Connect
}

ConnectionState → Disconnected
ClearAllSubscriptions()
NotifyUser("Authentication expired - reconnection required")
```

## Configuration Contract

### Required Settings
```csharp
public class CTraderConnectionSettings
{
    [Required]
    [DisplayName("Application ID")]
    public string ApplicationId { get; set; }

    [Required]
    [DisplayName("Application Secret")]
    public SecureString ApplicationSecret { get; set; }

    [Required]
    [DisplayName("Environment")]
    public CTraderEnvironment Environment { get; set; } = CTraderEnvironment.Demo;

    [DisplayName("Account ID")]
    public long? AccountId { get; set; }

    [DisplayName("Connection Timeout (seconds)")]
    [Range(5, 120)]
    public int ConnectionTimeout { get; set; } = 30;

    [DisplayName("Heartbeat Interval (seconds)")]
    [Range(10, 300)]
    public int HeartbeatInterval { get; set; } = 30;
}

public enum CTraderEnvironment
{
    Demo,    // demo-ctrader.com
    Live     // live-ctrader.com
}
```

### Connection URL Resolution
```csharp
// Environment-specific endpoints
Demo Environment:
- Host: "demo.ctraderop.com"
- Port: 5035 (TCP) / 5036 (WebSocket)
- SSL: Required

Live Environment:
- Host: "live.ctraderop.com"
- Port: 5035 (TCP) / 5036 (WebSocket)
- SSL: Required
```

## Performance Requirements

### Latency Targets
```csharp
// Connection Establishment (from clarification)
Target: Complete authentication within 5 seconds
Measurement: From ConnectMessage to Connected state

// Heartbeat Response
Target: Round-trip time < 1 second
Measurement: HeartbeatRequest to HeartbeatResponse

// Market Data Latency (from clarification)
Target: Market data updates within 100ms
Measurement: cTrader event timestamp to StockSharp message delivery
```

### Reliability Requirements
```csharp
// Connection Uptime
Target: 99.9% uptime during market hours
Automatic reconnection on transient failures
Maximum 10 reconnection attempts before manual intervention

// Authentication Token Management
Proactive token refresh before expiration
Secure token storage in memory only
Token invalidation on disconnect
```

## Security Contract

### Credential Protection
```csharp
// Secure Storage Requirements
- ApplicationSecret stored as SecureString
- OAuth2 tokens kept in memory only
- No credentials written to logs
- Secure disposal of sensitive objects

// Communication Security
- All connections use SSL/TLS 1.2+
- Certificate validation required
- No fallback to insecure protocols
```

### Rate Limiting Compliance
```csharp
// cTrader API Limits (estimated)
Authentication: 5 requests per minute
Market Data: 100 subscriptions per account
Trading: 10 orders per second
Historical Data: 5 requests per minute

// Rate Limiting Strategy
- Built-in SDK rate limiting respected
- Exponential backoff on rate limit errors
- Queue management for high-frequency operations
```