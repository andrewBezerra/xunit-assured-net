using XUnitAssured.Camunda.Extensions;

using Xunit.Abstractions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// A pessoa e o relógio: a user task que alguém aprovaria e os timers que levariam horas ou dias.
/// </summary>
/// <remarks>
/// <para>
/// O <c>xa-aprovacao</c> espera 1 hora (<c>esperar-analise</c>), pede a aprovação (<c>aprovar</c>,
/// com um prazo de 3 dias na borda, <c>prazo-aprovacao</c>) e então espera a confirmação do cliente
/// ou 2 horas (<c>sem-confirmacao</c>), num gateway de evento.
/// </para>
/// <para>
/// O relógio do cluster é de todos, então o pacote não o adianta: modifica a instância. No Operate,
/// um timer pulado aparece encerrado (TERMINATED), e o caminho segue dele.
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class PessoaERelogioTests(ITestOutputHelper saida)
{
	[Fact(DisplayName = "7. Two timers skipped and a user task completed by the test, ending at 'the customer gave up'")]
	public async Task Aprovacao_Sem_Confirmacao()
	{
		await Given()
			.Deploy("xa-aprovacao")
			// O timer intermediário: encerra a espera e ativa a user task
			.SkipTimer("esperar-analise")
			// A pessoa que aprovaria no Tasklist
			.MockUserTask("aprovar").ThenComplete(new { aprovado = true })
			// O timer do gateway de evento: encerra o gateway e descarta a espera da mensagem
			.SkipTimer("sem-confirmacao")
			.StartInstance("xa-aprovacao", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.Mostrar(saida, "esperar-analise e aguardar encerrados; aprovar completada; termina em desistiu")
			.AssertInstanceCompleted()
			.AssertCompletedElements("aprovar", "desistiu")
			.AssertVariable("aprovado", true)
			.AssertUserTaskMocked("aprovar")
			// Um timer pulado fica TERMINATED, e não COMPLETED: é esta a asserção que diz que ele passou
			.AssertTimerSkipped("esperar-analise")
			.AssertTimerSkipped("sem-confirmacao");
	}

	[Fact(DisplayName = "8. Nobody approves within 3 days: the boundary timer ends the user task and takes the expiry path")]
	public async Task Aprovacao_Expirou()
	{
		await Given()
			.Deploy("xa-aprovacao")
			.SkipTimer("esperar-analise")
			// O id é o do evento de borda; a borda interrompe, então a user task é encerrada
			.SkipTimer("prazo-aprovacao")
			.StartInstance("xa-aprovacao", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.Mostrar(saida, "aprovar encerrada pela borda prazo-aprovacao; termina em expirou")
			.AssertInstanceCompleted()
			.AssertCompletedElements("expirou")
			.AssertTimerSkipped("prazo-aprovacao");
	}
}
