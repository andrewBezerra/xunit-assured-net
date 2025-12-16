using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

namespace XUnitAssured.Base.Auth;

/// <summary>
/// Validates AuthenticationSettings using the IValidateOptions pattern from .NET.
/// This validator runs at startup when ValidateOnStart() is called.
/// </summary>
public class AuthenticationSettingsValidator : IValidateOptions<AuthenticationSettings>
{
	public ValidateOptionsResult Validate(string? name, AuthenticationSettings options)
	{
		var errors = new List<string>();

		// Only validate when authentication is enabled
		if (options.AuthenticationType != AuthenticationType.None)
		{
			if (options.BaseUrl == null)
			{
				errors.Add("Authentication.BaseUrl is required when authentication is enabled.");
			}
			else
			{
				if (!options.BaseUrl.IsAbsoluteUri)
				{
					errors.Add("Authentication.BaseUrl must be an absolute URI.");
				}
				else if (options.BaseUrl.Scheme != Uri.UriSchemeHttp && options.BaseUrl.Scheme != Uri.UriSchemeHttps)
				{
					errors.Add("Authentication.BaseUrl must use HTTP or HTTPS scheme.");
				}
			}

			if (string.IsNullOrWhiteSpace(options.ClientId))
			{
				errors.Add("Authentication.ClientId is required when authentication is enabled.");
			}

			if (string.IsNullOrWhiteSpace(options.ClientSecret))
			{
				errors.Add("Authentication.ClientSecret is required when authentication is enabled.");
			}
		}

		return errors.Count > 0
			? ValidateOptionsResult.Fail(errors)
			: ValidateOptionsResult.Success;
	}
}
