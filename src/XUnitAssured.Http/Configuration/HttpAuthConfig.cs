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

	// Specific authentication configurations. Exactly one is read, chosen by Type;
	// the others are ignored, which lets a settings file keep several variants
	// side by side and switch between them by changing Type alone.

	/// <summary>
	/// Username and password sent as an HTTP Basic <c>Authorization</c> header.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.Basic"/>.
	/// </summary>
	public BasicAuthConfig? Basic { get; set; }

	/// <summary>
	/// A fixed token sent as <c>Authorization: Bearer …</c> (the prefix is configurable).
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.Bearer"/>.
	/// </summary>
	public BearerAuthConfig? Bearer { get; set; }

	/// <summary>
	/// A bearer token obtained from <see cref="TokenEndpoint"/> and refreshed before it
	/// expires, so long test runs do not fail on a stale token.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.BearerWithAutoRefresh"/>.
	/// </summary>
	public BearerWithAutoRefreshConfig? BearerWithAutoRefresh { get; set; }

	/// <summary>
	/// A key sent in a header or a query-string parameter, as the API expects.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.ApiKey"/>.
	/// </summary>
	public ApiKeyAuthConfig? ApiKey { get; set; }

	/// <summary>
	/// OAuth 2.0 client credentials, password or authorization-code flow; the access
	/// token is requested from the token endpoint and cached per <see cref="TokenCache"/>.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.OAuth2"/>.
	/// </summary>
	public OAuth2Config? OAuth2 { get; set; }

	/// <summary>
	/// Arbitrary headers added to every request, for APIs with a bespoke scheme
	/// such as <c>X-Auth-Token</c> plus <c>X-User-Id</c>.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.CustomHeader"/>.
	/// </summary>
	public CustomHeaderAuthConfig? CustomHeader { get; set; }

	/// <summary>
	/// A client certificate for mutual TLS, loaded from a file or a certificate store.
	/// Read when <see cref="Type"/> is <see cref="AuthenticationType.Certificate"/>.
	/// </summary>
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
