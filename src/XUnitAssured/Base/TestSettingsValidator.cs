using System;

using FluentValidation;

using XUnitAssured.Base.Auth;
using XUnitAssured.Base.Kafka;

namespace XUnitAssured.Base;

public class TestSettingsValidator : AbstractValidator<BaseSettings>
{
	public TestSettingsValidator()
	{
		When(x => !string.IsNullOrEmpty(x.BaseUrl), () =>
		{
			RuleFor(x => x.BaseUrl)
				.Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
							 (uri.Scheme == Uri.UriSchemeHttp ||
							 uri.Scheme == Uri.UriSchemeHttps))
				.WithMessage("A URL deve ser válida e usar HTTP ou HTTPS.");
		});

		When(x => x.Authentication != null, () =>
		{
			RuleFor(x => x.Authentication!)
				.SetValidator(new AuthenticationSettings.AuthenticationSettingsValidator());
		});

		When(x => x.Kafka != null, () =>
		{
			RuleFor(x => x.Kafka!)
				.SetValidator(new KafkaSecurity.KafkaSecuritySettingsValidator());
		});
	}
}

