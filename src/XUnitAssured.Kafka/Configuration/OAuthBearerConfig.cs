namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// SASL/OAUTHBEARER with OpenID Connect: the client credentials the Kafka client exchanges
/// for a token at <see cref="TokenEndpointUrl"/>, and presents to the broker (#179).
/// </summary>
/// <remarks>
/// librdkafka asks for the token and renews it by itself (<c>sasl.oauthbearer.method=oidc</c>).
/// The broker validates it against the identity provider's keys.
/// </remarks>
public class OAuthBearerConfig
{
	/// <summary>
	/// The OAuth 2.0 token endpoint, e.g.
	/// <c>http://localhost:8080/realms/kafka/protocol/openid-connect/token</c> for Keycloak.
	/// </summary>
	public string TokenEndpointUrl { get; set; } = string.Empty;

	/// <summary>The OAuth client id.</summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>The OAuth client secret.</summary>
	public string ClientSecret { get; set; } = string.Empty;

	/// <summary>The scopes to ask for, separated by spaces. Optional.</summary>
	public string? Scope { get; set; }

	/// <summary>
	/// Use SSL/TLS to the broker (SASL_SSL instead of SASL_PLAINTEXT). Default: true.
	/// </summary>
	public bool UseSsl { get; set; } = true;
}
