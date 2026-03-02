using XUnitAssured.Playwright.Testing;

namespace XunitAssured.PlayWright.Samples.Local.Test;

/// <summary>
/// Base class for all Playwright sample tests using PlaywrightSamplesFixture.
/// Reduces boilerplate by pre-configuring the fixture type.
/// Provides Given() method pre-configured with Page and Settings.
/// </summary>
/// <example>
/// <code>
/// public class MyTests : PlaywrightSamplesTestBase, IClassFixture&lt;PlaywrightSamplesFixture&gt;
/// {
///     public MyTests(PlaywrightSamplesFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void Test() => Given()
///         .NavigateTo("/")
///         .When().Execute()
///         .Then().AssertTitle("Home");
/// }
/// </code>
/// </example>
public abstract class PlaywrightSamplesTestBase : PlaywrightTestBase<PlaywrightSamplesFixture>
{
	protected PlaywrightSamplesTestBase(PlaywrightSamplesFixture fixture) : base(fixture)
	{
	}
}
