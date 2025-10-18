using System.Collections.Concurrent;

namespace StockSharp.Customization.CTrader.Native;

using System.Reactive.Linq;
using OpenAPI.Net;
using OpenAPI.Net.Helpers;
using Google.Protobuf;

/// <summary>
/// cTrader OpenAPI client wrapper for StockSharp integration.
/// Core event-driven implementation with native OpenAPI event handlers.
/// Async methods are in CTraderClient.Async.cs partial file.
/// </summary>
partial class CTraderClient : BaseLogReceiver
{
	private readonly string _applicationId;
	private readonly string _applicationSecret;
	private readonly string _host;
	private readonly int _port;

	private OpenClient _client;
	private bool _isAuthenticated;
	private long _clientMsgIdCounter;

	public event Action<ConnectionStates> StateChanged;
	public event Action<Exception> Error;

	// Market data events
	public event Action<ProtoOASpotEvent> NewSpot;
	public event Action<ProtoOADepthEvent> NewDepth;
	public event Action<ProtoOAGetTrendbarsRes> NewCandle;

	// Transaction events
	public event Action<ProtoOAExecutionEvent> ExecutionEvent;
	public event Action<ProtoOAOrderErrorEvent> OrderError;

	// Account events
	public event Action<ProtoOAGetAccountListByAccessTokenRes> AccountsReceived;
	public event Action<ProtoOASymbolsListRes> SymbolsReceived;

	// Token refresh event
	public event Action<ProtoOARefreshTokenRes> TokenRefreshed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CTraderClient"/>.
	/// </summary>
	public CTraderClient(string applicationId, string applicationSecret, string host, int port)
	{
		_applicationId = applicationId ?? throw new ArgumentNullException(nameof(applicationId));
		_applicationSecret = applicationSecret ?? throw new ArgumentNullException(nameof(applicationSecret));
		_host = host ?? throw new ArgumentNullException(nameof(host));
		_port = port;
	}

	/// <summary>
	/// Connects to cTrader server.
	/// </summary>
	public async ValueTask Connect(CancellationToken cancellationToken, bool useWebSocket = false)
	{
		try
		{
			this.AddInfoLog("Connecting to cTrader OpenAPI at {0}:{1}", _host, _port);

			_client = new OpenClient(_host, _port, TimeSpan.FromSeconds(30), useWebSocket: useWebSocket);

			// Subscribe to all messages with unified error handler (exclude heartbeat for cleaner logging)
			_client.Where(iMessage => iMessage is not ProtoHeartbeatEvent).Subscribe(OnMessageReceived, OnClientError);
			_client.OfType<ProtoMessage>()
				.Where(pm => pm.HasClientMsgId)
				.Subscribe(OnProtoMessageReceived);
			
			// Subscribe to specific message types for routing
			_client.OfType<ProtoOASpotEvent>().Subscribe(OnSpotEvent);
			_client.OfType<ProtoOADepthEvent>().Subscribe(OnDepthQuotes);
			_client.OfType<ProtoOAGetTrendbarsRes>().Subscribe(OnTrendbar);
			_client.OfType<ProtoOAExecutionEvent>().Subscribe(OnExecution);
			_client.OfType<ProtoOAOrderErrorEvent>().Subscribe(OnOrderErrorEvent);
			await _client.Connect();

			this.AddInfoLog("[CTraderClient.Connect] Connected to cTrader OpenAPI.");
		}
		catch (Exception ex)
		{
			this.AddErrorLog("Failed to connect: {0}", ex);
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	/// Disconnects from cTrader server.
	/// </summary>
	public void Disconnect()
	{
		this.AddInfoLog("Disconnecting from cTrader");

		if (_client != null)
		{
			_client.Dispose();
			_client = null;
		}

		_isAuthenticated = false;
		StateChanged?.Invoke(ConnectionStates.Disconnected);
	}

	/// <summary>
	/// Generates a unique client message ID for request tracking.
	/// </summary>
	private string GenerateClientMsgId()
	{
		return $"msg-{Interlocked.Increment(ref _clientMsgIdCounter)}";
	}

	#region Event Handlers
	
	private void OnMessageReceived(IMessage message)
	{
		this.AddDebugLog("Message received: {0}", message.GetType().Name);
	}

	private void OnClientError(Exception ex)
	{
		this.AddErrorLog("OpenAPI client error: {0}", ex);
		Error?.Invoke(ex);
	}

	private void OnSpotEvent(ProtoOASpotEvent spotEvent)
	{
		NewSpot?.Invoke(spotEvent);
	}

	private void OnDepthQuotes(ProtoOADepthEvent depthQuotes)
	{
		NewDepth?.Invoke(depthQuotes);
	}

	private void OnTrendbar(ProtoOAGetTrendbarsRes trendbar)
	{
		NewCandle?.Invoke(trendbar);
	}

	private void OnExecution(ProtoOAExecutionEvent executionEvent)
	{
		ExecutionEvent?.Invoke(executionEvent);
	}

	private void OnOrderErrorEvent(ProtoOAOrderErrorEvent orderError)
	{
		OrderError?.Invoke(orderError);
	}

	#endregion

	protected override void DisposeManaged()
	{
		Disconnect();
		base.DisposeManaged();
	}

	// to get readable name after obfuscation
	public override string Name => nameof(CTrader) + "_" + nameof(CTraderClient);
}
