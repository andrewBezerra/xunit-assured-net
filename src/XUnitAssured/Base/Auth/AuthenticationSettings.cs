using System;

using FluentValidation;

namespace XUnitAssured.Base.Auth;

public class AuthenticationSettings
{
	public AuthenticationSettings() { }
	public AuthenticationSettings(string? baseUrl, string? clientId, string? clientSecret, AuthenticationType authenticationType = AuthenticationType.None)
	{
		BaseUrl = baseUrl;
		ClientId = clientId;
		ClientSecret = clientSecret;
		AuthenticationType = authenticationType;
	}

	public string? BaseUrl { get; set; }
	public string? ClientId { get; set; }
	public string? ClientSecret { get; set; }
	public AuthenticationType AuthenticationType { get; set; }

	public class AuthenticationSettingsValidator : AbstractValidator<AuthenticationSettings>
	{
		public AuthenticationSettingsValidator()
		{
			When(x => x.AuthenticationType != AuthenticationType.None,
			  () =>
			  {
				  RuleFor(x => x.BaseUrl)
					.NotNull().WithMessage("BaseUrl não pode ser vazio.")
					.NotEmpty().WithMessage("BaseUrl não pode ser vazio.")
					.Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
					.WithMessage("A URL deve ser válida e usar HTTP ou HTTPS.");
				  RuleFor(x => x.ClientId)
					.NotEmpty().WithMessage("ClientId não pode ser vazio.");
				  RuleFor(x => x.ClientSecret)
					.NotEmpty().WithMessage("ClientSecret não pode ser vazio.");
			  });
		}
	}
}
