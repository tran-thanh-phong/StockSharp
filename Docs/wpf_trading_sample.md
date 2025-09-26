# 📊 WPF Sample App – Menu & Flow Review

## ✅ Existing Menu Items (from user)
1. **List out supported connector**
2. **Connect to a connector with personal credentials**
   - Support multiple accounts per user
   - Support same connector (multi-account) and different connectors
3. **Signal / Strategy Management**
4. **Live Trading** = strategy + symbol + account
   - Rule setup
   - Order & account monitor
   - Trade history
5. **Back Test** = strategy + symbol + demo account
   - Setup
   - Running
   - Result

---

## 🔍 Review & Recommendations

### 1. Connectors & Accounts
- ✅ Already included.
- 🔧 **Add:** Test connection & status indicator per account.
- 🔧 **Add:** Centralized key management (encrypted storage).

### 2. Strategy Management
- ✅ Signal/Strategy management is included.
- 🔧 **Add:**
  - Parameter editor (timeframe, indicators, risk %).
  - Strategy templates (basic SMA crossover, breakout, etc.).
  - Enable/disable strategy.

### 3. Live Trading Flow
- ✅ Covers rule setup, monitor, history.
- 🔧 **Add:**
  - Start/Stop toggle for each running bot.
  - Risk control settings (SL/TP %, daily loss cap).
  - Real-time PnL display & equity curve.
  - Notification/alert panel (errors, fills).

### 4. Backtesting Flow
- ✅ Setup, run, results are included.
- 🔧 **Add:**
  - Data source selection (historical data provider).
  - Backtest speed control (1x, 5x, instant).
  - Visual chart output (candlesticks + trades overlay).
  - Performance metrics (Sharpe, drawdown, win rate).

### 5. Cross-Cutting Features
- 🔧 **Add:**
  - Logging & audit trail (per connector/account/strategy).
  - Settings menu (general app config).
  - Export/Import configs (strategies, runs).
  - Switch between **paper trading vs real trading** for live mode.

---

## 🏁 Revised Menu Structure

1. **Connectors**
   - Supported connectors list
   - Add/manage accounts
   - Connection test & status

2. **Strategies**
   - Create/edit strategy
   - Parameter setup
   - Templates & saved strategies

3. **Live Trading**
   - New Run: select strategy + symbol + account
   - Rule setup (SL/TP, risk, order type)
   - Monitor: balances, open positions, orders, PnL
   - Trade history (per run/account)
   - Start/Stop control

4. **Backtesting**
   - Setup: select strategy + symbol + timeframe + data source
   - Run: speed control, simulation log
   - Results: metrics (PnL, win rate, drawdown, Sharpe)
   - Chart visualization

5. **Tools / Settings**
   - Notifications/alerts
   - Logs & audit
   - Export/Import configs
   - Global settings (theme, data path, etc.)

---

## 🎯 Coverage Check
- **Backtest Flow** ✅ Full lifecycle: setup → run → results with charts + metrics.
- **Live Trading Flow** ✅ Full lifecycle: setup → run → monitor → history with risk controls.
- **Both flows share strategies**, ensuring reusability.

➡️ With these additions, the sample app will support the two essential workflows (backtest & live trading) in a realistic and extendable way.


---

## 🧩 Extensible Strategy Builder (Plugin‑Ready)

### Goals
- Start simple (EMA/ATR, MACD/EMA) but allow future expansion (news, Telegram OG, custom DLLs).
- Single strategy interface works for both **Backtest** and **Live**.

### Core Interfaces
```csharp
public interface IStrategy
{
    string Name { get; }
    void Initialize(StrategyContext ctx);
    SignalResult OnNewCandle(Candle candle);
    void OnOrderUpdate(OrderUpdate update);
}

public sealed class StrategyContext
{
    public IMarketDataFeed MarketData { get; init; }   // candles, ticks
    public IExecutionService Execution { get; init; }  // place/cancel orders
    public IAccountInfo Account { get; init; }         // balances, positions
    public IIndicatorRegistry Indicators { get; init; }// EMA, ATR, MACD...
    public IDictionary<string, object> Params { get; init; } // user params
}

public record SignalResult(TradeAction Action, decimal? Price = null,
                           decimal? StopLoss = null, decimal? TakeProfit = null,
                           decimal Confidence = 1m);
```

### Indicators as First‑Class Plugins
```csharp
public interface IIndicator
{
    string Key { get; }
    void Reset();
    void Update(Candle candle);
    decimal? Value { get; }
}
```
- Built‑ins: **EMA**, **ATR**, **MACD**, **Bollinger**.
- New indicators discovered via **MEF / Reflection** from `/plugins` folder.

### Strategy Composition Options
1. **Code Strategies** (compiled DLLs implementing `IStrategy`).
2. **Config‑Driven Strategies** (no code):
   ```json
   {
     "name": "EMA_ATR_Filter",
     "indicators": [
       {"type": "EMA", "alias": "emaFast", "period": 12},
       {"type": "EMA", "alias": "emaSlow", "period": 26},
       {"type": "ATR", "alias": "atr", "period": 14}
     ],
     "rules": [
       "BUY  when crossOver(emaFast, emaSlow) and atr > 0.5",
       "SELL when crossUnder(emaFast, emaSlow) or  stopHit() or takeProfitHit()"
     ],
     "risk": {"positionPct": 0.02, "slPct": 0.01, "tpPct": 0.02}
   }
   ```
   - Backed by a tiny **Rule Engine** that exposes primitives: `crossOver`, `crossUnder`, `slope`, comparisons, AND/OR.

### External Signal Providers (Phase‑Up)
- **News**: `INewsSignalProvider` pulls sentiment/keywords → emits `SignalResult`.
- **Telegram**: `ITelegramSignalProvider` parses channel posts → signals.
- Providers are optional plugins feeding a **Signal Bus** consumed by strategies.

### Runner Integration (Backtest & Live)
- **BacktestRunner**: feeds historical candles → `IStrategy` → simulated orders → metrics.
- **LiveRunner**: subscribes to live candles/WS → `IStrategy` → real/paper orders → monitoring.
- Same `StrategyContext` so strategies are 1:1 portable between modes.

### Menu Additions for WPF Sample
- **Strategies**
  - *New Strategy* → (Code) / (Config‑Driven)
  - *Parameters* → per strategy instance (bind to `StrategyContext.Params`)
  - *Plugins* → scan `/plugins`, enable/disable indicators & strategies
- **Backtesting**
  - *Rule Engine Tester* → dry‑run JSON rules on a slice of data
  - *Compare Runs* → select multiple results, show side‑by‑side metrics
- **Live Trading**
  - *Signal Stream View* → live list of generated signals (source: indicators/news/telegram)
  - *Risk Guard* → daily loss cap, max concurrent positions, toggle per run

### Minimal Roadmap for Extensibility
1. **M0**: Built‑in indicators (EMA/ATR/MACD), code strategies, Backtest/Live parity.
2. **M1**: Config‑driven strategies + Rule Engine + plugin discovery.
3. **M2**: External signal providers (News/Telegram) via provider plugins.
4. **M3**: Strategy Marketplace (import/export signed bundles), permissions.

### Validation Checklist
- Strategy DLL hot‑load without app restart.
- Backtest → Live run uses identical params & produces comparable signals.
- Rule Engine unit tests for common patterns (crossOver/crossUnder, SL/TP events).
- Per‑strategy logging & audit trail (inputs, signals, orders) exportable to CSV/JSON.

