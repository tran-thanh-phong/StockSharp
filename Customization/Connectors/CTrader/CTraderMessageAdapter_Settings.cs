namespace StockSharp.Customization.CTrader;

using System.ComponentModel.DataAnnotations;
using System.Security;

using Ecng.ComponentModel;

/// <summary>
/// The message adapter for cTrader.
/// </summary>
[MediaIcon(Media.MediaNames.ctrader)]
public partial class CTraderMessageAdapter : IKeySecretAdapter, ITokenAdapter, IDemoAdapter
{
	/// <summary>
	/// Default value for <see cref="MessageAdapter.HeartbeatInterval"/>.
	/// </summary>
	public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	[Display(
		Name = "Key",
		Description = "cTrader application ID for OAuth2 authentication",
		GroupName = "Connection",
		Order = 0)]
	[BasicSetting]
	public SecureString Key { get; set; }

	/// <inheritdoc />
	[Display(
		Name = "Secret",
		Description = "cTrader application secret for OAuth2 authentication",
		GroupName = "Connection",
		Order = 1)]
	[BasicSetting]
	public SecureString Secret { get; set; }

	/// <inheritdoc />
	[Display(
		Name = "Token",
		Description = "OAuth2 access token for account authorization (optional, for demo purposes)",
		GroupName = "Connection",
		Order = 2)]
	[BasicSetting]
	public SecureString Token { get; set; }

	/// <summary>
	/// OAuth2 refresh token for renewing access token.
	/// </summary>
	[Display(
		Name = "Refresh Token",
		Description = "OAuth2 refresh token for renewing access token (optional, for demo purposes)",
		GroupName = "Connection",
		Order = 3)]
	[BasicSetting]
	public SecureString RefreshToken { get; set; }

	/// <inheritdoc />
	[Display(
		Name = "Demo",
		Description = "Connect to demo trading instead of real trading server",
		GroupName = "Connection",
		Order = 4)]
	[BasicSetting]
	public bool IsDemo { get; set; } = true;

	
	/// <inheritdoc />
	public override void Save(SettingsStorage storage)
	{
		base.Save(storage);

		storage.SetValue(nameof(Key), Key);
		storage.SetValue(nameof(Secret), Secret);
		storage.SetValue(nameof(Token), Token);
		storage.SetValue(nameof(RefreshToken), RefreshToken);
		storage.SetValue(nameof(IsDemo), IsDemo);
	}

	/// <inheritdoc />
	public override void Load(SettingsStorage storage)
	{
		base.Load(storage);

		Key = storage.GetValue<SecureString>(nameof(Key));
		Secret = storage.GetValue<SecureString>(nameof(Secret));
		Token = storage.GetValue<SecureString>(nameof(Token));
		RefreshToken = storage.GetValue<SecureString>(nameof(RefreshToken));
		IsDemo = storage.GetValue<bool>(nameof(IsDemo));
	}

	/// <inheritdoc />
	public override string ToString()
	{
		var result = base.ToString() + ": " + Key?.ToId() + "@" + (IsDemo ? "Demo" : "Live");
		return result;
	}
}

