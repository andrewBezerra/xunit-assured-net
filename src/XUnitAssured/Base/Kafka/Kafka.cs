using FluentValidation;

using static XUnitAssured.Base.Kafka.KafkaSecurity;

namespace XUnitAssured.Base.Kafka;
public class Kafka
{
	public Kafka()
	{
	}
	public Kafka(string bootstrapServers, SchemaRegistrySettings? schemaRegistry, KafkaSecurity? security)
	{
		BootstrapServers = bootstrapServers;
		SchemaRegistry = schemaRegistry;
		if (security != null)
			SecuritySettings = security;
	}
	public string? BootstrapServers { get; set; }
	public SchemaRegistrySettings? SchemaRegistry { get; set; }
	public string? GroupName { get; set; }
	public KafkaSecurity SecuritySettings { get; set; } = new();

	public class KafkaSettingsValidator : AbstractValidator<Kafka?>
	{

		public KafkaSettingsValidator()
		{
			When(x => x?.BootstrapServers != null, () =>
			{
				RuleFor(x => x!.SecuritySettings)
					.NotNull().WithMessage("SecuritySettings não pode ser nulo.")
					.SetValidator(new KafkaSecuritySettingsValidator());
			});
		}
	}

}
