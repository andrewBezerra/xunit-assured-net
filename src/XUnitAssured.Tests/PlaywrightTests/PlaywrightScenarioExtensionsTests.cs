using Microsoft.Playwright;
using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "DSL")]
/// <summary>
/// Tests for PlaywrightScenarioExtensions DSL methods.
/// Verifies that each fluent method correctly creates and configures page actions.
/// </summary>
public class PlaywrightScenarioExtensionsTests
{
	// ──────────────────────────────────────────────
	// Navigation
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "NavigateTo should create Navigate action with URL")]
	public void NavigateTo_Should_Create_Navigate_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/login");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(1);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[0].Value.ShouldBe("/login");
	}

	[Fact(DisplayName = "NavigateTo should throw on null URL")]
	public void NavigateTo_Should_Throw_On_Null_Url()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act & Assert
		Should.Throw<ArgumentException>(() => scenario.NavigateTo(null!));
	}

	[Fact(DisplayName = "NavigateTo should throw on empty URL")]
	public void NavigateTo_Should_Throw_On_Empty_Url()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act & Assert
		Should.Throw<ArgumentException>(() => scenario.NavigateTo("   "));
	}

	// ──────────────────────────────────────────────
	// Click variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Click with CSS selector should create Click action")]
	public void Click_Css_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Click("#submit-btn");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions.Count.ShouldBe(2);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
		step.Actions[1].Locator.Selector.ShouldBe("#submit-btn");
	}

	[Fact(DisplayName = "ClickByRole should create Click action with Role locator")]
	public void ClickByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByRole(AriaRole.Button, "Sign In");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].Locator.Name.ShouldBe("Sign In");
	}

	[Fact(DisplayName = "ClickByText should create Click action with Text locator")]
	public void ClickByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByText("Submit form");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
		step.Actions[1].Locator.Selector.ShouldBe("Submit form");
	}

	[Fact(DisplayName = "ClickByLabel should create Click action with Label locator")]
	public void ClickByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByLabel("Remember me");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Locator.Selector.ShouldBe("Remember me");
	}

	[Fact(DisplayName = "ClickByTestId should create Click action with TestId locator")]
	public void ClickByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByTestId("submit-btn");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Locator.Selector.ShouldBe("submit-btn");
	}

	[Fact(DisplayName = "ClickByTitle should create Click action with Title locator")]
	public void ClickByTitle_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByTitle("Close");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Title);
		step.Actions[1].Locator.Selector.ShouldBe("Close");
	}

	// ──────────────────────────────────────────────
	// DoubleClick variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "DoubleClick should create DoubleClick action")]
	public void DoubleClick_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DoubleClick(".editable-cell");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DoubleClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
	}

	[Fact(DisplayName = "DoubleClickByTestId should create DoubleClick with TestId locator")]
	public void DoubleClickByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DoubleClickByTestId("cell-1");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DoubleClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Fill variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Fill with CSS selector should create Fill action")]
	public void Fill_Css_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/register").Fill("#email", "user@test.com");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
		step.Actions[1].Locator.Selector.ShouldBe("#email");
		step.Actions[1].Value.ShouldBe("user@test.com");
	}

	[Fact(DisplayName = "FillByLabel should create Fill action with Label locator")]
	public void FillByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/register").FillByLabel("Email", "user@test.com");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Locator.Selector.ShouldBe("Email");
		step.Actions[1].Value.ShouldBe("user@test.com");
	}

	[Fact(DisplayName = "FillByPlaceholder should create Fill action with Placeholder locator")]
	public void FillByPlaceholder_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/register").FillByPlaceholder("Enter your email", "user@test.com");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Placeholder);
		step.Actions[1].Locator.Selector.ShouldBe("Enter your email");
		step.Actions[1].Value.ShouldBe("user@test.com");
	}

	[Fact(DisplayName = "FillByRole should create Fill action with Role locator")]
	public void FillByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/search").FillByRole(AriaRole.Textbox, "Search", "playwright");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].Locator.Name.ShouldBe("Search");
		step.Actions[1].Value.ShouldBe("playwright");
	}

	[Fact(DisplayName = "FillByTestId should create Fill action with TestId locator")]
	public void FillByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/register").FillByTestId("email-input", "user@test.com");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Locator.Selector.ShouldBe("email-input");
		step.Actions[1].Value.ShouldBe("user@test.com");
	}

	// ──────────────────────────────────────────────
	// TypeText variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TypeText should create Type action")]
	public void TypeText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/search").TypeText("#search", "playwright");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Type);
		step.Actions[1].Value.ShouldBe("playwright");
	}

	[Fact(DisplayName = "TypeTextByLabel should create Type action with Label locator")]
	public void TypeTextByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/search").TypeTextByLabel("Search", "test");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Type);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	// ──────────────────────────────────────────────
	// Press
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Press should create Press action with key")]
	public void Press_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Press("#search", "Enter");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Press);
		step.Actions[1].Locator!.Selector.ShouldBe("#search");
		step.Actions[1].Value.ShouldBe("Enter");
	}

	[Fact(DisplayName = "PressByTestId should create Press action with TestId locator")]
	public void PressByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").PressByTestId("input-field", "Control+A");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Value.ShouldBe("Control+A");
	}

	// ──────────────────────────────────────────────
	// Clear
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Clear should create Clear action")]
	public void Clear_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Clear("#email");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Clear);
	}

	[Fact(DisplayName = "ClearByLabel should create Clear action with Label locator")]
	public void ClearByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClearByLabel("Email");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Clear);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	// ──────────────────────────────────────────────
	// Check / Uncheck
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Check should create Check action")]
	public void Check_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Check("#agree-terms");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Check);
	}

	[Fact(DisplayName = "CheckByLabel should create Check action with Label locator")]
	public void CheckByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").CheckByLabel("I agree to the terms");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Check);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Locator.Selector.ShouldBe("I agree to the terms");
	}

	[Fact(DisplayName = "CheckByRole should create Check action with Role locator")]
	public void CheckByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").CheckByRole(AriaRole.Checkbox, "Accept");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Check);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "Uncheck should create Uncheck action")]
	public void Uncheck_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Uncheck("#newsletter");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Uncheck);
	}

	[Fact(DisplayName = "UncheckByLabel should create Uncheck action with Label locator")]
	public void UncheckByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UncheckByLabel("Newsletter");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Uncheck);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	// ──────────────────────────────────────────────
	// SelectOption
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "SelectOption should create SelectOption action")]
	public void SelectOption_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectOption("#country", "BR");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SelectOption);
		step.Actions[1].Locator!.Selector.ShouldBe("#country");
		step.Actions[1].Value.ShouldBe("BR");
	}

	[Fact(DisplayName = "SelectOptionByLabel should create SelectOption with Label locator")]
	public void SelectOptionByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectOptionByLabel("Country", "Brazil");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Value.ShouldBe("Brazil");
	}

	[Fact(DisplayName = "SelectOptionByTestId should create SelectOption with TestId locator")]
	public void SelectOptionByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectOptionByTestId("sort-dropdown", "price-asc");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Value.ShouldBe("price-asc");
	}

	// ──────────────────────────────────────────────
	// Hover
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Hover should create Hover action")]
	public void Hover_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Hover(".dropdown-trigger");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Hover);
	}

	[Fact(DisplayName = "HoverByText should create Hover with Text locator")]
	public void HoverByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").HoverByText("Products");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
	}

	[Fact(DisplayName = "HoverByRole should create Hover with Role locator")]
	public void HoverByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").HoverByRole(AriaRole.Menuitem, "Settings");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].Locator.Name.ShouldBe("Settings");
	}

	[Fact(DisplayName = "HoverByTestId should create Hover with TestId locator")]
	public void HoverByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").HoverByTestId("avatar");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Focus
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Focus should create Focus action")]
	public void Focus_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Focus("#search-input");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Focus);
	}

	[Fact(DisplayName = "FocusByTestId should create Focus with TestId locator")]
	public void FocusByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").FocusByTestId("search");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Screenshot
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TakeScreenshot should create Screenshot action")]
	public void TakeScreenshot_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").TakeScreenshot("before-submit.png");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Screenshot);
		step.Actions[1].Value.ShouldBe("before-submit.png");
	}

	[Fact(DisplayName = "TakeScreenshot without filename should create action with null value")]
	public void TakeScreenshot_Without_Filename()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").TakeScreenshot();

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Screenshot);
		step.Actions[1].Value.ShouldBeNull();
	}

	// ──────────────────────────────────────────────
	// Wait / WaitForSelector
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Wait should create Wait action with milliseconds")]
	public void Wait_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").Wait(1000);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Wait);
		step.Actions[1].Value.ShouldBe("1000");
	}

	[Fact(DisplayName = "WaitForSelector should create WaitForSelector action")]
	public void WaitForSelector_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").WaitForSelector(".loading-complete");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.WaitForSelector);
		step.Actions[1].Value.ShouldBe(".loading-complete");
	}

	// ──────────────────────────────────────────────
	// Fluent Chaining
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should support full fluent chain with multiple locator strategies")]
	public void Should_Support_Full_Fluent_Chain()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/login")
			.FillByLabel("Email", "admin@test.com")
			.FillByPlaceholder("Enter password", "secret123")
			.CheckByLabel("Remember me")
			.ClickByRole(AriaRole.Button, "Sign In");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(5);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[2].Locator!.Type.ShouldBe(LocatorType.Placeholder);
		step.Actions[3].ActionType.ShouldBe(PageActionType.Check);
		step.Actions[3].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[4].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[4].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "Should reuse the same PlaywrightStep across chained calls")]
	public void Should_Reuse_Same_Step()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/page")
			.Click("#btn1")
			.Fill("#input1", "value")
			.Click("#btn2");

		// Assert - all actions in the same step
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions.Count.ShouldBe(4);
	}

	[Fact(DisplayName = "Complex form scenario should accumulate all actions correctly")]
	public void Complex_Form_Scenario()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/register")
			.Fill("#firstName", "Carlos")
			.FillByLabel("Last Name", "Bezerra")
			.FillByPlaceholder("Enter your email", "carlos@test.com")
			.FillByTestId("phone-input", "+5511999999999")
			.SelectOptionByLabel("Country", "Brazil")
			.CheckByLabel("I agree to the terms")
			.TakeScreenshot("before-submit.png")
			.ClickByRole(AriaRole.Button, "Register");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(9);

		// Verify diverse locator types
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);       // Fill CSS
		step.Actions[2].Locator!.Type.ShouldBe(LocatorType.Label);     // FillByLabel
		step.Actions[3].Locator!.Type.ShouldBe(LocatorType.Placeholder); // FillByPlaceholder
		step.Actions[4].Locator!.Type.ShouldBe(LocatorType.TestId);    // FillByTestId
		step.Actions[5].Locator!.Type.ShouldBe(LocatorType.Label);     // SelectOptionByLabel
		step.Actions[6].Locator!.Type.ShouldBe(LocatorType.Label);     // CheckByLabel
		step.Actions[7].ActionType.ShouldBe(PageActionType.Screenshot); // TakeScreenshot
		step.Actions[8].Locator!.Type.ShouldBe(LocatorType.Role);      // ClickByRole
	}

	// ──────────────────────────────────────────────
	// Upload Files
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "UploadFile should create UploadFile action")]
	public void UploadFile_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UploadFile("#file-input", @"C:\docs\resume.pdf");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.UploadFile);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
		step.Actions[1].FilePaths.ShouldNotBeNull();
		step.Actions[1].FilePaths!.Length.ShouldBe(1);
		step.Actions[1].FilePaths[0].ShouldBe(@"C:\docs\resume.pdf");
	}

	[Fact(DisplayName = "UploadFileByLabel should create UploadFile action with Label locator")]
	public void UploadFileByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UploadFileByLabel("Upload file", @"C:\docs\resume.pdf");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.UploadFile);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	[Fact(DisplayName = "UploadFileByTestId should create UploadFile action with TestId locator")]
	public void UploadFileByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UploadFileByTestId("file-upload", @"C:\docs\resume.pdf");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.UploadFile);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "UploadFiles should create UploadFile action with multiple files")]
	public void UploadFiles_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UploadFiles("#file-input", new[] { "file1.txt", "file2.txt" });

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.UploadFile);
		step.Actions[1].FilePaths!.Length.ShouldBe(2);
	}

	[Fact(DisplayName = "ClearUploadFile should create ClearUploadFile action")]
	public void ClearUploadFile_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClearUploadFile("#file-input");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.ClearUploadFile);
	}

	// ──────────────────────────────────────────────
	// Drag and Drop
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "DragTo should create DragTo action with source and target")]
	public void DragTo_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DragTo("#draggable", "#droppable");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DragTo);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
		step.Actions[1].Locator.Selector.ShouldBe("#draggable");
		step.Actions[1].TargetLocator.ShouldNotBeNull();
		step.Actions[1].TargetLocator!.Selector.ShouldBe("#droppable");
	}

	[Fact(DisplayName = "DragToByTestId should create DragTo action with TestId locators")]
	public void DragToByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DragToByTestId("item-1", "drop-zone");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DragTo);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].TargetLocator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "DragToByText should create DragTo action with Text locators")]
	public void DragToByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DragToByText("Card A", "Zone B");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DragTo);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
		step.Actions[1].TargetLocator!.Type.ShouldBe(LocatorType.Text);
	}

	[Fact(DisplayName = "DragTo with LocatorStrategy should create DragTo action")]
	public void DragTo_LocatorStrategy_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();
		var source = LocatorStrategy.ByRole(AriaRole.Listitem, "Task 1");
		var target = LocatorStrategy.ByTestId("done-column");

		// Act
		scenario.NavigateTo("/page").DragTo(source, target);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DragTo);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].TargetLocator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Scroll Into View
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ScrollIntoView should create ScrollIntoView action")]
	public void ScrollIntoView_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ScrollIntoView("#footer");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.ScrollIntoView);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
	}

	[Fact(DisplayName = "ScrollIntoViewByText should create ScrollIntoView with Text locator")]
	public void ScrollIntoViewByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ScrollIntoViewByText("Footer text");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.ScrollIntoView);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
	}

	[Fact(DisplayName = "ScrollIntoViewByTestId should create ScrollIntoView with TestId locator")]
	public void ScrollIntoViewByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ScrollIntoViewByTestId("load-more");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.ScrollIntoView);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "ScrollIntoViewByRole should create ScrollIntoView with Role locator")]
	public void ScrollIntoViewByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ScrollIntoViewByRole(AriaRole.Button, "Load More");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.ScrollIntoView);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	// ──────────────────────────────────────────────
	// Right Click
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "RightClick should create RightClick action")]
	public void RightClick_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").RightClick("#context-target");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.RightClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Css);
	}

	[Fact(DisplayName = "RightClickByRole should create RightClick with Role locator")]
	public void RightClickByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").RightClickByRole(AriaRole.Row, "Data Row");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.RightClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "RightClickByText should create RightClick with Text locator")]
	public void RightClickByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").RightClickByText("Item");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.RightClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
	}

	[Fact(DisplayName = "RightClickByTestId should create RightClick with TestId locator")]
	public void RightClickByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").RightClickByTestId("row-1");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.RightClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "RightClickByLabel should create RightClick with Label locator")]
	public void RightClickByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").RightClickByLabel("File name");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.RightClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	// ──────────────────────────────────────────────
	// Force Click
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ForceClick should create Click action with force flag")]
	public void ForceClick_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ForceClick("#hidden-btn");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Force.ShouldBeTrue();
	}

	[Fact(DisplayName = "ForceClickByRole should create Click action with force flag")]
	public void ForceClickByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ForceClickByRole(AriaRole.Button, "Submit");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Force.ShouldBeTrue();
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "ForceClickByTestId should create Click action with force flag")]
	public void ForceClickByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ForceClickByTestId("overlay-btn");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Force.ShouldBeTrue();
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Click With Modifiers
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ClickWithModifiers should create Click action with modifiers")]
	public void ClickWithModifiers_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickWithModifiers("#item", "Shift");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Click);
		step.Actions[1].Modifiers.ShouldNotBeNull();
		step.Actions[1].Modifiers!.Length.ShouldBe(1);
		step.Actions[1].Modifiers[0].ShouldBe("Shift");
	}

	[Fact(DisplayName = "ClickWithModifiers should support multiple modifiers")]
	public void ClickWithModifiers_Multiple_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickWithModifiers("#item", "Control", "Shift");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Modifiers!.Length.ShouldBe(2);
	}

	[Fact(DisplayName = "ClickByTextWithModifiers should create Click action with modifiers")]
	public void ClickByTextWithModifiers_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClickByTextWithModifiers("Item", "ControlOrMeta");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
		step.Actions[1].Modifiers.ShouldNotBeNull();
	}

	// ──────────────────────────────────────────────
	// Dispatch Event
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "DispatchEvent should create DispatchEvent action")]
	public void DispatchEvent_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DispatchEvent("#btn", "click");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DispatchEvent);
		step.Actions[1].Value.ShouldBe("click");
	}

	[Fact(DisplayName = "DispatchEventByRole should create DispatchEvent with Role locator")]
	public void DispatchEventByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DispatchEventByRole(AriaRole.Button, "click");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DispatchEvent);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "DispatchEventByTestId should create DispatchEvent with TestId locator")]
	public void DispatchEventByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DispatchEventByTestId("submit-btn", "click");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DispatchEvent);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Set Checked
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "SetChecked true should create SetChecked action")]
	public void SetChecked_True_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SetChecked("#terms", true);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[1].Value.ShouldBe("true");
	}

	[Fact(DisplayName = "SetChecked false should create SetChecked action")]
	public void SetChecked_False_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SetChecked("#terms", false);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[1].Value.ShouldBe("false");
	}

	[Fact(DisplayName = "SetCheckedByLabel should create SetChecked with Label locator")]
	public void SetCheckedByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SetCheckedByLabel("Subscribe", true);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Value.ShouldBe("true");
	}

	[Fact(DisplayName = "SetCheckedByRole should create SetChecked with Role locator")]
	public void SetCheckedByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SetCheckedByRole(AriaRole.Checkbox, true, "Accept");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "SetCheckedByTestId should create SetChecked with TestId locator")]
	public void SetCheckedByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SetCheckedByTestId("agree-checkbox", false);

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Value.ShouldBe("false");
	}

	// ──────────────────────────────────────────────
	// Select Multiple Options
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "SelectMultipleOptions should create action with multiple values")]
	public void SelectMultipleOptions_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectMultipleOptions("#colors", new[] { "red", "green", "blue" });

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SelectMultipleOptions);
		step.Actions[1].Values.ShouldNotBeNull();
		step.Actions[1].Values!.Length.ShouldBe(3);
	}

	[Fact(DisplayName = "SelectMultipleOptionsByLabel should create action with Label locator")]
	public void SelectMultipleOptionsByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectMultipleOptionsByLabel("Colors", new[] { "red", "blue" });

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SelectMultipleOptions);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	[Fact(DisplayName = "SelectMultipleOptionsByTestId should create action with TestId locator")]
	public void SelectMultipleOptionsByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectMultipleOptionsByTestId("color-select", new[] { "red" });

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SelectMultipleOptions);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	// ──────────────────────────────────────────────
	// Missing By* Variants (new tests)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "DoubleClickByText should create DoubleClick with Text locator")]
	public void DoubleClickByText_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DoubleClickByText("Editable text");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DoubleClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Text);
	}

	[Fact(DisplayName = "DoubleClickByLabel should create DoubleClick with Label locator")]
	public void DoubleClickByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DoubleClickByLabel("Word");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DoubleClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	[Fact(DisplayName = "DoubleClickByTitle should create DoubleClick with Title locator")]
	public void DoubleClickByTitle_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").DoubleClickByTitle("Edit cell");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.DoubleClick);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Title);
	}

	[Fact(DisplayName = "ClearByTestId should create Clear with TestId locator")]
	public void ClearByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClearByTestId("email-input");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Clear);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "ClearByRole should create Clear with Role locator")]
	public void ClearByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClearByRole(AriaRole.Textbox, "Search");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Clear);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "ClearByPlaceholder should create Clear with Placeholder locator")]
	public void ClearByPlaceholder_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").ClearByPlaceholder("Enter email");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Clear);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Placeholder);
	}

	[Fact(DisplayName = "UncheckByRole should create Uncheck with Role locator")]
	public void UncheckByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UncheckByRole(AriaRole.Checkbox, "Newsletter");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Uncheck);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "UncheckByTestId should create Uncheck with TestId locator")]
	public void UncheckByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").UncheckByTestId("newsletter-cb");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Uncheck);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
	}

	[Fact(DisplayName = "SelectOptionByRole should create SelectOption with Role locator")]
	public void SelectOptionByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").SelectOptionByRole(AriaRole.Listbox, "blue");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.SelectOption);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].Value.ShouldBe("blue");
	}

	[Fact(DisplayName = "TypeTextByTestId should create Type with TestId locator")]
	public void TypeTextByTestId_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").TypeTextByTestId("search-input", "hello");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Type);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.TestId);
		step.Actions[1].Value.ShouldBe("hello");
	}

	[Fact(DisplayName = "TypeTextByPlaceholder should create Type with Placeholder locator")]
	public void TypeTextByPlaceholder_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").TypeTextByPlaceholder("Search...", "playwright");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Type);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Placeholder);
	}

	[Fact(DisplayName = "TypeTextByRole should create Type with Role locator")]
	public void TypeTextByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").TypeTextByRole(AriaRole.Textbox, "Search", "pw");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Type);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	[Fact(DisplayName = "PressByLabel should create Press with Label locator")]
	public void PressByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").PressByLabel("Search", "Enter");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Press);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
		step.Actions[1].Value.ShouldBe("Enter");
	}

	[Fact(DisplayName = "PressByRole should create Press with Role locator")]
	public void PressByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").PressByRole(AriaRole.Textbox, "Control+A");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Press);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
		step.Actions[1].Value.ShouldBe("Control+A");
	}

	[Fact(DisplayName = "FocusByLabel should create Focus with Label locator")]
	public void FocusByLabel_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").FocusByLabel("Password");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Focus);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Label);
	}

	[Fact(DisplayName = "FocusByRole should create Focus with Role locator")]
	public void FocusByRole_Should_Create_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/page").FocusByRole(AriaRole.Textbox, "Email");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step!.Actions[1].ActionType.ShouldBe(PageActionType.Focus);
		step.Actions[1].Locator!.Type.ShouldBe(LocatorType.Role);
	}

	// ──────────────────────────────────────────────
	// Advanced Fluent Chain with New Actions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should support full fluent chain with all new actions")]
	public void Should_Support_Full_Chain_With_New_Actions()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/form")
			.FillByLabel("Name", "Test User")
			.UploadFileByLabel("Profile Photo", "photo.jpg")
			.SetCheckedByLabel("I agree", true)
			.SelectMultipleOptionsByLabel("Skills", new[] { "C#", "TypeScript" })
			.ScrollIntoViewByText("Submit")
			.ClickByRole(AriaRole.Button, "Submit");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(7);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[2].ActionType.ShouldBe(PageActionType.UploadFile);
		step.Actions[3].ActionType.ShouldBe(PageActionType.SetChecked);
		step.Actions[4].ActionType.ShouldBe(PageActionType.SelectMultipleOptions);
		step.Actions[5].ActionType.ShouldBe(PageActionType.ScrollIntoView);
		step.Actions[6].ActionType.ShouldBe(PageActionType.Click);
	}

	// ──────────────────────────────────────────────
	// Codegen / Recording
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "RecordAndPause should create Pause action")]
	public void RecordAndPause_Should_Create_Pause_Action()
	{
		// Arrange
		var scenario = ScenarioDsl.Given();

		// Act
		scenario.NavigateTo("/login").RecordAndPause();

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(2);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Pause);
		step.Actions[1].Locator.ShouldBeNull();
		step.Actions[1].Value.ShouldBeNull();
	}

	[Fact(DisplayName = "RecordAndPause should throw on null scenario")]
	public void RecordAndPause_Should_Throw_On_Null_Scenario()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			PlaywrightScenarioExtensions.RecordAndPause(null!));
	}

	[Fact(DisplayName = "RecordAndPause should be chainable in fluent DSL")]
	public void RecordAndPause_Should_Be_Chainable()
	{
		// Arrange & Act
		var scenario = ScenarioDsl.Given()
			.NavigateTo("/login")
			.RecordAndPause()
			.Fill("#email", "user@test.com")
			.Click("#submit");

		// Assert
		var step = scenario.CurrentStep as PlaywrightStep;
		step.ShouldNotBeNull();
		step!.Actions.Count.ShouldBe(4);
		step.Actions[0].ActionType.ShouldBe(PageActionType.Navigate);
		step.Actions[1].ActionType.ShouldBe(PageActionType.Pause);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Fill);
		step.Actions[3].ActionType.ShouldBe(PageActionType.Click);
	}
}
