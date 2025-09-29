using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.CTrader;

/// <summary>
/// Configuration manager for cTrader connector settings.
/// Phase 3.5 implementation - T023: Add configuration validation and persistence.
/// </summary>
public static class CTraderConfigurationManager
{
    private const string ConfigurationFileName = "ctrader_config.json";
    private const string DefaultConfigurationPath = "Configuration";

    /// <summary>
    /// Saves the configuration to a file.
    /// </summary>
    /// <param name="configuration">The configuration to save.</param>
    /// <param name="filePath">Optional custom file path. If null, uses default location.</param>
    /// <returns>True if saved successfully, false otherwise.</returns>
    public static bool SaveConfiguration(CTraderConfiguration configuration, string filePath = null)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        try
        {
            var path = filePath ?? GetDefaultConfigurationPath();
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create a serializable version of the configuration
            var serializableConfig = new SerializableConfiguration
            {
                ApplicationId = configuration.ApplicationId,
                ApplicationSecretEncrypted = EncryptSecureString(configuration.ApplicationSecret),
                Environment = configuration.Environment,
                Host = configuration.Host,
                Port = configuration.Port,
                UseSSL = configuration.UseSSL,
                AccountId = configuration.AccountId,
                HeartbeatIntervalSeconds = configuration.HeartbeatInterval.TotalSeconds,
                ConnectionTimeoutSeconds = configuration.ConnectionTimeout.TotalSeconds,
                RequestTimeoutSeconds = configuration.RequestTimeout.TotalSeconds,
                MaxRetryAttempts = configuration.MaxRetryAttempts,
                EnableLogging = configuration.EnableLogging,
                EnableDebugLogging = configuration.EnableDebugLogging,
                SavedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(serializableConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception)
        {
            // Log the exception in a real implementation
            return false;
        }
    }

    /// <summary>
    /// Loads the configuration from a file.
    /// </summary>
    /// <param name="filePath">Optional custom file path. If null, uses default location.</param>
    /// <returns>The loaded configuration, or a default configuration if loading fails.</returns>
    public static CTraderConfiguration LoadConfiguration(string filePath = null)
    {
        try
        {
            var path = filePath ?? GetDefaultConfigurationPath();

            if (!File.Exists(path))
            {
                return new CTraderConfiguration();
            }

            var json = File.ReadAllText(path);
            var serializableConfig = JsonSerializer.Deserialize<SerializableConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (serializableConfig == null)
            {
                return new CTraderConfiguration();
            }

            // Convert back to the main configuration
            var configuration = new CTraderConfiguration
            {
                ApplicationId = serializableConfig.ApplicationId ?? string.Empty,
                ApplicationSecret = DecryptToSecureString(serializableConfig.ApplicationSecretEncrypted),
                Environment = serializableConfig.Environment,
                Host = serializableConfig.Host ?? "demo.ctraderapi.com",
                Port = serializableConfig.Port,
                UseSSL = serializableConfig.UseSSL,
                AccountId = serializableConfig.AccountId,
                HeartbeatInterval = TimeSpan.FromSeconds(serializableConfig.HeartbeatIntervalSeconds),
                ConnectionTimeout = TimeSpan.FromSeconds(serializableConfig.ConnectionTimeoutSeconds),
                RequestTimeout = TimeSpan.FromSeconds(serializableConfig.RequestTimeoutSeconds),
                MaxRetryAttempts = serializableConfig.MaxRetryAttempts,
                EnableLogging = serializableConfig.EnableLogging,
                EnableDebugLogging = serializableConfig.EnableDebugLogging
            };

            return configuration;
        }
        catch (Exception)
        {
            // Log the exception in a real implementation
            return new CTraderConfiguration();
        }
    }

    /// <summary>
    /// Deletes the configuration file.
    /// </summary>
    /// <param name="filePath">Optional custom file path. If null, uses default location.</param>
    /// <returns>True if deleted successfully, false otherwise.</returns>
    public static bool DeleteConfiguration(string filePath = null)
    {
        try
        {
            var path = filePath ?? GetDefaultConfigurationPath();

            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a configuration file exists.
    /// </summary>
    /// <param name="filePath">Optional custom file path. If null, uses default location.</param>
    /// <returns>True if the configuration file exists, false otherwise.</returns>
    public static bool ConfigurationExists(string filePath = null)
    {
        var path = filePath ?? GetDefaultConfigurationPath();
        return File.Exists(path);
    }

    /// <summary>
    /// Validates a configuration and returns detailed validation results.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>Validation result with details.</returns>
    public static ConfigurationValidationResult ValidateConfiguration(CTraderConfiguration configuration)
    {
        if (configuration == null)
        {
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Configuration cannot be null." }
            };
        }

        return configuration.ValidateConfiguration();
    }

    /// <summary>
    /// Creates a backup of the current configuration.
    /// </summary>
    /// <param name="configuration">The configuration to backup.</param>
    /// <param name="backupPath">Optional backup path. If null, creates a timestamped backup.</param>
    /// <returns>The path of the created backup file, or null if backup failed.</returns>
    public static string BackupConfiguration(CTraderConfiguration configuration, string backupPath = null)
    {
        if (configuration == null)
            return null;

        try
        {
            var path = backupPath ?? GetBackupConfigurationPath();
            return SaveConfiguration(configuration, path) ? path : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Restores configuration from a backup file.
    /// </summary>
    /// <param name="backupPath">The path to the backup file.</param>
    /// <returns>The restored configuration, or null if restoration failed.</returns>
    public static CTraderConfiguration RestoreConfiguration(string backupPath)
    {
        if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            return null;

        return LoadConfiguration(backupPath);
    }

    /// <summary>
    /// Gets the default configuration file path.
    /// </summary>
    /// <returns>The default configuration file path.</returns>
    private static string GetDefaultConfigurationPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var stockSharpPath = Path.Combine(appDataPath, "StockSharp");
        return Path.Combine(stockSharpPath, DefaultConfigurationPath, ConfigurationFileName);
    }

    /// <summary>
    /// Gets a timestamped backup configuration file path.
    /// </summary>
    /// <returns>The backup configuration file path.</returns>
    private static string GetBackupConfigurationPath()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"ctrader_config_backup_{timestamp}.json";
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var stockSharpPath = Path.Combine(appDataPath, "StockSharp");
        var backupsPath = Path.Combine(stockSharpPath, DefaultConfigurationPath, "Backups");
        return Path.Combine(backupsPath, backupFileName);
    }

    /// <summary>
    /// Encrypts a SecureString for storage (simplified implementation).
    /// In a production environment, use proper encryption with machine-specific keys.
    /// </summary>
    /// <param name="secureString">The SecureString to encrypt.</param>
    /// <returns>Encrypted string representation.</returns>
    private static string EncryptSecureString(SecureString secureString)
    {
        if (secureString == null || secureString.Length == 0)
            return string.Empty;

        try
        {
            // TODO: Implement proper encryption in production
            // This is a simplified implementation for Phase 3.5
            var plainText = secureString.ToInsecureString();
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));
            return encoded;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts an encrypted string back to a SecureString.
    /// </summary>
    /// <param name="encryptedString">The encrypted string.</param>
    /// <returns>Decrypted SecureString.</returns>
    private static SecureString DecryptToSecureString(string encryptedString)
    {
        if (string.IsNullOrEmpty(encryptedString))
            return new SecureString();

        try
        {
            // TODO: Implement proper decryption in production
            // This is a simplified implementation for Phase 3.5
            var decodedBytes = Convert.FromBase64String(encryptedString);
            var plainText = System.Text.Encoding.UTF8.GetString(decodedBytes);

            var secureString = new SecureString();
            foreach (char c in plainText)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }
        catch (Exception)
        {
            return new SecureString();
        }
    }
}

/// <summary>
/// Serializable version of the configuration for JSON persistence.
/// </summary>
internal class SerializableConfiguration
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationSecretEncrypted { get; set; } = string.Empty;
    public CTraderEnvironment Environment { get; set; } = CTraderEnvironment.Demo;
    public string Host { get; set; } = "demo.ctraderapi.com";
    public int Port { get; set; } = 5035;
    public bool UseSSL { get; set; } = true;
    public long AccountId { get; set; }
    public double HeartbeatIntervalSeconds { get; set; } = 30;
    public double ConnectionTimeoutSeconds { get; set; } = 30;
    public double RequestTimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableLogging { get; set; } = true;
    public bool EnableDebugLogging { get; set; } = false;
    public DateTime SavedAt { get; set; }
}