using System;

namespace XUnitAssured.Http.TokenManagement;

/// <summary>
/// Represents a cached token entry with expiration information.
/// </summary>
public class TokenCacheEntry
{
	/// <summary>
	/// The access token value.
	/// </summary>
	public string AccessToken { get; set; } = string.Empty;

	/// <summary>
	/// The refresh token (if available).
	/// </summary>
	public string? RefreshToken { get; set; }

	/// <summary>
	/// When the token expires (UTC).
	/// </summary>
	public DateTimeOffset ExpiresAt { get; set; }

	/// <summary>
	/// When the token was cached (UTC).
	/// </summary>
	public DateTimeOffset CachedAt { get; set; }

	/// <summary>
	/// Checks if the token is still valid considering a buffer time.
	/// </summary>
	/// <param name="bufferSeconds">Buffer time in seconds before expiration</param>
	/// <returns>True if token is valid, false otherwise</returns>
	public bool IsValid(int bufferSeconds = 300)
	{
		return DateTimeOffset.UtcNow.AddSeconds(bufferSeconds) < ExpiresAt;
	}

	/// <summary>
	/// Creates a new token cache entry with default expiration (1 hour).
	/// </summary>
	public static TokenCacheEntry Create(string accessToken, int expiresInSeconds = 3600)
	{
		return new TokenCacheEntry
		{
			AccessToken = accessToken,
			ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds),
			CachedAt = DateTimeOffset.UtcNow
		};
	}
}
