namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Storage types for token cache.
/// </summary>
public enum TokenCacheStorageType
{
	/// <summary>
	/// In-memory cache (default, lost on application restart).
	/// </summary>
	Memory = 0,

	/// <summary>
	/// File-based cache (persists across application restarts).
	/// </summary>
	File = 1

	// Future: Redis, Database, etc.
}
