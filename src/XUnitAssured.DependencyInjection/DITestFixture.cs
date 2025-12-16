using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace XUnitAssured.DependencyInjection;

/// <summary>
/// Base test fixture with Dependency Injection support.
/// Re-exports TestBedFixture from Xunit.Microsoft.DependencyInjection for convenience.
/// </summary>
public abstract class DITestFixture : TestBedFixture
{
	// This class serves as a base for all test fixtures that need DI support.
	// Inheriting from TestBedFixture provides:
	// - AddServices method for configuring DI container
	// - GetTestAppSettings for configuration files
	// - IAsyncLifetime support for proper async setup/cleanup
}
