using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "DSL")]
/// <summary>
/// The verbs for what the browser holds, rather than what is on the screen.
///
/// <para>
/// The DSL had verbs for interacting with a page and none for cookies or storage, so a test
/// about a session had to drop to <c>IPage</c> and hand-write JavaScript — the point at which
/// a DSL stops earning its place. These came out of testing a real sign-in, where a session
/// secret moved out of <c>localStorage</c> and into an <c>HttpOnly</c> cookie.
/// </para>
///
/// <para>
/// What is asserted here is what the chain records, which is what the step will carry out.
/// Whether a browser then honours it is Playwright's contract, not this package's.
/// </para>
/// </summary>
public class BrowserStateExtensionsTests
{
	[Fact(DisplayName = "ClearCookies should record a cookie-clearing action")]
	public void ClearCookies_Should_Record_Action()
	{
		var scenario = ScenarioDsl.Given();

		scenario.ClearCookies();

		var acao = AcaoUnica(scenario);
		acao.ActionType.ShouldBe(PageActionType.ClearCookies);
	}

	[Fact(DisplayName = "SetLocalStorage should record both the key and the value")]
	public void SetLocalStorage_Should_Record_Key_And_Value()
	{
		var scenario = ScenarioDsl.Given();

		scenario.SetLocalStorage("auth_token", "abc.def.ghi");

		var acao = AcaoUnica(scenario);
		acao.ActionType.ShouldBe(PageActionType.SetLocalStorage);
		acao.Value.ShouldBe("auth_token");
		acao.SecondValue.ShouldBe("abc.def.ghi");
	}

	[Fact(DisplayName = "ClearLocalStorage should record a storage-clearing action")]
	public void ClearLocalStorage_Should_Record_Action()
	{
		var scenario = ScenarioDsl.Given();

		scenario.ClearLocalStorage();

		AcaoUnica(scenario).ActionType.ShouldBe(PageActionType.ClearLocalStorage);
	}

	// Estes verbos são de navegador, e a cadeia precisa continuar sabendo disso: é o tipo que
	// faz `Execute()` resolver para o construtor de asserções certo quando um projeto também
	// referencia HTTP e Kafka.
	[Fact(DisplayName = "Browser state verbs should keep the chain typed as a browser scenario")]
	public void Browser_State_Verbs_Should_Keep_The_Chain_Typed()
	{
		ScenarioDsl.Given().ClearCookies().ShouldBeAssignableTo<IBrowserScenario>();
		ScenarioDsl.Given().SetLocalStorage("k", "v").ShouldBeAssignableTo<IBrowserScenario>();
		ScenarioDsl.Given().ClearLocalStorage().ShouldBeAssignableTo<IBrowserScenario>();
	}

	// Uma arrumação costuma ser várias coisas seguidas; cada verbo acrescenta um passo em vez
	// de substituir o anterior.
	[Fact(DisplayName = "Browser state verbs should chain with the rest of the DSL")]
	public void Browser_State_Verbs_Should_Chain()
	{
		var scenario = ScenarioDsl.Given()
			.ClearCookies()
			.SetLocalStorage("idioma", "pt")
			.NavigateTo("/orders");

		var step = (PlaywrightStep)scenario.CurrentStep!;

		step.Actions.Count.ShouldBe(3);
		step.Actions[0].ActionType.ShouldBe(PageActionType.ClearCookies);
		step.Actions[1].ActionType.ShouldBe(PageActionType.SetLocalStorage);
		step.Actions[2].ActionType.ShouldBe(PageActionType.Navigate);
	}

	private static PageAction AcaoUnica(XUnitAssured.Core.Abstractions.ITestScenario scenario)
	{
		var step = scenario.CurrentStep.ShouldBeOfType<PlaywrightStep>();
		return step.Actions.ShouldHaveSingleItem();
	}
}
