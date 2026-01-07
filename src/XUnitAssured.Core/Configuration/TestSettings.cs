namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Base test settings for XUnitAssured integration tests.
/// This is a partial class - HTTP and Kafka packages extend it with their own properties.
/// </summary>
/// <remarks>
/// This class uses the partial class pattern to maintain zero coupling between Core and specific packages (HTTP, Kafka).
/// - Core package defines: TestMode, Environment
/// - HTTP package adds: HttpSettings (via partial class)
/// - Kafka package adds: KafkaSettings (via partial class)
/// 
/// Configuration is loaded from testsettings.json:
/// <code>
/// {
///   "testMode": "Local",
///   "environment": "dev",
///   "http": { ... },    // Populated if XUnitAssured.Http is referenced
///   "kafka": { ... }    // Populated if XUnitAssured.Kafka is referenced
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // Load settings from testsettings.json
/// var settings = TestSettings.Load();
/// 
/// // Access core properties
/// if (settings.TestMode == TestMode.Local)
/// {
///     // Run local tests
/// }
/// 
/// // If HTTP package is referenced, settings.Http will be available
/// // If Kafka package is referenced, settings.Kafka will be available
/// </code>
/// </example>
public partial class TestSettings
{
	/// <summary>
	/// Test execution mode (Local or Remote).
	/// Default: Local (in-memory services)
	/// </summary>
	public TestMode TestMode { get; set; } = TestMode.Local;

	/// <summary>
	/// Current environment name (e.g., "dev", "staging", "prod").
	/// Can be overridden by TEST_ENV environment variable.
	/// </summary>
	public string? Environment { get; set; }

	/// <summary>
	/// Loads test settings from testsettings.json.
	/// Searches in current directory and parent directories.
	/// </summary>
	/// <param name="environment">
	/// Optional environment name. If null, uses TEST_ENV environment variable.
	/// When specified, loads testsettings.{environment}.json if it exists.
	/// </param>
	/// <returns>Loaded test settings with all configured properties</returns>
	/// <example>
	/// <code>
	/// // Load default settings
	/// var settings = TestSettings.Load();
	/// 
	/// // Load environment-specific settings
	/// var stagingSettings = TestSettings.Load("staging");  // Loads testsettings.staging.json
	/// 
	/// // Or via environment variable:
	/// // TEST_ENV=staging dotnet test
	/// var settings = TestSettings.Load();  // Automatically loads staging config
	/// </code>
	/// </example>
	public static TestSettings Load(string? environment = null)
	{
		return TestSettingsLoader.Load(environment);
	}
}
