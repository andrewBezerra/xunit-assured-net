using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace XUnitAssured.Core.Testing;

/// <summary>
/// Base test fixture with Dependency Injection support.
/// Re-exports TestBedFixture from Xunit.Microsoft.DependencyInjection for convenience.
/// </summary>
/// <remarks>
/// This class serves as a base for all test fixtures that need DI support.
/// Inheriting from TestBedFixture provides:
/// <list type="bullet">
/// <item><description>AddServices method for configuring DI container</description></item>
/// <item><description>GetTestAppSettings for configuration files</description></item>
/// <item><description>IAsyncLifetime support for proper async setup/cleanup</description></item>
/// </list>
/// 
/// Example usage:
/// <code>
/// public class MyTestFixture : DITestFixture
/// {
///     protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
///     {
///         // Configure your services
///         services.AddSingleton&lt;IMyService, MyService&gt;();
///     }
///     
///     protected override IEnumerable&lt;TestAppSettings&gt; GetTestAppSettings()
///     {
///         yield return new() { Filename = "appsettings.json", IsOptional = false };
///     }
/// }
/// </code>
/// </remarks>
public abstract class DITestFixture : TestBedFixture
{
	// This class serves as a convenient base for all test fixtures that need DI support.
	// All functionality is inherited from TestBedFixture.
}
