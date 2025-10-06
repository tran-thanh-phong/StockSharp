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
	/// cTrader environment (Demo/Live).
	/// </summary>
	[Display(
		Name = "Environment",
		Description = "Trading environment (Demo or Live)",
		GroupName = "Connection",
		Order = 4)]
	public CTraderEnvironment Environment { get; set; } = CTraderEnvironment.Demo;

	/// <summary>
	/// Account ID for trading operations (auto-populated from access token).
	/// </summary>
	public long AccountId { get; set; }

	/// <inheritdoc />
	public override void Save(SettingsStorage storage)
	{
		base.Save(storage);

		storage.SetValue(nameof(ApplicationId), ApplicationId);
		storage.SetValue(nameof(ApplicationSecret), ApplicationSecret);
		storage.SetValue(nameof(AccessToken), AccessToken);
		storage.SetValue(nameof(RefreshToken), RefreshToken);
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
		Environment = storage.GetValue<CTraderEnvironment>(nameof(Environment));
	}

	/// <inheritdoc />
	public override string ToString()
	{
		var result = base.ToString() + ": " + ApplicationId + "@" + Environment;
		if (AccountId > 0)
			result += $" (Account: {AccountId})";
		return result;
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
