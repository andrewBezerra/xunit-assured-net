using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Tests.PlaywrightTests;

/// <summary>
/// O And() abre um passo novo também numa cadeia só de navegador.
///
/// <para>
/// Até a 6.3 os verbos do Playwright continuavam o passo atual sempre que ele era de navegador,
/// sem olhar o And(). As ações caíam todas no mesmo passo, e um Validate escrito entre elas --
/// para guardar o token que um fetch devolveu, por exemplo -- só rodava depois de todas. Foi
/// achado escrevendo o FetchFromPage com corpo adiado: o segundo fetch saía com o token nulo.
/// </para>
///
/// Sem navegador: o que se verifica é a montagem da cadeia.
/// </summary>
[Trait("Category", "Playwright")]
[Trait("Component", "DSL")]
public class FronteiraDePassoNoNavegadorTests
{
	[Fact(DisplayName = "Browser verbs without And() add to the same step")]
	public void Without_And_The_Step_Continues()
	{
		var cenario = ScenarioDsl.Given();

		cenario.NavigateTo("https://app.example/");
		var primeiro = cenario.CurrentStep;
		cenario.Reload();

		cenario.CurrentStep.ShouldBeSameAs(primeiro);
	}

	[Fact(DisplayName = "And() makes the next browser verb start a new step")]
	public void And_Starts_A_New_Step()
	{
		var cenario = ScenarioDsl.Given();

		cenario.NavigateTo("https://app.example/");
		var primeiro = cenario.CurrentStep;
		cenario.And().FetchFromPage("https://api.example/me");

		cenario.CurrentStep.ShouldNotBeNull();
		cenario.CurrentStep.ShouldNotBeSameAs(primeiro);
	}

	[Fact(DisplayName = "After the new step starts, the following verbs continue it")]
	public void The_New_Step_Then_Continues()
	{
		var cenario = ScenarioDsl.Given();

		cenario.NavigateTo("https://app.example/");
		cenario.And().FetchFromPage("https://api.example/login", "POST");
		var segundo = cenario.CurrentStep;
		cenario.FetchFromPage("https://api.example/me");

		cenario.CurrentStep.ShouldBeSameAs(segundo);
	}
}
