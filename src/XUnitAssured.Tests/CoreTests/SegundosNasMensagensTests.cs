using System;
using System.Collections.Generic;
using System.Globalization;

using Confluent.Kafka;

using Shouldly;
using Xunit;

using XUnitAssured.Kafka.Results;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// As mensagens de prazo esgotado escrevem os segundos do mesmo jeito em qualquer máquina (#166).
///
/// <para>
/// Antes, o número saía com a cultura da máquina: "1,5s" em pt-BR, "1.5s" no CI. E o Kafka
/// escrevia todos os dígitos de um prazo quebrado. Cada teste roda em pt-BR, a cultura em que o
/// problema aparece; a cultura é da thread, e volta ao fim.
/// </para>
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Results")]
public class SegundosNasMensagensTests
{
	public static TheoryData<double, string> Prazos => new()
	{
		{ 1500, "1.5" },
		{ 1234.5678, "1.23" },
		{ 30000, "30" },
	};

	[Theory(DisplayName = "On a pt-BR machine, every timeout message writes the seconds with a dot, rounded to two places")]
	[MemberData(nameof(Prazos))]
	public void Timeout_Messages_Are_The_Same_On_Every_Machine(double milissegundos, string segundos)
	{
		var prazo = TimeSpan.FromMilliseconds(milissegundos);

		var mensagens = EmPtBr(() => new[]
		{
			RabbitMqStepResult.CreateConsumeTimeout("pedidos", prazo).Errors[0],
			KafkaStepResult.CreateTimeout("pedidos", prazo).Errors[0],
			KafkaStepResult.CreateProduceTimeout("pedidos", prazo).Errors[0],
			KafkaStepResult.CreateBatchConsumePartial("pedidos", 3, new List<ConsumeResult<string, string>>(), prazo).Errors[0],
		});

		mensagens.ShouldBe(
		[
			$"No message arrived on 'pedidos' within {segundos}s.",
			$"No message consumed from topic 'pedidos' within timeout of {segundos}s",
			$"Failed to produce message to topic 'pedidos' within timeout of {segundos}s",
			$"Consumed 0 of 3 messages from topic 'pedidos' within timeout of {segundos}s",
		]);
	}

	private static T EmPtBr<T>(Func<T> acao)
	{
		var antes = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
		try
		{
			return acao();
		}
		finally
		{
			CultureInfo.CurrentCulture = antes;
		}
	}
}
