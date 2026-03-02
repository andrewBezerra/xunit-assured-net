using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Steps")]
/// <summary>
/// Tests for PageAction and PageActionType.
/// </summary>
public class PageActionTests
{
	[Fact(DisplayName = "Navigate action should store URL as value")]
	public void Navigate_Should_Store_Url()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Navigate,
			Value = "/login"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Navigate);
		action.Value.ShouldBe("/login");
		action.Locator.ShouldBeNull();
	}

	[Fact(DisplayName = "Click action should store locator")]
	public void Click_Should_Store_Locator()
	{
		// Arrange
		var locator = LocatorStrategy.Css("#submit");

		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = locator
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Click);
		action.Locator.ShouldNotBeNull();
		action.Locator!.Type.ShouldBe(LocatorType.Css);
		action.Locator.Selector.ShouldBe("#submit");
		action.Value.ShouldBeNull();
	}

	[Fact(DisplayName = "Fill action should store locator and value")]
	public void Fill_Should_Store_Locator_And_Value()
	{
		// Arrange
		var locator = LocatorStrategy.ByLabel("Email");

		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Fill,
			Locator = locator,
			Value = "user@test.com"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Fill);
		action.Locator.ShouldNotBeNull();
		action.Locator!.Selector.ShouldBe("Email");
		action.Value.ShouldBe("user@test.com");
	}

	[Fact(DisplayName = "Screenshot action should store filename as value")]
	public void Screenshot_Should_Store_Filename()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Screenshot,
			Value = "error-page.png"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Screenshot);
		action.Value.ShouldBe("error-page.png");
		action.Locator.ShouldBeNull();
	}

	[Fact(DisplayName = "Wait action should store duration as value")]
	public void Wait_Should_Store_Duration()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Wait,
			Value = "1000"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Wait);
		action.Value.ShouldBe("1000");
	}

	// ──────────────────────────────────────────────
	// ToString Tests
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ToString with locator and value should format correctly")]
	public void ToString_With_Locator_And_Value()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Fill,
			Locator = LocatorStrategy.Css("#email"),
			Value = "test@example.com"
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldBe("Fill(css=#email, \"test@example.com\")");
	}

	[Fact(DisplayName = "ToString with locator only should format correctly")]
	public void ToString_With_Locator_Only()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = LocatorStrategy.ByTestId("submit-btn")
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldBe("Click(testid=submit-btn)");
	}

	[Fact(DisplayName = "ToString with value only should format correctly")]
	public void ToString_With_Value_Only()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Navigate,
			Value = "/dashboard"
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldBe("Navigate(\"/dashboard\")");
	}

	[Fact(DisplayName = "ToString with no locator or value should show action type")]
	public void ToString_With_Nothing()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Screenshot
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldBe("Screenshot()");
	}

	// ──────────────────────────────────────────────
	// All PageActionType Enum Values
	// ──────────────────────────────────────────────

	[Theory(DisplayName = "All PageActionType values should be valid")]
	[InlineData(PageActionType.Navigate)]
	[InlineData(PageActionType.Click)]
	[InlineData(PageActionType.DoubleClick)]
	[InlineData(PageActionType.Fill)]
	[InlineData(PageActionType.Clear)]
	[InlineData(PageActionType.Type)]
	[InlineData(PageActionType.Press)]
	[InlineData(PageActionType.Check)]
	[InlineData(PageActionType.Uncheck)]
	[InlineData(PageActionType.SelectOption)]
	[InlineData(PageActionType.Hover)]
	[InlineData(PageActionType.Focus)]
	[InlineData(PageActionType.Screenshot)]
	[InlineData(PageActionType.Wait)]
	[InlineData(PageActionType.WaitForSelector)]
	[InlineData(PageActionType.UploadFile)]
	[InlineData(PageActionType.ClearUploadFile)]
	[InlineData(PageActionType.DragTo)]
	[InlineData(PageActionType.ScrollIntoView)]
	[InlineData(PageActionType.RightClick)]
	[InlineData(PageActionType.DispatchEvent)]
	[InlineData(PageActionType.SetChecked)]
	[InlineData(PageActionType.SelectMultipleOptions)]
	public void All_PageActionTypes_Should_Be_Valid(PageActionType actionType)
	{
		// Act
		var action = new PageAction { ActionType = actionType };

		// Assert
		action.ActionType.ShouldBe(actionType);
	}

	// ──────────────────────────────────────────────
	// New Action Properties Tests
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "UploadFile action should store file paths")]
	public void UploadFile_Should_Store_FilePaths()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.UploadFile,
			Locator = LocatorStrategy.ByLabel("Upload"),
			FilePaths = new[] { "file1.txt", "file2.txt" }
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.UploadFile);
		action.FilePaths.ShouldNotBeNull();
		action.FilePaths!.Length.ShouldBe(2);
		action.FilePaths[0].ShouldBe("file1.txt");
		action.FilePaths[1].ShouldBe("file2.txt");
	}

	[Fact(DisplayName = "DragTo action should store source and target locators")]
	public void DragTo_Should_Store_Source_And_Target()
	{
		// Arrange
		var source = LocatorStrategy.ByTestId("draggable");
		var target = LocatorStrategy.ByTestId("droppable");

		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.DragTo,
			Locator = source,
			TargetLocator = target
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.DragTo);
		action.Locator.ShouldBe(source);
		action.TargetLocator.ShouldBe(target);
	}

	[Fact(DisplayName = "ScrollIntoView action should store locator")]
	public void ScrollIntoView_Should_Store_Locator()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.ScrollIntoView,
			Locator = LocatorStrategy.ByText("Footer")
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.ScrollIntoView);
		action.Locator.ShouldNotBeNull();
		action.Locator!.Selector.ShouldBe("Footer");
	}

	[Fact(DisplayName = "RightClick action should store locator")]
	public void RightClick_Should_Store_Locator()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.RightClick,
			Locator = LocatorStrategy.Css("#context-menu")
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.RightClick);
		action.Locator.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Click with modifiers should store modifiers")]
	public void Click_Should_Store_Modifiers()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = LocatorStrategy.ByText("Item"),
			Modifiers = new[] { "Shift", "Control" }
		};

		// Assert
		action.Modifiers.ShouldNotBeNull();
		action.Modifiers!.Length.ShouldBe(2);
		action.Modifiers[0].ShouldBe("Shift");
		action.Modifiers[1].ShouldBe("Control");
	}

	[Fact(DisplayName = "Force click should store force flag")]
	public void Click_Should_Store_Force_Flag()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = LocatorStrategy.Css("#hidden-btn"),
			Force = true
		};

		// Assert
		action.Force.ShouldBeTrue();
	}

	[Fact(DisplayName = "DispatchEvent action should store event type")]
	public void DispatchEvent_Should_Store_EventType()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.DispatchEvent,
			Locator = LocatorStrategy.ByRole(Microsoft.Playwright.AriaRole.Button),
			Value = "click"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.DispatchEvent);
		action.Value.ShouldBe("click");
	}

	[Fact(DisplayName = "SetChecked action should store checked state")]
	public void SetChecked_Should_Store_State()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.SetChecked,
			Locator = LocatorStrategy.ByLabel("Subscribe"),
			Value = "true"
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.SetChecked);
		action.Value.ShouldBe("true");
	}

	[Fact(DisplayName = "SelectMultipleOptions action should store multiple values")]
	public void SelectMultipleOptions_Should_Store_Values()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.SelectMultipleOptions,
			Locator = LocatorStrategy.Css("#colors"),
			Values = new[] { "red", "green", "blue" }
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.SelectMultipleOptions);
		action.Values.ShouldNotBeNull();
		action.Values!.Length.ShouldBe(3);
	}

	// ──────────────────────────────────────────────
	// Updated ToString Tests
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ToString with target locator should format correctly")]
	public void ToString_With_TargetLocator()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.DragTo,
			Locator = LocatorStrategy.ByTestId("source"),
			TargetLocator = LocatorStrategy.ByTestId("target")
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldContain("DragTo");
		result.ShouldContain("target=");
	}

	[Fact(DisplayName = "ToString with file paths should format correctly")]
	public void ToString_With_FilePaths()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.UploadFile,
			Locator = LocatorStrategy.Css("#upload"),
			FilePaths = new[] { "file1.txt", "file2.txt" }
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldContain("UploadFile");
		result.ShouldContain("files=");
		result.ShouldContain("file1.txt");
	}

	[Fact(DisplayName = "ToString with modifiers should format correctly")]
	public void ToString_With_Modifiers()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = LocatorStrategy.ByText("Item"),
			Modifiers = new[] { "Shift" }
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldContain("Click");
		result.ShouldContain("modifiers=");
		result.ShouldContain("Shift");
	}

	[Fact(DisplayName = "ToString with force should format correctly")]
	public void ToString_With_Force()
	{
		// Arrange
		var action = new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = LocatorStrategy.Css("#btn"),
			Force = true
		};

		// Act
		var result = action.ToString();

		// Assert
		result.ShouldContain("Click");
		result.ShouldContain("force=true");
	}

	[Fact(DisplayName = "Pause action should have no locator or value")]
	public void Pause_Should_Have_No_Locator_Or_Value()
	{
		// Act
		var action = new PageAction
		{
			ActionType = PageActionType.Pause
		};

		// Assert
		action.ActionType.ShouldBe(PageActionType.Pause);
		action.Locator.ShouldBeNull();
		action.Value.ShouldBeNull();
	}
}
