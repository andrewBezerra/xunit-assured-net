namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Defines the mode in which integration tests are executed.
/// </summary>
public enum TestMode
{
	/// <summary>
	/// Local mode: Tests run against in-memory or local services.
	/// - HTTP: Uses WebApplicationFactory to host the application in-memory
	/// - Kafka: Connects to local Kafka instance (e.g., Docker, localhost:9092)
	/// This is the default mode for development and CI/CD pipelines.
	/// </summary>
	Local = 0,

	/// <summary>
	/// Remote mode: Tests run against deployed services in a remote environment.
	/// - HTTP: Connects to a real API endpoint (e.g., staging, production)
	/// - Kafka: Connects to a remote Kafka cluster (e.g., cloud-hosted, staging)
	/// Use this mode for end-to-end testing against actual deployed environments.
	/// </summary>
	Remote = 1
}
