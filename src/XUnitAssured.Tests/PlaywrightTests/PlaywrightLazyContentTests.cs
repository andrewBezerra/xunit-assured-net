using System;

using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "StepResult")]
/// <summary>
/// A step result used to capture the whole page HTML on every successful step,
/// which on a large page costs more than the step itself. The content is now
/// fetched on first read. What must hold: nothing is fetched until someone asks,
/// it is fetched once, and every existing way of reading it still works.
/// </summary>
public class PlaywrightLazyContentTests
{
	[Fact(DisplayName = "Creating a lazy result should not fetch the page content")]
	public void Creating_Lazy_Result_Should_Not_Fetch_Content()
	{
		var calls = 0;

		PlaywrightStepResult.CreateSuccess("https://app.test", "Home", () => { calls++; return "<html/>"; });

		calls.ShouldBe(0, "the point of laziness is that an unread page is never serialised");
	}

	[Fact(DisplayName = "PageContent should fetch on first read and reuse afterwards")]
	public void PageContent_Should_Fetch_Once()
	{
		var calls = 0;
		var result = PlaywrightStepResult.CreateSuccess("https://app.test", "Home", () => { calls++; return "<html>once</html>"; });

		var first = result.PageContent;
		var second = result.PageContent;

		first.ShouldBe("<html>once</html>");
		second.ShouldBeSameAs(first);
		calls.ShouldBe(1);
	}

	[Fact(DisplayName = "GetData<string>() should return the same content as PageContent")]
	public void GetData_Should_Match_PageContent()
	{
		var result = PlaywrightStepResult.CreateSuccess("https://app.test", "Home", () => "<html>data</html>");

		result.GetData<string>().ShouldBe(result.PageContent);
		result.DataType.ShouldBe(typeof(string));
	}

	[Fact(DisplayName = "A provider that yields null should make PageContent null, not throw")]
	public void Null_From_Provider_Should_Be_Null_Content()
	{
		// The step's provider returns null when the page has already been closed.
		var result = PlaywrightStepResult.CreateSuccess("https://app.test", "Home", () => null);

		result.PageContent.ShouldBeNull();
		result.GetData<string>().ShouldBeNull();
	}

	[Fact(DisplayName = "The eager factory should keep exposing the content through Data")]
	public void Eager_Factory_Should_Keep_Data()
	{
		var result = PlaywrightStepResult.CreateSuccess("https://app.test", "Home", "<html>eager</html>");

		result.Data.ShouldBe("<html>eager</html>");
		result.PageContent.ShouldBe("<html>eager</html>");
		result.GetData<string>().ShouldBe("<html>eager</html>");
	}

	[Fact(DisplayName = "Url, title, screenshots and console logs should be available without reading the content")]
	public void Metadata_Should_Not_Depend_On_Content()
	{
		var calls = 0;
		var result = PlaywrightStepResult.CreateSuccess(
			"https://app.test/orders", "Orders", () => { calls++; return "<html/>"; },
			screenshots: new() { "s1.png" }, consoleLogs: new() { "[log] hi" });

		result.Url.ShouldBe("https://app.test/orders");
		result.Title.ShouldBe("Orders");
		result.Screenshots.ShouldHaveSingleItem();
		result.ConsoleLogs.ShouldHaveSingleItem();
		result.Success.ShouldBeTrue();
		calls.ShouldBe(0);
	}

	[Fact(DisplayName = "A null provider should be rejected")]
	public void Null_Provider_Should_Be_Rejected()
	{
		Should.Throw<ArgumentNullException>(() =>
			PlaywrightStepResult.CreateSuccess("https://app.test", "Home", (Func<string?>)null!));
	}
}
