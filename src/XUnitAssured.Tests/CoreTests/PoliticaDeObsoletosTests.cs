using System;
using System.Linq;
using System.Reflection;

using Shouldly;
using Xunit;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// Tudo o que a próxima major remove passa antes por um <c>[Obsolete]</c> numa 6.x, e o aviso diz
/// quando sai e o que usar no lugar (#129). É o aviso que quem depende do membro recebe antes de o
/// build quebrar.
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Documentation")]
public class PoliticaDeObsoletosTests
{
	private static readonly Assembly[] Pacotes =
	[
		typeof(XUnitAssured.Core.DSL.ScenarioDsl).Assembly,
		typeof(XUnitAssured.Http.Extensions.HttpValidationBuilder).Assembly,
		typeof(XUnitAssured.Kafka.Extensions.KafkaValidationBuilder).Assembly,
		typeof(XUnitAssured.RabbitMq.Extensions.RabbitMqValidationBuilder).Assembly,
		typeof(XUnitAssured.Playwright.Extensions.PlaywrightValidationBuilder).Assembly,
		typeof(XUnitAssured.Camunda.Extensions.CamundaValidationBuilder).Assembly,
		typeof(XUnitAssured.Camunda.Testcontainers.CamundaContainerFixture).Assembly,
	];

	[Fact(DisplayName = "Every [Obsolete] in a package says when it is removed, and is a warning, not an error")]
	public void Every_Obsolete_Says_When_It_Goes()
	{
		var obsoletos = Pacotes
			.SelectMany(a => a.GetExportedTypes())
			.SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MemberInfo>().Append(t))
			.Select(m => (Membro: m, Atributo: m.GetCustomAttribute<ObsoleteAttribute>()))
			.Where(x => x.Atributo != null)
			.ToList();

		var semPrazo = obsoletos
			.Where(x => x.Atributo!.IsError || x.Atributo.Message?.Contains("removed in ") != true)
			.Select(x => $"{x.Membro.DeclaringType?.Name ?? x.Membro.Name}.{x.Membro.Name}")
			.ToList();

		obsoletos.ShouldNotBeEmpty("the Kafka JsonPath extension is obsolete since 6.10.1");
		semPrazo.ShouldBeEmpty("These [Obsolete] members do not say when they are removed, or break the build");
	}
}
