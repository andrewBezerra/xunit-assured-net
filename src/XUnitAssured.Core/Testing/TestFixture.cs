using System;
using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Core.Testing;

/// <summary>
/// Base test fixture for unified HTTP and Kafka integration tests.
/// This is a partial class - HTTP and Kafka packages extend it with their implementations.
/// Implements IDisposable to properly clean up resources.
/// </summary>
/// <remarks>
/// This class uses the partial class pattern to keep Core independent of HTTP/Kafka:
/// - Core: Defines TestSettings, TestMode, initialization hooks
/// - HTTP package: Adds HttpClient creation, WebApplicationFactory, HTTP-specific logic
/// - Kafka package: Adds Kafka producer/consumer creation, Kafka-specific logic
/// 
/// Derived classes should:
/// 1. Call base constructor
/// 2. Optionally override CreateWebApplicationFactory (HTTP)
/// 3. Use Settings.Http and Settings.Kafka as needed
/// </remarks>
/// <example>
/// <code>
/// // Create a concrete fixture
/// public class MyTestFixture : TestFixture
/// {
///     public MyTestFixture() : base()
///     {
///         // Base handles initialization based on testsettings.json
///     }
///     
///     // Optional: override HTTP factory
///     protected override WebApplicationFactory&lt;Program&gt; CreateWebApplicationFactory()
///     {
///         return new WebApplicationFactory&lt;Program&gt;()
///             .WithWebHostBuilder(builder => { /* custom config */ });
///     }
/// }
/// 
/// // Use in tests
/// public class MyTests : IClassFixture&lt;MyTestFixture&gt;
/// {
///     private readonly MyTestFixture _fixture;
///     
///     public MyTests(MyTestFixture fixture) => _fixture = fixture;
///     
///     [Fact]
///     public void Test_Http() => Given(_fixture).ApiResource("/api").Get();
///     
///     [Fact]
///     public void Test_Kafka() => Given(_fixture).Topic("my-topic").Produce(msg);
/// }
/// </code>
/// </example>
public abstract partial class TestFixture : IDisposable
{
	private bool _disposed;

	/// <summary>
	/// Unified test settings loaded from testsettings.json.
	/// Contains TestMode, Environment, and potentially Http/Kafka settings.
	/// </summary>
	public TestSettings Settings { get; protected set; }

	/// <summary>
	/// Current test execution mode (Local or Remote).
	/// Shortcut to Settings.TestMode.
	/// </summary>
	public TestMode TestMode => Settings.TestMode;

	/// <summary>
	/// Initializes the test fixture with optional environment.
	/// Loads settings from testsettings.json and calls partial initialization methods.
	/// </summary>
	/// <param name="environment">
	/// Optional environment name (e.g., "staging", "prod").
	/// If null, uses TEST_ENV environment variable.
	/// </param>
	protected TestFixture(string? environment = null)
	{
		// Load unified settings
		Settings = TestSettings.Load(environment
			?? Environment.GetEnvironmentVariable("TEST_ENV"));

		// Call partial initialization methods (implemented by HTTP/Kafka packages)
		InitializeHttp();
		InitializeKafka();
	}

	/// <summary>
	/// Initializes HTTP-specific resources.
	/// Implemented by XUnitAssured.Http package via partial class.
	/// Creates WebApplicationFactory (Local) or HttpClient (Remote).
	/// </summary>
	partial void InitializeHttp();

	/// <summary>
	/// Initializes Kafka-specific resources.
	/// Implemented by XUnitAssured.Kafka package via partial class.
	/// Validates KafkaSettings configuration.
	/// </summary>
	partial void InitializeKafka();

	/// <summary>
	/// Disposes HTTP-specific resources.
	/// Implemented by XUnitAssured.HTTP package via partial class.
	/// Cleans up WebApplicationFactory, HttpClient.
	/// </summary>
	partial void DisposeHttp();

	/// <summary>
	/// Disposes Kafka-specific resources.
	/// Implemented by XUnitAssured.Kafka package via partial class.
	/// Note: Producers/Consumers are typically disposed by test steps.
	/// </summary>
	partial void DisposeKafka();

	/// <summary>
	/// Disposes the test fixture and all managed resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes resources (managed and unmanaged).
	/// </summary>
	/// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			// Dispose managed resources via partial methods
			DisposeHttp();
			DisposeKafka();
		}

		_disposed = true;
	}
}
