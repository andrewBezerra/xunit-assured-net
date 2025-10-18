using System.Linq;

using XUnitAssured.Base.Auth;
using XUnitAssured.Base.Kafka;

namespace XUnitAssured.Base;

public class BaseSettings
{
	public string BaseUrl { get; set; }
	public string OpenApiDocument { get; set; }
	public AuthenticationSettings? Authentication { get; set; }
	public KafkaSecurity? Kafka { get; set; }

	public BaseSettings()
	{
		BaseUrl = string.Empty;
		OpenApiDocument = string.Empty;
		Authentication = new AuthenticationSettings();
		Kafka = null;
	}

	public BaseSettings(string baseUrl, string openApiDocument, AuthenticationSettings authentication, KafkaSecurity kafka)
	{
		BaseUrl = baseUrl;
		OpenApiDocument = openApiDocument;
		Authentication = authentication;
		Kafka = kafka;
	}

	public void Validate()
	{
		FluentValidation.Results.ValidationResult result = new TestSettingsValidator().Validate(this);
		if (!result.IsValid)
		{
			throw new InvalidTestSettingsException($"\nArquivo appsettings.json inválido!\n\n Falhas encontradas:\n{string.Join("\n", result.Errors.Select(x => $" - {x.ErrorMessage}"))}");
		}
	}
}
