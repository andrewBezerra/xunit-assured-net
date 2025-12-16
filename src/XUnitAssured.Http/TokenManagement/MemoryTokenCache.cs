using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace XUnitAssured.Http.TokenManagement;

/// <summary>
/// In-memory implementation of ITokenCache using ConcurrentDictionary.
/// Thread-safe and suitable for most testing scenarios.
/// Note: Cache is lost when application restarts.
/// </summary>
public class MemoryTokenCache : ITokenCache
{
	private readonly ConcurrentDictionary<string, TokenCacheEntry> _cache = new();

	/// <inheritdoc />
	public TokenCacheEntry? Get(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return null;

		if (_cache.TryGetValue(key, out var entry))
		{
			// Return only if still valid
			if (entry.IsValid())
				return entry;

			// Remove expired entry
			_cache.TryRemove(key, out _);
		}

		return null;
	}

	/// <inheritdoc />
	public void Set(string key, TokenCacheEntry entry)
	{
		if (string.IsNullOrWhiteSpace(key))
			return;

		if (entry == null)
			return;

		_cache[key] = entry;
	}

	/// <inheritdoc />
	public void Remove(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return;

		_cache.TryRemove(key, out _);
	}

	/// <inheritdoc />
	public void Clear()
	{
		_cache.Clear();
	}

	/// <inheritdoc />
	public IEnumerable<string> GetAllKeys()
	{
		return _cache.Keys.ToList();
	}

	/// <summary>
	/// Gets the number of cached tokens.
	/// </summary>
	public int Count => _cache.Count;
}
