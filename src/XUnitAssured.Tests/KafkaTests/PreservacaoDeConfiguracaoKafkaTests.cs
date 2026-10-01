using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Confluent.Kafka;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "DSL")]
/// <summary>
/// Guarda o padrão de reconstrução imutável do DSL Kafka.
///
/// <para>
/// Os passos são <c>init</c>-only, então cada verbo que muda um valor reconstrói o passo inteiro.
/// Qualquer propriedade deixada de fora dessa reconstrução é descartada em silêncio, e foi o que
/// aconteceu com o <c>AuthConfig</c> em cinco verbos do lado do consumo: autenticar e depois
/// ajustar o prazo trocava a credencial explícita pela da fixture, ou derrubava a conexão num
/// broker protegido com um erro que não aponta para a causa.
/// </para>
///
/// <para>
/// O pacote HTTP já tinha passado por isso e tinha guarda contra, em
/// <c>HttpStepConfigurationPreservationTests</c>. Este arquivo é o equivalente, e de propósito
/// não enumera propriedades à mão: ele compara por reflexão tudo o que o passo declara. Uma
/// propriedade nova nasce coberta, que é o único jeito de a guarda não envelhecer junto com a
/// classe.
/// </para>
/// </summary>
public class PreservacaoDeConfiguracaoKafkaTests
{
	[Fact(DisplayName = "WithTimeout should preserve every other consume setting")]
	public void WithTimeout_Should_Preserve_Everything_Else() =>
		PreservaTudoExceto(c => c.WithTimeout(TimeSpan.FromSeconds(7)), nameof(KafkaConsumeStep.Timeout));

	[Fact(DisplayName = "WithGroupId should preserve every other consume setting")]
	public void WithGroupId_Should_Preserve_Everything_Else() =>
		PreservaTudoExceto(c => c.WithGroupId("outro-grupo"), nameof(KafkaConsumeStep.GroupId));

	[Fact(DisplayName = "WithBootstrapServers should preserve every other consume setting")]
	public void WithBootstrapServers_Should_Preserve_Everything_Else() =>
		PreservaTudoExceto(
			c => c.WithBootstrapServers("broker:19092"), nameof(KafkaConsumeStep.BootstrapServers));

	[Fact(DisplayName = "WithSchema should preserve every other consume setting")]
	public void WithSchema_Should_Preserve_Everything_Else() =>
		PreservaTudoExceto(c => c.WithSchema(typeof(string)), nameof(KafkaConsumeStep.SchemaType));

	[Fact(DisplayName = "WithConsumerConfig should preserve every other consume setting")]
	public void WithConsumerConfig_Should_Preserve_Everything_Else() =>
		PreservaTudoExceto(
			c => c.WithConsumerConfig(new ConsumerConfig { GroupId = "explicito" }),
			nameof(KafkaConsumeStep.ConsumerConfig),
			// Um ConsumerConfig explícito traz o próprio grupo e endereço, então estes
			// acompanham o que foi passado em vez de sobreviverem.
			nameof(KafkaConsumeStep.GroupId),
			nameof(KafkaConsumeStep.BootstrapServers));

	// A autenticação é a que custa mais caro quando se perde, e é a que se perdia.
	[Fact(DisplayName = "Authentication should survive every consume verb")]
	public void Authentication_Should_Survive_Every_Consume_Verb()
	{
		var verbos = new (string Nome, Func<ITestScenario, ITestScenario> Aplicar)[]
		{
			("WithTimeout", c => c.WithTimeout(TimeSpan.FromSeconds(7))),
			("WithGroupId", c => c.WithGroupId("outro-grupo")),
			("WithBootstrapServers", c => c.WithBootstrapServers("broker:19092")),
			("WithSchema", c => c.WithSchema(typeof(string))),
			("WithConsumerConfig", c => c.WithConsumerConfig(new ConsumerConfig())),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var passo = (KafkaConsumeStep)aplicar(CenarioDeConsumo()).CurrentStep!;

			passo.AuthConfig.ShouldNotBeNull($"{nome} dropped the authentication");
			passo.AuthConfig!.SaslPlain!.Username.ShouldBe("usuario", $"{nome} changed the credential");
		}
	}

	/// <summary>
	/// Aplica o verbo e exige que tudo o que ele não deveria mexer continue igual.
	/// </summary>
	private static void PreservaTudoExceto(
		Func<ITestScenario, ITestScenario> aplicar, params string[] mudam)
	{
		var cenario = CenarioDeConsumo();
		var antes = Instantaneo((KafkaConsumeStep)cenario.CurrentStep!);

		var depois = (KafkaConsumeStep)aplicar(cenario).CurrentStep!;

		foreach (var (nome, valor) in antes)
		{
			if (mudam.Contains(nome))
				continue;

			Ler(depois, nome).ShouldBe(valor, $"{nome} was dropped when the step was rebuilt");
		}
	}

	/// <summary>
	/// Um passo de consumo com tudo o que ele sabe guardar preenchido, para que qualquer perda
	/// apareça. Montado direto em vez de pela cadeia, para não depender de arquivo de
	/// configuração nem de broker.
	/// </summary>
	private static ITestScenario CenarioDeConsumo()
	{
		var cenario = ScenarioDsl.Given();

		cenario.SetCurrentStep(new KafkaConsumeStep
		{
			Topic = "orders.created",
			SchemaType = typeof(int),
			Timeout = TimeSpan.FromSeconds(42),
			ConsumerConfig = null,
			GroupId = "grupo-do-teste",
			BootstrapServers = "localhost:19092",
			AuthConfig = new KafkaAuthConfig
			{
				Type = KafkaAuthenticationType.SaslPlain,
				SaslPlain = new SaslPlainConfig { Username = "usuario", Password = "senha" }
			}
		});

		return cenario;
	}

	private static IReadOnlyList<(string Nome, object? Valor)> Instantaneo(KafkaConsumeStep passo) =>
		Propriedades().Select(p => (p.Name, p.GetValue(passo))).ToList();

	private static object? Ler(KafkaConsumeStep passo, string nome) =>
		Propriedades().Single(p => p.Name == nome).GetValue(passo);

	/// <summary>
	/// Tudo o que o passo expõe e consegue carregar. Por reflexão, para que uma propriedade
	/// acrescentada depois entre na guarda sem ninguém precisar lembrar.
	/// </summary>
	private static IEnumerable<PropertyInfo> Propriedades() =>
		typeof(KafkaConsumeStep)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanWrite)
			.OrderBy(p => p.Name);
}
