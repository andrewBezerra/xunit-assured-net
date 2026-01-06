using SampleWebApi;
using SampleWebApi.Models;
using SampleWebApi.Authentication;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Text;
using static SampleWebApi.HandlerTest;

namespace SampleWebApi;

public static class RouteMappingExtensions
{
	public static WebApplication MapTestApplication(this WebApplication app)
	{
		app.MapPost();
		app.MapAuthEndpoints();
		return app;
	}

	public static WebApplication MapAuthEndpoints(this WebApplication app)
	{
		var authGroup = app.MapGroup("api/auth")
			.WithTags("Authentication")
			.WithOpenApi();

		// Basic Authentication
		authGroup.MapGet("basic", BasicAuth)
			.RequireAuthorization("BasicAuthPolicy")
			.WithOpenApi(operation =>
			{
				operation.Summary = "Basic Authentication Test";
				operation.Description = "Tests Basic Authentication. Use Authorization header with Basic scheme.";
				operation.Security = new List<OpenApiSecurityRequirement>
				{
					new OpenApiSecurityRequirement
					{
						[new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Basic" } }] = Array.Empty<string>()
					}
				};
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// Bearer Token Authentication
		authGroup.MapGet("bearer", BearerAuth)
			.WithOpenApi(operation =>
			{
				operation.Summary = "Bearer Token Authentication Test";
				operation.Description = "Tests Bearer Token Authentication. Use Authorization header with Bearer scheme.";
				operation.Security = new List<OpenApiSecurityRequirement>
				{
					new OpenApiSecurityRequirement
					{
						[new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
					}
				};
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// API Key Header Authentication
		authGroup.MapGet("apikey-header", ApiKeyHeaderAuth)
			.RequireAuthorization("ApiKeyPolicy")
			.WithOpenApi(operation =>
			{
				operation.Summary = "API Key (Header) Authentication Test";
				operation.Description = "Tests API Key Authentication via header. Use X-API-Key header.";
				operation.Security = new List<OpenApiSecurityRequirement>
				{
					new OpenApiSecurityRequirement
					{
						[new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = Array.Empty<string>()
					}
				};
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// API Key Query Authentication
		authGroup.MapGet("apikey-query", ApiKeyQueryAuth)
			.RequireAuthorization("ApiKeyPolicy")
			.WithOpenApi(operation =>
			{
				operation.Summary = "API Key (Query) Authentication Test";
				operation.Description = "Tests API Key Authentication via query parameter. Use ?api_key={key}";
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// Custom Header Authentication
		authGroup.MapGet("custom-header", CustomHeaderAuth)
			.RequireAuthorization("CustomHeaderPolicy")
			.WithOpenApi(operation =>
			{
				operation.Summary = "Custom Header Authentication Test";
				operation.Description = "Tests Custom Header Authentication. Requires X-Custom-Auth-Token and X-Custom-Session-Id headers.";
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// Certificate Authentication
		authGroup.MapGet("certificate", CertificateAuth)
			.AllowAnonymous() // Don't use policy here, validate inside endpoint to return 401 instead of 403
			.WithOpenApi(operation =>
			{
				operation.Summary = "Certificate (mTLS) Authentication Test";
				operation.Description = "Tests Certificate Authentication. Requires client certificate.";
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// OAuth2 Token Endpoint
		authGroup.MapPost("oauth2/token", OAuth2Token)
			.AllowAnonymous()
			.WithOpenApi(operation =>
			{
				operation.Summary = "OAuth2 Token Endpoint";
				operation.Description = "Generates OAuth2 access token for client credentials flow.";
				operation.Security = new List<OpenApiSecurityRequirement>
				{
					new OpenApiSecurityRequirement
					{
						[new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "OAuth2" } }] = new[] { "read", "write" }
					}
				};
				return operation;
			})
			.Accepts<OAuth2TokenRequest>("application/x-www-form-urlencoded")
			.Produces<OAuth2TokenResponse>(200)
			.Produces(400)
			.Produces(401);

		// OAuth2 Protected Endpoint
		authGroup.MapGet("oauth2/protected", OAuth2Protected)
			.WithOpenApi(operation =>
			{
				operation.Summary = "OAuth2 Protected Endpoint";
				operation.Description = "Tests OAuth2 Bearer Token Authentication. Requires token from /oauth2/token endpoint.";
				operation.Security = new List<OpenApiSecurityRequirement>
				{
					new OpenApiSecurityRequirement
					{
						[new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
					}
				};
				return operation;
			})
			.Produces<AuthResponse>(200)
			.Produces(401);

		// Public Endpoint
		authGroup.MapGet("public", PublicEndpoint)
			.AllowAnonymous()
			.WithOpenApi(operation =>
			{
				operation.Summary = "Public Endpoint";
				operation.Description = "Public endpoint that doesn't require authentication.";
				return operation;
			})
			.Produces<AuthResponse>(200);

		return app;
	}

	// Helper methods for authentication endpoints will be added here

	/// <summary>
	/// Endpoint for testing Basic Authentication.
	/// Expected header: Authorization: Basic base64(username:password)
	/// Valid credentials: username="admin", password="secret123"
	/// </summary>
	private static IResult BasicAuth(HttpContext context, ILogger<Program> logger)
	{
		// Authentication is handled by BasicAuthenticationHandler
		// If we reach here, authentication was successful
		var username = context.User.Identity?.Name ?? "Unknown";
		
		logger.LogInformation("Basic authentication endpoint accessed by user: {Username}", username);
		
		return Results.Ok(new AuthResponse
		{
			AuthType = "Basic",
			Authenticated = true,
			Username = username,
			Message = "Basic authentication successful"
		});
	}

	/// <summary>
	/// Endpoint for testing Bearer Token Authentication.
	/// Expected header: Authorization: Bearer {token}
	/// Valid token: "my-super-secret-token-12345"
	/// </summary>
	private static IResult BearerAuth(HttpRequest request, ILogger<Program> logger)
	{
		var authHeader = request.Headers["Authorization"].ToString();

		if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
		{
			logger.LogWarning("Bearer authentication failed: Missing or invalid Authorization header");
			return Results.Json(new { message = "Bearer token required" }, statusCode: 401);
		}

		var token = authHeader.Substring("Bearer ".Length).Trim();

		// Validate token
		if (token == "my-super-secret-token-12345")
		{
			logger.LogInformation("Bearer authentication successful");
			return Results.Ok(new AuthResponse
			{
				AuthType = "Bearer",
				Authenticated = true,
				Message = "Bearer token authentication successful"
			});
		}

		logger.LogWarning("Bearer authentication failed: Invalid token");
		return Results.Json(new { message = "Invalid bearer token" }, statusCode: 401);
	}

	/// <summary>
	/// Endpoint for testing API Key Authentication (Header).
	/// Expected header: X-API-Key: {api-key}
	/// Valid API key: "api-key-header-abc123xyz"
	/// </summary>
	private static IResult ApiKeyHeaderAuth(HttpContext context, ILogger<Program> logger)
	{
		// Authentication is handled by ApiKeyAuthenticationHandler
		// If we reach here, authentication was successful
		var authType = context.User.FindFirst("AuthType")?.Value ?? "ApiKey";
		
		logger.LogInformation("API Key (Header) authentication endpoint accessed");
		
		return Results.Ok(new AuthResponse
		{
			AuthType = authType,
			Authenticated = true,
			Message = "API Key (Header) authentication successful"
		});
	}

	/// <summary>
	/// Endpoint for testing API Key Authentication (Query Parameter).
	/// Expected query parameter: ?api_key={api-key}
	/// Valid API key: "api-key-query-xyz789abc"
	/// </summary>
	private static IResult ApiKeyQueryAuth(HttpContext context, ILogger<Program> logger)
	{
		// Authentication is handled by ApiKeyAuthenticationHandler
		// If we reach here, authentication was successful
		var authType = context.User.FindFirst("AuthType")?.Value ?? "ApiKey";
		
		logger.LogInformation("API Key (Query) authentication endpoint accessed");
		
		return Results.Ok(new AuthResponse
		{
			AuthType = authType,
			Authenticated = true,
			Message = "API Key (Query) authentication successful"
		});
	}

	/// <summary>
	/// Endpoint for testing Custom Header Authentication.
	/// Expected headers: 
	///   X-Custom-Auth-Token: custom-token-12345
	///   X-Custom-Session-Id: session-abc-xyz
	/// </summary>
	private static IResult CustomHeaderAuth(HttpContext context, ILogger<Program> logger)
	{
		// Authentication is handled by CustomHeaderAuthenticationHandler
		// If we reach here, authentication was successful
		var sessionId = context.User.FindFirst("SessionId")?.Value;
		
		logger.LogInformation("Custom Header authentication endpoint accessed");
		
		return Results.Ok(new AuthResponse
		{
			AuthType = "CustomHeader",
			Authenticated = true,
			Message = "Custom Header authentication successful",
			CustomData = sessionId != null ? new Dictionary<string, string>
			{
				["SessionId"] = sessionId
			} : null
		});
	}

	/// <summary>
	/// Endpoint for testing Certificate (mTLS) Authentication.
	/// Checks for client certificate in the request.
	/// </summary>
	private static IResult CertificateAuth(HttpContext context, ILogger<Program> logger)
	{
		var clientCertificate = context.Connection.ClientCertificate;

		if (clientCertificate == null)
		{
			logger.LogWarning("Certificate authentication failed: No client certificate provided");
			return Results.Json(new { message = "Client certificate required" }, statusCode: 401);
		}

		logger.LogInformation("Certificate authentication successful. Subject: {Subject}, Thumbprint: {Thumbprint}",
			clientCertificate.Subject, clientCertificate.Thumbprint);

		return Results.Ok(new AuthResponse
		{
			AuthType = "Certificate",
			Authenticated = true,
			Message = "Certificate authentication successful",
			CustomData = new Dictionary<string, string>
			{
				["Subject"] = clientCertificate.Subject,
				["Issuer"] = clientCertificate.Issuer,
				["Thumbprint"] = clientCertificate.Thumbprint,
				["ValidFrom"] = clientCertificate.NotBefore.ToString("O"),
				["ValidTo"] = clientCertificate.NotAfter.ToString("O")
			}
		});
	}

	/// <summary>
	/// OAuth2 Token endpoint for testing OAuth2 Client Credentials flow.
	/// Returns an access token for valid client credentials.
	/// Valid credentials: client_id="test-client-id", client_secret="test-client-secret"
	/// </summary>
	private static async Task<IResult> OAuth2Token(HttpContext context, ILogger<Program> logger)
	{
		// Read form data manually
		var form = await context.Request.ReadFormAsync();
		var grantType = form["grant_type"].ToString();
		var clientId = form["client_id"].ToString();
		var clientSecret = form["client_secret"].ToString();
		var scope = form["scope"].ToString();

		logger.LogInformation("OAuth2 token request received. Grant type: {GrantType}", grantType);

		// Validate grant type
		if (grantType != "client_credentials")
		{
			logger.LogWarning("OAuth2 token request failed: Unsupported grant type: {GrantType}", grantType);
			return Results.BadRequest(new { error = "unsupported_grant_type", error_description = "Only client_credentials grant type is supported" });
		}

		// Validate client credentials
		if (clientId != "test-client-id" || clientSecret != "test-client-secret")
		{
			logger.LogWarning("OAuth2 token request failed: Invalid client credentials");
			return Results.Json(new { error = "invalid_client", error_description = "Invalid client credentials" }, statusCode: 401);
		}

		// Generate mock access token
		var accessToken = $"oauth2-access-token-{Guid.NewGuid():N}";

		logger.LogInformation("OAuth2 token generated successfully for client: {ClientId}", clientId);

		return Results.Ok(new OAuth2TokenResponse
		{
			AccessToken = accessToken,
			TokenType = "Bearer",
			ExpiresIn = 3600,
			Scope = string.IsNullOrEmpty(scope) ? "read write" : scope
		});
	}

	/// <summary>
	/// Endpoint for testing OAuth2 Bearer Token Authentication.
	/// Expected header: Authorization: Bearer {oauth2-token}
	/// Valid token prefix: "oauth2-access-token-"
	/// </summary>
	private static IResult OAuth2Protected(HttpRequest request, ILogger<Program> logger)
	{
		var authHeader = request.Headers["Authorization"].ToString();

		if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
		{
			logger.LogWarning("OAuth2 authentication failed: Missing or invalid Authorization header");
			return Results.Json(new { message = "OAuth2 Bearer token required" }, statusCode: 401);
		}

		var token = authHeader.Substring("Bearer ".Length).Trim();

		// Validate OAuth2 token (simplified validation)
		if (token.StartsWith("oauth2-access-token-"))
		{
			logger.LogInformation("OAuth2 authentication successful");
			return Results.Ok(new AuthResponse
			{
				AuthType = "OAuth2",
				Authenticated = true,
				Message = "OAuth2 authentication successful"
			});
		}

		logger.LogWarning("OAuth2 authentication failed: Invalid token");
		return Results.Json(new { message = "Invalid OAuth2 token" }, statusCode: 401);
	}

	/// <summary>
	/// Public endpoint (no authentication required).
	/// </summary>
	private static IResult PublicEndpoint(ILogger<Program> logger)
	{
		logger.LogInformation("Public endpoint accessed");
		return Results.Ok(new AuthResponse
		{
			AuthType = "None",
			Authenticated = false,
			Message = "Public endpoint - no authentication required"
		});
	}

	private static WebApplication MapPost(this WebApplication app)
	{
		app.MapPost("v1/test", async (Request request, [FromServices] IMediator mediator) =>
		{
			var result = await mediator.Send(request);

			return result switch
			{
				BadRequestResponse bad => Results.Problem(statusCode: 400, title: bad.Title, detail: bad.Detail),
				GoneResponse gone => Results.Problem(statusCode: 410, title: gone.Title, detail: gone.Detail),
				UnprocessableEntityResponse unprocessable => Results.Problem(statusCode: 422, title: unprocessable.Title, detail: unprocessable.Detail),
				FailResponse fail => Results.Problem(statusCode: 500, title: fail.Title, detail: fail.Detail),
				SuccessResponse success => Results.Ok(success),
				_ => Results.Problem("Erro desconhecido."),
			};
		})
		.WithOpenApi()
		.Produces(200, typeof(SuccessResponse))
		.ProducesValidationProblem()
		.ProducesProblem(410)
		.ProducesProblem(422)
		.ProducesProblem(500);
		return app;
	}

	public record ResponseError(string nome, string endereco);

}
