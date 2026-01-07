using System;
using System.Collections.Generic;

using Confluent.Kafka;
using Confluent.SchemaRegistry;

using Microsoft.Extensions.Options;

namespace XUnitAssured.Kafka;

/// <summary>
/// Validates KafkaSettings using the IValidateOptions pattern from .NET.
/// This validator runs at startup when ValidateOnStart() is called.
/// </summary>
public class KafkaSettingsValidator : IValidateOptions<KafkaSettings>
{
	public ValidateOptionsResult Validate(string? name, KafkaSettings options)
	{
		var errors = new List<string>();

		// Validate BootstrapServers
		if (string.IsNullOrWhiteSpace(options.BootstrapServers))
		{
			errors.Add("BootstrapServers is required and cannot be empty.");
		}
		else
		{
			// Validate format: should be host:port or comma-separated list
			var servers = options.BootstrapServers.Split(',');
			foreach (var server in servers)
			{
				var trimmed = server.Trim();
				if (string.IsNullOrWhiteSpace(trimmed))
				{
					errors.Add("BootstrapServers contains empty entries.");
					break;
				}

				if (!trimmed.Contains(':'))
				{
					errors.Add($"Invalid BootstrapServer format: '{trimmed}'. Expected format: 'host:port'.");
					break;
				}
			}
		}

		// Validate SASL settings when SecurityProtocol requires authentication
		if (options.SecurityProtocol == SecurityProtocol.SaslPlaintext ||
		    options.SecurityProtocol == SecurityProtocol.SaslSsl)
		{
			if (!options.SaslMechanism.HasValue)
			{
				errors.Add("SaslMechanism is required when SecurityProtocol is SaslPlaintext or SaslSsl.");
			}

			if (string.IsNullOrWhiteSpace(options.SaslUsername))
			{
				errors.Add("SaslUsername is required when using SASL authentication.");
			}

			if (string.IsNullOrWhiteSpace(options.SaslPassword))
			{
				errors.Add("SaslPassword is required when using SASL authentication.");
			}
		}

		// Validate SSL settings when using SSL/TLS
		if (options.SecurityProtocol == SecurityProtocol.Ssl ||
		    options.SecurityProtocol == SecurityProtocol.SaslSsl)
		{
			if (!string.IsNullOrWhiteSpace(options.SslCaLocation))
			{
				// Validate that file exists if path is provided
				try
				{
					if (!System.IO.File.Exists(options.SslCaLocation))
					{
						errors.Add($"SSL CA certificate file not found: {options.SslCaLocation}");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Invalid SSL CA certificate path: {ex.Message}");
				}
			}
		}

		// Validate SchemaRegistry if provided
		if (options.SchemaRegistry != null)
		{
			if (string.IsNullOrWhiteSpace(options.SchemaRegistry.Url))
			{
				errors.Add("SchemaRegistry.Url is required when SchemaRegistry is configured.");
			}
			else
			{
				if (!Uri.TryCreate(options.SchemaRegistry.Url, UriKind.Absolute, out var uri))
				{
					errors.Add($"Invalid SchemaRegistry.Url: {options.SchemaRegistry.Url}");
				}
				else if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
				{
					errors.Add("SchemaRegistry.Url must use HTTP or HTTPS scheme.");
				}
			}

			// Validate authentication if provided
			// Only validate BasicAuthUserInfo when explicitly set to UserInfo (value 1)
			if (options.SchemaRegistry.BasicAuthCredentialsSource == AuthCredentialsSource.UserInfo)
			{
				if (string.IsNullOrWhiteSpace(options.SchemaRegistry.BasicAuthUserInfo))
				{
					errors.Add("SchemaRegistry.BasicAuthUserInfo is required when BasicAuthCredentialsSource is UserInfo.");
				}
				else if (!options.SchemaRegistry.BasicAuthUserInfo.Contains(':'))
				{
					errors.Add("SchemaRegistry.BasicAuthUserInfo must be in format 'username:password'.");
				}
			}
		}

		return errors.Count > 0
			? ValidateOptionsResult.Fail(errors)
			: ValidateOptionsResult.Success;
	}
}
