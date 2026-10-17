using System;
using System.Text;
using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for HTTP Basic Authentication.
/// Encodes username:password in Base64 and adds Authorization header.
/// </summary>
public class BasicAuthHandler : IAuthenticationHandler
{
	private readonly BasicAuthConfig _config;

	/// <summary>
	/// Creates a new BasicAuthHandler with the specified configuration.
	/// </summary>
	/// <param name="config">Basic authentication configuration</param>
	public BasicAuthHandler(BasicAuthConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.Basic;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		// Uma credencial vazia vai como está: mandá-la e ver a API recusar é um teste que se quer
		// escrever. O caminho do HttpClient próprio sempre a mandou, e os exemplos contam com isso.
		// Encode username:password in Base64
		var credentials = $"{_config.Username}:{_config.Password}";
		var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

		// Add Authorization header
		request.WithHeader("Authorization", $"Basic {base64Credentials}");
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.Basic && authConfig.Basic != null;
	}
}
