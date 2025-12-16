namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Base configuration for HTTP authentication.
/// </summary>
public class HttpAuthConfig
{
	/// <summary>
	/// Type of authentication to use.
	/// Default: None
	/// </summary>
	public AuthenticationType Type { get; set; } = AuthenticationType.None;

	/// <summary>
	/// Token endpoint URL (for BearerWithAutoRefresh and OAuth2).
	/// </summary>
	public string? TokenEndpoint { get; set; }

	/// <summary>
	/// Configuration for token request.
	/// </summary>
	public TokenRequestConfig TokenRequest { get; set; } = new();

	/// <summary>
	/// Configuration for token caching.
	/// </summary>
	public TokenCacheConfig TokenCache { get; set; } = new();

	/// <summary>
	/// Configuration for token extraction from response.
	/// </summary>
	public TokenExtractionConfig TokenExtraction { get; set; } = new();

	// Specific authentication configurations
	public BasicAuthConfig? Basic { get; set; }
	public BearerAuthConfig? Bearer { get; set; }
	public BearerWithAutoRefreshConfig? BearerWithAutoRefresh { get; set; }
	public ApiKeyAuthConfig? ApiKey { get; set; }
	public OAuth2Config? OAuth2 { get; set; }
	public CustomHeaderAuthConfig? CustomHeader { get; set; }
	public CertificateAuthConfig? Certificate { get; set; }

	/// <summary>
	/// Configures basic authentication.
	/// </summary>
	public void UseBasicAuth(string username, string password)
	{
		Type = AuthenticationType.Basic;
		Basic = new BasicAuthConfig
		{
			Username = username,
			Password = password
		};
	}

	/// <summary>
	/// Configures bearer token authentication.
	/// </summary>
	public void UseBearerToken(string token)
	{
		Type = AuthenticationType.Bearer;
		Bearer = new BearerAuthConfig
		{
			Token = token
		};
	}

	/// <summary>
	/// Configures bearer token with automatic refresh.
	/// </summary>
	public void UseBearerWithAutoRefresh(string tokenEndpoint)
	{
		Type = AuthenticationType.BearerWithAutoRefresh;
		TokenEndpoint = tokenEndpoint;
		BearerWithAutoRefresh = new BearerWithAutoRefreshConfig
		{
			TokenEndpoint = tokenEndpoint
		};
	}

	/// <summary>
	/// Configures API Key authentication.
	/// </summary>
	/// <param name="keyName">Name of the header or query parameter</param>
	/// <param name="keyValue">API key value</param>
	/// <param name="location">Where to send the key (Header or Query)</param>
	public void UseApiKey(string keyName, string keyValue, ApiKeyLocation location = ApiKeyLocation.Header)
	{
		Type = AuthenticationType.ApiKey;
		ApiKey = new ApiKeyAuthConfig
		{
			KeyName = keyName,
			KeyValue = keyValue,
			Location = location
		};
	}

	/// <summary>
	/// Configures OAuth 2.0 authentication.
	/// </summary>
	/// <param name="tokenUrl">Token endpoint URL</param>
	/// <param name="clientId">Client ID</param>
	/// <param name="clientSecret">Client Secret</param>
	/// <param name="grantType">Grant type (default: ClientCredentials)</param>
	public void UseOAuth2(string tokenUrl, string clientId, string clientSecret, OAuth2GrantType grantType = OAuth2GrantType.ClientCredentials)
	{
		Type = AuthenticationType.OAuth2;
		OAuth2 = new OAuth2Config
		{
			TokenUrl = tokenUrl,
			ClientId = clientId,
			ClientSecret = clientSecret,
			GrantType = grantType
		};
	}

	/// <summary>
	/// Configures custom header authentication.
	/// </summary>
	/// <param name="headerName">Header name</param>
	/// <param name="headerValue">Header value</param>
	public void UseCustomHeader(string headerName, string headerValue)
	{
		Type = AuthenticationType.CustomHeader;
		CustomHeader = new CustomHeaderAuthConfig();
		CustomHeader.AddHeader(headerName, headerValue);
	}

	/// <summary>
	/// Configures certificate-based authentication from file.
	/// </summary>
	/// <param name="certificatePath">Path to certificate file</param>
	/// <param name="password">Certificate password (optional)</param>
	public void UseCertificate(string certificatePath, string? password = null)
	{
		Type = AuthenticationType.Certificate;
		Certificate = new CertificateAuthConfig
		{
			CertificatePath = certificatePath,
			CertificatePassword = password
		};
	}

	/// <summary>
	/// Configures certificate-based authentication from Windows certificate store.
	/// </summary>
	/// <param name="thumbprint">Certificate thumbprint</param>
	/// <param name="storeLocation">Store location (default: CurrentUser)</param>
	/// <param name="storeName">Store name (default: My)</param>
	public void UseCertificateFromStore(string thumbprint, System.Security.Cryptography.X509Certificates.StoreLocation storeLocation = System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser, System.Security.Cryptography.X509Certificates.StoreName storeName = System.Security.Cryptography.X509Certificates.StoreName.My)
	{
		Type = AuthenticationType.Certificate;
		Certificate = new CertificateAuthConfig
		{
			Thumbprint = thumbprint,
			StoreLocation = storeLocation,
			StoreName = storeName
		};
	}
}
