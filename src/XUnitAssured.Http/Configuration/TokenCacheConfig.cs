namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for token caching behavior.
/// </summary>
public class TokenCacheConfig
{
	/// <summary>
	/// Enable token caching to avoid unnecessary token requests.
	/// Default: true
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Buffer time in seconds before token expiration to trigger refresh.
	/// This ensures tokens are refreshed before they expire.
	/// Default: 300 seconds (5 minutes)
	/// </summary>
	public int ExpirationBufferSeconds { get; set; } = 300;

	/// <summary>
	/// Storage type for token cache.
	/// Default: Memory
	/// </summary>
	public TokenCacheStorageType StorageType { get; set; } = TokenCacheStorageType.Memory;

	/// <summary>
	/// File path for file-based cache.
	/// Only used when StorageType = File.
	/// Default: ".xunitassured/tokencache.json"
	/// </summary>
	public string FilePath { get; set; } = ".xunitassured/tokencache.json";

	/// <summary>
	/// Cache key prefix (useful for multi-tenant scenarios).
	/// Default: "xunitassured:token:"
	/// </summary>
	public string CacheKeyPrefix { get; set; } = "xunitassured:token:";
}
