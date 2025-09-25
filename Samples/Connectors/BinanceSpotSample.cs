using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StockSharp.Messages;
using StockSharp.BusinessEntities;
using StockSharp.Binance.Spot;

namespace StockSharp.Samples.Connectors;

/// <summary>
/// Sample demonstrating Binance Spot connector usage for cryptocurrency trading.
/// </summary>
public class BinanceSpotSample
{
    private BinanceSpotMessageAdapter _adapter = null!;
    private readonly SecurityId _btcUsdt = new() { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };

    /// <summary>
    /// Run the Binance Spot connector sample.
    /// </summary>
    public async Task RunAsync()
    {
        Console.WriteLine("🚀 Binance Spot Connector Sample");
        Console.WriteLine("================================");

        try
        {
            // Step 1: Initialize the adapter
            await InitializeAdapterAsync();

            // Step 2: Connect and authenticate
            await ConnectAsync();

            // Step 3: Get available securities
            await LookupSecuritiesAsync();

            // Step 4: Subscribe to market data
            await SubscribeToMarketDataAsync();

            // Step 5: Demonstrate order operations (testnet only!)
            if (_adapter.UseTestnet)
            {
                await DemonstrateOrderOperationsAsync();
            }

            // Step 6: Monitor account updates
            await MonitorAccountAsync();

            Console.WriteLine("\n✅ Sample completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Sample failed: {ex.Message}");
        }
        finally
        {
            await CleanupAsync();
        }
    }

    /// <summary>
    /// Initialize the Binance Spot message adapter with configuration.
    /// </summary>
    private async Task InitializeAdapterAsync()
    {
        Console.WriteLine("\n📋 Initializing Binance Spot Adapter...");

        _adapter = new BinanceSpotMessageAdapter(TransactionIdGenerator.GetGenerator())
        {
            // CRITICAL: Always use testnet for samples and testing!
            UseTestnet = true,

            // Configure logging level
            LogLevel = LogLevels.Info,

            // Configure connection settings
            AutoReconnect = true,
            ReconnectInterval = TimeSpan.FromSeconds(30),
            MaxReconnectAttempts = 10,

            // Configure market data settings
            OrderBookDepth = 20,
            MaintainLocalOrderBook = true,

            // Configure request timeouts
            RequestTimeout = TimeSpan.FromSeconds(30),
            ReceiveWindow = TimeSpan.FromSeconds(5)
        };

        // Set up credentials (for testnet)
        // In production, load these from secure configuration!
        var testApiKey = Environment.GetEnvironmentVariable("BINANCE_TESTNET_API_KEY") ?? "";
        var testSecret = Environment.GetEnvironmentVariable("BINANCE_TESTNET_SECRET") ?? "";

        if (!string.IsNullOrEmpty(testApiKey) && !string.IsNullOrEmpty(testSecret))
        {
            _adapter.Key = new System.Security.SecureString();
            _adapter.Secret = new System.Security.SecureString();

            foreach (char c in testApiKey)
                _adapter.Key.AppendChar(c);
            foreach (char c in testSecret)
                _adapter.Secret.AppendChar(c);

            _adapter.Key.MakeReadOnly();
            _adapter.Secret.MakeReadOnly();

            Console.WriteLine("🔐 Credentials configured from environment variables");
        }
        else
        {
            Console.WriteLine("⚠️  No credentials found. Some features will not work.");
            Console.WriteLine("   Set BINANCE_TESTNET_API_KEY and BINANCE_TESTNET_SECRET environment variables");
        }

        // Set up message handling
        _adapter.NewOutMessage += OnMessageReceived;

        Console.WriteLine("✅ Adapter initialized successfully");
    }

    /// <summary>
    /// Connect to Binance and authenticate.
    /// </summary>
    private async Task ConnectAsync()
    {
        Console.WriteLine("\n🔌 Connecting to Binance Testnet...");

        var connectComplete = new TaskCompletionSource<bool>();

        void HandleConnectionMessage(Message message)
        {
            if (message is ConnectMessage connectMsg)
            {
                if (connectMsg.Error == null)
                {
                    Console.WriteLine("✅ Connected successfully!");
                    connectComplete.SetResult(true);
                }
                else
                {
                    Console.WriteLine($"❌ Connection failed: {connectMsg.Error.Message}");
                    connectComplete.SetException(connectMsg.Error);
                }
            }
        }

        _adapter.NewOutMessage += HandleConnectionMessage;

        try
        {
            _adapter.SendInMessage(new ConnectMessage());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await connectComplete.Task.WaitAsync(cts.Token);
        }
        finally
        {
            _adapter.NewOutMessage -= HandleConnectionMessage;
        }
    }

    /// <summary>
    /// Lookup available securities from Binance.
    /// </summary>
    private async Task LookupSecuritiesAsync()
    {
        Console.WriteLine("\n🔍 Looking up available securities...");

        var lookupComplete = new TaskCompletionSource<bool>();
        var securities = new List<SecurityMessage>();

        void HandleSecurityMessage(Message message)
        {
            switch (message)
            {
                case SecurityMessage secMsg:
                    securities.Add(secMsg);
                    break;

                case SecurityLookupResultMessage resultMsg:
                    if (resultMsg.Error == null)
                    {
                        Console.WriteLine($"✅ Found {securities.Count} securities");
                        lookupComplete.SetResult(true);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Security lookup failed: {resultMsg.Error.Message}");
                        lookupComplete.SetException(resultMsg.Error);
                    }
                    break;
            }
        }

        _adapter.NewOutMessage += HandleSecurityMessage;

        try
        {
            var lookupMsg = new SecurityLookupMessage
            {
                TransactionId = _adapter.TransactionIdGenerator.GetNextId()
            };

            _adapter.SendInMessage(lookupMsg);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await lookupComplete.Task.WaitAsync(cts.Token);

            // Show some popular securities
            var popularSymbols = securities
                .Where(s => s.SecurityId.SecurityCode?.Contains("USDT") == true)
                .Take(10)
                .ToArray();

            Console.WriteLine("\n📈 Popular USDT pairs:");
            foreach (var security in popularSymbols)
            {
                Console.WriteLine($"  {security.SecurityId.SecurityCode}: {security.Name}");
            }
        }
        finally
        {
            _adapter.NewOutMessage -= HandleSecurityMessage;
        }
    }

    /// <summary>
    /// Subscribe to real-time market data.
    /// </summary>
    private async Task SubscribeToMarketDataAsync()
    {
        Console.WriteLine("\n📊 Subscribing to market data for BTCUSDT...");

        var subscriptionComplete = new TaskCompletionSource<bool>();
        var tickCount = 0;
        var depthCount = 0;

        void HandleMarketData(Message message)
        {
            switch (message)
            {
                case MarketDataMessage mdMsg when mdMsg.IsSubscribe && mdMsg.Error == null:
                    Console.WriteLine($"✅ Subscribed to {mdMsg.DataType}");
                    if (!subscriptionComplete.Task.IsCompleted)
                        subscriptionComplete.SetResult(true);
                    break;

                case MarketDataMessage mdMsg when mdMsg.Error != null:
                    Console.WriteLine($"❌ Subscription failed: {mdMsg.Error.Message}");
                    if (!subscriptionComplete.Task.IsCompleted)
                        subscriptionComplete.SetException(mdMsg.Error);
                    break;

                case ExecutionMessage execMsg when execMsg.ExecutionType == ExecutionTypes.Tick:
                    tickCount++;
                    if (tickCount <= 5) // Show first 5 ticks
                    {
                        Console.WriteLine($"  🔴 Tick: {execMsg.Price:F2} | {execMsg.Volume:F6} | {execMsg.Side}");
                    }
                    break;

                case QuoteChangeMessage depthMsg:
                    depthCount++;
                    if (depthCount <= 3) // Show first 3 depth updates
                    {
                        var bestBid = depthMsg.Quotes?.Where(q => q.Side == Sides.Buy).OrderByDescending(q => q.Price).FirstOrDefault();
                        var bestAsk = depthMsg.Quotes?.Where(q => q.Side == Sides.Sell).OrderBy(q => q.Price).FirstOrDefault();

                        if (bestBid != null && bestAsk != null)
                        {
                            var spread = bestAsk.Price - bestBid.Price;
                            Console.WriteLine($"  📊 Spread: {bestBid.Price:F2} / {bestAsk.Price:F2} (${spread:F2})");
                        }
                    }
                    break;
            }
        }

        _adapter.NewOutMessage += HandleMarketData;

        try
        {
            // Subscribe to ticks (trades)
            _adapter.SendInMessage(new MarketDataMessage
            {
                TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                SecurityId = _btcUsdt,
                DataType = DataType.Ticks,
                IsSubscribe = true
            });

            // Subscribe to order book depth
            _adapter.SendInMessage(new MarketDataMessage
            {
                TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                SecurityId = _btcUsdt,
                DataType = DataType.MarketDepth,
                IsSubscribe = true
            });

            // Wait for subscriptions to complete
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await subscriptionComplete.Task.WaitAsync(cts.Token);

            // Let market data flow for a bit
            Console.WriteLine("📡 Receiving market data... (10 seconds)");
            await Task.Delay(TimeSpan.FromSeconds(10));

            Console.WriteLine($"✅ Received {tickCount} ticks and {depthCount} depth updates");
        }
        finally
        {
            _adapter.NewOutMessage -= HandleMarketData;
        }
    }

    /// <summary>
    /// Demonstrate order operations (testnet only).
    /// </summary>
    private async Task DemonstrateOrderOperationsAsync()
    {
        Console.WriteLine("\n💰 Demonstrating order operations (TESTNET)...");

        if (_adapter.Key == null || _adapter.Secret == null)
        {
            Console.WriteLine("⚠️  Skipping order operations - no credentials configured");
            return;
        }

        var orderComplete = new TaskCompletionSource<ExecutionMessage>();
        ExecutionMessage? placedOrder = null;

        void HandleOrderMessage(Message message)
        {
            if (message is ExecutionMessage execMsg && execMsg.ExecutionType == ExecutionTypes.Transaction)
            {
                if (execMsg.OrderState == OrderStates.Active || execMsg.OrderState == OrderStates.Done)
                {
                    placedOrder = execMsg;
                    orderComplete.TrySetResult(execMsg);
                }
                else if (execMsg.OrderState == OrderStates.Failed)
                {
                    Console.WriteLine($"❌ Order failed: {execMsg.Error?.Message}");
                    orderComplete.TrySetException(execMsg.Error ?? new Exception("Order failed"));
                }
            }
        }

        _adapter.NewOutMessage += HandleOrderMessage;

        try
        {
            // Place a limit order that won't execute (very low price)
            var orderMsg = new OrderRegisterMessage
            {
                TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                SecurityId = _btcUsdt,
                Side = Sides.Buy,
                OrderType = OrderTypes.Limit,
                Volume = 0.001m, // Minimum order size
                Price = 1000m,   // Very low price to avoid execution
                TimeInForce = TimeInForce.GTC
            };

            Console.WriteLine($"📝 Placing limit order: {orderMsg.Volume} BTC @ ${orderMsg.Price}");
            _adapter.SendInMessage(orderMsg);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await orderComplete.Task.WaitAsync(cts.Token);

            Console.WriteLine($"✅ Order placed: ID={result.OrderId}, State={result.OrderState}");

            // Cancel the order
            if (result.OrderId.HasValue)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));

                var cancelMsg = new OrderCancelMessage
                {
                    TransactionId = orderMsg.TransactionId,
                    SecurityId = _btcUsdt
                };

                Console.WriteLine($"❌ Cancelling order {result.OrderId}...");
                _adapter.SendInMessage(cancelMsg);

                await Task.Delay(TimeSpan.FromSeconds(2));
                Console.WriteLine("✅ Order cancellation requested");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Order operation failed (expected on testnet): {ex.Message}");
            if (ex.Message.Contains("insufficient") || ex.Message.Contains("balance"))
            {
                Console.WriteLine("   This is normal for testnet accounts with zero balance");
            }
        }
        finally
        {
            _adapter.NewOutMessage -= HandleOrderMessage;
        }
    }

    /// <summary>
    /// Monitor account updates via user data stream.
    /// </summary>
    private async Task MonitorAccountAsync()
    {
        Console.WriteLine("\n👤 Monitoring account updates...");

        if (_adapter.Key == null || _adapter.Secret == null)
        {
            Console.WriteLine("⚠️  Skipping account monitoring - no credentials configured");
            return;
        }

        var accountUpdates = 0;

        void HandleAccountUpdate(Message message)
        {
            if (message is PositionChangeMessage posMsg)
            {
                accountUpdates++;
                if (accountUpdates <= 3) // Show first few updates
                {
                    Console.WriteLine($"  💼 Account update for {posMsg.SecurityId.SecurityCode}");
                    if (posMsg.Changes != null)
                    {
                        foreach (var change in posMsg.Changes.Take(3))
                        {
                            Console.WriteLine($"    {change.Key}: {change.Value}");
                        }
                    }
                }
            }
        }

        _adapter.NewOutMessage += HandleAccountUpdate;

        try
        {
            Console.WriteLine("📡 Listening for account updates... (5 seconds)");
            await Task.Delay(TimeSpan.FromSeconds(5));

            if (accountUpdates > 0)
            {
                Console.WriteLine($"✅ Received {accountUpdates} account updates");
            }
            else
            {
                Console.WriteLine("ℹ️  No account updates received (normal for inactive testnet account)");
            }
        }
        finally
        {
            _adapter.NewOutMessage -= HandleAccountUpdate;
        }
    }

    /// <summary>
    /// Handle incoming messages from the adapter.
    /// </summary>
    private void OnMessageReceived(Message message)
    {
        // This is where you would handle all messages in a real application
        // For this sample, we handle messages in specific methods above
    }

    /// <summary>
    /// Clean up resources.
    /// </summary>
    private async Task CleanupAsync()
    {
        Console.WriteLine("\n🧹 Cleaning up...");

        try
        {
            if (_adapter != null)
            {
                // Unsubscribe from market data
                _adapter.SendInMessage(new MarketDataMessage
                {
                    TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                    SecurityId = _btcUsdt,
                    DataType = DataType.Ticks,
                    IsSubscribe = false
                });

                _adapter.SendInMessage(new MarketDataMessage
                {
                    TransactionId = _adapter.TransactionIdGenerator.GetNextId(),
                    SecurityId = _btcUsdt,
                    DataType = DataType.MarketDepth,
                    IsSubscribe = false
                });

                // Disconnect
                _adapter.SendInMessage(new DisconnectMessage());

                await Task.Delay(TimeSpan.FromSeconds(1));

                _adapter.Key?.Dispose();
                _adapter.Secret?.Dispose();
                _adapter.Dispose();
            }

            Console.WriteLine("✅ Cleanup completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Cleanup error: {ex.Message}");
        }
    }
}

/// <summary>
/// Entry point for the sample application.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("StockSharp Binance Spot Connector Sample");
        Console.WriteLine("=========================================");

        var sample = new BinanceSpotSample();
        await sample.RunAsync();

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}