using FluentValidation;

namespace XUnitAssured.Base.Kafka;

public class KafkaSecurity
{
	public KafkaSecurity()
	{

	}
	public string SecurityProtocol { get; set; } = "Plaintext";
	public string SaslMechanisms { get; set; } = "Plain";
	public string? Username { get; set; }
	public string? Password { get; set; }

	public class KafkaSecuritySettingsValidator : AbstractValidator<KafkaSecurity>
	{
		public KafkaSecuritySettingsValidator()
		{
			When(x => x.SecurityProtocol == "SaslPlaintext", () =>
			{
				RuleFor(x => x.SaslMechanisms)
						.NotEmpty().WithMessage("SaslMechanisms não pode ser vazio.");
				RuleFor(x => x.Username)
					.NotEmpty().WithMessage("Username não pode ser vazio.");
				RuleFor(x => x.Password)
					.NotNull().WithMessage("Password não pode ser nulo.");
			});
		}
	}
}
