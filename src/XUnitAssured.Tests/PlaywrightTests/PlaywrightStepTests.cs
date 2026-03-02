using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Steps")]
/// <summary>
/// Tests for PlaywrightStep action accumulation, properties, and validation.
/// Uses DSL extensions (public API) to add actions to steps.
/// </summary>
public class PlaywrightStepTests
{
	[Fact(DisplayName = "PlaywrightStep should have correct StepType")]
	public void Should_Have_Correct_StepType()
	{
		// Act
		var step = new PlaywrightStep();

		// Assert
		step.StepType.ShouldBe("Playwright");
	}

	[Fact(DisplayName = "PlaywrightStep should start with no actions")]
	public void Should_Start_With_No_Actions()
	{
		// Act
		var step = new PlaywrightStep();

		// Assert
		step.Actions.ShouldNotBeNull();
		step.Actions.Count.ShouldBe(0);
	}

	[Fact(DisplayName = "PlaywrightStep should start as not executed")]
	public void Should_Start_As_Not_Executed()
	{
		// Act
		var step = new PlaywrightStep();

		// Assert
		step.IsExecuted.ShouldBeFalse();
		step.Result.ShouldBeNull();
		step.Name.ShouldBeNull();
	}

	[Fact(DisplayName = "PlaywrightStep should accumulate actions via DSL")]
	public void Should_Accumulate_Actions_Via_Dsl()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/login")
			.Fill("#email", "test@test.com")
			.Fill("#password", "secret")
			.ClickByRole(Microsoft.Playwright.AriaRole.Button, "Sign In");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(4);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[3].ActionType.ShouldBe(PageActionType.Click);
	}

	[Fact(DisplayName = "PlaywrightStep should preserve action order via DSL")]
	public void Should_Preserve_Action_Order_Via_Dsl()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/page")
			.Click("#btn1")
			.Fill("#input1", "value")
			.TakeScreenshot("test.png")
			.Click("#btn2");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions.Count.ShouldBe(5);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[3].ActionType.ShouldBe(PageActionType.Screenshot);
		step.Actions[4].ActionType.ShouldBe(PageActionType.Click);
	}

	[Fact(DisplayName = "PlaywrightStep Actions should be read-only")]
	public void Actions_Should_Be_ReadOnly()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given().NavigateTo("/test");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions.ShouldBeAssignableTo<IReadOnlyList<PageAction>>();
	}

	[Fact(DisplayName = "PlaywrightStep should use default settings when none provided")]
	public void Should_Use_Default_Settings()
	{
		// Act
		var step = new PlaywrightStep();

		// Assert
		step.Settings.ShouldNotBeNull();
		step.Settings.Browser.ShouldBe(BrowserType.Chromium);
		step.Settings.Headless.ShouldBeTrue();
	}

	[Fact(DisplayName = "PlaywrightStep should accept custom settings")]
	public void Should_Accept_Custom_Settings()
	{
		// Arrange
		var settings = new PlaywrightSettings
		{
			BaseUrl = "https://myapp.com",
			Browser = BrowserType.Firefox,
			Headless = false
		};

		// Act
		var step = new PlaywrightStep { Settings = settings };

		// Assert
		step.Settings.BaseUrl.ShouldBe("https://myapp.com");
		step.Settings.Browser.ShouldBe(BrowserType.Firefox);
		step.Settings.Headless.ShouldBeFalse();
	}

	[Fact(DisplayName = "PlaywrightStep Page should be null by default")]
	public void Page_Should_Be_Null_By_Default()
	{
		// Act
		var step = new PlaywrightStep();

		// Assert
		step.Page.ShouldBeNull();
	}

	[Fact(DisplayName = "PlaywrightStep Validate should throw when not executed")]
	public void Validate_Should_Throw_When_Not_Executed()
	{
		// Arrange
		var step = new PlaywrightStep();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			step.Validate(result => { }));
	}

	[Fact(DisplayName = "PlaywrightStep should accumulate diverse action types via DSL")]
	public void Should_Accumulate_Diverse_Actions_Via_Dsl()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/")
			.FillByLabel("Email", "a@b.com")
			.ClickByTestId("submit")
			.CheckByLabel("Remember me")
			.SelectOption("#country", "BR")
			.HoverByText("Menu")
			.Wait(500)
			.TakeScreenshot("result.png");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions.Count.ShouldBe(8);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[3].ActionType.ShouldBe(PageActionType.Check);
		step.Actions[4].ActionType.ShouldBe(PageActionType.SelectOption);
		step.Actions[5].ActionType.ShouldBe(PageActionType.Hover);
		step.Actions[6].ActionType.ShouldBe(PageActionType.Wait);
		step.Actions[7].ActionType.ShouldBe(PageActionType.Screenshot);
	}
}
