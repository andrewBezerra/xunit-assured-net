using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

namespace XUnitAssured.Remote;

/// <summary>
/// Validates RemoteSettings using the IValidateOptions pattern from .NET.
/// This validator runs at startup when ValidateOnStart() is called.
/// </summary>
public class RemoteSettingsValidator : IValidateOptions<RemoteSettings>
{
	public ValidateOptionsResult Validate(string? name, RemoteSettings options)
	{
		var errors = new List<string>();

		// Validate BaseUrl
		if (string.IsNullOrWhiteSpace(options.BaseUrl))
		{
			errors.Add("BaseUrl is required and cannot be empty.");
		}
		else
		{
			if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri))
			{
				errors.Add($"Invalid BaseUrl: '{options.BaseUrl}'. Must be a valid absolute URI.");
			}
			else if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
			{
				errors.Add("BaseUrl must use HTTP or HTTPS scheme.");
			}
		}

		// Validate timeout
		if (options.TimeoutSeconds <= 0)
		{
			errors.Add("TimeoutSeconds must be greater than 0.");
		}

		if (options.TimeoutSeconds > 300) // 5 minutes max
		{
			errors.Add("TimeoutSeconds cannot exceed 300 seconds (5 minutes).");
		}

		// Validate retry settings
		if (options.MaxRetryAttempts < 0)
		{
			errors.Add("MaxRetryAttempts cannot be negative.");
		}

		if (options.MaxRetryAttempts > 10)
		{
			errors.Add("MaxRetryAttempts cannot exceed 10.");
		}

		if (options.RetryDelayMilliseconds < 0)
		{
			errors.Add("RetryDelayMilliseconds cannot be negative.");
		}

		if (options.RetryDelayMilliseconds > 60000) // 1 minute max
		{
			errors.Add("RetryDelayMilliseconds cannot exceed 60000 (1 minute).");
		}

		// Validate Authentication if provided
		if (options.Authentication != null)
		{
			ValidateAuthentication(options.Authentication, errors);
		}

		// Validate Kafka settings if provided
		if (options.Kafka != null)
		{
			ValidateKafkaSettings(options.Kafka, errors);
		}

		return errors.Count > 0
			? ValidateOptionsResult.Fail(errors)
			: ValidateOptionsResult.Success;
	}

	private static void ValidateAuthentication(AuthenticationSettings auth, List<string> errors)
	{
		switch (auth.Type)
		{
			case AuthenticationType.Bearer:
				if (string.IsNullOrWhiteSpace(auth.BearerToken))
				{
					errors.Add("BearerToken is required when AuthenticationType is Bearer.");
				}
				break;

			case AuthenticationType.Basic:
				if (string.IsNullOrWhiteSpace(auth.Username))
				{
					errors.Add("Username is required when AuthenticationType is Basic.");
				}
				if (string.IsNullOrWhiteSpace(auth.Password))
				{
					errors.Add("Password is required when AuthenticationType is Basic.");
				}
				break;

			case AuthenticationType.ApiKey:
				if (string.IsNullOrWhiteSpace(auth.ApiKey))
				{
					errors.Add("ApiKey is required when AuthenticationType is ApiKey.");
				}
				if (string.IsNullOrWhiteSpace(auth.ApiKeyHeaderName))
				{
					errors.Add("ApiKeyHeaderName is required when AuthenticationType is ApiKey.");
				}
				break;

			case AuthenticationType.Custom:
				if (string.IsNullOrWhiteSpace(auth.CustomHeaderName))
				{
					errors.Add("CustomHeaderName is required when AuthenticationType is Custom.");
				}
				if (string.IsNullOrWhiteSpace(auth.CustomHeaderValue))
				{
					errors.Add("CustomHeaderValue is required when AuthenticationType is Custom.");
				}
				break;

			case AuthenticationType.None:
				// No validation needed
				break;

			default:
				errors.Add($"Unknown AuthenticationType: {auth.Type}");
				break;
		}
	}

	private static void ValidateKafkaSettings(RemoteKafkaSettings kafka, List<string> errors)
	{
		// Validate BootstrapServers
		if (string.IsNullOrWhiteSpace(kafka.BootstrapServers))
		{
			errors.Add("Kafka.BootstrapServers is required and cannot be empty.");
		}
		else
		{
			// Validate format: should be host:port or comma-separated list
			var servers = kafka.BootstrapServers.Split(',');
			foreach (var server in servers)
			{
				var trimmed = server.Trim();
				if (string.IsNullOrWhiteSpace(trimmed))
				{
					errors.Add("Kafka.BootstrapServers contains empty entries.");
					break;
				}

				if (!trimmed.Contains(':'))
				{
					errors.Add($"Invalid Kafka.BootstrapServer format: '{trimmed}'. Expected format: 'host:port'.");
					break;
				}
			}
		}

		// Validate SecurityProtocol
		var validProtocols = new[] { "Plaintext", "Ssl", "SaslPlaintext", "SaslSsl" };
		if (!string.IsNullOrWhiteSpace(kafka.SecurityProtocol) && 
		    Array.IndexOf(validProtocols, kafka.SecurityProtocol) == -1)
		{
			errors.Add($"Invalid Kafka.SecurityProtocol: '{kafka.SecurityProtocol}'. " +
			          $"Valid values are: {string.Join(", ", validProtocols)}");
		}

		// Validate SASL settings when using SASL
		if (kafka.SecurityProtocol == "SaslPlaintext" || kafka.SecurityProtocol == "SaslSsl")
		{
			if (string.IsNullOrWhiteSpace(kafka.SaslMechanism))
			{
				errors.Add("Kafka.SaslMechanism is required when SecurityProtocol is SaslPlaintext or SaslSsl.");
			}
			else
			{
				var validMechanisms = new[] { "Plain", "ScramSha256", "ScramSha512", "Gssapi", "OAuthBearer" };
				if (Array.IndexOf(validMechanisms, kafka.SaslMechanism) == -1)
				{
					errors.Add($"Invalid Kafka.SaslMechanism: '{kafka.SaslMechanism}'. " +
					          $"Valid values are: {string.Join(", ", validMechanisms)}");
				}
			}

			if (string.IsNullOrWhiteSpace(kafka.SaslUsername))
			{
				errors.Add("Kafka.SaslUsername is required when using SASL authentication.");
			}

			if (string.IsNullOrWhiteSpace(kafka.SaslPassword))
			{
				errors.Add("Kafka.SaslPassword is required when using SASL authentication.");
			}
		}
	}
}
