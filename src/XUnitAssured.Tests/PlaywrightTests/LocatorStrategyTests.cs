using Microsoft.Playwright;
using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Locators")]
/// <summary>
/// Tests for LocatorStrategy factory methods, properties, and ToString.
/// </summary>
public class LocatorStrategyTests
{
	// ──────────────────────────────────────────────
	// CSS Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "CSS locator should store selector correctly")]
	public void Css_Should_Store_Selector()
	{
		// Act
		var locator = LocatorStrategy.Css("#login-btn");

		// Assert
		locator.Type.ShouldBe(LocatorType.Css);
		locator.Selector.ShouldBe("#login-btn");
		locator.Name.ShouldBeNull();
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "CSS locator should support complex selectors")]
	public void Css_Should_Support_Complex_Selectors()
	{
		// Act
		var locator = LocatorStrategy.Css("input[type='email'].form-control");

		// Assert
		locator.Type.ShouldBe(LocatorType.Css);
		locator.Selector.ShouldBe("input[type='email'].form-control");
	}

	[Fact(DisplayName = "CSS locator ToString should format correctly")]
	public void Css_ToString_Should_Format_Correctly()
	{
		// Act
		var locator = LocatorStrategy.Css(".submit-btn");

		// Assert
		locator.ToString().ShouldBe("css=.submit-btn");
	}

	[Fact(DisplayName = "CSS locator should throw on null selector")]
	public void Css_Should_Throw_On_Null()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => LocatorStrategy.Css(null!));
	}

	// ──────────────────────────────────────────────
	// Role Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Role locator should store role string correctly")]
	public void ByRole_String_Should_Store_Role()
	{
		// Act
		var locator = LocatorStrategy.ByRole("button", "Submit");

		// Assert
		locator.Type.ShouldBe(LocatorType.Role);
		locator.Selector.ShouldBe("button");
		locator.Name.ShouldBe("Submit");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "Role locator should accept AriaRole enum")]
	public void ByRole_AriaRole_Should_Store_Role()
	{
		// Act
		var locator = LocatorStrategy.ByRole(AriaRole.Button, "Sign In", exact: true);

		// Assert
		locator.Type.ShouldBe(LocatorType.Role);
		locator.Selector.ShouldBe("Button");
		locator.Name.ShouldBe("Sign In");
		locator.Exact.ShouldBeTrue();
	}

	[Fact(DisplayName = "Role locator without name should have null Name")]
	public void ByRole_Without_Name_Should_Be_Null()
	{
		// Act
		var locator = LocatorStrategy.ByRole(AriaRole.Navigation);

		// Assert
		locator.Name.ShouldBeNull();
	}

	[Fact(DisplayName = "Role locator ToString with name should include name")]
	public void ByRole_ToString_With_Name()
	{
		// Act
		var locator = LocatorStrategy.ByRole(AriaRole.Button, "Submit");

		// Assert
		locator.ToString().ShouldBe("role=Button[name=\"Submit\"]");
	}

	[Fact(DisplayName = "Role locator ToString without name should show role only")]
	public void ByRole_ToString_Without_Name()
	{
		// Act
		var locator = LocatorStrategy.ByRole(AriaRole.Navigation);

		// Assert
		locator.ToString().ShouldBe("role=Navigation");
	}

	// ──────────────────────────────────────────────
	// Text Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Text locator should store text correctly")]
	public void ByText_Should_Store_Text()
	{
		// Act
		var locator = LocatorStrategy.ByText("Sign in");

		// Assert
		locator.Type.ShouldBe(LocatorType.Text);
		locator.Selector.ShouldBe("Sign in");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "Text locator with exact match should set flag")]
	public void ByText_Exact_Should_Set_Flag()
	{
		// Act
		var locator = LocatorStrategy.ByText("Sign in", exact: true);

		// Assert
		locator.Exact.ShouldBeTrue();
	}

	[Fact(DisplayName = "Text locator ToString should format correctly")]
	public void ByText_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByText("Click me");

		// Assert
		locator.ToString().ShouldBe("text=Click me");
	}

	// ──────────────────────────────────────────────
	// Label Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Label locator should store label text correctly")]
	public void ByLabel_Should_Store_Label()
	{
		// Act
		var locator = LocatorStrategy.ByLabel("Email");

		// Assert
		locator.Type.ShouldBe(LocatorType.Label);
		locator.Selector.ShouldBe("Email");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "Label locator with exact should set flag")]
	public void ByLabel_Exact_Should_Set_Flag()
	{
		// Act
		var locator = LocatorStrategy.ByLabel("Password", exact: true);

		// Assert
		locator.Exact.ShouldBeTrue();
	}

	[Fact(DisplayName = "Label locator ToString should format correctly")]
	public void ByLabel_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByLabel("Username");

		// Assert
		locator.ToString().ShouldBe("label=Username");
	}

	// ──────────────────────────────────────────────
	// Placeholder Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Placeholder locator should store placeholder text correctly")]
	public void ByPlaceholder_Should_Store_Placeholder()
	{
		// Act
		var locator = LocatorStrategy.ByPlaceholder("Enter your email");

		// Assert
		locator.Type.ShouldBe(LocatorType.Placeholder);
		locator.Selector.ShouldBe("Enter your email");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "Placeholder locator ToString should format correctly")]
	public void ByPlaceholder_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByPlaceholder("Search...");

		// Assert
		locator.ToString().ShouldBe("placeholder=Search...");
	}

	// ──────────────────────────────────────────────
	// TestId Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TestId locator should store test ID correctly")]
	public void ByTestId_Should_Store_TestId()
	{
		// Act
		var locator = LocatorStrategy.ByTestId("submit-btn");

		// Assert
		locator.Type.ShouldBe(LocatorType.TestId);
		locator.Selector.ShouldBe("submit-btn");
	}

	[Fact(DisplayName = "TestId locator ToString should format correctly")]
	public void ByTestId_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByTestId("login-form");

		// Assert
		locator.ToString().ShouldBe("testid=login-form");
	}

	// ──────────────────────────────────────────────
	// AltText Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AltText locator should store alt text correctly")]
	public void ByAltText_Should_Store_AltText()
	{
		// Act
		var locator = LocatorStrategy.ByAltText("Company Logo");

		// Assert
		locator.Type.ShouldBe(LocatorType.AltText);
		locator.Selector.ShouldBe("Company Logo");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "AltText locator ToString should format correctly")]
	public void ByAltText_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByAltText("Profile picture");

		// Assert
		locator.ToString().ShouldBe("alttext=Profile picture");
	}

	// ──────────────────────────────────────────────
	// Title Locator
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Title locator should store title text correctly")]
	public void ByTitle_Should_Store_Title()
	{
		// Act
		var locator = LocatorStrategy.ByTitle("Close dialog");

		// Assert
		locator.Type.ShouldBe(LocatorType.Title);
		locator.Selector.ShouldBe("Close dialog");
		locator.Exact.ShouldBeFalse();
	}

	[Fact(DisplayName = "Title locator with exact should set flag")]
	public void ByTitle_Exact_Should_Set_Flag()
	{
		// Act
		var locator = LocatorStrategy.ByTitle("Close", exact: true);

		// Assert
		locator.Exact.ShouldBeTrue();
	}

	[Fact(DisplayName = "Title locator ToString should format correctly")]
	public void ByTitle_ToString()
	{
		// Act
		var locator = LocatorStrategy.ByTitle("Help");

		// Assert
		locator.ToString().ShouldBe("title=Help");
	}

	// ──────────────────────────────────────────────
	// LocatorType Enum Coverage
	// ──────────────────────────────────────────────

	[Theory(DisplayName = "All LocatorType values should have corresponding factory methods")]
	[InlineData(LocatorType.Css)]
	[InlineData(LocatorType.Role)]
	[InlineData(LocatorType.Text)]
	[InlineData(LocatorType.Label)]
	[InlineData(LocatorType.Placeholder)]
	[InlineData(LocatorType.TestId)]
	[InlineData(LocatorType.AltText)]
	[InlineData(LocatorType.Title)]
	public void All_LocatorTypes_Should_Be_Creatable(LocatorType type)
	{
		// Act
		var locator = type switch
		{
			LocatorType.Css => LocatorStrategy.Css("#test"),
			LocatorType.Role => LocatorStrategy.ByRole(AriaRole.Button),
			LocatorType.Text => LocatorStrategy.ByText("test"),
			LocatorType.Label => LocatorStrategy.ByLabel("test"),
			LocatorType.Placeholder => LocatorStrategy.ByPlaceholder("test"),
			LocatorType.TestId => LocatorStrategy.ByTestId("test"),
			LocatorType.AltText => LocatorStrategy.ByAltText("test"),
			LocatorType.Title => LocatorStrategy.ByTitle("test"),
			_ => throw new NotSupportedException()
		};

		// Assert
		locator.Type.ShouldBe(type);
		locator.Selector.ShouldNotBeNullOrEmpty();
	}
}
