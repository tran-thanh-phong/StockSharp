namespace StockSharp.CTraderConnector;

using System.ComponentModel.DataAnnotations;
using System.Security;

using Ecng.ComponentModel;

/// <summary>
/// The message adapter for cTrader.
/// </summary>
[MediaIcon(Media.MediaNames.ctrader)]
public partial class CTraderMessageAdapter
{
	/// <summary>
	/// Default value for <see cref="MessageAdapter.HeartbeatInterval"/>.
	/// </summary>
	public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Application ID for cTrader OAuth2 authentication.
	/// </summary>
	[Display(
		Name = "Application ID",
		Description = "cTrader application ID for OAuth2 authentication",
		GroupName = "Connection",
		Order = 0)]
	public string ApplicationId { get; set; }

	/// <summary>
	/// Application secret for cTrader OAuth2 authentication.
	/// </summary>
	[Display(
		Name = "Application Secret",
		Description = "cTrader application secret for OAuth2 authentication",
		GroupName = "Connection",
		Order = 1)]
	public SecureString ApplicationSecret { get; set; }

	/// <summary>
	/// OAuth2 access token for account authorization.
	/// </summary>
	[Display(
		Name = "Access Token",
		Description = "OAuth2 access token for account authorization (optional, for demo purposes)",
		GroupName = "Connection",
		Order = 2)]
	public SecureString AccessToken { get; set; }

	/// <summary>
	/// OAuth2 refresh token for renewing access token.
	/// </summary>
	[Display(
		Name = "Refresh Token",
		Description = "OAuth2 refresh token for renewing access token (optional, for demo purposes)",
		GroupName = "Connection",
		Order = 3)]
	public SecureString RefreshToken { get; set; }

	/// <summary>
	/// Account ID for trading operations.
	/// </summary>
	[Display(
		Name = "Account ID",
		Description = "Account ID for trading operations",
		GroupName = "Connection",
		Order = 4)]
	public long AccountId { get; set; }

	/// <summary>
	/// Server host for connection.
	/// </summary>
	[Display(
		Name = "Host",
		Description = "Server host address",
		GroupName = "Connection",
		Order = 5)]
	public string Host { get; set; } = "demo.ctraderapi.com";

	/// <summary>
	/// Server port for connection.
	/// </summary>
	[Display(
		Name = "Port",
		Description = "Server port",
		GroupName = "Connection",
		Order = 6)]
	public int Port { get; set; } = 5035;

	private CTraderEnvironment _environment = CTraderEnvironment.Demo;

	/// <summary>
	/// cTrader environment (Demo/Live).
	/// </summary>
	[Display(
		Name = "Environment",
		Description = "Trading environment (Demo or Live)",
		GroupName = "Connection",
		Order = 7)]
	public CTraderEnvironment Environment
	{
		get => _environment;
		set
		{
			_environment = value;

			// Update host based on environment
			if (value == CTraderEnvironment.Demo && Host == "live.ctraderapi.com")
				Host = "demo.ctraderapi.com";
			else if (value == CTraderEnvironment.Live && Host == "demo.ctraderapi.com")
				Host = "live.ctraderapi.com";
		}
	}

	/// <inheritdoc />
	public override void Save(SettingsStorage storage)
	{
		base.Save(storage);

		storage.SetValue(nameof(ApplicationId), ApplicationId);
		storage.SetValue(nameof(ApplicationSecret), ApplicationSecret);
		storage.SetValue(nameof(AccessToken), AccessToken);
		storage.SetValue(nameof(RefreshToken), RefreshToken);
		storage.SetValue(nameof(AccountId), AccountId);
		storage.SetValue(nameof(Host), Host);
		storage.SetValue(nameof(Port), Port);
		storage.SetValue(nameof(Environment), Environment);
	}

	/// <inheritdoc />
	public override void Load(SettingsStorage storage)
	{
		base.Load(storage);

		ApplicationId = storage.GetValue<string>(nameof(ApplicationId));
		ApplicationSecret = storage.GetValue<SecureString>(nameof(ApplicationSecret));
		AccessToken = storage.GetValue<SecureString>(nameof(AccessToken));
		RefreshToken = storage.GetValue<SecureString>(nameof(RefreshToken));
		AccountId = storage.GetValue<long>(nameof(AccountId));
		Host = storage.GetValue<string>(nameof(Host));
		Port = storage.GetValue<int>(nameof(Port));
		Environment = storage.GetValue<CTraderEnvironment>(nameof(Environment));
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return base.ToString() + ": " + ApplicationId + "@" + Environment;
	}
}

/// <summary>
/// cTrader environment enumeration.
/// </summary>
public enum CTraderEnvironment
{
	/// <summary>
	/// Demo environment.
	/// </summary>
	Demo,

	/// <summary>
	/// Live environment.
	/// </summary>
	Live
}
