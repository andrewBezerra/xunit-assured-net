namespace XUnitAssured.Base.Kafka;

public class SchemaRegistrySettings
{
	public SchemaRegistrySettings()
	{
	}
	public SchemaRegistrySettings(string? url)
	{
		Url = url;
	}
	public string? Url { get; set; }
	public string? Key { get; set; }
	public string? Secret { get; set; }
}
