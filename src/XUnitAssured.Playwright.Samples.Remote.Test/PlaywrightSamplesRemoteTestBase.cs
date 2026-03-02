using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Playwright.Samples.Remote.Test;

/// <summary>
/// Base class for all remote Playwright sample tests.
/// Reduces boilerplate by pre-configuring the fixture type for remote testing.
/// Provides Given() method pre-configured with Page and Settings.
/// </summary>
/// <remarks>
/// This base class is specifically designed for testing remote/deployed websites.
/// It uses PlaywrightSamplesRemoteFixture which loads configuration from playwrightsettings.json.
///
/// Usage:
/// <code>
/// public class MyTests : PlaywrightSamplesRemoteTestBase, IClassFixture&lt;PlaywrightSamplesRemoteFixture&gt;
/// {
///     public MyTests(PlaywrightSamplesRemoteFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void Test() => Given()
///         .NavigateTo("/")
///         .When().Execute()
///         .Then().AssertTitleContains("TodoMVC");
/// }
/// </code>
/// </remarks>
public abstract class PlaywrightSamplesRemoteTestBase : PlaywrightTestBase<PlaywrightSamplesRemoteFixture>
{
	protected PlaywrightSamplesRemoteTestBase(PlaywrightSamplesRemoteFixture fixture) : base(fixture)
	{
	}
}
