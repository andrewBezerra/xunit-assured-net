namespace XUnitAssured.Http.TokenManagement;

/// <summary>
/// Interface for token caching implementations.
/// Supports different storage backends (memory, file, etc.).
/// </summary>
public interface ITokenCache
{
	/// <summary>
	/// Gets a cached token by key.
	/// </summary>
	/// <param name="key">Cache key</param>
	/// <returns>Cached token entry if found and valid, null otherwise</returns>
	TokenCacheEntry? Get(string key);

	/// <summary>
	/// Saves a token to cache.
	/// </summary>
	/// <param name="key">Cache key</param>
	/// <param name="entry">Token entry to cache</param>
	void Set(string key, TokenCacheEntry entry);

	/// <summary>
	/// Removes a token from cache.
	/// </summary>
	/// <param name="key">Cache key</param>
	void Remove(string key);

	/// <summary>
	/// Clears all cached tokens.
	/// </summary>
	void Clear();

	/// <summary>
	/// Gets all cache keys.
	/// Useful for debugging and cache management.
	/// </summary>
	/// <returns>Collection of all cache keys</returns>
	System.Collections.Generic.IEnumerable<string> GetAllKeys();
}
