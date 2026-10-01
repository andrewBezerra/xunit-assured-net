using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Results;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "Configuration")]
/// <summary>
/// Um valor explícito ganha do contexto, mesmo quando é igual ao padrão.
///
/// <para>
/// A resolução decidia "não foi informado" comparando o valor com o literal do padrão:
/// <c>BootstrapServers != "localhost:9092"</c>. As duas coisas não são a mesma. Um projeto que
/// configure <c>localhost:9092</c> de propósito — que é justamente o endereço de um broker local
/// de desenvolvimento, portanto o valor com mais chance de ser escrito à mão — caía no caminho de
/// fallback e tinha o valor dele descartado em favor do contexto da fixture.
/// </para>
///
/// <para>
/// Os testes rodam contra um endereço na interface local sem nada escutando, com prazo curto, e
/// leem do diagnóstico da falha qual endereço o passo de fato usou. Nenhum broker é necessário, e
/// a asserção é sobre a decisão de precedência, não sobre o resultado da conexão.
/// </para>
/// </summary>
public class PrecedenciaDeConfiguracaoTests
{
	/// <summary>O literal que a resolução tratava como "ausente".</summary>
	private const string PadraoEscritoAMao = "localhost:9092";

	/// <summary>Outro endereço, também sem nada escutando, para o contexto da fixture.</summary>
	private const string EnderecoDaFixture = "127.0.0.1:59098";

	private const string GrupoPadraoEscritoAMao = "xunitassured-consumer";

	[Fact(DisplayName = "An explicit bootstrap address survives even when it equals the default")]
	public async Task Explicit_Bootstrap_Survives_When_It_Equals_The_Default()
	{
		var contexto = new ContextoComFixture();
		contexto.SetProperty("_KafkaBootstrapServers", EnderecoDaFixture);

		var passo = new KafkaConsumeStep
		{
			Topic = "precedencia",
			Timeout = TimeSpan.FromSeconds(1),
			BootstrapServers = PadraoEscritoAMao
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(contexto);

		EnderecoUsado(resultado).ShouldBe(PadraoEscritoAMao,
			"the step was told which broker to use and must not fall back to the fixture's");
	}

	[Fact(DisplayName = "An explicit group id survives even when it equals the default")]
	public async Task Explicit_Group_Survives_When_It_Equals_The_Default()
	{
		var contexto = new ContextoComFixture();
		contexto.SetProperty("_KafkaGroupId", "grupo-da-fixture");

		var passo = new KafkaConsumeStep
		{
			Topic = "precedencia",
			Timeout = TimeSpan.FromSeconds(1),
			BootstrapServers = EnderecoDaFixture,
			GroupId = GrupoPadraoEscritoAMao
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(contexto);

		GrupoUsado(resultado).ShouldBe(GrupoPadraoEscritoAMao,
			"the step was told which group to join and must not fall back to the fixture's");
	}

	// O outro lado da mesma regra, para que o conserto não troque um defeito por outro: quando
	// nada foi informado, o contexto continua valendo.
	[Fact(DisplayName = "With nothing configured, the context still wins")]
	public async Task With_Nothing_Configured_The_Context_Still_Wins()
	{
		var contexto = new ContextoComFixture();
		contexto.SetProperty("_KafkaBootstrapServers", EnderecoDaFixture);

		var passo = new KafkaConsumeStep
		{
			Topic = "precedencia",
			Timeout = TimeSpan.FromSeconds(1)
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(contexto);

		EnderecoUsado(resultado).ShouldBe(EnderecoDaFixture,
			"nothing was configured on the step, so the fixture's value is the right one");
	}

	[Fact(DisplayName = "The batch consume step resolves precedence the same way")]
	public async Task Batch_Consume_Resolves_The_Same_Way()
	{
		var contexto = new ContextoComFixture();
		contexto.SetProperty("_KafkaBootstrapServers", EnderecoDaFixture);

		var passo = new KafkaBatchConsumeStep
		{
			Topic = "precedencia",
			MessageCount = 1,
			Timeout = TimeSpan.FromSeconds(1),
			BootstrapServers = PadraoEscritoAMao
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(contexto);

		EnderecoUsado(resultado).ShouldBe(PadraoEscritoAMao);
	}

	// A armadilha que registrar a ausência cria, e que custa barato guardar: o construtor de
	// cópia tem de copiar o CAMPO e não a propriedade. Copiando a propriedade, um passo em que
	// nada foi configurado passaria a carregar o padrão como se tivesse sido informado, e o
	// contexto deixaria de ganhar depois do primeiro verbo que reconstrói o passo.
	[Fact(DisplayName = "Rebuilding a step must not turn the default into an explicit value")]
	public async Task Rebuilding_Must_Not_Turn_The_Default_Into_An_Explicit_Value()
	{
		var contexto = new ContextoComFixture();
		contexto.SetProperty("_KafkaBootstrapServers", EnderecoDaFixture);

		var semNadaConfigurado = new KafkaConsumeStep { Topic = "precedencia" };

		// É o que WithTimeout faz: reconstrói o passo pelo construtor de cópia.
		var reconstruido = new KafkaConsumeStep(semNadaConfigurado)
		{
			Timeout = TimeSpan.FromSeconds(1)
		};

		var resultado = (KafkaStepResult)await reconstruido.ExecuteAsync(contexto);

		EnderecoUsado(resultado).ShouldBe(EnderecoDaFixture,
			"nothing was ever configured, so rebuilding must not have invented an explicit value");
	}

	private static string? EnderecoUsado(KafkaStepResult resultado) =>
		resultado.GetProperty<string>("BootstrapServers");

	private static string? GrupoUsado(KafkaStepResult resultado) =>
		resultado.GetProperty<string>("GroupId");

	private sealed class ContextoComFixture : ITestContext
	{
		public IStepStorage Steps { get; } = new StepStorage();

		public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

		public T? GetProperty<T>(string key) =>
			Properties.TryGetValue(key, out var valor) && valor is T tipado ? tipado : default;

		public void SetProperty<T>(string key, T? value) => Properties[key] = value;
	}
}
