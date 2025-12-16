using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

using XUnitAssured.Base.Auth;

namespace XUnitAssured.Base;

/// <summary>
/// Validates BaseSettings using the IValidateOptions pattern from .NET.
/// This validator runs at startup when ValidateOnStart() is called.
/// </summary>
public class BaseSettingsValidator : IValidateOptions<BaseSettings>
{
	public ValidateOptionsResult Validate(string? name, BaseSettings options)
	{
		var errors = new List<string>();

		// Validate BaseUrl
		if (options.BaseUrl == null)
		{
			errors.Add("BaseUrl is required and cannot be null.");
		}
		else
		{
			if (!options.BaseUrl.IsAbsoluteUri)
			{
				errors.Add("BaseUrl must be an absolute URI.");
			}
			else if (options.BaseUrl.Scheme != Uri.UriSchemeHttp && options.BaseUrl.Scheme != Uri.UriSchemeHttps)
			{
				errors.Add("BaseUrl must use HTTP or HTTPS scheme.");
			}
		}

		// Validate OpenApiDocument (optional)
		if (options.OpenApiDocument != null)
		{
			if (!options.OpenApiDocument.IsAbsoluteUri)
			{
				errors.Add("OpenApiDocument must be an absolute URI when provided.");
			}
			else if (options.OpenApiDocument.Scheme != Uri.UriSchemeHttp && 
			         options.OpenApiDocument.Scheme != Uri.UriSchemeHttps &&
			         options.OpenApiDocument.Scheme != Uri.UriSchemeFile)
			{
				errors.Add("OpenApiDocument must use HTTP, HTTPS, or FILE scheme.");
			}
		}

		// Validate Authentication (if provided)
		if (options.Authentication != null && options.Authentication.AuthenticationType != AuthenticationType.None)
		{
			if (options.Authentication.BaseUrl == null)
			{
				errors.Add("Authentication.BaseUrl is required when authentication is enabled.");
			}
			else if (!options.Authentication.BaseUrl.IsAbsoluteUri)
			{
				errors.Add("Authentication.BaseUrl must be an absolute URI.");
			}
			else if (options.Authentication.BaseUrl.Scheme != Uri.UriSchemeHttp && 
			         options.Authentication.BaseUrl.Scheme != Uri.UriSchemeHttps)
			{
				errors.Add("Authentication.BaseUrl must use HTTP or HTTPS scheme.");
			}

			if (string.IsNullOrWhiteSpace(options.Authentication.ClientId))
			{
				errors.Add("Authentication.ClientId is required when authentication is enabled.");
			}

			if (string.IsNullOrWhiteSpace(options.Authentication.ClientSecret))
			{
				errors.Add("Authentication.ClientSecret is required when authentication is enabled.");
			}
		}

		return errors.Count > 0
			? ValidateOptionsResult.Fail(errors)
			: ValidateOptionsResult.Success;
	}
}
